namespace Yello.Client.Spaces;

/// <summary>
/// Whether the Role held in the active Space may create and rename Projects - so the controls a Role
/// lacks are absent rather than disabled (UX-DR27). Story 2.1, open question 12 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>A presentation decision, never an authorisation one.</b> The server checks <c>ManageProjects</c>
/// against the capability matrix on every request and refuses <c>403</c> whatever the client offers;
/// this only keeps the client from offering what will be refused.
/// </para>
/// <para>
/// <b>Held against the matrix by a gate</b>:
/// <c>RefusalBoundaryGateTests.The_clients_Project_controls_predicate_agrees_with_the_capability_matrix</c>
/// asks it about every Role and compares the answer with <c>RoleCapabilityMatrix</c>, as the permit
/// phrases are held (<c>SpaceSwitcherGateTests</c>). The client cannot reference the Domain ring.
/// </para>
/// </remarks>
public static class ProjectCapabilities
{
    /// <summary>
    /// Whether a Role may create and rename Projects (FR-16's <c>manage-projects</c>).
    /// </summary>
    /// <param name="roleCode">The Role's stable code, from the active Space's answer.</param>
    /// <returns><c>true</c> for Owner, Admin and Member; <c>false</c> for Viewer and anything unknown.</returns>
    public static bool PermitsManageProjects(string roleCode) =>
        roleCode is "owner" or "admin" or "member";
}
