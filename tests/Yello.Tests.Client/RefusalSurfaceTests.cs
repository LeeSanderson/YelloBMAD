using Microsoft.Playwright;
using Xunit;

namespace Yello.Tests.Client;

/// <summary>
/// The refusal page in a browser: an address that matches nothing and the routed
/// <c>/not-available</c> page render the same heading and put focus in the same place. AC9; UX-DR32.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why focus is asserted here.</b> The routed page gets its focus move from App.razor's
/// <c>FocusOnNavigate</c>. The router's <c>NotFound</c> branch has no route data to give it, so
/// without the surface's own move an unknown address left focus where the last link was. The copy
/// was the same and the focus was not - a difference only a browser shows.
/// </para>
/// <para>
/// <b>One locale, deliberately.</b> <see cref="BootSmokeTests"/> already boots every locale. This
/// case is about the page, not the culture, and each page load costs the shared browser.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P1")]
[Trait("Requirement", "UX-DR32")]
[Trait("Requirement", "NFR-9")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class RefusalSurfaceTests(ClientSurfaceFixture surface)
{
    private const int RenderTimeoutMilliseconds = 30_000;

    private const string Locale = "en-GB";

    [Theory]
    [InlineData("not-available")]
    [InlineData("an-address-that-matches-no-route")]
    public async Task Every_route_to_the_refusal_page_renders_it_with_focus_on_its_heading(string path)
    {
        var (context, page) = await surface.OpenAsync(Locale);

        await using (context)
        {
            await page.GotoAsync(new Uri(surface.BaseAddress, path).ToString());

            var heading = page.Locator("#app h1");

            await heading.WaitForAsync(new LocatorWaitForOptions
            {
                Timeout = RenderTimeoutMilliseconds,
                State = WaitForSelectorState.Visible,
            });

            Assert.Equal("Not available.", (await heading.InnerTextAsync()).Trim());

            // Focus moves after render, so wait for it rather than reading it once.
            await page.WaitForFunctionAsync(
                "() => document.activeElement && document.activeElement.matches('#app h1')",
                null,
                new PageWaitForFunctionOptions { Timeout = RenderTimeoutMilliseconds });
        }
    }
}
