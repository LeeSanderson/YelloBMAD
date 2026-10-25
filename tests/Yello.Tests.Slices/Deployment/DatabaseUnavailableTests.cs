using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts.Accounts;
using Yello.Contracts.Spaces;
using Yello.Domain.Accounts;
using Yello.Host.Sessions;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Deployment;

/// <summary>
/// A paused, resuming or unreachable database is an honest refusal, not a <c>500</c>; requests that
/// touch no database do not start the refusal purge; and a sign-in after a sign-out is admitted even if
/// the clock stepped back. Story 1.10 AC14 (and AC6, AC12); Task 5, Q11-Q13;
/// <c>deferred-work.md:314, 334, 336</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>What answers.</b> <c>DatabaseAvailability</c> maps a database failure anywhere in the pipeline to
/// one <c>503</c>; the refusal purge runs only after an authenticated request; and sign-in issues at
/// <c>SessionRevocation.IssueInstant</c>, the later of now and the last sign-out.
/// </para>
/// <para>
/// <b>The names these cases read:</b> the problem type <c>urn:yello:problem:service-unavailable</c>
/// (<c>ProblemTypes.ServiceUnavailable</c>);
/// the purge-claim log marker <see cref="PurgeClaimed"/>; and
/// <c>SessionRevocation.IssueInstant(DateTimeOffset now, DateTimeOffset? validAfter)</c>, reached by
/// name.
/// </para>
/// <para>
/// <b>Which path the <c>503</c> case reaches.</b> Before F1 is fixed, two local Host processes share the
/// developer-profile key ring, so the cookie decrypts and the revocation read is what fails. After F1
/// the ring is in the database, and the unreachable Host fails while loading it instead - Task 5's
/// "interaction with Q7 (a)". Both must answer the same <c>503</c>. If the revocation-read mapping can
/// no longer be reached over the wire once F1 lands, add the in-process case Task 5 names (a store
/// that throws) and record it.
/// </para>
/// </remarks>
/// <param name="fixture">This class's migrated database on the shared engine.</param>
/// <param name="engine">The process's one SQL Server, for a database this class takes offline.</param>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-3")]
[Trait("Requirement", "AR-33")]
[Trait("Requirement", "AD-10")]
public sealed class DatabaseUnavailableTests(MigratedDatabaseFixture fixture, SqlServerEngine engine) : IClassFixture<MigratedDatabaseFixture>
{
    private const string ServiceUnavailableType = "urn:yello:problem:service-unavailable";

    /// <summary>
    /// The text the Host logs, synchronously and on the request that claims it, when it claims the day's
    /// refusal purge. Proposed: Task 5 asks for "a purge counter or log line".
    /// </summary>
    private const string PurgeClaimed = "RefusalPurgeClaimed";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 1.10-SLC-012 - a validly encrypted Session cookie against an unreachable database is answered
    /// with a uniform <c>503 application/problem+json</c> carrying <c>Retry-After</c>, byte-identical on
    /// two routes, and admits nothing. Anchor: with the database up, the same cookie is <c>204</c>.
    /// </summary>
    [Fact]
    public async Task A_session_against_an_unreachable_database_is_a_uniform_503()
    {
        var connectionString = Available();
        IReadOnlyDictionary<string, string>? cookies = null;

        await HostProcess.RunAsync(connectionString, new Dictionary<string, string>(StringComparer.Ordinal) { ["ClientOrigin"] = DeploymentHosts.ClientOrigin }, async client =>
        {
            var session = new WireSession(client);
            await DeploymentHosts.RegisterAndSignInAsync(session, DeploymentHosts.RandomAddress());

            // Anchor: the cookie is good while the database is up.
            Assert.Equal(HttpStatusCode.NoContent, (await session.CurrentSessionAsync()).StatusCode);
            cookies = new Dictionary<string, string>(session.Cookies, StringComparer.Ordinal);
        });

        Assert.NotNull(cookies);

        WireResponse? current = null;
        WireResponse? spaces = null;

        await HostProcess.RunAsync(DeploymentHosts.UnreachableDatabase, new Dictionary<string, string>(StringComparer.Ordinal) { ["ClientOrigin"] = DeploymentHosts.ClientOrigin }, async client =>
        {
            var session = new WireSession(client);

            foreach (var (name, value) in cookies)
            {
                session.HoldCookie(name, value);
            }

            current = await session.CurrentSessionAsync();
            spaces = await session.GetAsync(SpaceRoutes.Directory);
        });

        AssertUniformServiceUnavailable(current, spaces);
    }

