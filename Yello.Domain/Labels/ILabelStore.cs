namespace Yello.Domain.Labels;

/// <summary>
/// Lists, defines and deletes the Labels of the Space the current unit of work is scoped to. FR-22; story 2.4.
/// </summary>
/// <remarks>
/// <b>Never asked about another Space</b>, for the reason <c>ITaskStore</c> gives: both isolation layers are already
/// narrowed, so another Space's Label is invisible here exactly as one that never existed is. Definition takes the
/// resolved Space only to stamp it on the row.
/// </remarks>
public interface ILabelStore
{
    /// <summary>
    /// Every Label in the resolved Space, by name and then by id.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Labels, materialised.</returns>
    Task<IReadOnlyList<Label>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Defines a Label in the resolved Space.
    /// </summary>
    /// <param name="spaceId">The resolved Space, from the pipeline's context and never from the request.</param>
    /// <param name="name">The name, already normalised.</param>
    /// <param name="colour">The colour, already a palette key.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Label, or <c>null</c> when the Space already holds a Label of that name.</returns>
    Task<Label?> CreateAsync(Guid spaceId, string name, string colour, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a Label of the resolved Space, unless a Task carries it (AC3).
    /// </summary>
    /// <param name="labelId">The Label, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the deletion came to.</returns>
    Task<LabelDeletion> DeleteAsync(Guid labelId, CancellationToken cancellationToken);
}

/// <summary>
/// What a Label deletion came to.
/// </summary>
public enum LabelDeletion
{
    /// <summary>
    /// The Label was deleted.
    /// </summary>
    Deleted,

    /// <summary>
    /// No Label in the resolved Space has the id.
    /// </summary>
    NotFound,

    /// <summary>
    /// At least one Task carries the Label; nothing was deleted.
    /// </summary>
    InUse,
}
