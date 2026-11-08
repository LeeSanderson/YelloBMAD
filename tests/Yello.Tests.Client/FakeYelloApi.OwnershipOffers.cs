using System.Collections.Concurrent;
using System.Text.Json;
using Yello.Contracts;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// The Ownership Offer routes on the fake: the offer, <c>POST /api/v1/spaces/{id}/ownership-offers</c> with
/// <c>{ membershipId }</c>; the Owner's read, <c>GET .../ownership-offers/pending</c>; and the revoke,
/// <c>DELETE .../ownership-offers/{offerId}</c>. No case written before the offer existed sends any of them, so every earlier
/// case is served the bytes it always was (each fell to the unmatched <c>404</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>As the Host answers them.</b> The pipeline first: a Space the switcher does not list is the boundary <c>404</c>; a
/// Role other than the Owner is the <c>403 insufficient-role</c> naming <c>offer-ownership-transfer</c>. Then the offer: a body
/// naming no Membership, or one not among the Space's <see cref="SpaceMemberships"/> other than the caller's own, is <c>400
/// invalid-offer-recipient</c>; a Space that already holds an offer is <c>409 ownership-offer-pending</c>; otherwise <c>201</c>
/// with the offer, its <c>ExpiresAt</c> <see cref="OfferLifetime"/> after <see cref="ServerNow"/> and its <c>ReadAt</c> at
/// <see cref="ServerNow"/>. The read answers the pending offer (<c>200</c>) or <c>204</c>. The revoke answers <c>204</c> for
/// the pending offer, <c>409 ownership-offer-not-pending</c> for another known id, and the boundary <c>404</c> otherwise. A key
/// already applied to the same path and body replays the answer.
/// </para>
/// <para>
/// <b>The fake's clock is the server's, never the browser's.</b> <see cref="ServerNow"/> is fixed by the case, so a case can
/// set the browser clock somewhere else entirely and see which clock the page counts from.
/// </para>
/// </remarks>
internal sealed partial class FakeYelloApi
{
    private readonly ConcurrentDictionary<string, (string Path, string Body, int Status, string Answer)> _offeredByKey = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, HashSet<Guid>> _closedOffers = new();

    /// <summary>
    /// Each Space's pending offer, as the read answers it: (offer id, recipient Membership id, recipient name, expiry, read
    /// time). Set by a case to start with an offer pending; written by the offer and cleared by the revoke.
    /// </summary>
    public ConcurrentDictionary<Guid, (Guid Id, Guid RecipientMembershipId, string RecipientName, DateTimeOffset ExpiresAt, DateTimeOffset ReadAt)> PendingOffers { get; } = new();

