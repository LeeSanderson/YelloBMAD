namespace Yello.Contracts.Memberships;

/// <summary>
/// One Membership, as the Space's Membership list answers it to an Owner or Admin, and as a Role change answers the row it
/// changed. Story 4.4, AC1, AC2, AC4; UX-DR14, UX-DR34, AD-23.
/// </summary>
/// <remarks>
/// <b>Exactly five members</b>, held by a contract-shape gate: never the Account id, the normalised address, the Space or
/// the join date. One of the two response contracts that carry an email address (the other is the Invitation list's) -
/// addresses are visible in the Membership list and nowhere else in the product (Lee's Q4 (a)).
/// </remarks>
/// <param name="Id">The Membership's id, which the Role change names.</param>
/// <param name="DisplayName">The Account's display name, read live for a current Member (Lee's Q5 (a)).</param>
/// <param name="EmailAddress">The Account's address as typed at registration, never the normalised form.</param>
/// <param name="Role">The Role, as its stable code: <c>owner</c>, <c>admin</c>, <c>member</c> or <c>viewer</c>.</param>
/// <param name="IsYou">Whether the row is the caller's own Membership.</param>
public sealed record MembershipResponse(Guid Id, string DisplayName, string EmailAddress, string Role, bool IsYou);
