using System.Collections.Concurrent;
using Microsoft.Playwright;
using Xunit;

namespace Yello.Tests.Client;

/// <summary>
/// The browser runs the same merge the server runs: the published client loads <c>Yello.Merge</c> at boot. Trimming drops
/// an assembly nothing roots, so this fails if the client stops registering the merge port from <c>Yello.Merge</c>'s
/// factory.
/// </summary>
/// <remarks>
/// Read from the requests the browser makes, not from the publish folder, because the claim is about what a browser
/// loads. The boot downloads every assembly up front (nothing is lazy-loaded), so one request is the whole proof.
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P1")]
[Trait("Requirement", "AR-18")]
[Trait("Requirement", "AD-12")]
public sealed class MergeAssemblyBootTests(ClientSurfaceFixture surface)
{
    private const int RenderTimeoutMilliseconds = 30_000;

    [Fact]
    public async Task The_published_client_loads_the_merge_assembly_at_boot()
    {
        var (context, page) = await surface.OpenAsync("en-GB");
        var loaded = new ConcurrentQueue<string>();

        await using (context)
        {
            page.Response += (_, response) =>
            {
                var path = new Uri(response.Url).AbsolutePath;

                if (path.Contains("/_framework/Yello.Merge", StringComparison.Ordinal) && response.Ok)
                {
                    loaded.Enqueue(path);
                }
            };

            await page.GotoAsync(surface.BaseAddress.ToString());
            await page.Locator("#app h1").WaitForAsync(new LocatorWaitForOptions
            {
                Timeout = RenderTimeoutMilliseconds,
                State = WaitForSelectorState.Attached,
            });

            Assert.False(loaded.IsEmpty, "The client booted without downloading Yello.Merge, so the browser cannot run the merge the server runs.");
            Assert.All(loaded, path => Assert.EndsWith(".wasm", path, StringComparison.Ordinal));
        }
    }
}
