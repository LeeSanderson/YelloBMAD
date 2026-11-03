namespace Yello.Domain.Memberships;

/// <summary>
/// What the Role capability matrix says about one Role and one <see cref="Capability"/>.
/// </summary>
public enum CapabilityDecision
{
    /// <summary>
    /// The Role permits the capability, unconditionally.
    /// </summary>
    Allowed,

    /// <summary>
    /// The Role does not permit the capability. The pipeline answers <c>403</c>.
    /// </summary>
    Denied,

    /// <summary>
    /// The Role passes the Role gate, and a constraint on the <i>target</i> still applies - the
    /// matrix's "Member ↔ Viewer only" and "Members and Viewers only" cells. Marked separately so an
    /// unconditional tick is never mistaken for the whole rule: the Role change's target rule is
    /// <see cref="RoleCapabilityMatrix.RoleChangeAuthorityOf"/> (story 4.4), carried by the pipeline's acting
    /// authority; the removal's is story 4.5's.
    /// </summary>
    Qualified,
}
