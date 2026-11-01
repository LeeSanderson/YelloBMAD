using System.Collections.Concurrent;
using System.Text.Json;
using Yello.Contracts;
using Yello.Contracts.Invitations;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// Story 4.1's route on the fake (ATDD run, 2026-11-01): issuing an Invitation,
/// <c>POST /api/v1/spaces/{id}/invitations</c>, held and released like 2.5's and 3.3's deletions. Story 4.2's routes (ATDD
/// run, 2026-11-01): the list, <c>GET</c> on the same path, and the revoke, <c>DELETE .../invitations/{invitationId}</c>.
/// No case written before 4.2 sends the list or the revoke, so every earlier case is served the bytes it always was (both
/// fell to the unmatched <c>404</c> before), and every other unmatched route keeps that answer.
/// </summary>
/// <remarks>
/// <para>
/// <b>The issue as the Host answers it under the story's recommendations.</b> Q5 (a): success is <c>204</c> with no body
/// and no <c>Location</c>. A key already applied with the same body replays the <c>204</c>; the same key on another body is
/// <c>422 idempotency-key-reused</c>. A held issue waits for its release (applied, refused or unanswered); a queued
/// refusal (<see cref="NextInvitationRefusal"/>) applies nothing and answers whatever status and problem type the case
/// set - <c>409 already-a-member</c>, <c>409 invitation-pending</c>, <c>400 role-not-offerable</c>, <c>403</c>,
/// <c>404</c>. Otherwise an unlisted Space is the boundary <c>404</c>, a Role other than Owner or Admin is
/// <c>403 insufficient-role</c> naming <c>issue-and-revoke-invitations</c> (the pipeline's answer), and a Role code other
/// than <c>admin</c>, <c>member</c> or <c>viewer</c> - <c>owner</c> above all - is <c>400 role-not-offerable</c>, as AC2's
/// server refuses it whatever the client offered.
/// </para>
/// <para>
/// <b>The list and the revoke as story 4.2 recommends them.</b> The list answers <see cref="PendingInvitations"/> for the
/// Space, in the order seeded (the case seeds newest first), as <see cref="ListedInvitation"/> rows - the Q5 (a) shape - or
/// <c>[]</c>; it can be held (<see cref="HoldInvitationListAsync"/>) to show what renders before the server answers. The
/// revoke answers <c>204</c> and removes the row; an id already revoked here is <c>409 invitation-not-pending</c> (Q2 (a));
/// an id the Space never listed is the boundary <c>404</c> (Q3 (a)); a key already applied to the same path replays the
/// <c>204</c>. Both refuse a Role other than Owner or Admin with the pipeline's <c>403</c>, and an unlisted Space with the
/// boundary <c>404</c>. A held revoke waits for its release; <see cref="NextRevokeRefusal"/> refuses the next one.
/// <b>An issued Invitation joins the list only when <see cref="ListIssuedInvitations"/> is set</b>, so 4.1's cases - which
/// assert the address appears nowhere on the page after a <c>204</c> - keep the page they were written against.
/// </para>
/// <para>
/// <b>Contract constants, never literals</b> (4.1 review P13): paths come from <see cref="InvitationRoutes"/> and, until
/// story 4.2 Task 2 declares <c>InvitationRoutes.InvitationOf</c>, from <see cref="InvitationWire.RevokePathOf"/>; the
/// <c>409</c>'s type from <see cref="InvitationWire.NotPendingProblemType"/> until <c>ProblemTypes.InvitationNotPending</c>
/// exists. The dev phase swaps both.
/// </para>
/// <para>
/// <b>Every request is recorded</b> (the body text and its headers, the <c>Idempotency-Key</c> among them) by
/// <see cref="FakeYelloApi"/>'s router; <see cref="InvitationsIssued"/>, <see cref="InvitationListReads"/> and
/// <see cref="InvitationRevokes"/> read them back for one Space.
/// </para>
/// </remarks>
internal sealed partial class FakeYelloApi
{
    private readonly ConcurrentDictionary<string, string> _invitedByKey = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _revokedByKey = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, byte> _revokedInvitations = new();
    private readonly Lock _pendingInvitationsLock = new();

    private DeleteHold? _invitationHold;
    private DeleteHold? _lastInvitationHold;
    private DeleteHold? _revokeHold;
    private DeleteHold? _lastRevokeHold;
    private DeleteHold? _listHold;
    private DeleteHold? _lastListHold;

    /// <summary>
    /// When set, the next Invitation issue is refused with this instead of applied: a status, a problem type and the
    /// capability a <c>403</c> names (or <c>null</c>).
    /// </summary>
    public (int Status, string ProblemType, string? Capability)? NextInvitationRefusal { get; set; }

