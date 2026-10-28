namespace Yello.Contracts.Boards;

/// <summary>
/// The Board endpoint's path, stated once for the client and the Host. FR-28; story 2.2, open question 5 (a).
/// </summary>
/// <remarks>
/// <b>One read per Board</b>, answering every column with its true total and its first page, so the client never
/// derives a count (AC5). Story 2.9 pages the Tasks: <see cref="Column"/> reads the page after a cursor, one bounded
/// keyset seek per request (AD-29), and the server owns the page size - there is no size parameter.
/// </remarks>
public static class BoardRoutes
{
    /// <summary>
    /// <c>GET</c> here for one Project's <see cref="BoardResponse"/>.
    /// </summary>
    public static string Board => "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/board";

    /// <summary>
    /// One Project's Board path, as a client calls it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The Project.</param>
    /// <returns>The path.</returns>
    public static string BoardOf(Guid spaceId, Guid projectId) => $"/api/v1/spaces/{spaceId:D}/projects/{projectId:D}/board";

    /// <summary>
    /// <c>GET</c> here, with an optional <c>after</c> cursor, for one column's <see cref="BoardColumnPageResponse"/> (story
    /// 2.9, open question 1 (a)).
    /// </summary>
    public static string Column => "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/board/columns/{statusId:guid}";

    /// <summary>
    /// One column's first page path, as a client calls it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The Project.</param>
    /// <param name="statusId">The column's Status.</param>
    /// <returns>The path.</returns>
    public static string ColumnOf(Guid spaceId, Guid projectId, Guid statusId) =>
        $"/api/v1/spaces/{spaceId:D}/projects/{projectId:D}/board/columns/{statusId:D}";

    /// <summary>
    /// One column's page path after a cursor, as a client calls it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The Project.</param>
    /// <param name="statusId">The column's Status.</param>
    /// <param name="after">The cursor - the last loaded Task's position key.</param>
    /// <returns>The path.</returns>
    public static string ColumnOf(Guid spaceId, Guid projectId, Guid statusId, string after) =>
        ColumnOf(spaceId, projectId, statusId) + "?after=" + Uri.EscapeDataString(after);
}
