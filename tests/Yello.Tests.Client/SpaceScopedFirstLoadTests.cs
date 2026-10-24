using Microsoft.Playwright;
using Xunit;
using Yello.Contracts.Spaces;

namespace Yello.Tests.Client;

/// <summary>
/// Story 1.9 AC7 / UX-DR31 in a real browser: when a Space-scoped surface loads, nothing renders from
/// cache before the server answers, no request is issued for a Space id the switcher did not list,
/// and nothing is prefetched across a Space boundary. The context bar may render its shell, but never
/// a Space name the server has not just sent.
/// </summary>
/// <remarks>
/// <para>
/// <b>The API is <see cref="FakeYelloApi"/></b>, at the network edge: no Host, no database, no
/// container, so this class adds no engine. A held answer (<see cref="FakeYelloApi.HoldAsync"/>) is
/// what makes "before the server answers" observable.
/// </para>
/// <para>
/// <b>Every absence is anchored</b>: the held answer is released and the name then appears, so "not
/// there" was not a selector that matches nothing.
/// </para>
/// <para>
/// <b>What story 1.7 already proves, and is not repeated:</b> the full-load half of "no request for
/// an unlisted id" (<c>SpaceSwitchingTests.A_Space_id_the_switcher_did_not_list_renders_the_refusal_page_and_is_never_requested</c>)
/// and the Back half of "nothing of the departed Space while the destination is held"
/// (<c>Back_navigation_purges_the_departed_Space_before_the_destination_answers</c>).
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "UX-DR31")]
[Trait("Requirement", "AD-7")]
[Trait("Requirement", "NFR-1")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class SpaceScopedFirstLoadTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;

    private const string ContextBar = "nav.context-bar";
    private const string SpaceName = "nav.context-bar .context-bar__space-name";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string SwitcherTrigger = "nav.context-bar button[aria-controls]";
    private const string Heading = "#app h1";

    private const string EnglishLocale = "en-GB";

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Unlisted = Guid.NewGuid();

    /// <summary>
    /// 1.9-E2E-001 - a first load of a Space with its answer held: no Space name and no Role chip in
    /// the bar, and no Space content in <c>main</c>. Released, the name appears.
    /// </summary>
    [Fact]
    public async Task On_first_load_nothing_of_the_Space_renders_before_the_server_answers()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);

            var arrived = api.HoldAsync(Northwind);

            await page.GotoAsync(Address($"spaces/{Northwind:D}"));
            await arrived.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            Assert.DoesNotContain("Northwind Redesign", await page.Locator("#app").InnerTextAsync(), StringComparison.Ordinal);
            Assert.Equal(0, await page.Locator(SpaceName).CountAsync());
            Assert.Equal(0, await page.Locator(RoleChip).CountAsync());

            api.Release(Northwind);

            await page.Locator(SpaceName, new PageLocatorOptions { HasText = "Northwind Redesign" })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        }
    }

    /// <summary>
    /// 1.9-E2E-002 - switching to another Space with its answer held shows nothing of the departed
    /// Space and nothing of the destination; and returning to a Space visited earlier, held again,
    /// does not render it from what the client saw the first time.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR30")]
    public async Task A_switch_renders_nothing_from_either_Space_until_the_destination_answers()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);

            var toHarbour = api.HoldAsync(Harbour);
            await ChooseInSwitcherAsync(page, "Harbour Ops");
            await toHarbour.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            await AssertNoSpaceRenderedAsync(page, "Northwind Redesign", "Harbour Ops");

            api.Release(Harbour);
            await page.Locator(SpaceName, new PageLocatorOptions { HasText = "Harbour Ops" })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var backToNorthwind = api.HoldAsync(Northwind);
            await ChooseInSwitcherAsync(page, "Northwind Redesign");
            await backToNorthwind.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            await AssertNoSpaceRenderedAsync(page, "Northwind Redesign", "Harbour Ops");

            api.Release(Northwind);
            await page.Locator(SpaceName, new PageLocatorOptions { HasText = "Northwind Redesign" })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        }
    }

    /// <summary>
    /// 1.9-E2E-003 - an in-app navigation (no reload) to a Space id the switcher did not list renders
    /// the refusal page and never requests the id. The full-load half is story 1.7's.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR32")]
    public async Task An_in_app_navigation_to_an_unlisted_Space_is_refused_without_a_request()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);

            // A marker on the window survives an in-app navigation and is lost on a reload.
            await page.EvaluateAsync("() => { window.__inApp = true; }");
            await page.EvaluateAsync("id => Blazor.navigateTo('/spaces/' + id)", Unlisted.ToString("D"));

            await page.Locator(Heading, new PageLocatorOptions { HasText = "Not available." })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.True(await page.EvaluateAsync<bool>("() => window.__inApp === true"), "The navigation reloaded the page, so it was not in-app.");
            Assert.True(api.WasRequested(Northwind), "Anchor: the listed Space was requested, so the request log is being read.");
            Assert.False(api.WasRequested(Unlisted), "The client requested a Space the switcher did not list.");
        }
    }

    /// <summary>
    /// 1.9-E2E-004 - across sign-in, landing and a switch, every Space-scoped request names only the
    /// Space being shown at the time: nothing is prefetched for another listed Space.
    /// </summary>
    [Fact]
    public async Task Nothing_is_prefetched_across_a_Space_boundary()
    {
        var api = TwoSpaces();
        api.SignedIn = false;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(Address("sign-in"));

            await page.Locator("#email-address").FillAsync($"browser-{Guid.NewGuid():N}@yello.test");
            await page.Locator("#password").FillAsync("a-password-nobody-else-uses-1!");
            await page.Locator("form button[type=submit]").ClickAsync();

            await page.Locator(SpaceName, new PageLocatorOptions { HasText = "Northwind Redesign" })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.True(api.WasRequested(Northwind), "Anchor: the landing Space was requested.");
            Assert.False(api.WasRequested(Harbour), "Harbour Ops was requested while Northwind Redesign was shown.");

            var beforeSwitch = api.Requests.Count;

            await ChooseInSwitcherAsync(page, "Harbour Ops");
            await page.Locator(SpaceName, new PageLocatorOptions { HasText = "Harbour Ops" })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var sinceSwitch = api.Requests
                .Skip(beforeSwitch)
                .Where(request => request.Method != "OPTIONS" && request.Path.StartsWith(SpaceRoutes.Directory + "/", StringComparison.Ordinal))
                .Select(request => request.Path)
                .ToList();

            // Anchor: the destination was requested after the switch; then nothing else Space-scoped was.
            Assert.Contains(sinceSwitch, path => path.Contains(Harbour.ToString("D"), StringComparison.OrdinalIgnoreCase));
            Assert.All(sinceSwitch, path => Assert.Contains(Harbour.ToString("D"), path, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// 1.9-E2E-005 / <c>deferred-work.md:348</c> - a listed Space the server refuses and a Space never
    /// listed render structurally the same refusal page: the element tree with tags, roles,
    /// <c>aria-*</c> and classes, the text of each node, and the path of the focused element.
    /// </summary>
    /// <remarks>
    /// Story 1.7's case compares <c>main</c>'s text, which an extra wrapper element or a different
    /// role would pass. Plant for Task 12: an extra wrapper <c>div</c> on one path is caught.
    /// </remarks>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "UX-DR32")]
    public async Task A_refused_listed_Space_and_an_unlisted_one_render_structurally_identical_refusal_pages()
    {
        var api = TwoSpaces();
        api.Roles.Remove(Harbour);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);

            var refused = await RefusalFingerprintAtAsync(page, $"spaces/{Harbour:D}");
            var unlisted = await RefusalFingerprintAtAsync(page, $"spaces/{Unlisted:D}");

            Assert.True(api.WasRequested(Harbour), "The listed Space was never asked about, so its refusal is not the server's.");
            Assert.Contains("Not available.", refused, StringComparison.Ordinal);
            Assert.Equal(refused, unlisted);
        }
    }

    private static async Task AssertNoSpaceRenderedAsync(IPage page, params string[] names)
    {
        var main = await page.Locator("main").InnerTextAsync();
        var label = await page.Locator(ContextBar).GetAttributeAsync("aria-label") ?? string.Empty;

        Assert.Equal(0, await page.Locator(SpaceName).CountAsync());
        Assert.Equal(0, await page.Locator(RoleChip).CountAsync());
        Assert.All(names, name =>
        {
            Assert.DoesNotContain(name, main, StringComparison.Ordinal);
            Assert.DoesNotContain(name, label, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// A signed-in Account who is Owner of Northwind Redesign and Viewer of Harbour Ops, listed in
    /// that order.
    /// </summary>
    private static FakeYelloApi TwoSpaces()
    {
        var api = new FakeYelloApi { SignedIn = true };

        api.Switcher.Add((Northwind, "Northwind Redesign"));
        api.Switcher.Add((Harbour, "Harbour Ops"));
        api.Roles[Northwind] = "owner";
        api.Roles[Harbour] = "viewer";

        return api;
    }

    private string Address(string path) => new Uri(surface.BaseAddress, path).ToString();

    private async Task OpenSpaceAsync(FakeYelloApi api, IPage page, Guid spaceId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address($"spaces/{spaceId:D}"));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    private static async Task ChooseInSwitcherAsync(IPage page, string name)
    {
        var trigger = page.Locator(SwitcherTrigger);
        var list = await trigger.GetAttributeAsync("aria-controls");

        await trigger.ClickAsync();

        var row = page.Locator($"[id='{list}'] a", new PageLocatorOptions { HasText = name });
        await row.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await row.ClickAsync();
    }

    /// <summary>
    /// The refusal page's structure: one line per element under <c>main</c> - depth, tag, role,
    /// <c>aria-*</c> attributes, classes and own text - then the focused element's path from
    /// <c>main</c>.
    /// </summary>
    private async Task<string> RefusalFingerprintAtAsync(IPage page, string path)
    {
        await page.GotoAsync(Address(path));

        await page.Locator(Heading, new PageLocatorOptions { HasText = "Not available." })
            .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

        await page.WaitForFunctionAsync(
            $"() => document.activeElement && document.activeElement.matches('{Heading}')",
            null,
            new PageWaitForFunctionOptions { Timeout = Timeout });

        return await RefusalFingerprint.ReadAsync(page);
    }
}
