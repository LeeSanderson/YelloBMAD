using Microsoft.Playwright;
using Xunit;
using Yello.Contracts.Labels;
using Yello.Contracts.Spaces;

namespace Yello.Tests.Client;

/// <summary>
/// Story 3.2 in a real browser: switching between several Spaces carries nothing over - the switcher, the Role and its
/// affordances, the purge on every trigger with a page kept mounted, browser storage, the first read, scroll, focus and
/// the announcement, two tabs, the route back, keyboard and visible focus, and an Account switch in one tab. AC1-AC17.
/// Written by the story 3.2 ATDD run, 2026-10-30.
/// </summary>
/// <remarks>
/// <para>
/// <b>A delta on story 1.7, with real several-Space data.</b> 1.7's <see cref="SpaceSwitchingTests"/> proved the seam
/// against one Space, where "nothing carries over" is trivially true. These cases seed two or three Spaces with distinct
/// Boards, Labels, Status sets and Project names, so a survivor of the departed Space is something a case can name.
/// </para>
/// <para>
/// <b>The API is <see cref="FakeYelloApi"/></b> at the network edge (story 1.7 Q5): no Host, no database, no container,
/// so this class adds no engine. It joins <see cref="ClientSurfaceCollection"/> - one dev server, one Chromium.
/// </para>
/// <para>
/// <b>Every absence is anchored</b> on a presence in the same case: the departed Space's names are seen on screen before
/// they are asserted gone; a held destination is proved held (its request arrived) before the DOM is read; a request
/// count is read after a barrier request (the switcher's refresh) the client is known to make.
/// </para>
/// <para>
/// <b>Green today, standing gates</b> (built by 1.7 and registered with by Epic 2): the cases left active below. The
/// red-phase cases are skipped with <c>red: AC&lt;n&gt;</c>, each naming what is missing. The checklist
/// (<c>_bmad-output/test-artifacts/atdd-checklist-3-2-switch-between-spaces-with-nothing-carrying-over.md</c>) records
/// which is which and the order to activate them.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-9")]
[Trait("Requirement", "UX-DR31")]
[Trait("Requirement", "NFR-1")]
[Trait("Requirement", "NFR-9")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class SpaceSwitchCarryOverTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;

    private const string EnglishLocale = "en-GB";

    private const string ContextBar = "nav.context-bar";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string BarProject = "nav.context-bar .context-bar__project";
    private const string SwitcherTrigger = "nav.context-bar button[aria-controls]";
    private const string Heading = "#app h1";
    private const string Card = ".task-card";
    private const string Dialog = "[role='dialog']";
    private const string ListRows = "main table tbody tr[data-task-id]";

    private const string NotAvailableCopy = "Not available.";
    private const string BackToYello = "Back to Yello.";
    private const string SettingsLink = "Space settings";
    private const string CreateProject = "Create a Project";
    private const string AddTask = "Add a Task";
    private const string ViewerName = "Harbour Ops — Viewer, read only in this Space";

    private const string NorthwindName = "Northwind Redesign";
    private const string HarbourName = "Harbour Ops";
    private const string LanternName = "Lantern Archive";

    private const string AtlasName = "Atlas Rebrand";
    private const string BeaconName = "Beacon Survey";

    /// <summary>
    /// Where focus is, by role in the bar or by a form control's label: <c>skip</c>, <c>switcher</c>, <c>settings</c>,
    /// <c>account</c>, <c>sign-out</c>, <c>row:{name}</c>, or <c>{tag}:{label or text}</c>.
    /// </summary>
    private const string DescribeFocus =
        """
        () => {
            const a = document.activeElement;
            if (!a || a === document.body) return 'body';
            if (a.matches('.skip-control')) return 'skip';
            if (a.matches('.context-bar__switcher')) return 'switcher';
            if (a.matches('.context-bar__settings')) return 'settings';
            if (a.matches('.context-bar__account')) return 'account';
            if (a.matches('.context-bar__sign-out')) return 'sign-out';
            if (a.closest('#space-switcher-list')) return 'row:' + a.textContent.trim();
            if (a.matches('nav.context-bar')) return 'bar';
            const label = a.labels && a.labels.length ? a.labels[0].textContent.trim() : null;
            return a.tagName.toLowerCase() + ':' + (label || a.getAttribute('aria-label') || a.textContent.trim());
        }
        """;

    /// <summary>
    /// Whether the focused element shows a visible focus indicator that is not inset (<c>EXPERIENCE.md:373</c>).
    /// </summary>
    private const string FocusRingVisible =
        """
        () => {
            const s = getComputedStyle(document.activeElement);
            return s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0 && parseFloat(s.outlineOffset || '0') >= 0;
        }
        """;

    /// <summary>
    /// Records every non-empty text the context bar's polite region changes to, in <c>window.__barSaid</c>. The bar's
    /// announcer is a <c>role="status"</c> paragraph, which <see cref="KeyboardHarness.RecordAnnouncements"/> (it watches
    /// <c>aria-live="polite"</c>) does not see.
    /// </summary>
    private const string RecordBarAnnouncements =
        """
        () => {
            window.__barSaid = [];
            const region = document.querySelector('nav.context-bar [role=status]');
            new MutationObserver(() => {
                const text = region.textContent.trim();
                if (text) window.__barSaid.push(text);
            }).observe(region, { childList: true, characterData: true, subtree: true });
        }
        """;

    private static readonly string[] NorthwindStatuses = ["Todo", "Doing", "Done"];
    private static readonly string[] HarbourStatuses = ["Backlog", "Underway", "Shipped"];

    private static readonly string[] NorthwindTitles = ["Draft the brand book", "Audit the logo files", "Brief the printers"];
    private static readonly string[] HarbourTitles = ["Survey the east pier", "Order new fenders", "Repaint bollard nine"];

    private static readonly string[] NorthwindLabels = ["Quarterly audit", "Legal hold"];
    private static readonly string[] HarbourLabels = ["Pier repairs", "Night shift"];

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Lantern = Guid.NewGuid();
    private static readonly Guid Unlisted = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();
    private static readonly Guid Beacon = Guid.NewGuid();

    /// <summary>
    /// AC1 (FR-9, UX-DR9) - with three Spaces at mixed Roles the switcher lists exactly those three by name, nothing else
    /// on a row (no Role, count or badge), <c>aria-current</c> on the active one only, and the foot link outside the list.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR9")]
    public async Task The_switcher_lists_three_Spaces_at_mixed_Roles_by_name_and_nothing_else()
    {
        var api = SeededSpaces();
        AddLantern(api);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Harbour);

            var rows = await OpenSwitcherAsync(page);

            // Anchor: the row count.
            Assert.Equal(3, await rows.CountAsync());
            Assert.Equal([NorthwindName, HarbourName, LanternName], (await rows.AllInnerTextsAsync()).Select(text => text.Trim()));

            foreach (var text in await rows.AllInnerTextsAsync())
            {
                Assert.DoesNotContain("Owner", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Viewer", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Member", text, StringComparison.OrdinalIgnoreCase);
            }

            Assert.Null(await rows.Nth(0).GetAttributeAsync("aria-current"));
            Assert.Equal("page", await rows.Nth(1).GetAttributeAsync("aria-current"));
            Assert.Null(await rows.Nth(2).GetAttributeAsync("aria-current"));

            // The foot link is in the bar and outside the list.
            Assert.Equal(1, await page.Locator(ContextBar).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Create a Space", Exact = true }).CountAsync());
            Assert.Equal(0, await page.Locator("#space-switcher-list").GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Create a Space", Exact = true }).CountAsync());
        }
    }

    /// <summary>
    /// AC2 / AC17 (NFR-2, X-9) - switching from a Space held as Owner to one held as Viewer: while the destination is held
    /// no Owner affordance, name or chip is on screen; once it answers the chip is <c>Viewer</c> with the Viewer accessible
    /// name, and still no Owner affordance. Switching back restores them from a fresh answer.
    /// </summary>
    [Fact]
    [Trait("Requirement", "NFR-2")]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "UX-DR28")]
    public async Task Switching_from_Owner_to_Viewer_drops_every_Owner_affordance_while_held_and_after()
    {
        var api = SeededSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);

            // Anchors: the Owner's affordances are present before the switch.
            await page.Locator("main").GetByText(CreateProject).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            Assert.Equal(1, await SettingsLinkIn(page).CountAsync());

            var arrived = api.HoldAsync(Harbour);

            await ChooseInSwitcherAsync(page, HarbourName);
            await arrived.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            Assert.Equal(0, await page.Locator("main").GetByText(CreateProject).CountAsync());
            Assert.Equal(0, await SettingsLinkIn(page).CountAsync());
            Assert.Equal(0, await page.Locator(RoleChip).CountAsync());
            Assert.DoesNotContain(NorthwindName, await page.Locator(ContextBar).InnerTextAsync(), StringComparison.Ordinal);

            api.Release(Harbour);

            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(ViewerName, await page.Locator(ContextBar).GetAttributeAsync("aria-label"));
            Assert.Equal(0, await page.Locator("main").GetByText(CreateProject).CountAsync());
            Assert.Equal(0, await SettingsLinkIn(page).CountAsync());

            var northwindReads = ActiveReadsOf(api, Northwind);

            await ChooseInSwitcherAsync(page, NorthwindName);
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Owner" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.Locator("main").GetByText(CreateProject).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(1, await SettingsLinkIn(page).CountAsync());

            // Restored from the destination's answer, read again - never from a client cache.
            Assert.True(ActiveReadsOf(api, Northwind) > northwindReads, "Northwind's Owner affordances returned without a fresh read of its Role.");
        }
    }

    /// <summary>
    /// AC17 (X-9) - as a Viewer on the destination's Board, no Task create control survives from the Owner's Board.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    public async Task A_Viewer_destination_Board_has_no_Task_create_control_after_an_Owners_Board()
    {
        var api = SeededSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);

            // Anchor: the Owner's Board offers creation.
            Assert.True(await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = AddTask, Exact = true }).CountAsync() > 0, "The Owner's Board offers no 'Add a Task' to lose.");

            await NavigateInAppAsync(page, $"/spaces/{Harbour:D}/projects/{Beacon:D}");
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.Locator(Card, new PageLocatorOptions { HasText = HarbourTitles[0] }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = AddTask, Exact = true }).CountAsync());
        }
    }

    /// <summary>
    /// AC2 - on the List view of A with a Status filter checked and a sort chosen, a switch to B and B's List shows no
    /// filter checked and the default sort.
    /// </summary>
    [Fact]
    public async Task A_List_filter_and_sort_do_not_survive_a_switch()
    {
        var api = SeededSpaces(harbourStatuses: NorthwindStatuses);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenListAsync(api, page, Northwind, Atlas);

            var sort = page.Locator("main select").First;
            var defaultSort = await sort.InputValueAsync();

            await page.GetByRole(AriaRole.Checkbox, new PageGetByRoleOptions { Name = "Doing", Exact = true }).CheckAsync(new LocatorCheckOptions { Timeout = Timeout });
            await sort.SelectOptionAsync(new SelectOptionValue { Index = 1 });

            // Anchors: the filter and the sort took.
            Assert.True(await page.GetByRole(AriaRole.Checkbox, new PageGetByRoleOptions { Name = "Doing", Exact = true }).IsCheckedAsync());
            Assert.NotEqual(defaultSort, await sort.InputValueAsync());

            await ChooseInSwitcherAsync(page, HarbourName);
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await NavigateInAppAsync(page, $"/spaces/{Harbour:D}/projects/{Beacon:D}/list");
            await page.Locator(ListRows).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.False(await page.GetByRole(AriaRole.Checkbox, new PageGetByRoleOptions { Name = "Doing", Exact = true }).IsCheckedAsync());
            Assert.Equal(defaultSort, await page.Locator("main select").First.InputValueAsync());
        }
    }

    /// <summary>
    /// AC2 - a Task open on A's Board is not open on B's Board after a switch, and the bar's Project name of A is gone
    /// in B's Space home.
    /// </summary>
    [Fact]
    public async Task An_open_Task_and_the_bars_Project_name_do_not_survive_a_switch()
    {
        var api = SeededSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, api, NorthwindTitles[0]);

            // Anchors: the panel is open and the bar names A's Project.
            Assert.Equal(1, await page.Locator(Dialog).CountAsync());
            Assert.Contains(AtlasName, await page.Locator(BarProject).InnerTextAsync(), StringComparison.Ordinal);

            await NavigateInAppAsync(page, $"/spaces/{Harbour:D}");
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator(BarProject).CountAsync());
            await AssertAbsentAsync(page, [AtlasName, .. NorthwindTitles]);

            await NavigateInAppAsync(page, $"/spaces/{Harbour:D}/projects/{Beacon:D}");
            await page.Locator(Card, new PageLocatorOptions { HasText = HarbourTitles[0] }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Dialog).CountAsync());
            await AssertAbsentAsync(page, [AtlasName, .. NorthwindTitles]);
        }
    }

    /// <summary>
    /// AC3 / AC4 (UX-DR31) - Back and Forward between two Spaces' Boards: while each destination is held, none of the
    /// departed Space's Task titles, Label names, Status names or Project name is anywhere in the DOM.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR30")]
    public async Task Back_and_Forward_between_two_Boards_leave_nothing_of_the_departed_Space_while_held()
    {
        var api = SeededSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);

            // Anchor: A's names are on screen to begin with.
            await AssertPresentAsync(page, [NorthwindTitles[0], NorthwindLabels[0], NorthwindStatuses[1], AtlasName]);

            await NavigateInAppAsync(page, $"/spaces/{Harbour:D}/projects/{Beacon:D}");
            await page.Locator(Card, new PageLocatorOptions { HasText = HarbourTitles[0] }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await AssertPresentAsync(page, [HarbourTitles[0], HarbourLabels[0], HarbourStatuses[1], BeaconName]);

            var back = api.HoldAsync(Northwind);

            await page.GoBackAsync();
            await back.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            await AssertAbsentAsync(page, [.. HarbourTitles, .. HarbourLabels, .. HarbourStatuses, BeaconName, HarbourName]);

            api.Release(Northwind);
            await page.Locator(Card, new PageLocatorOptions { HasText = NorthwindTitles[0] }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var forward = api.HoldAsync(Harbour);

            await page.GoForwardAsync();
            await forward.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            await AssertAbsentAsync(page, [.. NorthwindTitles, .. NorthwindLabels, NorthwindStatuses[1], AtlasName, NorthwindName]);

            api.Release(Harbour);
            await page.Locator(Card, new PageLocatorOptions { HasText = HarbourTitles[0] }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        }
    }

    /// <summary>
    /// AC5 (UX-DR31, AR-14) - after a session through two Spaces' Boards, Lists and settings, nothing is in
    /// <c>localStorage</c>, <c>sessionStorage</c> or IndexedDB. The several-Space path of
    /// <c>SpaceSwitchingTests.No_Space_scoped_content_reaches_web_storage</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-7")]
    public async Task A_session_through_two_Spaces_Boards_Lists_and_settings_leaves_nothing_in_browser_storage()
    {
        var api = SeededSpaces(harbourRole: "owner");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);

            await NavigateInAppAsync(page, $"/spaces/{Northwind:D}/projects/{Atlas:D}/list");
            await page.Locator(ListRows).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await NavigateInAppAsync(page, $"/spaces/{Northwind:D}/settings");
            await page.Locator("main").GetByText(NorthwindLabels[0]).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await NavigateInAppAsync(page, $"/spaces/{Harbour:D}/projects/{Beacon:D}");
            await page.Locator(Card, new PageLocatorOptions { HasText = HarbourTitles[0] }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await NavigateInAppAsync(page, $"/spaces/{Harbour:D}/projects/{Beacon:D}/list");
            await page.Locator(ListRows).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await NavigateInAppAsync(page, $"/spaces/{Harbour:D}/settings");
            await page.Locator("main").GetByText(HarbourLabels[0]).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.EvaluateAsync<int>("() => localStorage.length + sessionStorage.length"));
            Assert.Equal(0, await page.EvaluateAsync<int>("async () => (await indexedDB.databases()).length"));
        }
    }

    /// <summary>
    /// AC6 / AC7 (NFR-1, AR-9) - after A's Board, an in-app navigation to B with A's Project id renders nothing of A's
    /// Board while B's answer is held, and B's <c>404</c> renders the refusal page fingerprint-identical to an unlisted
    /// Space's.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-9")]
    public async Task A_Project_id_followed_under_another_Space_renders_nothing_of_the_first_and_refuses_as_unlisted()
    {
        var api = SeededSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await AssertPresentAsync(page, [NorthwindTitles[0], AtlasName]);

            var held = api.HoldBoardAsync(Atlas);

            await NavigateInAppAsync(page, $"/spaces/{Harbour:D}/projects/{Atlas:D}");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            await AssertAbsentAsync(page, [.. NorthwindTitles, .. NorthwindLabels, AtlasName, NorthwindName]);

            api.ReleaseBoard(Atlas);

            await page.Locator(Heading, new PageLocatorOptions { HasText = NotAvailableCopy }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await WaitForHeadingFocusAsync(page);

            var refused = await RefusalFingerprint.ReadAsync(page);

            await page.GotoAsync(Address($"spaces/{Unlisted:D}"));
            await page.Locator(Heading, new PageLocatorOptions { HasText = NotAvailableCopy }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await WaitForHeadingFocusAsync(page);

            Assert.Equal(await RefusalFingerprint.ReadAsync(page), refused);
        }
    }

    /// <summary>
    /// AC8 / AC2 (UX-DR33, Q4) - after scrolling A's Board, a switch to B's Board and Back to A's each land with the
    /// window and every Board column at scroll offset zero.
    /// </summary>
    /// <remarks>
    /// Q4 (a) assumed: measure first. Blazor's own scroll behaviour on internal navigation is unverified (story Task 4).
    /// If this case is green before any Q4 change, Q4 lands as (b) and this is the case that catches a framework change;
    /// record the measurement in the Debug Log either way.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "UX-DR33")]
    public async Task Scroll_is_at_the_origin_after_a_switch_and_after_Back()
    {
        var api = SeededSpaces(tasksPerBoard: 60);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 500);
            await OpenBoardAsync(api, page, Northwind, Atlas);

            await ScrollEverythingAsync(page);

            // Anchor: something did scroll, or the zeros below would be nothing.
            Assert.True(await ScrolledAsync(page) > 0, "Nothing on A's Board scrolled, so the case proves nothing.");

            await NavigateInAppAsync(page, $"/spaces/{Harbour:D}/projects/{Beacon:D}");
            await page.Locator(Card, new PageLocatorOptions { HasText = HarbourTitles[0] }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await ScrolledAsync(page));

            await ScrollEverythingAsync(page);
            await page.GoBackAsync();
            await page.Locator(Card, new PageLocatorOptions { HasText = NorthwindTitles[0] }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await ScrolledAsync(page));
            Assert.Equal("manual", await page.EvaluateAsync<string>("() => history.scrollRestoration"));
        }
    }

    /// <summary>
    /// AC9 (UX-DR39, UX-DR38, NFR-9) - a switch by Back between Spaces lands focus on the context bar and announces the
    /// destination once, politely; nothing said after it names the departed Space.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR39")]
    [Trait("Requirement", "UX-DR38")]
    public async Task Back_between_Spaces_focuses_the_bar_and_announces_the_destination_once()
    {
        var api = SeededSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);
            await page.EvaluateAsync(RecordBarAnnouncements);

            await ChooseInSwitcherAsync(page, HarbourName);
            await page.Locator(ContextBar, new PageLocatorOptions { HasText = HarbourName }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await WaitUntilBarSaidAsync(page, HarbourName);

            await page.EvaluateAsync("() => { window.__barSaid = []; }");

            await page.GoBackAsync();
            await page.Locator(ContextBar, new PageLocatorOptions { HasText = NorthwindName }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await page.WaitForFunctionAsync(
                $"() => document.activeElement && document.activeElement.matches('{ContextBar}')",
                null,
                new PageWaitForFunctionOptions { Timeout = Timeout });

            await WaitUntilBarSaidAsync(page, NorthwindName);

            var said = await BarSaidAsync(page);

            Assert.Single(said, text => text.Contains(NorthwindName, StringComparison.Ordinal));
            Assert.Contains(said, text => text.Contains("Owner", StringComparison.Ordinal));
            Assert.DoesNotContain(said, text => text.Contains(HarbourName, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// AC10 (<c>EXPERIENCE.md:93</c>, Q6 (a)) - one Account, two tabs in one browser context, on two Spaces: switching,
    /// filtering and purging in tab 1 leaves tab 2's open Task, focus and Space untouched, and tab 2 never asks about A.
    /// </summary>
    /// <remarks>
    /// Q6 (a) assumed: one <see cref="IBrowserContext"/> (shared cookies and storage, like real tabs), each page with its
    /// own <see cref="FakeYelloApi"/> seeded identically, so each tab's requests are its own record.
    /// </remarks>
    [Fact]
    public async Task Two_tabs_on_two_Spaces_share_no_state()
    {
        var first = SeededSpaces(harbourStatuses: NorthwindStatuses);
        var second = SeededSpaces(harbourStatuses: NorthwindStatuses);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            var other = await context.NewPageAsync();

            await OpenBoardAsync(second, other, Harbour, Beacon);
            await OpenPanelAsync(other, second, HarbourTitles[0]);

            var focusedBefore = await other.EvaluateAsync<bool>("() => !!document.activeElement && !!document.activeElement.closest('[role=dialog]')");

            await OpenListAsync(first, page, Northwind, Atlas);
            await page.GetByRole(AriaRole.Checkbox, new PageGetByRoleOptions { Name = "Doing", Exact = true }).CheckAsync(new LocatorCheckOptions { Timeout = Timeout });

            await ChooseInSwitcherAsync(page, HarbourName);
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await NavigateInAppAsync(page, $"/spaces/{Harbour:D}/projects/{Beacon:D}");
            await OpenPanelAsync(page, first, HarbourTitles[1]);

            // Tab 2 is as it was: its panel on its own Task, focus where it was, its Space in its bar.
            Assert.Equal(1, await other.Locator(Dialog).CountAsync());
            Assert.Contains(HarbourTitles[0], await other.Locator(Dialog).InnerTextAsync(), StringComparison.Ordinal);
            Assert.Equal(focusedBefore, await other.EvaluateAsync<bool>("() => !!document.activeElement && !!document.activeElement.closest('[role=dialog]')"));
            Assert.Contains(HarbourName, await other.Locator(ContextBar).InnerTextAsync(), StringComparison.Ordinal);

            // Anchor: tab 1 did ask about A; tab 2 never did.
            Assert.True(first.WasRequested(Northwind));
            Assert.False(second.WasRequested(Northwind), "Tab 2 issued a request naming the Space tab 1 left.");
        }
    }

    /// <summary>
    /// AC12 (UX-DR32, Q1 (a), <c>deferred-work.md:358</c>) - when a listed Space answers <c>409</c> on arrival, "Back to
    /// Yello" lands on a different listed Space, and the refusing Space is asked about once only.
    /// </summary>
    [Fact(Skip = "red: AC12 - the route back always lands on spaces[0], so a Space that refused is offered again (story 3.2 Task 3, Q1 (a))")]
    [Trait("Requirement", "UX-DR32")]
    public async Task The_route_back_skips_a_listed_Space_that_refused_with_409()
    {
        var api = SeededSpaces();
        api.ArrivalRefusals[Northwind] = 409;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(Address($"spaces/{Northwind:D}"));

            // Anchor: Northwind was asked about, and refused.
            await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = BackToYello }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            Assert.Equal(1, ActiveReadsOf(api, Northwind));

            await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = BackToYello }).ClickAsync();

            await WaitForPathAsync(page, $"/spaces/{Harbour:D}");
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(1, ActiveReadsOf(api, Northwind));
        }
    }

    /// <summary>
    /// AC12 (UX-DR32, Q1 (a)) - when every listed Space refuses in this load, the route back stops on the refusal page with
    /// the bar's shell, whose switcher still lists both; no Space is asked about again.
    /// </summary>
    [Fact(Skip = "red: AC12 - with every listed Space refusing, the route back offers spaces[0] again (story 3.2 Task 3, Q1 (a))")]
    [Trait("Requirement", "UX-DR32")]
    public async Task When_every_listed_Space_refuses_the_route_back_stops_on_the_refusal_page()
    {
        var api = SeededSpaces();
        api.ArrivalRefusals[Northwind] = 409;
        api.ArrivalRefusals[Harbour] = 403;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(Address($"spaces/{Northwind:D}"));
            await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = BackToYello }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            // First route back: Harbour, the one not yet refused - which refuses too.
            await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = BackToYello }).ClickAsync();
            await WaitForPathAsync(page, $"/spaces/{Harbour:D}");
            await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = BackToYello }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(1, ActiveReadsOf(api, Northwind));
            Assert.Equal(1, ActiveReadsOf(api, Harbour));

            // Second route back: nothing left to offer, so the refusal page stands, with the shell.
            await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = BackToYello }).ClickAsync();
            await page.Locator(Heading, new PageLocatorOptions { HasText = NotAvailableCopy }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(1, await page.Locator(ContextBar).CountAsync());
            Assert.Equal(0, await page.Locator(RoleChip).CountAsync());

            // Barrier: opening the switcher is a request the client is known to make; after it the counts are read.
            var rows = await OpenSwitcherAsync(page);

            Assert.Equal([NorthwindName, HarbourName], (await rows.AllInnerTextsAsync()).Select(text => text.Trim()));
            Assert.Equal(1, ActiveReadsOf(api, Northwind));
            Assert.Equal(1, ActiveReadsOf(api, Harbour));
        }
    }

    /// <summary>
    /// AC13 (UX-DR32, AC7, <c>deferred-work.md:364</c>) - a tab that visited A, then has A removed from its switcher
    /// answer, follows an in-app navigation (no reload) to A: it is refused with no request for A, and its route back
    /// lands on a listed Space, never on A.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR32")]
    public async Task An_in_app_navigation_to_a_Space_removed_after_a_visit_is_refused_with_no_request()
    {
        var api = SeededSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);

            await ChooseInSwitcherAsync(page, HarbourName);
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            api.Switcher.Remove((Northwind, NorthwindName));
            api.Roles.Remove(Northwind);

            // The switcher's next answer no longer lists A: opening it refreshes the tab's directory.
            var rows = await OpenSwitcherAsync(page);
            await page.WaitForFunctionAsync(
                "() => document.querySelectorAll('#space-switcher-list a').length === 1",
                null,
                new PageWaitForFunctionOptions { Timeout = Timeout });
            Assert.Equal([HarbourName], (await rows.AllInnerTextsAsync()).Select(text => text.Trim()));
            await page.Locator(SwitcherTrigger).PressAsync("Escape");

            var askedBefore = RequestsNaming(api, Northwind);

            await NavigateInAppAsync(page, $"/spaces/{Northwind:D}");
            await page.Locator(Heading, new PageLocatorOptions { HasText = NotAvailableCopy }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(askedBefore, RequestsNaming(api, Northwind));

            await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = BackToYello }).ClickAsync();
            await WaitForPathAsync(page, $"/spaces/{Harbour:D}");

            Assert.Equal(askedBefore, RequestsNaming(api, Northwind));
        }
    }

    /// <summary>
    /// AC14 (AC3, AC4, Q3, <c>deferred-work.md:542</c>) - Back from B's settings to A's keeps the settings page mounted;
    /// while A's answer is held, none of B's Label names is in the DOM.
    /// </summary>
    /// <remarks>
    /// Q3 (a) assumed: plant each defence layer separately and record which the browser sees. Removing the
    /// <c>@key</c> AND skipping <c>LabelSection.Purge</c> together must turn this red; skipping <c>Purge</c> alone may
    /// stay green, which is the data rule holding by the key - record it, and close <c>:542</c> with that record.
    /// </remarks>
    [Fact]
    public async Task Back_from_one_Spaces_settings_to_anothers_leaves_no_Label_of_the_departed_Space_while_held()
    {
        var api = SeededSpaces(harbourRole: "owner");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(Address($"spaces/{Northwind:D}/settings"));
            await page.Locator("main").GetByText(NorthwindLabels[0]).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await NavigateInAppAsync(page, $"/spaces/{Harbour:D}/settings");
            await page.Locator("main").GetByText(HarbourLabels[0]).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            // Anchor: B's Labels are on screen before Back.
            await AssertPresentAsync(page, HarbourLabels);

            var back = api.HoldAsync(Northwind);

            await page.GoBackAsync();
            await back.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            await AssertAbsentAsync(page, [.. HarbourLabels, HarbourName]);

            api.Release(Northwind);
            await page.Locator("main").GetByText(NorthwindLabels[0]).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await AssertAbsentAsync(page, HarbourLabels);
        }
    }

    /// <summary>
    /// AC15 (NFR-9, UX-DR40) - a switch made by keyboard alone: Tab to the trigger, Enter, into the rows, Enter. Focus ends
    /// on the context bar and the destination is announced.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR39")]
    [Trait("Requirement", "UX-DR40")]
    public async Task A_keyboard_only_switch_lands_focus_on_the_bar_and_announces_the_destination()
    {
        var api = SeededSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);
            await page.EvaluateAsync(RecordBarAnnouncements);
            await KeyboardHarness.ResetFocusAsync(page);

            await TabUntilAsync(page, "switcher");
            await page.Keyboard.PressAsync("Enter");
            await page.Locator("#space-switcher-list a").First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await TabUntilAsync(page, "row:" + HarbourName);
            await page.Keyboard.PressAsync("Enter");

            await page.Locator(ContextBar, new PageLocatorOptions { HasText = HarbourName }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.WaitForFunctionAsync(
                $"() => document.activeElement && document.activeElement.matches('{ContextBar}')",
                null,
                new PageWaitForFunctionOptions { Timeout = Timeout });

            await WaitUntilBarSaidAsync(page, HarbourName);
        }
    }

    /// <summary>
    /// AC15 (NFR-9, UX-DR40, UX-DR41) - the context bar's Tab order follows reading order (skip control, switcher, Space
    /// settings for an Owner, Account, Sign out), every stop shows a visible focus ring that is not inset, and at 320px the
    /// Role chip is kept.
    /// </summary>
    [Theory]
    [InlineData("owner", 1280, 800)]
    [InlineData("viewer", 1280, 800)]
    [InlineData("owner", 320, 256)]
    [InlineData("viewer", 320, 256)]
    [Trait("Requirement", "UX-DR40")]
    [Trait("Requirement", "UX-DR41")]
    public async Task The_bars_Tab_order_follows_reading_order_with_a_visible_ring_at_every_stop(string role, int width, int height)
    {
        var api = SeededSpaces(northwindRole: role);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(width, height);
            await OpenSpaceAsync(api, page, Northwind);
            await KeyboardHarness.ResetFocusAsync(page);

            string[] expected = role == "owner"
                ? ["skip", "switcher", "settings", "account", "sign-out"]
                : ["skip", "switcher", "account", "sign-out"];

            var seen = new List<string>();

            foreach (var _ in expected)
            {
                await page.Keyboard.PressAsync("Tab");

                seen.Add(await page.EvaluateAsync<string>(DescribeFocus));

                Assert.True(await page.EvaluateAsync<bool>(FocusRingVisible), $"No visible, non-inset focus ring on '{seen[^1]}'.");
            }

            Assert.Equal(expected, seen);
            Assert.True(await page.Locator(RoleChip).IsVisibleAsync(), "The Role chip is not visible at this width.");
        }
    }

    /// <summary>
    /// AC15 (<c>deferred-work.md:669</c>) - on <c>/spaces/new</c> the Tab order is the name, Create, Cancel, each with a
    /// visible focus ring.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR40")]
    public async Task The_create_pages_Tab_order_is_name_Create_Cancel_with_a_visible_ring()
    {
        var api = SeededSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);
            await NavigateInAppAsync(page, "/spaces/new");
            await page.GetByLabel("Space name", new PageGetByLabelOptions { Exact = true }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await KeyboardHarness.ResetFocusAsync(page);
            await TabUntilAsync(page, "input:Space name");

            Assert.True(await page.EvaluateAsync<bool>(FocusRingVisible), "No visible focus ring on the name field.");

            await page.Keyboard.PressAsync("Tab");
            Assert.Equal("button:Create", await page.EvaluateAsync<string>(DescribeFocus));
            Assert.True(await page.EvaluateAsync<bool>(FocusRingVisible), "No visible focus ring on Create.");

            await page.Keyboard.PressAsync("Tab");
            Assert.Equal("button:Cancel", await page.EvaluateAsync<string>(DescribeFocus));
            Assert.True(await page.EvaluateAsync<bool>(FocusRingVisible), "No visible focus ring on Cancel.");
        }
    }

    /// <summary>
    /// AC15 (<c>deferred-work.md:669</c>) - Space settings' rename section: Rename, then (opened) the field, Save, Cancel,
    /// each with a visible focus ring.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR40")]
    public async Task The_rename_sections_Tab_order_is_Rename_field_Save_Cancel_with_a_visible_ring()
    {
        var api = SeededSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(Address($"spaces/{Northwind:D}/settings"));
            await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Rename", Exact = true })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await KeyboardHarness.ResetFocusAsync(page);
            await TabUntilAsync(page, "button:Rename");

            Assert.True(await page.EvaluateAsync<bool>(FocusRingVisible), "No visible focus ring on Rename.");

            await page.Keyboard.PressAsync("Enter");
            await page.GetByLabel("Space name", new PageGetByLabelOptions { Exact = true }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            if (await page.EvaluateAsync<string>(DescribeFocus) != "input:Space name")
            {
                await TabUntilAsync(page, "input:Space name");
            }

            Assert.True(await page.EvaluateAsync<bool>(FocusRingVisible), "No visible focus ring on the name field.");

            await page.Keyboard.PressAsync("Tab");
            Assert.Equal("button:Save", await page.EvaluateAsync<string>(DescribeFocus));
            Assert.True(await page.EvaluateAsync<bool>(FocusRingVisible), "No visible focus ring on Save.");

            await page.Keyboard.PressAsync("Tab");
            Assert.Equal("button:Cancel", await page.EvaluateAsync<string>(DescribeFocus));
            Assert.True(await page.EvaluateAsync<bool>(FocusRingVisible), "No visible focus ring on Cancel.");
        }
    }

    /// <summary>
    /// AC16 (AC3, <c>EXPERIENCE.md:164</c>) - Account 1 (Spaces A and B) signs out and Account 2 (Space C) signs in on the
    /// same tab with no reload: nothing of Account 1 remains, the switcher is asked for Account 2 before anything
    /// Space-scoped, and no request names A or B after sign-out.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-2")]
    public async Task An_Account_switch_in_one_tab_leaves_nothing_of_the_first_Account()
    {
        var api = SeededSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);

            // Anchor: Account 1's Space and Board are on screen.
            await AssertPresentAsync(page, [NorthwindName, NorthwindTitles[0], AtlasName]);

            await page.Locator(ContextBar).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Sign out" }).ClickAsync();
            await WaitForPathAsync(page, "/sign-in");

            var signedOutAt = api.Requests.Count;

            // The fake's identity becomes Account 2's: one Space, Lantern Archive, held as Member.
            api.Switcher.Clear();
            api.Roles.Clear();
            api.Switcher.Add((Lantern, LanternName));
            api.Roles[Lantern] = "member";

            await page.Locator("#email-address").FillAsync($"second-{Guid.NewGuid():N}@yello.test");
            await page.Locator("#password").FillAsync("a-password-nobody-else-uses-2!");
            await page.Locator("form button[type=submit]").ClickAsync();

            await WaitForPathAsync(page, $"/spaces/{Lantern:D}");
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Member" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await AssertAbsentAsync(page, [NorthwindName, HarbourName, AtlasName, BeaconName, .. NorthwindTitles, .. NorthwindLabels]);

            var rows = await OpenSwitcherAsync(page);

            Assert.Equal([LanternName], (await rows.AllInnerTextsAsync()).Select(text => text.Trim()));

            var after = api.Requests.Skip(signedOutAt).ToList();
            var directory = after.FindIndex(request => request.Method == "GET" && request.Path == SpaceRoutes.Directory);
            var firstScoped = after.FindIndex(request => request.Path.StartsWith(SpaceRoutes.Directory + "/", StringComparison.Ordinal));

            Assert.True(directory >= 0, "The switcher was never asked for Account 2.");
            Assert.True(firstScoped < 0 || directory < firstScoped, "A Space-scoped request preceded Account 2's switcher.");
            Assert.DoesNotContain(after, request => request.Path.Contains(Northwind.ToString("D"), StringComparison.OrdinalIgnoreCase)
                || request.Path.Contains(Harbour.ToString("D"), StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// A signed-in Account holding Northwind Redesign (Owner by default) with the Project Atlas Rebrand, and Harbour Ops
    /// (Viewer by default) with the Project Beacon Survey, listed in that order. Each Board holds its Space's own titles,
    /// Status names and Labels (applied to its first Task), so a survivor of either can be named.
    /// </summary>
    private static FakeYelloApi SeededSpaces(
        string northwindRole = "owner",
        string harbourRole = "viewer",
        string[]? harbourStatuses = null,
        int tasksPerBoard = 0)
    {
        var api = new FakeYelloApi { SignedIn = true };

        api.Switcher.Add((Northwind, NorthwindName));
        api.Switcher.Add((Harbour, HarbourName));
        api.Roles[Northwind] = northwindRole;
        api.Roles[Harbour] = harbourRole;

        SeedSpace(api, Northwind, (Atlas, AtlasName), NorthwindStatuses, NorthwindTitles, NorthwindLabels, tasksPerBoard);
        SeedSpace(api, Harbour, (Beacon, BeaconName), harbourStatuses ?? HarbourStatuses, HarbourTitles, HarbourLabels, tasksPerBoard);

        return api;
    }

    /// <summary>
    /// Adds Lantern Archive, held as Member, third in the switcher.
    /// </summary>
    private static void AddLantern(FakeYelloApi api)
    {
        api.Switcher.Add((Lantern, LanternName));
        api.Roles[Lantern] = "member";
    }

    private static void SeedSpace(
        FakeYelloApi api,
        Guid spaceId,
        (Guid Id, string Name) project,
        string[] statuses,
        string[] titles,
        string[] labelNames,
        int extraTasks)
    {
        var (projectId, projectName) = project;
        var labels = labelNames.Select((name, index) => new FakeLabel(Guid.NewGuid(), name, LabelColours.Keys[index % LabelColours.Keys.Count])).ToList();
        api.Labels[spaceId] = labels;

        var columns = statuses.Select(status => new FakeColumn(Guid.NewGuid(), status, 0, [])).ToList();

        foreach (var (title, index) in titles.Select((title, index) => (title, index)))
        {
            var column = columns[index % columns.Count];
            IReadOnlyList<Guid> applied = index == 0 ? [.. labels.Select(label => label.Id)] : [];

            column.Tasks.Add(new FakeTask(Guid.NewGuid(), title, column.StatusId, $"a{column.Tasks.Count:D5}", DateTimeOffset.UtcNow, LabelIds: applied));
            column.Total++;
        }

        for (var extra = 1; extra <= extraTasks; extra++)
        {
            var column = columns[0];

            column.Tasks.Add(new FakeTask(Guid.NewGuid(), $"{projectName} filler {extra:D3}", column.StatusId, $"a{column.Tasks.Count:D5}", DateTimeOffset.UtcNow, LabelIds: []));
            column.Total++;
        }

        api.Boards[(spaceId, projectId)] = new FakeBoard(new FakeBoardProject(projectId, projectName), columns);
        api.Projects[spaceId] = [new FakeProject(projectId, projectName, DateTimeOffset.UtcNow)];
    }

    private static int ActiveReadsOf(FakeYelloApi api, Guid spaceId) =>
        api.Requests.Count(request => request.Method == "GET" && request.Path == SpaceRoutes.ActiveOf(spaceId));

    private static int RequestsNaming(FakeYelloApi api, Guid spaceId) =>
        api.Requests.Count(request => request.Path.Contains(spaceId.ToString("D"), StringComparison.OrdinalIgnoreCase));

    private static ILocator SettingsLinkIn(IPage page) =>
        page.Locator(ContextBar).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = SettingsLink, Exact = true });

    private static async Task AssertPresentAsync(IPage page, IEnumerable<string> names)
    {
        var body = await page.EvaluateAsync<string>("() => document.body.innerHTML");

        foreach (var name in names)
        {
            Assert.True(body.Contains(name, StringComparison.Ordinal), $"Anchor: '{name}' is not on screen to begin with.");
        }
    }

    private static async Task AssertAbsentAsync(IPage page, IEnumerable<string> names)
    {
        var body = await page.EvaluateAsync<string>("() => document.body.innerHTML");

        foreach (var name in names)
        {
            Assert.False(body.Contains(name, StringComparison.Ordinal), $"'{name}' of the departed Space is still in the DOM.");
        }
    }

    private static Task NavigateInAppAsync(IPage page, string path) =>
        page.EvaluateAsync("path => Blazor.navigateTo(path)", path);

    private static Task ScrollEverythingAsync(IPage page) =>
        page.EvaluateAsync(
            """
            () => {
                window.scrollTo(0, document.documentElement.scrollHeight);
                for (const element of document.querySelectorAll('main *')) {
                    if (element.scrollHeight > element.clientHeight) element.scrollTop = element.scrollHeight;
                }
            }
            """);

    /// <summary>
    /// The window's vertical offset plus every scrolled element's in <c>main</c>.
    /// </summary>
    private static Task<int> ScrolledAsync(IPage page) =>
        page.EvaluateAsync<int>(
            """
            () => Math.round(window.scrollY) + [...document.querySelectorAll('main *')].reduce((sum, element) => sum + Math.round(element.scrollTop), 0)
            """);

    private static async Task TabUntilAsync(IPage page, string expected)
    {
        var seen = new List<string>();

        for (var press = 0; press < 20; press++)
        {
            await page.Keyboard.PressAsync("Tab");

            var focused = await page.EvaluateAsync<string>(DescribeFocus);
            seen.Add(focused);

            if (focused == expected)
            {
                return;
            }
        }

        Assert.Fail($"Tab never reached '{expected}'; passed [{string.Join(" | ", seen)}].");
    }

    private static async Task<List<string>> BarSaidAsync(IPage page) => [.. await page.EvaluateAsync<string[]>("() => window.__barSaid")];

    private static async Task WaitUntilBarSaidAsync(IPage page, string text)
    {
        try
        {
            await page.WaitForFunctionAsync(
                "text => window.__barSaid.some(said => said.includes(text))",
                text,
                new PageWaitForFunctionOptions { Timeout = Timeout, PollingInterval = 50 });
        }
        catch (TimeoutException)
        {
            Assert.Fail($"The bar never said '{text}'; said: [{string.Join(" | ", await BarSaidAsync(page))}].");
        }
    }

    private static Task WaitForHeadingFocusAsync(IPage page) =>
        page.WaitForFunctionAsync(
            $"() => document.activeElement && document.activeElement.matches('{Heading}')",
            null,
            new PageWaitForFunctionOptions { Timeout = Timeout });

    /// <summary>
    /// Polls the page's own <c>location.pathname</c> - never Playwright's URL wait (<c>TESTING-CONVENTIONS.md</c>).
    /// </summary>
    private static Task WaitForPathAsync(IPage page, string path) =>
        page.WaitForFunctionAsync("path => location.pathname === path", path, new PageWaitForFunctionOptions { Timeout = Timeout, PollingInterval = 50 });

    private string Address(string path) => new Uri(surface.BaseAddress, path).ToString();

    private async Task OpenSpaceAsync(FakeYelloApi api, IPage page, Guid spaceId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address($"spaces/{spaceId:D}"));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    private async Task OpenBoardAsync(FakeYelloApi api, IPage page, Guid spaceId, Guid projectId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address($"spaces/{spaceId:D}/projects/{projectId:D}"));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.Locator(Card).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    private async Task OpenListAsync(FakeYelloApi api, IPage page, Guid spaceId, Guid projectId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address($"spaces/{spaceId:D}/projects/{projectId:D}/list"));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.Locator(ListRows).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    private static async Task OpenPanelAsync(IPage page, FakeYelloApi api, string title)
    {
        var taskId = api.Boards.Values.SelectMany(board => board.Columns).SelectMany(column => column.Tasks).First(task => task.Title == title).Id;

        await page.Locator($"{Card} button[data-task-id='{taskId:D}']").ClickAsync();
        await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    private static async Task<ILocator> OpenSwitcherAsync(IPage page)
    {
        var trigger = page.Locator(SwitcherTrigger);
        var list = await trigger.GetAttributeAsync("aria-controls");

        Assert.False(string.IsNullOrEmpty(list), "The switcher trigger names no list in aria-controls.");

        if (await trigger.GetAttributeAsync("aria-expanded") != "true")
        {
            await trigger.ClickAsync();
        }

        var rows = page.Locator($"[id='{list}'] a");
        await rows.First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

        return rows;
    }

    private static async Task ChooseInSwitcherAsync(IPage page, string name)
    {
        var rows = await OpenSwitcherAsync(page);

        await rows.Filter(new LocatorFilterOptions { HasText = name }).ClickAsync();
    }
}
