namespace Yello.Contracts.Projects;

/// <summary>
/// The Project endpoints' paths, stated once for the client that calls them and the Host that serves
/// them. FR-17, FR-18; story 2.1.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>{spaceId}</c> first after the version</b> (AR-9), so the pipeline resolves every one of these
/// because of the route's shape.
/// </para>
/// <para>
/// <b>No route moves a Project</b> (AC7): the collection takes <c>GET</c> and <c>POST</c>, one Project
/// takes <c>PATCH</c> for its name and <c>DELETE</c> (story 2.5), and nothing carries a segment after the
/// Project's id.
/// </para>
/// </remarks>
public static class ProjectRoutes
{
    /// <summary>
    /// The route parameter naming one Project.
    /// </summary>
    public static string ProjectIdParameter => "projectId";

    /// <summary>
    /// <c>GET</c> here for every Project in the Space, by name then id, as <see cref="ProjectResponse"/>
    /// rows.
    /// </summary>
    public static string List => "/api/v1/spaces/{spaceId:guid}/projects";

    /// <summary>
    /// <c>POST</c> a <see cref="CreateProjectRequest"/> here: <c>201</c> with the
    /// <see cref="ProjectResponse"/> and its <c>Location</c>.
    /// </summary>
    public static string Create => List;

    /// <summary>
    /// One Project's template: <c>PATCH</c> renames it (<see cref="Rename"/>), <c>DELETE</c> deletes it with every
    /// Task in it - <c>204</c>, no body either way (story 2.5).
    /// </summary>
    public static string Project => "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}";

    /// <summary>
    /// <c>PATCH</c> a <see cref="RenameProjectRequest"/> here: <c>200</c> with the renamed
    /// <see cref="ProjectResponse"/>. The same template as <see cref="Project"/>.
    /// </summary>
    public static string Rename => Project;

    /// <summary>
    /// The list and create path for one Space, as a client calls it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The path.</returns>
    public static string ListIn(Guid spaceId) => $"/api/v1/spaces/{spaceId:D}/projects";

    /// <summary>
    /// One Project's path, as a client calls it and as a creation's <c>Location</c> states it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The Project.</param>
    /// <returns>The path.</returns>
    public static string ProjectOf(Guid spaceId, Guid projectId) => $"/api/v1/spaces/{spaceId:D}/projects/{projectId:D}";
}
