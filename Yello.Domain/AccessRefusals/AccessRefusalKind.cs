namespace Yello.Domain.AccessRefusals;

/// <summary>
/// The two refusals AD-20 records, and no third. NFR-7 requires them to be distinguishable in the
/// record, even though the first is never distinguished on the wire.
/// </summary>
public enum AccessRefusalKind
{
    /// <summary>
    /// A Space-boundary <c>404</c>: the caller holds no Membership in the named Space, <i>or</i> no
    /// such Space exists. The two causes are one kind here, as they are one answer on the wire (AD-3).
    /// </summary>
    CrossSpace,

    /// <summary>
    /// A <c>403</c>: the caller is a member, and the Role the Membership carries does not permit the
    /// capability the endpoint declares.
    /// </summary>
    InsufficientRole,
}
