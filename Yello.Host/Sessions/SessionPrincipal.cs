using System.Globalization;
using System.Security.Claims;

namespace Yello.Host.Sessions;

/// <summary>
/// What goes into a Session ticket, and what must never go into one. AC1, AD-1 / AR-4, NFR-2.
/// </summary>
/// <remarks>
/// <para>
/// <b>Exactly one claim: the Account's identity.</b> AC1 fixes a Session as identifying the
/// Account "and carr[ying] no Role, no Space and no capability of its own", and AD-1 states the
/// same rule from the other side - "Role is never read from a cookie, a claim, an API Token
/// payload, or any cache outliving the request". A ticket with one claim has nothing for a later
/// story to read a Role out of.
/// </para>
/// <para>
/// <b>Why AC2 is then true by construction, and why that must be said rather than tested
/// vacuously.</b> Nothing on the Session stores the active Space: the only two session-context
/// keys in the whole spine are <c>SESSION_CONTEXT('SpaceId')</c> (AD-2) and
/// <c>SESSION_CONTEXT('AccountId')</c> (AD-24), and the active Space arrives per request in the
/// <c>{spaceId}</c> route segment. There is therefore nothing here for a Space change to alter,
/// which makes AC2 a property of this method rather than a behaviour to implement. Requests for
/// two different Spaces on one Session are served each from their own Space
/// (<c>ActiveSpaceEndpointTests</c>); what is not yet exercised is a switch through the product,
/// because no Space switcher exists until story 1.7.
/// </para>
/// <para>
/// <b>A Yello-owned claim type rather than <c>ClaimTypes.NameIdentifier</c>.</b> The framework's
/// constants are a vocabulary in which <c>ClaimTypes.Role</c> is the next one to hand - Gate C
/// bans it by literal, and reaching into that namespace for one member invites reaching for the
/// other. A URN in Yello's own space says what this is and belongs to nothing else.
/// </para>
/// <para>
/// <b>No email address, deliberately.</b> <c>harness-constraints.md:63</c> marks "an Account is
/// unique by email address" as the load-bearing assumption OAuth sign-in will break and requires
/// this story to leave it soft. Resolving the Account <i>by</i> address at sign-in is fine; letting
/// the address reach a ticket that outlives the request is exactly what "do not let email-as-
/// identity reach the Session" forbids.
/// </para>
/// </remarks>
public static class SessionPrincipal
{
    /// <summary>
    /// The one claim type a Yello Session ticket carries.
    /// </summary>
    public static string AccountIdClaimType => "urn:yello:claim:account-id";

    /// <summary>
    /// The authentication type the identity is built with.
    /// </summary>
    /// <remarks>
    /// Non-empty, and that matters: a <see cref="ClaimsIdentity"/> constructed without an
    /// authentication type reports <c>IsAuthenticated == false</c> however many claims it holds,
    /// so an empty string here would issue tickets that authenticate nobody.
    /// </remarks>
    public static string AuthenticationType => "Yello.Session";

    /// <summary>
    /// Builds the principal a Session ticket is made from.
    /// </summary>
    /// <param name="accountId">The authenticated Account.</param>
    /// <returns>A principal carrying one claim and nothing else.</returns>
    public static ClaimsPrincipal For(Guid accountId) =>
        new(new ClaimsIdentity(
            [new Claim(AccountIdClaimType, accountId.ToString("D", CultureInfo.InvariantCulture))],
            AuthenticationType));

    /// <summary>
    /// Reads the Account identity back out of an authenticated principal.
    /// </summary>
    /// <param name="principal">The principal the cookie handler produced.</param>
    /// <returns>The Account, or <c>null</c> when the principal carries no usable identity.</returns>
    /// <remarks>
    /// Returns <c>null</c> rather than throwing on a malformed value. The principal is
    /// reconstructed from a ticket this application encrypted, so a claim that is not a
    /// <see cref="Guid"/> cannot arise from anything Yello wrote - but the input is a cookie, and a
    /// cookie is a thing a caller hands you.
    /// </remarks>
    public static Guid? AccountIdOf(ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirst(AccountIdClaimType)?.Value;

        return Guid.TryParseExact(value, "D", out var accountId) ? accountId : null;
    }
}