    /// <summary>
    /// 1.10-SLC-013 - a Host with a fresh profile against an unreachable database, presented a cookie
    /// another process issued, answers <c>503</c>: loading the key ring failed, and it must not mint a
    /// fresh key in memory and refuse the cookie as <c>401</c>, which would void every outstanding
    /// Session for the life of the process.
    /// </summary>
    [Fact]
    public async Task A_cold_start_that_cannot_load_the_key_ring_is_a_503_not_a_sign_out()
    {
        var connectionString = Available();
        IReadOnlyDictionary<string, string>? cookies = null;

        await HostProcess.RunAsync(connectionString, DeploymentHosts.InAFreshContainer(DeploymentHosts.NewProfile()), async client =>
        {
            DeploymentHosts.ForwardHttps(client);

            var session = new WireSession(client);
            await DeploymentHosts.RegisterAndSignInAsync(session, DeploymentHosts.RandomAddress());
            cookies = new Dictionary<string, string>(session.Cookies, StringComparer.Ordinal);
        });

        Assert.NotNull(cookies);

        WireResponse? current = null;
        WireResponse? spaces = null;

        await HostProcess.RunAsync(DeploymentHosts.UnreachableDatabase, DeploymentHosts.InAFreshContainer(DeploymentHosts.NewProfile()), async client =>
        {
            DeploymentHosts.ForwardHttps(client);

            var session = new WireSession(client);

            foreach (var (name, value) in cookies)
            {
                session.HoldCookie(name, value);
            }

            current = await session.CurrentSessionAsync();
            spaces = await session.GetAsync(SpaceRoutes.Directory);
        });

        AssertUniformServiceUnavailable(current, spaces);
    }

    /// <summary>
    /// 1.10-SLC-016 - the revocation read itself failing: a Host that has already loaded its key ring
    /// loses its database mid-process, and a Session it issued is answered with the same uniform
    /// <c>503</c>, never the unhandled <c>500</c> of <c>deferred-work.md:336</c>. Anchor: the same cookie
    /// was <c>204</c> moments before.
    /// </summary>
    /// <remarks>
    /// <b>Why a database that goes away mid-process.</b> The key ring lives in the database (F1), so the
    /// cases above fail while loading the ring and never reach the revocation read - which is the path
    /// this case exists for (Task 5, "Interaction with Q7 (a)"). Taking a database of the class's own
    /// offline reaches it over the wire.
    /// </remarks>
    [Fact]
    public async Task A_revocation_read_against_a_database_that_went_away_is_the_same_503()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = await engine.CreateDatabaseAsync(cancellationToken);
        var name = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

