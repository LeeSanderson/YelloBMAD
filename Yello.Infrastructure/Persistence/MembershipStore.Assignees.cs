using Microsoft.EntityFrameworkCore;
using Yello.Application.Memberships;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// The Memberships as Assignees: the candidates every Role reads, the facts an assignment email names, and the
/// unassignment that a removal and a leave perform before their <c>DELETE</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Here, not in a store of their own</b>, because the candidates are display names and the email needs an address, and
/// this store is one of the few named readers of both. The candidates carry no address and no Role.
/// </para>
/// <para>
/// <b>The unassignment is explicit</b>: the Assignee key carries <c>SpaceId</c>, so it cannot be <c>ON DELETE SET NULL</c>
/// (that would null the Space), and a NO ACTION key refuses the <c>DELETE</c> of a Membership any Task still names. It runs
/// under the Memberships lock the removal already holds Exclusive, which every assignment takes Shared, so no assignment can
/// commit between it and the <c>DELETE</c>. A removal or leave that deletes nothing is refused, and the refusal rolls the
/// unassignment back with the rest of the unit of work.
/// </para>
/// </remarks>
internal sealed partial class MembershipStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AssigneeCandidate>> ListAssigneesAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.Memberships
            .AsNoTracking()
            .Join(
                dbContext.Accounts,
                membership => membership.AccountId,
                account => account.Id,
                (membership, account) => new { membership.Id, membership.CreatedAt, account.DisplayName })
            .OrderBy(row => row.CreatedAt)
            .ThenBy(row => row.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(row => new AssigneeCandidate(row.Id, row.DisplayName))];
    }

    /// <inheritdoc />
    public async Task<AssignmentNotice?> AssignmentNoticeAsync(Guid actorAccountId, Guid membershipId, Guid taskId, CancellationToken cancellationToken)
    {
        var spaceId = ScopedSpaceId();

        var recipient = await dbContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.Id == membershipId)
            .Join(dbContext.Accounts, membership => membership.AccountId, account => account.Id, (_, account) => new { account.Id, account.EmailAddress })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var assignerName = await dbContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.SpaceId == spaceId && membership.AccountId == actorAccountId)
            .Join(dbContext.Accounts, membership => membership.AccountId, account => account.Id, (_, account) => account.DisplayName)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var task = await dbContext.Tasks
            .AsNoTracking()
            .Where(candidate => candidate.Id == taskId)
            .Join(dbContext.Projects, candidate => candidate.ProjectId, project => project.Id, (candidate, project) => new { candidate.Title, ProjectName = project.Name })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return recipient is null || assignerName is null || task is null
            ? null
            : new AssignmentNotice(recipient.Id, recipient.EmailAddress, assignerName, task.ProjectName, task.Title);
    }

    /// <inheritdoc />
    public async Task<OwnershipOfferNotice?> OwnershipOfferNoticeAsync(Guid actorAccountId, Guid recipientMembershipId, CancellationToken cancellationToken)
    {
        var spaceId = ScopedSpaceId();

        var recipient = await dbContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.Id == recipientMembershipId)
            .Join(dbContext.Accounts, membership => membership.AccountId, account => account.Id, (_, account) => new { account.EmailAddress, account.DisplayName })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (recipient is null)
        {
            return null;
        }

        var offererName = await dbContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.SpaceId == spaceId && membership.AccountId == actorAccountId)
            .Join(dbContext.Accounts, membership => membership.AccountId, account => account.Id, (_, account) => account.DisplayName)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new OwnershipOfferNotice(recipient.EmailAddress, recipient.DisplayName, offererName);
    }

    /// <inheritdoc />
    public async Task<string?> DisplayNameOfAsync(Guid membershipId, CancellationToken cancellationToken) =>
        await dbContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.Id == membershipId)
            .Join(dbContext.Accounts, membership => membership.AccountId, account => account.Id, (_, account) => account.DisplayName)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Unassigns every Task of this Space that names the Membership, in the caller's unit of work and under its Memberships
    /// lock. Sends nothing: nobody is told of an unassignment.
    /// </summary>
    private Task<int> UnassignAsync(Guid spaceId, Guid membershipId, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE dbo.[Task] SET AssigneeMembershipId = NULL
            WHERE SpaceId = {spaceId}
              AND AssigneeMembershipId = {membershipId};
            """,
            cancellationToken);
}
