using Xunit;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Deployment;

/// <summary>
/// <c>RunningHost</c>'s log cursor counts only the requests the harness itself sent, so another
/// client's requests - a browser's, in the real-Host collection - cannot satisfy a wait early. Story
/// 1.10 AC16; Task 9 (b); <c>deferred-work.md:400</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as a red-phase scaffold by the ATDD run on 2026-10-25.</b> Today the cursor waits until
/// the number of request-finished lines since it reaches the number of requests the harness sent
/// (<c>RunningHost.cs:303-365</c>), and a line is a line whoever caused it. So the five untagged lines
/// below satisfy the wait for the harness's one request, and the slice is returned before that
/// request's own line has necessarily arrived.
/// </para>
/// <para>
/// <b>Honest about the race.</b> Before the fix this fails on most runs, not on every run: whether the
/// harness's own line reaches the pump before the wait returns is a matter of microseconds. After the
/// fix (a tag the harness sets and the cursor counts) it passes on every run, which is what makes it a
/// gate. Confirm the failure on activation over several runs, and record the count.
/// </para>
/// </remarks>
/// <param name="fixture">This class's migrated database on the shared engine.</param>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "AR-35")]
public sealed class RunningHostCursorTests(MigratedDatabaseFixture fixture) : IClassFixture<MigratedDatabaseFixture>
{
    /// <summary>
    /// 1.10-SLC-015 - requests from another client do not satisfy the harness's wait; the slice holds
    /// the harness's own request.
    /// </summary>
    [Fact(Skip = "red: AC16 - the cursor counts every request-finished line, not the harness's own (Task 9 (b))")]
    public async Task Another_clients_requests_do_not_satisfy_the_harnesss_wait()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        await using var host = await RunningHost.StartAsync(fixture.ConnectionString!);

        using var handler = HostProcess.CreateClientHandler();
        using var browser = new HttpClient(handler) { BaseAddress = host.Address };

        var cursor = await host.LogCursorAsync();
        var mine = "/api/v1/harness-" + Guid.NewGuid().ToString("N");

        var harnessRequest = host.Client.GetAsync(new Uri(mine, UriKind.Relative), TestContext.Current.CancellationToken);

        for (var request = 0; request < 5; request++)
        {
            using var untagged = await browser.GetAsync(new Uri("/api/v1/browser-" + Guid.NewGuid().ToString("N"), UriKind.Relative), TestContext.Current.CancellationToken);
        }

        using (await harnessRequest)
        {
            var slice = await host.LogSinceAsync(cursor);

            Assert.Contains(mine, slice, StringComparison.Ordinal);
        }
    }
}
