namespace Yello.Contracts.Spaces;

/// <summary>
/// The response header that carries the Role a Space-scoped request was judged under, as the Role's stable code
/// (<c>owner</c>, <c>admin</c>, <c>member</c> or <c>viewer</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Present on every <c>2xx</c> and on the <c>403 insufficient-role</c></b> of a request that resolved the caller's
/// Membership, so an open tab learns a changed Role from the next answer it gets, a read included, without polling.
/// </para>
/// <para>
/// <b>Never on a <c>404</c></b>, of either kind, nor on any answer given before or without a Membership. A header only the
/// in-Space <c>404</c> carried would tell it apart from the boundary's, which must stay indistinguishable.
/// </para>
/// <para>
/// <b>Live on a replay:</b> an <c>Idempotency-Key</c> replay carries the Role of the replaying request, never a stored one.
/// </para>
/// </remarks>
public static class RoleHeader
{
    /// <summary>
    /// The response header.
    /// </summary>
    public static string HeaderName => "Yello-Role";
}