    /// <summary>
    /// The server's clock as the fake answers it: every offer's <c>ReadAt</c>, and the base of a new offer's <c>ExpiresAt</c>.
    /// </summary>
    public DateTimeOffset ServerNow { get; set; } = new(2026, 11, 8, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// How long a new offer stays open.
    /// </summary>
    public TimeSpan OfferLifetime { get; set; } = TimeSpan.FromDays(OwnershipOfferWire.ExpiryDays);

    /// <summary>
    /// When set, the next offer is refused with this instead of made.
    /// </summary>
    public (int Status, string ProblemType)? NextOfferRefusal { get; set; }

    /// <summary>
    /// Every offer sent in <paramref name="spaceId"/>, in order.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The requests.</returns>
    public IReadOnlyList<FakeRequest> OwnershipOffersSent(Guid spaceId) =>
        [.. _requests.Where(request => request.Method == "POST" && request.Path == OwnershipOfferRoutes.OffersOf(spaceId))];

    /// <summary>
    /// Every revoke sent in <paramref name="spaceId"/>, in order.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The requests.</returns>
    public IReadOnlyList<FakeRequest> OfferRevokes(Guid spaceId) =>
        [.. _requests.Where(request => request.Method == "DELETE" && TryOffer(request.Path, out var revokedIn, out _) && revokedIn == spaceId)];

    private (int Status, string Body)? OwnershipOffer(FakeRequest request)
    {
        var prefix = SpaceRoutes.Directory + "/";
        var segments = request.Path.StartsWith(prefix, StringComparison.Ordinal) ? request.Path[prefix.Length..].Split('/') : [];

        if (segments is not [var space, "ownership-offers", ..] || !Guid.TryParse(space, out var spaceId))
        {
            return null;
        }

        return (request.Method, segments) switch
        {
            ("POST", [_, _]) => Offer(spaceId, request),
            ("GET", [_, _, "pending"]) => RefusedOutsideTheOwner(spaceId) ?? ReadPending(spaceId),
            ("DELETE", [_, _, var offer]) when Guid.TryParse(offer, out var offerId) => RefusedOutsideTheOwner(spaceId) ?? Revoke(spaceId, offerId),
            _ => null,
        };
    }

    private (int Status, string Body) Offer(Guid spaceId, FakeRequest request)
    {
        if (RefusedOutsideTheOwner(spaceId) is { } refused)
        {
            return refused;
        }

        var key = request.Header(Idempotency.HeaderName);
        var body = request.Body ?? string.Empty;

        if (key is not null && _offeredByKey.TryGetValue(key, out var earlier))
        {
            return earlier.Path == request.Path && earlier.Body == body
                ? (earlier.Status, earlier.Answer)
                : (422, Problem(ProblemTypes.IdempotencyKeyReused));
        }

        var (status, answer) = Decide(spaceId, body);

        if (status == 201 && key is not null)
        {
            _offeredByKey[key] = (request.Path, body, status, answer);
        }

        return (status, answer);
    }

    private (int Status, string Body) Decide(Guid spaceId, string body)
    {
        if (NextOfferRefusal is { } refusal)
        {
            NextOfferRefusal = null;

            return (refusal.Status, Problem(refusal.ProblemType));
        }

        var listed = SpaceMemberships.TryGetValue(spaceId, out var rows) ? rows : [];
        var recipient = TryOfferedMembership(body, out var membershipId)
            ? listed.Find(row => row.Id == membershipId && !row.IsYou && row.Role != "owner")
            : null;

        if (recipient is null)
        {
            return (400, Problem(ProblemTypes.InvalidOfferRecipient));
        }

        if (PendingOffers.ContainsKey(spaceId))
        {
            return (409, Problem(ProblemTypes.OwnershipOfferPending));
        }

        var offer = (Id: Guid.NewGuid(), RecipientMembershipId: recipient.Id, RecipientName: recipient.DisplayName, ExpiresAt: ServerNow + OfferLifetime, ReadAt: ServerNow);

        PendingOffers[spaceId] = offer;

        return (201, Serialize(offer));
    }

    private (int Status, string Body) ReadPending(Guid spaceId) =>
        PendingOffers.TryGetValue(spaceId, out var offer)
            ? (200, Serialize((offer.Id, offer.RecipientMembershipId, offer.RecipientName, offer.ExpiresAt, ServerNow)))
            : (204, string.Empty);

    private (int Status, string Body) Revoke(Guid spaceId, Guid offerId)
    {
        if (PendingOffers.TryGetValue(spaceId, out var offer) && offer.Id == offerId)
        {
            PendingOffers.TryRemove(spaceId, out _);
            _closedOffers.GetOrAdd(spaceId, _ => []).Add(offerId);

            return (204, string.Empty);
        }

        return _closedOffers.TryGetValue(spaceId, out var closed) && closed.Contains(offerId)
            ? (409, Problem(ProblemTypes.OwnershipOfferNotPending))
            : (404, Problem(ProblemTypes.NotAvailable));
    }

    /// <summary>
    /// The pipeline's answers before the slice runs: an unlisted Space is the boundary <c>404</c>; anyone but the Owner the
    /// <c>403</c> naming the capability.
    /// </summary>
    private (int Status, string Body)? RefusedOutsideTheOwner(Guid spaceId)
    {
        if (!Switcher.Any(space => space.Id == spaceId))
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        return Roles.TryGetValue(spaceId, out var role) && role == "owner"
            ? null
            : (403, JsonSerializer.Serialize(new ProblemResponse(ProblemTypes.InsufficientRole, Errors: null, Capability: OwnershipOfferWire.CapabilityCode), JsonOptions));
    }

    private static string Serialize((Guid Id, Guid RecipientMembershipId, string RecipientName, DateTimeOffset ExpiresAt, DateTimeOffset ReadAt) offer) =>
        JsonSerializer.Serialize(
            new { id = offer.Id, recipientMembershipId = offer.RecipientMembershipId, recipientName = offer.RecipientName, expiresAt = offer.ExpiresAt, readAt = offer.ReadAt },
            JsonOptions);

    /// <summary>
    /// Reads <c>{ membershipId }</c> as a GUID. Not an object, no such member, or anything but a GUID string is no Membership.
    /// </summary>
    private static bool TryOfferedMembership(string body, out Guid membershipId)
    {
        membershipId = Guid.Empty;

        try
        {
            using var document = JsonDocument.Parse(body.Length == 0 ? "null" : body);

            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("membershipId", out var value)
                && value.ValueKind == JsonValueKind.String
                && value.TryGetGuid(out membershipId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Whether <paramref name="path"/> is <c>/api/v1/spaces/{s}/ownership-offers/{o}</c>.
    /// </summary>
    private static bool TryOffer(string path, out Guid spaceId, out Guid offerId)
    {
        var prefix = SpaceRoutes.Directory + "/";
        var segments = path.StartsWith(prefix, StringComparison.Ordinal) ? path[prefix.Length..].Split('/') : [];

        spaceId = Guid.Empty;
        offerId = Guid.Empty;

        return segments is [var space, "ownership-offers", var offer]
            && Guid.TryParse(space, out spaceId)
            && Guid.TryParse(offer, out offerId);
    }
}
