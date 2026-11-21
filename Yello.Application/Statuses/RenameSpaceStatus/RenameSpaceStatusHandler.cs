using Yello.Domain.Statuses;

namespace Yello.Application.Statuses.RenameSpaceStatus;

/// <summary>
/// Renames one of the Space's default Statuses, with one decision for every Project that gave it a name of its own.
/// </summary>
/// <remarks>
/// <b>Only the default's name is written</b>, and the <c>Rename</c> operations the plan deletes: the Status keeps its id,
/// so every Task, Board column and Project operation that names it keeps working, and every Project that has not renamed
/// it shows the new name on its next read.
/// </remarks>
/// <param name="writer">Changes the defaults under the Space-level Status lock.</param>
public sealed class RenameSpaceStatusHandler(SpaceDefaultStatusWriter writer)
{
    /// <summary>
    /// Renames the default.
    /// </summary>
    /// <param name="statusId">The default, from the route.</param>
    /// <param name="name">The name, already normalised.</param>
    /// <param name="replaceProjectNames">The decision for every conflicting Project, or <c>null</c> when none was sent.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened, and the defaults when the Status was renamed.</returns>
    public Task<SpaceStatusWriteResult> HandleAsync(Guid statusId, string name, bool? replaceProjectNames, CancellationToken cancellationToken) =>
        writer.WriteAsync((defaults, deltas) => SpaceDefaultStatusPlan.Rename(defaults, deltas, statusId, name, replaceProjectNames), cancellationToken);
}
