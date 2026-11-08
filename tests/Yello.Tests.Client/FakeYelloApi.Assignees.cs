using System.Collections.Concurrent;
using System.Text.Json;
using Yello.Contracts;
using Yello.Contracts.Memberships;
using Yello.Contracts.Spaces;
using Yello.Contracts.Tasks;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// The assignment routes on the fake: the candidates read, <c>GET /api/v1/spaces/{id}/assignees</c>, and the assignment,
/// <c>PUT /api/v1/spaces/{id}/tasks/{taskId}/assignee</c> with <c>{ membershipId }</c>. No case written before assignment
/// existed sends either, so every earlier case is served the bytes it always was (both fell to the unmatched <c>404</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>As the Host answers them.</b> The pipeline first: a Space the switcher does not list is the boundary <c>404</c>; for the
/// assignment, a Viewer is the <c>403 insufficient-role</c> naming <c>assign-task</c>. Then the body: not an object, or no
/// <c>membershipId</c> member, is <c>400 invalid-assignee</c>. Then the store: a Task the Space does not hold is the in-Space
/// <c>404</c>; a Membership id not among <see cref="SpaceAssignees"/> for the Space is <c>400 invalid-assignee</c>, the same
/// bytes for every such id; otherwise the Task's <c>AssigneeMembershipId</c> is set (or cleared by <c>null</c>) and the answer
/// is the Task. A key already applied to the same path and body replays the answer; on another body it is
/// <c>422 idempotency-key-reused</c>.
/// </para>
/// <para>
/// <b>Hooks</b>, following the Task edit's: <see cref="HoldAssignmentAsync"/> holds the next assignment until
/// <see cref="ReleaseAssignment"/>; <see cref="NextAssignmentRefusal"/> refuses the next one.
/// </para>
/// </remarks>
internal sealed partial class FakeYelloApi
{
    private readonly ConcurrentDictionary<string, (string Path, string Body, string Answer)> _assignedByKey = new(StringComparer.Ordinal);

    private DeleteHold? _assignmentHold;
    private DeleteHold? _lastAssignmentHold;

    /// <summary>
    /// Each Space's candidates, in join order, as the candidates read answers them: (Membership id, display name).
    /// </summary>
    public ConcurrentDictionary<Guid, List<(Guid MembershipId, string DisplayName)>> SpaceAssignees { get; } = new();

    /// <summary>
    /// When set, the next assignment is refused with this instead of applied.
    /// </summary>
    public (int Status, string ProblemType, string? Capability)? NextAssignmentRefusal { get; set; }

    /// <summary>
    /// Every assignment in <paramref name="spaceId"/> the page sent, in order.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The requests.</returns>
    public IReadOnlyList<FakeRequest> Assignments(Guid spaceId) =>
        [.. _requests.Where(request => request.Method == "PUT" && TryAssignee(request.Path, out var assignedIn, out _) && assignedIn == spaceId)];

    /// <summary>
    /// Every candidates read the page sent, in order, with the Space it named.
    /// </summary>
    /// <returns>Each read's Space.</returns>
    public IReadOnlyList<Guid> CandidateReads() =>
        [.. _requests.Where(request => request.Method == "GET").Select(request => TryAssignees(request.Path, out var readIn) ? readIn : Guid.Empty).Where(id => id != Guid.Empty)];

    /// <summary>
    /// The Assignee a Task holds now in the fake, or <c>null</c>.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="taskId">The Task.</param>
    /// <returns>The Membership id, or <c>null</c>.</returns>
    public Guid? AssigneeOf(Guid spaceId, Guid taskId) => FindTaskIn(spaceId, taskId)?.Task.AssigneeMembershipId;

    /// <summary>
    /// Holds the <b>next</b> assignment until it is released.
    /// </summary>
    /// <returns>A task that completes when the held assignment has arrived and is waiting.</returns>
    public Task HoldAssignmentAsync()
    {
        var hold = new DeleteHold();

        _assignmentHold = hold;
        _lastAssignmentHold = hold;

        return hold.Arrived.Task;
    }

    /// <summary>
    /// Lets the held assignment go with <paramref name="outcome"/>: <see cref="TaskEditOutcome.Admitted"/> applies it.
    /// </summary>
    /// <param name="outcome">The answer.</param>
    public void ReleaseAssignment(TaskEditOutcome outcome) => _lastAssignmentHold?.Released.TrySetResult(outcome);

    private async Task<(int Status, string Body)?> AssigneeAsync(FakeRequest request)
    {
        if (request.Method == "GET" && TryAssignees(request.Path, out var readSpace))
        {
            return !Switcher.Any(space => space.Id == readSpace)
                ? (404, Problem(ProblemTypes.NotAvailable))
                : (200, JsonSerializer.Serialize(new { assignees = CandidatesOf(readSpace).Select(candidate => new { membershipId = candidate.MembershipId, displayName = candidate.DisplayName }) }, JsonOptions));
        }

        if (request.Method == "PUT" && TryAssignee(request.Path, out var spaceId, out var taskId))
        {
            return await AssignAsync(spaceId, taskId, request).ConfigureAwait(false);
        }

        return null;
    }

