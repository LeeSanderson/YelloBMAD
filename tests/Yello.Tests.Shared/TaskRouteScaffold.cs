namespace Yello.Tests.Shared;

/// <summary>
/// Story 2.2's routes, wire codes and problem type, held on the test side until <c>Yello.Contracts</c>
/// states them. Written by the story 2.2 ATDD run, as 2.1's <c>ProjectRouteScaffold</c> was: on activation
/// every use is replaced by <c>TaskRoutes</c>, <c>BoardRoutes</c> and <c>ProblemTypes.InvalidTaskTitle</c>,
/// and this file is deleted.
/// </summary>
/// <remarks>
/// The shapes are the story's Q5 (a): a Task is created under its Project, read by its own id under its
/// Space only (a Task can change Project later, FR-41, so its URL must not hold one), and the Board is
/// read under its Project. Every pattern carries <c>{spaceId}</c> first after the version (AR-9).
/// </remarks>
public static class TaskRouteScaffold
{
    /// <summary>
    /// <c>POST</c> a <c>CreateTaskRequest</c> (<c>{"title": ...}</c>) here: <c>201</c> with the Task and its
    /// <c>Location</c>.
    /// </summary>
    public static string Create => "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/tasks";

    /// <summary>
    /// <c>GET</c> here for the Project's Board: <c>{project, columns}</c>.
    /// </summary>
    public static string Board => "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/board";

    /// <summary>
    /// The machine-readable reason a title is refused (<c>400</c>).
    /// </summary>
    public static string InvalidTaskTitle => "urn:yello:problem:invalid-task-title";

    /// <summary>
    /// The capability a Task creation declares, as it travels on the wire.
    /// </summary>
    public static string ManageTasks => "manage-tasks";

    /// <summary>
    /// The bound a Task creation consults, as it travels on the wire.
    /// </summary>
    public static string TasksPerProject => "tasks-per-project";

    /// <summary>
    /// Its limit (<c>BoundRegistry</c>, NFR-8).
    /// </summary>
    public static int TasksPerProjectLimit => 5_000;

    /// <summary>
    /// The longest title Q3 (a) admits.
    /// </summary>
    public static int TitleMaximumLength => 200;

    /// <summary>
    /// The create path for one Project, as a client calls it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The Project.</param>
    /// <returns>The path.</returns>
    public static string CreateIn(Guid spaceId, Guid projectId) => $"/api/v1/spaces/{spaceId:D}/projects/{projectId:D}/tasks";

    /// <summary>
    /// One Task's own path, as a creation's <c>Location</c> states it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="taskId">The Task.</param>
    /// <returns>The path.</returns>
    public static string TaskOf(Guid spaceId, Guid taskId) => $"/api/v1/spaces/{spaceId:D}/tasks/{taskId:D}";

    /// <summary>
    /// One Project's Board path, as a client calls it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The Project.</param>
    /// <returns>The path.</returns>
    public static string BoardOf(Guid spaceId, Guid projectId) => $"/api/v1/spaces/{spaceId:D}/projects/{projectId:D}/board";

    /// <summary>
    /// The client's Board page for one Project (Task 10): ids only, never a name.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The Project.</param>
    /// <returns>The client path.</returns>
    public static string ClientBoard(Guid spaceId, Guid projectId) => $"/spaces/{spaceId:D}/projects/{projectId:D}";
}
