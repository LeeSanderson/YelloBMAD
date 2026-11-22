using Yello.Application.Spaces;

namespace Yello.Application.Statuses.RestoreProjectStatus;

/// <summary>
/// Adds a Space default this Project removed back to its set, under the same id.
/// </summary>
/// <remarks>
/// Only a default the Project's own delta removes can come back this way: anything else in the path, a default still in
/// the set, a Status the Project added or another Space's, is not found, and so is a default the Space itself has since
/// removed. A Status the Project added is gone once removed, and its name is added again through the add route.
/// </remarks>
/// <param name="writer">Writes the delta under the Project's lock.</param>
public sealed class RestoreProjectStatusHandler(ProjectStatusDeltaWriter writer)
{
    /// <summary>
    /// Adds the default back.
    /// </summary>
    /// <param name="context">The active Space, as the pipeline resolved it on this request.</param>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="statusId">The removed default, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened, and the Project's set when the default is back.</returns>
    public Task<ProjectStatusWriteResult> HandleAsync(ActiveSpaceContext context, Guid projectId, Guid statusId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return writer.WriteAsync(context.SpaceId, projectId, draft => Plan(draft, statusId), cancellationToken);
    }

    private static ProjectStatusWriteOutcome Plan(ProjectStatusDeltaDraft draft, Guid statusId)
    {
        if (!draft.HasRemoved(statusId) || !draft.IsDefault(statusId))
        {
            return ProjectStatusWriteOutcome.NotFound;
        }

        draft.Restore(statusId);

        return ProjectStatusWriteOutcome.Written;
    }
}
