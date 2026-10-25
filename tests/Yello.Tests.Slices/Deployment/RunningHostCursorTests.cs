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
/// <b>The cursor counts tagged lines only.</b> Every request <c>RunningHost.Client</c> sends carries
/// <c>RunningHost.HarnessTag</c>; a wait counts request-finished lines that carry it, so untagged lines
/// from another client cannot satisfy it.
/// </para>
/// <para>
/// <b>Honest about the race.</b> Without the tag, whether the harness's own line reaches the pump
/// before the wait returns is a matter of microseconds, so SLC-015 alone can pass against a broken
/// cursor. SLC-017 is the deterministic case; SLC-015 stays as the shape the defect was reported in.
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
    [Fact]
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

    /// <summary>
    /// 1.10-SLC-017 - the deterministic half of SLC-015: the server-side duration of one harness
    /// request is read from the harness's own line, even when another client's requests finished in the
    /// same slice.
    /// </summary>
    /// <remarks>
    /// <b>Deterministic, where SLC-015 is a race</b> that rarely loses on a fast machine. Here the other
    /// client's requests
    /// have finished before the harness sends its one, so their lines are in the slice by the time it is
    /// read. Counting every request-finished line finds six where one was sent, and
    /// <see cref="RunningHost.ServerDurationOfAsync"/> refuses that.
    /// </remarks>
    [Fact]
    public async Task Another_clients_requests_are_not_read_as_the_harnesss_duration()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        await using var host = await RunningHost.StartAsync(fixture.ConnectionString!);

        using var handler = HostProcess.CreateClientHandler();
        using var browser = new HttpClient(handler) { BaseAddress = host.Address };

        var duration = await host.ServerDurationOfAsync(async () =>
        {
            for (var request = 0; request < 5; request++)
            {
                using var untagged = await browser.GetAsync(new Uri("/api/v1/browser-" + Guid.NewGuid().ToString("N"), UriKind.Relative), TestContext.Current.CancellationToken);
            }

            using var mine = await host.Client.GetAsync(new Uri("/api/v1/harness-" + Guid.NewGuid().ToString("N"), UriKind.Relative), TestContext.Current.CancellationToken);
        });

        Assert.True(duration >= 0, $"The harness's request reported a duration of {duration} ms.");
    }
}