        try
        {
            await new RegistrationDatabase(connectionString).MigrateAsync(cancellationToken);

            await using var host = await RunningHost.StartAsync(connectionString, new Dictionary<string, string>(StringComparer.Ordinal) { ["ClientOrigin"] = DeploymentHosts.ClientOrigin });

            var session = new WireSession(host.Client);
            await DeploymentHosts.RegisterAndSignInAsync(session, DeploymentHosts.RandomAddress());

            Assert.Equal(HttpStatusCode.NoContent, (await session.CurrentSessionAsync()).StatusCode);

            await SetOnlineAsync(name, online: false);

            var current = await session.CurrentSessionAsync();
            var spaces = await session.GetAsync(SpaceRoutes.Directory);

            AssertUniformServiceUnavailable(current, spaces);
        }
        finally
        {
            await SetOnlineAsync(name, online: true);
            await engine.DropDatabaseAsync(connectionString);
        }
    }

    /// <summary>
    /// 1.10-SLC-014 - with the purge due, requests that touch no database (the anti-forgery token, the
    /// unmatched <c>/api</c> fallback, an unauthenticated <c>401</c>) claim no purge; a signed-in request
    /// claims exactly one, and the aged refusal record is deleted.
    /// </summary>
    /// <remarks>
    /// <b>Seed data, checked against the schema.</b> One <c>AccessRefusal</c> row aged 100 days (the
    /// retention is 90, <c>AccessRefusalPurge.Retention</c>). <c>AccessRefusal</c> has no foreign key and
    /// no row-level security predicate (<c>20261024121107_SpaceBoundaryRefusals.cs:18-23, 39-51</c>):
    /// every column is non-nullable and supplied, <c>Capability</c> is the stored name of <c>Capability.ReadProjectsAndTasks</c> within <c>nvarchar(64)</c>, <c>Outcome</c> is 404 as <c>AccessRefusalRecorder</c> writes for a cross-Space refusal,
    /// <c>Kind</c> fits <c>nvarchar(32)</c> and is the stored name of <c>AccessRefusalKind.CrossSpace</c>.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AD-20")]
    public async Task Only_a_request_that_already_reached_the_database_starts_the_refusal_purge()
    {
        var connectionString = Available();
        var aged = await SeedAgedRefusalAsync(connectionString);

        await using var host = await RunningHost.StartAsync(connectionString, new Dictionary<string, string>(StringComparer.Ordinal) { ["ClientOrigin"] = DeploymentHosts.ClientOrigin });

        var start = await host.LogCursorAsync();

        var cancellationToken = TestContext.Current.CancellationToken;

        using (await host.Client.GetAsync(new Uri(SessionRoutes.AntiforgeryToken, UriKind.Relative), cancellationToken))
        using (await host.Client.GetAsync(new Uri("/api/v1/nothing-" + Guid.NewGuid().ToString("N"), UriKind.Relative), cancellationToken))
        using (var unauthenticated = await host.Client.GetAsync(new Uri(SessionRoutes.Current, UriKind.Relative), cancellationToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        }

        var anonymous = await host.LogSinceAsync(start);

        Assert.DoesNotContain(PurgeClaimed, anonymous, StringComparison.Ordinal);
        Assert.True(await RefusalExistsAsync(connectionString, aged), "The aged refusal was purged by an anonymous request.");

        var actor = await ProductActor.RegisterAsync(host, new WireDatabase(connectionString), "purge");
        Assert.Equal(HttpStatusCode.OK, (await actor.Session.GetAsync(SpaceRoutes.Directory)).StatusCode);

        var whole = await host.LogSinceAsync(start);

        Assert.Equal(1, Occurrences(whole, PurgeClaimed));

        // The purge runs off the request path; bounded, so a purge that never runs fails here.
        var deadline = TimeProvider.System.GetTimestamp();
        while (await RefusalExistsAsync(connectionString, aged))
        {
            Assert.True(TimeProvider.System.GetElapsedTime(deadline) < TimeSpan.FromSeconds(20), "The claimed purge never deleted the aged refusal.");
            await Task.Yield();
        }
    }

    /// <summary>
    /// 1.10-UNIT-004 - a sign-in after a sign-out, with the host clock stepped back past
    /// <c>SessionsValidAfter</c>, is issued at <c>SessionsValidAfter</c> and admitted; the pre-sign-out
    /// ticket is still refused.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-2")]
    public void A_sign_in_after_a_clock_step_back_is_issued_at_the_sign_out_instant_and_admitted()
    {
        var issueInstant = typeof(SessionRevocation).GetMethod(
            "IssueInstant",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            [typeof(DateTimeOffset), typeof(DateTimeOffset?)]);

        Assert.True(issueInstant is not null, "SessionRevocation.IssueInstant(DateTimeOffset now, DateTimeOffset? validAfter) does not exist (story 1.10 Task 5).");

        var signedOut = new DateTimeOffset(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);
        var steppedBack = signedOut.AddMinutes(-10);
        var standing = new SessionStanding(signedOut);

        var issued = Assert.IsType<DateTimeOffset>(issueInstant.Invoke(null, [steppedBack, signedOut]));

        Assert.Equal(signedOut, issued);
        Assert.True(standing.Admits(issued));

        // Anchor: a ticket from before the sign-out is still refused, and a clock that did not step
        // back is used as it is.
        Assert.False(standing.Admits(signedOut.AddMinutes(-1)));
        Assert.Equal(signedOut.AddMinutes(5), Assert.IsType<DateTimeOffset>(issueInstant.Invoke(null, [signedOut.AddMinutes(5), signedOut])));
    }

    private static void AssertUniformServiceUnavailable(WireResponse? first, WireResponse? second)
    {
        Assert.NotNull(first);
        Assert.NotNull(second);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, first.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, second.StatusCode);
        Assert.StartsWith("application/problem+json", first.ContentType, StringComparison.Ordinal);
        Assert.NotNull(first.Header("Retry-After"));

        Assert.Equal(first.Body, second.Body);
        Assert.Equal(first.HeaderNames, second.HeaderNames);
        Assert.Equal(ServiceUnavailableType, JsonSerializer.Deserialize<JsonElement>(first.Body, JsonOptions).GetProperty("type").GetString());
    }

    private async Task SetOnlineAsync(string database, bool online)
    {
        var server = await engine.ServerConnectionStringAsync(TestContext.Current.CancellationToken);

        await using var connection = new SqlConnection(server);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Review SQL queries - the name is the engine's own yello_{guid:N}; no input reaches it.
        command.CommandText = online
            ? $"ALTER DATABASE [{database}] SET ONLINE"
            : $"ALTER DATABASE [{database}] SET OFFLINE WITH ROLLBACK IMMEDIATE";
#pragma warning restore CA2100

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Guid> SeedAgedRefusalAsync(string connectionString)
    {
        var id = Guid.NewGuid();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO dbo.AccessRefusal (Id, AccountId, TargetSpaceId, Capability, Outcome, Kind, OccurredAt) " +
            "VALUES (@id, @account, @space, N'ReadProjectsAndTasks', 404, N'CrossSpace', DATEADD(day, -100, SYSDATETIMEOFFSET()))";
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@account", Guid.NewGuid());
        command.Parameters.AddWithValue("@space", Guid.NewGuid());

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        return id;
    }

    private static async Task<bool> RefusalExistsAsync(string connectionString, Guid id)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM dbo.AccessRefusal WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture) == 1;
    }

    private static int Occurrences(string text, string value)
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

    private string Available()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        return fixture.ConnectionString!;
    }
}
