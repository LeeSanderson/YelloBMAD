namespace Yello.Client.Spaces;

/// <summary>
/// What the Members section of Space settings shows for a Role: whether it renders at all, which Roles each row's
/// "Change Role" offers and which rows carry "Remove"; and whether Space home offers "Leave this Space".
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
    /// Whether <paramref name="roleCode"/>'s authority to change a Role is the Admin's "Member ↔ Viewer only" - so a
    /// <c>403</c> on a change is that limit, said as such, and any other Role's <c>403</c> is the refusal's own copy (code
    /// review 2026-11-03).
    /// </summary>
    /// <param name="roleCode">The viewer's Role, as its stable code.</param>
    /// <returns><c>true</c> for an Admin.</returns>
    public static bool IsLimitedToMemberAndViewer(string roleCode) => roleCode is Admin;

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

    /// <summary>
    /// Whether a row carries Remove: the viewer's removal rule for the row's Role. The Owner removes every other row; an
    /// Admin removes Member and Viewer rows; nobody's own row carries Remove - leaving is Space home's "Leave this Space" -
    /// and the Owner's row carries it for no one.
    /// </summary>
    /// <param name="viewerRole">The viewer's Role, as its stable code.</param>
    /// <param name="targetRole">The row's Role, as its stable code.</param>
    /// <param name="isYou">Whether the row is the viewer's own.</param>
    /// <returns><c>true</c> where the server's removal rule admits the row.</returns>
    public static bool Removable(string viewerRole, string targetRole, bool isYou) =>
        !isYou && (viewerRole, targetRole) is (Owner, Admin or Member or Viewer) or (Admin, Member or Viewer);

    /// <summary>
    /// Whether "Leave this Space" is offered: every Role but the Owner, who leaves only by an accepted Ownership Offer or by
    /// deleting the Space.
    /// </summary>
    /// <param name="roleCode">The viewer's Role, as its stable code.</param>
    /// <returns><c>true</c> for Admin, Member and Viewer.</returns>
    public static bool MayLeave(string roleCode) => roleCode is Admin or Member or Viewer;
}
