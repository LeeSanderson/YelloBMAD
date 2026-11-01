using System.Collections.Concurrent;
using System.Text.Json;
using Yello.Contracts;

namespace Yello.Tests.Client;

/// <summary>
/// Story 4.1's route on the fake (ATDD run, 2026-11-01): issuing an Invitation,
/// <c>POST /api/v1/spaces/{id}/invitations</c>, held and released like 2.5's and 3.3's deletions. No case written before
/// 4.1 sends this route, so every earlier case is served the bytes it always was (it fell to the unmatched <c>404</c>
/// before), and every other unmatched route keeps that answer.
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
/// <b>Every request is recorded</b> (the body text and its headers, the <c>Idempotency-Key</c> among them) by
/// <see cref="FakeYelloApi"/>'s router; <see cref="InvitationsIssued"/> reads them back for one Space.
/// </para>
/// </remarks>
internal sealed partial class FakeYelloApi
{
    /// <summary>
    /// The route's template. It becomes <c>InvitationRoutes.Issue</c> (and <c>InvitationRoutes.InvitationsOf(spaceId)</c>)
    /// in <c>Yello.Contracts/Invitations</c> when story 4.1's dev phase adds it; replace this constant with the contract
    /// route then, so a contract change breaks the fake as well as the Host.
    /// </summary>
    private const string InvitationsRoute = "/api/v1/spaces/{spaceId:guid}/invitations";

    private const string RoleNotOfferableType = "urn:yello:problem:role-not-offerable";

    private readonly ConcurrentDictionary<string, string> _invitedByKey = new(StringComparer.Ordinal);

    private DeleteHold? _invitationHold;
    private DeleteHold? _lastInvitationHold;

    /// <summary>
    /// When set, the next Invitation issue is refused with this instead of applied: a status, a problem type and the
    /// capability a <c>403</c> names (or <c>null</c>).
    /// </summary>
    public (int Status, string ProblemType, string? Capability)? NextInvitationRefusal { get; set; }

    /// <summary>
    /// The issue route for one Space, as <c>InvitationRoutes.InvitationsOf</c> will name it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The path.</returns>
    public static string InvitationsPathOf(Guid spaceId) =>
        InvitationsRoute.Replace("{spaceId:guid}", spaceId.ToString("D"), StringComparison.Ordinal);

    /// <summary>
    /// Every Invitation issue in <paramref name="spaceId"/> the page sent, in order, each with its body text and headers.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The requests.</returns>
    public IReadOnlyList<FakeRequest> InvitationsIssued(Guid spaceId) =>
        [.. _requests.Where(request => request.Method == "POST" && request.Path == InvitationsPathOf(spaceId))];

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
    /// Story 4.1's answer, consulted before the main routes: <c>null</c> when the request is not an Invitation issue.
    /// </summary>
    private async Task<(int Status, string Body)?> InvitationAsync(FakeRequest request) =>
        request.Method == "POST" && TryInvitations(request.Path, out var spaceId)
            ? await IssueInvitationAsync(spaceId, request).ConfigureAwait(false)
            : null;

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

        if (Switcher.All(space => space.Id != spaceId) || !Roles.TryGetValue(spaceId, out var role))
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        // The pipeline's guard is IssueAndRevokeInvitations, Owner and Admin: a client that sent the issue for any other
        // Role meets the 403 here as it would there, not a fake 204 (the 3.3 code-review precedent).
        if (role is not ("owner" or "admin"))
        {
            return (403, JsonSerializer.Serialize(new ProblemResponse(ProblemTypes.InsufficientRole, Errors: null, Capability: "issue-and-revoke-invitations"), JsonOptions));
        }

        if (RoleCodeOf(body) is not ("admin" or "member" or "viewer"))
        {
            return (400, Problem(RoleNotOfferableType));
        }

        if (key is not null)
        {
            _invitedByKey[key] = body;
        }

        return (204, string.Empty);
    }

    /// <summary>
    /// The <c>role</c> member of an issue body, or <c>null</c> when the body is unreadable or carries no string Role.
    /// </summary>
    private static string? RoleCodeOf(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrEmpty(body) ? "{}" : body);

            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("role", out var role)
                && role.ValueKind == JsonValueKind.String
                    ? role.GetString()
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
}
