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
/// <b>A lock held to the transaction's end, and that is the whole point.</b> Each count takes its scope's application lock
/// first - the Space's for Projects, the Project's for Tasks (<see cref="ApplicationLocks"/>, story 2.9, open question 14
/// (a)) - and then counts in a statement of its own, which reads the last committed state. So a second creation in the
/// same Space (or Project) waits until the first has committed, and then counts the first's row. Two creations at one
/// below the limit therefore cannot both pass - the race story 1.6 left open for presence is not repeated for a bound
/// whose whole point is the number. Until story 2.9 both were <c>UPDLOCK, HOLDLOCK</c> range scans, whose last key could
/// lie in another Space.
/// </para>
/// <para>
/// <b>The same lock serialises Task creation within a Project</b>, which the position key relies on:
/// <c>TaskStore</c> reads the column's last key and appends after it, and no second creation in that
/// Project can read the same last key while the first is uncommitted (story 2.2, open question 2 (a)).
/// A Status change is the second writer that places a key, and <c>TaskStore.EditAsync</c> takes this same
/// Project lock before it does (story 2.3 Task 6), so an edit and a creation queue at one lock. A Task
/// deletion and a move take the same lock before their row, and a Project deletion takes it after the Project row
/// (story 2.5), so none can commit between a creation's count and its insert.
/// </para>
/// <para>
/// <b>No <c>Where</c> on <c>SpaceId</c>.</b> The row-level security policy narrows the statement to
/// the request's Space; raw SQL is not subject to the query filter, so the policy is the layer here,
/// and the count reads only the request's Space. The locks are named for the request's Space
/// (<see cref="ApplicationLocks"/>), which the policy cannot narrow - a name is not a row - so a count naming another
/// Space's Project takes a lock of its own Space's and waits on nothing of the other's. Raw SQL lives only in
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
            Bound.ProjectsPerSpace => CountProjectsAsync(cancellationToken),

            Bound.TasksPerProject when scopeId is { } projectId => CountTasksAsync(projectId, cancellationToken),

            Bound.TasksPerProject => throw new ArgumentException(
                "Tasks per Project is counted within one Project, and none was given.",
                nameof(scopeId)),

            _ => throw new ArgumentOutOfRangeException(
                nameof(bound),
                bound,
                "No creation endpoint consults this bound through the pipeline yet; add its count here when one does."),
        };

    /// <summary>
    /// The Space's Projects, counted under the Space's own lock (story 2.9, open question 14 (a)): the second creation in a
    /// Space waits at the lock until the first has committed, then its count - a statement of its own, reading the last
    /// committed state - sees the first's row. Another Space's creation takes another lock and never waits here.
    /// </summary>
    private async Task<int> CountProjectsAsync(CancellationToken cancellationToken)
    {
        await ApplicationLocks.TakeProjectsPerSpaceAsync(dbContext, cancellationToken).ConfigureAwait(false);

        return await dbContext.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.[Project]")
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The Project's Tasks, counted under the Project's own lock (story 2.9): the lock every key placer of the Project takes,
    /// so the count sees every committed creation and no Task is placed between the count and this creation's insert.
    /// </summary>
    private async Task<int> CountTasksAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await ApplicationLocks.TakeProjectTasksAsync(dbContext, projectId, cancellationToken).ConfigureAwait(false);

        return await dbContext.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.[Task] WHERE ProjectId = {projectId}")
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
