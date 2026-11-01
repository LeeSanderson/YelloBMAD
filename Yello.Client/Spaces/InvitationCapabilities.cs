namespace Yello.Client.Spaces;

/// <summary>
/// Whether the Role held in the active Space may issue Invitations - so the invite form is absent rather than disabled for
/// the Roles that lack it (UX-DR27). Story 4.1, AC1, AC10.
/// </summary>
/// <remarks>
/// <para>
/// <b>A presentation decision, never an authorisation one.</b> The server checks <c>IssueAndRevokeInvitations</c>
/// against the capability matrix on every request and refuses <c>403</c> whatever the client offers.
/// </para>
/// <para>
/// <b>Held against the matrix by a gate</b>:
/// <c>RefusalBoundaryGateTests.The_clients_invite_predicate_agrees_with_the_capability_matrix</c>, and part of
/// <see cref="SpaceCapabilities.PermitsSpaceSettings"/>'s union. The client cannot reference the Domain ring.
/// </para>
/// </remarks>
public static class InvitationCapabilities
{
    /// <summary>
    /// The Roles an Invitation may offer, by stable code, in the picker's order: every Role but Owner (FR-10, FR-42; Lee's
    /// Q2 - an Admin may offer Admin too).
    /// </summary>
    public static IReadOnlyList<string> OfferableRoles { get; } = ["admin", "member", "viewer"];

    /// <summary>
    /// Whether a Role may issue Invitations (FR-16's <c>issue-and-revoke-invitations</c>).
    /// </summary>
    /// <param name="roleCode">The Role's stable code, from the active Space's answer.</param>
    /// <returns><c>true</c> for Owner and Admin; <c>false</c> for Member, Viewer and anything unknown.</returns>
    public static bool PermitsIssueInvitations(string roleCode) =>
        roleCode is "owner" or "admin";
}
