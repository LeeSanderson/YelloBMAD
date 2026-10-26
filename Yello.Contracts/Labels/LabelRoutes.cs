namespace Yello.Contracts.Labels;

/// <summary>
/// The Label endpoints' paths, stated once for the client that calls them and the Host that serves them. FR-22;
/// story 2.4.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>{spaceId}</c> first after the version</b> (AR-9), so the pipeline resolves every one of these because of the
/// route's shape.
/// </para>
/// <para>
/// <b>Under the Space, never a Project</b>: a Label is available to every Project in its Space (FR-22). Applying one
/// has no route of its own - it is the Task edit's <c>labelIds</c> member (open question 1 (a)) - and no route renames
/// or recolours one (open question 10).
/// </para>
/// </remarks>
public static class LabelRoutes
{
    /// <summary>
    /// The route parameter naming one Label.
    /// </summary>
    public static string LabelIdParameter => "labelId";

    /// <summary>
    /// <c>GET</c> here for every Label in the Space, by name then id, as <see cref="LabelResponse"/> rows.
    /// </summary>
    public static string List => "/api/v1/spaces/{spaceId:guid}/labels";

    /// <summary>
    /// <c>POST</c> a <see cref="CreateLabelRequest"/> here: <c>201</c> with the <see cref="LabelResponse"/> and its
    /// <c>Location</c>.
    /// </summary>
    public static string Create => List;

    /// <summary>
    /// One Label: <c>DELETE</c> it here for <c>204</c>, or <c>409 label-in-use</c> while a Task carries it.
    /// </summary>
    public static string Label => "/api/v1/spaces/{spaceId:guid}/labels/{labelId:guid}";

    /// <summary>
    /// The list and create path for one Space, as a client calls it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The path.</returns>
    public static string LabelsOf(Guid spaceId) => $"/api/v1/spaces/{spaceId:D}/labels";

    /// <summary>
    /// One Label's path, as a client calls it and as a creation's <c>Location</c> states it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="labelId">The Label.</param>
    /// <returns>The path.</returns>
    public static string LabelOf(Guid spaceId, Guid labelId) => $"/api/v1/spaces/{spaceId:D}/labels/{labelId:D}";
}
