namespace Yello.Client.Spaces;

/// <summary>
/// Whether the Role held in the active Space may shape a Project's Statuses - so the "Project settings" link and every
/// control of the Status delta editor are absent rather than disabled for the Roles that lack it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A presentation decision, never an authorisation one.</b> The server checks <c>ConfigureProjectStatusDelta</c>
/// against the capability matrix on every write and refuses <c>403</c> whatever the client offers.
/// </para>
/// <para>
/// <b>Held against the matrix by a gate</b>:
/// <c>RefusalBoundaryGateTests.The_clients_Project_Status_delta_predicate_agrees_with_the_capability_matrix</c>. The
/// client cannot reference the Domain ring.
/// </para>
/// </remarks>
public static class StatusCapabilities
{
    /// <summary>
    /// Whether a Role may add, rename and reorder a Project's Statuses (<c>configure-project-status-delta</c>).
    /// </summary>
    /// <param name="roleCode">The Role's stable code, from the active Space's answer.</param>
    /// <returns><c>true</c> for Owner and Admin; <c>false</c> for Member, Viewer and anything unknown.</returns>
    public static bool PermitsConfigureProjectStatusDelta(string roleCode) =>
        roleCode is "owner" or "admin";

    /// <summary>
    /// Whether a Role may add, rename and reorder the Space's default Statuses (<c>configure-space-default-statuses</c>):
    /// the Default Statuses section of Space settings and every control in it, the rename dialog included. Held against the
    /// matrix by <c>The_clients_Space_default_Status_predicate_agrees_with_the_capability_matrix</c>.
    /// </summary>
    /// <param name="roleCode">The Role's stable code, from the active Space's answer.</param>
    /// <returns><c>true</c> for Owner and Admin; <c>false</c> for Member, Viewer and anything unknown.</returns>
    public static bool PermitsConfigureSpaceDefaultStatuses(string roleCode) =>
        roleCode is "owner" or "admin";
}
