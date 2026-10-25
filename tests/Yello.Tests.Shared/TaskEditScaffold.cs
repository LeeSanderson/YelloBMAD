namespace Yello.Tests.Shared;

/// <summary>
/// The by-id Task route and the two problem types story 2.3 adds, stated once on the test side because
/// <c>Yello.Contracts</c> does not hold them yet. Written by the story 2.3 ATDD run. <b>Temporary</b>: on
/// activation each use is replaced by the contract constant (<c>TaskRoutes.Task</c>,
/// <c>ProblemTypes.InvalidTaskStatus</c>, <c>ProblemTypes.InvalidDueDate</c>) and this file is deleted, as
/// story 2.2's <c>TaskRouteScaffold</c> was.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded against the story's recommended options.</b> Q1 (a): one <c>PATCH</c> on the Task's own
/// path, merge-patch semantics. Q4 (a): any instant within years 2000-2100, normalised to UTC. Q5 (a):
/// <c>400</c> with a stable type per field. If a decision differs, correct this file first and record it.
/// </para>
/// <para>
/// The Task's own path is already a contract (<c>TaskRoutes.TaskOf</c>, the creation's <c>Location</c>); only
/// the pattern the Host will map is new.
/// </para>
/// </remarks>
public static class TaskEditScaffold
{
    /// <summary>
    /// <c>GET</c> and <c>PATCH</c> one Task: story 2.3 Task 4's <c>TaskRoutes.Task</c>. The URL never holds the
    /// Project (FR-41).
    /// </summary>
    public static string Task => "/api/v1/spaces/{spaceId:guid}/tasks/{taskId:guid}";

    /// <summary>
    /// The route parameter that names the Task.
    /// </summary>
    public static string TaskIdParameter => "taskId";

    /// <summary>
    /// <c>400</c>: the Status is not in the effective set of the Task's own Project - a random id and another
    /// Space's real Status id alike (Q5 (a), Q13 (a)).
    /// </summary>
    public static string InvalidTaskStatus => "urn:yello:problem:invalid-task-status";

    /// <summary>
    /// <c>400</c>: the due date is not an instant within the accepted range (Q4 (a)).
    /// </summary>
    public static string InvalidDueDate => "urn:yello:problem:invalid-due-date";

    /// <summary>
    /// The earliest due date Q4 (a) accepts, inclusive.
    /// </summary>
    public static DateTimeOffset EarliestDueDate { get; } = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The first due date Q4 (a) refuses after the range: the start of 2101.
    /// </summary>
    public static DateTimeOffset FirstDueDateAfterTheRange { get; } = new(2101, 1, 1, 0, 0, 0, TimeSpan.Zero);
}
