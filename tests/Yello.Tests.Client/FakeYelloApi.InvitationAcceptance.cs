using System.Collections.Concurrent;
using System.Text.Json;
using Yello.Contracts;
using Yello.Contracts.Accounts;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// Story 4.3's routes on the fake (ATDD run, 2026-11-01): the Invitation view, <c>GET /api/v1/invitations/offer</c> with the
/// token in the <c>Yello-Invitation-Token</c> header, and the accept, <c>POST /api/v1/invitations/acceptance</c> with
/// <c>{ invitationToken }</c> (Lee's Q4 (a)); and registration, <c>POST /api/v1/accounts</c>, which the view's "Create an
/// Account and join" chains before sign-in and the accept (Lee's Q3 (a)). No case written before 4.3 sends any of the
/// three, so every earlier case is served the bytes it always was (all three fell to the unmatched <c>404</c> before).
/// </summary>
/// <remarks>
/// <para>
/// <b>As the Host answers them under Lee's decisions.</b> The view answers <see cref="Offers"/>' entry for the token -
/// with <c>addressedToYou</c> <c>null</c> when no Session is held and <see cref="AddresseeSignedIn"/> when one is (Q5
/// (a)) - or the uniform <c>404 invitation-not-valid</c> for any other token (Q6 (a)). The accept needs a Session
/// (<c>401</c> otherwise, Q7 (a)); a key already applied replays its <c>200</c>; an accepted or unknown token is the
/// uniform <c>404</c>; a Session that is not the addressee's is <c>403 invitation-for-another-account</c>; otherwise it
/// answers <c>200 { spaceId }</c> (Q10 (a)), lists the Space in the switcher at the offered Role (as the Host's next
/// switcher read would), and the token is spent. A held accept waits for its release; <see cref="NextAcceptRefusal"/>
/// refuses the next one (the bound's <c>409</c> carries <c>bound</c> and <c>limit</c>).
/// </para>
/// <para>
/// <b>Paths and types are <see cref="InvitationWire"/>'s temporary stand-ins</b> until Task 3 declares them in
/// <c>Yello.Contracts</c>; the dev phase swaps them, as it swaps them in the wire suites (4.1 review P13: no literals here).
/// </para>
/// </remarks>
internal sealed partial class FakeYelloApi
{
    private readonly ConcurrentDictionary<string, Guid> _acceptedByKey = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _spentTokens = new(StringComparer.Ordinal);

    private DeleteHold? _acceptHold;
    private DeleteHold? _lastAcceptHold;

