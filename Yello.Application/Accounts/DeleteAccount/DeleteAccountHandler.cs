using Yello.Application.Spaces;

namespace Yello.Application.Accounts.DeleteAccount;

/// <summary>
/// Deletes the signed-in Account: refused while it owns a Space, naming every one; otherwise it leaves every Space it
/// belongs to and is erased.
/// </summary>
/// <remarks>
/// <para>
/// <b>Leaves in id order, one Space per transaction</b>, then erases. A Membership that arrives meanwhile (an Invitation
/// accepted in another tab) is found by the erase and left in one more pass; one that keeps arriving after that is
/// answered <see cref="AccountDeletionOutcome.Interrupted"/> rather than chased in a loop another Account could keep
/// feeding.
/// </para>
/// <para>
/// <b>An Account that becomes an Owner part-way</b> - an offer of ownership accepted before its Space's leave ran - stops
/// the sequence with the owned-Spaces refusal, read again so it names what is owned now. Spaces already left stay left.
/// </para>
/// </remarks>
/// <param name="deletion">The three steps.</param>
public sealed class DeleteAccountHandler(IAccountDeletion deletion)
{
    private const int Passes = 2;

    /// <summary>
    /// Refuses, or leaves every Space and erases the Account, in at most two passes.
    /// </summary>
    /// <param name="context">The Account-scoped context the pipeline established.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the deletion came to.</returns>
    public Task<AccountDeletionResult> HandleAsync(AccountScopedContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Split for S4457.
        return HandleCoreAsync(context.AccountId, cancellationToken);
    }

    private async Task<AccountDeletionResult> HandleCoreAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var holdings = await deletion.CheckAsync(accountId, cancellationToken).ConfigureAwait(false);

        for (var pass = 0; pass < Passes; pass++)
        {
            if (holdings.Owned.Count > 0)
            {
                return AccountDeletionResult.OwnsSpaces(holdings.Owned);
            }

            if (!await LeaveEveryAsync(accountId, holdings.Held, cancellationToken).ConfigureAwait(false))
            {
                var now = await deletion.CheckAsync(accountId, cancellationToken).ConfigureAwait(false);

                return now.Owned.Count > 0 ? AccountDeletionResult.OwnsSpaces(now.Owned) : AccountDeletionResult.Interrupted;
            }

            var erasure = await deletion.EraseAsync(accountId, cancellationToken).ConfigureAwait(false);

            if (erasure.Erased)
            {
                return AccountDeletionResult.Deleted;
            }

            holdings = erasure.Remaining;
        }

        return holdings.Owned.Count > 0 ? AccountDeletionResult.OwnsSpaces(holdings.Owned) : AccountDeletionResult.Interrupted;
    }

    /// <summary>
    /// Leaves each Space in turn; <c>false</c> as soon as one is owned.
    /// </summary>
    private async Task<bool> LeaveEveryAsync(Guid accountId, IReadOnlyList<Guid> spaces, CancellationToken cancellationToken)
    {
        foreach (var spaceId in spaces)
        {
            var leave = await deletion.LeaveAsync(accountId, spaceId, cancellationToken).ConfigureAwait(false);

            if (leave == SpaceLeave.OwnershipHeld)
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// What a deletion came to.
/// </summary>
public enum AccountDeletionOutcome
{
    /// <summary>
    /// The Account is gone.
    /// </summary>
    Deleted,

    /// <summary>
    /// Refused: the Account owns at least one Space.
    /// </summary>
    OwnsSpaces,

    /// <summary>
    /// Not finished: Memberships kept arriving while it ran. Sending it again finishes it.
    /// </summary>
    Interrupted,
}

/// <summary>
/// A deletion's outcome, with the owned Spaces when it was refused for them.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Owned">On <see cref="AccountDeletionOutcome.OwnsSpaces"/>, every Space owned; otherwise empty.</param>
public sealed record AccountDeletionResult(AccountDeletionOutcome Outcome, IReadOnlyList<SpaceIdentity> Owned)
{
    /// <summary>
    /// The Account was deleted.
    /// </summary>
    public static AccountDeletionResult Deleted { get; } = new(AccountDeletionOutcome.Deleted, []);

    /// <summary>
    /// The deletion did not finish.
    /// </summary>
    public static AccountDeletionResult Interrupted { get; } = new(AccountDeletionOutcome.Interrupted, []);

    /// <summary>
    /// Refused while the Account owns these Spaces.
    /// </summary>
    /// <param name="owned">Every Space owned.</param>
    /// <returns>The result.</returns>
    public static AccountDeletionResult OwnsSpaces(IReadOnlyList<SpaceIdentity> owned) => new(AccountDeletionOutcome.OwnsSpaces, owned);
}
