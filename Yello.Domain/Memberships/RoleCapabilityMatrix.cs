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
    /// The Owner's target rule for <see cref="Capability.ChangeMembershipRole"/>: every Role but Owner, from and to.
    /// </summary>
    private static readonly RoleChangeAuthority OwnerChanges = new(
        new HashSet<Role> { Role.Admin, Role.Member, Role.Viewer },
        new HashSet<Role> { Role.Admin, Role.Member, Role.Viewer },
        new HashSet<Role> { Role.Owner });

    /// <summary>
    /// The Admin's target rule for <see cref="Capability.ChangeMembershipRole"/>: "Member ↔ Viewer only".
    /// </summary>
    private static readonly RoleChangeAuthority AdminChanges = new(
        new HashSet<Role> { Role.Member, Role.Viewer },
        new HashSet<Role> { Role.Member, Role.Viewer },
        new HashSet<Role> { Role.Owner, Role.Admin });

    /// <summary>
    /// The Qualified cells' target rule for changing a Role (story 4.4, AC4; Lee's Q1 (a); <c>deferred-work.md:329</c>):
    /// the Roles <paramref name="actor"/> may change a Membership from and to, and the Roles it must still hold for that.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>FR-13</b> (<c>prd.md:293-301</c>): "An Admin can change Memberships between Member and Viewer only. Only an Owner
    /// can promote a Membership to Admin, or demote one from Admin" - the latter is PRD section 12 assumption #4
    /// (<c>prd.md:895</c>), still unconfirmed. "No Role change can produce a second Owner or remove the sole Owner", so
    /// <see cref="Role.Owner"/> is in no set for any actor (Lee's Q2 (a)).
    /// </para>
    /// <para>
    /// <b>Asked by the pipeline only</b>, with the Role it read on this request, like <see cref="Decide"/>. The answer
    /// carries sets, never the Role, so the slice that consumes it reads no Role. A Role the matrix denies the capability
    /// gets <see cref="RoleChangeAuthority.None"/>.
    /// </para>
    /// </remarks>
    /// <param name="actor">The Role the acting Account's Membership carries, read on this request.</param>
    /// <returns>The target rule.</returns>
    public static RoleChangeAuthority RoleChangeAuthorityOf(Role actor) => actor switch
    {
        Role.Owner => OwnerChanges,
        Role.Admin => AdminChanges,
        _ => RoleChangeAuthority.None,
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
