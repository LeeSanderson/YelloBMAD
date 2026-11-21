using Yello.Application.Spaces;

namespace Yello.Application.Statuses.PlaceProjectStatus;

/// <summary>
/// Moves a Status of one Project's set to the front, or right after another of its Statuses.
/// </summary>
/// <param name="writer">Writes the delta under the Project's lock.</param>
public sealed class PlaceProjectStatusHandler(ProjectStatusDeltaWriter writer)
{
    /// <summary>
    /// Places the Status.
    /// </summary>
    /// <param name="context">The active Space, as the pipeline resolved it on this request.</param>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="statusId">The Status, from the route.</param>
    /// <param name="afterStatusId">The Status it goes after, or <c>null</c> for first.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened, and the Project's set when the Status was placed.</returns>
    public Task<ProjectStatusWriteResult> HandleAsync(ActiveSpaceContext context, Guid projectId, Guid statusId, Guid? afterStatusId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return writer.WriteAsync(context.SpaceId, projectId, draft => Plan(draft, statusId, afterStatusId), cancellationToken);
    }

    private static ProjectStatusWriteOutcome Plan(ProjectStatusDeltaDraft draft, Guid statusId, Guid? afterStatusId)
    {
        if (!draft.Holds(statusId))
        {
            return ProjectStatusWriteOutcome.NotFound;
        }

        if (afterStatusId is { } anchor && (anchor == statusId || !draft.Holds(anchor)))
        {
            return ProjectStatusWriteOutcome.PositionInvalid;
        }

        draft.Place(statusId, afterStatusId);

        return ProjectStatusWriteOutcome.Written;
    }
}
