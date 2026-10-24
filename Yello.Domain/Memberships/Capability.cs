namespace Yello.Domain.Memberships;

/// <summary>
/// Something a Role may or may not permit inside a Space - one member per Role-decided row of the
/// FR-16 capability matrix. FR-16 / AR-9.
/// </summary>
/// <remarks>
/// <para>
/// <b>Transcribed from <c>role-capability-matrix.md:9-28</c>, the SPEC companion</b>, which differs
/// from <c>prd.md:338-354</c> in two rows: "Transfer ownership" there is "Offer to transfer
/// ownership" here, and the companion adds "Accept or decline an Ownership Offer". The companion is
/// the source taken (story 1.6, decision 5).
/// </para>
/// <para>
/// <b>The Ownership-Offer acceptance row is deliberately absent.</b> AD-26 / AR-13 make it "the only
/// capability in Yello decided outside the FR-16 Role matrix": it is decided by row identity - the
/// Offer names its recipient - and never by Role. A member here would be one the Role check could
/// consult, which is exactly the mistake that row exists to prevent.
/// </para>
/// <para>
/// <b>Persisted by name.</b> The <c>AccessRefusal</c> record stores the member's name, as
/// <c>Membership</c> stores its <see cref="Role"/>, so reordering this enum is not a migration. On
/// the wire a refusal carries the kebab-case form from <see cref="StableCode"/>, which the client
/// uses as a resource key.
/// </para>
/// </remarks>
public enum Capability
{
    /// <summary>
    /// Read Projects and Tasks. Every Role.
    /// </summary>
    ReadProjectsAndTasks,

    /// <summary>
    /// Create, edit and delete Projects. Owner, Admin, Member.
    /// </summary>
    ManageProjects,

    /// <summary>
    /// Create, edit and delete Tasks. Owner, Admin, Member.
    /// </summary>
    ManageTasks,

    /// <summary>
    /// Edit a Task description collaboratively. Owner, Admin, Member.
    /// </summary>
    EditTaskDescriptionCollaboratively,

    /// <summary>
    /// Assign a Task. Owner, Admin, Member.
    /// </summary>
    AssignTask,

    /// <summary>
    /// Configure the Space's default Statuses. Owner, Admin.
    /// </summary>
    ConfigureSpaceDefaultStatuses,

    /// <summary>
    /// Configure a Project's Status delta. Owner, Admin.
    /// </summary>
    ConfigureProjectStatusDelta,

    /// <summary>
    /// Manage Labels. Owner, Admin.
    /// </summary>
    ManageLabels,

    /// <summary>
    /// Issue and revoke Invitations. Owner, Admin.
    /// </summary>
    IssueAndRevokeInvitations,

    /// <summary>
    /// Change a Membership's Role. Owner; Admin only between Member and Viewer, a target constraint
    /// stories 4.4 and 4.5 own.
    /// </summary>
    ChangeMembershipRole,

    /// <summary>
    /// Remove a Membership. Owner; Admin only for Members and Viewers, a target constraint stories
    /// 4.4 and 4.5 own.
    /// </summary>
    RemoveMembership,

    /// <summary>
    /// Rename the Space. Owner, Admin.
    /// </summary>
    RenameSpace,

    /// <summary>
    /// Offer to transfer ownership. Owner only.
    /// </summary>
    OfferOwnershipTransfer,

    /// <summary>
    /// Delete the Space. Owner only.
    /// </summary>
    DeleteSpace,

    /// <summary>
    /// Issue an API Token for oneself. Every Role. Listed per Space by the matrix; story 1.8 decides
    /// where its Account-scoped surface checks it.
    /// </summary>
    IssueApiTokenForOneself,
}
