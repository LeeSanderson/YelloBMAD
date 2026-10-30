namespace Yello.Host.Spaces;

/// <summary>
/// The Account-scoped surfaces AD-24 names, as amended on 2026-10-29 (story 3.1): its two read
/// surfaces, and the one Account-scoped unit of work that writes - Space creation. A further member
/// is another amendment to AD-24 rather than a code change. AD-24 / AR-29.
/// </summary>
/// <remarks>
/// Every endpoint the pipeline establishes an <c>AccountScopedContext</c> for carries one of these
/// as metadata (<see cref="AccountScopedResolution"/>), so the set of such endpoints is enumerable
/// and gated: <c>SpaceSwitcherGateTests.The_Account_scoped_surfaces_are_exactly_the_AD_24_names_as_amended</c>
/// and <c>Every_Account_scoped_endpoint_names_an_AD_24_surface</c>.
/// </remarks>
public enum AccountScopedSurface
{
    /// <summary>
    /// The Space switcher - <c>GET /api/v1/spaces</c>. Story 1.7.
    /// </summary>
    SpaceSwitcher,

    /// <summary>
    /// Account settings - the API Token list and revocation (story 1.8); Account deletion is 5.4's.
    /// </summary>
    AccountSettings,

    /// <summary>
    /// Space creation - <c>POST /api/v1/spaces</c>. Story 3.1; named by the AD-24 amendment of
    /// 2026-10-29. Not a read surface: it reads only the caller's own Membership count, for the
    /// Spaces-per-Account bound, and returns only the new Space's identity. The pipeline runs it on the
    /// Space creation unit of work (<c>ActiveSpaceResolution.CreateSpaceAsync</c>).
    /// </summary>
    SpaceCreation,
}
