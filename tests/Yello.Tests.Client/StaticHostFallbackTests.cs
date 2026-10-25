using Microsoft.Playwright;
using Xunit;

namespace Yello.Tests.Client;

/// <summary>
/// The client served the way Static Web Apps serves it - the published output, from a static host,
/// with <c>staticwebapp.config.json</c>'s <c>navigationFallback</c> - boots on a deep link loaded
/// directly. Story 1.10 AC13 (AD-7); Task 7.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as a red-phase scaffold by the ATDD run on 2026-10-25.</b> There is no
/// <c>staticwebapp.config.json</c> yet and no static host to serve the publish output.
/// </para>
/// <para>
/// <b>Not the dev server.</b> The dev server answers every client route with <c>index.html</c> on its
/// own, so a case run against it passes whatever the static host's configuration says. This case
/// needs the <i>published</i> output (<c>dotnet publish Yello.Client -c Release</c>) served by a static
/// file server on an OS port that implements exactly one rule - <c>navigationFallback</c>, read from
/// the published <c>staticwebapp.config.json</c> - so the fallback under test is the file's, not the
/// server's.
/// </para>
/// <para>
/// <b>Scaffold contract.</b> <see cref="PublishedClientHost.StartAsync"/> throws naming Task 7. On
/// activation, implement it (or replace it with a real type) and record the shape in the Debug Log. If
/// a static server proves too heavy, the story allows testing the file's rules as data instead
/// (<c>DeploymentShapeTests.The_static_host_falls_back_to_index_html_for_client_routes</c> does that
/// half already); record the deviation.
/// </para>
/// <para>
/// <b>No collection and no fixture</b>, so this class starts nothing while it is skipped. The browser
/// is launched in the case; on activation, move it into a collection if it shares one.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AD-7")]
[Trait("Requirement", "AR-38")]
public sealed class StaticHostFallbackTests
{
    private const int RenderTimeoutMilliseconds = 30_000;

    /// <summary>
    /// 1.10-E2E-001 - <c>/spaces/{id}</c> loaded directly from the static host reaches the router: the
    /// application boots and renders a routed page, and the framework's error banner stays hidden.
    /// </summary>
    [Fact(Skip = "red: AC13 - no staticwebapp.config.json and no static host for the published output (Task 7)")]
    public async Task A_deep_link_on_the_published_client_boots_and_routes()
    {
        await using var host = await PublishedClientHost.StartAsync();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        var response = await page.GotoAsync(new Uri(host.BaseAddress, $"/spaces/{Guid.NewGuid():D}").ToString());

        Assert.NotNull(response);
        Assert.Equal(200, response.Status);

        await page.Locator("#app h1").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = RenderTimeoutMilliseconds,
        });

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync(), "The client booted into the framework's error banner.");
    }

    /// <summary>
    /// The published client on a static host. A scaffold contract: see the class remarks.
    /// </summary>
    private interface IPublishedClientHost : IAsyncDisposable
    {
        Uri BaseAddress { get; }
    }

    /// <summary>
    /// Starts <see cref="IPublishedClientHost"/>. Throws until Task 7 implements it.
    /// </summary>
    private static class PublishedClientHost
    {
        public static Task<IPublishedClientHost> StartAsync() =>
            throw new InvalidOperationException(
                "No static host serves the published client yet (story 1.10 Task 7): publish Yello.Client, write " +
                "appsettings.Production.json into the output, and serve it applying staticwebapp.config.json's " +
                "navigationFallback.");
    }
}
