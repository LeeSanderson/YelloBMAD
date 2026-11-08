namespace Yello.Contracts.Spaces;

/// <summary>
/// A pending Ownership Offer, as the Owner sees it.
/// </summary>
/// <remarks>
/// <b>Exactly five members</b>, held by a contract-shape gate, and no address: the offer's answer is recorded whole by the
/// idempotency record, which may hold none (AD-23). The client counts the days left as <see cref="ExpiresAt"/> minus
/// <see cref="ReadAt"/>, both from the database clock, never from the browser's.
/// </remarks>
/// <param name="Id">The offer, which the revoke names.</param>
/// <param name="RecipientMembershipId">The Membership offered ownership.</param>
/// <param name="RecipientName">The recipient's display name now.</param>
/// <param name="ExpiresAt">When the offer stops being actionable, by the database clock.</param>
/// <param name="ReadAt">The database clock when this answer was read.</param>
public sealed record OwnershipOfferResponse(
    Guid Id,
    Guid RecipientMembershipId,
    string RecipientName,
    DateTimeOffset ExpiresAt,
    DateTimeOffset ReadAt);
