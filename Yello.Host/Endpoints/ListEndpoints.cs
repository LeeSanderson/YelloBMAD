using Yello.Application.Lists.ViewList;
using Yello.Contracts;
using Yello.Contracts.Boards;
using Yello.Contracts.Lists;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// Read one page of a Project's List View. FR-30 (partial: the Assignee completes in 4.6); story 2.10, AC1, AC3, AC11,
/// AC12, AC15, AC16, AC22, open questions 1 (a) and 4 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>The read capability every Role holds</b>, so a Viewer is served byte for byte the List a Member is (AC16), on both
/// surfaces. Nothing here writes.
/// </para>
/// <para>
/// <b>The request is checked in a stated order, each check on what was sent alone</b> (Task 4 (c)): the query first -
/// <c>400 invalid-list-query</c> - then the cursor against the requested sort - <c>400 invalid-list-cursor</c> - and only
/// then does the handler run, whose "no such Project" is the boundary's <c>404 not-available</c>, byte for byte, and not
/// recorded (AD-3, AD-20). So no refusal ever depends on what is stored.
/// </para>
/// <para>
/// <b>No size parameter is read</b> (AC12): a <c>limit</c>, <c>pageSize</c> or anything else the route does not name is
/// ignored, and every page is at most <c>ListPage.Size</c> Tasks.
/// </para>
/// </remarks>
public static class ListEndpoints
{
    /// <summary>
    /// Registers <c>GET /api/v1/spaces/{spaceId}/projects/{projectId}/list</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapList(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(ListRoutes.List, ReadAsync)
            .RequiresCapability(Capability.ReadProjectsAndTasks);

    /// <summary>
    /// Reads one page.
    /// </summary>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="request">The request, whose query names the sort, the filters and the cursor.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>200</c> with the page; <c>400 invalid-list-query</c>; <c>400 invalid-list-cursor</c>; or the boundary's
    /// <c>404 not-available</c> for a Project id that matches nothing in the active Space.
    /// </returns>
    public static Task<IResult> ReadAsync(Guid projectId, HttpRequest request, ViewListHandler handler, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(handler);

        return ReadPageAsync(projectId, request.Query, handler, cancellationToken);
    }

    private static async Task<IResult> ReadPageAsync(Guid projectId, IQueryCollection query, ViewListHandler handler, CancellationToken cancellationToken)
    {
        if (!ListQueryReading.TryReadQuery(query, out var sort, out var filter))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: ProblemTypes.InvalidListQuery,
                title: "A List query names an offered sort, Status and Label ids, and due bounds with an offset.");
        }

        if (!ListQueryReading.TryReadCursor(query[ListRoutes.AfterParameter], sort, out var after))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: ProblemTypes.InvalidListCursor,
                title: "A List cursor is one this route issued for the same sort.");
        }

        var page = await handler.HandleAsync(projectId, sort, filter, after, cancellationToken).ConfigureAwait(false);

        if (page is null)
        {
            // Not recorded: an in-Space not-found crossed no boundary (AD-20). The body is the boundary's.
            return BoundaryProblems.NotAvailable();
        }

        return Results.Ok(new ListPageResponse(
            new BoardProjectResponse(page.Project.Id, page.Project.Name),
            [.. page.Statuses.Select(status => new ListStatusResponse(status.Id, status.Name))],
            page.Total,
            [.. page.Tasks.Select(task => TaskEndpoints.Response(task, page.LabelIds.GetValueOrDefault(task.Id, [])))],
            page.NextAfter));
    }
}
