namespace Yello.Contracts.Invitations;

/// <summary>
/// One pending Invitation, as the Space's list answers it to an Owner or Admin. Story 4.2, AC1, open question 5 (a);
/// FR-12, UX-DR14.
/// </summary>
/// <remarks>
/// Exactly the address, the Role and the issuer UX-DR14 names, the id the revoke needs and the issue date. Never the token
/// hash, the normalised address, the issuer's Account, the Space, the state or the expiry - a contract-shape gate holds
/// these five members.
/// </remarks>
/// <param name="Id">The Invitation's id, which the revoke names.</param>
/// <param name="EmailAddress">The address as issued, trimmed.</param>
/// <param name="Role">The Role offered, as its stable code: <c>admin</c>, <c>member</c> or <c>viewer</c>.</param>
/// <param name="IssuerName">The issuer's display name as captured at issue - never a live Account lookup.</param>
/// <param name="IssuedAt">When it was issued.</param>
public sealed record InvitationResponse(Guid Id, string EmailAddress, string Role, string IssuerName, DateTimeOffset IssuedAt);
