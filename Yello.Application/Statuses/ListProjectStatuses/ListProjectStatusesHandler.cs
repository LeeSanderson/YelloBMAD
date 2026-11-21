using Yello.Domain.Boards;
using Yello.Domain.Statuses;

namespace Yello.Application.Statuses.ListProjectStatuses;

/// <summary>
/// Reads one Project's effective Status set, each Status marked with where it comes from and how many of the Project's
/// Tasks hold it.
/// </summary>
/// <remarks>
/// <b>No Role, no Space and no lock</b> (AR-3): the pipeline resolved the Space and checked the read capability every
/// Role holds, so a Viewer reads exactly what an Owner does.
/// </remarks>
/// <param name="boards">Reads the Project and its Task counts.</param>
/// <param name="statuses">Reads the Space's defaults and the Project's delta.</param>
public sealed class ListProjectStatusesHandler(IBoardReader boards, IStatusDefinitionReader statuses)
{
    /// <summary>
    /// Reads the set.
    /// </summary>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The set, or <c>null</c> when no Project in the active Space has that id.</returns>
    public async Task<ProjectStatusSet?> HandleAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (await boards.FindProjectAsync(projectId, cancellationToken).ConfigureAwait(false) is null)
        {
            return null;
        }

        var effective = await statuses.EffectiveOfAsync(projectId, cancellationToken).ConfigureAwait(false);
        var counts = await boards.CountTasksByStatusAsync(projectId, cancellationToken).ConfigureAwait(false);

        return new ProjectStatusSet(effective, counts);
    }
}
