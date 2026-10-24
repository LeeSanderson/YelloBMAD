using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Accounts;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Accounts.AuthenticateAccount;

/// <summary>
/// The Session endpoints, against a Host that is actually running. AC1, AC3, AC4, AC5.
/// </summary>
/// <remarks>
/// <para>
/// <b>A real process, not a <c>WebApplicationFactory</c>.</b> There is no
/// <c>Microsoft.AspNetCore.Mvc.Testing</c> in this solution and no central pin for one - and the
/// process buys what in-memory hosting does not: <c>Program.cs</c> genuinely runs, so deleting the
/// <c>UseAuthentication</c> call or the CORS registration fails these tests rather than leaving
/// them green against a pipeline the test assembled itself. Story 1.1 found that shape of hole the
/// hard way.
/// </para>
/// <para>
/// <b>One Host process per test rather than one for the class.</b> Each takes a couple of seconds,
/// and the alternative is tests that can see each other's log output - which is the evidence half
/// of two of these assertions - and that share a data-protection key ring, which would make the
/// expired-Session case's separate configuration meaningless.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-2")]
[Trait("Requirement", "AR-14")]
[Trait("Requirement", "AD-7")]
[Trait("Requirement", "AR-28")]
[Trait("Requirement", "AD-23")]
[Trait("Requirement", "AR-34")]
public sealed class SessionEndpointTests(MigratedDatabaseFixture fixture)
    : IClassFixture<MigratedDatabaseFixture>
{
    private const string Password = "a-password-nobody-else-uses-1!";
    private const string WrongPassword = "a-different-password-entirely-2?";
    private const string ClientOrigin = "https://client.yello.test";
    private const string OtherOrigin = "https://attacker.example.test";
    private const string SessionCookie = "yello_session";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Configuration every Host in this class starts with: an allowed origin to be exactly.
    /// </summary>
    private static readonly Dictionary<string, string> Configured = new(StringComparer.Ordinal)
    {
        ["ClientOrigin"] = ClientOrigin,
    };

    /// <summary>
    /// The same, plus a lifetime that has already elapsed - so every ticket issued is born expired.
    /// </summary>
    private static readonly Dictionary<string, string> AlreadyExpired = new(StringComparer.Ordinal)
    {
        ["ClientOrigin"] = ClientOrigin,
        ["Session__Lifetime"] = "-00:00:01",
    };

    /// <summary>
    /// AC4's fourth clause, measured where AC4 actually states it: between the two RESPONSES.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The standing duration test measures <c>IdentityPasswordHasher.Verify</c>, which is not
    /// what AC4 says.</b> AC4 requires "the two responses are identical in status, body, shape and
    /// duration", and <c>PasswordVerificationDurationTests</c> compares the hasher in isolation - so
    /// the component that genuinely differs between the two arms is the one thing it cannot see. An
    /// unknown address returns <c>null</c> from <c>FindByEmailAddressAsync</c>; a wrong password
    /// materialises a projected row first. That difference is a datastore round trip, and until this
    /// test it was never measured. Story 1.3 shipped a container-backed
    /// <c>RegistrationDurationTests</c> beside its pure one for exactly this reason; story 1.4
    /// shipped no container-backed duration class at all, and its only end-to-end figures came from
    /// a harness that was deleted. Added at code review on 2026-09-19.
    /// </para>
    /// <para>
    /// <b>It lives in this class deliberately, rather than in a new one.</b> A fresh
    /// container-backed class would start a tenth SQL Server engine on a machine the deferred-work
    /// ledger already records running nine concurrently against 16.6 GB, with the operating system
    /// killing a background process for memory during this story's own test run. This class already
    /// holds the fixture, so the measurement costs no additional engine. Story 1.9 owns the topology
    /// decision that would make that constraint go away.
    /// </para>
    /// <para>
    /// <b>What it cannot prove.</b> Both arms here verify against a hash at the CURRENT work factor,
    /// so this says nothing about the legacy-factor asymmetry -
    /// <c>PasswordVerificationDurationTests.The_work_factor_has_not_been_raised_without_closing_the_timing_oracle</c>
    /// guards that, and the skipped test beside it demonstrates it.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task The_two_failure_causes_take_the_same_time_over_the_wire()
    {
        var connectionString = Available();
        var known = RandomAddress();

        DurationVerdict? verdict = null;

        await HostProcess.RunAsync(connectionString, Configured, async client =>
        {
            var session = new WireSession(client);

            await RegisterAsync(session, known);

            // A wrong password for an address that EXISTS, against an address that does not. Both
            // answer the same 401; the question is whether they take the same time to do it.
            verdict = await DurationIndistinguishability.CompareAsync(
                async () => await session.SignInAsync(known, WrongPassword),
                async () => await session.SignInAsync(RandomAddress(), WrongPassword));
        });

        Assert.NotNull(verdict);
        Assert.True(
            verdict.AreIndistinguishable,
            "An unknown address and a wrong password produce responses that differ in DURATION, so "
            + "AC4's fourth clause does not hold on the wire however identical the bodies are. The "
            + "read is the likely cause: one arm materialises a row and the other returns null, "
            + $"and only an end-to-end measurement can see it. {verdict.Describe()}");
    }

    /// <summary>
    /// AC1: valid credentials issue a Session, and the endpoint says nothing else at all.
    /// </summary>
    [Fact]
    public async Task Valid_credentials_answer_204_and_issue_a_session()
    {
        var connectionString = Available();
        var address = RandomAddress();

        WireResponse? signIn = null;
        WireResponse? current = null;
        string? setCookie = null;

        await HostProcess.RunAsync(connectionString, Configured, async client =>
        {
            var session = new WireSession(client);

            await RegisterAsync(session, address);

            signIn = await session.SignInAsync(address, Password);
            setCookie = session.SetCookieFor("yello_session");

            current = await session.CurrentSessionAsync();
        });

        Assert.NotNull(signIn);
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);
        Assert.Empty(signIn.Body);

        // The Session was issued, and it is the cookie that carried it: the request below sent no
        // credential of any other kind.
        Assert.NotNull(setCookie);

        Assert.NotNull(current);
        Assert.Equal(HttpStatusCode.NoContent, current.StatusCode);
    }

    /// <summary>
    /// AC5: the cookie is <c>HttpOnly; Secure; SameSite=None</c> - read off the header rather than
    /// inferred.
    /// </summary>
    /// <remarks>
    /// <b>The three are one decision, not three.</b> <c>SameSite=None</c> is required because the
    /// client and the API are distinct origins, and every current browser rejects
    /// <c>SameSite=None</c> without <c>Secure</c> - so a cookie missing either is not a cookie a
    /// browser will keep. Asserted on the wire because that is where a browser reads them: a policy
    /// object set correctly and never applied would pass an options-only assertion.
    /// </remarks>
    [Fact]
    public async Task The_session_cookie_is_http_only_secure_and_same_site_none()
    {
        var connectionString = Available();
        var address = RandomAddress();

        string? setCookie = null;

        await HostProcess.RunAsync(connectionString, Configured, async client =>
        {
            var session = new WireSession(client);

            await RegisterAsync(session, address);
            await session.SignInAsync(address, Password);

            setCookie = session.SetCookieFor("yello_session");
        });

        Assert.NotNull(setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=none", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC4: the two failure causes are identical in status, body, shape and headers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two responses are compared to <i>each other</i> rather than each against an expectation,
    /// which is what "identical in shape" actually requires - an assertion that both are 401 would
    /// still pass if one grew a <c>WWW-Authenticate</c>, an <c>errors</c> array or a different
    /// content type.
    /// </para>
    /// <para>
    /// Duration is the fourth clause and is measured separately, by
    /// <c>PasswordVerificationDurationTests</c>, which needs no container and 21 interleaved
    /// samples per arm.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task An_unknown_address_answers_exactly_as_a_wrong_password_does()
    {
        var connectionString = Available();
        var known = RandomAddress();

        WireResponse? unknownAddress = null;
        WireResponse? wrongPassword = null;

        await HostProcess.RunAsync(connectionString, Configured, async client =>
        {
            var session = new WireSession(client);

            await RegisterAsync(session, known);

            unknownAddress = await session.SignInAsync(RandomAddress(), Password);
            wrongPassword = await session.SignInAsync(known, WrongPassword);
        });

        Assert.NotNull(unknownAddress);
        Assert.NotNull(wrongPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, unknownAddress.StatusCode);
        Assert.Equal(unknownAddress.StatusCode, wrongPassword.StatusCode);
        Assert.Equal(unknownAddress.Body, wrongPassword.Body);
        Assert.Equal(unknownAddress.ContentType, wrongPassword.ContentType);

        // Header NAMES, not values. What would matter is one path carrying a Set-Cookie, a
        // WWW-Authenticate or a Retry-After the other does not.
        Assert.Equal(unknownAddress.HeaderNames, wrongPassword.HeaderNames);

        // NEITHER PATH ISSUES A COOKIE. Worth asserting separately from the header-name comparison
        // above, which would be satisfied by both of them issuing one.
        Assert.Null(unknownAddress.Header("Set-Cookie"));
    }

    /// <summary>
    /// AC4: the failure body carries one stable type and nothing that could name a cause.
    /// </summary>
    [Fact]
    public async Task The_failure_body_carries_one_type_and_no_errors()
    {
        var connectionString = Available();

        WireResponse? failure = null;

        await HostProcess.RunAsync(connectionString, Configured, async client =>
        {
            var session = new WireSession(client);

            await session.FetchAntiforgeryTokenAsync();

            failure = await session.SignInAsync(RandomAddress(), Password);
        });

        Assert.NotNull(failure);
        Assert.Equal("application/problem+json", failure.ContentType?.Split(';')[0]);

        var problem = JsonSerializer.Deserialize<ProblemResponse>(failure.Body, JsonOptions);

        Assert.NotNull(problem);
        Assert.Equal(ProblemTypes.AuthenticationFailed, problem.Type);

        // No codes. An `errors` array is where a cause would eventually be written, and the client
        // renders one string keyed on the type precisely because there is nothing else to key on.
        Assert.Null(problem.Errors);

        // And the prose that IS there says nothing either. The title is not the contract (AR-34),
        // but it travels on the wire, so it is held to the same vocabulary the log is.
        var disclosures = new[] { "no account", "not found", "unknown", "exists", "wrong password", "incorrect password" };

        Assert.DoesNotContain(
            disclosures,
            word => failure.Body.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// AC3: a request with no Session grants nothing, and is refused at the authentication boundary
    /// with a 401.
    /// </summary>
    /// <remarks>
    /// <b>A 401, and specifically not a 302 and not a 404.</b> The cookie handler's default
    /// challenge is a redirect to <c>/Account/Login</c>, which for an API means a caller receives a
    /// redirect to a page that does not exist; AD-3 puts the 403/404 line at the Space boundary "and
    /// nowhere else", so a 404 here would be the wrong answer from the other direction. Asserted as
    /// an exact status rather than "not success", because both wrong answers are non-success.
    /// </remarks>
    [Fact]
    public async Task A_request_with_no_session_is_refused_with_401()
    {
        var connectionString = Available();

        WireResponse? response = null;

        await HostProcess.RunAsync(connectionString, Configured, async client =>
        {
            var session = new WireSession(client);

            response = await session.CurrentSessionAsync();
        });

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Header("Location"));
    }

    /// <summary>
    /// AC3: a Session that has expired grants access to nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A real expiry, evaluated by the framework's own handler, with nothing sleeping.</b> The
    /// Host is started with a negative lifetime, so the ticket it issues is already past its
    /// expiry when it is written - and the cookie handler reads that instant out of the encrypted
    /// payload on the very next request. <c>Task.Delay</c> as a synchronisation mechanism is banned
    /// outright by this repository's conventions and would have been the obvious way to write this.
    /// </para>
    /// <para>
    /// <b>The sign-in still answers 204 and still sets a cookie</b>, which is worth asserting
    /// rather than assuming: it proves the refusal below is the <i>expiry</i> being evaluated
    /// rather than the sign-in having quietly failed, which would make this test pass for the wrong
    /// reason.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task An_expired_session_grants_nothing()
    {
        var connectionString = Available();
        var address = RandomAddress();

        WireResponse? signIn = null;
        WireResponse? current = null;
        string? setCookie = null;

        await HostProcess.RunAsync(connectionString, AlreadyExpired, async client =>
        {
            var session = new WireSession(client);

            await RegisterAsync(session, address);

            signIn = await session.SignInAsync(address, Password);
            setCookie = session.SetCookieFor("yello_session");

            current = await session.CurrentSessionAsync();
        });

        Assert.NotNull(signIn);
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);
        Assert.NotNull(setCookie);

        Assert.NotNull(current);
        Assert.Equal(HttpStatusCode.Unauthorized, current.StatusCode);
    }

    /// <summary>
    /// AC3: a Session cookie that was not issued by this Host grants nothing either.
    /// </summary>
    /// <remarks>
    /// The ticket is authenticated encryption, so an edited one fails to decrypt rather than
    /// decrypting to something else - but "the framework does that" is exactly the kind of claim
    /// this repository asserts rather than repeats. It also covers the shape a caller actually
    /// tries: a plausible-looking value in a cookie of the right name.
    /// </remarks>
    [Fact]
    public async Task A_forged_session_cookie_grants_nothing()
    {
        var connectionString = Available();

        WireResponse? response = null;

        await HostProcess.RunAsync(connectionString, Configured, async client =>
        {
            var session = new WireSession(client);

            response = await session.CurrentSessionAsync(
                sessionCookie: "yello_session=CfDJ8NotARealTicketAtAll_0123456789");
        });

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// R8, P0: a state-changing request without an anti-forgery token is refused. AR-14.
    /// </summary>
    /// <remarks>
    /// <b>Both halves are asserted, and the second is what makes the first mean something.</b> A
    /// test that only checked the refusal would pass against an endpoint that refused everything -
    /// including a correctly-tokened request - so the same credentials are sent again WITH the token
    /// and must succeed. That pairing is the planted-signal rule applied to a negative test.
    /// </remarks>
    [Fact]
    public async Task A_state_changing_request_without_an_antiforgery_token_is_refused()
    {
        var connectionString = Available();
        var address = RandomAddress();

        WireResponse? without = null;
        WireResponse? with = null;

        await HostProcess.RunAsync(connectionString, Configured, async client =>
        {
            var session = new WireSession(client);

            await RegisterAsync(session, address);

            without = await session.SignInAsync(address, Password, withAntiforgeryToken: false);
            with = await session.SignInAsync(address, Password);
        });

        Assert.NotNull(without);
        Assert.Equal(HttpStatusCode.BadRequest, without.StatusCode);

        var problem = JsonSerializer.Deserialize<ProblemResponse>(without.Body, JsonOptions);

        Assert.NotNull(problem);
        Assert.Equal(ProblemTypes.AntiforgeryTokenMissing, problem.Type);

        // The refusal is about the missing token and nothing else: the same submission with one
        // succeeds.
        Assert.NotNull(with);
        Assert.Equal(HttpStatusCode.NoContent, with.StatusCode);
    }

    /// <summary>
    /// AR-14: registration is a state-changing request too, and takes a token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Question 5, decided yes. An exception list is where a rule like this rots; the cost is one
    /// header. Asserted because it changes a shipped endpoint's contract, so a later story removing
    /// it should have to say so.
    /// </para>
    /// <para>
    /// <b>It asserted only the status code until code review on 2026-09-19, which is the exact
    /// defect Debug Log #4 records fixing on its sign-in sibling.</b> A malformed registration is
    /// also a 400, and so is a framework binding failure - so this stayed green with the
    /// anti-forgery requirement removed from <c>POST /api/v1/accounts</c> entirely, refusing the
    /// request for some other reason. The weakest assertion in the file was the one carrying the
    /// most contested decision. It now asserts the problem <c>type</c> and pairs the refusal with a
    /// correctly-tokened registration that must succeed, matching
    /// <see cref="A_state_changing_request_without_an_antiforgery_token_is_refused"/>.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Registration_requires_an_antiforgery_token_as_well()
    {
        var connectionString = Available();
        var address = RandomAddress();

        HttpStatusCode? withoutStatus = null;
        var withoutBody = string.Empty;
        WireResponse? with = null;

        await HostProcess.RunAsync(connectionString, Configured, async client =>
        {
            var response = await client.PostAsJsonAsync(
                AccountRoutes.Register,
                new RegisterAccountRequest("Ravi", address, Password),
                JsonOptions);

            withoutStatus = response.StatusCode;
            withoutBody = await response.Content.ReadAsStringAsync();

            // The same address, now with a token. Registration answers 204 on both the new-address
            // and the already-registered paths, so this cannot be confused by ordering.
            //
            // FetchAntiforgeryTokenAsync first, because WireSession sends the header only when a
            // token is already held - it does not fetch one on demand. Without it this half returns
            // the same 400 as the half above and the pairing proves nothing.
            var session = new WireSession(client);

            await session.FetchAntiforgeryTokenAsync();

            with = await session.RegisterAsync(
                new RegisterAccountRequest("Ravi", address, Password));
        });

        Assert.Equal(HttpStatusCode.BadRequest, withoutStatus);

        var problem = JsonSerializer.Deserialize<ProblemResponse>(withoutBody, JsonOptions);

        Assert.NotNull(problem);
        Assert.Equal(ProblemTypes.AntiforgeryTokenMissing, problem.Type);

        // The refusal is about the missing token and nothing else.
        Assert.NotNull(with);
        Assert.Equal(HttpStatusCode.NoContent, with.StatusCode);
    }

    /// <summary>
    /// R8, P0: a wildcard origin is never returned, and neither is a reflected one. AC5, AD-7.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Three claims, and the third is the one a mistake actually produces.</b> The configured
    /// origin is allowed with credentials; a wildcard is never sent; and an arbitrary origin is not
    /// echoed back. The reflected form is the trap: <c>AllowAnyOrigin</c> with
    /// <c>AllowCredentials</c> throws at runtime, and the exception message pushes an implementer
    /// straight onto <c>SetIsOriginAllowed(_ =&gt; true)</c>, which allows every origin while
    /// naming one - so it looks correct in exactly this header.
    /// </para>
    /// <para>
    /// <b>It is also the CSRF trust list.</b> .NET 10's automatic anti-request-forgery middleware
    /// reads this same policy, so an origin allowed here is an origin trusted there.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Cors_allows_exactly_the_configured_origin_and_never_a_wildcard_or_a_reflection()
    {
        var connectionString = Available();

        WireResponse? allowed = null;
        WireResponse? refused = null;

        await HostProcess.RunAsync(connectionString, Configured, async client =>
        {
            var session = new WireSession(client);

            allowed = await session.PreflightSignInAsync(ClientOrigin);
            refused = await session.PreflightSignInAsync(OtherOrigin);
        });

        Assert.NotNull(allowed);
        Assert.Equal(ClientOrigin, allowed.Header("Access-Control-Allow-Origin"));
        Assert.Equal("true", allowed.Header("Access-Control-Allow-Credentials"));

        Assert.NotNull(refused);

        // Not the wildcard, not the caller's own origin, not anything.
        Assert.Null(refused.Header("Access-Control-Allow-Origin"));
        Assert.Null(refused.Header("Access-Control-Allow-Credentials"));

        // And the allowed one is a named origin rather than a wildcard that happens to work.
        Assert.NotEqual("*", allowed.Header("Access-Control-Allow-Origin"));
    }

    /// <summary>
    /// A response to a request claiming an unknown origin carries no CORS grant, so a browser can
    /// never read it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What this asserts is narrower than it first looks, and the narrowing is the finding.</b>
    /// It was written as "a sign-in claiming an unknown origin establishes no Session" and it
    /// failed: the response set a Session cookie. That is correct behaviour rather than a defect,
    /// and the reasoning is worth keeping because it is easy to get backwards.
    /// </para>
    /// <para>
    /// The caller here is not a browser. It holds the anti-forgery cookie and the matching request
    /// token - it fetched them - so it is not a cross-site forgery by any definition: forgery means
    /// causing <i>somebody else's</i> browser to make a request with <i>their</i> credentials.
    /// .NET 10's automatic CSRF middleware would deny a genuinely cross-site <c>Origin</c>, but
    /// explicit token validation takes precedence over it and replaces its verdict, which is the
    /// documented order and the one this Host relies on for its JSON endpoints.
    /// </para>
    /// <para>
    /// <b>What CORS actually buys, and what is therefore asserted:</b> no browser at that origin
    /// can read this response or the cookie it sets, because the response carries no
    /// <c>Access-Control-Allow-Origin</c> for it. A raw HTTP client can always send any request it
    /// likes with any header it likes; that is not what AC5 is about, and an assertion implying
    /// otherwise would claim a guarantee no origin policy has ever provided.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task A_response_to_an_unknown_origin_carries_no_cors_grant()
    {
        var connectionString = Available();
        var address = RandomAddress();

        WireResponse? response = null;

        await HostProcess.RunAsync(connectionString, Configured, async client =>
        {
            var session = new WireSession(client);

            await RegisterAsync(session, address);

            response = await session.SignInAsync(address, Password, origin: OtherOrigin);
        });

        Assert.NotNull(response);
        Assert.Null(response.Header("Access-Control-Allow-Origin"));
        Assert.Null(response.Header("Access-Control-Allow-Credentials"));
    }

    /// <summary>
    /// AD-23 in the log: no password, no address, and nothing that tells the three outcomes apart.
    /// AR-34: never the cookie.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The vocabulary half is the one worth having.</b> The uniform 401 is undone by a log line
    /// saying "no account for that address" as surely as by a response body, and logs travel
    /// further than responses do. Story 1.3 met this from an unexpected direction on 2026-09-12,
    /// when EF Core's command log named every duplicate registration by the shape of the statements
    /// it did not run.
    /// </para>
    /// <para>
    /// Scoped to what the REQUESTS produced rather than the whole log: the Host's startup output
    /// legitimately contains some of this vocabulary, and scanning it was a false positive when
    /// story 1.3 first wrote the equivalent.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task No_log_line_carries_the_password_the_cookie_or_the_outcome()
    {
        var connectionString = Available();
        var address = RandomAddress();
        var setCookie = string.Empty;

        var log = await HostProcess.RunAsync(connectionString, Configured, async client =>
        {
            var session = new WireSession(client);

            await RegisterAsync(session, address);

            // All three outcomes, so the log has every chance to say which was which.
            await session.SignInAsync(address, Password);
            setCookie = session.SetCookieFor("yello_session") ?? string.Empty;

            // A FRESH TOKEN, BECAUSE THE LAST ONE WAS ISSUED TO AN ANONYMOUS CALLER. ASP.NET Core
            // binds an anti-forgery token to the identity it was minted for, so the token that
            // carried the sign-in above is refused now that this caller holds a Session. Without
            // this line the next two requests are turned away with a 400 before they reach the
            // endpoint - which is how the client's own missing refresh was found, because this
            // assertion then counted one processed line instead of three.
            await session.FetchAntiforgeryTokenAsync();

            await session.SignInAsync(address, WrongPassword);
            await session.SignInAsync(RandomAddress(), Password);
        });

        Assert.DoesNotContain(Password, log, StringComparison.Ordinal);
        Assert.DoesNotContain(WrongPassword, log, StringComparison.Ordinal);
        Assert.DoesNotContain(address, log, StringComparison.Ordinal);

        // AR-34: the cookie is never logged. Asserted against the value actually issued rather than
        // against the cookie's name, which would match the configuration line that declares it.
        Assert.False(string.IsNullOrEmpty(setCookie), "No Session cookie was issued, so this asserts nothing.");
        Assert.DoesNotContain(setCookie.Split(';')[0].Split('=')[1], log, StringComparison.Ordinal);

        var listeningAt = log.LastIndexOf(HostProcess.ListeningMarker, StringComparison.Ordinal);

        Assert.True(
            listeningAt >= 0,
            "The Host never reported an address, so there is no startup boundary to scan after and "
            + "no requests were served. The assertions below would pass vacuously.");

        var afterStartup = log[listeningAt..];

        // THREE sign-ins, ONE message, THREE times. If any outcome logged anything another did
        // not, these counts would differ.
        Assert.Equal(3, CountOccurrences(afterStartup, "Sign-in request processed."));
        Assert.Equal(0, CountOccurrences(afterStartup, "Sign-in request rejected"));

        var disclosures = new[]
        {
            "no account", "not found", "notfound", "unknown address", "no such",
            "authenticated", "succeeded", "failed", "invalid password", "wrong",
        };

        var leaked = (
            from line in afterStartup.Split(Environment.NewLine)
            from word in disclosures
            where line.Contains(word, StringComparison.OrdinalIgnoreCase)
            select $"'{word}' in: {line.Trim()}")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            leaked.Count == 0,
            "The Host logged sign-in vocabulary that can tell the outcomes apart, and AD-23 is "
            + "undone by a log line as surely as by a response body. If a later story needs one of "
            + "these words in an unrelated message, narrow the list rather than deleting it."
            + $"{Environment.NewLine}{Environment.NewLine}"
            + string.Join(Environment.NewLine, leaked.Select(l => $"  - {l}")));
    }

    /// <summary>
    /// AC5: no response on this path carries the password.
    /// </summary>
    [Fact]
    public async Task No_response_body_carries_the_password()
    {
        var connectionString = Available();
        var address = RandomAddress();

        var bodies = new List<string>();

        await HostProcess.RunAsync(connectionString, Configured, async client =>
        {
            var session = new WireSession(client);

            await RegisterAsync(session, address);

            bodies.Add((await session.SignInAsync(address, Password)).Body);
            bodies.Add((await session.SignInAsync(address, WrongPassword)).Body);

            // The rejection path too: an error body is where a value most often escapes, because it
            // is the one place a framework is tempted to echo what it was given.
            bodies.Add((await session.SignInAsync(string.Empty, Password)).Body);
        });

        Assert.Equal(3, bodies.Count);
        Assert.DoesNotContain(bodies, body => body.Contains(Password, StringComparison.Ordinal));
        Assert.DoesNotContain(bodies, body => body.Contains(WrongPassword, StringComparison.Ordinal));
    }

    /// <summary>
    /// A malformed submission is a 400 with codes, and is not the 401 a failed authentication is.
    /// AR-34.
    /// </summary>
    /// <remarks>
    /// The distinction is compatible with AD-23 for the reason story 1.3 recorded for registration:
    /// every rule in the validator reads only the submitted values, so no outcome of it can vary
    /// with what is stored. A submission that reaches this branch was never an authentication
    /// attempt.
    /// </remarks>
    [Fact]
    public async Task A_malformed_submission_answers_with_a_stable_problem_type_and_codes()
    {
        var connectionString = Available();

        WireResponse? response = null;

        await HostProcess.RunAsync(connectionString, Configured, async client =>
        {
            var session = new WireSession(client);

            await session.FetchAntiforgeryTokenAsync();

            response = await session.SignInAsync(string.Empty, string.Empty);
        });

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.ContentType?.Split(';')[0]);

        var problem = JsonSerializer.Deserialize<ProblemResponse>(response.Body, JsonOptions);

        Assert.NotNull(problem);
        Assert.Equal(ProblemTypes.InvalidSignIn, problem.Type);
        Assert.Equal(["email-address-required", "password-required"], problem.Errors);
    }

    /// <summary>
    /// Story 1.6 Task 9 - the two PBKDF2 endpoints are rate-limited, and the 429 is uniform: a known
    /// and an unknown address are refused alike, as <c>application/problem+json</c> with a stable
    /// type and a <c>Retry-After</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The limit is configured low for this case only.</b> <c>HostProcess</c> raises it out of the
    /// way for every other wire test, which sign in far more than ten times from one address; this
    /// case sets <c>RateLimiting__PermitsPerWindow</c> back down to reach it in a few requests.
    /// </para>
    /// <para>
    /// <b>The partition may never include the submitted address</b> (AD-23). So the limit is reached
    /// with one address and then asserted with another: a limiter partitioned on the address would
    /// serve the second one, and fail here. Anchored on a first sign-in answered 401, so a limiter
    /// refusing everything cannot pass.
    /// </para>
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "AD-18")]
    public async Task Sign_in_past_the_limit_is_refused_with_429_alike_for_a_known_and_an_unknown_address()
    {
        var connectionString = Available();
        var known = RandomAddress();

        var limited = new Dictionary<string, string>(Configured, StringComparer.Ordinal)
        {
            ["RateLimiting__PermitsPerWindow"] = "3",
        };

        WireResponse? first = null;
        WireResponse? knownRefused = null;
        WireResponse? unknownRefused = null;

        await HostProcess.RunAsync(connectionString, limited, async client =>
        {
            var session = new WireSession(client);

            await RegisterAsync(session, known);

            first = await session.SignInAsync(known, WrongPassword);

            // Bounded: a limiter that never refuses fails on the assertion below, not by hanging.
            for (var attempt = 0; attempt < 10 && knownRefused?.StatusCode != HttpStatusCode.TooManyRequests; attempt++)
            {
                knownRefused = await session.SignInAsync(known, WrongPassword);
            }

            unknownRefused = await session.SignInAsync(RandomAddress(), WrongPassword);
        });

        Assert.NotNull(first);
        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);

        Assert.NotNull(knownRefused);
        Assert.NotNull(unknownRefused);

        Assert.Equal(HttpStatusCode.TooManyRequests, knownRefused.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, unknownRefused.StatusCode);

        Assert.Equal(knownRefused.Body, unknownRefused.Body);
        Assert.Equal(knownRefused.HeaderNames, unknownRefused.HeaderNames);
        Assert.StartsWith("application/problem+json", knownRefused.ContentType, StringComparison.Ordinal);
        Assert.NotNull(knownRefused.Header("Retry-After"));
        Assert.Equal(
            "urn:yello:problem:rate-limited",
            JsonSerializer.Deserialize<ProblemResponse>(knownRefused.Body, JsonOptions)?.Type);
    }

    /// <summary>
    /// Story 1.6 Task 9 - registration, the other PBKDF2 endpoint, is rate-limited too, and its 429
    /// is the same for a new address and one already registered.
    /// </summary>
    /// <remarks>
    /// Only registrations are sent, so the limit reached is registration's own. Taking the policy off
    /// <c>MapRegisterAccount</c> fails this case. Anchored on a first registration answered 204, so a
    /// limiter refusing everything cannot pass. The already-registered address is refused with the
    /// same body as a new one, so the limiter is no oracle for which addresses exist (AD-23).
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "AD-23")]
    public async Task Registration_past_the_limit_is_refused_with_429_alike_for_a_new_and_a_registered_address()
    {
        var connectionString = Available();
        var registered = RandomAddress();

        var limited = new Dictionary<string, string>(Configured, StringComparer.Ordinal)
        {
            ["RateLimiting__PermitsPerWindow"] = "3",
        };

        WireResponse? first = null;
        WireResponse? newRefused = null;
        WireResponse? registeredRefused = null;

        await HostProcess.RunAsync(connectionString, limited, async client =>
        {
            var session = new WireSession(client);
            await session.FetchAntiforgeryTokenAsync();

            first = await session.RegisterAsync(new RegisterAccountRequest("Ravi", registered, Password));

            // Bounded: a limiter that never refuses fails on the assertion below, not by hanging.
            for (var attempt = 0; attempt < 10 && newRefused?.StatusCode != HttpStatusCode.TooManyRequests; attempt++)
            {
                newRefused = await session.RegisterAsync(new RegisterAccountRequest("Ravi", RandomAddress(), Password));
            }

            registeredRefused = await session.RegisterAsync(new RegisterAccountRequest("Ravi", registered, Password));
        });

        Assert.NotNull(first);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        Assert.NotNull(newRefused);
        Assert.NotNull(registeredRefused);

        Assert.Equal(HttpStatusCode.TooManyRequests, newRefused.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, registeredRefused.StatusCode);

        Assert.Equal(newRefused.Body, registeredRefused.Body);
        Assert.Equal(newRefused.HeaderNames, registeredRefused.HeaderNames);
        Assert.NotNull(newRefused.Header("Retry-After"));
        Assert.Equal(
            "urn:yello:problem:rate-limited",
            JsonSerializer.Deserialize<ProblemResponse>(newRefused.Body, JsonOptions)?.Type);
    }

    // ---------------------------------------------------------------------------------------------
    // ---------------------------------------------------------------------------------------------
    // Story 1.7 - sign-out (open question 4: DELETE /api/v1/sessions, 204, anti-forgery required,
    // idempotent). In this class rather than a new one: a container-backed class is one more SQL
    // Server engine on a full run.
    //
    // SIGN-OUT ENDS THE SESSION ON THE SERVER. FR-2 / CAP-2 / story 1.4 AC3: "a Session that has
    // expired or been invalidated grants access to nothing". Lee's decision (a) of 2026-10-24: sign-out
    // stamps the Account's SessionsValidAfter, and every authenticated request compares it with the
    // ticket's issue instant. So a replayed pre-sign-out cookie is refused (401), and a Session issued
    // after sign-out is served. Sign-out ends the Account's Sessions on every device - an accepted
    // consequence of the same decision.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// AC11 / Q4 - signing out answers <c>204</c>, expires the Session cookie, and the next request
    /// carrying what the browser now holds is refused - and so is a copy of the cookie taken before
    /// sign-out, replayed (FR-2: an invalidated Session grants access to nothing).
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-2")]
    [Trait("Requirement", "UX-DR31")]
    public async Task Sign_out_ends_the_Session_and_expires_its_cookie()
    {
        var connectionString = Available();
        var address = RandomAddress();

        WireResponse? before = null;
        WireResponse? signOut = null;
        WireResponse? after = null;
        WireResponse? replayed = null;
        string? expiry = null;

        await HostProcess.RunAsync(connectionString, Configured, async client =>
        {
            var session = new WireSession(client);

            await RegisterAsync(session, address);
            Assert.Equal(HttpStatusCode.NoContent, (await session.SignInAsync(address, Password)).StatusCode);

            before = await session.CurrentSessionAsync();

            // A copy of the Session cookie as it stood before sign-out - what an attacker who lifted
            // it, or a second browser holding the same Session, would replay.
            var copied = session.Cookies[SessionCookie];

            // A token minted while anonymous is refused once a Session exists
            // (TESTING-CONVENTIONS.md:227-233), so it is re-fetched after sign-in.
            await session.FetchAntiforgeryTokenAsync();

            signOut = await session.DeleteAsync(SessionRoutes.Authenticate);
            expiry = session.SetCookieFor(SessionCookie);

            after = await session.CurrentSessionAsync();
            replayed = await session.CurrentSessionAsync($"{SessionCookie}={copied}");
        });

        Assert.NotNull(before);
        Assert.Equal(HttpStatusCode.NoContent, before.StatusCode);

        Assert.NotNull(signOut);
        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);

        // The cookie is cleared with a past expiry and the same attributes it was set with.
        Assert.NotNull(expiry);
        Assert.Contains("expires=Thu, 01 Jan 1970", expiry, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", expiry, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=none", expiry, StringComparison.OrdinalIgnoreCase);

        Assert.NotNull(after);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);

        // The server ended the Session, not only the browser's copy of it (Lee's decision (a)).
        Assert.NotNull(replayed);
        Assert.Equal(HttpStatusCode.Unauthorized, replayed.StatusCode);
    }

    /// <summary>
    /// AC11 / FR-2 - signing out ends the Sessions issued before it, and only those: a fresh sign-in
    /// after sign-out is served, on a Session-only route and on the Space switcher alike, while the
    /// pre-sign-out cookie replayed beside it is still refused.
    /// </summary>
    /// <remarks>
    /// The other half of <see cref="Sign_out_ends_the_Session_and_expires_its_cookie"/>: a revocation
    /// that refused every later Session too would pass that case and lock the Account out. Both
    /// cookies are presented on the same Host in the same minute, so the comparison is the ticket's
    /// issue instant against the Account's <c>SessionsValidAfter</c> and nothing coarser.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "FR-2")]
    [Trait("Requirement", "AD-27")]
    public async Task A_Session_issued_after_sign_out_is_served_and_the_one_before_is_not()
    {
        var connectionString = Available();
        var address = RandomAddress();

        WireResponse? fresh = null;
        WireResponse? freshSwitcher = null;
        WireResponse? replayed = null;
        WireResponse? replayedSwitcher = null;

        await HostProcess.RunAsync(connectionString, Configured, async client =>
        {
            var session = new WireSession(client);

            await RegisterAsync(session, address);
            Assert.Equal(HttpStatusCode.NoContent, (await session.SignInAsync(address, Password)).StatusCode);

            var copied = session.Cookies[SessionCookie];

            await session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await session.DeleteAsync(SessionRoutes.Authenticate)).StatusCode);

            // Anonymous again, so the token minted for the signed-in identity is refused; re-fetched.
            await session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await session.SignInAsync(address, Password)).StatusCode);

            fresh = await session.CurrentSessionAsync();
            freshSwitcher = await session.GetAsync(SpaceRoutes.Directory);

            replayed = await session.CurrentSessionAsync($"{SessionCookie}={copied}");

            var replay = new WireSession(client);
            replay.HoldCookie(SessionCookie, copied);
            replayedSwitcher = await replay.GetAsync(SpaceRoutes.Directory);
        });

        Assert.NotNull(fresh);
        Assert.Equal(HttpStatusCode.NoContent, fresh.StatusCode);

        Assert.NotNull(freshSwitcher);
        Assert.Equal(HttpStatusCode.OK, freshSwitcher.StatusCode);

        Assert.NotNull(replayed);
        Assert.Equal(HttpStatusCode.Unauthorized, replayed.StatusCode);

        Assert.NotNull(replayedSwitcher);
        Assert.Equal(HttpStatusCode.Unauthorized, replayedSwitcher.StatusCode);
    }

    /// <summary>
    /// AC11 / AR-14 - sign-out is state-changing, so without an anti-forgery token it is refused and
    /// the Session survives; the same request with the token then succeeds.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-14")]
    public async Task Sign_out_without_an_antiforgery_token_is_refused_and_the_Session_survives()
    {
        var connectionString = Available();
        var address = RandomAddress();

        WireResponse? without = null;
        WireResponse? stillSignedIn = null;
        WireResponse? with = null;

        await HostProcess.RunAsync(connectionString, Configured, async client =>
        {
            var session = new WireSession(client);

            await RegisterAsync(session, address);
            Assert.Equal(HttpStatusCode.NoContent, (await session.SignInAsync(address, Password)).StatusCode);
            await session.FetchAntiforgeryTokenAsync();

            without = await session.DeleteAsync(SessionRoutes.Authenticate, withAntiforgeryToken: false);
            stillSignedIn = await session.CurrentSessionAsync();
            with = await session.DeleteAsync(SessionRoutes.Authenticate);
        });

        Assert.NotNull(without);
        Assert.Equal(HttpStatusCode.BadRequest, without.StatusCode);

        Assert.NotNull(stillSignedIn);
        Assert.Equal(HttpStatusCode.NoContent, stillSignedIn.StatusCode);

        // The anchor that makes the refusal mean something: the tokened request is accepted.
        Assert.NotNull(with);
        Assert.Equal(HttpStatusCode.NoContent, with.StatusCode);
    }

    /// <summary>
    /// Q4 - sign-out is idempotent: a caller holding no Session is answered <c>204</c>, not
    /// <c>401</c>, so a double press is harmless.
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "AR-14")]
    public async Task Sign_out_without_a_Session_answers_204()
    {
        var connectionString = Available();

        WireResponse? noSession = null;
        WireResponse? current = null;

        await HostProcess.RunAsync(connectionString, Configured, async client =>
        {
            var session = new WireSession(client);

            await session.FetchAntiforgeryTokenAsync();

            current = await session.CurrentSessionAsync();
            noSession = await session.DeleteAsync(SessionRoutes.Authenticate);
        });

        Assert.NotNull(current);
        Assert.Equal(HttpStatusCode.Unauthorized, current.StatusCode);

        Assert.NotNull(noSession);
        Assert.Equal(HttpStatusCode.NoContent, noSession.StatusCode);
    }

    private string Available()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        return fixture.ConnectionString!;
    }

    private static string RandomAddress() => $"person-{Guid.NewGuid():N}@example.test";

    /// <summary>
    /// Registers an Account through the real endpoint, so every sign-in below is against a
    /// credential the product itself created.
    /// </summary>
    /// <remarks>
    /// Through the endpoint rather than by writing a row, deliberately: a hash planted directly is
    /// a hash this test made, and what these cases are about is whether the two halves of the
    /// product agree. <c>RehashIntegrationTests</c> plants rows, because its subject is a hash made
    /// at a work factor registration cannot produce.
    /// </remarks>
    private static async Task RegisterAsync(WireSession session, string address)
    {
        await session.FetchAntiforgeryTokenAsync();

        var response = await session.RegisterAsync(
            new RegisterAccountRequest("Ravi-" + Guid.NewGuid().ToString("N"), address, Password));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
