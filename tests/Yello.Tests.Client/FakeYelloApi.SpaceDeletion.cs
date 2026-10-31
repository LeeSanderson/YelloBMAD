using System.Text.Json;
using Yello.Contracts;
using Yello.Contracts.Spaces;

namespace Yello.Tests.Client;

/// <summary>
/// Story 3.3's routes on the fake (ATDD run, 2026-10-31): the Owner's Space deletion, <c>DELETE /api/v1/spaces/{id}</c>,
/// held and released like 2.5's deletions; Q2 (a)'s counts read, <c>GET /api/v1/spaces/{id}/deletion</c>; and a switcher
/// that can be made to fail, for AC14 and AC15. No case written before 3.3 sends either route or sets
/// <see cref="SwitcherFails"/>, so every earlier case is served the bytes it always was (both routes fell to the unmatched
/// <c>404</c> before).
/// </summary>
/// <remarks>
/// <para>
/// <b>The deletion as the Host answers it under Q1 (a).</b> An unlisted Space - a non-member's, or one already deleted -
/// is the boundary <c>404</c>, so a replay of an applied deletion is a <c>404</c> too, and never a replayed <c>204</c>: the
/// Host records no key for this route (story 3.3 Q1). A held deletion waits for its release (applied, refused or
/// unanswered); a queued refusal applies nothing; an applied one takes the Space out of the switcher, its Role, its
/// Projects, Boards and Labels.
/// </para>
/// <para>
/// <b>The counts read's path and body are assumed</b> (Q2 (a)): <c>/deletion</c> under the active Space, answering
/// <c>{projects, tasks, otherMembers}</c> - the camel-cased <c>SpaceDeletionSummaryResponse(int Projects, int Tasks, int
/// OtherMembers)</c> the story names. Retarget <see cref="DeletionSummaryPathOf"/> and the body to the contract once the
/// dev phase adds it. A Role other than Owner is <c>403 insufficient-role</c> with <c>delete-space</c>.
/// </para>
/// </remarks>
internal sealed partial class FakeYelloApi
{
    private DeleteHold? _spaceDeleteHold;
    private DeleteHold? _lastSpaceDeleteHold;

    /// <summary>
    /// The counts the deletion's confirm states, per Space. A Space with no entry is counted from the fake's Projects and
    /// Boards, with no other Members.
    /// </summary>
    public IDictionary<Guid, (int Projects, int Tasks, int OtherMembers)> DeletionSummaries { get; } =
        new Dictionary<Guid, (int Projects, int Tasks, int OtherMembers)>();

    /// <summary>
    /// When set, the next Space deletion is refused with this instead of applied.
    /// </summary>
    public (int Status, string ProblemType, string? Capability)? NextSpaceDeleteRefusal { get; set; }

    /// <summary>
    /// When set, the switcher (<c>GET /api/v1/spaces</c>) answers <c>503 service-unavailable</c>: the "could not be asked"
    /// state of AC14 and AC15.
    /// </summary>
    public bool SwitcherFails { get; set; }

    /// <summary>
    /// Q2 (a)'s counts read for one Space - an assumed path; see the class remarks.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The path.</returns>
    public static string DeletionSummaryPathOf(Guid spaceId) => SpaceRoutes.ActiveOf(spaceId) + "/deletion";

    /// <summary>
    /// Every deletion of <paramref name="spaceId"/> the page sent, in order.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The requests.</returns>
    public IReadOnlyList<FakeRequest> SpaceDeletions(Guid spaceId) =>
        [.. _requests.Where(request => request.Method == "DELETE" && request.Path == SpaceRoutes.ActiveOf(spaceId))];

    /// <summary>
    /// How many times the page asked for <paramref name="spaceId"/>'s deletion counts.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The count.</returns>
    public int DeletionSummaryReads(Guid spaceId) =>
        _requests.Count(request => request.Method == "GET" && request.Path == DeletionSummaryPathOf(spaceId));

    /// <summary>
    /// Holds the <b>next</b> Space deletion until it is released.
    /// </summary>
    /// <returns>A task that completes when the held deletion has arrived and is waiting.</returns>
    public Task HoldSpaceDeleteAsync()
    {
        var hold = new DeleteHold();

        _spaceDeleteHold = hold;
        _lastSpaceDeleteHold = hold;

        return hold.Arrived.Task;
    }

