using Microsoft.EntityFrameworkCore;
using Yello.Application.Spaces;
using Yello.Domain.Bounds;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Counts what a creation bound limits, on the request's unit of work, under a lock held to its end.
/// AD-25 / AR-30; stories 2.1 and 2.2.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>UPDLOCK, HOLDLOCK</c>, and that is the whole point.</b> The count takes key-range update locks
/// on the rows it reads and keeps them until the transaction ends, so a second creation in the same
/// Space (or Project) waits at its own count until the first has committed, and then counts the first's
/// row. Two creations at one below the limit therefore cannot both pass - the race story 1.6 left open
/// for presence is not repeated for a bound whose whole point is the number.
/// </para>
/// <para>
/// <b>The same lock serialises Task creation within a Project</b>, which the position key relies on:
/// <c>TaskStore</c> reads the column's last key and appends after it, and no second creation in that
/// Project can read the same last key while the first is uncommitted (story 2.2, open question 2 (a)).
/// A Status change is the second writer that places a key, and <c>TaskStore.EditAsync</c> takes this same
/// Project range before it does (story 2.3 Task 6), so an edit and a creation queue at one lock. A Task
/// deletion takes the same range before its row, and a Project deletion takes it after the Project row
/// (story 2.5), so neither can commit between a creation's count and its insert.
/// </para>
/// <para>
/// <b>No <c>Where</c> on <c>SpaceId</c>.</b> The row-level security policy narrows the statement to
/// the request's Space; raw SQL is not subject to the query filter, so the policy is the layer here,
/// and it is the one the lock follows. For Tasks, the <c>(SpaceId, ProjectId, ...)</c> index the policy
/// and the Project predicate seek keeps the range lock inside one Project's range. Raw SQL lives only in
/// this ring (A-13), and every value is a parameter.
/// </para>
/// <para>
/// <b>An unknown or foreign Project counts 0.</b> The policy hides another Space's Tasks, so the bound
/// passes and the slice answers the boundary's <c>404</c> - the bound never discloses a Project.
/// </para>
/// <para>
/// <b>Two bounds today.</b> Each later bound (Spaces per Account, 3.1; Memberships per Space, 4.3) adds
/// its own count here when its creation endpoint declares it.
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
internal sealed class BoundCounter(YelloDbContext dbContext) : IBoundCounter
{
    /// <inheritdoc />
    public Task<int> CountAsync(Bound bound, Guid? scopeId, CancellationToken cancellationToken) =>
        bound switch
        {
            Bound.ProjectsPerSpace => dbContext.Database
                .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.[Project] WITH (UPDLOCK, HOLDLOCK)")
                .SingleAsync(cancellationToken),

            Bound.TasksPerProject when scopeId is { } projectId => dbContext.Database
                .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.[Task] WITH (UPDLOCK, HOLDLOCK) WHERE ProjectId = {projectId}")
                .SingleAsync(cancellationToken),

            Bound.TasksPerProject => throw new ArgumentException(
                "Tasks per Project is counted within one Project, and none was given.",
                nameof(scopeId)),

            _ => throw new ArgumentOutOfRangeException(
                nameof(bound),
                bound,
                "No creation endpoint consults this bound through the pipeline yet; add its count here when one does."),
        };
}
