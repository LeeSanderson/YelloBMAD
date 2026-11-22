using Yello.Application.Tasks.MoveStatusTasksToProject;
using Yello.Contracts.Projects;
using Yello.Contracts.Tasks;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// Move every Task of one Status of a Project into another Project of the active Space.
/// </summary>
/// <remarks>
/// <para>
/// <b>No Membership check, no Role and no idempotency here</b> - the reasons <see cref="TaskEndpoints"/> gives. The pipeline
/// has resolved the Space, checked <c>ManageTasks</c> and replayed a key it had already seen.
/// </para>
/// <para>
/// <b>Both surfaces</b>, on the single move's terms: the caller names a Project and a Status, never a position. <b>No bound
/// declared</b>: the route names the source, which a move out never fills, and the destination is in the body, so the slice
/// counts it after its own locks and this endpoint answers that count as the pipeline answers its own.
/// </para>
/// <para>
/// <b>Only <see cref="MoveStatusTasksToProjectOutcome.NotFound"/> answers the boundary <c>404</c></b>; an outcome this switch
/// does not name throws, so a new one can never be answered as a missing source by default.
/// </para>
/// </remarks>
public static class StatusTasksProjectMoveEndpoints
{
    private static readonly string LoggerCategory = typeof(StatusTasksProjectMoveEndpoints).FullName!;

    /// <summary>
    /// Registers <c>POST /api/v1/spaces/{spaceId}/projects/{projectId}/statuses/{statusId}/tasks/project</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapMoveStatusTasksToProject(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(ProjectStatusRoutes.TasksToProject, MoveAsync)
            .RequiresCapability(Capability.ManageTasks);

    /// <summary>
    /// Moves every Task of one Status of a Project into another Project of the active Space.
    /// </summary>
    /// <param name="spaceId">The active Space, from the route; used only to log a bound reached.</param>
    /// <param name="projectId">The source Project, from the route.</param>
    /// <param name="statusId">The source Status, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="loggerFactory">Builds the logger a bound reached is logged on.</param>
    /// <param name="httpContext">The request, whose body is read here.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The answers <see cref="ProjectStatusRoutes.TasksToProject"/> lists.</returns>
    public static async Task<IResult> MoveAsync(
        Guid spaceId,
        Guid projectId,
        Guid statusId,
        MoveStatusTasksToProjectHandler handler,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (destinationId, destinationStatusId, refusal) = await TaskProjectMoveBody.ReadBulkAsync(httpContext.Request, cancellationToken).ConfigureAwait(false);

        if (refusal is not null)
        {
            return refusal;
        }

        var result = await handler.HandleAsync(projectId, statusId, destinationId, destinationStatusId, cancellationToken).ConfigureAwait(false);

        switch (result.Outcome)
        {
            case MoveStatusTasksToProjectOutcome.Moved:
                return Results.Ok(new MoveStatusTasksToProjectResponse(destinationId, result.StatusId, result.MovedCount));

            // Not recorded: validation refusals inside the Space, the single move's bytes for the same causes.
            case MoveStatusTasksToProjectOutcome.ProjectInvalid:
                return TaskProjectMoveBody.InvalidProject();

            case MoveStatusTasksToProjectOutcome.StatusDestinationInvalid:
                return TaskProjectMoveBody.InvalidStatus();

            case MoveStatusTasksToProjectOutcome.StatusDestinationRequired:
                return TaskProjectMoveBody.StatusRequired();

            // The creation's refusal and log line for the same bound, byte for byte; the unit of work rolls back.
            case MoveStatusTasksToProjectOutcome.BoundReached:
                SpaceLog.BoundReachedFor(loggerFactory.CreateLogger(LoggerCategory), spaceId, result.Bound!);

                return BoundaryProblems.BoundReached(result.Bound!);

            // Not recorded: an in-Space not-found crossed no boundary. The body is the boundary's.
            case MoveStatusTasksToProjectOutcome.NotFound:
                return BoundaryProblems.NotAvailable();

            default:
                throw new InvalidOperationException($"The bulk move answered an outcome the endpoint does not map: {result.Outcome}.");
        }
    }
}
