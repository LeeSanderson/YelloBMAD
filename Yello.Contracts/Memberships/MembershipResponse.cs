namespace Yello.Contracts.Memberships;

/// <summary>
/// One Membership, as the Space's Membership list and the single-row read answer it to an Owner or Admin. A Role
/// change answers <see cref="MembershipRoleChangeResponse"/> instead, which carries no address, because the idempotency
/// record stores that answer whole.
/// </summary>
/// <remarks>
/// <para>
/// <b>Exactly six members</b>, held by a contract-shape gate: never the Account id, the normalised address, the Space or
/// the join date. One of the two response contracts that carry an email address (the other is the Invitation list's) -
/// addresses are visible in the Membership list and nowhere else in the product (Lee's Q4 (a)).
/// </para>
/// <para>
/// <b><see cref="InSpaceNow"/> is request recency, not an open editor</b>: an unexpired presence row for this Account in
/// this Space, read against the database clock. A Session has no server identity, so this is the only "live session"
/// signal there is. It is read under this Space's key and says nothing about any other Space.
/// </para>
/// </remarks>
/// <param name="Id">The Membership's id, which the Role change and the removal name.</param>
/// <param name="DisplayName">The Account's display name, read live for a current Member (Lee's Q5 (a)).</param>
/// <param name="EmailAddress">The Account's address as typed at registration, never the normalised form.</param>
/// <param name="Role">The Role, as its stable code: <c>owner</c>, <c>admin</c>, <c>member</c> or <c>viewer</c>.</param>
/// <param name="IsYou">Whether the row is the caller's own Membership.</param>
/// <param name="InSpaceNow">Whether the Account has made a request in this Space within the presence window.</param>
public sealed record MembershipResponse(Guid Id, string DisplayName, string EmailAddress, string Role, bool IsYou, bool InSpaceNow);
