namespace Yello.Contracts.Accounts;

/// <summary>
/// The anti-forgery contract between the two origins: the header the request token travels in,
/// and the shape the token endpoint answers with. AR-14 / AD-7.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a cross-origin JSON API needs this at all, when .NET 10 registers a CSRF middleware by
/// default.</b> That middleware is real and is already active - <c>WebApplication.CreateBuilder</c>
/// adds it - and it inspects <c>Sec-Fetch-Site</c>, falling back to <c>Origin</c>, trusting
/// whatever the CORS policy trusts. What it does <i>not</i> do is enforce anything here: its
/// verdict is recorded on <c>IAntiforgeryValidationFeature</c> and read only by a <i>form</i>
/// consumer, so a JSON endpoint that binds no form data records a verdict nobody reads. AR-14
/// requires that "every state-changing request carries an anti-forgery token", and a middleware
/// that records an unread verdict does not carry one. Hence the token system beside it.
/// </para>
/// <para>
/// <b>The token travels in a header, and the cookie half stays <c>HttpOnly</c>.</b> The usual
/// browser recipe puts the request token in a cookie the page reads back with JavaScript; that
/// would mean reading <c>document.cookie</c> from WebAssembly through interop, and it puts a
/// credential-shaped value somewhere a script can reach. Fetching it from
/// <see cref="SessionRoutes.AntiforgeryToken"/> and holding it in a field costs one request and
/// keeps both halves out of anything scriptable - which is also what keeps AC5's "no credential is
/// ever written to <c>localStorage</c> or <c>sessionStorage</c>" true without relying on nobody
/// reaching for them.
/// </para>
/// <para>
/// <b>This is not what stops cross-site requests; CORS is.</b> The token is the belt AR-14 names
/// and the configured-origin CORS policy is the braces - and the braces are load-bearing here,
/// because the .NET 10 middleware reads the same policy. Neither is a substitute for the other:
/// a wildcard origin would disarm both at once, which is the second, independent reason AC5's
/// wildcard prohibition cannot be satisfied by a wildcard that "happens to work".
/// </para>
/// </remarks>
public static class Antiforgery
{
    /// <summary>
    /// The header a state-changing request carries its anti-forgery request token in.
    /// </summary>
    /// <remarks>
    /// A custom header is itself part of the defence: a browser will not send one cross-origin
    /// without a successful CORS preflight, so an attacker's page cannot produce this request at
    /// all against an origin the policy does not name.
    /// </remarks>
    public static string HeaderName => "X-XSRF-TOKEN";
}

/// <summary>
/// What <see cref="SessionRoutes.AntiforgeryToken"/> answers with.
/// </summary>
/// <param name="RequestToken">
/// The token to send back in <see cref="Antiforgery.HeaderName"/>. Paired with a cookie the same
/// response sets; neither half is accepted without the other.
/// </param>
public sealed record AntiforgeryTokenResponse(string RequestToken);
