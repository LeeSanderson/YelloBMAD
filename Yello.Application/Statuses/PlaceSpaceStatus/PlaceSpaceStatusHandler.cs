using Yello.Domain.Statuses;

namespace Yello.Application.Statuses.PlaceSpaceStatus;

/// <summary>
/// Moves one of the Space's default Statuses first or right after another default.
/// </summary>
/// <remarks>
/// <b>Only the defaults' positions are written.</b> A Project that never placed the Status follows the new Space order on
/// its next read; one that placed it keeps its own placement, because its place names an anchor, not an ordinal.
/// </remarks>
/// <param name="writer">Changes the defaults under the Space-level Status lock.</param>
public sealed class PlaceSpaceStatusHandler(SpaceDefaultStatusWriter writer)
{
    /// <summary>
    /// Places the default.
    /// </summary>
    /// <param name="statusId">The default, from the route.</param>
    /// <param name="afterStatusId">Another default it goes after, or <c>null</c> for first.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened, and the defaults when the Status was placed.</returns>
    public Task<SpaceStatusWriteResult> HandleAsync(Guid statusId, Guid? afterStatusId, CancellationToken cancellationToken) =>
        writer.WriteAsync((defaults, _) => SpaceDefaultStatusPlan.Place(defaults, statusId, afterStatusId), cancellationToken);
}
