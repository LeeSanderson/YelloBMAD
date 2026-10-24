using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Yello.Domain.ApiTokens;
using Yello.Host.Spaces;

namespace Yello.Host.ApiTokens;

/// <summary>
/// Authenticates a request carrying <c>Authorization: Bearer</c> by looking its Token up - on every
/// request, with no cache. FR-36; AD-4, AD-6; story 1.8, AC8 and AC9.
/// </summary>
/// <remarks>
/// <para>
/// <b>A Token is a credential only where it may act: a Space-scoped route that is not
/// Session-only.</b> Anywhere else - the Space switcher, Account settings, <c>sessions/current</c>,
/// sign-out, registration, sign-in, or a Space-scoped route marked <see cref="SessionOnlyEndpoint"/>
/// - this handler authenticates nothing, so the request is answered exactly as one carrying no
/// credential at all. The rule is decided by the route's shape and marker, like Space resolution, so
/// a route added later is covered because of what it is.
/// </para>
/// <para>
/// <b>Nothing found is not a failure</b>, deliberately: a malformed, unknown, revoked or invalidated
/// Token is <see cref="AuthenticateResult.NoResult"/>, so the request continues unauthenticated and
/// the pipeline's one <c>401</c> answers it - the same bytes as no credential. A failure result would
/// put a message in the log for each presented value.
/// </para>
/// </remarks>
internal sealed class ApiTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!ApiTokenAuthentication.TryReadBearer(Request, out var presented)
            || !ApiTokenAuthentication.AdmitsTokens(Context))
        {
            return AuthenticateResult.NoResult();
        }

        var holder = await Context.RequestServices.GetRequiredService<IApiTokenStore>()
            .FindAsync(presented, Context.RequestAborted)
            .ConfigureAwait(false);

        if (holder is null)
        {
            return AuthenticateResult.NoResult();
        }

        var principal = ApiTokenPrincipal.For(holder.TokenId, holder.AccountId, holder.BoundSpaceId);

        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}

/// <summary>
/// The Token scheme's names, its registration, and the <see cref="SessionOnlyEndpoint"/> marker.
/// Story 1.8, open question 5.
/// </summary>
internal static class ApiTokenAuthentication
{
    /// <summary>
    /// The Token authentication scheme.
    /// </summary>
    internal const string SchemeName = "Yello.ApiToken";

    /// <summary>
    /// The default scheme: a policy that forwards to the Token scheme when the request carries a
    /// bearer, and to the Session cookie otherwise.
    /// </summary>
    internal const string SelectorScheme = "Yello.Credential";

    private const string BearerPrefix = "Bearer ";

    /// <summary>
    /// Registers the default selector, the Session cookie and the Token scheme.
    /// </summary>
    /// <remarks>
    /// <b>A request carrying a bearer is authenticated by the Token alone</b>, and any cookie on it is
    /// never read - so "which credential?" has one answer, and a Session cannot sit beside a Token on
    /// one request. Every <c>SignInAsync</c> and <c>SignOutAsync</c> names the cookie scheme, so the
    /// selector never decides where a Session is written or ended.
    /// </remarks>
    /// <param name="authentication">The authentication builder, whose default is the selector.</param>
    /// <returns>The same builder, for chaining.</returns>
    internal static AuthenticationBuilder AddApiTokens(this AuthenticationBuilder authentication) =>
        authentication
            .AddPolicyScheme(SelectorScheme, displayName: null, options =>
                options.ForwardDefaultSelector = context =>
                    TryReadBearer(context.Request, out _) ? SchemeName : Sessions.SessionCookiePolicy.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ApiTokenAuthenticationHandler>(SchemeName, configureOptions: null);

    /// <summary>
    /// Marks a Space-scoped endpoint a Token may not reach: a request presenting one is answered as
    /// though it carried no credential.
    /// </summary>
    /// <param name="builder">The endpoint.</param>
    /// <returns>The same builder, for chaining.</returns>
    internal static RouteHandlerBuilder SessionOnly(this RouteHandlerBuilder builder) =>
        builder.WithMetadata(SessionOnlyEndpoint.Instance);

    /// <summary>
    /// Whether the endpoint serving this request admits a Token: Space-scoped, and not Session-only.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <returns><c>true</c> when a Token may authenticate it.</returns>
    internal static bool AdmitsTokens(HttpContext httpContext) =>
        ActiveSpaceResolution.IsSpaceScoped(httpContext)
        && httpContext.GetEndpoint()?.Metadata.GetMetadata<SessionOnlyEndpoint>() is null;

    /// <summary>
    /// Reads a bearer value off the request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="presented">The value after <c>Bearer </c>, untrimmed beyond the scheme.</param>
    /// <returns><c>true</c> when the request carries <c>Authorization: Bearer</c>.</returns>
    internal static bool TryReadBearer(HttpRequest request, out string presented)
    {
        string? header = request.Headers.Authorization;

        if (header is not null && header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            presented = header[BearerPrefix.Length..].Trim();

            return true;
        }

        presented = string.Empty;

        return false;
    }
}

/// <summary>
/// The metadata that makes a Space-scoped endpoint unreachable with a Token.
/// </summary>
internal sealed class SessionOnlyEndpoint
{
    /// <summary>
    /// The one instance.
    /// </summary>
    internal static SessionOnlyEndpoint Instance { get; } = new();

    private SessionOnlyEndpoint()
    {
    }
}
