using System.Collections.Concurrent;
using System.Text.Json;
using Yello.Contracts;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// The recipient's side of an Ownership Offer on the fake: the read, <c>GET /api/v1/spaces/{id}/ownership-offers/mine</c>;
/// the acceptance, <c>POST .../ownership-offers/{offerId}/acceptance</c>; and the decline,
/// <c>POST .../ownership-offers/{offerId}/decline</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>As the Host answers them.</b> A Space the switcher does not list is the boundary <c>404</c>. No Role is refused: all
/// three are decided by identity. The read answers the offer naming this Account (<see cref="OffersForMe"/>) with
/// <c>ReadAt</c> at <see cref="ServerNow"/>, or <c>204</c>. The acceptance and the decline answer <c>204</c> for that offer
/// and remove it; <c>409 ownership-offer-not-pending</c> for an offer already answered; the in-Space <c>404</c> otherwise. A
/// key already applied to the same path replays the answer; on another path it is <c>422</c>.
/// </para>
/// <para>
/// <b>The Role moves after the acceptance's own answer.</b> The Host's <c>Yello-Role</c> header on the acceptance's
/// <c>204</c> echoes the Role judged at request start, so the fake makes the Account the Owner only from the next request
/// on (<see cref="ApplyAnsweredOfferRoles"/>, called first in <c>DecideAsync</c>). With no acceptance it does nothing, so
/// every earlier case is served the bytes it always was.
/// </para>
/// <para>
/// The routes are <see cref="OwnershipOfferWire"/>'s stand-ins until <c>OwnershipOfferRoutes</c> declares them.
/// </para>
/// </remarks>
internal sealed partial class FakeYelloApi
{
    private readonly ConcurrentDictionary<string, (string Path, int Status, string Answer)> _answeredByKey = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, HashSet<Guid>> _answeredOffers = new();
    private readonly ConcurrentDictionary<Guid, string> _roleAfterAnswer = new();

    /// <summary>
    /// The actionable offer naming this Account in each Space, as the recipient's read answers it: (offer id, the offerer's
    /// captured name, expiry). Set by a case; removed by an acceptance or a decline.
    /// </summary>
    public ConcurrentDictionary<Guid, (Guid Id, string OffererName, DateTimeOffset ExpiresAt)> OffersForMe { get; } = new();

    /// <summary>
    /// When set, the next acceptance is refused with this instead of applied. A refusal of
    /// <c>ownership-offer-not-pending</c> or <c>owner-role-fixed</c> also removes the offer naming this Account, as the Host's
    /// next read would find none.
    /// </summary>
    public (int Status, string ProblemType)? NextOfferAcceptRefusal { get; set; }

    /// <summary>
    /// When set, the next acceptance is applied and its answer then lost (a connection reset), so the client's resend must
    /// carry the same key and is answered by the replay.
    /// </summary>
    public bool LoseNextAcceptAnswer { get; set; }

    /// <summary>
    /// Every recipient's read sent in <paramref name="spaceId"/>, in order.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The requests.</returns>
    public IReadOnlyList<FakeRequest> MyOfferReads(Guid spaceId) =>
        [.. _requests.Where(request => request.Method == "GET" && request.Path == OwnershipOfferWire.MineOf(spaceId))];

    /// <summary>
    /// Every acceptance sent in <paramref name="spaceId"/>, in order.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The requests.</returns>
    public IReadOnlyList<FakeRequest> AcceptancesSent(Guid spaceId) =>
        [.. _requests.Where(request => request.Method == "POST" && request.Path.StartsWith(OwnershipOfferRoutes.OffersOf(spaceId) + "/", StringComparison.Ordinal) && request.Path.EndsWith("/acceptance", StringComparison.Ordinal))];

    /// <summary>
    /// Every decline sent in <paramref name="spaceId"/>, in order.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The requests.</returns>
    public IReadOnlyList<FakeRequest> DeclinesSent(Guid spaceId) =>
        [.. _requests.Where(request => request.Method == "POST" && request.Path.StartsWith(OwnershipOfferRoutes.OffersOf(spaceId) + "/", StringComparison.Ordinal) && request.Path.EndsWith("/decline", StringComparison.Ordinal))];