    private async Task<(int Status, string Body)> AssignAsync(Guid spaceId, Guid taskId, FakeRequest request)
    {
        if (RefusedByThePipeline(spaceId) is { } refused)
        {
            return refused;
        }

        var key = request.Header(Idempotency.HeaderName);
        var body = request.Body ?? string.Empty;

        if (key is not null && _assignedByKey.TryGetValue(key, out var earlier))
        {
            return earlier.Path == request.Path && earlier.Body == body
                ? (200, earlier.Answer)
                : (422, Problem(ProblemTypes.IdempotencyKeyReused));
        }

        var outcome = await NextAssignmentOutcomeAsync().ConfigureAwait(false);

        if (outcome.Abort)
        {
            return (AbortConnection, string.Empty);
        }

        if (outcome.Status != 200)
        {
            return (outcome.Status, JsonSerializer.Serialize(new ProblemResponse(outcome.ProblemType, Errors: null, Capability: outcome.Capability), JsonOptions));
        }

        var (status, answer) = Apply(spaceId, taskId, body);

        if (status == 200 && key is not null)
        {
            _assignedByKey[key] = (request.Path, body, answer);
        }

        return (status, answer);
    }

    /// <summary>
    /// The pipeline's answers before the slice runs: an unlisted Space is the boundary <c>404</c>, a Viewer the <c>403</c>.
    /// </summary>
    private (int Status, string Body)? RefusedByThePipeline(Guid spaceId)
    {
        if (!Switcher.Any(space => space.Id == spaceId))
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        return Roles.TryGetValue(spaceId, out var role) && role == "viewer"
            ? (403, JsonSerializer.Serialize(new ProblemResponse(ProblemTypes.InsufficientRole, Errors: null, Capability: AssignmentWire.AssignCapabilityCode), JsonOptions))
            : null;
    }

    private async Task<TaskEditOutcome> NextAssignmentOutcomeAsync()
    {
        if (Interlocked.Exchange(ref _assignmentHold, null) is { } hold)
        {
            hold.Arrived.TrySetResult();

            return await hold.Released.Task.ConfigureAwait(false);
        }

        if (NextAssignmentRefusal is { } refusal)
        {
            NextAssignmentRefusal = null;

            return new TaskEditOutcome(refusal.Status, refusal.ProblemType, refusal.Capability, Abort: false);
        }

        return TaskEditOutcome.Admitted;
    }

    /// <summary>
    /// The store's answer: a body naming no Membership, or one the Space does not list, is <c>400 invalid-assignee</c>; a Task
    /// the Space does not hold is the in-Space <c>404</c>; otherwise the Task with its Assignee set or cleared.
    /// </summary>
    private (int Status, string Body) Apply(Guid spaceId, Guid taskId, string body)
    {
        if (!TryMembershipId(body, out var membershipId) || (membershipId is { } named && !CandidatesOf(spaceId).Exists(candidate => candidate.MembershipId == named)))
        {
            return (400, Problem(ProblemTypes.InvalidAssignee));
        }

        if (FindTaskIn(spaceId, taskId) is not { } found)
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        var assigned = found.Task with { AssigneeMembershipId = membershipId };

        found.Column.Tasks[found.Column.Tasks.FindIndex(candidate => candidate.Id == taskId)] = assigned;

        return (200, JsonSerializer.Serialize(assigned, JsonOptions));
    }

    private List<(Guid MembershipId, string DisplayName)> CandidatesOf(Guid spaceId) =>
        SpaceAssignees.TryGetValue(spaceId, out var candidates) ? candidates : [];

    /// <summary>
    /// Reads <c>{ membershipId }</c>: a GUID or <c>null</c>. Not an object, no such member, or any other value is no Membership
    /// id at all.
    /// </summary>
    private static bool TryMembershipId(string body, out Guid? membershipId)
    {
        membershipId = null;

        try
        {
            using var document = JsonDocument.Parse(body.Length == 0 ? "null" : body);

            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("membershipId", out var value))
            {
                return false;
            }

            if (value.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            if (value.ValueKind == JsonValueKind.String && value.TryGetGuid(out var id))
            {
                membershipId = id;

                return true;
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Whether <paramref name="path"/> is <c>/api/v1/spaces/{s}/assignees</c>.
    /// </summary>
    private static bool TryAssignees(string path, out Guid spaceId)
    {
        var prefix = SpaceRoutes.Directory + "/";
        var segments = path.StartsWith(prefix, StringComparison.Ordinal) ? path[prefix.Length..].Split('/') : [];

        spaceId = Guid.Empty;

        return segments is [var space, "assignees"]
            && Guid.TryParse(space, out spaceId)
            && path == AssigneeRoutes.ListOf(spaceId);
    }

    /// <summary>
    /// Whether <paramref name="path"/> is <c>/api/v1/spaces/{s}/tasks/{t}/assignee</c>.
    /// </summary>
    private static bool TryAssignee(string path, out Guid spaceId, out Guid taskId)
    {
        var prefix = SpaceRoutes.Directory + "/";
        var segments = path.StartsWith(prefix, StringComparison.Ordinal) ? path[prefix.Length..].Split('/') : [];

        spaceId = Guid.Empty;
        taskId = Guid.Empty;

        return segments is [var space, "tasks", var task, "assignee"]
            && Guid.TryParse(space, out spaceId)
            && Guid.TryParse(task, out taskId)
            && path == TaskRoutes.AssigneeOf(spaceId, taskId);
    }
}
