using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Yello.Application.Memberships;
using Yello.Domain;
using Yello.Domain.Memberships;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Lists the resolved Space's Memberships and changes one's Role, inside the request's Space-scoped unit of work. FR-13,
/// UX-DR14, UX-DR34, AR-12/AD-5, AR-16/AD-9; story 4.4, AC1, AC2, AC4-AC7; Lee's Q1 (a), Q2 (a), Q4 (a), Q5 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>The list</b> reads one page in join order - keyset on <c>(CreatedAt, Id)</c> - joined to the policy-free
/// <c>Account</c> on the Membership's own <c>AccountId</c>, and a separate count for the true total. No <c>Where</c> on
/// <c>SpaceId</c>: both isolation layers are narrowed to the request's Space, and <c>Account</c> is reached only through a
/// Membership they admit. The Role is read here and answered as its stable code, so the slice reads none (AD-1).
/// </para>
/// <para>
/// <b>The address and the name.</b> The address is <c>Account.EmailAddress</c> as typed at registration, never the
/// normalised form (Lee's Q4 (a)); this store is one of the named readers of it. The display name is read live, for current
/// Members in Membership management only (Lee's Q5 (a)). The reason attribution never does this, stated
/// (<c>EXPERIENCE.md:156</c>): resolving a current name globally would propagate a later name change into a Space the reader
/// no longer shares with that person. A current Membership's name is shared by definition, so the list may read it; authored
/// content keeps the name captured at authoring time (<c>Invitation.IssuerName</c>; 7.2's change log).
/// </para>
/// <para>
/// <b>The Role change is one guarded statement under the Space's Memberships lock</b> (Q17 (a)), composing the acting
/// authority's Role sets (Lee's Q1 (a)):
/// <c>UPDATE Membership SET Role = @new WHERE Id = @target AND Role IN (@from) AND @new IN (@to) AND Role &lt;&gt; @new
/// AND EXISTS (the actor's Membership, read with READCOMMITTEDLOCK, still holds a Role in @heldBy)</c>. The lock comes
/// first, so a statement that runs after a holder committed sees what it committed; the actor clause makes an actor demoted
/// while its change was queued fail as beyond its authority (NFR-2, "no tolerance"). No <c>HOLDLOCK</c> (story 3.4). Owner
/// is in no set, so the statement can neither read an Owner row as its target nor write <c>Owner</c>; the filtered unique
/// index's <c>2601</c>, should a second Owner ever be attempted, is answered <see cref="RoleChangeOutcome.OwnerRoleFixed"/>,
/// never a <c>500</c>.
/// </para>
/// <para>
/// <b>On a rowcount of zero it reads why</b> (4.2's revoke precedent): the target's Role, or that no Membership in this
/// Space has the id - another Space's included, which both layers hide - and whether the actor still holds its authority;
/// <see cref="RoleChangeAuthority.Judge"/> decides. A committed change records the target's Account for its
/// <c>MembershipChanged</c> publish after commit (AC6); a refusal or a no-op records nothing. Raw SQL tracks nothing, so the
/// pipeline's idempotency record saves cleanly after it.
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
/// <param name="membershipChanges">Collects the committed change's <c>MembershipChanged</c>.</param>
internal sealed class MembershipStore(YelloDbContext dbContext, IMembershipChanges membershipChanges) : IMembershipStore
{
    private const int UniqueIndexViolationErrorNumber = 2601;

    private const int UniqueConstraintViolationErrorNumber = 2627;

    /// <inheritdoc />
    public async Task<(IReadOnlyList<ListedMembership> Rows, int Total)> ListAsync(
        Guid callerAccountId,
        MembershipCursor? after,
        int take,
        CancellationToken cancellationToken)
    {
        var total = await dbContext.Memberships.AsNoTracking().CountAsync(cancellationToken).ConfigureAwait(false);

        var memberships = dbContext.Memberships.AsNoTracking();

        if (after is not null)
        {
            memberships = memberships.Where(membership =>
                membership.CreatedAt > after.CreatedAt
                || (membership.CreatedAt == after.CreatedAt && membership.Id.CompareTo(after.Id) > 0));
        }

        var rows = await memberships
            .OrderBy(membership => membership.CreatedAt)
            .ThenBy(membership => membership.Id)
            .Take(take)
            .Join(
                dbContext.Accounts,
                membership => membership.AccountId,
                account => account.Id,
                (membership, account) => new
                {
                    membership.Id,
                    membership.Role,
                    membership.CreatedAt,
                    IsYou = membership.AccountId == callerAccountId,
                    account.DisplayName,
                    account.EmailAddress,
                })
            .OrderBy(row => row.CreatedAt)
            .ThenBy(row => row.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (rows.Select(row => new ListedMembership(row.Id, row.DisplayName, row.EmailAddress, StableCode.Of(row.Role), row.IsYou, row.CreatedAt)).ToList(), total);
    }

    /// <inheritdoc />
    public Task<RoleChangeResult> ChangeRoleAsync(
        Guid actorAccountId,
        Guid membershipId,
        Role proposed,
        RoleChangeAuthority authority,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authority);

        // Split for S4457.
        return ChangeRoleCoreAsync(actorAccountId, membershipId, proposed, authority, cancellationToken);
    }