    /// <summary>
    /// Lets the held Space deletion go and applies it: <c>204</c>.
    /// </summary>
    public void ReleaseSpaceDelete() => _lastSpaceDeleteHold?.Released.TrySetResult(TaskEditOutcome.Deleted);

    /// <summary>
    /// Lets the held Space deletion go with a refusal or a server failure instead: nothing is deleted.
    /// </summary>
    /// <param name="status">The status code.</param>
    /// <param name="problemType">The problem type.</param>
    /// <param name="capability">The capability a <c>403</c> names, or <c>null</c>.</param>
    public void ReleaseSpaceDeleteRefused(int status, string problemType, string? capability = null) =>
        _lastSpaceDeleteHold?.Released.TrySetResult(new TaskEditOutcome(status, problemType, capability, Abort: false));

    /// <summary>
    /// Lets the held Space deletion go with no answer - the connection reset. Nothing is deleted, so a resend under the same
    /// key is applied.
    /// </summary>
    public void ReleaseSpaceDeleteUnanswered() =>
        _lastSpaceDeleteHold?.Released.TrySetResult(new TaskEditOutcome(0, string.Empty, Capability: null, Abort: true));

    /// <summary>
    /// Story 3.3's answers, consulted before every other route: <c>null</c> when the request is none of them.
    /// </summary>
    private async Task<(int Status, string Body)?> SpaceDeletionAsync(FakeRequest request)
    {
        if (SwitcherFails && request.Method == "GET" && request.Path == SpaceRoutes.Directory)
        {
            return (503, Problem(ProblemTypes.ServiceUnavailable));
        }

        if (request.Method == "GET" && TryDeletionSummary(request.Path, out var summarySpace))
        {
            return DeletionSummary(summarySpace);
        }

        return request.Method == "DELETE" && TryActiveSpace(request.Path, out var deletedSpace)
            ? await DeleteSpaceAsync(deletedSpace).ConfigureAwait(false)
            : null;
    }

    private (int Status, string Body) DeletionSummary(Guid spaceId)
    {
        if (Switcher.All(space => space.Id != spaceId) || !Roles.TryGetValue(spaceId, out var role))
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        if (role != "owner")
        {
            return (403, JsonSerializer.Serialize(new ProblemResponse(ProblemTypes.InsufficientRole, Errors: null, Capability: "delete-space"), JsonOptions));
        }

        var projectsHeld = Projects.TryGetValue(spaceId, out var held) ? held.Count : 0;
        var tasksHeld = Boards.Where(entry => entry.Key.SpaceId == spaceId).SelectMany(entry => entry.Value.Columns).Sum(column => column.Total);
        var (projects, tasks, otherMembers) = DeletionSummaries.TryGetValue(spaceId, out var stated) ? stated : (projectsHeld, tasksHeld, 0);

        return (200, JsonSerializer.Serialize(new { projects, tasks, otherMembers }, JsonOptions));
    }

    private async Task<(int Status, string Body)> DeleteSpaceAsync(Guid spaceId)
    {
        var outcome = await DeleteOutcomeAsync(() => Interlocked.Exchange(ref _spaceDeleteHold, null), () =>
        {
            var refusal = NextSpaceDeleteRefusal;

            NextSpaceDeleteRefusal = null;
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

        var index = Switcher.ToList().FindIndex(space => space.Id == spaceId);

        if (index < 0)
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        Switcher.RemoveAt(index);
        _ = Roles.Remove(spaceId);
        _ = Projects.Remove(spaceId);
        _ = Labels.Remove(spaceId);

        foreach (var board in Boards.Keys.Where(key => key.SpaceId == spaceId).ToList())
        {
            _ = Boards.Remove(board);
        }

        return (204, string.Empty);
    }

    /// <summary>
    /// Whether <paramref name="path"/> is <see cref="DeletionSummaryPathOf"/> for some Space.
    /// </summary>
    private static bool TryDeletionSummary(string path, out Guid spaceId)
    {
        const string leaf = "/deletion";

        spaceId = Guid.Empty;

        return path.EndsWith(leaf, StringComparison.Ordinal)
            && TryActiveSpace(path[..^leaf.Length], out spaceId);
    }
}
