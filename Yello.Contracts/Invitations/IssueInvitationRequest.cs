namespace Yello.Contracts.Invitations;

/// <summary>
/// The body of an Invitation issue: the address and the Role, and nothing else. The Space is the route's; the expiry is
/// the database's; the token is never on the wire. FR-10; story 4.1, AC1.
/// </summary>
/// <param name="EmailAddress">The address to invite: any plausible address, with or without an Account.</param>
/// <param name="Role">The Role's stable code: <c>admin</c>, <c>member</c> or <c>viewer</c> - never <c>owner</c>.</param>
public sealed record IssueInvitationRequest(string? EmailAddress, string? Role);
