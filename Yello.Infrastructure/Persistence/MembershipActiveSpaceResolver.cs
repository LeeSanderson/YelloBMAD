using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Yello.Application.Spaces;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Resolves a request's <see cref="ActiveSpaceContext"/> from the Membership row, inside the unit
/// of work that request's Space-scoped reads and writes then run on. AD-1, AD-2 / AR-5, AR-6.
/// </summary>
/// <remarks>
/// <para>
/// <b>Set, then verify - the resolution of the bootstrap problem.</b> <c>Membership</c> carries the
/// row-level security predicate on <c>SESSION_CONTEXT('SpaceId')</c>, so the read that establishes
/// the context is hidden by the policy the context unlocks. This scopes the connection to the
/// requested Space first, then reads the Account's Membership under that scope. The read cannot
/// see a Membership in any other Space, so a row means membership and no row means refusal. The
/// window in which the connection names a Space the caller may not belong to is closed by
/// construction rather than by ordering discipline: it exists only inside this method, nothing but
/// the Membership read runs in it, and a refusal rolls the transaction back and ends the request.
/// </para>
/// <para>
/// <b>Why the requested Space may be named before it is verified.</b> The spine makes
/// <c>{spaceId}</c> the designed input: "<c>spaceId</c> is always the first path segment after the
/// version; it is the input to AD-1" (<c>ARCHITECTURE-SPINE.md:267</c>). What AR-5 forbids is
/// serving data for a Space the client named without a Membership check, and the check happens here
/// before anything is served. AD-2's own reason for the prohibition is the pooled connection, and
/// that is answered by setting the context per unit of work rather than per connection.
/// </para>
/// <para>
/// <b>One transaction, owning the connection from open to close.</b> Without it EF Core closes the
/// connection between statements and a reopened one has no session context; with it, every
/// statement the request makes runs on the connection this method scoped. Committing or disposing
/// releases the connection to the pool, whose <c>sp_reset_connection</c> clears the key before
/// anyone else receives it - measured, not assumed; see <see cref="SpaceSessionContext"/>.
/// </para>
/// <para>
/// <b>The Role is read here and nowhere else</b> (AC7). There is no cache in front of this read,
/// and <see cref="SpaceScope"/> is request-scoped, so a Role changed between two requests is
/// visible to the second.
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
/// <param name="spaceScope">The request's Space scope, which the query filters read.</param>
internal sealed class MembershipActiveSpaceResolver(YelloDbContext dbContext, SpaceScope spaceScope)
    : IActiveSpaceResolver
{
    /// <inheritdoc />
    public async Task<IActiveSpaceUnitOfWork?> BeginAsync(
        Guid accountId,
        Guid spaceId,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await SpaceSessionContext.SetAsync(dbContext.Database, spaceId, cancellationToken).ConfigureAwait(false);
            spaceScope.Narrow(spaceId);

            // Both layers are narrowed to the requested Space before this runs, so it can only
            // ever find this Account's Membership in this one Space.
            var membership = await dbContext.Memberships
                .AsNoTracking()
                .Where(candidate => candidate.AccountId == accountId && candidate.SpaceId == spaceId)
                .Select(candidate => new { candidate.Role })
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (membership is null)
            {
                // Refused rather than defaulted (AC1). The scope returns to matching nothing, and the
                // rollback releases a connection that named a Space this Account is not in.
                spaceScope.Clear();
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                await transaction.DisposeAsync().ConfigureAwait(false);

                return null;
            }

            var context = new ActiveSpaceContext(accountId, spaceId, membership.Role);
            spaceScope.Resolve(context);

            return new UnitOfWork(transaction, context, spaceScope);
        }
        catch
        {
            spaceScope.Clear();
            await transaction.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// The open transaction and the context resolved inside it.
    /// </summary>
    /// <param name="transaction">The transaction the unit of work runs in.</param>
    /// <param name="context">The resolved context.</param>
    /// <param name="spaceScope">
    /// The request's Space scope, returned to matching nothing when the unit of work is disposed
    /// without completing - a refusal after resolution (a Token bound to another Space, story 1.8, or
    /// a denied capability) leaves no resolved Space behind, as a non-member's refusal does not.
    /// </param>
    private sealed class UnitOfWork(IDbContextTransaction transaction, ActiveSpaceContext context, SpaceScope spaceScope)
        : IActiveSpaceUnitOfWork
    {
        private bool _completed;

        /// <inheritdoc />
        public ActiveSpaceContext Context => context;

        /// <inheritdoc />
        public async Task CompleteAsync(CancellationToken cancellationToken)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _completed = true;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            if (!_completed)
            {
                spaceScope.Clear();
            }

            return transaction.DisposeAsync();
        }
    }
}
