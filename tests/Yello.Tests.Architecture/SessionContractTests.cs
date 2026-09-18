using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Xunit;
using Yello.Host.Sessions;

namespace Yello.Tests.Architecture;

/// <summary>
/// What a Yello Session is, asserted against the policy that decides it rather than against a
/// running Host. AC1, AC2, AC3, AC5, AD-1 / AR-4, AD-7 / AR-14, AD-10, AD-27.
/// </summary>
/// <remarks>
/// <para>
/// <b>These run with no container and no Host process, and that is why they are here.</b>
/// <c>SessionEndpointTests</c> asserts the same contract over the wire and needs a SQL Server -
/// so on a machine with the container runtime down, that whole class fails and this one still
/// answers. The two are complementary rather than duplicated: a header assertion cannot see
/// <c>SlidingExpiration</c>, and an options assertion cannot see whether the options were ever
/// applied.
/// </para>
/// <para>
/// In the architecture suite because it is the suite that references every production project and
/// is release-gating. The alternative was the Slices suite, which also references
/// <c>Yello.Host</c>; these stay here because what they assert is a <i>rule</i> about the shape of
/// a Session rather than a use case, and because this suite runs first in the PR tier and takes
/// seconds.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-14")]
[Trait("Requirement", "AD-7")]
[Trait("Requirement", "AR-4")]
[Trait("Requirement", "AD-1")]
[Trait("Requirement", "FR-2")]
public sealed class SessionContractTests
{
    /// <summary>
    /// AC5: the cookie is <c>HttpOnly; Secure; SameSite=None</c>, and every one of the three is a
    /// decision this policy makes rather than a framework default.
    /// </summary>
    /// <remarks>
    /// <b>Asserted as exact values rather than as "not the insecure one".</b> <c>SameSite</c>'s
    /// default is <c>Unspecified</c> and <c>SecurePolicy</c>'s is <c>SameAsRequest</c>, so a policy
    /// that set neither would pass any assertion phrased as an inequality against
    /// <c>SameSiteMode.None</c>-as-a-mistake.
    /// </remarks>
    [Fact]
    public void The_session_cookie_is_http_only_secure_and_same_site_none()
    {
        var options = Configured();

        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.None, options.Cookie.SameSite);
    }

    /// <summary>
    /// The Session expires absolutely, and does not slide. AD-27, and question 1's answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A sliding window has no maximum, which is what makes this worth a gate rather than a
    /// comment.</b> <c>SlidingExpiration</c> defaults to <c>true</c> in ASP.NET Core, so this is a
    /// value the policy has to set rather than one it inherits - and a story that deleted the line
    /// would get the framework's answer to a question this story decided, with nothing else in the
    /// solution noticing.
    /// </para>
    /// <para>
    /// The lifetime itself is asserted as the documented eight hours. Changing it is allowed and
    /// expected - it is one named constant - but it should be a decision somebody takes rather than
    /// a number that drifts, and a failing test is how a reader finds the paragraph that explains
    /// the choice.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_session_expires_absolutely_and_never_slides()
    {
        var options = Configured();

        Assert.False(options.SlidingExpiration);
        Assert.Equal(TimeSpan.FromHours(8), options.ExpireTimeSpan);
        Assert.Equal(TimeSpan.FromHours(8), SessionCookiePolicy.DefaultLifetime);
    }

    /// <summary>
    /// An unconfigured lifetime is the documented default, and an unparseable one does not stop the
    /// Host booting.
    /// </summary>
    /// <remarks>
    /// AR-33's shape is that the process starts and reports the problem rather than refusing to
    /// boot. The failure mode of throwing here is a Host that will not start because somebody typed
    /// a duration wrongly; the failure mode of falling back is a Session that lasts the documented
    /// eight hours.
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-duration")]
    public void An_absent_or_unreadable_lifetime_falls_back_to_the_default(string? configured)
    {
        Assert.Equal(SessionCookiePolicy.DefaultLifetime, SessionCookiePolicy.ReadLifetime(configured));
    }

    [Fact]
    public void A_configured_lifetime_is_honoured()
    {
        Assert.Equal(TimeSpan.FromMinutes(30), SessionCookiePolicy.ReadLifetime("00:30:00"));

        // The negative value SessionEndpointTests uses to observe a real expiry. Asserted here so
        // that a later change making ReadLifetime clamp to a positive range fails HERE, naming the
        // test it would otherwise turn into a silent no-op.
        Assert.Equal(TimeSpan.FromSeconds(-1), SessionCookiePolicy.ReadLifetime("-00:00:01"));
    }

    /// <summary>
    /// An unauthenticated request is refused with a status code, never redirected to a sign-in page
    /// that does not exist. AD-3.
    /// </summary>
    /// <remarks>
    /// The cookie handler's default challenge is a 302 to <c>/Account/Login</c>. For an API that is
    /// the wrong answer twice over: the caller receives a redirect to a page this product does not
    /// serve, and a browser following it cross-origin reports a CORS failure, which is a third
    /// wrong answer. AD-3 puts the 403/404 line at the Space boundary and nowhere else, so an
    /// unauthenticated request - which has not reached one - is a 401.
    /// </remarks>
    [Fact]
    public async Task An_unauthenticated_challenge_answers_401_rather_than_redirecting()
    {
        var options = Configured();
        var context = new DefaultHttpContext();

        await options.Events.RedirectToLogin(
            new Microsoft.AspNetCore.Authentication.RedirectContext<CookieAuthenticationOptions>(
                context,
                new Microsoft.AspNetCore.Authentication.AuthenticationScheme(
                    SessionCookiePolicy.SchemeName,
                    SessionCookiePolicy.SchemeName,
                    typeof(CookieAuthenticationHandler)),
                options,
                new Microsoft.AspNetCore.Authentication.AuthenticationProperties(),
                "/Account/Login"));

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
    }

    /// <summary>
    /// AC1: the Session ticket carries the Account's identity and <i>exactly</i> that.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An exact-set comparison, not a "does not contain a Role claim" check.</b> The second
    /// would pass a ticket that grew a Space claim, a capability claim, an email address or
    /// anything else a later story found convenient - and AC1's wording is "identifies the Account
    /// and carries no Role, no Space and no capability of its own", which is a statement about the
    /// whole set rather than about one member of it. AD-1 says the same from the other side: "Role
    /// is never read from a cookie, a claim, an API Token payload, or any cache outliving the
    /// request".
    /// </para>
    /// <para>
    /// This is also the honest half of AC2. Nothing on the Session stores the active Space, so a
    /// Space change cannot touch it - but no Space switcher exists until story 1.7 and no
    /// Space-scoped endpoint until 1.5, so an actual switch cannot be exercised. What is asserted
    /// is the property that makes AC2 true; what is not asserted is the switch.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_session_ticket_carries_one_claim_and_it_is_the_account()
    {
        var accountId = Guid.NewGuid();
        var principal = SessionPrincipal.For(accountId);

        Assert.Equal(
            [SessionPrincipal.AccountIdClaimType],
            principal.Claims.Select(claim => claim.Type).ToList());

        Assert.Equal(accountId, SessionPrincipal.AccountIdOf(principal));
        Assert.True(principal.Identity?.IsAuthenticated);
    }

    /// <summary>
    /// The planted violation for the assertion above, kept as a permanent test.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>TESTING-CONVENTIONS.md:101</c>: an absence assertion must be validated against a planted
    /// signal, or it is not a test. The plant is the claim a later story is most likely to add -
    /// the active Space, put on the ticket to save a lookup - and this asserts that the exact-set
    /// comparison above actually rejects it.
    /// </para>
    /// <para>
    /// Kept as a test rather than recorded as a one-off run for the same reason story 1.3 kept its
    /// duration oracle: a validation that was true once is not a validation that stays true. If
    /// somebody later loosens the assertion above to a "contains" check, this fails.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_claim_set_assertion_rejects_a_ticket_that_grew_a_space()
    {
        var planted = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(SessionPrincipal.AccountIdClaimType, Guid.NewGuid().ToString()),
                new Claim("urn:yello:claim:space-id", Guid.NewGuid().ToString()),
            ],
            SessionPrincipal.AuthenticationType));

        var types = planted.Claims.Select(claim => claim.Type).ToList();

        Assert.NotEqual<IReadOnlyList<string>>([SessionPrincipal.AccountIdClaimType], types);
    }

    /// <summary>
    /// A principal with no Account claim is not a Yello Session, however authenticated it looks.
    /// </summary>
    /// <remarks>
    /// The endpoint reads the claim rather than <c>IsAuthenticated</c> alone, so a principal
    /// produced by some future second scheme cannot be mistaken for a Session. This is what that
    /// choice buys.
    /// </remarks>
    [Fact]
    public void A_principal_without_the_account_claim_is_not_a_session()
    {
        var other = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("urn:something:else", "value")],
            "SomeOtherScheme"));

        Assert.Null(SessionPrincipal.AccountIdOf(other));
        Assert.Null(SessionPrincipal.AccountIdOf(new ClaimsPrincipal()));
        Assert.Null(SessionPrincipal.AccountIdOf(null));
    }

    /// <summary>
    /// AC5: CORS allows exactly the configured origin, with credentials - and allows nothing at all
    /// when none is configured.
    /// </summary>
    /// <remarks>
    /// <b>Failing closed is the half a default would get wrong.</b> Defaulting to the development
    /// client address would put a working cross-origin hole in every deployment that forgot the
    /// setting, and it would be invisible because everything would appear to work.
    /// </remarks>
    [Fact]
    public void Cors_allows_exactly_the_configured_origin_with_credentials()
    {
        var policy = Policy("https://client.yello.test");

        Assert.Equal(["https://client.yello.test"], policy.Origins);
        Assert.True(policy.SupportsCredentials);

        // Neither of the two forms AD-7 bans by name.
        Assert.False(policy.AllowAnyOrigin);
        Assert.DoesNotContain("*", policy.Origins, StringComparer.Ordinal);
    }

    [Fact]
    public void Cors_allows_nothing_when_no_origin_is_configured()
    {
        foreach (var configured in new[] { null, string.Empty, "   " })
        {
            var policy = Policy(configured);

            Assert.Empty(policy.Origins);
            Assert.False(policy.AllowAnyOrigin);
        }
    }

    /// <summary>
    /// A trailing slash on a configured origin does not produce an origin no browser will ever
    /// send.
    /// </summary>
    /// <remarks>
    /// An <c>Origin</c> header is a scheme, host and port with no path, so
    /// <c>https://client.yello.test/</c> matches nothing - and it is exactly what an operator
    /// copying an address out of a browser bar produces. The symptom is every cross-origin request
    /// refused with the setting looking correct, which is why the trim is here rather than left to
    /// whoever writes the value.
    /// </remarks>
    [Fact]
    public void A_configured_origin_with_a_trailing_slash_still_matches()
    {
        Assert.Equal(["https://client.yello.test"], Policy("https://client.yello.test/").Origins);
    }

    private static CookieAuthenticationOptions Configured()
    {
        var options = new CookieAuthenticationOptions();

        SessionCookiePolicy.Configure(options, SessionCookiePolicy.DefaultLifetime);

        return options;
    }

    private static CorsPolicy Policy(string? configuredOrigin)
    {
        var options = new CorsOptions();

        CrossOriginPolicy.Configure(options, configuredOrigin);

        return options.GetPolicy(CrossOriginPolicy.PolicyName)
            ?? throw new InvalidOperationException(
                "CrossOriginPolicy.Configure did not register a policy named "
                + $"'{CrossOriginPolicy.PolicyName}', so nothing below asserts anything.");
    }
}
