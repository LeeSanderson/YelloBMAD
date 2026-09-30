using Yello.Domain.Statuses;

namespace Yello.Application.Statuses.ListStatuses;

/// <summary>
/// Lists the active Space's Statuses - the product's first Space-scoped read.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this slice exists in story 1.5.</b> The resolution pipeline needs a real Space-scoped
/// surface to be proved against, and a route that does not exist answers 404 - which is exactly
/// AC1's refusal, so a test against a missing route would pass for the wrong reason. The Board is
/// drawn in Status order, so this is the read Epic 2's Board needs first rather than a surface
/// invented for a test.
/// </para>
/// <para>
/// <b>Nothing cross-cutting is re-implemented here</b> (AR-3). No Space resolution and no Membership
/// check: the pipeline has done both before this runs, and every Role may read a Space's Statuses,
/// so there is no capability to check either. The FR-16 matrix is story 1.6's.
/// </para>
/// </remarks>
/// <param name="reader">Reads the Statuses of the Space the unit of work is scoped to.</param>
public sealed class ListStatusesHandler(IStatusDefinitionReader reader)
{
    /// <summary>
    /// Lists the active Space's Statuses.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Statuses, in Board order.</returns>
    public Task<IReadOnlyList<StatusDefinition>> HandleAsync(CancellationToken cancellationToken) =>
        reader.ListAsync(cancellationToken);
}
