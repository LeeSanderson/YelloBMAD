namespace Yello.Infrastructure.Persistence;

/// <summary>
/// The application-state half of the Account-scoped context: which Account this request's
/// Account-scoped read is for, held in memory and never read back from the database. AD-24 / AR-29,
/// AD-2 / AR-7.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="SpaceScope"/>'s twin, for the one table AD-24 widens.</b> <c>Membership</c>'s query
/// filter admits a row whose <c>SpaceId</c> is the request's Space <i>or</i> whose <c>AccountId</c>
/// is the request's Account, and this is where the second half comes from. Like the Space half it is
/// application state, so the EF filter stays an <i>independent</i> second layer: if the
/// <c>AccountId</c> session key were ever set wrongly, the filter would still exclude another
/// Account's Memberships, and the reverse.
/// </para>
/// <para>
/// <b>Narrowed only by the Account-scoped adapter, and only for the length of its unit of
/// work.</b> A Space-scoped unit of work never narrows it: if it did, the widened filter would show
/// that request the Account's Memberships in every other Space.
/// <c>SpaceIsolationLayerTests.A_Space_scoped_unit_of_work_sees_only_its_own_Membership_of_an_Account_in_several_Spaces</c>
/// is the standing gate. The mutators are internal for that reason.
/// </para>
/// <para>
/// <b>Request-scoped, and fails closed.</b> Until narrowed, <see cref="AccountId"/> is <c>null</c>,
/// and a filter comparing a non-nullable column with <c>null</c> matches no row - so a context built
/// with no scope sees no Membership through this half at all.
/// </para>
/// </remarks>
public sealed class AccountScope
{
    /// <summary>
    /// The Account this request's Account-scoped read is filtered to, or <c>null</c> for none.
    /// </summary>
    public Guid? AccountId { get; private set; }

    /// <summary>
    /// Narrows the <c>Membership</c> filter's Account half to one Account.
    /// </summary>
    /// <param name="accountId">The Account, from the Session.</param>
    internal void Narrow(Guid accountId) => AccountId = accountId;

    /// <summary>
    /// Returns to the fail-closed state once the Account-scoped unit of work is over.
    /// </summary>
    internal void Clear() => AccountId = null;
}
