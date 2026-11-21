namespace Yello.Contracts.Spaces;

/// <summary>
/// The Space's most recent Ownership Offer, as its offering Owner is told it was declined.
/// </summary>
/// <remarks>
/// <b>Exactly two members</b>, held by a contract-shape gate: no address, no recipient id and no Role. The name is read now,
/// not captured, and is <c>null</c> once the recipient's Membership is gone, because a retained offer outlives the Membership
/// it names.
/// </remarks>
/// <param name="Id">The declined offer.</param>
/// <param name="RecipientName">The recipient's display name now, or <c>null</c> once its Membership is gone.</param>
public sealed record DeclinedOwnershipOfferResponse(Guid Id, string? RecipientName);
