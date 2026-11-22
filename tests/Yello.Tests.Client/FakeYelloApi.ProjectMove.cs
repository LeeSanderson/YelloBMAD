using System.Text.Json;
using Yello.Contracts;
using Yello.Contracts.Tasks;

namespace Yello.Tests.Client;

/// <summary>
/// A Task moved into another Project on the fake, answered as the Host is to answer it: the card leaves its Board and joins the
/// end of its column on the destination's Board, and the answer is the Task as stored with its new <c>projectId</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Inert unless a case sends <c>POST …/tasks/{taskId}/project</c></b> (<see cref="TaskRoutes.ProjectMove"/>), which no earlier
/// case does, so every other case is served exactly what it would be without this partial.
/// </para>
/// <para>
/// <b>The decisions, in the Host's order</b>, so a browser case proves the page and not the fake: a Task the Space does not
/// hold is the not-found; an unreadable body, one that is not an object, leaves out <c>projectId</c>, or names a position or a
/// Space is <c>400 invalid-task-edit</c>; a <c>projectId</c> that is not an id, or not another Project of the Space, is
/// <c>400 invalid-task-project</c>; a <c>statusId</c> that is neither an id nor <c>null</c>, or one the destination's set does
/// not hold, is <c>400 invalid-status-destination</c>; no <c>statusId</c> and the Task's own Status outside the destination's
/// set is <c>409 status-destination-required</c>; a destination holding 5,000 Tasks is <c>409 bound-reached</c>; otherwise the
/// move is applied. The destination's set is its seeded <see cref="ProjectStatuses"/> entry, or its Board's columns.
/// </para>
/// </remarks>
internal sealed partial class FakeYelloApi
{
    private const int TasksPerProject = 5000;

    private static readonly string[] ForbiddenProjectMoveMembers = ["positionKey", "afterTaskId", "spaceId"];

    private TaskEditHold? _projectMoveHold;
    private TaskEditHold? _lastProjectMoveHold;

    /// <summary>
    /// Every move into another Project the page sent, in order.
    /// </summary>
    public IReadOnlyList<FakeRequest> ProjectMoves =>
        [.. _requests.Where(request => request.Method == "POST" && TryProjectMovePath(request.Path, out _, out _))];

    /// <summary>
    /// The move-to-Project path for one Task.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="taskId">The Task.</param>
    /// <returns>The path.</returns>
    public static string ProjectMovePathOf(Guid spaceId, Guid taskId) => TaskRoutes.ProjectMoveOf(spaceId, taskId);

    /// <summary>
    /// Holds the <b>next</b> move into another Project until it is released.
    /// </summary>
    /// <returns>A task that completes when the held move has arrived and is waiting.</returns>
    public Task HoldProjectMoveAsync()
    {
        var hold = new TaskEditHold();

        _projectMoveHold = hold;
        _lastProjectMoveHold = hold;

        return hold.Arrived.Task;
    }

    /// <summary>
    /// Lets the held move go; it is then decided as any other.
    /// </summary>
    public void ReleaseProjectMove() => _lastProjectMoveHold?.Released.TrySetResult(TaskEditOutcome.Admitted);

    /// <summary>
    /// Lets the held move go with a refusal instead: nothing moves. A <c>bound-reached</c> refusal carries the bound and its
    /// limit, as the Host's does.
    /// </summary>
    /// <param name="status">The status code.</param>
    /// <param name="problemType">The problem type.</param>
    public void ReleaseProjectMoveRefused(int status, string problemType) =>
        _lastProjectMoveHold?.Released.TrySetResult(new TaskEditOutcome(status, problemType, Capability: null, Abort: false));

    /// <summary>
    /// Lets the held move go with no answer - the connection reset. Nothing moves.
    /// </summary>
    public void ReleaseProjectMoveUnanswered() =>
        _lastProjectMoveHold?.Released.TrySetResult(new TaskEditOutcome(0, string.Empty, Capability: null, Abort: true));

    /// <summary>
    /// The answer to a move into another Project; <c>null</c> when the request is not one.
    /// </summary>
    private async Task<(int Status, string Body)?> ProjectMoveAsync(FakeRequest request)
    {
        if (request.Method != "POST" || !TryProjectMovePath(request.Path, out var spaceId, out var taskId))
        {
            return null;
        }

        var outcome = TaskEditOutcome.Admitted;

        if (Interlocked.Exchange(ref _projectMoveHold, null) is { } hold)
        {
            hold.Arrived.TrySetResult();
            outcome = await hold.Released.Task.ConfigureAwait(false);
        }

        if (outcome.Abort)
        {
            return (AbortConnection, string.Empty);
        }

        if (outcome.Status != 200)
        {
            return (outcome.Status, ProjectMoveProblem(outcome.ProblemType));
        }

        return FindTaskIn(spaceId, taskId) is { } found
            ? MoveToProject(spaceId, found.Board, found.Column, found.Task, request.Body)
            : (404, Problem(ProblemTypes.NotAvailable));
    }

