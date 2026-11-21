using Yello.Application.Spaces;

namespace Yello.Application.Statuses.RenameProjectStatus;

/// <summary>
/// Renames a Status in one Project: a default or one the Project added, by the same operation either way.
/// </summary>
/// <remarks>
/// <b>The Space's definition is never written</b>: the name lives in the Project's delta, so every other Project, the
/// Space's own read and every Task's <c>StatusId</c> are untouched.
/// </remarks>
/// <param name="writer">Writes the delta under the Project's lock.</param>
public sealed class RenameProjectStatusHandler(ProjectStatusDeltaWriter writer)
{
    /// <summary>
    /// Renames the Status.
    /// </summary>
    /// <param name="context">The active Space, as the pipeline resolved it on this request.</param>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="statusId">The Status, from the route.</param>
    /// <param name="name">The name, already normalised.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened, and the Project's set when the Status was renamed.</returns>
    public Task<ProjectStatusWriteResult> HandleAsync(ActiveSpaceContext context, Guid projectId, Guid statusId, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return writer.WriteAsync(context.SpaceId, projectId, draft => Plan(draft, statusId, name), cancellationToken);
    }

    private static ProjectStatusWriteOutcome Plan(ProjectStatusDeltaDraft draft, Guid statusId, string name)
    {
        if (!draft.Holds(statusId))
        {
            return ProjectStatusWriteOutcome.NotFound;
        }

        draft.Rename(statusId, name);

        return ProjectStatusWriteOutcome.Written;
    }
}
