using Yello.Application.Spaces;
using Yello.Application.Statuses;
using Yello.Application.Statuses.RemoveProjectStatus;
using Yello.Application.Statuses.RestoreProjectStatus;
using Yello.Contracts.Projects;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// Remove a Status from one Project, moving its Tasks, and add a removed Space default back.
/// </summary>
/// <remarks>
/// Apart from <see cref="ProjectStatusEndpoints"/> only for size; the same rules hold: <c>ConfigureProjectStatusDelta</c>,
/// both surfaces, no bound, the body read inside the endpoint, and a Project or Status in the path that is not this
/// Project's answering the boundary's <c>404</c>.
/// </remarks>
public static class ProjectStatusRemovalEndpoints
{
    /// <summary>
    /// Registers <c>POST</c> on one of the Project's Statuses' remove action.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapRemoveProjectStatus(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(ProjectStatusRoutes.Remove, RemoveAsync)
            .RequiresCapability(Capability.ConfigureProjectStatusDelta);

    /// <summary>
    /// Registers <c>POST</c> on one of the Project's removed defaults' add-back action.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapRestoreProjectStatus(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(ProjectStatusRoutes.Restore, RestoreAsync)
            .RequiresCapability(Capability.ConfigureProjectStatusDelta);

    /// <summary>
    /// Removes a Status from the Project, moving every Task that holds it to the destination.
    /// </summary>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="statusId">The Status, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request: its services hold the resolved context, and its body is read here.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>200</c> with the set; <c>400 invalid-status-destination</c>; <c>409 status-destination-required</c> or
    /// <c>status-set-empty</c>; or the boundary's <c>404</c>.
    /// </returns>
    public static async Task<IResult> RemoveAsync(Guid projectId, Guid statusId, RemoveProjectStatusHandler handler, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (await JsonRequestBody.ReadAsync(httpContext.Request, typeof(RemoveProjectStatusRequest), cancellationToken).ConfigureAwait(false) is not RemoveProjectStatusRequest request)
        {
            return ProjectStatusAnswers.Refusal(ProjectStatusWriteOutcome.DestinationInvalid);
        }

        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();
        var result = await handler.HandleAsync(context, projectId, statusId, request.DestinationStatusId, cancellationToken).ConfigureAwait(false);

        return result.Set is { } set ? Results.Ok(ProjectStatusEndpoints.Responses(set)) : ProjectStatusAnswers.Refusal(result.Outcome);
    }

    /// <summary>
    /// Adds a removed Space default back to the Project.
    /// </summary>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="statusId">The removed default, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request: its services hold the resolved context.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>200</c> with the set; <c>409 status-name-taken</c>; or the boundary's <c>404</c>.</returns>
    public static async Task<IResult> RestoreAsync(Guid projectId, Guid statusId, RestoreProjectStatusHandler handler, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();
        var result = await handler.HandleAsync(context, projectId, statusId, cancellationToken).ConfigureAwait(false);

        return result.Set is { } set ? Results.Ok(ProjectStatusEndpoints.Responses(set)) : ProjectStatusAnswers.Refusal(result.Outcome);
    }
}