    /// <summary>
    /// Every arrival read of <paramref name="spaceId"/> (<c>GET /api/v1/spaces/{id}</c>), in order.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The requests.</returns>
    public IReadOnlyList<FakeRequest> SpaceArrivalReads(Guid spaceId) =>
        [.. _requests.Where(request => request.Method == "GET" && request.Path == SpaceRoutes.ActiveOf(spaceId))];

    /// <summary>
    /// Applies the Role an acceptance moved, from the request after its answer on.
    /// </summary>
    private void ApplyAnsweredOfferRoles()
    {
        foreach (var spaceId in _roleAfterAnswer.Keys)
        {
            if (_roleAfterAnswer.TryRemove(spaceId, out var role))
            {
                Roles[spaceId] = role;
            }
        }
    }

    private (int Status, string Body) ReadMine(Guid spaceId)
    {
        if (!Switcher.Any(space => space.Id == spaceId))
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        return OffersForMe.TryGetValue(spaceId, out var offer)
            ? (200, JsonSerializer.Serialize(new { id = offer.Id, offererName = offer.OffererName, expiresAt = offer.ExpiresAt, readAt = ServerNow }, JsonOptions))
            : (204, string.Empty);
    }

    private (int Status, string Body) AnswerOffer(Guid spaceId, Guid offerId, FakeRequest request, bool accept)
    {
        if (!Switcher.Any(space => space.Id == spaceId))
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        var key = request.Header(Idempotency.HeaderName);

        if (key is not null && _answeredByKey.TryGetValue(key, out var earlier))
        {
            return earlier.Path == request.Path ? (earlier.Status, earlier.Answer) : (422, Problem(ProblemTypes.IdempotencyKeyReused));
        }

        if (accept && TakeAcceptRefusal(spaceId) is { } refused)
        {
            return refused;
        }

        if (OffersForMe.TryGetValue(spaceId, out var offer) && offer.Id == offerId)
        {
            return Answered(spaceId, offerId, request.Path, key, accept);
        }

        return _answeredOffers.TryGetValue(spaceId, out var answered) && answered.Contains(offerId)
            ? (409, Problem(ProblemTypes.OwnershipOfferNotPending))
            : (404, Problem(ProblemTypes.NotAvailable));
    }

    /// <summary>
    /// The refusal a case set for the next acceptance, once; a refusal that closes the offer removes it.
    /// </summary>
    private (int Status, string Body)? TakeAcceptRefusal(Guid spaceId)
    {
        if (NextOfferAcceptRefusal is not { } refusal)
        {
            return null;
        }

        NextOfferAcceptRefusal = null;

        if (refusal.ProblemType == ProblemTypes.OwnershipOfferNotPending || refusal.ProblemType == ProblemTypes.OwnerRoleFixed)
        {
            OffersForMe.TryRemove(spaceId, out _);
        }

        return (refusal.Status, Problem(refusal.ProblemType));
    }

    /// <summary>
    /// Applies an acceptance or a decline of the offer naming this Account: the offer is gone, an acceptance moves the Role
    /// from the next request on, and the key replays the <c>204</c>.
    /// </summary>
    private (int Status, string Body) Answered(Guid spaceId, Guid offerId, string path, string? key, bool accept)
    {
        OffersForMe.TryRemove(spaceId, out _);
        _answeredOffers.GetOrAdd(spaceId, _ => []).Add(offerId);

        if (key is not null)
        {
            _answeredByKey[key] = (path, 204, string.Empty);
        }

        if (!accept)
        {
            return (204, string.Empty);
        }

        _roleAfterAnswer[spaceId] = "owner";

        if (LoseNextAcceptAnswer)
        {
            LoseNextAcceptAnswer = false;

            return (AbortConnection, string.Empty);
        }

        return (204, string.Empty);
    }
}