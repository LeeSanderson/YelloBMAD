namespace Yello.Contracts.Projects;

/// <summary>
/// The paths of one Project's Statuses: the read of its effective set, and the add, rename, place, remove and add-back
/// writes, stated once
/// for the client that calls them and the Host that serves them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Under the Project, after <c>{spaceId}</c></b>, so the pipeline resolves the Space because of the route's
/// shape, and the Project and the Status are read inside it.
/// </para>
/// <para>
/// <b>Place is an action of its own</b>, never a member of the rename: a body that could carry a name and a position
/// would need two meanings for <c>null</c>. Every answer is the Project's whole set as <see cref="ProjectStatusResponse"/>
/// rows, in effective order.
/// </para>
/// </remarks>
public static class ProjectStatusRoutes
{
    /// <summary>
    /// The route parameter naming one Status.
    /// </summary>
    public static string StatusIdParameter => "statusId";

    /// <summary>
    /// <c>GET</c> here for the Project's effective set; <c>POST</c> an <see cref="AddProjectStatusRequest"/> here to add a
    /// Status - <c>201</c> with the set.
    /// </summary>
    public static string Collection => "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/statuses";

    /// <summary>
    /// One Status of the Project: <c>PATCH</c> a <see cref="RenameProjectStatusRequest"/> here to rename it in this Project.
    /// </summary>
    public static string Item => "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/statuses/{statusId:guid}";

    /// <summary>
    /// <c>POST</c> a <see cref="PlaceProjectStatusRequest"/> here to move the Status first or after another.
    /// </summary>
    public static string Place => "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/statuses/{statusId:guid}/place";

    /// <summary>
    /// <c>POST</c> a <see cref="RemoveProjectStatusRequest"/> here to take the Status out of the Project's set, moving its
    /// Tasks to the destination in the same transaction. The only way a Status leaves a Project.
    /// </summary>
    public static string Remove => "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/statuses/{statusId:guid}/remove";

    /// <summary>
    /// <c>POST</c> here, with no body, to add back a Space default this Project removed, under its own id.
    /// </summary>
    public static string Restore => "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/statuses/{statusId:guid}/restore";

    /// <summary>
    /// The collection's path, as a client calls it and as an add's <c>Location</c> states it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The Project.</param>
    /// <returns>The path.</returns>
    public static string CollectionOf(Guid spaceId, Guid projectId) => $"/api/v1/spaces/{spaceId:D}/projects/{projectId:D}/statuses";

    /// <summary>
    /// One Status's path under a Project.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The Project.</param>
    /// <param name="statusId">The Status.</param>
    /// <returns>The path.</returns>
    public static string ItemOf(Guid spaceId, Guid projectId, Guid statusId) => $"{CollectionOf(spaceId, projectId)}/{statusId:D}";

    /// <summary>
    /// The place action's path.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The Project.</param>
    /// <param name="statusId">The Status being placed.</param>
    /// <returns>The path.</returns>
    public static string PlaceOf(Guid spaceId, Guid projectId, Guid statusId) => $"{ItemOf(spaceId, projectId, statusId)}/place";

    /// <summary>
    /// The remove action's path.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The Project.</param>
    /// <param name="statusId">The Status being removed.</param>
    /// <returns>The path.</returns>
    public static string RemoveOf(Guid spaceId, Guid projectId, Guid statusId) => $"{ItemOf(spaceId, projectId, statusId)}/remove";

    /// <summary>
    /// The add-back action's path.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The Project.</param>
    /// <param name="statusId">The removed Space default.</param>
    /// <returns>The path.</returns>
    public static string RestoreOf(Guid spaceId, Guid projectId, Guid statusId) => $"{ItemOf(spaceId, projectId, statusId)}/restore";
}
