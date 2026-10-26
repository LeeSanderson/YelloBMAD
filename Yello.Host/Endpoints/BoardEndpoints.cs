using Yello.Application.Boards.ViewBoard;
using Yello.Contracts.Boards;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// Read one Project's Board. FR-28; story 2.2.
/// </summary>
/// <remarks>
/// <b>The read capability every Role holds</b>, so a Viewer is served byte for byte the Board a Member is
/// (AC3's server half): Role changes what can be done to a Board, never what it shows. Both surfaces.
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
                [.. column.Tasks.Select(task => TaskEndpoints.Response(task, board.LabelIds.GetValueOrDefault(task.Id, [])))]))]);

        return Results.Ok(response);
    }
}
