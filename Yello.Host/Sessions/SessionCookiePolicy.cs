using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Yello.Host.Sessions;

/// <summary>
/// What a Yello Session <i>is</i>, stated in one place: an encrypted cookie ticket carrying an
/// Account identity, expiring at a fixed instant, with no row in any table. AC1, AC3, AC5,
/// AD-7 / AR-14.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing upstream states a Session lifetime, a sliding rule, a rotation rule or a
/// revocation-on-password-change rule.</b> Verified by grep across the PRD, the spec set, the
/// architecture spine, the epics, the readiness report and <c>decisions-settled.md</c>, which has
/// no session row at all. So the answer below is a decision this story took, and it is written
/// here rather than inline in the composition root so that there is one place to read it and one
/// place to change it.
/// </para>
/// <para>
/// <b>The decision: a pure cookie ticket, absolute expiry, no sliding, no database row.</b>
/// AD-27 (<c>ARCHITECTURE-SPINE.md:241</c>) requires anything expiring by the passage of time to
/// carry an expiry evaluated "server-side inside the guarded statement's own <c>WHERE</c> clause",
/// and <b>fails the build on a scheduled component writing a terminal expiry state</b>; AD-10
/// (<c>:136</c>) forbids any component querying the database on a fixed interval. A ticket's expiry
/// is carried in the encrypted payload and evaluated when the cookie is read, so it satisfies both
/// by construction - there is no table to sweep, no sweeper to schedule and no per-request read to
/// add to a free-tier database that auto-pauses after fifteen minutes.
/// </para>
/// <para>
/// <b>AC3's word "invalidated" does not by itself require a server-side Session record.</b>
/// Membership removal revokes <i>access</i>, and per-request Membership resolution - story 1.5's -
/// makes that immediate: the Session survives and grants nothing, which is exactly what AC3 asks
/// for. Read against AD-1, where authorisation is a per-request <c>(Account, Space)</c> decision
/// and "Role is never read from a cookie, a claim, an API Token payload, or any cache outliving the
/// request", a revocation list would be a second mechanism for something the first already
/// achieves. If one is ever genuinely needed it is <c>ExpiresAt</c>-shaped and evaluated on read,
/// per AD-27.
/// </para>
/// <para>
/// <b>Not <c>AddIdentityCookies</c>, and not <c>AddIdentity</c>.</b>
/// <c>AddIdentity&lt;TUser, TRole&gt;</c> requires a role type and fails Gate C outright.
/// <c>AddIdentityCookies</c> is roleless but brings Identity's security-stamp validator, whose
/// <c>ValidationInterval</c> is a re-read of the user store on a timer - which is the shape AD-10
/// bans - and story 1.3 registered <c>PasswordHasher&lt;HashedCredential&gt;</c> directly and took
/// no Identity builder at all. Plain cookie authentication is the same ticket without either, and
/// it keeps "the whole of Identity that Yello wires" true.
/// </para>
/// <para>
/// <b>Configured once, here, because AD-7 says so in as many words.</b> Its <i>Prevents</i> clause
/// bans "per-endpoint cookie configuration" - stronger than AR-14's paraphrase, and the binding
/// text. Nothing outside this class sets a cookie attribute.
/// </para>
/// <para>
/// <b>Public rather than internal, and reachable by name.</b> Story 1.1 established that logic a
/// test must reach cannot live inside <c>Program.cs</c> - deleting an entire startup call there
/// left 52 of 52 tests green. AC5's cookie attributes are a contract, so
/// <c>SessionContractTests</c> asserts them against the options this class produces as well as
/// against a real <c>Set-Cookie</c> header from a running Host. Asserting only the header would
/// leave the policy untested on every attribute a header does not spell out; asserting only the
/// options would leave the wiring untested. Both, because they fail differently.
/// </para>
/// </remarks>
public static class SessionCookiePolicy
{
    /// <summary>
    /// The configuration key that overrides <see cref="DefaultLifetime"/>.
    /// </summary>
    /// <remarks>
    /// <b>It exists so that a test can watch the framework's own expiry evaluation happen.</b>
    /// A Session expiring eight hours from now cannot be observed expiring by any test this
    /// repository permits: <c>Task.Delay</c> as a synchronisation mechanism is banned outright,
    /// and the Host runs as a separate process so no in-process <c>TimeProvider</c> reaches it.
    /// A <i>negative</i> lifetime makes every ticket born already expired, which turns AC3 into an
    /// ordinary assertion against a real cookie rather than a claim about a code path.
    /// </remarks>
    public static string LifetimeConfigurationKey => "Session:Lifetime";

