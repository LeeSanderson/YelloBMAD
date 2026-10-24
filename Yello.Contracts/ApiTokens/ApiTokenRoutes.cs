namespace Yello.Contracts.ApiTokens;

/// <summary>
/// The API Token endpoints' paths, stated once for the client that calls them and the Host that
/// serves them. Story 1.8.
/// </summary>
/// <remarks>
/// <b>Issue is Space-scoped; list and revoke are Account settings.</b> A Token is written to the
/// Space it is bound to, so issuing carries <c>{spaceId}</c> and the pipeline resolves the
/// Membership, checks the capability and records a boundary refusal as for any Space-scoped route.
/// Listing and revoking read and change the Account's own Tokens across its Spaces, on AD-24's
/// second Account-scoped surface. All three are Session-only: a Token can never issue, list or revoke
/// Tokens (AC9).
/// </remarks>
public static class ApiTokenRoutes
{
    /// <summary>
    /// <c>POST</c> here, with an <see cref="IssueApiTokenRequest"/>, to issue a Token bound to the
    /// Space. <c>201</c> with an <see cref="IssuedApiTokenResponse"/> - the only response that ever
    /// carries the plaintext.
    /// </summary>
    public static string Issue => "/api/v1/spaces/{spaceId:guid}/api-tokens";

    /// <summary>
    /// <c>GET</c> here for the Account's Tokens, as <see cref="ApiTokenResponse"/> rows.
    /// </summary>
    public static string List => "/api/v1/account/api-tokens";

    /// <summary>
    /// <c>DELETE</c> here to revoke one of the Account's Tokens. <c>204</c> whether or not it was one.
    /// </summary>
    public static string Revoke => "/api/v1/account/api-tokens/{tokenId:guid}";

    /// <summary>
    /// The route parameter naming a Token.
    /// </summary>
    public static string TokenIdParameter => "tokenId";

    /// <summary>
    /// The issue path for one Space.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The path.</returns>
    public static string IssueIn(Guid spaceId) => $"/api/v1/spaces/{spaceId:D}/api-tokens";

    /// <summary>
    /// The revoke path for one Token.
    /// </summary>
    /// <param name="tokenId">The Token.</param>
    /// <returns>The path.</returns>
    public static string RevokeOf(Guid tokenId) => $"/api/v1/account/api-tokens/{tokenId:D}";
}
