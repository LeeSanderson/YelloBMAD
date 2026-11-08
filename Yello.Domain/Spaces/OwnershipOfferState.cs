namespace Yello.Domain.Spaces;

/// <summary>
/// The stored states of an <see cref="OwnershipOffer"/>. Stored as the member's name.
/// </summary>
/// <remarks>
/// <b>"Expired" is not one of them</b> (AD-27): an offer is expired when it is <see cref="Pending"/> and its <c>ExpiresAt</c>
/// has passed by the database clock, derived on read. <see cref="Lapsed"/> is only ever written by an event inside the
/// transaction that caused it.
/// </remarks>
public enum OwnershipOfferState
{
    /// <summary>
    /// Made and not yet answered, revoked or lapsed. At most one per Space, by a filtered unique index.
    /// </summary>
    Pending,

    /// <summary>
    /// Accepted by the recipient, which made it the Owner. Terminal.
    /// </summary>
    Accepted,

    /// <summary>
    /// Declined by the recipient. Terminal; no Role changed.
    /// </summary>
    Declined,

    /// <summary>
    /// Revoked by the offering Owner. Terminal; no Role changed.
    /// </summary>
    Revoked,

    /// <summary>
    /// Ended by an event rather than an answer: the recipient's Membership was removed or left, or a new offer in the Space
    /// found it past its expiry. Terminal; no Role changed.
    /// </summary>
    Lapsed,
}
