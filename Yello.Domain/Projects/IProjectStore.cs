namespace Yello.Domain.Projects;

/// <summary>
/// Creates, renames and lists the Projects of the Space the current unit of work is scoped to.
/// FR-17, FR-18.
/// </summary>
/// <remarks>
/// <b>Never asked about another Space.</b> Every call runs inside the request's Space-scoped unit of
/// work, where both isolation layers are already narrowed, so nothing here takes a Space to search -
/// creation takes the resolved Space only to stamp it on the new row.
/// </remarks>
public interface IProjectStore
{
    /// <summary>
    /// Creates a Project in the resolved Space.
    /// </summary>
    /// <param name="spaceId">The resolved Space, from the pipeline's context and never from the request.</param>
    /// <param name="name">The name, already normalised.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The created Project.</returns>
    Task<Project> CreateAsync(Guid spaceId, string name, CancellationToken cancellationToken);

    /// <summary>
    /// Renames a Project in the resolved Space, changing its name and nothing else.
    /// </summary>
    /// <param name="projectId">The Project.</param>
    /// <param name="name">The new name, already normalised.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The renamed Project, or <c>null</c> when no Project with that id is visible.</returns>
    Task<Project?> RenameAsync(Guid projectId, string name, CancellationToken cancellationToken);

    /// <summary>
    /// Every Project in the resolved Space, by name and then by id.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Projects, materialised.</returns>
    Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken);
}
