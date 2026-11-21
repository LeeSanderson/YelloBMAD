using Yello.Application.Spaces;

namespace Yello.Application.Statuses.RemoveProjectStatus;

/// <summary>
/// Takes a Status out of one Project's set, moving every Task that holds it to a destination in the same transaction.
/// </summary>
/// <remarks>
/// <b>Every decision is made under the Project's lock</b>, in a fixed order: a Status outside the set is not found; the last
/// Status is never removed, whatever the destination; a destination is checked before occupancy, so a request means one
/// thing however many Tasks the Status holds; and occupancy is counted after the lock, so a Task that arrived after the
/// caller read its counts is migrated or the request is refused, never stranded.
/// </remarks>
/// <param name="writer">Writes the delta under the Project's lock.</param>
public sealed class RemoveProjectStatusHandler(ProjectStatusDeltaWriter writer)
{
    /// <summary>
    /// Removes the Status.
    /// </summary>
    /// <param name="context">The active Space, as the pipeline resolved it on this request.</param>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="statusId">The Status, from the route.</param>
    /// <param name="destinationStatusId">Where its Tasks go, or <c>null</c>.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened, and the Project's set when the Status was removed.</returns>
    public Task<ProjectStatusWriteResult> HandleAsync(ActiveSpaceContext context, Guid projectId, Guid statusId, Guid? destinationStatusId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return writer.WriteAsync(context.SpaceId, projectId, (draft, token) => PlanAsync(draft, statusId, destinationStatusId, token), cancellationToken);
    }

    private static async Task<ProjectStatusWriteOutcome> PlanAsync(ProjectStatusDeltaDraft draft, Guid statusId, Guid? destinationStatusId, CancellationToken cancellationToken)
    {
        if (!draft.Holds(statusId))
        {
            return ProjectStatusWriteOutcome.NotFound;
        }

        if (draft.Before.Count == 1)
        {
            return ProjectStatusWriteOutcome.SetEmpty;
        }

        // The set the removal leaves is the set before it without the removed Status: re-anchoring keeps the rest.
        if (destinationStatusId is { } destination && (destination == statusId || !draft.Holds(destination)))
        {
            return ProjectStatusWriteOutcome.DestinationInvalid;
        }

        if (destinationStatusId is null && await draft.OccupantsAsync(statusId, cancellationToken).ConfigureAwait(false) > 0)
        {
            return ProjectStatusWriteOutcome.DestinationRequired;
        }

        draft.Remove(statusId, destinationStatusId);

        return ProjectStatusWriteOutcome.Written;
    }
}
