using Yello.Application.Spaces;
using Yello.Domain.Tasks;

namespace Yello.Application.Tasks.EditTaskDescription;

/// <summary>
/// Admits a batch of encoded merge changes to one Task's description in the active Space: each change is admitted or
/// refused on its own, the newly admitted ones are appended to the log, and the projection is rewritten in the same
/// transaction.
/// </summary>
/// <remarks>
/// <para>
/// <b>Changes, never text.</b> The server never takes a client's text as the description; it applies each change through
/// the merge, so two editors writing at once both keep their changes. The REST route reaches this slice today, and the
/// sync channel's frames are to reach the same slice.
/// </para>
/// <para>
/// <b>No Role here</b>: the pipeline resolved the Space and checked the collaborative-edit capability before this ran.
/// </para>
/// </remarks>
/// <param name="log">The description log.</param>
public sealed class EditTaskDescriptionHandler(ITaskDescriptionLog log)
{
    /// <summary>
    /// Admits the batch.
    /// </summary>
    /// <param name="context">The active Space and the Account acting, as the pipeline resolved them.</param>
    /// <param name="taskId">The Task, from the route.</param>
    /// <param name="batch">The encoded changes, in the order the client made them, and the client replica's version.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the batch came to.</returns>
    public Task<DescriptionAppend> HandleAsync(
        ActiveSpaceContext context,
        Guid taskId,
        DescriptionBatch batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return log.AppendAsync(context.SpaceId, taskId, context.AccountId, batch, cancellationToken);
    }
}
