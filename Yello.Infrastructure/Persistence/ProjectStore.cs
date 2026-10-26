using Microsoft.EntityFrameworkCore;
using Yello.Domain;
using Yello.Domain.Projects;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Creates, renames, lists and deletes Projects inside the request's Space-scoped unit of work. Stories 2.1 and 2.5.
/// </summary>
/// <remarks>
/// <para>
/// <b>No <c>Where</c> on <c>SpaceId</c>, anywhere.</b> Both isolation layers are already narrowed to
/// the request's Space (see <see cref="StatusDefinitionReader"/>), so a Project in another Space is
/// invisible here exactly as one that never existed is.
/// </para>
/// <para>
/// <b>Rename writes one column.</b> <c>ExecuteUpdate</c> with a single <c>SetProperty</c> on
/// <c>Name</c>: the statement cannot touch the Space, and the database would refuse it if it did.
/// </para>
/// <para>
/// <b>A deletion locks the Project row, then the Project's Task range, then deletes</b> (story 2.5, open question 5).
/// The range is what serialises it against a Task creation in that Project: a creation counts under that range in the
/// pipeline (the Tasks-per-Project bound, <see cref="BoundCounter"/>), then reads the Project, then inserts through the
/// composite foreign key. Without the range a deletion could commit between the creation's read and its insert, which
/// would meet the foreign key (547) as a <c>500</c>; with it the deletion waits, and then takes the new Task with it. A
/// creation queued behind a deletion finds no Project and answers the boundary's <c>404</c>. The row's update lock is
/// compatible with the shared lock the creation's foreign-key check takes, so the order cannot cycle - measured by story
/// 2.5's RACE-P1 and RACE-P2.
/// </para>
/// <para>
/// <b>One statement, cascades in the database.</b> <c>ExecuteDelete</c> on the Project; <c>FK_Task_Project_SpaceId_ProjectId</c>
/// and <c>TaskLabel</c>'s key to <c>Task</c> are <c>CASCADE</c> and carry <c>SpaceId</c>, so they cannot leave the Space.
/// Never batched: atomicity wins over lock escalation on a large Project (ledgered to story 2.9).
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
/// <param name="identifiers">Produces the Project's identity (AC6).</param>
/// <param name="clock">Stamps the creation instant.</param>
internal sealed class ProjectStore(YelloDbContext dbContext, IIdentifierGenerator identifiers, TimeProvider clock)
    : IProjectStore
{
    /// <inheritdoc />
    public async Task<Project> CreateAsync(Guid spaceId, string name, CancellationToken cancellationToken)
    {
        var project = new Project
        {
            Id = identifiers.Next(),
            SpaceId = spaceId,
            Name = name,
            CreatedAt = clock.GetUtcNow(),
        };

        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return project;
    }

    /// <inheritdoc />
    public async Task<Project?> RenameAsync(Guid projectId, string name, CancellationToken cancellationToken)
    {
        var renamed = await dbContext.Projects
            .Where(project => project.Id == projectId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(project => project.Name, name), cancellationToken)
            .ConfigureAwait(false);

        if (renamed == 0)
        {
            return null;
        }

        return await dbContext.Projects
            .AsNoTracking()
            .SingleAsync(project => project.Id == projectId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, int>> TaskCountsAsync(CancellationToken cancellationToken) =>
        await dbContext.Tasks
            .AsNoTracking()
            .GroupBy(task => task.ProjectId)
            .Select(group => new { ProjectId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.ProjectId, row => row.Count, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<int> TaskCountAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Tasks.CountAsync(task => task.ProjectId == projectId, cancellationToken);

    /// <inheritdoc />
    public async Task<ProjectDeletion> DeleteAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var locked = await dbContext.Projects
            .FromSql($"SELECT * FROM dbo.[Project] WITH (UPDLOCK, HOLDLOCK, INDEX(1)) WHERE Id = {projectId}")
            .AsNoTracking()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        if (locked == 0)
        {
            return ProjectDeletion.NotFound;
        }

        // The Project's Task range, in the statement shape a creation's bound count takes, so the two meet on it.
        _ = await dbContext.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.[Task] WITH (UPDLOCK, HOLDLOCK) WHERE ProjectId = {projectId}")
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        var deleted = await dbContext.Projects
            .Where(project => project.Id == projectId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return deleted == 0 ? ProjectDeletion.NotFound : ProjectDeletion.Deleted;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Projects
            .AsNoTracking()
            .OrderBy(project => project.Name)
            .ThenBy(project => project.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
