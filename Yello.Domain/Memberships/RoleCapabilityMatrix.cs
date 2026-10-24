namespace Yello.Domain.Memberships;

/// <summary>
/// The FR-16 Role capability matrix, as data: one decision per <see cref="Role"/> and
/// <see cref="Capability"/>. FR-16 / AR-9.
/// </summary>
/// <remarks>
/// <para>
/// <b>The matrix governs.</b> AC3: "where an individual restatement disagrees with the matrix the
/// matrix is correct and the restatement is a defect." This table is transcribed from
/// <c>role-capability-matrix.md:9-28</c>, and
/// <c>RefusalBoundaryGateTests.The_capability_matrix_agrees_with_the_transcription_in_every_cell</c>
/// compares every cell against a second, independent transcription.
/// </para>
/// <para>
/// <b>Consulted by the request pipeline and by nothing else.</b> AR-3 makes authorisation a pipeline
/// behaviour, so a slice never asks this table a question; the pipeline asks it once per
/// Space-scoped request, with the Role read off the Membership row on that request (AD-1). A gate
/// fails the build on any other dependent.
/// </para>
/// <para>
/// <b>Not a policy.</b> There is no <c>[Authorize(Roles=...)]</c>, no <c>RequireRole</c> and no
/// <c>IsInRole</c> anywhere in this solution (Gate C), because a single role claim cannot say which
/// of an Account's Spaces it applies to.
/// </para>
/// </remarks>
public static class RoleCapabilityMatrix
{
    private const CapabilityDecision Yes = CapabilityDecision.Allowed;
    private const CapabilityDecision No = CapabilityDecision.Denied;
    private const CapabilityDecision Qualified = CapabilityDecision.Qualified;

    /// <summary>
    /// The rows, in the matrix's order, with the Owner, Admin, Member and Viewer columns.
    /// </summary>
    private static readonly Dictionary<Capability, CapabilityDecision[]> Rows = new()
    {
        [Capability.ReadProjectsAndTasks] = [Yes, Yes, Yes, Yes],
        [Capability.ManageProjects] = [Yes, Yes, Yes, No],
        [Capability.ManageTasks] = [Yes, Yes, Yes, No],
        [Capability.EditTaskDescriptionCollaboratively] = [Yes, Yes, Yes, No],
        [Capability.AssignTask] = [Yes, Yes, Yes, No],
        [Capability.ConfigureSpaceDefaultStatuses] = [Yes, Yes, No, No],
        [Capability.ConfigureProjectStatusDelta] = [Yes, Yes, No, No],
        [Capability.ManageLabels] = [Yes, Yes, No, No],
        [Capability.IssueAndRevokeInvitations] = [Yes, Yes, No, No],
        [Capability.ChangeMembershipRole] = [Yes, Qualified, No, No],
        [Capability.RemoveMembership] = [Yes, Qualified, No, No],
        [Capability.RenameSpace] = [Yes, Yes, No, No],
        [Capability.OfferOwnershipTransfer] = [Yes, No, No, No],
        [Capability.DeleteSpace] = [Yes, No, No, No],
        [Capability.IssueApiTokenForOneself] = [Yes, Yes, Yes, Yes],
    };

    /// <summary>
    /// What the matrix says about a Role and a capability.
    /// </summary>
    /// <param name="role">The Role the acting Account's Membership carries, read on this request.</param>
    /// <param name="capability">The capability the endpoint declares.</param>
    /// <returns>The decision. Anything not in the table is <see cref="CapabilityDecision.Denied"/>.</returns>
    /// <remarks>
    /// Fails closed: a capability added to the enum and not to the table, or a Role value outside
    /// the four, is denied rather than defaulted.
    /// </remarks>
    public static CapabilityDecision Decide(Role role, Capability capability)
    {
        var column = role switch
        {
            Role.Owner => 0,
            Role.Admin => 1,
            Role.Member => 2,
            Role.Viewer => 3,
            _ => -1,
        };

        return column >= 0 && Rows.TryGetValue(capability, out var row)
            ? row[column]
            : CapabilityDecision.Denied;
    }
}
