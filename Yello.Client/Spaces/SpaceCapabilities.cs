namespace Yello.Client.Spaces;

/// <summary>
/// Whether the Role held in the active Space may rename it, and whether it may reach Space settings at all - so the
/// rename section and the context bar's Space settings link are absent rather than disabled for the Roles that lack
/// them (UX-DR27). Story 3.1, AC3, AC4; open question 4 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>A presentation decision, never an authorisation one.</b> The server checks <c>RenameSpace</c> against the
/// capability matrix on every request and refuses <c>403</c> whatever the client offers.
/// </para>
/// <para>
/// <b>The link's audience is defined in one place</b>: <see cref="PermitsSpaceSettings"/> is the union of the page's
/// sections' predicates - the name section's and the Label section's - so the link is never offered where no section
/// would render, nor withheld where one would. Held against the matrix by
/// <c>RefusalBoundaryGateTests.The_clients_rename_predicate_agrees_with_the_capability_matrix</c> and
/// <c>The_clients_Space_settings_predicate_is_the_union_of_its_sections</c>.
/// </para>
/// </remarks>
public static class SpaceCapabilities
{
    /// <summary>
    /// Whether a Role may rename the Space (FR-16's <c>rename-space</c>).
    /// </summary>
    /// <param name="roleCode">The Role's stable code, from the active Space's answer.</param>
    /// <returns><c>true</c> for Owner and Admin; <c>false</c> for Member, Viewer and anything unknown.</returns>
    public static bool PermitsRenameSpace(string roleCode) =>
        roleCode is "owner" or "admin";

    /// <summary>
    /// Whether a Role may reach Space settings: whether any of its sections would render for it.
    /// </summary>
    /// <param name="roleCode">The Role's stable code, from the active Space's answer.</param>
    /// <returns><c>true</c> when the name section or the Label section is offered to the Role.</returns>
    public static bool PermitsSpaceSettings(string roleCode) =>
        PermitsRenameSpace(roleCode) || LabelCapabilities.PermitsManageLabels(roleCode);
}
