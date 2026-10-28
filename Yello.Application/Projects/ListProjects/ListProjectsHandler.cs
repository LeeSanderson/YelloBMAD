using Yello.Domain.Projects;

namespace Yello.Application.Projects.ListProjects;

/// <summary>
/// Lists every Project in the active Space, by name then id, each with its true Task count. FR-18; story 2.1, AC4;
/// story 2.5, AC2.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same list for every Role</b>: Role decides what can be done to a Project, not whether it is
/// visible. The pipeline checked the read capability, which every Role holds.
/// </para>
/// <para>
/// <b>The count is a true total</b> (AD-29), read in one grouped statement for the whole Space, so the deletion confirm
/// can name it when it opens (story 2.5, open question 2 (a)).
/// </para>
/// <para>
/// <b>Two statements, not one snapshot</b> (story 2.9, AC13, open question 10 (a)): under <c>READ_COMMITTED_SNAPSHOT</c>
/// the Project list and the grouped count each read the last state committed before they began, and neither waits on a
/// writer. A creation, deletion or move that commits between them can leave a count one off the list for this read only
/// (a Project listed with the count it had, or a count for a Project the list no longer holds, which is dropped);
/// the next read corrects it. Snapshot isolation for the request was rejected: presence writes in every resolution would
/// meet update conflict 3960.
/// </para>
/// </remarks>
/// <param name="store">Reads the Projects of the Space the unit of work is scoped to.</param>
public sealed class ListProjectsHandler(IProjectStore store)
{
    /// <summary>
    /// Lists the active Space's Projects.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Projects, by name then id, with their counts.</returns>
    public async Task<IReadOnlyList<ListedProject>> HandleAsync(CancellationToken cancellationToken)
    {
        var projects = await store.ListAsync(cancellationToken).ConfigureAwait(false);
        var counts = await store.TaskCountsAsync(cancellationToken).ConfigureAwait(false);

        return [.. projects.Select(project => new ListedProject(project, counts.GetValueOrDefault(project.Id)))];
    }
}
