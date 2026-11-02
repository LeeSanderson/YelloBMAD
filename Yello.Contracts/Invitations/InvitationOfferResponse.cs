namespace Yello.Contracts.Invitations;

/// <summary>
/// The Invitation view's answer: what is offered, by whom, and nothing else. Story 4.3, AC2; UX-DR18, Lee's Q5 (a).
/// </summary>
/// <remarks>
/// <b>Exactly these four members</b>, held by a contract-shape gate: never the invited address, the Space's, Invitation's
/// or issuer's id, the expiry, the state or a count. A link holder may be a stranger the mail was forwarded to.
/// </remarks>
/// <param name="SpaceName">The invited Space's name.</param>
/// <param name="Role">The Role offered, as its stable code: <c>admin</c>, <c>member</c> or <c>viewer</c>.</param>
/// <param name="IssuerName">The issuer's display name, as captured at issue.</param>
/// <param name="AddressedToYou">
/// With a Session, whether its Account's address is the invited one; <c>null</c> without one (a bearer Token included).
/// Neither address is ever returned (AC7).
/// </param>
public sealed record InvitationOfferResponse(string SpaceName, string Role, string IssuerName, bool? AddressedToYou);
