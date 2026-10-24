namespace Yello.Host.Spaces;

/// <summary>
/// The Account-scoped surfaces AD-24 names - exactly two, and a third is an amendment to AD-24
/// rather than a code change. AD-24 / AR-29.
/// </summary>
/// <remarks>
/// Every endpoint the pipeline establishes an <c>AccountScopedContext</c> for carries one of these
/// as metadata (<see cref="AccountScopedResolution"/>), so the set of such endpoints is enumerable
/// and gated: <c>SpaceSwitcherGateTests.The_Account_scoped_surfaces_are_exactly_the_two_AD_24_names</c>
/// and <c>Every_Account_scoped_endpoint_names_an_AD_24_surface</c>.
/// </remarks>
public enum AccountScopedSurface
{
    /// <summary>
    /// The Space switcher - <c>GET /api/v1/spaces</c>. Story 1.7.
    /// </summary>
    SpaceSwitcher,

    /// <summary>
    /// Account settings. Reserved: stories 1.8 (API Tokens) and 5.4 (deletion) map it.
    /// </summary>
    AccountSettings,
}
