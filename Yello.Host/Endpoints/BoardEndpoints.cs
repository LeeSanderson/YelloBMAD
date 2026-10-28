using Yello.Application.Boards.ViewBoard;
using Yello.Application.Boards.ViewBoardColumn;
using Yello.Contracts;
using Yello.Contracts.Boards;
using Yello.Domain.Memberships;
using Yello.Domain.Tasks;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// Read one Project's Board, and one page of one of its columns. FR-28; stories 2.2 and 2.9.
/// </summary>
/// <remarks>
/// <para>
/// <b>The read capability every Role holds</b>, so a Viewer is served byte for byte the Board a Member is
/// (AC3's server half): Role changes what can be done to a Board, never what it shows. Both surfaces. The column page
/// (story 2.9) is the same read, one column further: same capability, same boundary.
/// </para>
/// <para>
/// <b>The Board answers each column's first page</b> (story 2.9, AC9, open question 1 (a)) with its cursor
/// (<c>nextAfter</c>), and the column-page route answers the page after a cursor - each at most
/// <see cref="Application.Boards.BoardPage.Size"/> Tasks, the server's size and never the client's.
/// </para>
/// </remarks>
public static class BoardEndpoints
{
    /// <summary>
    /// Registers <c>GET /api/v1/spaces/{spaceId}/projects/{projectId}/board</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapBoard(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(BoardRoutes.Board, ReadAsync)
            .RequiresCapability(Capability.ReadProjectsAndTasks);

    /// <summary>
    /// Registers <c>GET /api/v1/spaces/{spaceId}/projects/{projectId}/board/columns/{statusId}?after={key}</c>. Story 2.9.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapBoardColumn(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(BoardRoutes.Column, ReadColumnAsync)
            .RequiresCapability(Capability.ReadProjectsAndTasks);

    /// <summary>
    /// Reads one page of one column.
    /// </summary>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="statusId">The column's Status, from the route.</param>
    /// <param name="after">The cursor, from the query: the last loaded Task's position key; absent for the first page.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>200</c> with the page; <c>400 invalid-board-cursor</c> for a cursor that is not a position key - checked first,
    /// because it depends only on the request; or the boundary's <c>404 not-available</c> - byte for byte, and not
    /// recorded - for a Project id that matches nothing in the active Space or a Status outside its effective set.
    /// </returns>
    public static async Task<IResult> ReadColumnAsync(
        Guid projectId,
        Guid statusId,
        string? after,
        ViewBoardColumnHandler handler,
        CancellationToken cancellationToken)
    {
        if (after is not null && !PositionKey.IsWellFormed(after))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: ProblemTypes.InvalidBoardCursor,
                title: "A Board cursor is a position key.");
        }

        var page = await handler.HandleAsync(projectId, statusId, after, cancellationToken).ConfigureAwait(false);

        if (page is null)
        {
            // Not recorded: an in-Space not-found crossed no boundary (AD-20). The body is the boundary's.
            return BoundaryProblems.NotAvailable();
        }

        return Results.Ok(new BoardColumnPageResponse(
            page.StatusId,
            page.Total,
            [.. page.Tasks.Select(task => TaskEndpoints.Response(task, page.LabelIds.GetValueOrDefault(task.Id, [])))],
            page.NextAfter));
    }

    /// <summary>
    /// Reads the Board.
    /// </summary>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>200</c> with the Board, or the boundary's <c>404 not-available</c> - byte for byte, and not
    /// recorded - for a Project id that matches nothing in the active Space.
    /// </returns>
    public static async Task<IResult> ReadAsync(Guid projectId, ViewBoardHandler handler, CancellationToken cancellationToken)
    {
        var board = await handler.HandleAsync(projectId, cancellationToken).ConfigureAwait(false);

        if (board is null)
        {
            // Not recorded: an in-Space not-found crossed no boundary (AD-20). The body is the boundary's.
            return BoundaryProblems.NotAvailable();
        }

        var response = new BoardResponse(
            new BoardProjectResponse(board.Project.Id, board.Project.Name),
            [.. board.Columns.Select(column => new BoardColumnResponse(
                column.Status.Id,
                column.Status.Name,
                column.Total,
                [.. column.Tasks.Select(task => TaskEndpoints.Response(task, board.LabelIds.GetValueOrDefault(task.Id, [])))],
                column.NextAfter))]);

        return Results.Ok(response);
    }
}
