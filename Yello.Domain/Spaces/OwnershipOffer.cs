namespace Yello.Domain.Spaces;

/// <summary>
/// An Owner's offer to hand its Space to one existing Membership of that Space. Making it moves nothing: the Owner stays the
/// Owner until the recipient accepts (FR-8, AD-26).
/// </summary>
/// <remarks>
/// <para>
/// <b>It names Memberships by id, with no foreign key to either</b>. A retained terminal offer outlives the Memberships it
/// names, which a key to <c>Membership</c> would either refuse (no action), erase (cascade) or blank together with the Space
/// (set null). That a <see cref="OwnershipOfferState.Pending"/> offer always names a current Membership rests instead on the
/// guarded insert and on the lapse a removal or leave writes, both under the Space's Memberships lock.
/// </para>
/// <para>
/// <b>Its state is written only by guarded statements</b>, each <c>WHERE State = Pending</c> with a rowcount check. Expiry is
/// derived, never stored: <see cref="ExpiresAt"/> is stamped by the database clock at insert and compared only by the shared
/// actionable predicate.
/// </para>
/// <para>
/// <b>Terminal offers are retained</b>; only the Space's deletion removes them.
/// </para>
/// </remarks>
public sealed class OwnershipOffer
{
    private const int LongestOffererName = 128;

    /// <summary>
    /// The longest offerer name the column holds: the Account display name's 128.
    /// </summary>
    public static int OffererNameMaxLength => LongestOffererName;

    /// <summary>
    /// The offer's identity, generated application-side (AR-34).
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The Space offered. Non-nullable (AD-2), and set once.
    /// </summary>
    public required Guid SpaceId { get; init; }

    /// <summary>
    /// The Membership offered ownership: any Role but the Owner's.
    /// </summary>
    public required Guid RecipientMembershipId { get; init; }

    /// <summary>
    /// The offering Owner's Membership.
    /// </summary>
    public required Guid OfferedByMembershipId { get; init; }

    /// <summary>
    /// The offering Owner's display name as it stood when the offer was made: attribution never reads a live Account name.
    /// </summary>
    public required string OffererName { get; init; }

    /// <summary>
    /// The stored state.
    /// </summary>
    public required OwnershipOfferState State { get; init; }

    /// <summary>
    /// When the offer stops being actionable, stamped by the database clock at insert.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// When the offer was made, in UTC.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
