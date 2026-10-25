using Microsoft.EntityFrameworkCore;
using Yello.Application.Spaces;
using Yello.Domain.Bounds;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Counts what a creation bound limits, on the request's unit of work, under a lock held to its end.
/// AD-25 / AR-30; story 2.1, Task 6.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>UPDLOCK, HOLDLOCK</c>, and that is the whole point.</b> The count takes key-range update locks
/// on the rows it reads and keeps them until the transaction ends, so a second creation in the same
/// Space waits at its own count until the first has committed, and then counts the first's row. Two
/// creations at one below the limit therefore cannot both pass - the race story 1.6 left open for
/// presence is not repeated for a bound whose whole point is the number.
/// </para>
/// <para>
/// <b>No <c>Where</c> on <c>SpaceId</c>.</b> The row-level security policy narrows the statement to
/// the request's Space; raw SQL is not subject to the query filter, so the policy is the layer here,
/// and it is the one the lock follows. Raw SQL lives only in this ring (A-13).
/// </para>
/// <para>
/// <b>One bound today.</b> Each later bound (Tasks per Project, 2.2; Spaces per Account, 3.1;
/// Memberships per Space, 4.3) adds its own count here when its creation endpoint declares it.
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
internal sealed class BoundCounter(YelloDbContext dbContext) : IBoundCounter
{
    /// <inheritdoc />
    public Task<int> CountAsync(Bound bound, CancellationToken cancellationToken)
    {
        if (bound != Bound.ProjectsPerSpace)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bound),
                bound,
                "No creation endpoint consults this bound through the pipeline yet; add its count here when one does.");
        }

        return dbContext.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.[Project] WITH (UPDLOCK, HOLDLOCK)")
            .SingleAsync(cancellationToken);
    }
}
