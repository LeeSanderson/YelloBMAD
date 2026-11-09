using System.Linq.Expressions;
using Yello.Domain.Invitations;
using Yello.Domain.Spaces;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// The one shared expiry predicate: an offer is actionable while it is <c>Pending</c> and its expiry is still ahead by
/// the database clock. AR-31 / AD-27; story 4.1, AC8.
/// </summary>
/// <remarks>
/// <para>
/// <b>Declared once, here, and evaluated nowhere else.</b> Every guarded statement that decides on an offer - the Invitation's
/// duplicate-pending check, pending list and revoke (<see cref="InvitationStore"/>), its view, screen and accept, and
/// the Ownership Offer's reads, revoke, acceptance and decline (<see cref="OwnershipOfferStore"/>) - composes one of these
/// expressions into its own <c>WHERE</c> clause, so it translates to SQL and runs against <c>SYSDATETIMEOFFSET()</c>
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

    /// <summary>
    /// <c>State = Pending AND ExpiresAt &gt; SYSDATETIMEOFFSET()</c>, for an Ownership Offer.
    /// </summary>
    internal static Expression<Func<OwnershipOffer, bool>> OwnershipOfferActionable { get; } =
        offer => offer.State == OwnershipOfferState.Pending && offer.ExpiresAt > DatabaseClock.Now();

    /// <summary>
    /// A <c>Pending</c> Ownership Offer that is not actionable: its expiry has passed by the database clock.
    /// </summary>
    /// <remarks>
    /// <b>The one write it drives is a new offer's</b>, which lapses such a row before inserting so that a stale offer never
    /// holds the Space's one pending place. That is a request-path write caused by the new offer, not expiry being written:
    /// no job and no timer reads this.
    /// </remarks>
    internal static Expression<Func<OwnershipOffer, bool>> OwnershipOfferPendingButExpired { get; } =
        offer => offer.State == OwnershipOfferState.Pending && offer.ExpiresAt <= DatabaseClock.Now();
}
