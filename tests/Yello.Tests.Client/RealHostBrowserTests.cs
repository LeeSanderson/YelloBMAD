using Microsoft.Playwright;
using Xunit;

namespace Yello.Tests.Client;

/// <summary>
/// Story 1.9 AC11 - the real client in Chromium against a real Host and a real database: the real
/// <c>SameSite=None; Secure</c> cookie survives the two origins, the real preflight admits the
/// anti-forgery header, a deep link to the other Account's Space renders the refusal page without
/// requesting it, and a real <c>401</c> reaches the client's session-end path.
/// <c>deferred-work.md:157-163, 318</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Red-phase scaffolds, and deliberately not in a collection yet.</b> Task 9 builds
/// <c>RealHostCollection</c>: the client dev server first, then a Host with
/// <c>ClientOrigin</c> set to the client's origin, on a database on the suite's <b>one</b> shared
/// engine, with <c>wwwroot/appsettings.Development.json</c> answered by <c>page.RouteAsync</c> carrying
/// the Host's real address - the only fake in that collection. Until then this class takes no
/// fixture, so it starts no engine, no Host and no browser.
/// </para>
/// <para>
/// <b>Scaffold contract.</b> <see cref="IRealHostWorld"/> lists what the cases need; on activation,
/// implement it over Task 9's collection or replace it, and record which in the Debug Log. The Host's
/// log, not a fake's request list, is the witness that B's id was never requested.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "NFR-1")]
[Trait("Requirement", "AR-14")]
[Trait("Requirement", "UX-DR31")]
public sealed class RealHostBrowserTests
{
    private const float Timeout = 30_000;

    private const string RoleChip = "nav.context-bar .role-chip";
    private const string SpaceName = "nav.context-bar .context-bar__space-name";
    private const string Heading = "#app h1";

    /// <summary>
    /// 1.9-E2E-006 - signing in through <c>SignIn.razor</c> against the real Host lands on A's Space
    /// with the bar and the Role chip drawn: the real cookie crossed the two origins, and the real
    /// preflight admitted the anti-forgery header.
    /// </summary>
    [Fact(Skip = "red: AC11 - the real-Host collection does not exist yet (story 1.9 Task 9)")]
    [Trait("Requirement", "FR-2")]
    public async Task Signing_in_against_the_real_Host_lands_on_the_Space_with_the_bar_drawn()
    {
        await using var world = await RealHost.StartAsync();
        var page = await world.OpenSignedInAsync();

        await WaitForPathAsync(page, $"/spaces/{world.SpaceOfA:D}");
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

        Assert.Equal(world.SpaceNameOfA, (await page.Locator(SpaceName).TextContentAsync())?.Trim());
    }

    /// <summary>
    /// 1.9-E2E-007 / AC2 - a deep link to B's Space renders the refusal page, and the Host received
    /// no request naming B's Space. Anchored on the Host having logged A's own Space request.
    /// </summary>
    [Fact(Skip = "red: AC11 - the real-Host collection does not exist yet (story 1.9 Task 9)")]
    [Trait("Requirement", "UX-DR32")]
    public async Task A_deep_link_to_another_Accounts_Space_is_refused_without_reaching_the_Host()
    {
        await using var world = await RealHost.StartAsync();
        var page = await world.OpenSignedInAsync();
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

        var cursor = world.HostLogCursor();

        await page.GotoAsync(new Uri(world.ClientAddress, $"spaces/{world.SpaceOfB:D}").ToString());
        await page.Locator(Heading, new PageLocatorOptions { HasText = "Not available." })
            .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

        var log = await world.HostLogSinceAsync(cursor);

        Assert.Contains("/api/v1/spaces", log, StringComparison.Ordinal);
        Assert.DoesNotContain(world.SpaceOfB.ToString("D"), log, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 1.9-E2E-008 - A's Sessions ended on the server: A's next navigation gets a real <c>401</c> and
    /// lands on <c>/sign-in</c> with the reason stated, the Space-scoped state purged.
    /// </summary>
    [Fact(Skip = "red: AC11 - the real-Host collection does not exist yet (story 1.9 Task 9)")]
    [Trait("Requirement", "FR-2")]
    [Trait("Requirement", "NFR-2")]
    public async Task A_Session_ended_on_the_server_lands_the_client_on_sign_in_with_the_reason()
    {
        await using var world = await RealHost.StartAsync();
        var page = await world.OpenSignedInAsync();
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

        await world.EndSessionsOfAAsync();

        await page.GotoAsync(new Uri(world.ClientAddress, $"spaces/{world.SpaceOfA:D}").ToString());
        await WaitForPathAsync(page, "/sign-in");

        Assert.Contains("reason=", page.Url, StringComparison.Ordinal);
        Assert.Equal(0, await page.Locator(SpaceName).CountAsync());
        Assert.DoesNotContain(world.SpaceNameOfA, await page.Locator("#app").InnerTextAsync(), StringComparison.Ordinal);
    }

    private static Task WaitForPathAsync(IPage page, string path) =>
        page.WaitForFunctionAsync(
            "path => location.pathname === path",
            path,
            new PageWaitForFunctionOptions { Timeout = Timeout });

    /// <summary>
    /// The entry point of the scaffold contract. Throws until story 1.9 Task 9 builds the collection.
    /// </summary>
    private static class RealHost
    {
        public static Task<IRealHostWorld> StartAsync() =>
            throw new InvalidOperationException(
                "The real-Host collection does not exist yet (story 1.9 Task 9: client dev server, Host with ClientOrigin, "
                + "a database on the shared engine, two Accounts seeded through the real API).");
    }

    /// <summary>
    /// What the real-Host cases need: two Accounts seeded through the real API, A signed in through
    /// the real sign-in page, the Host's log for the case, and a way to end A's Sessions on the server
    /// (sign-out from a second context, or <c>SessionsValidAfter</c> stamped by SQL).
    /// </summary>
    private interface IRealHostWorld : IAsyncDisposable
    {
        Uri ClientAddress { get; }

        Guid SpaceOfA { get; }

        string SpaceNameOfA { get; }

        Guid SpaceOfB { get; }

        Task<IPage> OpenSignedInAsync();

        long HostLogCursor();

        Task<string> HostLogSinceAsync(long cursor);

        Task EndSessionsOfAAsync();
    }
}
