using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Yello.Application.Invitations;
using Yello.Domain;
using Yello.Infrastructure.Invitations;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// The Invitation unit of work: the token resolved to its Space through the policy-free token index, then one transaction
/// under the <c>SpaceId</c> session key alone, set to that Space. Story 4.3, Lee's Q1 (a) and Q2 (a); AD-2 and AD-24 as
/// amended on 2026-11-02, AD-28.
/// </summary>
/// <remarks>
/// <para>
/// <b>The fourth named setter of the <c>SpaceId</c> key</b>, beside <see cref="MembershipActiveSpaceResolver"/> (the
/// route's Space, after a Membership), <see cref="AccountRegistrationStore"/> (the new Personal Space) and
/// <see cref="SpaceCreationScope"/> (the new Space) - held to exactly those four by
/// <c>SpaceSwitcherGateTests.The_SpaceId_key_is_set_by_exactly_the_named_units_of_work</c>. <b>Its value is the server's</b>:
/// the <c>OfferedSpaceId</c> the Invitation store wrote into the index at issue, found by the hash of the presented token,
/// never anything the request carried (AD-2). It sets no <c>AccountId</c> key, so it is not an Account-scoped surface and
/// <c>Membership</c>'s filter shows it this Space's rows and no other's.
/// </para>
/// <para>
/// <b>The one reader of the token index</b> (<see cref="InvitationToken"/>). A token that resolves to nothing - never
/// issued, malformed, or gone with its Space - opens the same transaction under a server-generated sentinel id that names
/// no Space and no Invitation, so the read that follows runs the same statements and sees nothing (Lee's Q6 (a)): the
/// work does not branch on whether the token was recognised.
/// </para>
/// <para>
/// <b>The application-state scope is narrowed to match</b> - <see cref="SpaceScope"/> to the resolved Space, never
/// <c>Resolve</c>d, because the caller holds no Membership there (yet) and no Role is read - and returns to the fail-closed
/// state when the unit of work ends. The view never completes it, so it commits nothing; the accept completes it only
/// after a materialised success.
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
/// <param name="spaceScope">The request's Space scope.</param>
/// <param name="identifiers">Produces the sentinel for a token that resolves to nothing.</param>
internal sealed class InvitationScope(YelloDbContext dbContext, SpaceScope spaceScope, IIdentifierGenerator identifiers)
    : IInvitationScope
{
    /// <inheritdoc />
    public Task<IInvitationUnitOfWork> BeginAsync(string presentedToken, Guid? accountId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(presentedToken);

        // Split for S4457.
        return BeginCoreAsync(presentedToken, accountId, cancellationToken);
    }

    private async Task<IInvitationUnitOfWork> BeginCoreAsync(string presentedToken, Guid? accountId, CancellationToken cancellationToken)
    {
        // Every presented string is hashed, well-formed or not, so a malformed token costs what a real one does.
        var hash = InvitationTokenSecret.Hash(presentedToken);

        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var resolved = await dbContext.Set<InvitationToken>()
                .AsNoTracking()
                .Where(token => token.TokenHash == hash)
                .Select(token => new { token.InvitationId, token.OfferedSpaceId })
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            var spaceId = resolved?.OfferedSpaceId ?? identifiers.Next();
            var invitationId = resolved?.InvitationId ?? identifiers.Next();

            await SpaceSessionContext.SetAsync(dbContext.Database, spaceId, cancellationToken).ConfigureAwait(false);

            spaceScope.Narrow(spaceId);

            return new UnitOfWork(transaction, new InvitationContext(spaceId, invitationId, accountId), spaceScope);
        }
        catch
        {
            spaceScope.Clear();
            await transaction.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// The open transaction and the context it runs under.
    /// </summary>
    private sealed class UnitOfWork(IDbContextTransaction transaction, InvitationContext context, SpaceScope spaceScope)
        : IInvitationUnitOfWork
    {
        /// <inheritdoc />
        public InvitationContext Context => context;

        /// <inheritdoc />
        public Task CompleteAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            spaceScope.Clear();

            return transaction.DisposeAsync();
        }
    }
}
