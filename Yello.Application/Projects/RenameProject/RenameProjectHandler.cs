using Yello.Application.Projects.ListProjects;
using Yello.Domain.Projects;

namespace Yello.Application.Projects.RenameProject;

/// <summary>
/// Renames a Project in the active Space, changing its name and nothing else. FR-17; story 2.1, AC3.
/// </summary>
/// <remarks>
/// <b>No Space and no Role here</b> (AR-3). The unit of work is already scoped to the active Space, so a
/// Project id from another Space matches nothing - exactly like an id that never existed. The answer states the
/// Project's true Task count, as the listing does (story 2.5, open question 2 (a)).
/// </remarks>
/// <param name="store">Renames the Project.</param>
public sealed class RenameProjectHandler(IProjectStore store)
{
    /// <summary>
    /// Renames the Project.
    /// </summary>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="name">The new name, already normalised.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The renamed Project, or <c>null</c> when no Project in the active Space has that id.</returns>
    public async Task<ListedProject?> HandleAsync(Guid projectId, string name, CancellationToken cancellationToken)
    {
        var renamed = await store.RenameAsync(projectId, name, cancellationToken).ConfigureAwait(false);

        if (renamed is null)
        {
            return null;
        }

        var counts = await store.TaskCountsAsync(cancellationToken).ConfigureAwait(false);

        return new ListedProject(renamed, counts.GetValueOrDefault(renamed.Id));
    }
}
