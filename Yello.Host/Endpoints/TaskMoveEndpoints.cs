using Yello.Application.Tasks.MoveTask;
using Yello.Contracts.Tasks;
using Yello.Domain.Memberships;
using Yello.Host.ApiTokens;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// Move one Task of the active Space to a Status and a position within it. FR-29; story 2.6.
/// </summary>
/// <remarks>
/// <para>
/// <b>No Membership check, no Role, no bound count and no idempotency here</b> - the reasons <see cref="TaskEndpoints"/>
/// gives (AR-3). The pipeline has resolved the Space, checked <c>ManageTasks</c> and replayed a key it had already seen.
/// </para>
/// <para>
/// <b>Session only</b> - the one place the API is deliberately narrower than the browser (FR-35): marked so in the
/// endpoint's metadata, never branched on in the slice (AD-4). <b>The body is read inside the endpoint</b>, never bound.
/// </para>
/// </remarks>
public static class TaskMoveEndpoints
{
    /// <summary>
    /// Registers <c>POST /api/v1/spaces/{spaceId}/tasks/{taskId}/move</c>. Story 2.6.
    /// </summary>
    /// <remarks>
    /// <b>Session only</b> (FR-35, open question 1 (a)): a Task's position is readable over the API and never writable,
    /// so a Token here is answered exactly as no credential. <b>No bound</b>: a move adds no Task, so consulting
    /// Tasks-per-Project would refuse moves in a full Project.
    /// </remarks>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapMoveTask(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(TaskRoutes.Move, MoveAsync)
            .RequiresCapability(Capability.ManageTasks)
            .SessionOnly();

    /// <summary>
    /// Moves one Task of the active Space to a Status and a position, named by the Task it lands after.
    /// </summary>
    /// <param name="taskId">The Task, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request, whose body is read here.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>200</c> with the Task as stored; <c>400 invalid-task-edit</c> or <c>invalid-task-status</c>;
    /// <c>409 task-position-unavailable</c>; or the boundary's <c>404 not-available</c> - byte for byte, and not
    /// recorded - for an id that matches nothing in the active Space.
    /// </returns>
    public static async Task<IResult> MoveAsync(
        Guid taskId,
        MoveTaskHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (statusId, afterTaskId, refusal) = await TaskMoveBody.ReadAsync(httpContext.Request, cancellationToken).ConfigureAwait(false);

        if (refusal is not null)
        {
            return refusal;
        }

        var result = await handler.HandleAsync(taskId, statusId, afterTaskId, cancellationToken).ConfigureAwait(false);

        return result.Outcome switch
        {
            MoveTaskOutcome.Moved => Results.Ok(TaskEndpoints.Response(result.Task!, result.LabelIds)),

            // Not recorded: validation refusals inside the Space, the same bytes for a random id and a foreign one (I-13).
            MoveTaskOutcome.StatusOutsideTheEffectiveSet => TaskEditBody.InvalidStatus(),
            MoveTaskOutcome.PositionUnavailable => TaskMoveBody.PositionUnavailable(),
            MoveTaskOutcome.PositionFull => TaskMoveBody.PositionFull(),

            // Not recorded: an in-Space not-found crossed no boundary (AD-20). The body is the boundary's.
            _ => BoundaryProblems.NotAvailable(),
        };
    }
}
