namespace Yello.Contracts.Accounts;

/// <summary>
/// The Account endpoints' paths, stated once for the client that calls them and the Host that
/// serves them.
/// </summary>
/// <remarks>
/// <para>
/// <b>In <c>Yello.Contracts</c> because a route is part of the wire contract.</b> The alternative
/// - the Host declaring its own path and the client writing the same string into a component -
/// is two sources of truth whose divergence produces a 404 at runtime and passes every build.
/// <c>Yello.Client</c> cannot reference <c>Yello.Host</c> (the ring table forbids the edge), so
/// the shared project is the only place both can read.
/// </para>
/// <para>
/// <b>The version segment is in the path, not in a header.</b> Story 8.2 owns versioning and
/// deprecation properly - <c>Asp.Versioning.Http</c> is already pinned for it - and this story
/// deliberately does not build any of that. What it does is avoid shipping an unversioned path
/// that 8.2 would then have to break: <c>/api/v1/accounts</c> costs nothing today and leaves 8.2
/// a shape to work with.
/// </para>
/// </remarks>
public static class AccountRoutes
{
    /// <summary>
    /// <c>POST</c> here to register an Account. Answers <c>204 No Content</c> whether the address
    /// was new or already registered (AD-23).
    /// </summary>
    public static string Register => "/api/v1/accounts";
}