    /// <summary>
    /// Each live token's offer and the Space it opens. Seed it before the page opens.
    /// </summary>
    public ConcurrentDictionary<string, (OfferedInvitation Offer, Guid SpaceId)> Offers { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Whether the signed-in Account is the one the Invitation names (the view's <c>addressedToYou</c> with a Session, and
    /// whether the accept is admitted). Story 4.3.
    /// </summary>
    public bool AddresseeSignedIn { get; set; } = true;

    /// <summary>
    /// When set, the next accept is refused with this instead of applied: a status, a problem type, and for the bound's
    /// <c>409</c> its code and limit (or <c>null</c>).
    /// </summary>
    public (int Status, string ProblemType, string? Bound, int? Limit)? NextAcceptRefusal { get; set; }

    /// <summary>
    /// Every view read the page sent, in order, each with its headers.
    /// </summary>
    public IReadOnlyList<FakeRequest> InvitationOfferReads =>
        [.. _requests.Where(request => request.Method == "GET" && request.Path == InvitationWire.OfferRoute)];

    /// <summary>
    /// Every accept the page sent, in order, each with its body and headers.
    /// </summary>
    public IReadOnlyList<FakeRequest> InvitationAccepts =>
        [.. _requests.Where(request => request.Method == "POST" && request.Path == InvitationWire.AcceptanceRoute)];

    /// <summary>
    /// Holds the <b>next</b> accept until it is released.
    /// </summary>
    /// <returns>A task that completes when the held accept has arrived and is waiting.</returns>
    public Task HoldAcceptAsync()
    {
        var hold = new DeleteHold();

        _acceptHold = hold;
        _lastAcceptHold = hold;

        return hold.Arrived.Task;
    }

    /// <summary>
    /// Lets the held accept go and applies it: <c>200</c>.
    /// </summary>
    public void ReleaseAccept() => _lastAcceptHold?.Released.TrySetResult(TaskEditOutcome.Admitted);

    /// <summary>
    /// Lets the held accept go with no answer - the connection reset. Nothing is accepted, so a resend under the same key
    /// is applied.
    /// </summary>
    public void ReleaseAcceptUnanswered() =>
        _lastAcceptHold?.Released.TrySetResult(new TaskEditOutcome(0, string.Empty, Capability: null, Abort: true));

    /// <summary>
    /// Story 4.3's answers, consulted before the main routes: <c>null</c> when the request is none of the three.
    /// </summary>
    private async Task<(int Status, string Body)?> InvitationAcceptanceAsync(FakeRequest request) =>
        (request.Method, request.Path) switch
        {
            ("GET", var path) when path == InvitationWire.OfferRoute => Offer(request),
            ("POST", var path) when path == InvitationWire.AcceptanceRoute => await AcceptInvitationAsync(request).ConfigureAwait(false),
            ("POST", var path) when path == AccountRoutes.Register => (204, string.Empty),
            _ => null,
        };

    private (int Status, string Body) Offer(FakeRequest request)
    {
        var token = request.Header(InvitationWire.TokenHeaderName) ?? string.Empty;

        if (_spentTokens.ContainsKey(token) || !Offers.TryGetValue(token, out var offered))
        {
            return (404, Problem(InvitationWire.NotValidProblemType));
        }

        return (200, JsonSerializer.Serialize(offered.Offer with { AddressedToYou = SignedIn ? AddresseeSignedIn : null }, JsonOptions));
    }

    private async Task<(int Status, string Body)> AcceptInvitationAsync(FakeRequest request)
    {
        if (!SignedIn)
        {
            return (401, Problem(ProblemTypes.AuthenticationFailed));
        }

        var key = request.Header(Idempotency.HeaderName);

        if (key is not null && _acceptedByKey.TryGetValue(key, out var replayed))
        {
            return (200, JsonSerializer.Serialize(new { spaceId = replayed }, JsonOptions));
        }

        var outcome = await DeleteOutcomeAsync(() => Interlocked.Exchange(ref _acceptHold, null), () => null).ConfigureAwait(false);

        if (outcome.Abort)
        {
            return (AbortConnection, string.Empty);
        }

        if (NextAcceptRefusal is { } refusal)
        {
            NextAcceptRefusal = null;

            return (refusal.Status, JsonSerializer.Serialize(new ProblemResponse(refusal.ProblemType, Errors: null, Capability: null, refusal.Bound, refusal.Limit), JsonOptions));
        }

        var token = MemberOf(request.Body ?? string.Empty, "invitationToken") ?? string.Empty;

        if (_spentTokens.ContainsKey(token) || !Offers.TryGetValue(token, out var offered))
        {
            return (404, Problem(InvitationWire.NotValidProblemType));
        }

        if (!AddresseeSignedIn)
        {
            return (403, Problem(InvitationWire.ForAnotherAccountProblemType));
        }

        _spentTokens[token] = 0;
        Switcher.Add((offered.SpaceId, offered.Offer.SpaceName));
        Roles[offered.SpaceId] = offered.Offer.Role;

        if (key is not null)
        {
            _acceptedByKey[key] = offered.SpaceId;
        }

        return (200, JsonSerializer.Serialize(new { spaceId = offered.SpaceId }, JsonOptions));
    }
}
