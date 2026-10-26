using Yello.Domain.Labels;

namespace Yello.Application.Labels.ListLabels;

/// <summary>
/// Lists every Label defined in the active Space, by name then id. FR-22; story 2.4, AC1, AC7.
/// </summary>
/// <remarks>
/// <b>The same list for every Role</b>: a Viewer sees the chips a Member applied, so reading Labels takes the read
/// capability every Role holds, which the pipeline checked. This slice reads no Role and no Space.
/// </remarks>
/// <param name="store">Reads the Labels of the Space the unit of work is scoped to.</param>
public sealed class ListLabelsHandler(ILabelStore store)
{
    /// <summary>
    /// Lists the active Space's Labels.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Labels, by name then id.</returns>
    public Task<IReadOnlyList<Label>> HandleAsync(CancellationToken cancellationToken) =>
        store.ListAsync(cancellationToken);
}