    private static string Names(IReadOnlySet<Role> roles) => string.Join(',', roles.Select(role => role.ToString()));

    private async Task<RoleChangeResult> ChangeRoleCoreAsync(
        Guid actorAccountId,
        Guid membershipId,
        Role proposed,
        RoleChangeAuthority authority,
        CancellationToken cancellationToken)
    {
        await ApplicationLocks.TakeMembershipsAsync(dbContext, cancellationToken).ConfigureAwait(false);

        var spaceId = dbContext.ScopedSpaceId
            ?? throw new InvalidOperationException("A Role is changed only inside a Space-scoped unit of work.");

        int changed;

        try
        {
            changed = await GuardedUpdateAsync(spaceId, actorAccountId, membershipId, proposed, authority, cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException exception) when (exception.Number is UniqueIndexViolationErrorNumber or UniqueConstraintViolationErrorNumber)
        {
            // A second Owner refused by UX_Membership_SpaceId_Owner (AR-12): unreachable while Owner is in no set, and a
            // stable answer rather than a 500 should it ever be reached. The unit of work rolls back on the failure.
            return new RoleChangeResult(RoleChangeOutcome.OwnerRoleFixed, null);
        }

        var target = await TargetAsync(membershipId, cancellationToken).ConfigureAwait(false);

        if (changed == 1)
        {
            // Published once the request's unit of work commits, never on its rollback (AC6, Q7 (a)).
            membershipChanges.Record(target!.AccountId, spaceId);

            return new RoleChangeResult(RoleChangeOutcome.Changed, await RowAsync(actorAccountId, membershipId, cancellationToken).ConfigureAwait(false));
        }

        if (target is null)
        {
            return new RoleChangeResult(RoleChangeOutcome.NotFound, null);
        }

        var actor = await dbContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.AccountId == actorAccountId)
            .Select(membership => new TargetMembership(membership.Role, membership.AccountId))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var refusal = authority.Judge(target.Role, proposed, actor is not null && authority.HeldBy.Contains(actor.Role));

        return refusal switch
        {
            RoleChangeRefusal.Unchanged => new RoleChangeResult(RoleChangeOutcome.Unchanged, await RowAsync(actorAccountId, membershipId, cancellationToken).ConfigureAwait(false)),
            RoleChangeRefusal.OwnerRoleFixed => new RoleChangeResult(RoleChangeOutcome.OwnerRoleFixed, null),
            RoleChangeRefusal.BeyondAuthority => new RoleChangeResult(RoleChangeOutcome.BeyondAuthority, null),
            _ => throw new InvalidOperationException($"Unanswered Role change refusal {refusal}."),
        };
    }

    /// <summary>
    /// The one guarded statement. The Role sets travel as comma-separated Role names, split in the statement: the names are
    /// the enum's own, never anything the request carried.
    /// </summary>
    private Task<int> GuardedUpdateAsync(
        Guid spaceId,
        Guid actorAccountId,
        Guid membershipId,
        Role proposed,
        RoleChangeAuthority authority,
        CancellationToken cancellationToken)
    {
        var proposedName = proposed.ToString();
        var from = Names(authority.From);
        var to = Names(authority.To);
        var heldBy = Names(authority.HeldBy);

        return dbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE dbo.[Membership] SET Role = {proposedName}
            WHERE Id = {membershipId}
              AND SpaceId = {spaceId}
              AND Role IN (SELECT value FROM STRING_SPLIT({from}, ','))
              AND {proposedName} IN (SELECT value FROM STRING_SPLIT({to}, ','))
              AND Role <> {proposedName}
              AND EXISTS (
                  SELECT 1 FROM dbo.[Membership] AS actor WITH (READCOMMITTEDLOCK)
                  WHERE actor.SpaceId = {spaceId}
                    AND actor.AccountId = {actorAccountId}
                    AND actor.Role IN (SELECT value FROM STRING_SPLIT({heldBy}, ',')));
            """,
            cancellationToken);
    }

    /// <summary>
    /// The target as both isolation layers show it: its Role and Account, or <c>null</c> when no Membership in this Space
    /// has the id.
    /// </summary>
    private Task<TargetMembership?> TargetAsync(Guid membershipId, CancellationToken cancellationToken) =>
        dbContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.Id == membershipId)
            .Select(membership => new TargetMembership(membership.Role, membership.AccountId))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// The target's row as the list shows it.
    /// </summary>
    private async Task<ListedMembership> RowAsync(Guid callerAccountId, Guid membershipId, CancellationToken cancellationToken)
    {
        var row = await dbContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.Id == membershipId)
            .Join(
                dbContext.Accounts,
                membership => membership.AccountId,
                account => account.Id,
                (membership, account) => new
                {
                    membership.Id,
                    membership.Role,
                    membership.CreatedAt,
                    IsYou = membership.AccountId == callerAccountId,
                    account.DisplayName,
                    account.EmailAddress,
                })
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ListedMembership(row.Id, row.DisplayName, row.EmailAddress, StableCode.Of(row.Role), row.IsYou, row.CreatedAt);
    }

    /// <summary>
    /// A Membership's Role and Account, as the disambiguation read sees it.
    /// </summary>
    /// <param name="Role">Its Role now.</param>
    /// <param name="AccountId">Its Account.</param>
    private sealed record TargetMembership(Role Role, Guid AccountId);
}
