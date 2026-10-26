using Yello.Domain.Labels;

namespace Yello.Application.Labels.DeleteLabel;

/// <summary>
/// Deletes a Label of the active Space, unless a Task carries it. FR-22; story 2.4, AC3, AC10.
/// </summary>
/// <remarks>
/// <para>
/// <b>Refused while in use, never stripped</b> (open question 11): the deletion is refused until the applications are
/// removed, so no Task ever references a Label that does not exist. The store decides it under a lock, because an
/// application can arrive between any check here and the delete.
/// </para>
/// <para>
/// <b>No Role, no Space</b> (AR-3): the pipeline resolved the Space and checked <c>ManageLabels</c>. Another Space's
/// Label id matches nothing here, exactly like an id that never existed.
/// </para>
/// </remarks>
/// <param name="store">Deletes the Label.</param>
public sealed class DeleteLabelHandler(ILabelStore store)
{
    /// <summary>
    /// Deletes the Label.
    /// </summary>
    /// <param name="labelId">The Label, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the deletion came to.</returns>
    public Task<LabelDeletion> HandleAsync(Guid labelId, CancellationToken cancellationToken) =>
        store.DeleteAsync(labelId, cancellationToken);
}
