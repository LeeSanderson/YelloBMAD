using Microsoft.Playwright;
using Xunit;

namespace Yello.Tests.Client;

/// <summary>
/// Story 1.7 in a real browser: signing in lands somewhere, the context bar and its Role chip, the
/// switcher, the purge on switch and on Back, the refusal page's route back, and the browser-owned
/// surfaces. AC1, AC2, AC4, AC5, AC7, AC8, AC9, AC10, AC11; X-8.
/// </summary>
/// <remarks>
/// <para>
/// The API is
/// <see cref="FakeYelloApi"/>, at the network edge (open question 5) - no Host, no database, no
/// container, so this class adds no engine.
/// </para>
/// <para>
/// <b>The one active case</b>, <see cref="A_faked_API_answers_the_client_across_origins_with_credentials"/>,
/// tests the assumption every other case here stands on: that a <c>RouteAsync</c> fake can answer
/// the WebAssembly client's credentialed, cross-origin, custom-header requests at all. It is green
/// today against the sign-in page story 1.4 shipped, and is a standing check on the harness.
/// </para>
/// <para>
/// <b>The selectors are the story's markup</b> (Task 7): <c>nav.context-bar</c>, a
/// <c>.role-chip</c> span inside it, the switcher trigger as the bar's
/// <c>button[aria-controls]</c>, a polite <c>role="status"</c> region in the bar, and the Space
/// home at <c>/spaces/{id}</c> (Task 6). They are stated once, below. A different choice made in
/// dev-story is corrected here and recorded in the Debug Log.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-9")]
[Trait("Requirement", "UX-DR9")]
[Trait("Requirement", "UX-DR28")]
[Trait("Requirement", "UX-DR31")]
[Trait("Requirement", "NFR-9")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class SpaceSwitchingTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;

    private const string ContextBar = "nav.context-bar";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string SwitcherTrigger = "nav.context-bar button[aria-controls]";
    private const string Announcement = "nav.context-bar [role=status]";
    private const string Heading = "#app h1";

    private const string EnglishLocale = "en-GB";

    /// <summary>
    /// The languages <c>base.css</c> withholds the uppercase transform from.
    /// </summary>
    private static readonly string[] LossyCasingLanguages = ["tr", "az", "el"];

    /// <summary>
    /// The sign-in surface's path.
    /// </summary>
    private const string ClientSignInPath = "/sign-in";

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Unlisted = Guid.NewGuid();

    /// <summary>
    /// Q5's harness - a <c>RouteAsync</c> fake answers the client's cross-origin, credentialed
    /// requests: the anti-forgery token it issues is read by the client and comes back on the sign-in
    /// submission's custom header, which a browser sends only after any preflight has passed.
    /// </summary>
    /// <remarks>
    /// <b>Active, and green before story 1.7.</b> If this fails, every scaffold below is standing on
    /// nothing; fix the harness before activating any of them.
    /// </remarks>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "AR-14")]
    public async Task A_faked_API_answers_the_client_across_origins_with_credentials()
    {
        var api = new FakeYelloApi();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(Address("sign-in"));

            await SubmitSignInAsync(page);

            var submission = await api.SignInSeen.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            Assert.Equal(api.IssuedToken, submission.Header(Contracts.Accounts.Antiforgery.HeaderName));
        }
    }

    /// <summary>
    /// AC11 / AC5 - signing in lands on the first Space the switcher lists, with the context bar
    /// naming it and the Role chip showing the Role held there, in sentence case.
    /// </summary>
    /// <remarks>
    /// The first browser-driven sign-in (<c>deferred-work.md:153</c>), faked at the network edge - so
    /// it does not prove the real cookie across origins or the real preflight (re-owned by 1.9).
    /// </remarks>
    [Theory]
    [InlineData("en-GB")]
    [InlineData("tr-TR")]
    [Trait("Requirement", "FR-2")]
    public async Task Signing_in_lands_on_the_first_switcher_Space_with_its_name_and_Role(string locale)
    {
        var api = TwoSpaces();
        api.SignedIn = false;

        var (context, page) = await surface.OpenAsync(locale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(Address("sign-in"));

            await SubmitSignInAsync(page);

            await WaitForPathAsync(page, $"/spaces/{Northwind:D}");
            await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Contains("Northwind Redesign", await page.Locator(ContextBar).InnerTextAsync(), StringComparison.Ordinal);
            Assert.Equal("Owner", (await page.Locator(RoleChip).TextContentAsync())?.Trim());
        }
    }

    /// <summary>
    /// AC1 / AC2 - the switcher lists exactly the Spaces served, in the order served, each row the
    /// Space name and nothing else; only <c>aria-current</c> marks the active one.
    /// </summary>
    [Fact]
    public async Task The_switcher_lists_exactly_the_served_Spaces_by_name_only()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);

            var rows = await OpenSwitcherAsync(page);

            Assert.Equal(["Northwind Redesign", "Harbour Ops"], (await rows.AllInnerTextsAsync()).Select(text => text.Trim()));
            Assert.Equal("page", await rows.Nth(0).GetAttributeAsync("aria-current"));
            Assert.Null(await rows.Nth(1).GetAttributeAsync("aria-current"));
        }
    }

    /// <summary>
    /// AC4 / UX-DR39 - choosing a Space moves focus to the context bar and announces the
    /// destination's name and Role, politely, and nothing of the departed Space.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR39")]
    [Trait("Requirement", "UX-DR38")]
    public async Task Choosing_a_Space_moves_focus_to_the_context_bar_and_announces_the_destination()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);

            var rows = await OpenSwitcherAsync(page);
            await rows.Nth(1).ClickAsync();

            // Waited on the destination's answer reaching the bar rather than on the URL: Playwright's
            // URL wait intermittently never resolved for this history-API navigation even with the
            // page already there (observed with the address and the DOM both on the destination).
            await page.Locator(ContextBar, new PageLocatorOptions { HasText = "Harbour Ops" })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.EndsWith($"/spaces/{Harbour:D}", new Uri(page.Url).AbsolutePath, StringComparison.Ordinal);

            // The final focus owner after FocusOnNavigate has run - the bar must win (Task 7's conflict).
            await page.WaitForFunctionAsync(
                $"() => document.activeElement && document.activeElement.matches('{ContextBar}')",
                null,
                new PageWaitForFunctionOptions { Timeout = Timeout });

            var announced = await page.Locator(Announcement).InnerTextAsync();

            Assert.Contains("Harbour Ops", announced, StringComparison.Ordinal);
            Assert.Contains("Viewer", announced, StringComparison.Ordinal);
            Assert.Contains("read only in this Space", announced, StringComparison.Ordinal);
            Assert.DoesNotContain("Northwind", announced, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC7 - Back purges the departed Space before any render: while the destination's answer is
    /// held, nothing of the departed Space is in the bar.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR30")]
    public async Task Back_navigation_purges_the_departed_Space_before_the_destination_answers()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);

            await (await OpenSwitcherAsync(page)).Nth(1).ClickAsync();
            await page.Locator(ContextBar, new PageLocatorOptions { HasText = "Harbour Ops" })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var arrived = api.HoldAsync(Northwind);

            await page.GoBackAsync();
            await arrived.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            // Held: the destination has not answered, so the bar may show its shell and nothing else.
            Assert.DoesNotContain("Harbour Ops", await page.Locator("#app").InnerTextAsync(), StringComparison.Ordinal);
            Assert.Equal(0, await page.Locator(RoleChip).CountAsync());

            api.Release(Northwind);

            await page.Locator(ContextBar, new PageLocatorOptions { HasText = "Northwind Redesign" })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        }
    }

    /// <summary>
    /// AC10 - a Space id the switcher did not list renders the refusal page, and no Space-scoped
    /// request for it is ever issued.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR32")]
    public async Task A_Space_id_the_switcher_did_not_list_renders_the_refusal_page_and_is_never_requested()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(Address($"spaces/{Unlisted:D}"));

            await page.Locator(Heading, new PageLocatorOptions { HasText = "Not available." })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            // Anchor: the switcher was asked, so the check had something to check against.
            Assert.Contains(api.Requests, request => request.Path == Contracts.Spaces.SpaceRoutes.Directory);
            Assert.False(api.WasRequested(Unlisted), "The client requested a Space the switcher did not list.");
        }
    }

    /// <summary>
    /// AC9 - the two 404 causes - a listed Space the server now refuses, and a Space never listed -
    /// render the same page, words and focus, with no client-side branch between them.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR32")]
    public async Task A_refused_listed_Space_and_an_unlisted_one_render_the_same_refusal_page()
    {
        var api = TwoSpaces();
        api.Roles.Remove(Harbour);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);

            var refused = await RefusalPageAtAsync(page, $"spaces/{Harbour:D}");
            var unlisted = await RefusalPageAtAsync(page, $"spaces/{Unlisted:D}");

            Assert.True(api.WasRequested(Harbour), "The listed Space was never asked about, so its refusal is not the server's.");
            Assert.Equal(refused, unlisted);
        }
    }

    /// <summary>
    /// AC9 / UX-DR32 - the refusal page's route back lands on a Space taken from the switcher's
    /// current answer, never a remembered last Space: having last visited the second Space, Back to
    /// Yello lands on the first listed.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR32")]
    public async Task The_refusal_pages_route_back_lands_on_a_switcher_Space()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Harbour);

            await page.GotoAsync(Address($"spaces/{Unlisted:D}"));
            await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Back to Yello." }).ClickAsync();

            await WaitForPathAsync(page, $"/spaces/{Northwind:D}");

            // The first listed Space - not the one last visited (Harbour Ops), which is what a
            // remembered route back would have chosen.
            Assert.EndsWith($"/spaces/{Northwind:D}", new Uri(page.Url).AbsolutePath, StringComparison.Ordinal);
            Assert.DoesNotContain(Harbour.ToString("D"), page.Url, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// AC5 / X-8 - the Role chip is a bordered, display-only span outside the tab order, uppercase by
    /// <c>text-transform</c> - and not uppercased where the DOCUMENT is Turkish, Azeri or Greek, where
    /// <c>base.css</c> withholds it.
    /// </summary>
    /// <remarks>
    /// <b>The expectation follows the document's language, not the browser's.</b> The exclusion keys
    /// on the document's <c>lang</c>, which is the culture the client resolved -
    /// and with one supported culture (<c>en</c>), a Turkish browser is served English, correctly
    /// uppercased. The expectation is now derived from the document's language, so the day a
    /// Turkish translation lands this case asserts <c>none</c> without being edited.
    /// </remarks>
    [Theory]
    [InlineData("en-GB")]
    [InlineData("tr-TR")]
    [Trait("Priority", "P1")]
    public async Task The_Role_chip_is_bordered_and_outside_the_tab_order(string locale)
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(locale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);

            var chip = page.Locator(RoleChip);

            var documentLanguage = await page.EvaluateAsync<string>("() => document.documentElement.lang");
            var expectedTransform = Array.Exists(LossyCasingLanguages, lossy =>
                documentLanguage == lossy || documentLanguage.StartsWith(lossy + "-", StringComparison.Ordinal))
                ? "none"
                : "uppercase";

            Assert.Equal("SPAN", await chip.EvaluateAsync<string>("e => e.tagName"));
            Assert.Equal(-1, await chip.EvaluateAsync<int>("e => e.tabIndex"));
            Assert.NotEqual("0px", await chip.EvaluateAsync<string>("e => getComputedStyle(e).borderTopWidth"));
            Assert.Equal(expectedTransform, await chip.EvaluateAsync<string>("e => getComputedStyle(e).textTransform"));
            Assert.Equal("Owner", (await chip.TextContentAsync())?.Trim());

            // The exclusion itself, exercised in a browser: until a Turkish translation exists the
            // client never serves a Turkish document, so this row gives the rendered chip one.
            if (string.Equals(locale, "tr-TR", StringComparison.Ordinal))
            {
                await page.EvaluateAsync("() => { document.documentElement.lang = 'tr'; }");

                Assert.Equal("none", await chip.EvaluateAsync<string>("e => getComputedStyle(e).textTransform"));
            }
        }
    }

    /// <summary>
    /// AC8 / UX-DR33 - on a Space route the document title is still the fixed <c>Yello</c>, and scroll
    /// restoration is manual.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR33")]
    public async Task A_Space_route_keeps_the_fixed_title_and_manual_scroll_restoration()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);

            Assert.Equal("Yello", await page.TitleAsync());
            Assert.Equal("manual", await page.EvaluateAsync<string>("() => history.scrollRestoration"));
        }
    }

    /// <summary>
    /// AC7 - after visiting two Spaces, nothing was written to <c>localStorage</c>,
    /// <c>sessionStorage</c> or IndexedDB.
    /// </summary>
    /// <remarks>
    /// The runtime half of <c>ClientStorageBanTests</c>, which bans the mechanism in the IL; this sees
    /// what the browser actually holds after a real switch.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AD-7")]
    public async Task No_Space_scoped_content_reaches_web_storage()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);
            await (await OpenSwitcherAsync(page)).Nth(1).ClickAsync();
            await page.Locator(ContextBar, new PageLocatorOptions { HasText = "Harbour Ops" })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.EvaluateAsync<int>("() => localStorage.length + sessionStorage.length"));
            Assert.Equal(0, await page.EvaluateAsync<int>("async () => (await indexedDB.databases()).length"));
        }
    }

    /// <summary>
    /// AC11 / Q4 - Sign out ends the Session on the server and lands on sign-in with no
    /// <c>reason</c>: the person chose it, nothing ended under them.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-2")]
    public async Task Signing_out_lands_on_sign_in_with_no_reason()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);

            await page.Locator(ContextBar).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Sign out" }).ClickAsync();

            var signOut = await api.SignOutSeen.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            Assert.Equal(api.IssuedToken, signOut.Header(Contracts.Accounts.Antiforgery.HeaderName));

            await WaitForPathAsync(page, ClientSignInPath);

            Assert.DoesNotContain("reason=", page.Url, StringComparison.Ordinal);
            Assert.Equal(0, await page.Locator(ContextBar).CountAsync());
        }
    }

    /// <summary>
    /// AC11 / Q4 - a sign-out refused for its anti-forgery token is sent once more with a fresh
    /// token, so the server still ends the Session and expires the cookie.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-2")]
    public async Task A_sign_out_refused_for_its_token_is_sent_again_with_a_fresh_one()
    {
        var api = TwoSpaces();
        api.RefuseFirstSignOut = true;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);

            await page.Locator(ContextBar).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Sign out" }).ClickAsync();

            await WaitForPathAsync(page, ClientSignInPath);

            Assert.Equal(2, api.Requests.Count(request => request.Method == "DELETE" && request.Path == Contracts.Accounts.SessionRoutes.Authenticate));
            Assert.False(api.SignedIn, "The second sign-out never reached the server.");
        }
    }

    /// <summary>
    /// Q8 / UX-DR40 - Esc closes the switcher from its trigger, where focus sits once it is opened,
    /// and focus stays on the trigger.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR40")]
    public async Task Esc_on_the_trigger_closes_the_switcher_and_keeps_focus_there()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);
            await OpenSwitcherAsync(page);

            var trigger = page.Locator(SwitcherTrigger);
            await trigger.PressAsync("Escape");

            await page.WaitForFunctionAsync(
                $"() => document.querySelector('{SwitcherTrigger}').getAttribute('aria-expanded') === 'false'",
                null,
                new PageWaitForFunctionOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator("nav.context-bar .space-switcher").CountAsync());
            Assert.True(await trigger.EvaluateAsync<bool>("e => e === document.activeElement"));
        }
    }

    /// <summary>
    /// AC11 / Q6 / UX-DR35 - an Account holding no Membership arriving at <c>/</c> is told so, and no
    /// Space is created for it.
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "UX-DR35")]
    [Trait("Requirement", "FR-7")]
    public async Task An_Account_with_no_Space_is_told_so_and_none_is_created()
    {
        var api = new FakeYelloApi { SignedIn = true };
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(surface.BaseAddress.ToString());

            await page.Locator(Heading).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Contains(api.Requests, request => request.Path == Contracts.Spaces.SpaceRoutes.Directory);
            Assert.DoesNotContain(api.Requests, request => request.Method == "POST" && request.Path.StartsWith(Contracts.Spaces.SpaceRoutes.Directory, StringComparison.Ordinal));
            Assert.DoesNotContain("sign-in", page.Url, StringComparison.Ordinal);
            Assert.Equal(0, await page.Locator(RoleChip).CountAsync());

            // The heading renders after FocusOnNavigate has run, so the page focuses it itself.
            await page.WaitForFunctionAsync(
                $"() => document.activeElement && document.activeElement.matches('{Heading}')",
                null,
                new PageWaitForFunctionOptions { Timeout = Timeout });
        }
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

    /// <summary>
    /// Waits until the page's own <c>location.pathname</c> is <paramref name="path"/>.
    /// </summary>
    /// <remarks>
    /// <b>Not Playwright's URL wait.</b> Its default waits for a <c>load</c> event a history-API
    /// navigation never fires; with <c>Commit</c> instead it still hung intermittently on a navigation
    /// that had already happened (the address and the DOM both on the destination). Polling the page's
    /// own location has no such gap - see <c>TESTING-CONVENTIONS.md</c>.
    /// </remarks>
    private static Task WaitForPathAsync(IPage page, string path) =>
        page.WaitForFunctionAsync(
            "path => location.pathname === path",
            path,
            new PageWaitForFunctionOptions { Timeout = Timeout });

    private string Address(string path) => new Uri(surface.BaseAddress, path).ToString();

    private async Task OpenSpaceAsync(FakeYelloApi api, IPage page, Guid spaceId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address($"spaces/{spaceId:D}"));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    private static async Task<ILocator> OpenSwitcherAsync(IPage page)
    {
        var trigger = page.Locator(SwitcherTrigger);
        var list = await trigger.GetAttributeAsync("aria-controls");

        Assert.False(string.IsNullOrEmpty(list), "The switcher trigger names no list in aria-controls.");

        await trigger.ClickAsync();

        Assert.Equal("true", await trigger.GetAttributeAsync("aria-expanded"));

        var rows = page.Locator($"[id='{list}'] a");
        await rows.First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

        return rows;
    }

    private async Task<(string Text, string Focused)> RefusalPageAtAsync(IPage page, string path)
    {
        await page.GotoAsync(Address(path));

        await page.Locator(Heading, new PageLocatorOptions { HasText = "Not available." })
            .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

        await page.WaitForFunctionAsync(
            $"() => document.activeElement && document.activeElement.matches('{Heading}')",
            null,
            new PageWaitForFunctionOptions { Timeout = Timeout });

        return (
            await page.Locator("main").InnerTextAsync(),
            await page.EvaluateAsync<string>("() => document.activeElement.outerHTML"));
    }

    private static async Task SubmitSignInAsync(IPage page)
    {
        await page.Locator("#email-address").FillAsync($"browser-{Guid.NewGuid():N}@yello.test");
        await page.Locator("#password").FillAsync("a-password-nobody-else-uses-1!");
        await page.Locator("form button[type=submit]").ClickAsync();
    }
}