    /// <summary>
    /// When set, the next revoke is refused with this instead of applied. Story 4.2.
    /// </summary>
    public (int Status, string ProblemType, string? Capability)? NextRevokeRefusal { get; set; }

    /// <summary>
    /// Each Space's pending Invitations as the list answers them, newest first. Story 4.2. Seed it before the page opens;
    /// change it while a page is open only through <see cref="WithdrawInvitation"/>.
    /// </summary>
    public ConcurrentDictionary<Guid, List<ListedInvitation>> PendingInvitations { get; } = new();

    /// <summary>
    /// Whether an issue the fake applies joins <see cref="PendingInvitations"/> at the top, as the Host's next list would
    /// show it. Off by default (see the remarks). Story 4.2.
    /// </summary>
    public bool ListIssuedInvitations { get; set; }

    /// <summary>
    /// The issuer name an issued Invitation is listed with when <see cref="ListIssuedInvitations"/> is set. Story 4.2.
    /// </summary>
    public string InvitationIssuerName { get; set; } = "Priya Raman";

    /// <summary>
    /// Takes an Invitation off the list as the server would once it is no longer pending - accepted or revoked elsewhere -
    /// so the next read omits it. Story 4.2.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="invitationId">The Invitation.</param>
    public void WithdrawInvitation(Guid spaceId, Guid invitationId) =>
        _ = PendingInvitations.TryGetValue(spaceId, out var listed) && RemoveListed(listed, invitationId);

    /// <summary>
    /// The issue route for one Space, as the contract names it, so a contract change breaks the fake as well as the Host.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The path.</returns>
    public static string InvitationsPathOf(Guid spaceId) =>
        InvitationRoutes.InvitationsOf(spaceId);

    /// <summary>
    /// One Invitation's path - the revoke's - as story 4.2 Task 2 will name it. Story 4.2.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="invitationId">The Invitation.</param>
    /// <returns>The path.</returns>
    public static string InvitationPathOf(Guid spaceId, Guid invitationId) =>
        InvitationWire.RevokePathOf(spaceId, invitationId);

    /// <summary>
    /// Every Invitation issue in <paramref name="spaceId"/> the page sent, in order, each with its body text and headers.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The requests.</returns>
    public IReadOnlyList<FakeRequest> InvitationsIssued(Guid spaceId) =>
        [.. _requests.Where(request => request.Method == "POST" && request.Path == InvitationsPathOf(spaceId))];

    /// <summary>
    /// Every read of <paramref name="spaceId"/>'s Invitation list the page sent, in order. Story 4.2.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The requests.</returns>
    public IReadOnlyList<FakeRequest> InvitationListReads(Guid spaceId) =>
        [.. _requests.Where(request => request.Method == "GET" && request.Path == InvitationsPathOf(spaceId))];

    /// <summary>
    /// Every revoke in <paramref name="spaceId"/> the page sent, in order, each with its headers. Story 4.2.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The requests.</returns>
    public IReadOnlyList<FakeRequest> InvitationRevokes(Guid spaceId) =>
        [.. _requests.Where(request => request.Method == "DELETE" && TryInvitation(request.Path, out var revokedIn, out _) && revokedIn == spaceId)];

    /// <summary>
    /// Holds the <b>next</b> Invitation issue until it is released.
    /// </summary>
    /// <returns>A task that completes when the held issue has arrived and is waiting.</returns>
    public Task HoldInvitationAsync()
    {
        var hold = new DeleteHold();

        _invitationHold = hold;
        _lastInvitationHold = hold;

        return hold.Arrived.Task;
    }

    /// <summary>
    /// Lets the held issue go and applies it: <c>204</c>.
    /// </summary>
    public void ReleaseInvitation() => _lastInvitationHold?.Released.TrySetResult(TaskEditOutcome.Deleted);

    /// <summary>
    /// Lets the held issue go with a refusal or a server failure instead: nothing is issued.
    /// </summary>
    /// <param name="status">The status code.</param>
    /// <param name="problemType">The problem type.</param>
    /// <param name="capability">The capability a <c>403</c> names, or <c>null</c>.</param>
    public void ReleaseInvitationRefused(int status, string problemType, string? capability = null) =>
        _lastInvitationHold?.Released.TrySetResult(new TaskEditOutcome(status, problemType, capability, Abort: false));

    /// <summary>
    /// Lets the held issue go with no answer - the connection reset. Nothing is issued, so a resend under the same key is
    /// applied.
    /// </summary>
    public void ReleaseInvitationUnanswered() =>
        _lastInvitationHold?.Released.TrySetResult(new TaskEditOutcome(0, string.Empty, Capability: null, Abort: true));

