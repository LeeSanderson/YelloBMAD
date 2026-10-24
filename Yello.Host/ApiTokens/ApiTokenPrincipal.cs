using System.Globalization;
using System.Security.Claims;

namespace Yello.Host.ApiTokens;

/// <summary>
/// What an authenticated API Token request carries, and what it must never carry. AD-1, AD-4, AD-6;
/// story 1.8, AC9.
/// </summary>
/// <remarks>
/// <para>
/// <b>Built from the Token's row, never from the Token string.</b> The bearer value is only ever
/// hashed and looked up; the Account, the bound Space and the Token's id all come back from the
/// database on this request.
/// </para>
/// <para>
/// <b>Three claims of its own, and not the Session's.</b> The Space switcher, Account settings,
/// <c>sessions/current</c> and sign-out all read <c>SessionPrincipal.AccountIdOf</c>, which finds
/// nothing here - so a Token is a stranger to every Session surface by construction. No Role claim:
/// capability is the current Membership's, read per request.
/// </para>
/// <para>
/// <b>The Token id is for story 8.3</b>, which partitions the per-Token rate limit by it.
/// </para>
/// </remarks>
public static class ApiTokenPrincipal
{
    /// <summary>
    /// The Account that issued the Token.
    /// </summary>
    public static string AccountIdClaimType => "urn:yello:claim:token-account-id";

    /// <summary>
    /// The one Space the Token is bound to.
    /// </summary>
    public static string BoundSpaceIdClaimType => "urn:yello:claim:token-space-id";

    /// <summary>
    /// The Token itself.
    /// </summary>
    public static string TokenIdClaimType => "urn:yello:claim:token-id";

    /// <summary>
    /// The authentication type the identity is built with. Non-empty, so the identity authenticates.
    /// </summary>
    public static string AuthenticationType => "Yello.ApiToken";

    /// <summary>
    /// Builds the principal for a Token found on this request.
    /// </summary>
    /// <param name="tokenId">The Token.</param>
    /// <param name="accountId">The Account that issued it.</param>
    /// <param name="boundSpaceId">The Space it is bound to.</param>
    /// <returns>A principal carrying exactly the three Token claims.</returns>
    public static ClaimsPrincipal For(Guid tokenId, Guid accountId, Guid boundSpaceId) =>
        new(new ClaimsIdentity(
            [
                new Claim(AccountIdClaimType, accountId.ToString("D", CultureInfo.InvariantCulture)),
                new Claim(BoundSpaceIdClaimType, boundSpaceId.ToString("D", CultureInfo.InvariantCulture)),
                new Claim(TokenIdClaimType, tokenId.ToString("D", CultureInfo.InvariantCulture)),
            ],
            AuthenticationType));

    /// <summary>
    /// Reads the Token's Account and bound Space back off an authenticated principal.
    /// </summary>
    /// <param name="principal">The request's principal.</param>
    /// <returns>Both, or <c>null</c> when the principal is not an authenticated Token.</returns>
    public static (Guid AccountId, Guid BoundSpaceId)? Of(ClaimsPrincipal? principal)
    {
        if (principal?.Identity is not { IsAuthenticated: true, AuthenticationType: var type }
            || !string.Equals(type, AuthenticationType, StringComparison.Ordinal))
        {
            return null;
        }

        return Guid.TryParseExact(principal.FindFirst(AccountIdClaimType)?.Value, "D", out var accountId)
            && Guid.TryParseExact(principal.FindFirst(BoundSpaceIdClaimType)?.Value, "D", out var spaceId)
                ? (accountId, spaceId)
                : null;
    }

    /// <summary>
    /// Whether the request was authenticated by an API Token.
    /// </summary>
    /// <param name="principal">The request's principal.</param>
    /// <returns><c>true</c> for a Token principal.</returns>
    public static bool IsToken(ClaimsPrincipal? principal) => Of(principal) is not null;
}
