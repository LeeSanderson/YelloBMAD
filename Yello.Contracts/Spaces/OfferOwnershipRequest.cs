namespace Yello.Contracts.Spaces;

/// <summary>
/// The body of an Ownership Offer: the one Membership of the Space it names.
/// </summary>
/// <remarks>
/// <b>Exactly one member</b>, held by a contract-shape gate: an offer names a Membership, never an address, an Invitation or
/// an Account. Nullable so that a body without it reaches the endpoint's own <c>400 invalid-offer-recipient</c>.
/// </remarks>
/// <param name="MembershipId">The recipient's Membership.</param>
public sealed record OfferOwnershipRequest(Guid? MembershipId);
