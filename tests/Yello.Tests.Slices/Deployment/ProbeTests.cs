using System.Net;
using Xunit;
using Yello.Contracts.Accounts;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Deployment;

/// <summary>
/// The liveness and readiness probes answer from process state: with the database unreachable they
/// still answer <c>200</c>, and answering them opens no connection. Story 1.10 AC6 (AD-10 / AR-33);
/// Task 4, Q15 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>The probes are mapped by <c>HealthProbes</c></b>, outside <c>/api</c>.
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
    [Fact]
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
        // Issuing an anti-forgery token reads the key ring from the database, so it is the simplest
        // request that must fail here.
        using var token = await host.Client.GetAsync(new Uri(SessionRoutes.AntiforgeryToken, UriKind.Relative), deadline.Token);

        Assert.True(token.StatusCode >= HttpStatusCode.InternalServerError, $"A request against an unreachable database answered {token.StatusCode}.");
    }
}
