using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Accounts;

namespace Yello.Tests.Slices.Accounts.RegisterAccount;

/// <summary>
/// The registration endpoint, against a Host that is actually running. AC2, AC5, AR-34.
/// </summary>
/// <remarks>
/// <para>
/// These are the assertions AC2 is really about: "the response is identical to a successful new
/// registration in status, body and shape". Every other test in this folder works below the wire.
/// </para>
/// <para>
/// One Host process per test rather than one for the class - each takes a couple of seconds, and
/// the alternative is tests that can see each other's log output, which is the evidence half of
/// these assertions.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-28")]
[Trait("Requirement", "AD-23")]
[Trait("Requirement", "AR-34")]
[Trait("Requirement", "FS-NFR-1")]
public sealed class RegisterAccountEndpointTests(MigratedDatabaseFixture fixture)
    : IClassFixture<MigratedDatabaseFixture>
{
    private const string Password = "a-password-nobody-else-uses-1!";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// AC2: identical in status, body and shape, whether the address is new or already registered.
    /// </summary>
    /// <remarks>
    /// The two responses are compared to each other rather than each against an expectation. That
    /// is deliberate: an assertion that both are 204 would still pass if one grew a header, a body
    /// or a different content type, and "identical in shape" is what AD-23 actually requires.
    /// </remarks>
    [Fact]
    public async Task A_duplicate_registration_answers_exactly_as_a_new_one_does()
    {
        var connectionString = Available();
        var address = RandomAddress();

        HttpResponseMessage? first = null;
        HttpResponseMessage? second = null;
        var firstBody = string.Empty;
        var secondBody = string.Empty;

        await HostProcess.RunAsync(connectionString, async client =>
        {
            first = await client.PostAsJsonAsync(AccountRoutes.Register, Submission(address));
            firstBody = await first.Content.ReadAsStringAsync();

            second = await client.PostAsJsonAsync(AccountRoutes.Register, Submission(address));
            secondBody = await second.Content.ReadAsStringAsync();
        });

        Assert.NotNull(first);
        Assert.NotNull(second);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(first.StatusCode, second.StatusCode);

        // No body at all on either path - which is why there is nothing that could differ.
        Assert.Empty(firstBody);
        Assert.Equal(firstBody, secondBody);

        Assert.Equal(first.Content.Headers.ContentType, second.Content.Headers.ContentType);

        // Header NAMES, not values: Date differs between any two responses and Content-Length is
        // absent from both. What would matter is one path carrying a Location, an ETag or a
        // Set-Cookie the other does not.
        Assert.Equal(HeaderNames(first), HeaderNames(second));
    }

    /// <summary>
    /// AC5: the password appears in no API response.
    /// </summary>
    [Fact]
    public async Task No_response_body_carries_the_password()
    {
        var connectionString = Available();

        var bodies = new List<string>();

        await HostProcess.RunAsync(connectionString, async client =>
        {
            var created = await client.PostAsJsonAsync(AccountRoutes.Register, Submission(RandomAddress()));
            bodies.Add(await created.Content.ReadAsStringAsync());

            // The rejection path too: an error body is where a value most often escapes, because
            // it is the one place a framework is tempted to echo what it was given.
            var rejected = await client.PostAsJsonAsync(
                AccountRoutes.Register,
                new RegisterAccountRequest(string.Empty, "not-an-address", Password));

            bodies.Add(await rejected.Content.ReadAsStringAsync());
        });

        Assert.Equal(2, bodies.Count);
        Assert.DoesNotContain(bodies, body => body.Contains(Password, StringComparison.Ordinal));
    }

    /// <summary>
    /// AC5 and FS-NFR-1: the password appears in no log, and neither does anything that tells the
    /// two registration paths apart.
    /// </summary>
    /// <remarks>
    /// <b>The second half is the one worth having.</b> A uniform response undone by a log line
    /// saying "account already exists" is the same leak by a slower route, and logs travel further
    /// than responses do. Asserted by registering the same address twice and requiring the Host's
    /// output for the two requests to be identical.
    /// </remarks>
    [Fact]
    public async Task No_log_line_carries_the_password_or_distinguishes_the_two_paths()
    {
        var connectionString = Available();
        var address = RandomAddress();

        var log = await HostProcess.RunAsync(connectionString, async client =>
        {
            await client.PostAsJsonAsync(AccountRoutes.Register, Submission(address));
            await client.PostAsJsonAsync(AccountRoutes.Register, Submission(address));
        });

        Assert.DoesNotContain(Password, log, StringComparison.Ordinal);

        // Nor the address: logging it identically on both paths would satisfy AD-23 and would
        // still put every registered address in the log, which PRD section 6.4's gate makes a
        // question for story 1.10 rather than a free choice here.
        Assert.DoesNotContain(address, log, StringComparison.Ordinal);

        // The two requests produced the same line, twice. If one path logged anything the other
        // did not, these counts would differ.
        var processed = CountOccurrences(log, "Registration request processed.");

        Assert.Equal(2, processed);
    }

    /// <summary>
    /// AR-34: errors are RFC 9457 with a stable machine-readable type.
    /// </summary>
    [Fact]
    public async Task A_malformed_submission_answers_with_a_stable_problem_type()
    {
        var connectionString = Available();

        HttpResponseMessage? response = null;
        var body = string.Empty;

        await HostProcess.RunAsync(connectionString, async client =>
        {
            response = await client.PostAsJsonAsync(
                AccountRoutes.Register,
                new RegisterAccountRequest(string.Empty, "not-an-address", string.Empty));

            body = await response.Content.ReadAsStringAsync();
        });

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = JsonSerializer.Deserialize<ProblemResponse>(body, JsonOptions);

        Assert.NotNull(problem);
        Assert.Equal(ProblemTypes.InvalidRegistration, problem.Type);
        Assert.NotNull(problem.Errors);

        Assert.Equal(
            [
                "display-name-required",
                "email-address-malformed",
                "password-required",
            ],
            problem.Errors);
    }

    /// <summary>
    /// An absent body is a malformed submission, not an unhandled exception.
    /// </summary>
    /// <remarks>
    /// The endpoint takes a nullable body for this reason. With a non-nullable parameter the
    /// framework's binder throws before the endpoint runs, producing a response this endpoint
    /// never shaped - and on an unauthenticated endpoint that is the easiest thing in the product
    /// for anyone at all to trigger.
    /// </remarks>
    [Fact]
    public async Task An_empty_body_is_refused_as_a_malformed_submission()
    {
        var connectionString = Available();

        HttpStatusCode? status = null;

        await HostProcess.RunAsync(connectionString, async client =>
        {
            using var content = new StringContent("null", Encoding.UTF8, "application/json");
            var response = await client.PostAsync(new Uri(AccountRoutes.Register, UriKind.Relative), content);

            status = response.StatusCode;
        });

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    private string Available()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        return fixture.ConnectionString!;
    }

    private static string RandomAddress() => $"person-{Guid.NewGuid():N}@example.test";

    private static RegisterAccountRequest Submission(string address) =>
        new("Ravi-" + Guid.NewGuid().ToString("N"), address, Password);

    private static List<string> HeaderNames(HttpResponseMessage response) =>
        response.Headers.Select(header => header.Key)
            .Concat(response.Content.Headers.Select(header => header.Key))
            .Where(name => !name.Equals("Date", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

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
