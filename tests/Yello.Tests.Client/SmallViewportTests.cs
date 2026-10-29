using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;
using Yello.Contracts.Labels;
using static Yello.Tests.Client.DragHarness;
using static Yello.Tests.Client.KeyboardHarness;

namespace Yello.Tests.Client;

/// <summary>
/// Story 2.11 in a real browser: the Board at every breakpoint. At 1280px and wider every column of five Statuses shows
/// side by side with the full context bar; at 768-1279px the row scrolls sideways inside itself and the bar drops the
/// Project name; below 768px one column shows at a time behind the Status pager - a tablist whose panel is the column, the
/// only route to another Status - with its keys, its true-total announcement, the Board following a moved Task, the
/// keyboard and the drag held to the visible column, and the 1.4.10 reflow audit at 320 CSS px. AC1-AC20; UX-DR20, UX-DR41,
/// UX-DR42; NFR-9; X-11.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every case states its viewport</b> with <see cref="IPage.SetViewportSizeAsync(int, int)"/>; Playwright cannot apply
/// browser page zoom, so 400% on a 1280×1024 monitor is the 320×256 CSS viewport it yields (open question 10 (a)). RTL is
/// forced with <c>dir="rtl"</c> on the document, as story 2.8 did: no product locale is right to left.
/// </para>
/// <para>
/// <b>The pager appears once the keys module has measured the viewport</b>, after the Board's first render: every narrow
/// case waits for the tablist first (<see cref="WaitForPagerAsync"/>). The CSS hides the unselected columns from the first
/// paint regardless.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P1")]
[Trait("Requirement", "UX-DR20")]
[Trait("Requirement", "UX-DR41")]
[Trait("Requirement", "NFR-9")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class SmallViewportTests(ClientSurfaceFixture surface)
{
    private const string Tablist = "main [role='tablist']";
    private const string Tab = "main [role='tab']";
    private const string SelectedTab = "main [role='tab'][aria-selected='true']";
    private const string Columns = "main .board-column";
    private const string Dialog = "[role='dialog']";
    private const string Menu = "[role='menu']";
    private const string PickerOption = ".move-picker [role='option']";
    private const string AddTask = "Add a Task";
    private const string BottomOfColumn = "Bottom of column";
    private const string InsufficientRoleType = "urn:yello:problem:insufficient-role";

    /// <summary>
    /// A Space name of exactly 50 characters (AC16).
    /// </summary>
    private const string LongSpaceName = "Northwind Redesign and the Quarterly Planning Team";

    /// <summary>
    /// The 1.4.10 measurement (open question 10 (a)): the page never scrolls sideways, and no element in <c>main</c>, the
    /// context bar or an open dialog has a horizontal scroll. Returns the problems.
    /// </summary>
    private const string Reflow =
        """
        () => {
            const problems = [];
            const page = document.scrollingElement;
            if (page.scrollWidth > page.clientWidth + 1) problems.push('the page scrolls sideways by ' + (page.scrollWidth - page.clientWidth) + 'px');
            for (const element of document.querySelectorAll('main *, nav.context-bar, nav.context-bar *, [role="dialog"], [role="dialog"] *')) {
                const style = getComputedStyle(element);
                if (style.display === 'none') continue;
                if (['auto', 'scroll'].includes(style.overflowX) && element.scrollWidth > element.clientWidth + 1) {
                    problems.push('scrolls sideways: ' + element.tagName + '.' + element.className + ' by ' + (element.scrollWidth - element.clientWidth) + 'px');
                }
            }
            return problems;
        }
        """;

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();
    private static readonly Guid Beacon = Guid.NewGuid();

    /// <summary>
    /// AC1, AC17, Q9 (a) - at 1280×800 a Board of five Statuses shows every column wholly inside the viewport with no
    /// horizontal Board scroll, no pager, and a context bar carrying the Space name, the Project name and the Role chip.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task At_1280px_five_columns_fit_side_by_side_under_the_full_context_bar()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, FiveStatuses, ["Alpha"], ["Bravo"], ["Charlie"], ["Delta"], ["Echo"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);
            await WaitForKeysAsync(page);

            var outside = await page.EvaluateAsync<string[]>(
                "() => [...document.querySelectorAll('main .board-column')].filter(c => { const r = c.getBoundingClientRect(); return r.left < 0 || r.right > window.innerWidth || r.width === 0; }).map(c => c.querySelector('h2').textContent.trim())");

            Assert.Equal(5, await page.Locator(Columns).CountAsync());
            Assert.Empty(outside);
            Assert.True(await page.EvaluateAsync<bool>("() => { const row = document.querySelector('.board__columns'); return row.scrollWidth <= row.clientWidth; }"), "The Board scrolls sideways at 1280px.");
            Assert.Equal(0, await page.Locator(Tablist).CountAsync());
            Assert.True(await page.Locator("nav.context-bar .context-bar__space-name").IsVisibleAsync());
            Assert.True(await page.Locator("nav.context-bar .context-bar__project").IsVisibleAsync(), "The Project name is not in the bar at 1280px.");
            Assert.True(await page.Locator(RoleChip).IsVisibleAsync());
        }
    }

    /// <summary>
    /// AC2, AC17 - at 1024×800 the five columns sit side by side and the row scrolls sideways inside itself, the page does
    /// not, and the bar drops the Project name while keeping the Space name and the Role chip.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task At_1024px_the_row_scrolls_inside_itself_and_the_bar_drops_the_Project_name()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, FiveStatuses, ["Alpha"], ["Bravo"], ["Charlie"], ["Delta"], ["Echo"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1024, 800);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);
            await WaitForKeysAsync(page);

            Assert.True(await page.EvaluateAsync<bool>("() => { const row = document.querySelector('.board__columns'); return row.scrollWidth > row.clientWidth; }"), "The row does not scroll sideways at 1024px.");
            Assert.True(await page.EvaluateAsync<bool>("() => document.scrollingElement.scrollWidth <= document.scrollingElement.clientWidth"), "The page scrolls sideways at 1024px.");
            Assert.Equal(0, await page.Locator(Tablist).CountAsync());
            Assert.Equal(0, await page.Locator($"{Columns}[hidden]").CountAsync());
            Assert.False(await page.Locator("nav.context-bar .context-bar__project").IsVisibleAsync(), "The Project name is still in the bar at 1024px.");
            Assert.True(await page.Locator("nav.context-bar .context-bar__space-name").IsVisibleAsync());
            Assert.True(await page.Locator(RoleChip).IsVisibleAsync());
        }
    }

    /// <summary>
    /// AC3, AC9, AC11, Q1 (a) - at 375×667 the Board is one column behind a tablist with one tab per Status in effective
    /// order; exactly one is selected, controls a tabpanel labelled by it, and is the tablist's one tab stop; every other
    /// column is hidden and out of the accessibility tree.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task At_375px_one_column_shows_behind_a_tablist_over_the_Statuses()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Alpha"], ["Bravo"], ["Charlie"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenNarrowAsync(api, page, 375, 667);

            Assert.Equal(ThreeStatuses, await TabNamesAsync(page));
            Assert.Equal("Statuses", await page.Locator(Tablist).GetAttributeAsync("aria-label"));
            Assert.Equal(1, await page.Locator(SelectedTab).CountAsync());
            Assert.Equal(1, await page.Locator($"{Tab}[tabindex='0']").CountAsync());
            Assert.Equal(2, await page.Locator($"{Tab}[tabindex='-1']").CountAsync());

            var selected = page.Locator(SelectedTab);
            var panelId = await selected.GetAttributeAsync("aria-controls");
            var panel = page.Locator($"[id='{panelId}']");

            Assert.Equal("tabpanel", await panel.GetAttributeAsync("role"));
            Assert.Equal(await selected.GetAttributeAsync("id"), await panel.GetAttributeAsync("aria-labelledby"));
            Assert.True(await panel.IsVisibleAsync());
            Assert.Equal(["Alpha"], await TitlesInAsync(page, "Todo"));

            // The others: hidden, and absent from the accessibility tree (getByRole leaves hidden elements out).
            Assert.Equal(2, await page.Locator($"{Columns}[hidden]").CountAsync());
            Assert.False(await ColumnNamed(page, "Doing").IsVisibleAsync());
            Assert.False(await ColumnNamed(page, "Done").IsVisibleAsync());
            Assert.Equal(1, await page.GetByRole(AriaRole.Tabpanel).CountAsync());
            Assert.Equal(0, await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Bravo", Exact = true }).CountAsync());
        }
    }

    /// <summary>
    /// AC4, AC10, AD-29, Q3 (a), Q4 (a) - on the tablist <c>→</c>, <c>←</c>, <c>Home</c> and <c>End</c> move focus and
    /// selection together and clamp at the ends; each change says "{Status}, {n} Tasks." once with the TRUE total - 120 in a
    /// column showing 50 - and the same sentence on a return is said again.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-29")]
    public async Task The_tablist_keys_move_focus_and_selection_and_say_the_true_total()
    {
        var api = TwoSpaces(Northwind, Harbour);
        api.PageSize = 50;
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, Many(120), ["Bravo", "Charlie", "Delta"], ["Echo"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenNarrowAsync(api, page, 375, 667);
            await page.EvaluateAsync(RecordAnnouncements);

            Assert.Equal(50, await ColumnNamed(page, "Todo").Locator(".task-card").CountAsync());

            await TabNamed(page, "Todo").FocusAsync();
            await PressOnPagerAsync(page, "ArrowRight", "Doing");
            await WaitUntilSaidAsync(page, "Doing, 3 Tasks.");
            await PressOnPagerAsync(page, "ArrowRight", "Done");
            await WaitUntilSaidAsync(page, "Done, 1 Task.");

            // The end clamps: nothing moves, nothing is said.
            await PressOnPagerAsync(page, "ArrowRight", "Done");
            await PressOnPagerAsync(page, "Home", "Todo");
            await WaitUntilSaidAsync(page, "Todo, 120 Tasks.");

            // The start clamps too.
            await PressOnPagerAsync(page, "ArrowLeft", "Todo");
            await PressOnPagerAsync(page, "End", "Done");
            await WaitUntilSaidAsync(page, "Done, 1 Task.", 2);
            await PressOnPagerAsync(page, "Home", "Todo");
            await WaitUntilSaidAsync(page, "Todo, 120 Tasks.", 2);

            var said = await SaidAsync(page);

            Assert.Equal(1, said.Count(sentence => sentence.Contains("Doing, 3 Tasks.", StringComparison.Ordinal)));
            Assert.Equal(2, said.Count(sentence => sentence.Contains("Done, 1 Task.", StringComparison.Ordinal)));
            Assert.Equal(2, said.Count(sentence => sentence.Contains("Todo, 120 Tasks.", StringComparison.Ordinal)));
            Assert.DoesNotContain(said, sentence => sentence.Contains("50 Tasks", StringComparison.Ordinal));
            Assert.Empty(api.ColumnPageRequests);
        }
    }

    /// <summary>
    /// AC8, AC9, UX-DR42 - under <c>dir="rtl"</c> <c>←</c> on the tablist goes to the next Status and <c>→</c> to the previous;
    /// the tabs' order in the DOM and in the accessibility tree is still the Status sequence.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR42")]
    public async Task Under_right_to_left_the_tablist_arrows_mirror_and_the_Status_order_does_not()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Alpha"], ["Bravo"], ["Charlie"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenNarrowAsync(api, page, 375, 667);
            await page.EvaluateAsync("() => { document.documentElement.dir = 'rtl'; }");

            await TabNamed(page, "Todo").FocusAsync();
            await PressOnPagerAsync(page, "ArrowLeft", "Doing");
            await PressOnPagerAsync(page, "ArrowLeft", "Done");
            await PressOnPagerAsync(page, "ArrowRight", "Doing");

            Assert.Equal(ThreeStatuses, await TabNamesAsync(page));
            Assert.Equal(
                ThreeStatuses,
                (await page.GetByRole(AriaRole.Tab).AllTextContentsAsync()).Select(text => ThreeStatuses.First(status => text.Trim().StartsWith(status, StringComparison.Ordinal))).ToArray());
        }
    }

    /// <summary>
    /// AC5, AC10, Q2 (a) - at 375px a move from the card's actions control and Move picker takes the pager with the Task:
    /// while it is held the destination tab is already selected and focus is on the card's title; on admission "Moved to …"
    /// is said and no tab sentence is; refused, the tab follows the card back to where it was.
    /// </summary>
    /// <param name="admitted">Whether the move is admitted (otherwise refused).</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "UX-DR21")]
    public async Task A_move_from_the_card_menu_takes_the_pager_with_the_Task(bool admitted)
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Alpha", "Bravo"], ["Charlie"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenNarrowAsync(api, page, 375, 667);
            await page.EvaluateAsync(RecordAnnouncements);

            await ActionsFor(page, "Alpha").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await page.Locator(Menu).GetByRole(AriaRole.Menuitem, new LocatorGetByRoleOptions { Name = "Move", Exact = true }).ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await page.Locator(PickerOption, new PageLocatorOptions { HasText = "Doing" }).First.ClickAsync(new LocatorClickOptions { Timeout = WaitMs });

            var held = api.HoldTaskMoveAsync();

            await page.Locator(PickerOption, new PageLocatorOptions { HasText = BottomOfColumn }).First.ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await held.WaitAsync(TimeSpan.FromMilliseconds(WaitMs), TestContext.Current.CancellationToken);

            await WaitForSelectedTabAsync(page, "Doing");
            await WaitForFocusAsync(page, "Alpha");
            Assert.True(await CardNamed(page, "Alpha").IsVisibleAsync());

            if (admitted)
            {
                api.ReleaseTaskMove();
                await WaitUntilSaidAsync(page, "Moved to Doing, position 2 of 2.");

                Assert.Equal("Doing", await SelectedTabNameAsync(page));
            }
            else
            {
                api.ReleaseTaskMoveRefused(403, InsufficientRoleType, "manage-tasks");
                await WaitUntilSaidAsync(page, "Returned to Todo, position 1.");
                await WaitForSelectedTabAsync(page, "Todo");
                await WaitForFocusAsync(page, "Alpha");

                Assert.True(await CardNamed(page, "Alpha").IsVisibleAsync());
            }

            Assert.DoesNotContain(await SaidAsync(page), sentence => Regex.IsMatch(sentence, @"^(Todo|Doing|Done), \d+ Tasks?\.$"));
        }
    }

    /// <summary>
    /// AC5, UX-DR39 - at 375px a move from the Task detail panel's Move control selects the destination tab while the panel
    /// keeps focus; closing the panel returns focus to the card, which is in the column on screen.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR21")]
    public async Task A_move_from_the_panel_takes_the_pager_and_closing_returns_focus_to_the_visible_card()
    {
        var api = TwoSpaces(Northwind, Harbour);
        var tasks = Seed(api, Northwind, Atlas, ThreeStatuses, ["Alpha", "Bravo"], ["Charlie"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenNarrowAsync(api, page, 375, 667);
            await page.EvaluateAsync(RecordAnnouncements);

            await page.Locator($".task-card button[data-task-id='{tasks["Alpha"].Id:D}']").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            await page.Locator(Dialog).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Move", Exact = true }).ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await page.Locator(PickerOption, new PageLocatorOptions { HasText = "Doing" }).First.ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await page.Locator(PickerOption, new PageLocatorOptions { HasText = BottomOfColumn }).First.ClickAsync(new LocatorClickOptions { Timeout = WaitMs });

            await WaitUntilSaidAsync(page, "Moved to Doing, position 2 of 2.");
            await WaitForSelectedTabAsync(page, "Doing");

            Assert.True(await page.EvaluateAsync<bool>("() => !!document.activeElement && !!document.activeElement.closest('[role=\"dialog\"]')"), "Focus left the panel on the move.");

            await page.Keyboard.PressAsync("Escape");
            await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = WaitMs });
            await WaitForFocusAsync(page, "Alpha");

            Assert.True(await CardNamed(page, "Alpha").IsVisibleAsync());
            Assert.Equal("Doing", await SelectedTabNameAsync(page));
        }
    }

    /// <summary>
    /// AC12, Q5 (a), X-4 - at 375px on a card <c>←</c>/<c>→</c> move nothing and are prevented; <c>Space</c>, a carried
    /// <c>↓</c> and <c>Space</c> reorder within the column; a carried <c>→</c> leaves the Task in its column; and the
    /// cross-Status equivalent is <c>Enter</c> on the actions control, then the Move picker.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR24")]
    public async Task At_375px_the_inline_arrows_move_nothing_and_Space_reorders_within_the_column()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Alpha", "Bravo", "Charlie"], ["Delta"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenNarrowAsync(api, page, 375, 667);
            await WaitForKeysAsync(page);
            await page.EvaluateAsync(RecordKeys);
            await page.EvaluateAsync(RecordAnnouncements);
            await FocusCardAsync(page, "Alpha");

            // Each inline arrow is followed by ↓: in the first column it reaches Bravo, so landing anywhere else proves the arrow moved.
            await page.Keyboard.PressAsync("ArrowRight");
            await PressAndFocusAsync(page, "ArrowDown", "Bravo");
            await page.Keyboard.PressAsync("ArrowLeft");
            await PressAndFocusAsync(page, "ArrowUp", "Alpha");

            Assert.Equal(new List<bool> { true, true, true, true }, await ArrowsAsync(page));
            Assert.Equal("Todo", await SelectedTabNameAsync(page));

            await page.Keyboard.PressAsync(" ");
            await page.Locator(".task-card--carried").WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            await page.Keyboard.PressAsync("ArrowDown");
            await WaitUntilSaidAsync(page, "Todo, position 2 of 3.");
            await page.Keyboard.PressAsync(" ");
            await WaitUntilSaidAsync(page, "Moved to Todo, position 2 of 3.");

            Assert.Equal(["Bravo", "Alpha", "Charlie"], await TitlesInAsync(page, "Todo"));

            await WaitForFocusAsync(page, "Alpha");
            await page.Keyboard.PressAsync(" ");
            await page.Locator(".task-card--carried").WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            await page.Keyboard.PressAsync("ArrowRight");
            await page.Keyboard.PressAsync("Escape");
            await WaitUntilSaidAsync(page, "Returned to Todo, position 2.");

            Assert.Equal(["Bravo", "Alpha", "Charlie"], await TitlesInAsync(page, "Todo"));
            Assert.Single(Moves(api));
            Assert.DoesNotContain(await SaidAsync(page), sentence => sentence.Contains("Doing", StringComparison.Ordinal));

            await ActionsFor(page, "Bravo").FocusAsync(new LocatorFocusOptions { Timeout = WaitMs });
            await page.Keyboard.PressAsync("Enter");
            await page.Locator(Menu).GetByRole(AriaRole.Menuitem, new LocatorGetByRoleOptions { Name = "Move", Exact = true }).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
        }
    }

    /// <summary>
    /// AC13, Q6 (a), <c>deferred-work.md:574</c> - at 375px a pointer drag toward the inline edge never previews a drop in a
    /// hidden column nor scrolls the Board sideways, and the drop lands in the visible column: no cross-Status move is sent.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR22")]
    public async Task At_375px_a_pointer_drag_drops_only_within_the_visible_column()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Alpha", "Bravo"], ["Charlie"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenNarrowAsync(api, page, 375, 667);
            await page.EvaluateAsync(RecordAnnouncements);

            await LiftWithMouseAsync(page, "Alpha");

            var bravo = await PointInAsync(CardNamed(page, "Bravo"), 0.8f);

            await MoveMouseToAsync(page, (373, bravo.Y));
            await MoveMouseToAsync(page, (2, bravo.Y));

            Assert.Equal(0, await page.Locator($"{Columns}:not([data-pager-selected]) .drop-zone").CountAsync());
            Assert.Equal(0, await page.EvaluateAsync<double>("() => document.querySelector('.board__columns').scrollLeft"));

            await MoveMouseToAsync(page, bravo);
            await WaitForDropZoneAsync(page, "Todo", null);
            await page.Mouse.UpAsync();
            await WaitUntilSaidAsync(page, "Moved to Todo, position 2 of 2.");

            using var body = JsonDocument.Parse(Assert.Single(Moves(api)).Body ?? "{}");

            Assert.Equal(StatusOf(api, Northwind, Atlas, "Todo"), body.RootElement.GetProperty("statusId").GetGuid());
            Assert.Equal("Todo", await SelectedTabNameAsync(page));
        }
    }

    /// <summary>
    /// AC13, UX-DR42, <c>deferred-work.md:574</c> - at 1024px under <c>dir="rtl"</c> a pointer drag held at the inline end
    /// (the view's physical left) auto-scrolls the Board toward the last column, and the drop lands there: drag direction
    /// mirrors with the layout.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR42")]
    [Trait("Requirement", "UX-DR22")]
    public async Task Under_right_to_left_at_1024px_a_drag_to_the_inline_end_scrolls_and_drops_there()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, FiveStatuses, ["Alpha", "Bravo"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1024, 800);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);
            await page.EvaluateAsync("() => { document.documentElement.dir = 'rtl'; }");
            await page.EvaluateAsync(RecordAnnouncements);

            Assert.False(await InViewAsync(page, "Done"), "The last column is already in view; the case proves nothing.");

            await LiftWithMouseAsync(page, "Alpha");

            var column = await BoxAsync(ColumnNamed(page, "Todo"));

            await MoveMouseToAsync(page, (6, column.Y + 60));
            await page.WaitForFunctionAsync(
                "() => { const done = [...document.querySelectorAll('main .board-column')].find(c => c.querySelector('h2').textContent.trim() === 'Done'); const r = done.getBoundingClientRect(); return document.querySelector('.board__columns').scrollLeft < 0 && r.left >= 0; }",
                null,
                new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });

            var done = await BoxAsync(ColumnNamed(page, "Done"));

            await MoveMouseToAsync(page, (done.X + (done.Width / 2), done.Y + Math.Min(40, done.Height / 2)));
            await WaitForDropZoneAsync(page, "Done", null);
            await page.Mouse.UpAsync();
            await WaitUntilSaidAsync(page, "Moved to Done, position 1 of 1.");

            using var body = JsonDocument.Parse(Assert.Single(Moves(api)).Body ?? "{}");

            Assert.Equal(StatusOf(api, Northwind, Atlas, "Done"), body.RootElement.GetProperty("statusId").GetGuid());
        }
    }

    /// <summary>
    /// AC14, Q2 (a), UX-DR39 - a resize from 1280px to 375px with focus on a Task in the third column selects the third tab
    /// and keeps focus on that Task, reading nothing; going wide from a tab drops the tablist, moves focus to the visible
    /// column's card, and keeps every loaded card.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR39")]
    [Trait("Requirement", "AD-29")]
    public async Task Crossing_768px_keeps_focus_on_the_Task_and_reads_nothing()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Alpha"], ["Bravo"], ["Charlie"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);
            await WaitForKeysAsync(page);
            await FocusCardAsync(page, "Charlie");
            await page.EvaluateAsync(RecordAnnouncements);

            var requests = api.Requests.Count;

            await page.SetViewportSizeAsync(375, 800);
            await WaitForPagerAsync(page);
            await WaitForSelectedTabAsync(page, "Done");
            await WaitForFocusAsync(page, "Charlie");

            Assert.True(await CardNamed(page, "Charlie").IsVisibleAsync());

            // The Board's own change of tab says nothing (AC10).
            Assert.Empty(await SaidAsync(page));

            await TabNamed(page, "Done").FocusAsync();
            await PressOnPagerAsync(page, "ArrowLeft", "Doing");

            await page.SetViewportSizeAsync(1280, 800);
            await page.Locator(Tablist).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = WaitMs });
            await WaitForFocusAsync(page, "Bravo");

            Assert.Equal(3, await page.Locator(".task-card").CountAsync());
            Assert.Equal(0, await page.Locator($"{Columns}[hidden]").CountAsync());

            foreach (var title in new[] { "Alpha", "Bravo", "Charlie" })
            {
                Assert.True(await CardNamed(page, title).IsVisibleAsync(), $"{title} is not visible at 1280px.");
            }

            Assert.Equal(requests, api.Requests.Count);
        }
    }

    /// <summary>
    /// AC14, AC11 - going narrow with focus in the create field of the first column, while the active card is in the third,
    /// shows the column holding focus: focus stays in the field, and every other column is hidden and out of view.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR39")]
    public async Task Crossing_768px_shows_the_column_holding_focus_even_off_a_stop()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Alpha"], ["Bravo"], ["Charlie"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);
            await WaitForKeysAsync(page);
            await FocusCardAsync(page, "Charlie");

            await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = AddTask, Exact = true }).ClickAsync(new LocatorClickOptions { Timeout = WaitMs });

            var field = ColumnNamed(page, "Todo").Locator("input[type='text']");

            await page.WaitForFunctionAsync("() => document.activeElement && document.activeElement.matches(\"main .board-column input[type='text']\")", null, new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });

            await page.SetViewportSizeAsync(375, 800);
            await WaitForPagerAsync(page);
            await WaitForSelectedTabAsync(page, "Todo");

            Assert.True(await field.IsVisibleAsync(), "The create field is not on screen after going narrow.");
            Assert.True(await field.EvaluateAsync<bool>("f => document.activeElement === f"), "Focus left the create field.");
            Assert.False(await ColumnNamed(page, "Done").IsVisibleAsync(), "The active card's column is still drawn.");
            Assert.Equal(1, await page.GetByRole(AriaRole.Tabpanel).CountAsync());
        }
    }

    /// <summary>
    /// AC14, Q2 (a) - a keyboard carry heading for another column goes on when the viewport goes narrow, back in its own
    /// column, on the tab the pager shows; Esc then returns the Task with nothing sent.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR39")]
    [Trait("Requirement", "UX-DR24")]
    public async Task Crossing_768px_during_a_carry_keeps_it_in_its_own_column()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Alpha", "Bravo"], ["Charlie"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);
            await WaitForKeysAsync(page);
            await page.EvaluateAsync(RecordAnnouncements);
            await FocusCardAsync(page, "Alpha");

            await page.Keyboard.PressAsync(" ");
            await page.Locator(".task-card--carried").WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            await page.Keyboard.PressAsync("ArrowRight");
            await WaitUntilSaidAsync(page, "Doing, position");

            await page.SetViewportSizeAsync(375, 800);
            await WaitForPagerAsync(page);
            await WaitForSelectedTabAsync(page, "Todo");

            Assert.True(await page.Locator(".task-card--carried").IsVisibleAsync(), "The carry did not go on in its own column.");
            Assert.Equal(0, await ColumnNamed(page, "Doing").Locator(".drop-zone").CountAsync());

            await WaitForFocusAsync(page, "Alpha");
            await page.Keyboard.PressAsync("Escape");
            await WaitUntilSaidAsync(page, "Returned to Todo, position 1.");

            Assert.Empty(Moves(api));
            Assert.Equal(["Alpha", "Bravo"], await TitlesInAsync(page, "Todo"));
        }
    }

    /// <summary>
    /// AC9, AC10 - a click on a tab selects it and says its Status and true total; <c>Tab</c> from the tablist reaches the
    /// visible column's one roving stop, and <c>Shift+Tab</c> comes back to the selected tab. Nothing is said on load.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_clicked_tab_is_said_and_Tab_reaches_the_visible_column()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Alpha"], ["Bravo", "Charlie"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenNarrowAsync(api, page, 375, 667);

            Assert.DoesNotContain(
                await page.EvaluateAsync<string[]>("() => [...document.querySelectorAll('[aria-live=\"polite\"]')].map(r => r.textContent.trim())"),
                said => said.Contains("Todo,", StringComparison.Ordinal));

            await page.EvaluateAsync(RecordAnnouncements);
            await TabNamed(page, "Doing").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await WaitForSelectedTabAsync(page, "Doing");
            await WaitUntilSaidAsync(page, "Doing, 2 Tasks.");

            await page.WaitForFunctionAsync("() => document.activeElement && document.activeElement.matches(\"[role='tab'][aria-selected='true']\")", null, new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });
            await PressAndFocusAsync(page, "Tab", "Bravo");
            await page.Keyboard.PressAsync("Shift+Tab");
            await page.WaitForFunctionAsync("() => document.activeElement && document.activeElement.matches(\"[role='tab'][aria-selected='true']\")", null, new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });

            Assert.Equal("Doing", await SelectedTabNameAsync(page));
        }
    }

    /// <summary>
    /// AC9, AC12 - a tab chosen while a Task is carried ends the carry: the Task is returned where it was and said so, nothing
    /// is sent, and no carried card is left in the hidden column.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR24")]
    public async Task A_tab_chosen_during_a_carry_returns_the_Task()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Alpha", "Bravo"], ["Charlie"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenNarrowAsync(api, page, 375, 667);
            await WaitForKeysAsync(page);
            await page.EvaluateAsync(RecordAnnouncements);
            await FocusCardAsync(page, "Alpha");

            await page.Keyboard.PressAsync(" ");
            await page.Locator(".task-card--carried").WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            await TabNamed(page, "Doing").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await WaitForSelectedTabAsync(page, "Doing");
            await WaitUntilSaidAsync(page, "Returned to Todo, position 1.");

            Assert.Equal(0, await page.Locator(".task-card--carried").CountAsync());
            Assert.Empty(Moves(api));
        }
    }

    /// <summary>
    /// AC15 - on an empty Board with another tab selected, the empty state's create action shows the first tab, where the
    /// create field is drawn, and focus lands in it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task On_an_empty_Board_the_create_action_shows_the_first_tab()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(375, 667);
            await api.InstallAsync(page);
            await page.GotoAsync(new Uri(surface.BaseAddress, $"spaces/{Northwind:D}/projects/{Atlas:D}").ToString());
            await WaitForPagerAsync(page);

            await TabNamed(page, "Doing").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await WaitForSelectedTabAsync(page, "Doing");
            await page.Locator(".board__empty-action").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await WaitForSelectedTabAsync(page, "Todo");

            var field = ColumnNamed(page, "Todo").Locator("input[type='text']");

            await page.WaitForFunctionAsync("() => document.activeElement && document.activeElement.matches(\"main .board-column input[type='text']\")", null, new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });

            Assert.True(await field.IsVisibleAsync(), "The create field is in a hidden column.");
        }
    }

    /// <summary>
    /// AC15, UX-DR27 - at 375px a Viewer sees the identical pager, which selects and says as a mover's does, and no create,
    /// actions or Move control - absent, not disabled.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    public async Task At_375px_a_Viewer_has_the_pager_and_no_mover_controls()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Harbour, Beacon, ThreeStatuses, ["Alpha"], ["Bravo", "Charlie"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(375, 667);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Harbour, Beacon);
            await WaitForPagerAsync(page);
            await page.EvaluateAsync(RecordAnnouncements);

            Assert.Equal(ThreeStatuses, await TabNamesAsync(page));

            await TabNamed(page, "Todo").FocusAsync();
            await PressOnPagerAsync(page, "ArrowRight", "Doing");
            await WaitUntilSaidAsync(page, "Doing, 2 Tasks.");

            Assert.Equal(0, await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = AddTask, Exact = true }).CountAsync());
            Assert.Equal(0, await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { NameRegex = new Regex("^Actions for ") }).CountAsync());
            Assert.Equal(0, await page.Locator(".task-card--movable").CountAsync());
            Assert.Equal(0, await page.Locator("main [disabled], main [aria-disabled='true']").CountAsync());
        }
    }

    /// <summary>
    /// AC15, Q7 (a) - at 375px a mover sees the create affordance on the first tab only, and a Task created there appears in
    /// the first column, on screen.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task At_375px_the_create_affordance_is_on_the_first_tab_and_creates_there()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Alpha"], ["Bravo"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenNarrowAsync(api, page, 375, 667);
            await page.EvaluateAsync(RecordAnnouncements);

            var add = page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = AddTask, Exact = true });

            Assert.True(await add.IsVisibleAsync(), "No create affordance on the first tab.");

            await TabNamed(page, "Doing").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await WaitForSelectedTabAsync(page, "Doing");

            Assert.Equal(0, await add.CountAsync());

            await TabNamed(page, "Todo").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await WaitForSelectedTabAsync(page, "Todo");
            await add.ClickAsync(new LocatorClickOptions { Timeout = WaitMs });

            var field = ColumnNamed(page, "Todo").Locator("input[type='text']");

            await field.FillAsync("Echo");
            await field.PressAsync("Enter");
            await WaitUntilSaidAsync(page, "Task added.");

            Assert.True(await CardNamed(page, "Echo").IsVisibleAsync());
            Assert.Equal(["Alpha", "Echo"], await TitlesInAsync(page, "Todo"));
            Assert.Equal("Todo", await SelectedTabNameAsync(page));
        }
    }

    /// <summary>
    /// AC6, AC16, Q8 (a), UX-DR9 - at 320×800 with a 50-character Space name the Role chip is visible with the accessible
    /// name it has at 1280px, "Switch Space" is clipped out of sight but still in the switcher's accessible name, the
    /// Project name is dropped, the bar wraps, and the page never scrolls sideways.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR9")]
    public async Task At_320px_the_bar_keeps_the_Role_chip_and_drops_the_switch_label_visually()
    {
        var api = TwoSpaces(Northwind, Harbour);
        api.Switcher[0] = (Northwind, LongSpaceName);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Alpha"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);

            var chip = page.Locator(RoleChip);
            var wideName = await AccessibleTextAsync(chip);

            await page.SetViewportSizeAsync(320, 800);
            await WaitForPagerAsync(page);

            var label = page.Locator("nav.context-bar .context-bar__switch-label");

            Assert.True(await chip.IsVisibleAsync());
            Assert.Equal(wideName, await AccessibleTextAsync(chip));
            Assert.NotEqual("none", await label.EvaluateAsync<string>("l => getComputedStyle(l).clipPath"));
            Assert.Equal(1, await page.Locator("nav.context-bar").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { NameRegex = new Regex("Switch Space") }).CountAsync());
            Assert.False(await page.Locator("nav.context-bar .context-bar__project").IsVisibleAsync());
            Assert.True(await page.Locator("nav.context-bar").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Sign out", Exact = true }).IsVisibleAsync());
            Assert.True(await page.Locator("nav.context-bar .context-bar__account").IsVisibleAsync());
            Assert.Equal(0, await page.Locator("nav.context-bar [aria-haspopup='menu']").CountAsync());
            Assert.Empty(await page.EvaluateAsync<string[]>(Reflow));
        }
    }

    /// <summary>
    /// AC7, AC18, Q10 (a), X-11 - the 1.4.10 audit: at 320×256 CSS px (1280×1024 at 400%) and at 375×667, on a Board of
    /// three Statuses holding long titles, four long Labels and a due date, in every tab and then with the Task detail panel
    /// open, nothing needs two-dimensional scrolling - the page never scrolls sideways and nothing in it does either; the
    /// tablist wraps.
    /// </summary>
    /// <param name="width">The CSS viewport width.</param>
    /// <param name="height">The CSS viewport height.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(320, 256)]
    [InlineData(375, 667)]
    [Trait("Priority", "P1")]
    public async Task The_reflow_audit_finds_no_two_dimensional_scrolling(int width, int height)
    {
        var api = TwoSpaces(Northwind, Harbour);
        var labels = Enumerable.Range(0, 4).Select(index => new FakeLabel(Guid.NewGuid(), $"A long Label name number {index}", LabelColours.Keys[0])).ToList();
        const string longTitle = "Prepare the quarterly planning document for the infrastructure review board";
        var tasks = Seed(api, Northwind, Atlas, ["In Progress for a long while", "Waiting on someone outside the team", "Done"], [longTitle, "Short one"], ["Second column card"], ["Third column card"]);

        api.Labels[Northwind] = labels;

        var board = api.Boards[(Northwind, Atlas)];
        var first = board.Columns[0];

        first.Tasks[0] = first.Tasks[0] with { DueDate = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero), LabelIds = [.. labels.Select(label => label.Id)] };

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenNarrowAsync(api, page, width, height);

            foreach (var status in await TabNamesAsync(page))
            {
                await TabNamed(page, status).ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
                await WaitForSelectedTabAsync(page, status);

                var problems = await page.EvaluateAsync<string[]>(Reflow);

                Assert.True(problems.Length == 0, $"{status} at {width}×{height}: {string.Join("; ", problems)}");
            }

            // The tablist wraps rather than scrolling: its tabs sit on more than one line at this width, inside it.
            Assert.True(await page.Locator(Tablist).EvaluateAsync<bool>("l => l.scrollWidth <= l.clientWidth + 1"), "The tablist overflows sideways.");

            await TabNamed(page, "In Progress for a long while").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await page.Locator($".task-card button[data-task-id='{tasks[longTitle].Id:D}']").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            var inPanel = await page.EvaluateAsync<string[]>(Reflow);

            Assert.True(inPanel.Length == 0, $"The panel at {width}×{height}: {string.Join("; ", inPanel)}");
        }
    }

    /// <summary>
    /// AC20, UX-DR31, UX-DR33, Q2 (a) - the selected tab is Space-scoped and never stored: with the second tab selected, no
    /// Web Storage holds anything and neither the address nor the title carries the Status; after switching Space, the
    /// other Space's Board starts on its first tab.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR31")]
    public async Task The_selected_tab_is_never_stored_and_a_Board_opened_again_starts_on_the_first_tab()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Alpha"], ["Bravo"]);
        var doing = StatusOf(api, Northwind, Atlas, "Doing");
        _ = Seed(api, Harbour, Beacon, ThreeStatuses, ["Charlie"], ["Delta"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenNarrowAsync(api, page, 375, 667);

            await TabNamed(page, "Doing").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await WaitForSelectedTabAsync(page, "Doing");

            Assert.Equal(0, await page.EvaluateAsync<int>("() => localStorage.length + sessionStorage.length"));
            Assert.Equal(0, await page.EvaluateAsync<int>("async () => (await indexedDB.databases()).length"));
            Assert.DoesNotContain(doing.ToString("D"), page.Url, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(doing.ToString("N"), page.Url, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Doing", page.Url, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Doing", await page.TitleAsync(), StringComparison.OrdinalIgnoreCase);

            var trigger = page.Locator("nav.context-bar button[aria-controls]");
            var list = await trigger.GetAttributeAsync("aria-controls");

            await trigger.ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await page.Locator($"[id='{list}'] a", new PageLocatorOptions { HasText = "Harbour Ops" }).ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await page.Locator("nav.context-bar", new PageLocatorOptions { HasText = "Harbour Ops" }).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            await OpenBoardAsync(page, surface.BaseAddress, Harbour, Beacon);
            await WaitForPagerAsync(page);

            Assert.Equal("Todo", await SelectedTabNameAsync(page));
            Assert.True(await CardNamed(page, "Charlie").IsVisibleAsync());
            Assert.Equal(0, await page.EvaluateAsync<int>("() => localStorage.length + sessionStorage.length"));
        }
    }

    /// <summary>
    /// UX-DR20 - in every browser locale the pager renders over the three Statuses and its accessible name is the copy.
    /// </summary>
    /// <param name="locale">The browser locale.</param>
    /// <returns>A task.</returns>
    [Theory]
    [MemberData(nameof(BrowserLocales.All), MemberType = typeof(BrowserLocales))]
    public async Task The_pager_renders_in_every_browser_locale(string locale)
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Alpha"], ["Bravo"], ["Charlie"]);
        var (context, page) = await surface.OpenAsync(locale);

        await using (context)
        {
            await OpenNarrowAsync(api, page, 375, 667);

            Assert.Equal("Statuses", await page.Locator(Tablist).GetAttributeAsync("aria-label"));
            Assert.Equal(ThreeStatuses, await TabNamesAsync(page));
        }
    }

    private static ILocator TabNamed(IPage page, string status) =>
        page.Locator(Tab, new PageLocatorOptions { Has = page.Locator(".status-pager__name", new PageLocatorOptions { HasTextRegex = new Regex("^" + Regex.Escape(status) + "$") }) });

    private static ILocator ActionsFor(IPage page, string title) =>
        page.Locator(".task-card").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = $"Actions for {title}", Exact = true });

    private static async Task<string[]> TabNamesAsync(IPage page) =>
        [.. (await page.Locator($"{Tab} .status-pager__name").AllTextContentsAsync()).Select(name => name.Trim())];

    private static Task<string> SelectedTabNameAsync(IPage page) =>
        page.EvaluateAsync<string>("() => document.querySelector(\"main [role='tab'][aria-selected='true'] .status-pager__name\").textContent.trim()");

    private static Task<string> AccessibleTextAsync(ILocator locator) =>
        locator.EvaluateAsync<string>("e => (e.getAttribute('aria-label') || e.textContent).trim()");

    private static Task<bool> InViewAsync(IPage page, string status) =>
        ColumnNamed(page, status).EvaluateAsync<bool>("c => { const r = c.getBoundingClientRect(); return r.left >= 0 && r.right <= window.innerWidth; }");

    /// <summary>
    /// Waits until the tab selected is <paramref name="status"/>'s.
    /// </summary>
    private static Task WaitForSelectedTabAsync(IPage page, string status) =>
        page.WaitForFunctionAsync(
            "status => { const tab = document.querySelector(\"main [role='tab'][aria-selected='true'] .status-pager__name\"); return !!tab && tab.textContent.trim() === status; }",
            status,
            new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });

    /// <summary>
    /// Presses a key on the focused tab and waits until <paramref name="status"/>'s tab is selected and focused.
    /// </summary>
    private static async Task PressOnPagerAsync(IPage page, string key, string status)
    {
        await page.Keyboard.PressAsync(key);
        await WaitForSelectedTabAsync(page, status);
        await page.WaitForFunctionAsync(
            "status => { const a = document.activeElement; return !!a && a.matches(\"[role='tab']\") && a.querySelector('.status-pager__name').textContent.trim() === status; }",
            status,
            new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });
    }

    /// <summary>
    /// Waits for the tablist: the keys module has measured the viewport and told the Board it is narrow.
    /// </summary>
    private static Task WaitForPagerAsync(IPage page) =>
        page.Locator(Tablist).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

    /// <summary>
    /// Opens Northwind's Atlas Board at a narrow viewport and waits for the pager.
    /// </summary>
    private async Task OpenNarrowAsync(FakeYelloApi api, IPage page, int width, int height)
    {
        await page.SetViewportSizeAsync(width, height);
        await api.InstallAsync(page);
        await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);
        await WaitForPagerAsync(page);
    }
}
