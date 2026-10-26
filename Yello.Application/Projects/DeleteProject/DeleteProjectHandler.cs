using Yello.Domain.Projects;

namespace Yello.Application.Projects.DeleteProject;

/// <summary>
/// Deletes a Project of the active Space, and with it every Task in it and every Label applied to those Tasks,
/// immediately and irreversibly. FR-17's delete clause; story 2.5, AC2, AC3, AC7.
/// </summary>
/// <remarks>
/// <para>
/// <b>No trash and no restore</b> (PRD §6.2): the rows are gone when this answers. The Space's Labels stay.
/// </para>
/// <para>
/// <b>No expected count.</b> The confirm states the count it read when it opened; a Task added since is deleted too,
/// as FR-17 says "Deleting a Project deletes its Tasks" (story 2.5, open question 2).
/// </para>
/// <para>
/// <b>No Role, no Space</b> (AR-3): the pipeline resolved the Space and checked <c>ManageProjects</c>. Another Space's
/// Project id matches nothing here, exactly like an id that never existed.
/// </para>
/// </remarks>
/// <param name="store">Deletes the Project.</param>
public sealed class DeleteProjectHandler(IProjectStore store)
{
    /// <summary>
    /// Deletes the Project.
    /// </summary>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the deletion came to.</returns>
    public Task<ProjectDeletion> HandleAsync(Guid projectId, CancellationToken cancellationToken) =>
        store.DeleteAsync(projectId, cancellationToken);
}
