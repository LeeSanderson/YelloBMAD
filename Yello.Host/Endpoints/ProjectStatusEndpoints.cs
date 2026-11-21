using Yello.Application.Spaces;
using Yello.Application.Statuses;
using Yello.Application.Statuses.AddProjectStatus;
using Yello.Application.Statuses.ListProjectStatuses;
using Yello.Application.Statuses.PlaceProjectStatus;
using Yello.Application.Statuses.RenameProjectStatus;
using Yello.Contracts.Projects;
using Yello.Domain.Memberships;
using Yello.Domain.Statuses;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// Read one Project's effective Status set, and add, rename and place a Status in it.
/// </summary>
/// <remarks>
/// <para>
/// <b>No Membership check, no Role and no idempotency here</b> - the reasons <see cref="ProjectEndpoints"/> gives (AR-3).
/// Each route carries <c>{spaceId}</c>, so the pipeline has resolved the Space, checked the declared capability and
/// replayed a key it had already seen before this code runs.
/// </para>
/// <para>
/// <b>Reading takes the capability every Role holds</b>; <b>every write takes <c>ConfigureProjectStatusDelta</c></b>, which
/// only Owners and Admins hold. <b>No bound</b>, and <b>both surfaces</b> (AD-4). <b>Bodies are read inside the
/// endpoint</b>, never bound.
/// </para>
/// <para>
/// <b>A Project or a Status in the path that is not this Space's, or not in this Project's set, answers the boundary's
/// <c>404</c></b>, byte for byte, unrecorded (AD-20): another Project's Status is not this Project's, whichever Space holds
/// it.
/// </para>
/// </remarks>
public static class ProjectStatusEndpoints
{
    /// <summary>
    /// Registers <c>GET</c> on the Project's Statuses.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapListProjectStatuses(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(ProjectStatusRoutes.Collection, ListAsync)
            .RequiresCapability(Capability.ReadProjectsAndTasks);

    /// <summary>
    /// Registers <c>POST</c> on the Project's Statuses.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapAddProjectStatus(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(ProjectStatusRoutes.Collection, AddAsync)
            .RequiresCapability(Capability.ConfigureProjectStatusDelta);

    /// <summary>
    /// Registers <c>PATCH</c> on one of the Project's Statuses.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapRenameProjectStatus(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPatch(ProjectStatusRoutes.Item, RenameAsync)
            .RequiresCapability(Capability.ConfigureProjectStatusDelta);

    /// <summary>
    /// Registers <c>POST</c> on one of the Project's Statuses' place action.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapPlaceProjectStatus(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(ProjectStatusRoutes.Place, PlaceAsync)
            .RequiresCapability(Capability.ConfigureProjectStatusDelta);

    /// <summary>
    /// Reads the Project's effective set.
    /// </summary>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>200</c> with the set in effective order, or the boundary's <c>404</c>.</returns>
    public static async Task<IResult> ListAsync(Guid projectId, ListProjectStatusesHandler handler, CancellationToken cancellationToken)
    {
        var set = await handler.HandleAsync(projectId, cancellationToken).ConfigureAwait(false);

        return set is null ? BoundaryProblems.NotAvailable() : Results.Ok(Responses(set));
    }

    /// <summary>
    /// Adds a Status to the Project.
    /// </summary>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request: its services hold the resolved context, and its body is read here.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>201</c> with the set and its <c>Location</c>; <c>400 invalid-status-name</c> or <c>invalid-status-position</c>;
    /// <c>409 status-name-taken</c>; or the boundary's <c>404</c>.
    /// </returns>
    public static async Task<IResult> AddAsync(Guid projectId, AddProjectStatusHandler handler, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var request = await JsonRequestBody.ReadAsync(httpContext.Request, typeof(AddProjectStatusRequest), cancellationToken).ConfigureAwait(false) as AddProjectStatusRequest;

        if (!StatusName.TryNormalise(request?.Name, out var name))
        {
            return ProjectStatusAnswers.InvalidName();
        }

        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();
        var result = await handler.HandleAsync(context, projectId, name, request!.AfterStatusId, cancellationToken).ConfigureAwait(false);

        if (result.Set is not { } set)
        {
            return ProjectStatusAnswers.Refusal(result.Outcome);
        }

        // Set on the response rather than through Results.Created, so the pipeline's idempotency record reads it from one
        // place whatever the endpoint.
        httpContext.Response.Headers.Location = ProjectStatusRoutes.CollectionOf(context.SpaceId, projectId);

        return Results.Json(Responses(set), statusCode: StatusCodes.Status201Created);
    }

    /// <summary>
    /// Renames a Status in the Project.
    /// </summary>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="statusId">The Status, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request: its services hold the resolved context, and its body is read here.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>200</c> with the set; <c>400 invalid-status-name</c>; <c>409 status-name-taken</c>; or the boundary's <c>404</c>.
    /// </returns>
    public static async Task<IResult> RenameAsync(Guid projectId, Guid statusId, RenameProjectStatusHandler handler, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var request = await JsonRequestBody.ReadAsync(httpContext.Request, typeof(RenameProjectStatusRequest), cancellationToken).ConfigureAwait(false) as RenameProjectStatusRequest;

        if (!StatusName.TryNormalise(request?.Name, out var name))
        {
            return ProjectStatusAnswers.InvalidName();
        }

        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();
        var result = await handler.HandleAsync(context, projectId, statusId, name, cancellationToken).ConfigureAwait(false);

        return result.Set is { } set ? Results.Ok(Responses(set)) : ProjectStatusAnswers.Refusal(result.Outcome);
    }

    /// <summary>
    /// Places a Status of the Project first or after another.
    /// </summary>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="statusId">The Status, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request: its services hold the resolved context, and its body is read here.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>200</c> with the set; <c>400 invalid-status-position</c>; or the boundary's <c>404</c>.</returns>
    public static async Task<IResult> PlaceAsync(Guid projectId, Guid statusId, PlaceProjectStatusHandler handler, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (await JsonRequestBody.ReadAsync(httpContext.Request, typeof(PlaceProjectStatusRequest), cancellationToken).ConfigureAwait(false) is not PlaceProjectStatusRequest request)
        {
            return ProjectStatusAnswers.Refusal(ProjectStatusWriteOutcome.PositionInvalid);
        }

        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();
        var result = await handler.HandleAsync(context, projectId, statusId, request.AfterStatusId, cancellationToken).ConfigureAwait(false);

        return result.Set is { } set ? Results.Ok(Responses(set)) : ProjectStatusAnswers.Refusal(result.Outcome);
    }

    private static List<ProjectStatusResponse> Responses(ProjectStatusSet set) =>
        [.. set.Statuses.Select(status => new ProjectStatusResponse(
            status.Id,
            status.Name,
            status.Source == StatusSource.AddedHere ? ProjectStatusSources.AddedHere : ProjectStatusSources.SpaceDefault,
            status.DefaultName,
            set.TaskCounts.GetValueOrDefault(status.Id)))];
}
