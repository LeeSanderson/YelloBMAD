using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Yello.Application.Accounts.DeleteAccount;
using Yello.Application.ApiTokens;
using Yello.Application.Spaces;
using Yello.Domain.ApiTokens;
using Yello.Domain.Memberships;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// The Space switcher's read: every Space an Account holds a Membership in, as identity only, under
/// an Account-scoped unit of work. AD-24 / AR-29; AD-2 / AR-5, AR-6, AR-7.
/// </summary>
/// <remarks>
/// <para>
/// <b>The only caller of the <c>AccountId</c> session key.</b> It opens a transaction, scopes the
/// connection to the Account with <see cref="SpaceSessionContext.SetAccountAsync"/>, narrows
/// <see cref="AccountScope"/> to the same Account, runs one query, and ends - the shape
/// <see cref="MembershipActiveSpaceResolver"/> has for a Space. It never sets <c>SpaceId</c>, and a
/// Space-scoped unit of work never reaches here (<c>SpaceSwitcherGateTests</c>).
/// </para>
/// <para>
/// <b>No <c>WHERE</c> on <c>AccountId</c>, and that is the point</b> - the same rule
/// <see cref="StatusDefinitionReader"/> follows for Spaces. <c>Membership</c>'s policy and filter
/// are both narrowed to this Account, so the query states what it wants rather than where it may
/// look; a predicate written here as well would be a third layer that hides whether the other two
/// work.
/// </para>
/// <para>
/// <b><c>Space</c> is reached only through the join.</b> <c>Space</c> carries no policy (Lee's
/// decision at story 1.5, pending the AD-24 amendment), so which Spaces this can name is decided by
/// the <c>Membership</c> rows the two layers admit, in the same statement. Only the id and the name
/// are projected - nothing else about a Space leaves this method.
/// </para>
/// <para>
/// <b>The reads commit nothing.</b> The transaction exists to hold the connection, and the
/// session key with it, for the one statement; disposing it releases the connection to the pool,
/// whose reset clears the key.
/// </para>
/// <para>
/// <b>Account deletion's two Account-scoped steps live here too</b> (<see cref="HoldingsAsync"/>,
/// <see cref="EraseAsync"/>), so the <c>AccountId</c> key keeps exactly its two setters. The erase is
/// the one unit of work here that commits, and it writes only rows keyed by the Account: its refusal
/// records and its own row.
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
/// <param name="accountScope">The request's Account scope, which <c>Membership</c>'s filter reads.</param>
internal sealed class AccountSpaceDirectory(YelloDbContext dbContext, AccountScope accountScope)
    : IAccountSpaceDirectory, IAccountApiTokenDirectory
{
    private const int ForeignKeyViolationErrorNumber = 547;

    /// <inheritdoc />
    public Task<IReadOnlyList<SpaceIdentity>> ListAsync(
        AccountScopedContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Split from the body below because S4457 requires the argument check to run eagerly.
        return InAccountScopeAsync(context.AccountId, ListSpacesAsync, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>The Account-settings Token list (story 1.8, AC7), in the same unit of work.</b>
    /// <c>ApiToken</c> carries no policy, so it is narrowed to the Account by a <c>WHERE</c>; each
    /// Token's Space is then named through a join from the Account's own <c>Membership</c> row - the
    /// one its composite foreign key points at - so the read reaches <c>Space</c> only as the switcher
    /// does. Nothing but the Token's id, name and issue instant and the Space's id and name is
    /// projected: no Role, no hash.
    /// </para>
    /// </remarks>
    public Task<IReadOnlyList<ApiTokenListing>> ListApiTokensAsync(
        AccountScopedContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var accountId = context.AccountId;

        return InAccountScopeAsync(accountId, ct => ListTokensAsync(accountId, ct), cancellationToken);
    }

    private async Task<IReadOnlyList<ApiTokenListing>> ListTokensAsync(Guid accountId, CancellationToken cancellationToken) =>
        await dbContext.Set<ApiToken>()
            .AsNoTracking()
            .Where(token => token.AccountId == accountId)
            .Join(
                dbContext.Memberships.AsNoTracking(),
                token => new { SpaceId = token.BoundSpaceId, token.AccountId },
                membership => new { membership.SpaceId, membership.AccountId },
                (token, membership) => new { token.Id, token.Name, token.CreatedAt, membership.SpaceId })
            .Join(
                dbContext.Spaces.AsNoTracking(),
                row => row.SpaceId,
                space => space.Id,
                (row, space) => new { row.Id, row.Name, row.CreatedAt, SpaceId = space.Id, SpaceName = space.Name })
            .OrderBy(row => row.CreatedAt)
            .ThenBy(row => row.Id)
            .Select(row => new ApiTokenListing(row.Id, row.Name, new SpaceIdentity(row.SpaceId, row.SpaceName), row.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<IReadOnlyList<SpaceIdentity>> ListSpacesAsync(CancellationToken cancellationToken) =>
        await dbContext.Memberships
            .AsNoTracking()
            .Join(
                dbContext.Spaces.AsNoTracking(),
                membership => membership.SpaceId,
                space => space.Id,
                (_, space) => new { space.Id, space.Name })
            .OrderBy(space => space.Name)
            .ThenBy(space => space.Id)
            .Select(space => new SpaceIdentity(space.Id, space.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// The Account deletion's check: under the Account's lock, what the Account owns and every Space it holds a
    /// Membership in. Writes nothing.
    /// </summary>
    /// <remarks>
    /// The lock is the one Space creation takes, so a Space being created for the Account is either committed and read
    /// here as owned, or created after and read by the erase.
    /// </remarks>
    /// <param name="accountId">The Account being deleted.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What it owns and holds.</returns>
    internal Task<AccountHoldings> HoldingsAsync(Guid accountId, CancellationToken cancellationToken) =>
        InAccountScopeAsync(accountId, LockedHoldingsAsync, cancellationToken);

    /// <summary>
    /// The Account deletion's erase: under the Account's lock, when the Account holds no Membership, its refusal records
    /// and its row are deleted and committed. The row's references go with it: its Account-scoped idempotency records
    /// cascade, and every Invitation it issued, in any Space, keeps its row with no issuer.
    /// </summary>
    /// <param name="accountId">The Account being deleted.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>Erased, or what the Account still owns and holds.</returns>
    internal Task<AccountErasure> EraseAsync(Guid accountId, CancellationToken cancellationToken) =>
        InAccountScopeAsync(accountId, ct => EraseLockedAsync(accountId, ct), erasure => erasure.Erased, cancellationToken);

    private async Task<AccountHoldings> LockedHoldingsAsync(CancellationToken cancellationToken)
    {
        await ApplicationLocks.TakeSpacesPerAccountAsync(dbContext, cancellationToken).ConfigureAwait(false);

        return await ReadHoldingsAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<AccountErasure> EraseLockedAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var holdings = await LockedHoldingsAsync(cancellationToken).ConfigureAwait(false);

        if (holdings.Held.Count > 0)
        {
            return new AccountErasure(Erased: false, holdings);
        }

        await AccessRefusalPurge.EraseAccountAsync(dbContext, accountId, cancellationToken).ConfigureAwait(false);

        try
        {
            // Zero rows is an Account another request already erased: the outcome asked for.
            await dbContext.Database.ExecuteSqlAsync(
                $"DELETE FROM dbo.[Account] WHERE Id = {accountId};",
                cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException exception) when (exception.Number == ForeignKeyViolationErrorNumber)
        {
            // A Membership committed after the read above (an Invitation accepted in another tab takes no Account lock):
            // the caller reads the holdings again and leaves it.
            return new AccountErasure(Erased: false, new AccountHoldings([], []));
        }

        return new AccountErasure(Erased: true, new AccountHoldings([], []));
    }

    /// <summary>
    /// The Account's owned Spaces by name then id, and every Space it holds a Membership in, by id. As in
    /// <see cref="ListSpacesAsync"/>, no <c>WHERE</c> on the Account: both layers are narrowed to it.
    /// </summary>
    private async Task<AccountHoldings> ReadHoldingsAsync(CancellationToken cancellationToken)
    {
        var owned = await dbContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.Role == Role.Owner)
            .Join(
                dbContext.Spaces.AsNoTracking(),
                membership => membership.SpaceId,
                space => space.Id,
                (_, space) => new { space.Id, space.Name })
            .OrderBy(space => space.Name)
            .ThenBy(space => space.Id)
            .Select(space => new SpaceIdentity(space.Id, space.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var held = await dbContext.Memberships
            .AsNoTracking()
            .OrderBy(membership => membership.SpaceId)
            .Select(membership => membership.SpaceId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AccountHoldings(owned, held);
    }

    /// <summary>
    /// Runs one read under an Account-scoped unit of work: a transaction holding the connection, the
    /// <c>AccountId</c> session key, and <see cref="AccountScope"/> narrowed to the same Account.
    /// </summary>
    private Task<T> InAccountScopeAsync<T>(
        Guid accountId,
        Func<CancellationToken, Task<T>> read,
        CancellationToken cancellationToken) =>
        InAccountScopeAsync(accountId, read, _ => false, cancellationToken);

    /// <summary>
    /// Runs one piece of work under an Account-scoped unit of work, committing it only when <paramref name="commits"/>
    /// says so of its result; otherwise disposing the transaction rolls it back.
    /// </summary>
    private async Task<T> InAccountScopeAsync<T>(
        Guid accountId,
        Func<CancellationToken, Task<T>> read,
        Func<T, bool> commits,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await using (transaction.ConfigureAwait(false))
        {
            try
            {
                await SpaceSessionContext
                    .SetAccountAsync(dbContext.Database, accountId, cancellationToken)
                    .ConfigureAwait(false);

                accountScope.Narrow(accountId);

                var result = await read(cancellationToken).ConfigureAwait(false);

                if (commits(result))
                {
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }

                return result;
            }
            finally
            {
                accountScope.Clear();
            }
        }
    }
}
