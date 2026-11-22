using System.Text.Json;
using Yello.Contracts;
using Yello.Contracts.Projects;

namespace Yello.Tests.Client;

/// <summary>
/// Every Task of one Status moved into another Project on the fake, answered as the Host is to answer it: the source column
/// is emptied, its Tasks join the end of the landing column on the destination's Board in their order, and the answer is
/// <c>{ projectId, statusId, movedCount }</c>. The List rows follow, since the List route serves the Boards' Tasks.
/// </summary>
/// <remarks>
/// <para>
/// <b>Inert unless a case sends <c>POST …/projects/{projectId}/statuses/{statusId}/tasks/project</c></b>, which no earlier
/// case does, so every other case is served exactly what it would be without this partial.
/// </para>
/// <para>
/// <b>The decisions, in the Host's order</b>, so a browser case proves the page and not the fake: the body first, read as a
/// document (unreadable, not an object, no <c>projectId</c>, or a position, a Space or a Task list is
/// <c>400 invalid-task-edit</c>; a <c>projectId</c> that is not an id is <c>400 invalid-task-project</c>; a <c>statusId</c>
/// that is neither an id nor <c>null</c> is <c>400 invalid-status-destination</c>); then a source Project the Space does not
/// hold is the not-found; a destination that is not another Project of the Space is <c>400 invalid-task-project</c>; a
/// source Status outside the source's set is the not-found; a supplied Status outside the destination's set is
/// <c>400 invalid-status-destination</c>; an empty Status answers <c>200</c> with nothing moved; a landing Status outside
/// the destination's set is <c>409 status-destination-required</c>; a destination that cannot take every Task is
/// <c>409 bound-reached</c>; otherwise the move is applied. A set is the Project's seeded <see cref="ProjectStatuses"/>
/// entry, or its Board's columns. The count moved is the source column's true <see cref="FakeColumn.Total"/>.
/// </para>
/// </remarks>
internal sealed partial class FakeYelloApi
{
    private static readonly string[] ForbiddenStatusBulkMoveMembers = ["positionKey", "afterTaskId", "spaceId", "taskIds"];

    private TaskEditHold? _statusBulkMoveHold;
    private TaskEditHold? _lastStatusBulkMoveHold;

    /// <summary>
    /// Every bulk move the page sent, in order.
    /// </summary>
    public IReadOnlyList<FakeRequest> StatusBulkMoves =>
        [.. _requests.Where(request => request.Method == "POST" && TryStatusBulkMovePath(request.Path, out _, out _, out _))];

    /// <summary>
    /// The bulk move path for one Status of one Project.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The source Project.</param>
    /// <param name="statusId">The source Status.</param>
    /// <returns>The path.</returns>
    public static string StatusBulkMovePathOf(Guid spaceId, Guid projectId, Guid statusId) => ProjectStatusRoutes.TasksToProjectOf(spaceId, projectId, statusId);

    /// <summary>
    /// Holds the <b>next</b> bulk move until it is released.
    /// </summary>
    /// <returns>A task that completes when the held move has arrived and is waiting.</returns>
    public Task HoldStatusBulkMoveAsync()
    {
        var hold = new TaskEditHold();

        _statusBulkMoveHold = hold;
        _lastStatusBulkMoveHold = hold;

        return hold.Arrived.Task;
    }

    /// <summary>
    /// Lets the held bulk move go; it is then decided as any other.
    /// </summary>
    public void ReleaseStatusBulkMove() => _lastStatusBulkMoveHold?.Released.TrySetResult(TaskEditOutcome.Admitted);

    /// <summary>
    /// Lets the held bulk move go with a refusal instead: nothing moves. A <c>bound-reached</c> refusal carries the bound and
    /// its limit, as the Host's does.
    /// </summary>
    /// <param name="status">The status code.</param>
    /// <param name="problemType">The problem type.</param>
    public void ReleaseStatusBulkMoveRefused(int status, string problemType) =>
        _lastStatusBulkMoveHold?.Released.TrySetResult(new TaskEditOutcome(status, problemType, Capability: null, Abort: false));

    /// <summary>
    /// Lets the held bulk move go with no answer - the connection reset. Nothing moves.
    /// </summary>
    public void ReleaseStatusBulkMoveUnanswered() =>
        _lastStatusBulkMoveHold?.Released.TrySetResult(new TaskEditOutcome(0, string.Empty, Capability: null, Abort: true));

