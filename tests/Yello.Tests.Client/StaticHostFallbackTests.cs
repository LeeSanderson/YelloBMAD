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
/// <b>Served by <see cref="PublishedClientHost"/></b>, which publishes the client and applies the
/// published <c>staticwebapp.config.json</c>.
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
/// <b>The static host</b> is <see cref="PublishedClientHost"/>: it publishes the client, writes the
/// <c>appsettings.Production.json</c> the deploy writes, and serves the output applying the published
/// <c>staticwebapp.config.json</c>'s <c>navigationFallback</c> and nothing else (activated by the dev
/// phase of story 1.10).
/// </para>
/// <para>
/// <b>No collection and no fixture.</b> The browser and the static host are started in the case; move
/// them into a collection if a second case ever shares them.
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
    [Fact]
    public async Task A_deep_link_on_the_published_client_boots_and_routes()
    {
        await using var host = await PublishedClientHost.StartAsync(TestContext.Current.CancellationToken);

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
}
