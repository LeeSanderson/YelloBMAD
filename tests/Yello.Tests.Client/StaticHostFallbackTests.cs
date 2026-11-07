using System.Collections.Concurrent;
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
/// <b>Served by <see cref="PublishedClientHost"/></b>, the suite's one client host: the published output,
/// with the deploy's <c>appsettings.Production.json</c>, on an OS port, applying the published
/// <c>staticwebapp.config.json</c>'s <c>navigationFallback</c> and nothing else - so the fallback under
/// test is the file's, not the server's. A dev server would answer every client route with
/// <c>index.html</c> on its own and prove nothing.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AD-7")]
[Trait("Requirement", "AR-38")]
public sealed class StaticHostFallbackTests(ClientSurfaceFixture surface)
{
    private const int RenderTimeoutMilliseconds = 30_000;

    /// <summary>
    /// 1.10-E2E-001 - <c>/spaces/{id}</c> loaded directly from the static host reaches the router: the
    /// application boots and renders a routed page, the framework's error banner stays hidden, and the
    /// client calls the API at the address the deploy wrote - never its own origin.
    /// </summary>
    /// <remarks>
    /// The API address is the one <see cref="PublishedClientHost"/> writes over the published
    /// <c>appsettings.Production.json</c>, as the deploy does. The WebAssembly loader reads only the
    /// appsettings files the boot configuration lists, so this case fails if the committed file stops
    /// being published, or the client stops reading it - in both cases the client falls back to its own
    /// origin. Nothing listens at the address: the browser's requests to it are answered here with a
    /// <c>401</c> carrying the CORS headers the Host would send.
    /// </remarks>
    [Fact]
    public async Task A_deep_link_on_the_published_client_boots_routes_and_calls_the_configured_api()
    {
        await using var context = await surface.NewContextAsync(new BrowserNewContextOptions());
        var page = await context.NewPageAsync();

        var origin = surface.BaseAddress.GetLeftPart(UriPartial.Authority);
        var apiCall = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ownOriginApiCalls = new ConcurrentQueue<string>();

        await page.RouteAsync("**/*", async route =>
        {
            var url = new Uri(route.Request.Url);

            if (url.Host.Equals(PublishedClientHost.ConfiguredApiHost, StringComparison.OrdinalIgnoreCase))
            {
                apiCall.TrySetResult(route.Request.Url);
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 401,
                    Headers = new Dictionary<string, string>
                    {
                        ["Access-Control-Allow-Origin"] = origin,
                        ["Access-Control-Allow-Credentials"] = "true",
                        ["Access-Control-Allow-Headers"] = route.Request.Headers.GetValueOrDefault("access-control-request-headers", "*"),
                        ["Access-Control-Allow-Methods"] = "GET, POST, DELETE",
                    },
                });

                return;
            }

            if (url.AbsolutePath.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                ownOriginApiCalls.Enqueue(route.Request.Url);
            }

            await route.ContinueAsync();
        });

        var response = await page.GotoAsync(new Uri(surface.BaseAddress, $"/spaces/{Guid.NewGuid():D}").ToString());

        Assert.NotNull(response);
        Assert.Equal(200, response.Status);

        await page.Locator("#app h1").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = RenderTimeoutMilliseconds,
        });

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync(), "The client booted into the framework's error banner.");

        var called = await apiCall.Task.WaitAsync(TimeSpan.FromMilliseconds(RenderTimeoutMilliseconds), TestContext.Current.CancellationToken);

        Assert.StartsWith($"https://{PublishedClientHost.ConfiguredApiHost}/api/", called, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(ownOriginApiCalls);
    }
}
