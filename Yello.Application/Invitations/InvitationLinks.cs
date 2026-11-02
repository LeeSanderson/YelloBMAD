namespace Yello.Application.Invitations;

/// <summary>
/// Builds the link an invitation email carries: <c>{ClientOrigin}/invitations/{token}</c>. AD-28, UX-DR18; story 4.1,
/// AC7. The link opens story 4.3's Invitation view (the client's <c>ClientRoutes.Invitation</c>, which a gate holds equal
/// to <see cref="Path"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The URL carries only the opaque token</b>: no Space name or id. The token identifies which offer is in play and is
/// never the authority for accepting it.
/// </para>
/// <para>
/// <b>With no usable client origin the link is origin-less</b> (<c>/invitations/{token}</c>). Such a Host already
/// refuses every browser request and says so at startup (<c>CrossOriginPolicy</c>); issuing still succeeds rather than
/// failing on configuration the issue itself does not need. Every deployed Host has the origin.
/// </para>
/// </remarks>
/// <param name="ClientOrigin">The configured client origin, or <c>null</c>.</param>
public sealed record InvitationLinks(Uri? ClientOrigin)
{
    /// <summary>
    /// The path under the client origin, before the token.
    /// </summary>
    public static string Path => "/invitations/";

    /// <summary>
    /// The link for a token.
    /// </summary>
    /// <param name="token">The token's plaintext.</param>
    /// <returns>The link.</returns>
    public Uri For(string token) =>
        ClientOrigin is { } origin
            ? new Uri(origin.GetLeftPart(UriPartial.Authority) + Path + token, UriKind.Absolute)
            : new Uri(Path + token, UriKind.Relative);
}
