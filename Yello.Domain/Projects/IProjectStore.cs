namespace Yello.Domain.Projects;

/// <summary>
/// Creates, renames, lists and deletes the Projects of the Space the current unit of work is scoped to.
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

    /// <summary>
    /// The true number of Tasks in each Project of the resolved Space (AD-29), for the deletion confirm (story 2.5,
    /// open question 2 (a)). A Project holding none is absent.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The count per Project id.</returns>
    Task<IReadOnlyDictionary<Guid, int>> TaskCountsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The true number of Tasks in one Project of the resolved Space (AD-29), for a rename's answer (story 2.5, open
    /// question 2 (a)).
    /// </summary>
    /// <param name="projectId">The Project.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>How many Tasks it holds, in every Status.</returns>
    Task<int> TaskCountAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a Project of the resolved Space with every Task in it and every Label applied to those Tasks,
    /// immediately and irreversibly (FR-17, PRD §6.2; story 2.5, AC3). The Space's Labels stay.
    /// </summary>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the deletion came to.</returns>
    Task<ProjectDeletion> DeleteAsync(Guid projectId, CancellationToken cancellationToken);
}

/// <summary>
/// What a Project deletion came to. No "already deleted": that would tell a caller an id once existed (AD-3).
/// </summary>
public enum ProjectDeletion
{
    /// <summary>
    /// The Project, its Tasks and their applications were deleted.
    /// </summary>
    Deleted,

    /// <summary>
    /// No Project in the resolved Space has the id, whether it never existed, was deleted, or is another Space's.
    /// </summary>
    NotFound,
}
