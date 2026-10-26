namespace Yello.Contracts.Tasks;

/// <summary>
/// The Task endpoints' paths, stated once for the client that calls them and the Host that serves them.
/// FR-19; story 2.2, open question 5 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b><c>{spaceId}</c> first after the version</b> (AR-9), so the pipeline resolves every one of these
/// because of the route's shape. There is no bare <c>/tasks/{id}</c> outside a Space.
/// </para>
/// <para>
/// <b>A Task is created under its Project and addressed under its Space alone.</b> FR-41 moves a Task
/// between Projects, so its own URL - a creation's <c>Location</c>, and story 2.3's by-id route - must not
/// hold a Project that may stop being true.
/// </para>
/// </remarks>
public static class TaskRoutes
{
    /// <summary>
    /// <c>POST</c> a <see cref="CreateTaskRequest"/> here: <c>201</c> with the <see cref="TaskResponse"/> and
    /// its <c>Location</c>.
    /// </summary>
    public static string Create => "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/tasks";

    /// <summary>
    /// The route parameter naming one Task.
    /// </summary>
    public static string TaskIdParameter => "taskId";

    /// <summary>
    /// One Task: <c>GET</c> it as a <see cref="TaskResponse"/>, or <c>PATCH</c> an <see cref="EditTaskRequest"/>
    /// here for <c>200</c> with the Task as stored (story 2.3), or <c>DELETE</c> it for <c>204</c>, no body (story 2.5).
    /// The path never holds the Project (FR-41).
    /// </summary>
    public static string Task => "/api/v1/spaces/{spaceId:guid}/tasks/{taskId:guid}";

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
}
