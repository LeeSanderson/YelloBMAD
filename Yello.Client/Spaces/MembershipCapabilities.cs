namespace Yello.Client.Spaces;

/// <summary>
/// What the Members section of Space settings shows for a Role: whether it renders at all, and which Roles each row's
/// "Change Role" offers. Story 4.4, AC1, AC4, AC10; FR-13; UX-DR11, UX-DR27; PRD section 12 #4.
/// </summary>
/// <remarks>
/// <para>
/// <b>Presentation, never authority.</b> The server decides (the pipeline's capability check and the acting authority's
/// guarded statement); these mirror it so a control a Role lacks is absent rather than disabled (UX-DR27). Two gates hold
/// them to the matrix: <c>The_clients_Members_section_predicate_agrees_with_the_capability_matrix</c> and
/// <c>The_clients_Role_offer_agrees_with_the_target_rules_for_every_pair</c>.
/// </para>
/// <para>
/// <b>Owner is never offered</b>: ownership moves only by an Ownership Offer (FR-42). The Owner's own row, and every row an
/// Admin may not change (another Admin, the Owner), offers nothing.
/// </para>
/// </remarks>
public static class MembershipCapabilities
{
    private const string Owner = "owner";

    private const string Admin = "admin";

    private const string Member = "member";

    private const string Viewer = "viewer";

    private const int Hundred = 100;

    private static readonly IReadOnlyList<string> Everything = [Admin, Member, Viewer];

    private static readonly IReadOnlyList<string> BetweenMemberAndViewer = [Member, Viewer];

    /// <summary>
    /// The Memberships-per-Space bound the section's meta counts against ("{n} of 100"; AR-29, NFR-8).
    /// </summary>
    public static int MembershipsPerSpace => Hundred;

    /// <summary>
    /// Whether the Members section renders for a Role: Owner and Admin, the Roles <c>change-membership-role</c> admits
    /// (Lee's Q4 (a)).
    /// </summary>
    /// <param name="roleCode">The viewer's Role, as its stable code.</param>
    /// <returns><c>true</c> for Owner and Admin.</returns>
    public static bool PermitsManageMemberships(string roleCode) =>
        roleCode is Owner or Admin;

    /// <summary>
    /// The Roles a row offers, in the order the picker lists them: the viewer's target rule for the row's current Role.
    /// </summary>
    /// <param name="viewerRole">The viewer's Role, as its stable code.</param>
    /// <param name="targetRole">The row's Role, as its stable code.</param>
    /// <returns>Admin, Member and Viewer for the Owner on any other row; Member and Viewer for an Admin on a Member's or a
    /// Viewer's row; nothing otherwise.</returns>
    public static IReadOnlyList<string> OfferableRoles(string viewerRole, string targetRole) => (viewerRole, targetRole) switch
    {
        (Owner, Admin or Member or Viewer) => Everything,
        (Admin, Member or Viewer) => BetweenMemberAndViewer,
        _ => [],
    };
}
