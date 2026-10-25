using System.Globalization;
using System.Net;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts.Accounts;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Deployment;

/// <summary>
/// A Session survives a new revision and a cold start: a Session cookie and an anti-forgery pair
/// issued by one Host process are readable by the next, and the Session is honoured or refused by
/// <c>SessionsValidAfter</c> alone. Story 1.10 AC12; Task 4, Q7 (a), Finding F1.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the ATDD run on 2026-10-25.</b> Nothing persists the key ring
/// yet, so a container start regenerates it and every Session ends.
/// </para>
/// <para>
/// <b>Each Host process gets an empty user profile of its own</b>
/// (<see cref="DeploymentHosts.InAFreshContainer"/>). Without that, two local Host processes share the
/// developer's profile key ring and F1 cannot be seen. Plant (story Task 13): remove the persistence;
/// this class must fail. If it does not, the profile isolation is wrong.
/// </para>
/// <para>
/// <b>The anchor is a Host on another database</b>, also with a fresh profile, refusing the same
/// cookie: the ring travels with the database and nowhere else. That database is a second one of this
/// class's own on the shared engine, migrated, and dropped at the end.
/// </para>
/// <para>
/// <b>Proposed names, to correct on activation:</b> the key table <c>dbo.DataProtectionKeys</c> is the
/// default of <c>Microsoft.AspNetCore.DataProtection.EntityFrameworkCore</c>'s
/// <c>IDataProtectionKeyContext</c> (Q7 (a)).
/// </para>
/// </remarks>
/// <param name="fixture">This class's migrated database on the shared engine.</param>
/// <param name="engine">The process's one SQL Server, for the anchor's second database.</param>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-2")]
[Trait("Requirement", "AR-14")]
public sealed class DataProtectionPersistenceTests(MigratedDatabaseFixture fixture, SqlServerEngine engine)
    : IClassFixture<MigratedDatabaseFixture>
{
    /// <summary>
    /// 1.10-SLC-010 - a Session cookie issued by one process is honoured by the next on the same
    /// database, each with a fresh profile; the key ring is a row in the database. Anchor: a Host on a
    /// different database refuses it.
    /// </summary>
    [Fact(Skip = "red: AC12 - the Data Protection key ring is not persisted, so a cold start ends every Session (Task 4, Q7 (a), F1)")]
    public async Task A_session_issued_before_a_cold_start_is_honoured_after_it()
    {
        var connectionString = Available();
        var issued = await IssueSessionAsync(connectionString);

        Assert.True(await KeyRowsAsync(connectionString) > 0, "No key was persisted to dbo.DataProtectionKeys.");

        WireResponse? afterRestart = null;

        await HostProcess.RunAsync(connectionString, DeploymentHosts.InAFreshContainer(DeploymentHosts.NewProfile()), async client =>
        {
            DeploymentHosts.ForwardHttps(client);
            afterRestart = await Replay(client, issued).CurrentSessionAsync();
        });

        Assert.NotNull(afterRestart);
        Assert.Equal(HttpStatusCode.NoContent, afterRestart.StatusCode);

        // Anchor: the same cookie means nothing to a Host whose database holds another ring.
        var elsewhere = await engine.CreateDatabaseAsync(TestContext.Current.CancellationToken);

        try
        {
            await new RegistrationDatabase(elsewhere).MigrateAsync(TestContext.Current.CancellationToken);

            WireResponse? onAnotherDatabase = null;

            await HostProcess.RunAsync(elsewhere, DeploymentHosts.InAFreshContainer(DeploymentHosts.NewProfile()), async client =>
            {
                DeploymentHosts.ForwardHttps(client);
                onAnotherDatabase = await Replay(client, issued).CurrentSessionAsync();
            });

            Assert.NotNull(onAnotherDatabase);
            Assert.Equal(HttpStatusCode.Unauthorized, onAnotherDatabase.StatusCode);
        }
        finally
        {
            await engine.DropDatabaseAsync(elsewhere);
        }
    }

    /// <summary>
    /// 1.10-SLC-011 - after a cold start the previous process's anti-forgery pair is still accepted on
    /// a state change (sign-out), and once signed out the replayed Session cookie is refused:
    /// <c>SessionsValidAfter</c> still rules across processes.
    /// </summary>
    [Fact(Skip = "red: AC12 - the Data Protection key ring is not persisted, so the previous process's anti-forgery pair is unreadable (Task 4, F1)")]
    public async Task After_a_cold_start_the_old_antiforgery_pair_still_signs_out_and_sign_out_still_ends_the_session()
    {
        var connectionString = Available();
        var issued = await IssueSessionAsync(connectionString);

        HttpStatusCode? signOut = null;
        WireResponse? replayed = null;

        await HostProcess.RunAsync(connectionString, DeploymentHosts.InAFreshContainer(DeploymentHosts.NewProfile()), async client =>
        {
            DeploymentHosts.ForwardHttps(client);

            using var request = new HttpRequestMessage(HttpMethod.Delete, new Uri(SessionRoutes.Authenticate, UriKind.Relative));
            request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", issued.Cookies.Select(pair => $"{pair.Key}={pair.Value}")));
            request.Headers.TryAddWithoutValidation(Antiforgery.HeaderName, issued.RequestToken);

            using var response = await client.SendAsync(request);
            signOut = response.StatusCode;

            replayed = await Replay(client, issued).CurrentSessionAsync();
        });

        Assert.Equal(HttpStatusCode.NoContent, signOut);
        Assert.NotNull(replayed);
        Assert.Equal(HttpStatusCode.Unauthorized, replayed.StatusCode);
    }

    /// <summary>
    /// Signs an Account in on a Host process with a fresh profile, proves the Session works there, and
    /// stops the process - returning every cookie it set and the anti-forgery request token.
    /// </summary>
    private static async Task<IssuedSession> IssueSessionAsync(string connectionString)
    {
        IssuedSession? issued = null;

        await HostProcess.RunAsync(connectionString, DeploymentHosts.InAFreshContainer(DeploymentHosts.NewProfile()), async client =>
        {
            DeploymentHosts.ForwardHttps(client);

            var session = new WireSession(client);
            await DeploymentHosts.RegisterAndSignInAsync(session, DeploymentHosts.RandomAddress());

            Assert.Equal(HttpStatusCode.NoContent, (await session.CurrentSessionAsync()).StatusCode);
            Assert.True(session.Cookies.ContainsKey(DeploymentHosts.SessionCookie), "Sign-in set no Session cookie.");

            issued = new IssuedSession(
                new Dictionary<string, string>(session.Cookies, StringComparer.Ordinal),
                session.AntiforgeryRequestToken ?? throw new InvalidOperationException("No anti-forgery token was issued."));
        });

        return issued ?? throw new InvalidOperationException("The issuing Host never ran its work.");
    }

    private static WireSession Replay(HttpClient client, IssuedSession issued)
    {
        var session = new WireSession(client);

        foreach (var (name, value) in issued.Cookies)
        {
            session.HoldCookie(name, value);
        }

        return session;
    }

    private static async Task<int> KeyRowsAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM dbo.DataProtectionKeys";

        return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture);
    }

    private string Available()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        return fixture.ConnectionString!;
    }

    /// <summary>
    /// What a stopped Host process left a browser holding.
    /// </summary>
    private sealed record IssuedSession(IReadOnlyDictionary<string, string> Cookies, string RequestToken);
}
