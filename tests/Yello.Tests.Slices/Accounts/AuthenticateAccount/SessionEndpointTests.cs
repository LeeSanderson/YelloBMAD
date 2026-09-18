using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Accounts;
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
    /// Question 5, decided yes. An exception list is where a rule like this rots; the cost is one
    /// header. Asserted because it changes a shipped endpoint's contract, so a later story removing
    /// it should have to say so.
    /// </remarks>
    [Fact]
    public async Task Registration_requires_an_antiforgery_token_as_well()
    {
        var connectionString = Available();

        HttpStatusCode? without = null;

        await HostProcess.RunAsync(connectionString, Configured, async client =>
        {
            var response = await client.PostAsJsonAsync(
                AccountRoutes.Register,
                new RegisterAccountRequest("Ravi", RandomAddress(), Password),
                JsonOptions);

            without = response.StatusCode;
        });

        Assert.Equal(HttpStatusCode.BadRequest, without);
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
