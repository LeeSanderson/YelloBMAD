namespace Yello.Client.Spaces;

/// <summary>
/// Whether the Role held in the active Space may define and delete Labels - so the Space settings link and the define
/// form are absent rather than disabled for the Roles that lack it (UX-DR27). Story 2.4, AC1.
/// </summary>
/// <remarks>
/// <para>
/// <b>A presentation decision, never an authorisation one.</b> The server checks <c>ManageLabels</c> against the
/// capability matrix on every request and refuses <c>403</c> whatever the client offers. Applying a Label is a Task
/// edit, offered by <see cref="TaskCapabilities"/>.
/// </para>
/// <para>
/// <b>Held against the matrix by a gate</b>:
/// <c>RefusalBoundaryGateTests.The_clients_Label_definition_predicate_agrees_with_the_capability_matrix</c>. The
/// client cannot reference the Domain ring.
/// </para>
/// </remarks>
public static class LabelCapabilities
{
    /// <summary>
    /// Whether a Role may define and delete Labels (FR-16's <c>manage-labels</c>).
    /// </summary>
    /// <param name="roleCode">The Role's stable code, from the active Space's answer.</param>
    /// <returns><c>true</c> for Owner and Admin; <c>false</c> for Member, Viewer and anything unknown.</returns>
    public static bool PermitsManageLabels(string roleCode) =>
        roleCode is "owner" or "admin";
}
