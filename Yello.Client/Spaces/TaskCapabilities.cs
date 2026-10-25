namespace Yello.Client.Spaces;

/// <summary>
/// Whether the Role held in the active Space may create Tasks - so the controls a Role lacks are absent
/// rather than disabled (UX-DR27). Story 2.2, AC3.
/// </summary>
/// <remarks>
/// <para>
/// <b>A presentation decision, never an authorisation one.</b> The server checks <c>ManageTasks</c>
/// against the capability matrix on every request and refuses <c>403</c> whatever the client offers.
/// </para>
/// <para>
/// <b>Held against the matrix by a gate</b>:
/// <c>RefusalBoundaryGateTests.The_clients_Task_controls_predicate_agrees_with_the_capability_matrix</c>,
/// the sibling of <see cref="ProjectCapabilities"/>'s. The client cannot reference the Domain ring.
/// </para>
/// </remarks>
public static class TaskCapabilities
{
    /// <summary>
    /// Whether a Role may create Tasks (FR-16's <c>manage-tasks</c>).
    /// </summary>
    /// <param name="roleCode">The Role's stable code, from the active Space's answer.</param>
    /// <returns><c>true</c> for Owner, Admin and Member; <c>false</c> for Viewer and anything unknown.</returns>
    public static bool PermitsManageTasks(string roleCode) =>
        roleCode is "owner" or "admin" or "member";
}
