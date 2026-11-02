namespace Yello.Contracts.Invitations;

/// <summary>
/// The accept's body: the token, and nothing else. Story 4.3, AC4; Lee's Q4 (a).
/// </summary>
/// <remarks>
/// <b>The token identifies the offer; it never authorises it</b> (FR-11, AD-28). The Space and the Role come from the
/// Invitation's row, and the authority is the signed-in Account's own address. Not named <c>Token</c>: that name is the
/// API Token issuance response's alone (<c>ApiTokenGateTests</c>).
/// </remarks>
/// <param name="InvitationToken">The token from the invitation link.</param>
public sealed record AcceptInvitationRequest(string? InvitationToken);
