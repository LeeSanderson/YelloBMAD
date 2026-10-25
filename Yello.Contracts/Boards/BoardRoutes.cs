namespace Yello.Contracts.Boards;

/// <summary>
/// The Board endpoint's path, stated once for the client and the Host. FR-28; story 2.2, open question 5 (a).
/// </summary>
/// <remarks>
/// <b>One read per Board</b>, answering every column with its true total, so the client never derives a
/// count (AC5). Story 2.9 pages the Tasks; the shape of the totals does not change.
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
}
