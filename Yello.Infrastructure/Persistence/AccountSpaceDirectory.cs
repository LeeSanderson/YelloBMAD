using Microsoft.EntityFrameworkCore;
using Yello.Application.Spaces;

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
/// <b>Read-only, so it commits nothing.</b> The transaction exists to hold the connection, and the
/// session key with it, for the one statement; disposing it releases the connection to the pool,
/// whose reset clears the key.
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
/// <param name="accountScope">The request's Account scope, which <c>Membership</c>'s filter reads.</param>
internal sealed class AccountSpaceDirectory(YelloDbContext dbContext, AccountScope accountScope)
    : IAccountSpaceDirectory
{
    /// <inheritdoc />
    public Task<IReadOnlyList<SpaceIdentity>> ListAsync(
        AccountScopedContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Split from the body below because S4457 requires the argument check to run eagerly.
        return ListCoreAsync(context.AccountId, cancellationToken);
    }

    private async Task<IReadOnlyList<SpaceIdentity>> ListCoreAsync(Guid accountId, CancellationToken cancellationToken)
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

                return await dbContext.Memberships
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
            }
            finally
            {
                accountScope.Clear();
            }
        }
    }
}
