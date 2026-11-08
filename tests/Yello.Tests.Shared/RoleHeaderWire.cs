namespace Yello.Tests.Shared;

/// <summary>
/// The Role the pipeline judged a Space-scoped request under, as its answer echoes it: a response header carrying the Role's
/// stable code on every <c>2xx</c> and on the <c>403 insufficient-role</c>, and on nothing else.
/// </summary>
/// <remarks>
/// <see cref="HeaderName"/> is a stand-in for the contract constant (<c>Yello.Contracts.Spaces.RoleHeader.HeaderName</c>),
/// which does not exist yet. Swap every use for the contract member when it lands, then delete this member.
/// </remarks>
public static class RoleHeaderWire
{
    /// <summary>
    /// The header's name.
    /// </summary>
    public static string HeaderName => "Yello-Role";

    /// <summary>
    /// The Role stable codes the header may carry.
    /// </summary>
    public static IReadOnlyList<string> RoleCodes { get; } = ["owner", "admin", "member", "viewer"];
}
