using Yello.Application.Spaces;
using Yello.Application.Tasks.EditTaskDescription;
using Yello.Application.Tasks.ViewTaskDescription;
using Yello.Contracts.Tasks;
using Yello.Domain.Memberships;
using Yello.Domain.Tasks;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// One Task's description: read the projection and the state an editor starts from, and send a batch of merge changes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Changes in, never text.</b> The change route carries encoded merge changes and the editor's version; its answer lists
/// what was refused, the server's version and what the editor lacks. Text leaves the server only as the read's projection.
/// </para>
/// <para>
/// <b>No Role, no bound and no idempotency here</b> - the reasons <see cref="TaskEndpoints"/> gives. A Viewer is refused by
/// the pipeline before the handler, so nothing is written. Both surfaces.
/// </para>
/// <para>
/// <b>Nothing here logs a payload, a description or a title.</b>
/// </para>
/// </remarks>
public static class TaskDescriptionEndpoints
{
    /// <summary>
    /// Registers <c>GET /api/v1/spaces/{spaceId}/tasks/{taskId}/description</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapViewTaskDescription(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(TaskRoutes.Description, ViewAsync)
            .RequiresCapability(Capability.ReadProjectsAndTasks);

    /// <summary>
    /// Registers <c>POST /api/v1/spaces/{spaceId}/tasks/{taskId}/description/changes</c>.
    /// </summary>
    /// <remarks>
    /// <b>No bound</b>: a change adds no Task, Project or Label.
    /// </remarks>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapEditTaskDescription(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(TaskRoutes.DescriptionChanges, EditAsync)
            .RequiresCapability(Capability.EditTaskDescriptionCollaboratively);

    /// <summary>
    /// Reads one Task's description.
    /// </summary>
    /// <param name="taskId">The Task, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>200</c> with the description, or the boundary's <c>404 not-available</c> - byte for byte, and not recorded - for an
    /// id that matches nothing in the active Space.
    /// </returns>
    public static async Task<IResult> ViewAsync(Guid taskId, ViewTaskDescriptionHandler handler, CancellationToken cancellationToken)
    {
        var read = await handler.HandleAsync(taskId, cancellationToken).ConfigureAwait(false);

        return read is null
            ? BoundaryProblems.NotAvailable()
            : Results.Ok(new TaskDescriptionResponse(read.Text, read.State, read.Version));
    }

    /// <summary>
    /// Admits a batch of changes to one Task's description.
    /// </summary>
    /// <param name="taskId">The Task, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request: its services hold the context the pipeline resolved, and its body is read here.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>200</c> with what the batch came to, even when every change is refused; <c>400 invalid-description-changes</c> or
    /// <c>400 description-batch-too-large</c>; or the boundary's <c>404 not-available</c>, not recorded, for a Task that is
    /// not there.
    /// </returns>
    public static async Task<IResult> EditAsync(
        Guid taskId,
        EditTaskDescriptionHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (batch, refusal) = await TaskDescriptionBody.ReadAsync(httpContext.Request, cancellationToken).ConfigureAwait(false);

        if (refusal is not null)
        {
            return refusal;
        }

        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();
        var append = await handler.HandleAsync(context, taskId, batch!, cancellationToken).ConfigureAwait(false);

        return append.Outcome switch
        {
            DescriptionAppendOutcome.Applied => Results.Ok(new TaskDescriptionChangesResponse(append.Refused, append.Version, append.Missing)),

            DescriptionAppendOutcome.InvalidVersion => TaskDescriptionBody.Invalid(),

            // Not recorded: an in-Space not-found crossed no boundary. The body is the boundary's.
            _ => BoundaryProblems.NotAvailable(),
        };
    }
}
