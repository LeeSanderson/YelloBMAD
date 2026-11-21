using Yello.Application.Accounts.DeleteAccount;
using Yello.Application.Memberships;
using Yello.Application.Spaces;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Account deletion's three steps. The Account-scoped check and erase are <see cref="AccountSpaceDirectory"/>'s; each
/// Space is left on that Space's own unit of work, through the same leave a Member's "Leave this Space" runs.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sets no session key itself.</b> The <c>SpaceId</c> key is set by <see cref="IActiveSpaceResolver"/> and the
/// <c>AccountId</c> key by the directory, one unit of work at a time and never both on one connection: each unit of work
/// releases its connection to the pool when it ends, and the pool's reset clears the key before the next one opens.
/// </para>
/// <para>
/// <b>The Space scope is cleared after each leave.</b> A completed unit of work leaves its Space resolved for the rest of
/// the request, which a single-Space request never notices; this request goes on to another Space and to an
/// Account-scoped step, and neither may run with a Space still resolved.
/// </para>
/// </remarks>
/// <param name="directory">The Account-scoped steps.</param>
/// <param name="resolver">Opens each Space's unit of work.</param>
/// <param name="memberships">The leave.</param>
/// <param name="spaceScope">The request's Space scope.</param>
internal sealed class AccountDeletion(
    AccountSpaceDirectory directory,
    IActiveSpaceResolver resolver,
    IMembershipStore memberships,
    SpaceScope spaceScope)
    : IAccountDeletion
{
    /// <inheritdoc />
    public Task<AccountHoldings> CheckAsync(Guid accountId, CancellationToken cancellationToken) =>
        directory.HoldingsAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<SpaceLeave> LeaveAsync(Guid accountId, Guid spaceId, CancellationToken cancellationToken)
    {
        try
        {
            var unitOfWork = await resolver.BeginAsync(accountId, spaceId, cancellationToken).ConfigureAwait(false);

            if (unitOfWork is null)
            {
                return SpaceLeave.AlreadyGone;
            }

            await using (unitOfWork.ConfigureAwait(false))
            {
                var outcome = await memberships.LeaveAsync(accountId, cancellationToken).ConfigureAwait(false);

                switch (outcome)
                {
                    case RemovalOutcome.Removed:
                        await unitOfWork.CompleteAsync(cancellationToken).ConfigureAwait(false);
                        return SpaceLeave.Left;

                    case RemovalOutcome.OwnershipHeld:
                        return SpaceLeave.OwnershipHeld;

                    default:
                        return SpaceLeave.AlreadyGone;
                }
            }
        }
        finally
        {
            spaceScope.Clear();
        }
    }

    /// <inheritdoc />
    public Task<AccountErasure> EraseAsync(Guid accountId, CancellationToken cancellationToken) =>
        directory.EraseAsync(accountId, cancellationToken);
}