    /// <summary>
    /// Holds the <b>next</b> revoke until it is released. Story 4.2.
    /// </summary>
    /// <returns>A task that completes when the held revoke has arrived and is waiting.</returns>
    public Task HoldRevokeAsync()
    {
        var hold = new DeleteHold();

        _revokeHold = hold;
        _lastRevokeHold = hold;

        return hold.Arrived.Task;
    }

    /// <summary>
    /// Lets the held revoke go and applies it: <c>204</c>. Story 4.2.
    /// </summary>
    public void ReleaseRevoke() => _lastRevokeHold?.Released.TrySetResult(TaskEditOutcome.Deleted);

    /// <summary>
    /// Lets the held revoke go with a refusal or a server failure instead: nothing is revoked. Story 4.2.
    /// </summary>
    /// <param name="status">The status code.</param>
    /// <param name="problemType">The problem type.</param>
    /// <param name="capability">The capability a <c>403</c> names, or <c>null</c>.</param>
    public void ReleaseRevokeRefused(int status, string problemType, string? capability = null) =>
        _lastRevokeHold?.Released.TrySetResult(new TaskEditOutcome(status, problemType, capability, Abort: false));

    /// <summary>
    /// Lets the held revoke go with no answer - the connection reset. Story 4.2.
    /// </summary>
    public void ReleaseRevokeUnanswered() =>
        _lastRevokeHold?.Released.TrySetResult(new TaskEditOutcome(0, string.Empty, Capability: null, Abort: true));

    /// <summary>
    /// Holds the <b>next</b> list read until it is released, to show what the page renders before the server answers
    /// (UX-DR31). Story 4.2.
    /// </summary>
    /// <returns>A task that completes when the held read has arrived and is waiting.</returns>
    public Task HoldInvitationListAsync()
    {
        var hold = new DeleteHold();

        _listHold = hold;
        _lastListHold = hold;

        return hold.Arrived.Task;
    }

    /// <summary>
    /// Lets the held list read go, answered as the list stands then. Story 4.2.
    /// </summary>
    public void ReleaseInvitationList() => _lastListHold?.Released.TrySetResult(TaskEditOutcome.Admitted);

    /// <summary>
    /// Stories 4.1 and 4.2's answers, consulted before the main routes: <c>null</c> when the request is none of the
    /// Invitation routes.
    /// </summary>
    private async Task<(int Status, string Body)?> InvitationAsync(FakeRequest request)
    {
        if (TryInvitations(request.Path, out var spaceId))
        {
            return request.Method switch
            {
                "POST" => await IssueInvitationAsync(spaceId, request).ConfigureAwait(false),
                "GET" => await ListInvitationsAsync(spaceId).ConfigureAwait(false),
                _ => null,
            };
        }

        return request.Method == "DELETE" && TryInvitation(request.Path, out var revokedIn, out var invitationId)
            ? await RevokeInvitationAsync(revokedIn, invitationId, request).ConfigureAwait(false)
            : null;
    }

    private async Task<(int Status, string Body)> IssueInvitationAsync(Guid spaceId, FakeRequest request)
    {
        var key = request.Header(Idempotency.HeaderName);
        var body = request.Body ?? string.Empty;

        if (key is not null && _invitedByKey.TryGetValue(key, out var applied))
        {
            return string.Equals(applied, body, StringComparison.Ordinal)
                ? (204, string.Empty)
                : (422, Problem(ProblemTypes.IdempotencyKeyReused));
        }

        var outcome = await DeleteOutcomeAsync(() => Interlocked.Exchange(ref _invitationHold, null), () =>
        {
            var refusal = NextInvitationRefusal;

            NextInvitationRefusal = null;
            return refusal;
        }).ConfigureAwait(false);

        if (outcome.Abort)
        {
            return (AbortConnection, string.Empty);
        }

        if (outcome.Status != 204)
        {
            return (outcome.Status, JsonSerializer.Serialize(new ProblemResponse(outcome.ProblemType, Errors: null, Capability: outcome.Capability), JsonOptions));
        }

        if (InvitationRefusal(spaceId) is { } refused)
        {
            return refused;
        }

        if (MemberOf(body, "role") is not ("admin" or "member" or "viewer"))
        {
            return (400, Problem(ProblemTypes.RoleNotOfferable));
        }

        if (key is not null)
        {
            _invitedByKey[key] = body;
        }

        if (ListIssuedInvitations)
        {
            var listed = PendingInvitations.GetOrAdd(spaceId, _ => []);

            lock (_pendingInvitationsLock)
            {
                listed.Insert(0, new ListedInvitation(Guid.NewGuid(), (MemberOf(body, "emailAddress") ?? string.Empty).Trim(), MemberOf(body, "role")!, InvitationIssuerName, DateTimeOffset.UtcNow));
            }
        }

        return (204, string.Empty);
    }

