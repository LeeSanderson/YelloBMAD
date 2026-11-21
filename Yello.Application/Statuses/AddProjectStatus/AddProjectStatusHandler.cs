using Yello.Application.Spaces;

namespace Yello.Application.Statuses.AddProjectStatus;

/// <summary>
/// Adds a Status to one Project's set, at its end or right after another of its Statuses.
/// </summary>
/// <remarks>
/// <b>An add at a position is two operations in one write</b> - the add, then the place - so each kind of operation keeps
/// one meaning, and a later Space reorder treats the new Status like any other Status the Project placed.
/// </remarks>
/// <param name="writer">Writes the delta under the Project's lock.</param>
public sealed class AddProjectStatusHandler(ProjectStatusDeltaWriter writer)
{
    /// <summary>
    /// Adds the Status.
    /// </summary>
    /// <param name="context">The active Space, as the pipeline resolved it on this request.</param>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="name">The name, already normalised.</param>
    /// <param name="afterStatusId">The Status it goes after, or <c>null</c> to append it.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened, and the Project's set when the Status was added.</returns>
    public Task<ProjectStatusWriteResult> HandleAsync(ActiveSpaceContext context, Guid projectId, string name, Guid? afterStatusId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return writer.WriteAsync(context.SpaceId, projectId, draft => Plan(draft, name, afterStatusId), cancellationToken);
    }

    private static ProjectStatusWriteOutcome Plan(ProjectStatusDeltaDraft draft, string name, Guid? afterStatusId)
    {
        if (afterStatusId is { } anchor && !draft.Holds(anchor))
        {
            return ProjectStatusWriteOutcome.PositionInvalid;
        }

        var added = draft.Add(name);

        if (afterStatusId is { } after)
        {
            draft.Place(added, after);
        }

        return ProjectStatusWriteOutcome.Written;
    }
}
