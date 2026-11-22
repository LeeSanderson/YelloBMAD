using Yello.Application.Tasks.MoveTaskToProject;
using Yello.Contracts.Tasks;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// Move one Task of the active Space into another Project of the same Space.
/// </summary>
/// <remarks>
/// <para>
/// <b>No Membership check, no Role and no idempotency here</b> - the reasons <see cref="TaskEndpoints"/> gives. The pipeline
/// has resolved the Space, checked <c>ManageTasks</c> and replayed a key it had already seen.
/// </para>
/// <para>
/// <b>Both surfaces.</b> The caller names a Project and a Status, never a position, so the API's one exception for positions
/// does not apply. <b>No bound declared</b>: the pipeline counts the Project a route names, and this destination is in the
/// body, so the slice counts it after its own locks and this endpoint answers that count as the pipeline answers its own.
/// </para>
/// </remarks>
public static class TaskProjectMoveEndpoints
{
    private static readonly string LoggerCategory = typeof(TaskProjectMoveEndpoints).FullName!;

    /// <summary>
    /// Registers <c>POST /api/v1/spaces/{spaceId}/tasks/{taskId}/project</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapMoveTaskToProject(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(TaskRoutes.ProjectMove, MoveAsync)
            .RequiresCapability(Capability.ManageTasks);

    /// <summary>
    /// Moves one Task of the active Space into another of its Projects.
    /// </summary>
    /// <param name="spaceId">The active Space, from the route; used only to log a bound reached.</param>
    /// <param name="taskId">The Task, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="loggerFactory">Builds the logger a bound reached is logged on.</param>
    /// <param name="httpContext">The request, whose body is read here.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The answers <see cref="TaskRoutes.ProjectMove"/> lists.</returns>
    public static async Task<IResult> MoveAsync(
        Guid spaceId,
        Guid taskId,
        MoveTaskToProjectHandler handler,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (projectId, statusId, refusal) = await TaskProjectMoveBody.ReadAsync(httpContext.Request, cancellationToken).ConfigureAwait(false);

        if (refusal is not null)
        {
            return refusal;
        }

        var result = await handler.HandleAsync(taskId, projectId, statusId, cancellationToken).ConfigureAwait(false);

        switch (result.Outcome)
        {
            case MoveTaskToProjectOutcome.Moved:
                return Results.Ok(TaskEndpoints.Response(result.Task!, result.LabelIds));

            // Not recorded: validation refusals inside the Space, the same bytes for a random id and a foreign one.
            case MoveTaskToProjectOutcome.ProjectInvalid:
                return TaskProjectMoveBody.InvalidProject();

            case MoveTaskToProjectOutcome.StatusDestinationInvalid:
                return TaskProjectMoveBody.InvalidStatus();

            case MoveTaskToProjectOutcome.StatusDestinationRequired:
                return TaskProjectMoveBody.StatusRequired();

            // The creation's refusal and log line for the same bound, byte for byte; the unit of work rolls back.
            case MoveTaskToProjectOutcome.BoundReached:
                SpaceLog.BoundReachedFor(loggerFactory.CreateLogger(LoggerCategory), spaceId, result.Bound!);

                return BoundaryProblems.BoundReached(result.Bound!);

            // Not recorded: an in-Space not-found crossed no boundary. The body is the boundary's.
            default:
                return BoundaryProblems.NotAvailable();
        }
    }
}
