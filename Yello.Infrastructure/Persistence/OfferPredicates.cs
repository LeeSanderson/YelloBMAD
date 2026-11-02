using System.Linq.Expressions;
using Yello.Domain.Invitations;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// The one shared expiry predicate: an offer is actionable while it is <c>Pending</c> and its expiry is still ahead by
/// the database clock. AR-31 / AD-27; story 4.1, AC8.
/// </summary>
/// <remarks>
/// <para>
/// <b>Declared once, here, and evaluated nowhere else.</b> Every guarded statement that decides on an offer - 4.1's
/// duplicate-pending check, 4.2's pending list and its revoke (<see cref="InvitationStore"/>), 4.3's view, screen and accept, and
/// 5.1-5.3's Ownership Offers after them - composes this
/// expression into its own <c>WHERE</c> clause, so it translates to SQL and runs against <c>SYSDATETIMEOFFSET()</c>
/// inside that statement. No <c>TimeProvider</c> and no C# instant appear in it; a gate holds that this is the only
/// place an expiry is compared.
/// </para>
/// <para>
/// <b>No job and no timer ever writes "expired".</b> An expired offer is a <c>Pending</c> row whose expiry has passed:
/// derived on read, never stored, never deleted.
/// </para>
/// </remarks>
internal static class OfferPredicates
{
    /// <summary>
    /// <c>State = Pending AND ExpiresAt &gt; SYSDATETIMEOFFSET()</c>, for an Invitation.
    /// </summary>
    internal static Expression<Func<Invitation, bool>> Actionable { get; } =
        invitation => invitation.State == InvitationState.Pending && invitation.ExpiresAt > DatabaseClock.Now();
}