    private (int Status, string Body) MoveToProject(Guid spaceId, FakeBoard source, FakeColumn column, FakeTask task, string? body)
    {
        var (refusal, projectId, statusId) = ReadProjectMove(body);

        if (refusal is not null)
        {
            return (400, Problem(refusal));
        }

        if (projectId == source.Project.Id || !Boards.TryGetValue((spaceId, projectId), out var destination)
            || !(Projects.TryGetValue(spaceId, out var projects) && projects.Exists(project => project.Id == projectId)))
        {
            return (400, Problem(ProblemTypes.InvalidTaskProject));
        }

        IReadOnlyList<Guid> set = ProjectStatuses.TryGetValue((spaceId, projectId), out var seeded)
            ? [.. seeded.Select(status => status.Id)]
            : [.. destination.Columns.Select(candidate => candidate.StatusId)];

        if (statusId is { } supplied && !set.Contains(supplied))
        {
            return (400, Problem(ProblemTypes.InvalidStatusDestination));
        }

        var landing = statusId ?? task.StatusId;

        if (!set.Contains(landing))
        {
            return (409, Problem(ProblemTypes.StatusDestinationRequired));
        }

        if (destination.Columns.Sum(candidate => candidate.Total) >= TasksPerProject)
        {
            return (409, ProjectMoveProblem(ProblemTypes.BoundReached));
        }

        var into = destination.Columns.FirstOrDefault(candidate => candidate.StatusId == landing);

        if (into is null)
        {
            return (409, Problem(ProblemTypes.StatusDestinationRequired));
        }

        var last = into.Tasks.Count > 0 ? into.Tasks[^1].PositionKey : "a";
        var moved = task with { StatusId = landing, PositionKey = last + "1" };

        column.Tasks.Remove(task);
        column.Total--;
        into.Tasks.Add(moved);
        into.Total++;

        var answer = JsonSerializer.SerializeToNode(moved, JsonOptions)!.AsObject();

        answer["projectId"] = projectId;

        return (200, answer.ToJsonString(JsonOptions));
    }

    /// <summary>
    /// A move body read as the Host reads it: a document, never bound.
    /// </summary>
    private static (string? Refusal, Guid ProjectId, Guid? StatusId) ReadProjectMove(string? body)
    {
        JsonElement root;

        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrEmpty(body) ? "null" : body);

            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return (ProblemTypes.InvalidTaskEdit, Guid.Empty, null);
        }

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("projectId", out var project))
        {
            return (ProblemTypes.InvalidTaskEdit, Guid.Empty, null);
        }

        if (Array.Exists(ForbiddenProjectMoveMembers, member => root.TryGetProperty(member, out _)))
        {
            return (ProblemTypes.InvalidTaskEdit, Guid.Empty, null);
        }

        if (project.ValueKind != JsonValueKind.String || !project.TryGetGuid(out var projectId))
        {
            return (ProblemTypes.InvalidTaskProject, Guid.Empty, null);
        }

        if (!root.TryGetProperty("statusId", out var status) || status.ValueKind == JsonValueKind.Null)
        {
            return (null, projectId, null);
        }

        return status.ValueKind == JsonValueKind.String && status.TryGetGuid(out var statusId)
            ? (null, projectId, statusId)
            : (ProblemTypes.InvalidStatusDestination, Guid.Empty, null);
    }

    private static string ProjectMoveProblem(string problemType) =>
        problemType == ProblemTypes.BoundReached
            ? JsonSerializer.Serialize(new ProblemResponse(problemType, Errors: null, Bound: "tasks-per-project", Limit: TasksPerProject), JsonOptions)
            : Problem(problemType);

    private static bool TryProjectMovePath(string path, out Guid spaceId, out Guid taskId)
    {
        spaceId = Guid.Empty;
        taskId = Guid.Empty;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments is ["api", "v1", "spaces", var space, "tasks", var task, "project"]
            && Guid.TryParse(space, out spaceId)
            && Guid.TryParse(task, out taskId);
    }
}