    private async Task<(int Status, string Body)> ListInvitationsAsync(Guid spaceId)
    {
        if (Interlocked.Exchange(ref _listHold, null) is { } hold)
        {
            hold.Arrived.TrySetResult();
            _ = await hold.Released.Task.ConfigureAwait(false);
        }

        if (InvitationRefusal(spaceId) is { } refused)
        {
            return refused;
        }

        ListedInvitation[] rows = [];

        if (PendingInvitations.TryGetValue(spaceId, out var listed))
        {
            lock (_pendingInvitationsLock)
            {
                rows = [.. listed];
            }
        }

        return (200, JsonSerializer.Serialize(rows, JsonOptions));
    }

    private async Task<(int Status, string Body)> RevokeInvitationAsync(Guid spaceId, Guid invitationId, FakeRequest request)
    {
        var key = request.Header(Idempotency.HeaderName);

        if (key is not null && _revokedByKey.TryGetValue(key, out var appliedTo))
        {
            return string.Equals(appliedTo, request.Path, StringComparison.Ordinal)
                ? (204, string.Empty)
                : (422, Problem(ProblemTypes.IdempotencyKeyReused));
        }

        var outcome = await DeleteOutcomeAsync(() => Interlocked.Exchange(ref _revokeHold, null), () =>
        {
            var refusal = NextRevokeRefusal;

            NextRevokeRefusal = null;
            return refusal;
        }).ConfigureAwait(false);

        if (outcome.Abort)
        {
            return (AbortConnection, string.Empty);
        }

        if (outcome.Status != 204)
        {
            return (outcome.Status, JsonSerializer.Serialize(new ProblemResponse(outcome.ProblemType, Errors: null, Capability: outcome.Capability), JsonOptions));
        }

        if (InvitationRefusal(spaceId) is { } refused)
        {
            return refused;
        }

        if (_revokedInvitations.ContainsKey(invitationId))
        {
            return (409, Problem(InvitationWire.NotPendingProblemType));
        }

        var removed = PendingInvitations.TryGetValue(spaceId, out var listed) && RemoveListed(listed, invitationId);

        if (!removed)
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        _revokedInvitations[invitationId] = 0;

        if (key is not null)
        {
            _revokedByKey[key] = request.Path;
        }

        return (204, string.Empty);
    }

    /// <summary>
    /// The pipeline's answer before any Invitation route runs: the boundary <c>404</c> for a Space not listed, and the
    /// <c>403</c> naming <c>issue-and-revoke-invitations</c> for a Role other than Owner or Admin (the 3.3 code-review
    /// precedent: a client that sent the request for a lesser Role meets the refusal here as it would there); <c>null</c>
    /// when the request passes.
    /// </summary>
    private (int Status, string Body)? InvitationRefusal(Guid spaceId)
    {
        if (Switcher.All(space => space.Id != spaceId) || !Roles.TryGetValue(spaceId, out var role))
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        return role is "owner" or "admin"
            ? null
            : (403, JsonSerializer.Serialize(new ProblemResponse(ProblemTypes.InsufficientRole, Errors: null, Capability: "issue-and-revoke-invitations"), JsonOptions));
    }

    private bool RemoveListed(List<ListedInvitation> listed, Guid invitationId)
    {
        lock (_pendingInvitationsLock)
        {
            return listed.RemoveAll(row => row.Id == invitationId) > 0;
        }
    }

    /// <summary>
    /// A string member of an issue body, or <c>null</c> when the body is unreadable or carries no such string.
    /// </summary>
    private static string? MemberOf(string body, string name)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrEmpty(body) ? "{}" : body);

            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(name, out var member)
                && member.ValueKind == JsonValueKind.String
                    ? member.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether <paramref name="path"/> is <see cref="InvitationsPathOf"/> for some Space.
    /// </summary>
    private static bool TryInvitations(string path, out Guid spaceId)
    {
        const string leaf = "/invitations";

        spaceId = Guid.Empty;

        return path.EndsWith(leaf, StringComparison.Ordinal)
            && TryActiveSpace(path[..^leaf.Length], out spaceId)
            && path == InvitationsPathOf(spaceId);
    }

    /// <summary>
    /// Whether <paramref name="path"/> is <see cref="InvitationPathOf"/> for some Space and Invitation. Story 4.2.
    /// </summary>
    private static bool TryInvitation(string path, out Guid spaceId, out Guid invitationId)
    {
        var slash = path.LastIndexOf('/');

        spaceId = Guid.Empty;
        invitationId = Guid.Empty;

        return slash > 0
            && Guid.TryParse(path[(slash + 1)..], out invitationId)
            && TryInvitations(path[..slash], out spaceId)
            && path == InvitationPathOf(spaceId, invitationId);
    }
}
