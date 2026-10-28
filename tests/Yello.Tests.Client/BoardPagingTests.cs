using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;
using static Yello.Tests.Client.DragHarness;
using static Yello.Tests.Client.KeyboardHarness;

namespace Yello.Tests.Client;

/// <summary>
/// Story 2.9 in a real browser, PR tier: each column is a contiguous loaded prefix read 50 at a time, appended and never
/// recycled; focus follows the Task's identity across a page boundary and the keyboard drives the window; every route -
/// <c>↓</c>, a carried <c>↓</c>, "Show more", the Move picker - reaches the next page; the true total reaches the eye and
/// assistive technology; nothing is announced on loading; and a Task with no loaded card sends focus to its column.
/// AC3-AC7, AC10-AC12, AC14 (the drag's per-frame half); test design X-5 (P0), X-6 (P1); UX-DR24, UX-DR25, UX-DR26,
/// UX-DR38, UX-DR39; AD-29. Written red by the story 2.9 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>The API is <see cref="FakeYelloApi"/> with <see cref="FakeYelloApi.PageSize"/> set to 50</b>: the Board answers each
/// column's first page with <c>nextAfter</c>, and <c>GET …/board/columns/{statusId}?after={key}</c> the next (Q1 (a)). A page
/// can be held (<see cref="FakeYelloApi.HoldColumnPageAsync"/>) to assert what the Board does while it is out - hold,
/// assert, release, assert. No new fixture, no container.
/// </para>
/// <para>
/// <b>The markup and copy built</b> (story Q5 (a), Q7 (a), Task 5): a "Show more Tasks" button at a partly loaded column's
/// foot whose accessible name starts with that text (<see cref="ShowMoreName"/>); the Move picker's position list ends, for
/// a partly loaded column, with an option starting "Show more" (<see cref="MorePositionsName"/>); "Bottom of column" only
/// when the column is fully loaded.
/// </para>
/// <para>
/// <b>Why append and never recycle</b> (AC3): a recycled row re-points keyboard focus at a different Task, so the next
/// keyboard move operates on the wrong one - data-corrupting, reachable only by keyboard, invisible to pointer testing.
/// These cases hold the element identity itself: an element is tagged with its Task's id before a page arrives and must
/// still be that Task's after.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AD-29")]
[Trait("Requirement", "UX-DR24")]
[Trait("Requirement", "NFR-9")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class BoardPagingTests(ClientSurfaceFixture surface)
{
    private const int PageSize = 50;

    private const string BoardLoaded = "Board loaded.";

    private static readonly Regex ShowMoreName = new("^Show more Tasks", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly Regex MorePositionsName = new("^Show more", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();
    private static readonly Guid Beacon = Guid.NewGuid();

    /// <summary>
    /// X-5 (P0; AC7, AC11, AC6) - focus on the last loaded card, <c>↓</c> fetches the next page; while it is out, focus stays
    /// on that card and a second <c>↓</c> is ignored, never queued past the boundary; once it lands, focus is on Task 51 by
    /// identity, the element that held Task 50 is still Task 50's, 100 cards are drawn, and nothing was said.
    /// </summary>
    /// <remarks>Plant owed by the dev phase (story Task 12): render a page by replacing the list, or key cards by index.</remarks>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Down_on_the_last_loaded_card_loads_the_next_page_and_focus_lands_on_the_next_Task_by_identity()
    {
        var api = PagedSpaces();
        var tasks = Seed(api, Northwind, Atlas, ThreeStatuses, Many(120));
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(RecordAnnouncements);
            await FocusCardAsync(page, "Card 50");
            await page.EvaluateAsync("() => { window.__was = document.activeElement; }");

            var held = api.HoldColumnPageAsync();

            await page.Keyboard.PressAsync("ArrowDown");
            await held.WaitAsync(TimeSpan.FromMilliseconds(WaitMs), TestContext.Current.CancellationToken);

            // While the page is out: the keyboard drives the window, never the reverse.
            await page.Keyboard.PressAsync("ArrowDown");
            Assert.Equal("Card 50", await FocusedAsync(page));

            api.ReleaseColumnPage();

            await WaitForFocusAsync(page, "Card 51");
            await WaitForCardCountAsync(page, "Todo", 2 * PageSize);

            var request = Assert.Single(api.ColumnPageRequests);

            Assert.Equal(FakeYelloApi.ColumnPathOf(Northwind, Atlas, StatusOf(api, Northwind, Atlas, "Todo")), request.Path);
            Assert.Contains("after=" + Uri.EscapeDataString(tasks["Card 50"].PositionKey), request.Query, StringComparison.Ordinal);
            Assert.True(
                await page.EvaluateAsync<bool>(
                    "id => window.__was.isConnected && window.__was.closest('.task-card').querySelector('[data-task-id]').dataset.taskId === id",
                    tasks["Card 50"].Id.ToString("D")),
                "The element that held Task 50 now represents another Task, or was replaced (AC3, AC7).");
            Assert.Empty(await SaidAsync(page));
        }
    }

    /// <summary>
    /// AC3 - "Show more" appends the next page: every card element drawn before it is still connected and still represents
    /// the same Task id, the first 50 in their places; no element is reused for another Task.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Paging_appends_cards_and_never_reuses_an_element_for_another_Task()
    {
        var api = PagedSpaces();
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, Many(120));
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await WaitForCardCountAsync(page, "Todo", PageSize);

            await page.EvaluateAsync(
                """
                () => {
                    window.__tagged = [...document.querySelectorAll('main .board-column')][0].querySelectorAll('li.task-card');
                    for (const card of window.__tagged) card.__yelloTask = card.querySelector('[data-task-id]').dataset.taskId;
                }
                """);

            await ShowMore(page, "Todo").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await WaitForCardCountAsync(page, "Todo", 2 * PageSize);

            var drift = await page.EvaluateAsync<string[]>(
                """
                () => {
                    const now = [...[...document.querySelectorAll('main .board-column')][0].querySelectorAll('li.task-card')];
                    return [...window.__tagged].flatMap((card, index) =>
                        !card.isConnected ? ['detached ' + card.__yelloTask]
                        : card.querySelector('[data-task-id]').dataset.taskId !== card.__yelloTask ? ['re-pointed ' + card.__yelloTask]
                        : now[index] !== card ? ['moved ' + card.__yelloTask]
                        : []);
                }
                """);

            Assert.Empty(drift);
        }
    }

    /// <summary>
    /// X-6 (P1; AC4, AC10) - with 150 of 5,000 loaded, every drawn card's <c>aria-posinset</c> is its true ordinal and its
    /// <c>aria-setsize</c> 5,000; the column is described by "5000 Tasks" and its chip shows 5000 - the eye and assistive
    /// technology agree. Extends 2.8's <c>Each_column_is_named_by_its_Status_and_described_by_its_true_total</c> to a paged
    /// column rather than duplicating it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "UX-DR25")]
    public async Task With_150_of_5000_loaded_every_card_states_its_true_ordinal_and_the_true_total()
    {
        var api = PagedSpaces();
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, Many(5_000));
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);

            for (var loaded = 2 * PageSize; loaded <= 3 * PageSize; loaded += PageSize)
            {
                await ShowMore(page, "Todo").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
                await WaitForCardCountAsync(page, "Todo", loaded);
            }

            var cards = await ColumnNamed(page, "Todo").Locator("li.task-card").EvaluateAllAsync<string[]>(
                "cards => cards.map(card => card.getAttribute('aria-posinset') + '/' + card.getAttribute('aria-setsize'))");
            var description = await ColumnNamed(page, "Todo").EvaluateAsync<string>(
                "section => document.getElementById(section.getAttribute('aria-describedby')).textContent.trim()");

            Assert.Equal(Enumerable.Range(1, 3 * PageSize).Select(ordinal => $"{ordinal}/5000"), cards);
            Assert.Equal("5000 Tasks", description);
            Assert.Equal("5000", await TotalOfAsync(page, "Todo"));
        }
    }

    /// <summary>
    /// AC6, UX-DR38 - nothing is announced when a page loads or the window grows: "Show more", and <c>↓</c> across a page
    /// boundary, say nothing (the cold load's one "Board loaded." is open question 8 (a), said before recording starts).
    /// </summary>
    /// <remarks>Plant owed by the dev phase (story Task 5): say "Loaded." on append - this case must catch it.</remarks>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR38")]
    public async Task Nothing_is_announced_when_a_page_loads_or_the_window_grows()
    {
        var api = PagedSpaces();
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, Many(200));
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = BoardLoaded })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = WaitMs });
            await page.EvaluateAsync(RecordAnnouncements);

            await ShowMore(page, "Todo").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await WaitForCardCountAsync(page, "Todo", 2 * PageSize);

            await FocusCardAsync(page, "Card 100");
            await PressAndFocusAsync(page, "ArrowDown", "Card 101");
            await WaitForCardCountAsync(page, "Todo", 3 * PageSize);

            Assert.Empty(await SaidAsync(page));
        }
    }

    /// <summary>
    /// AC10 - a Task created in a partly loaded column is counted at once and not drawn until paging reaches it: the chip
    /// moves to 121, "Task added." is said once, no card appears; paging to the end draws it last, exactly once.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_Task_created_in_a_partly_loaded_column_is_counted_but_not_drawn_until_paging_reaches_it()
    {
        var api = PagedSpaces();
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, Many(120));
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(RecordAnnouncements);

            await ColumnNamed(page, "Todo").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Add a Task", Exact = true }).ClickAsync();

            var field = ColumnNamed(page, "Todo").Locator("input[type='text']");

            await field.FillAsync("Created late");
            await field.PressAsync("Enter");
            await WaitUntilSaidAsync(page, "Task added.");

            Assert.Equal("121", await TotalOfAsync(page, "Todo"));
            Assert.Equal(0, await CardNamed(page, "Created late").CountAsync());
            Assert.Equal(PageSize, await ColumnNamed(page, "Todo").Locator("li.task-card").CountAsync());

            await ShowMore(page, "Todo").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await WaitForCardCountAsync(page, "Todo", 2 * PageSize);
            await ShowMore(page, "Todo").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await WaitForCardCountAsync(page, "Todo", 121);

            Assert.Equal("Created late", (await TitlesInAsync(page, "Todo"))[^1]);
            Assert.Single(await SaidAsync(page), said => said.Contains("Task added.", StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// AC10, AC5 (open question 4 (a)) - a page that returns a Task already drawn (another Session moved Card 10 below the
    /// cursor) moves that card to the page's position - newer read wins - and never draws it twice.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-28")]
    public async Task A_page_holding_a_Task_already_drawn_moves_its_card_and_never_draws_it_twice()
    {
        var api = PagedSpaces();
        var tasks = Seed(api, Northwind, Atlas, ThreeStatuses, Many(120));
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await WaitForCardCountAsync(page, "Todo", PageSize);

            // The element drawing Card 11, tagged: when Card 10's card moves, every card after it shifts up one place, and
            // an element keyed by its place rather than its Task would come to draw Card 12 (dev-phase addition - AC3's
            // plant "key cards by index" is caught here, where a page reorders, not where it only appends).
            await page.EvaluateAsync(
                "id => { window.__eleven = document.querySelector(`[data-task-id='${id}']`).closest('.task-card'); }",
                tasks["Card 11"].Id.ToString("D"));

            // Another Session moves Card 10 to directly after Card 60: a key between theirs, past the cursor.
            var column = api.Boards[(Northwind, Atlas)].Columns[0];
            var index = column.Tasks.FindIndex(task => task.Id == tasks["Card 10"].Id);

            column.Tasks[index] = tasks["Card 10"] with { PositionKey = tasks["Card 60"].PositionKey + "V" };

            await ShowMore(page, "Todo").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await page.WaitForFunctionAsync(
                "() => [...[...document.querySelectorAll('main .board-column')][0].querySelectorAll('.task-card__title')].some(title => title.textContent.trim() === 'Card 99')",
                null,
                new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });

            var titles = await TitlesInAsync(page, "Todo");

            Assert.Single(titles, title => title == "Card 10");
            Assert.Equal(titles.IndexOf("Card 60") + 1, titles.IndexOf("Card 10"));
            Assert.Equal(titles.Count, titles.Distinct(StringComparer.Ordinal).Count());
            Assert.True(
                await page.EvaluateAsync<bool>(
                    "id => window.__eleven.isConnected && window.__eleven.querySelector('[data-task-id]').dataset.taskId === id",
                    tasks["Card 11"].Id.ToString("D")),
                "The element that drew Card 11 now draws another Task, or was replaced (AC3).");
        }
    }

    /// <summary>
    /// AC11 - a carried Task stepped <c>↓</c> past the last loaded card fetches the next page first, then steps: while the
    /// page is out the drop zone stays last; once it lands the zone is before Card 52 (after Card 51).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-29")]
    public async Task A_carried_Task_stepped_down_past_the_last_loaded_card_loads_the_next_page_first()
    {
        var api = PagedSpaces();
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, Many(120));
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await FocusCardAsync(page, "Card 49");
            await page.Keyboard.PressAsync(" ");
            await page.Keyboard.PressAsync("ArrowDown");
            await WaitForDropZoneAsync(page, "Todo", before: null);

            var held = api.HoldColumnPageAsync();

            await page.Keyboard.PressAsync("ArrowDown");
            await held.WaitAsync(TimeSpan.FromMilliseconds(WaitMs), TestContext.Current.CancellationToken);
            await WaitForDropZoneAsync(page, "Todo", before: null);

            api.ReleaseColumnPage();

            await WaitForDropZoneAsync(page, "Todo", "Card 52");
            Assert.Single(api.ColumnPageRequests);

            await page.Keyboard.PressAsync("Escape");
        }
    }

    /// <summary>
    /// AC11, Q5 (a) - "Show more" at a partly loaded column's foot appends the next page and moves focus to its first Task;
    /// once the column is fully loaded the control is gone.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR26")]
    public async Task Show_more_appends_the_next_page_and_moves_focus_to_its_first_Task()
    {
        var api = PagedSpaces();
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, Many(120), ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);

            // A column that fits one page has no control.
            Assert.Equal(0, await ShowMore(page, "Doing").CountAsync());

            await ShowMore(page, "Todo").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await WaitForFocusAsync(page, "Card 51");
            await WaitForCardCountAsync(page, "Todo", 2 * PageSize);

            await ShowMore(page, "Todo").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await WaitForFocusAsync(page, "Card 101");
            await WaitForCardCountAsync(page, "Todo", 120);

            Assert.Equal(0, await ShowMore(page, "Todo").CountAsync());
        }
    }

    /// <summary>
    /// AC10 (story 2.9 code review: the clause had no case) - a move placed directly after a partly loaded column's last
    /// loaded card is admitted between the cursor and the next key, so the cursor advances to the moved Task's key: the card
    /// stays drawn last after admission, and the next page starts after it - Card 51 next, the moved Task never twice.
    /// </summary>
    /// <remarks>
    /// Planted (no cursor advance in <c>Relocate</c>): on admission the key sorts past the old cursor, the card is held out
    /// and disappears, and the case fails at the first count.
    /// </remarks>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-29")]
    public async Task A_move_placed_after_the_last_loaded_card_advances_the_cursor_and_stays_drawn()
    {
        var api = PagedSpaces();
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Mover"], Many(120));
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(RecordAnnouncements);

            await CardNamed(page, "Mover").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Actions for Mover", Exact = true }).ClickAsync();
            await page.Locator("[role='menu'] [role='menuitem']", new PageLocatorOptions { HasText = "Move" }).ClickAsync();
            await page.Locator(".move-picker [role='option']", new PageLocatorOptions { HasText = "Doing" }).ClickAsync();
            await page.Locator(".move-picker [role='option']", new PageLocatorOptions { HasText = "Position 51, after Card 50" }).ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await WaitUntilSaidAsync(page, "Moved to Doing, position 51 of 121.");

            var admitted = await TitlesInAsync(page, "Doing");

            Assert.Equal(PageSize + 1, admitted.Count);
            Assert.Equal("Mover", admitted[^1]);
            Assert.Equal("121", await TotalOfAsync(page, "Doing"));

            await ShowMore(page, "Doing").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await WaitForCardCountAsync(page, "Doing", (2 * PageSize) + 1);

            var titles = await TitlesInAsync(page, "Doing");

            Assert.Single(titles, title => title == "Mover");
            Assert.Equal("Card 51", titles[titles.IndexOf("Mover") + 1]);
            Assert.Equal(titles.Count, titles.Distinct(StringComparer.Ordinal).Count());
        }
    }

    /// <summary>
    /// AD-3, AR-10 (story 2.9 code review) - a column page answered <c>404</c> raises the Board's one refusal surface, as
    /// the Board's own read does, rather than leaving a control that does nothing.
    /// </summary>
    /// <remarks>Planted (the page's <c>404</c> swallowed): nothing is shown and the case fails waiting for the copy.</remarks>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-10")]
    public async Task A_column_page_answered_404_raises_the_Boards_refusal_surface()
    {
        var api = PagedSpaces();
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, Many(120));
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);

            // The Project is gone by the time the next page is asked for: the route answers 404 not-available.
            Assert.True(api.Boards.Remove((Northwind, Atlas)));

            await ShowMore(page, "Todo").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await page.GetByText("Not available.").WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            Assert.Single(api.ColumnPageRequests);
        }
    }

    /// <summary>
    /// AC11, UX-DR27 - "Show more" is a read and every Role has it: a Viewer pages a column as a mover does.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Priority", "P1")]
    public async Task A_Viewer_pages_a_column_too()
    {
        var api = PagedSpaces();
        _ = Seed(api, Harbour, Beacon, ThreeStatuses, Many(80));
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Harbour, Beacon);

            await ShowMore(page, "Todo").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await WaitForCardCountAsync(page, "Todo", 80);

            // Fully loaded now: the control is gone for the Viewer too (dev-phase addition: the scaffold asserted nothing).
            Assert.Equal(0, await ShowMore(page, "Todo").CountAsync());
        }
    }

    /// <summary>
    /// AC11 - the Move picker's position list for a partly loaded column ends with an option that loads the next page into
    /// the Board's own column state and extends the list; "Bottom of column" is offered only once the column is fully
    /// loaded, because <c>afterTaskId</c> must name a concrete neighbour.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-29")]
    public async Task The_Move_picker_extends_its_positions_page_by_page_and_offers_Bottom_only_when_fully_loaded()
    {
        var api = PagedSpaces();
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Mover"], Many(120));
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);

            await CardNamed(page, "Mover").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Actions for Mover", Exact = true }).ClickAsync();
            await page.Locator("[role='menu'] [role='menuitem']", new PageLocatorOptions { HasText = "Move" }).ClickAsync();
            await page.Locator(".move-picker [role='option']", new PageLocatorOptions { HasText = "Doing" }).ClickAsync();
            await page.Locator(".move-picker [role='option']", new PageLocatorOptions { HasText = "Top of column" }).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            var options = page.Locator(".move-picker [role='option']");

            Assert.Equal(0, await options.Filter(new LocatorFilterOptions { HasText = "Bottom of column" }).CountAsync());
            Assert.Equal(PageSize, await options.Filter(new LocatorFilterOptions { HasTextRegex = new Regex("^Position ", RegexOptions.None, TimeSpan.FromSeconds(1)) }).CountAsync());

            await page.GetByRole(AriaRole.Option, new PageGetByRoleOptions { NameRegex = MorePositionsName }).ClickAsync();
            await WaitForCardCountAsync(page, "Doing", 2 * PageSize);
            await page.GetByRole(AriaRole.Option, new PageGetByRoleOptions { NameRegex = MorePositionsName }).ClickAsync();
            await WaitForCardCountAsync(page, "Doing", 120);

            await options.Filter(new LocatorFilterOptions { HasText = "Bottom of column" }).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            Assert.Equal(0, await page.GetByRole(AriaRole.Option, new PageGetByRoleOptions { NameRegex = MorePositionsName }).CountAsync());
            Assert.Equal(2, api.ColumnPageRequests.Count);
        }
    }

    /// <summary>
    /// AC12, UX-DR39 (open question 7 (a)) - the panel's Task changed to a Status whose column is partly loaded is held out
    /// past the cursor, so it has no card; closing the panel moves focus to that column's <c>&lt;section&gt;</c>, never to
    /// <c>&lt;body&gt;</c>.
    /// </summary>
    /// <remarks>Plant owed by the dev phase (story Task 6): no focus request - focus lands on <c>body</c> and this fails.</remarks>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR39")]
    public async Task Closing_the_panel_on_a_Task_held_out_past_the_cursor_focuses_its_column()
    {
        var api = PagedSpaces();
        var tasks = Seed(api, Northwind, Atlas, ThreeStatuses, ["Alpha", "Bravo"], Many(120));
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks["Alpha"].Id);
            await ChooseStatusAsync(page, "Doing");
            await WaitUntilTotalAsync(page, "Doing", "121");

            await page.Keyboard.PressAsync("Escape");
            await page.Locator("[role='dialog']").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = WaitMs });

            await WaitForFocusAsync(page, "column:Doing");
            Assert.Equal(0, await CardNamed(page, "Alpha").CountAsync());
        }
    }

    /// <summary>
    /// AC12, UX-DR39 - deleting, from the panel, a Task whose card is not loaded (held out past the cursor by a Status change)
    /// moves focus to the column that held it, never to <c>&lt;body&gt;</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR39")]
    public async Task Deleting_a_Task_whose_card_is_not_loaded_focuses_its_column()
    {
        var api = PagedSpaces();
        var tasks = Seed(api, Northwind, Atlas, ThreeStatuses, ["Alpha", "Bravo"], Many(120));
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks["Alpha"].Id);
            await ChooseStatusAsync(page, "Doing");
            await WaitUntilTotalAsync(page, "Doing", "121");

            var dialog = page.Locator("[role='dialog']");

            await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete Task", Exact = true }).ClickAsync();
            await dialog.Locator(".destructive-confirm .button-danger").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await dialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = WaitMs });

            await WaitForFocusAsync(page, "column:Doing");
            Assert.Equal("120", await TotalOfAsync(page, "Doing"));
        }
    }

    /// <summary>
    /// AC14 (the drag's half; <c>deferred-work.md:578</c>) - a lifted card whose pointer is still reads no card rect: across
    /// ten animation frames with no pointer movement and no auto-scroll, <c>getBoundingClientRect</c> is called zero times
    /// (the rects are cached at lift, on scroll, on auto-scroll and on append).
    /// </summary>
    /// <remarks>Plant owed by the dev phase (story Task 5): the per-frame <c>#hitTest</c> restored - the count grows and this fails.</remarks>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-3")]
    public async Task A_lifted_card_reads_no_card_rects_while_the_pointer_is_still()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, Many(200), ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(
                """
                () => {
                    window.__rects = 0;
                    const original = Element.prototype.getBoundingClientRect;
                    Element.prototype.getBoundingClientRect = function () { window.__rects++; return original.call(this); };
                }
                """);

            await LiftWithMouseAsync(page, "Card 05");
            await FramesAsync(page, 3);

            var before = await page.EvaluateAsync<int>("() => window.__rects");

            await FramesAsync(page, 10);

            var after = await page.EvaluateAsync<int>("() => window.__rects");

            await page.Mouse.UpAsync();

            Assert.True(after == before, $"A still pointer read {after - before} rects over ten frames (AC14, deferred-work.md:578).");
        }
    }

    private static FakeYelloApi PagedSpaces()
    {
        var api = TwoSpaces(Northwind, Harbour);

        api.PageSize = PageSize;

        return api;
    }

    private static ILocator ShowMore(IPage page, string status) =>
        ColumnNamed(page, status).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { NameRegex = ShowMoreName });

    private static Task WaitForCardCountAsync(IPage page, string status, int count) =>
        page.WaitForFunctionAsync(
            "([status, count]) => { const column = [...document.querySelectorAll('main .board-column')].find(c => c.querySelector('h2').textContent.trim() === status); return !!column && column.querySelectorAll('li.task-card').length === count; }",
            new object[] { status, count },
            new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });

    private static Task WaitUntilTotalAsync(IPage page, string status, string total) =>
        page.WaitForFunctionAsync(
            "([status, total]) => { const column = [...document.querySelectorAll('main .board-column')].find(c => c.querySelector('h2').textContent.trim() === status); return !!column && column.querySelector('.column-count').textContent.trim() === total; }",
            new object[] { status, total },
            new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });

    /// <summary>
    /// Waits for <paramref name="count"/> animation frames of the page's own - frame-driven, not a sleep.
    /// </summary>
    private static Task FramesAsync(IPage page, int count) =>
        page.EvaluateAsync("count => new Promise(resolve => { let n = 0; const tick = () => (++n >= count ? resolve() : requestAnimationFrame(tick)); requestAnimationFrame(tick); })", count);

    private static async Task OpenPanelAsync(IPage page, Guid taskId)
    {
        await page.Locator($".task-card button[data-task-id='{taskId:D}']").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
        await page.Locator("[role='dialog']").WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
    }

    private static async Task ChooseStatusAsync(IPage page, string status)
    {
        var dialog = page.Locator("[role='dialog']");

        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Status" }).ClickAsync();
        await dialog.Locator("[role='listbox'] [role='option']", new LocatorLocatorOptions { HasText = status }).ClickAsync();
    }

    private async Task OpenAsync(FakeYelloApi api, IPage page, Guid spaceId, Guid projectId)
    {
        await api.InstallAsync(page);
        await OpenBoardAsync(page, surface.BaseAddress, spaceId, projectId);
        await WaitForKeysAsync(page);
    }
}