    /// <summary>
    /// The answer to a bulk move; <c>null</c> when the request is not one.
    /// </summary>
    private async Task<(int Status, string Body)?> StatusBulkMoveAsync(FakeRequest request)
    {
        if (request.Method != "POST" || !TryStatusBulkMovePath(request.Path, out var spaceId, out var projectId, out var statusId))
        {
            return null;
        }

        var outcome = TaskEditOutcome.Admitted;

        if (Interlocked.Exchange(ref _statusBulkMoveHold, null) is { } hold)
        {
            hold.Arrived.TrySetResult();
            outcome = await hold.Released.Task.ConfigureAwait(false);
        }

        if (outcome.Abort)
        {
            return (AbortConnection, string.Empty);
        }

        return outcome.Status != 200
            ? (outcome.Status, ProjectMoveProblem(outcome.ProblemType))
            : MoveStatusToProject(spaceId, projectId, statusId, request.Body);
    }

    private (int Status, string Body) MoveStatusToProject(Guid spaceId, Guid sourceId, Guid statusId, string? body)
    {
        var (refusal, destinationId, supplied) = ReadStatusBulkMove(body);

        if (refusal is not null)
        {
            return (400, Problem(refusal));
        }

        if (!HoldsProject(spaceId, sourceId) || !Boards.TryGetValue((spaceId, sourceId), out var source))
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        if (destinationId == sourceId || !HoldsProject(spaceId, destinationId) || !Boards.TryGetValue((spaceId, destinationId), out var destination))
        {
            return (400, Problem(ProblemTypes.InvalidTaskProject));
        }

        var column = source.Columns.FirstOrDefault(candidate => candidate.StatusId == statusId);

        if (column is null || !SetOf(spaceId, sourceId, source).Contains(statusId))
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        var set = SetOf(spaceId, destinationId, destination);

        if (supplied is { } named && !set.Contains(named))
        {
            return (400, Problem(ProblemTypes.InvalidStatusDestination));
        }

        var landing = supplied ?? statusId;
        var moving = column.Total;

        if (moving == 0)
        {
            return (200, BulkMoveAnswer(destinationId, landing, 0));
        }

        var into = destination.Columns.FirstOrDefault(candidate => candidate.StatusId == landing);

        if (!set.Contains(landing) || into is null)
        {
            return (409, Problem(ProblemTypes.StatusDestinationRequired));
        }

        if (destination.Columns.Sum(candidate => candidate.Total) + moving > TasksPerProject)
        {
            return (409, ProjectMoveProblem(ProblemTypes.BoundReached));
        }

        AppendAll(column, into, landing);

        return (200, BulkMoveAnswer(destinationId, landing, moving));
    }

    /// <summary>
    /// The single move's body rules, with a list of Tasks forbidden too.
    /// </summary>
    private static (string? Refusal, Guid ProjectId, Guid? StatusId) ReadStatusBulkMove(string? body)
    {
        var read = ReadProjectMove(body);

        return read.Refusal is null && HasForbiddenBulkMember(body) ? (ProblemTypes.InvalidTaskEdit, Guid.Empty, null) : read;
    }

    /// <summary>
    /// Every Task of <paramref name="column"/>, in its order, appended to the end of <paramref name="into"/>; the source
    /// column is left empty.
    /// </summary>
    private static void AppendAll(FakeColumn column, FakeColumn into, Guid landing)
    {
        foreach (var task in column.Tasks)
        {
            var last = into.Tasks.Count > 0 ? into.Tasks[^1].PositionKey : "a";

            into.Tasks.Add(task with { StatusId = landing, PositionKey = last + "1" });
        }

        into.Total += column.Total;
        column.Tasks.Clear();
        column.Total = 0;
    }

    private bool HoldsProject(Guid spaceId, Guid projectId) =>
        Projects.TryGetValue(spaceId, out var projects) && projects.Exists(project => project.Id == projectId);

    private List<Guid> SetOf(Guid spaceId, Guid projectId, FakeBoard board) =>
        ProjectStatuses.TryGetValue((spaceId, projectId), out var seeded)
            ? [.. seeded.Select(status => status.Id)]
            : [.. board.Columns.Select(candidate => candidate.StatusId)];

    private static bool HasForbiddenBulkMember(string? body)
    {
        using var document = JsonDocument.Parse(string.IsNullOrEmpty(body) ? "null" : body);
        var root = document.RootElement;

        return root.ValueKind == JsonValueKind.Object && Array.Exists(ForbiddenStatusBulkMoveMembers, member => root.TryGetProperty(member, out _));
    }

    private static string BulkMoveAnswer(Guid projectId, Guid statusId, int movedCount) =>
        JsonSerializer.Serialize(new { projectId, statusId, movedCount }, JsonOptions);

    private static bool TryStatusBulkMovePath(string path, out Guid spaceId, out Guid projectId, out Guid statusId)
    {
        spaceId = Guid.Empty;
        projectId = Guid.Empty;
        statusId = Guid.Empty;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments is ["api", "v1", "spaces", var space, "projects", var project, "statuses", var status, "tasks", "project"]
            && Guid.TryParse(space, out spaceId)
            && Guid.TryParse(project, out projectId)
            && Guid.TryParse(status, out statusId);
    }
}
