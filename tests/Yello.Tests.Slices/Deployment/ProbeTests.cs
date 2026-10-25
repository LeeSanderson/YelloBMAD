using System.Net;
using Xunit;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Deployment;

/// <summary>
/// The liveness and readiness probes answer from process state: with the database unreachable they
/// still answer <c>200</c>, and answering them opens no connection. Story 1.10 AC6 (AD-10 / AR-33);
/// Task 4, Q15 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as a red-phase scaffold by the ATDD run on 2026-10-25.</b> The probes do not exist yet.
/// </para>
/// <para>
/// <b>No database at all</b>, so no engine: the Host is given a server nothing listens on. The anchor
/// proves the database really was unreachable - a sign-in, which must read it, does not succeed.
/// </para>
/// <para>
/// <b>Depends on the purge trigger moving (Task 5, Q13 (a)).</b> Today the refusal purge is offered
/// on every request, so the first probe would itself try the database. The log-slice assertion below
/// catches that, which is the point: a probe that wakes the database is a defect whichever component
/// opened the connection.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-33")]
[Trait("Requirement", "AD-10")]
public sealed class ProbeTests
{
    private static readonly string[] ConnectionAttemptMarkers =
    [
        "Microsoft.EntityFrameworkCore",
        "SqlException",
        "Microsoft.Data.SqlClient",
        "RefusalPurge",
    ];

    /// <summary>
    /// 1.10-SLC-004 - both probes answer <c>200</c> with the database unreachable, anonymously, with a
    /// body that says nothing about the database, and the Host's log for them shows no connection
    /// attempt.
    /// </summary>
    [Fact(Skip = "red: AC6 - /health/live and /health/ready are not mapped yet (Task 4, Q15 (a))")]
    public async Task Both_probes_answer_from_process_state_with_the_database_unreachable()
    {
        var settings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ClientOrigin"] = DeploymentHosts.ClientOrigin,
        };

        await using var host = await RunningHost.StartAsync(DeploymentHosts.UnreachableDatabase, settings);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));

        var cursor = await host.LogCursorAsync();

        using var live = await host.Client.GetAsync(new Uri("/health/live", UriKind.Relative), deadline.Token);
        using var ready = await host.Client.GetAsync(new Uri("/health/ready", UriKind.Relative), deadline.Token);

        var probeSlice = await host.LogSinceAsync(cursor);

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

        foreach (var body in new[] { await live.Content.ReadAsStringAsync(deadline.Token), await ready.Content.ReadAsStringAsync(deadline.Token) })
        {
            Assert.DoesNotContain("database", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("sql", body, StringComparison.OrdinalIgnoreCase);
        }

        Assert.All(ConnectionAttemptMarkers, marker => Assert.DoesNotContain(marker, probeSlice, StringComparison.Ordinal));

        // Anchor: the database really is unreachable - a request that must read it does not succeed.
        var session = new WireSession(host.Client);
        await session.FetchAntiforgeryTokenAsync();
        var signIn = await session.SignInAsync(DeploymentHosts.RandomAddress(), DeploymentHosts.Password);

        Assert.True(signIn.StatusCode >= HttpStatusCode.InternalServerError, $"A sign-in against an unreachable database answered {signIn.StatusCode}.");
    }
}