    /// <summary>
    /// The scheme name. One scheme, and it is the default for both authenticating and
    /// challenging.
    /// </summary>
    public static string SchemeName => CookieAuthenticationDefaults.AuthenticationScheme;

    /// <summary>
    /// The cookie's name.
    /// </summary>
    /// <remarks>
    /// The <c>__Host-</c> prefix was considered and rejected: it requires <c>Path=/</c>, no
    /// <c>Domain</c> and <c>Secure</c> - all true here - but browsers additionally refuse it on a
    /// cookie carrying <c>SameSite=None</c> in several current implementations, and AC5 requires
    /// <c>SameSite=None</c> because the client and the API are distinct origins. The prefix is
    /// worth revisiting the day story 1.10 chooses a same-origin deployment.
    /// </remarks>
    public static string CookieName => "yello_session";

    /// <summary>
    /// How long a Session lasts, absolutely, from the moment it is issued.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Eight hours, and it is a working day rather than a derived figure</b> - because nothing
    /// upstream derives one. Long enough that nobody is asked who they are twice in a day's work,
    /// which is the story's own sentence ("moving between contexts never asks me who I am again");
    /// short enough that a browser left open overnight on a shared machine is not still signed in.
    /// </para>
    /// <para>
    /// <b>Absolute, not sliding.</b> A sliding window has no maximum: a tab polling anything keeps
    /// a Session alive indefinitely, and "how long does a Session last" then has no answer at all.
    /// An absolute expiry is also the only shape AD-27's "evaluated on read" reads cleanly as -
    /// there is one instant in the payload, and reading it is the whole evaluation.
    /// </para>
    /// <para>
    /// <b>What happens on a password change: nothing, because there is no password-change
    /// flow.</b> No FR, CAP or journey in the artifact set defines one, and story 5.4 (delete your
    /// account) is the only Account-mutating story in the epics. Recorded rather than designed for:
    /// whichever story adds one inherits the question, and the honest answer for a stateless ticket
    /// is that existing Sessions survive until this lifetime elapses.
    /// </para>
    /// </remarks>
    public static TimeSpan DefaultLifetime => TimeSpan.FromHours(DefaultLifetimeHours);

    /// <summary>
    /// A working day. A named constant because S109 refuses the literal, and because the number is
    /// the decision.
    /// </summary>
    private const int DefaultLifetimeHours = 8;

    /// <summary>
    /// Applies the whole policy to the cookie handler's options.
    /// </summary>
    /// <param name="options">The handler's options.</param>
    /// <param name="lifetime">
    /// How long an issued ticket is good for. <see cref="ReadLifetime"/> resolves it from
    /// configuration.
    /// </param>
    /// <remarks>
    /// Reachable by name from a test, and deliberately so: story 1.1 found that a static local
    /// function inside a top-level program cannot be reached at all, and deleting an entire startup
    /// call left 52 of 52 tests green. The cookie's attributes are AC5's contract, so they are
    /// asserted directly against the options this method produces as well as against a real
    /// <c>Set-Cookie</c> header.
    /// </remarks>
    public static void Configure(CookieAuthenticationOptions options, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Cookie.Name = CookieName;

        // THE THREE ATTRIBUTES AC5 NAMES, AND THEY ARE ONE DECISION RATHER THAN THREE.
        // SameSite=None is required because the client and the API are distinct origins, and every
        // current browser rejects SameSite=None without Secure - so the pair is not separable.
        // HttpOnly is what keeps the ticket out of reach of any script, which is the same
        // guarantee AC5's "no credential is ever written to localStorage or sessionStorage" is
        // after, arriving by the route the browser enforces rather than the one we promise.
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.IsEssential = true;

        options.ExpireTimeSpan = lifetime;
        options.SlidingExpiration = false;

        // NO REDIRECTS. The cookie handler's default challenge is a 302 to /Account/Login, which
        // for an API means a caller receives a redirect to a page that does not exist instead of
        // being told its Session is no good - and a browser following it cross-origin would report
        // a CORS failure, which is a third wrong answer. AD-3 puts the 403/404 line at the Space
        // boundary and nowhere else; an unauthenticated request has not reached one, so the answer
        // is 401 and it is this story's to give.
        //
        // Set even though nothing in this story challenges: the endpoints read HttpContext.User
        // directly and answer for themselves (see SessionEndpoints). Leaving the framework default
        // in place would mean the first story to write [Authorize] inherits a 302 nobody chose.
        options.Events.OnRedirectToLogin = RefuseWithStatusCodeAsync;
        options.Events.OnRedirectToAccessDenied = RefuseWithStatusCodeAsync;
    }

