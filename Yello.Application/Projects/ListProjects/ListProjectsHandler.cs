using Yello.Domain.Projects;

namespace Yello.Application.Projects.ListProjects;

/// <summary>
/// Lists every Project in the active Space, by name then id. FR-18; story 2.1, AC4.
/// </summary>
/// <remarks>
/// <b>The same list for every Role</b>: Role decides what can be done to a Project, not whether it is
/// visible. The pipeline checked the read capability, which every Role holds.
/// </remarks>
/// <param name="store">Reads the Projects of the Space the unit of work is scoped to.</param>
public sealed class ListProjectsHandler(IProjectStore store)
{
    /// <summary>
    /// Lists the active Space's Projects.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Projects, by name then id.</returns>
    public Task<IReadOnlyList<Project>> HandleAsync(CancellationToken cancellationToken) =>
        store.ListAsync(cancellationToken);
}
