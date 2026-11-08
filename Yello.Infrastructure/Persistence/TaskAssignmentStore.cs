using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Yello.Domain.Tasks;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Assigns and unassigns Tasks inside the request's Space-scoped unit of work.
/// </summary>
/// <remarks>
/// <para>
/// <b>One guarded statement under the Space's Memberships lock, taken Shared</b>: it writes the Assignee only when the
/// Membership is one of this Space's when the statement runs, and its <c>OUTPUT</c> gives the previous Assignee, so the
/// caller knows whether anything changed. A removal, a leave and a Role change take the same lock Exclusive, so the
/// Membership the statement finds cannot be deleted before this commits, and a removal queued behind this sees the
/// assignment and unassigns it before its <c>DELETE</c>. Two assignments in one Space never wait on each other.
/// </para>
/// <para>
/// <b>No Project lock and no row lock beyond the statement's own</b>: an assignment places no position key, so two
/// assignments of one Task serialise on the row and the last writer wins, as every Task edit does.
/// </para>
/// <para>
/// <b>On a rowcount of zero it reads why</b>: no Task with the id here is the in-Space not-found; otherwise the Membership
/// is not this Space's - random, another Space's, or removed while this waited - and the refusal is the same for each.
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
internal sealed class TaskAssignmentStore(YelloDbContext dbContext) : ITaskAssignments
{
    private const int ForeignKeyViolationErrorNumber = 547;

    /// <inheritdoc />
    public async Task<TaskAssignment> AssignAsync(Guid taskId, Guid? membershipId, CancellationToken cancellationToken)
    {
        var spaceId = dbContext.ScopedSpaceId
            ?? throw new InvalidOperationException("A Task is assigned only inside a Space-scoped unit of work.");

        await ApplicationLocks.TakeMembershipsSharedAsync(dbContext, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<Guid?> previous;

        try
        {
            previous = await dbContext.Database
                .SqlQuery<Guid?>(
                    $"""
                    UPDATE dbo.[Task] SET AssigneeMembershipId = {membershipId}
                    OUTPUT deleted.AssigneeMembershipId AS [Value]
                    WHERE SpaceId = {spaceId}
                      AND Id = {taskId}
                      AND ({membershipId} IS NULL OR EXISTS (
                          SELECT 1 FROM dbo.[Membership]
                          WHERE SpaceId = {spaceId} AND Id = {membershipId}))
                    """)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SqlException exception) when (exception.Number == ForeignKeyViolationErrorNumber)
        {
            // The Assignee key refusing a Membership the EXISTS clause saw: unreachable while every Membership deleter takes
            // the lock held above, and answered as the same refusal rather than a 500 should it ever be reached.
            return TaskAssignment.InvalidAssignee;
        }

        if (previous.Count == 1)
        {
            var task = await dbContext.Tasks
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == taskId, cancellationToken)
                .ConfigureAwait(false);

            return TaskAssignment.Assigned(task, previous[0]);
        }

        var exists = await dbContext.Tasks
            .AsNoTracking()
            .AnyAsync(candidate => candidate.Id == taskId, cancellationToken)
            .ConfigureAwait(false);

        return exists ? TaskAssignment.InvalidAssignee : TaskAssignment.NotFound;
    }
}
