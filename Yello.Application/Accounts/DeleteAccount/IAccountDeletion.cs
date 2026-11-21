using Yello.Application.Spaces;

namespace Yello.Application.Accounts.DeleteAccount;

/// <summary>
/// The three steps of deleting an Account, each its own transaction: an Account-scoped check, a leave of one Space on
/// that Space's own unit of work, and an Account-scoped erase.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why three transactions and not one.</b> The leave unassigns Tasks, lapses offers and deletes presence, and those
/// tables admit only the <c>SpaceId</c> session key, which is set read-only once per connection. One transaction across
/// several Spaces would need a unit of work that changes or holds both keys, which would relax Space isolation. The cost is that a
/// deletion stopped part-way has left some Spaces with the Account row still standing; sending it again finishes it.
/// </para>
/// <para>
/// Implemented in Infrastructure, so every session key stays there; the handler orders the steps and decides nothing by
/// Role.
/// </para>
/// </remarks>
public interface IAccountDeletion
{
    /// <summary>
    /// Under the Account's lock, reads the Spaces the Account owns and the Spaces it holds a Membership in. Writes nothing.
    /// </summary>
    /// <param name="accountId">The Account being deleted.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the Account owns and holds.</returns>
    Task<AccountHoldings> CheckAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>
    /// Leaves one Space through that Space's own leave: unassigned, offers naming it lapsed, presence and Tokens gone, the
    /// Membership deleted - or nothing, when the Account now owns the Space.
    /// </summary>
    /// <param name="accountId">The Account being deleted.</param>
    /// <param name="spaceId">The Space to leave.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the leave came to.</returns>
    Task<SpaceLeave> LeaveAsync(Guid accountId, Guid spaceId, CancellationToken cancellationToken);

    /// <summary>
    /// Under the Account's lock, erases the Account - its refusal records and its row - if it holds no Membership any
    /// more; otherwise erases nothing and says what it still holds.
    /// </summary>
    /// <param name="accountId">The Account being deleted.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>Whether it was erased, and what is left if not.</returns>
    Task<AccountErasure> EraseAsync(Guid accountId, CancellationToken cancellationToken);
}

/// <summary>
/// What an Account owns and holds.
/// </summary>
/// <param name="Owned">Every Space it owns, by name then id: identity only.</param>
/// <param name="Held">Every Space it holds a Membership in, by id.</param>
public sealed record AccountHoldings(IReadOnlyList<SpaceIdentity> Owned, IReadOnlyList<Guid> Held);

/// <summary>
/// What one Space's leave came to.
/// </summary>
public enum SpaceLeave
{
    /// <summary>
    /// The Membership is gone, with everything the leave takes with it.
    /// </summary>
    Left,

    /// <summary>
    /// There was no Membership to leave: something else removed it first.
    /// </summary>
    AlreadyGone,

    /// <summary>
    /// The Account owns the Space now, so nothing was written.
    /// </summary>
    OwnershipHeld,
}

/// <summary>
/// What the erase came to.
/// </summary>
/// <param name="Erased">The Account row is gone.</param>
/// <param name="Remaining">When not erased, what the Account still owns and holds.</param>
public sealed record AccountErasure(bool Erased, AccountHoldings Remaining);
