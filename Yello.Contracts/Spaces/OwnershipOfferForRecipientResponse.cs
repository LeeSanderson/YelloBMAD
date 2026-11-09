namespace Yello.Contracts.Spaces;

/// <summary>
/// The actionable Ownership Offer naming the caller's own Membership, as its recipient sees it.
/// </summary>
/// <remarks>
/// <b>Exactly four members</b>, held by a contract-shape gate: no recipient id, no address, no Role and no other Membership.
/// The offerer's name is the one captured when the offer was made, never a live read. The client counts the days left as
/// <see cref="ExpiresAt"/> minus <see cref="ReadAt"/>, both from the database clock, never from the browser's.
/// </remarks>
/// <param name="Id">The offer, which the acceptance and the decline name.</param>
/// <param name="OffererName">The offering Owner's display name, as captured on the offer.</param>
/// <param name="ExpiresAt">When the offer stops being actionable, by the database clock.</param>
/// <param name="ReadAt">The database clock when this answer was read.</param>
public sealed record OwnershipOfferForRecipientResponse(
    Guid Id,
    string OffererName,
    DateTimeOffset ExpiresAt,
    DateTimeOffset ReadAt);