    /// <summary>
    /// Resolves the ticket lifetime from configuration, falling back to
    /// <see cref="DefaultLifetime"/>.
    /// </summary>
    /// <param name="configured">
    /// The configured value, or <c>null</c>/blank when none is set - which is the ordinary case and
    /// is why the default is a constant rather than a required setting.
    /// </param>
    /// <returns>The lifetime to issue tickets for.</returns>
    /// <remarks>
    /// An unparseable value falls back to the default rather than throwing. AR-33's shape is that
    /// the process starts and reports the problem rather than refusing to boot, and the failure
    /// mode of throwing here is a Host that will not start because somebody typed a duration
    /// wrongly - while the failure mode of falling back is a Session lasting the documented eight
    /// hours. The second is plainly the better one to have. <b>"Reports the problem" was not
    /// implemented until code review on 2026-09-19</b> - the fallback was silent, so the sentence
    /// above described something nothing did. Use the overload below to get the diagnostic; the
    /// composition root logs it.
    /// </remarks>
    public static TimeSpan ReadLifetime(string? configured) => ReadLifetimeSetting(configured).Lifetime;

    /// <summary>
    /// Resolves the ticket lifetime and says what was wrong with the configured value, if anything.
    /// </summary>
    /// <param name="configured">The configured value, or <c>null</c>/blank when none is set.</param>
    /// <returns>
    /// The lifetime to issue tickets for, and the problem with the configured value - <c>null</c>
    /// when it was absent or entirely sound. An absent value is not a problem: it is the ordinary
    /// case, and the default is documented. Returned as a pair rather than through an <c>out</c>
    /// parameter, which S3874 refuses.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>A bare integer is refused, because <c>TimeSpan</c> reads it as DAYS.</b>
    /// <c>TimeSpan.TryParse("8")</c> succeeds and yields <c>8.00:00:00</c> - eight days. For a
    /// setting documented in hours, <c>8</c> is the obvious thing an operator types, and it silently
    /// bought a Session lasting twenty-four times its documented life with no warning anywhere,
    /// because the value parsed fine and the "unparseable falls back" safety net never fired.
    /// Requiring a <c>:</c> makes the unit explicit and costs nothing: every value anybody means
    /// here has one. Found at code review on 2026-09-19.
    /// </para>
    /// <para>
    /// <b>A zero or negative lifetime is still honoured, and still reported.</b> It cannot simply be
    /// refused: the negative value is the mechanism AC3's expiry test depends on - it makes every
    /// ticket born already expired, which is how a real framework expiry evaluation gets observed
    /// without <c>Task.Delay</c>. So it stays legal and becomes loud. Left silent, the symptom is a
    /// Host where sign-in returns 204 with a cookie, the client reports success, and every
    /// subsequent request answers 401: authentication completely down, with no error anywhere.
    /// </para>
    /// </remarks>
    public static LifetimeSetting ReadLifetimeSetting(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return new LifetimeSetting(DefaultLifetime, Problem: null);
        }

        if (!configured.Contains(':', StringComparison.Ordinal))
        {
            return new LifetimeSetting(
                DefaultLifetime,
                $"'{configured}' is not a duration - a bare number is read as DAYS. "
                + "Write it as hh:mm:ss (eight hours is '08:00:00'). Falling back to the default.");
        }

        if (!TimeSpan.TryParse(configured, CultureInfo.InvariantCulture, out var lifetime))
        {
            return new LifetimeSetting(
                DefaultLifetime,
                $"'{configured}' could not be read as a duration. Write it as hh:mm:ss. "
                + "Falling back to the default.");
        }

        return new LifetimeSetting(
            lifetime,
            lifetime <= TimeSpan.Zero
                ? $"'{configured}' is zero or negative, so every Session is issued already expired "
                    + "and nobody can stay signed in. This is deliberate in the tests that observe "
                    + "expiry; anywhere else it is a mistake."
                : null);
    }

    private static Task RefuseWithStatusCodeAsync<TOptions>(RedirectContext<TOptions> context)
        where TOptions : AuthenticationSchemeOptions
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;

        return Task.CompletedTask;
    }
}

/// <summary>
/// A resolved Session lifetime, and what was wrong with the configured value that produced it.
/// </summary>
/// <remarks>
/// A pair rather than an <c>out</c> parameter because S3874 refuses the latter. The problem travels
/// beside the value rather than being thrown, because AR-33's shape is that the process starts and
/// reports the problem: the composition root logs it and boots on the default.
/// </remarks>
/// <param name="Lifetime">The lifetime to issue tickets for.</param>
/// <param name="Problem">
/// A sentence naming what was wrong with the configured value, or <c>null</c> when it was absent or
/// entirely sound.
/// </param>
public sealed record LifetimeSetting(TimeSpan Lifetime, string? Problem);
