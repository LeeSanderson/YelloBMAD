using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;
using Yello.Contracts.Labels;
using static Yello.Tests.Client.DragHarness;
using static Yello.Tests.Client.KeyboardHarness;

namespace Yello.Tests.Client;

/// <summary>
/// Story 2.10 in a real browser, PR tier: the List View reached by the Board/List toggle, its rows, filters and sort, the
/// stated page size and "Show more Tasks", keyboard row traversal across a page boundary by Task identity, the true total
/// in <c>aria-rowcount</c> and the announced count, the empty filter, a Viewer's identical view, the purge on leaving the
/// Space, the refusal surface, reflow at 320px and under text spacing, and every browser locale. AC1, AC7-AC9, AC11,
/// AC16-AC20; FR-30, UX-DR19, UX-DR24, UX-DR26, UX-DR27, UX-DR31, UX-DR33, UX-DR35, UX-DR38-UX-DR40, AD-29; NFR-9.
/// Written red by the story 2.10 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>The API is <see cref="FakeYelloApi"/></b>, whose List route (story 2.10, Q1 (a)) serves the Board's Tasks as rows,
/// sorted and filtered as the Host will, 50 at a time with an opaque cursor; a page can be held
/// (<see cref="FakeYelloApi.HoldListPageAsync"/>) - hold, assert, release, assert. No new fixture, no container.
/// </para>
/// <para>
/// <b>Markup and copy assumed</b> (story Tasks 5, 6, Q2, Q6, Q8; correct each to what is built and record the correction in
/// the Debug Log): the client route <c>/spaces/{s}/projects/{p}/list</c>; a <c>nav</c> holding two links named "Board" and
/// "List", the current one <c>aria-current="page"</c>; a native <c>table</c> in <c>main</c> whose body rows are
/// <c>tr[data-task-id]</c>, one roving tab stop (<c>tabindex="0"</c>) inside the active row; Status and Label filters as
/// checkboxes named by the Status or Label; a sort <c>select</c> whose accessible name starts "Sort"; "Showing {loaded} of
/// {total}" under the table; a "Show more Tasks" button; the copy "List loaded.", "{n} Tasks match.", "1 Task matches.",
/// "No Tasks match." and "Nothing here yet." (Q11 (a)).
/// </para>
/// <para>
/// <b>Why append and never recycle</b> (AC18): a recycled row re-points keyboard focus at a different Task. These cases tag
/// an element with its Task's id before a page arrives and require it to be that Task's after.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-30")]
[Trait("Requirement", "UX-DR19")]
[Trait("Requirement", "AD-29")]
[Trait("Requirement", "NFR-9")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class ListViewTests(ClientSurfaceFixture surface)
{
    private const int PageSize = 50;

    private const string ListLoaded = "List loaded.";

    private const string NoTasksMatch = "No Tasks match.";

    private const string NotAvailableCopy = "Not available.";

    private const string Rows = "main table tbody tr[data-task-id]";

    private const string TextSpacingOverride =
        "* { line-height: 1.5 !important; letter-spacing: 0.12em !important; word-spacing: 0.16em !important; } "
        + "p { margin-block-end: 2em !important; }";

    private static readonly Regex ShowMoreName = new("^Show more Tasks", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly Regex SortName = new("^Sort", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly string[] ChipNames = ["Chip A", "Chip B", "Chip C", "Chip D", "Chip E"];

    private static readonly DateTimeOffset FirstDue = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();

    /// <summary>
    /// AC1, Q6 (a) - the Board's view toggle opens the List View at its own address, and the List's toggle returns to the
    /// Board; on each, <c>aria-current="page"</c> marks the current view and only it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1 - the List View page and the view toggle do not exist yet (story Task 5 (a), (b))")]
    public async Task The_toggle_opens_the_List_from_the_Board_and_returns_with_aria_current_on_each()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = SeedList(api, Northwind, Atlas, 12);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);

            Assert.Equal("page", await Toggle(page, "Board").GetAttributeAsync("aria-current"));
            Assert.Null(await Toggle(page, "List").GetAttributeAsync("aria-current"));

            await Toggle(page, "List").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await WaitForPathAsync(page, $"/spaces/{Northwind:D}/projects/{Atlas:D}/list");
            await page.Locator(Rows).First.WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            Assert.Equal("page", await Toggle(page, "List").GetAttributeAsync("aria-current"));
            Assert.Null(await Toggle(page, "Board").GetAttributeAsync("aria-current"));
            Assert.Equal(12, await page.Locator(Rows).CountAsync());

            await Toggle(page, "Board").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await WaitForPathAsync(page, $"/spaces/{Northwind:D}/projects/{Atlas:D}");
            await page.Locator(".task-card").First.WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
        }
    }

    /// <summary>
    /// AC20, UX-DR42, 2.3 Q4 - a row shows its title, its Status's name, its due date in the viewer's own zone (a Task due at
    /// midnight UTC on 3 November is due on 2 November in Los Angeles), or nothing when it has none, and at most three Label
    /// chips followed by "+N".
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC20 - the List View's rows do not exist yet (story Task 5 (c))")]
    public async Task A_row_shows_its_title_Status_due_date_in_the_viewers_zone_and_three_chips_then_plus_N()
    {
        var api = TwoSpaces(Northwind, Harbour);
        var labels = ChipNames.Select(name => new FakeLabel(Guid.NewGuid(), name, LabelColours.Keys[0])).ToList();
        var tasks = SeedList(api, Northwind, Atlas, 3);

        api.Labels[Northwind] = labels;
        Replace(api, tasks[0] with { DueDate = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero), LabelIds = [.. labels.Select(label => label.Id)] });
        Replace(api, tasks[1] with { DueDate = null, LabelIds = [] });

        var (context, page) = await surface.OpenAsync(EnglishLocale, "America/Los_Angeles");

        await using (context)
        {
            await OpenListAsync(api, page, Northwind, Atlas);

            var labelled = RowOf(page, tasks[0].Id);
            var bare = RowOf(page, tasks[1].Id);

            Assert.Contains(tasks[0].Title, await labelled.InnerTextAsync(), StringComparison.Ordinal);
            Assert.Contains("Todo", await labelled.InnerTextAsync(), StringComparison.Ordinal);
            Assert.Contains("2 Nov", await labelled.InnerTextAsync(), StringComparison.Ordinal);
            Assert.Equal(3, await labelled.Locator(".label-chip").CountAsync());
            Assert.Contains("+2", await labelled.InnerTextAsync(), StringComparison.Ordinal);
            Assert.Equal(0, await bare.Locator(".label-chip").CountAsync());
            Assert.DoesNotContain("Nov", await bare.InnerTextAsync(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC9, AC11, UX-DR38 - a deliberate filter change announces the true filtered total once, politely: checking the
    /// "Doing" Status says "{n} Tasks match." exactly once, and the stated count and <c>aria-rowcount</c> come from the same
    /// number.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - the List View's filters and count announcement do not exist yet (story Task 5 (c))")]
    public async Task A_filter_change_announces_the_true_count_once()
    {
        var api = TwoSpaces(Northwind, Harbour);
        var tasks = SeedList(api, Northwind, Atlas, 812);
        var doing = tasks.Count(task => task.StatusId == StatusOf(api, Northwind, Atlas, "Doing"));
        var sentence = string.Create(CultureInfo.InvariantCulture, $"{doing} Tasks match.");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenListAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(RecordAnnouncements);

            await page.GetByRole(AriaRole.Checkbox, new PageGetByRoleOptions { Name = "Doing", Exact = true }).CheckAsync(new LocatorCheckOptions { Timeout = WaitMs });
            await WaitUntilSaidAsync(page, sentence);
            await WaitForTextAsync(page, string.Create(CultureInfo.InvariantCulture, $"Showing {PageSize} of {doing}"));

            Assert.Single(await SaidAsync(page), said => said.Contains(sentence, StringComparison.Ordinal));
            Assert.Equal((doing + 1).ToString(CultureInfo.InvariantCulture), await page.Locator("main table").GetAttributeAsync("aria-rowcount"));
        }
    }

    /// <summary>
    /// AC8, AC9 - a filter matching nothing states "No Tasks match." and nothing else - no table header, no count, no
    /// suggestion to broaden - and that sentence is what is announced.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC8 - the List View's empty filter state does not exist yet (story Task 5 (c))")]
    public async Task A_filter_matching_nothing_states_No_Tasks_match_and_nothing_else()
    {
        var api = TwoSpaces(Northwind, Harbour);
        var unused = new FakeLabel(Guid.NewGuid(), "Unused", LabelColours.Keys[0]);

        _ = SeedList(api, Northwind, Atlas, 20);
        api.Labels[Northwind] = [unused];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenListAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(RecordAnnouncements);

            await page.GetByRole(AriaRole.Checkbox, new PageGetByRoleOptions { Name = unused.Name, Exact = true }).CheckAsync(new LocatorCheckOptions { Timeout = WaitMs });
            await WaitUntilSaidAsync(page, NoTasksMatch);
            await page.Locator("main table").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = WaitMs });

            var region = await page.GetByText(NoTasksMatch, new PageGetByTextOptions { Exact = true }).InnerTextAsync();

            Assert.Equal(NoTasksMatch, region.Trim());
            Assert.DoesNotContain("Showing", await page.Locator("main").InnerTextAsync(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC7, AC18, AC19, Q2 (a) - the page size is stated: "Showing 50 of 812"; "Show more Tasks" appends the next 50 -
    /// every row drawn before it still connected, still the same Task, in its place - the statement becomes "Showing 100 of
    /// 812", and nothing is announced.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC7 - the List View's paging does not exist yet (story Task 5 (c))")]
    public async Task Show_more_appends_rows_never_recycles_one_states_the_page_size_and_says_nothing()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = SeedList(api, Northwind, Atlas, 812);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenListAsync(api, page, Northwind, Atlas);
            await WaitForTextAsync(page, "Showing 50 of 812");
            await page.EvaluateAsync(RecordAnnouncements);
            await page.EvaluateAsync($"() => {{ window.__tagged = [...document.querySelectorAll('{Rows}')]; for (const row of window.__tagged) row.__yelloTask = row.dataset.taskId; }}");

            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { NameRegex = ShowMoreName }).ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await WaitForRowCountAsync(page, 2 * PageSize);
            await WaitForTextAsync(page, "Showing 100 of 812");

            var drift = await page.EvaluateAsync<string[]>(
                $$"""
                () => {
                    const now = [...document.querySelectorAll('{{Rows}}')];
                    return [...window.__tagged].flatMap((row, index) =>
                        !row.isConnected ? ['detached ' + row.__yelloTask]
                        : row.dataset.taskId !== row.__yelloTask ? ['re-pointed ' + row.__yelloTask]
                        : now[index] !== row ? ['moved ' + row.__yelloTask]
                        : []);
                }
                """);

            Assert.Empty(drift);
            Assert.Empty(await SaidAsync(page));
        }
    }

    /// <summary>
    /// AC7, AC18, UX-DR39 (X-5's shape; P0) - focus on the last loaded row, <c>↓</c> fetches the next page; while it is out,
    /// focus stays and a second <c>↓</c> is ignored; once it lands, focus is on Task 51 <b>by identity</b>, the element that
    /// held Task 50 is still Task 50's, 100 rows are drawn, and nothing was said.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC7 - the List View's keyboard row traversal does not exist yet (story Task 6)")]
    public async Task Down_on_the_last_loaded_row_loads_the_next_page_and_focus_lands_on_the_next_Task_by_identity()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = SeedList(api, Northwind, Atlas, 120);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenListAsync(api, page, Northwind, Atlas);
            await WaitForListKeysAsync(page);
            await page.EvaluateAsync(RecordAnnouncements);

            var fiftieth = await page.Locator(Rows).Nth(PageSize - 1).GetAttributeAsync("data-task-id");

            await page.Keyboard.PressAsync("End");
            await WaitForFocusedRowAsync(page, fiftieth!);
            await page.EvaluateAsync("() => { window.__was = document.activeElement.closest('tr[data-task-id]'); }");

            var held = api.HoldListPageAsync();

            await page.Keyboard.PressAsync("ArrowDown");
            await held.WaitAsync(TimeSpan.FromMilliseconds(WaitMs), TestContext.Current.CancellationToken);
            await page.Keyboard.PressAsync("ArrowDown");

            Assert.Equal(fiftieth, await FocusedRowAsync(page));

            api.ReleaseListPage();

            await WaitForRowCountAsync(page, 2 * PageSize);

            var fiftyFirst = await page.Locator(Rows).Nth(PageSize).GetAttributeAsync("data-task-id");

            await WaitForFocusedRowAsync(page, fiftyFirst!);

            Assert.Equal(2, api.ListRequests.Count);
            Assert.True(
                await page.EvaluateAsync<bool>("id => window.__was.isConnected && window.__was.dataset.taskId === id", fiftieth),
                "The row that held Task 50 now represents another Task, or was replaced (AC18).");
            Assert.Empty(await SaidAsync(page));
        }
    }

    /// <summary>
    /// AC11, X-6 - with 150 of 5,000 rows loaded, the table states <c>aria-rowcount</c> 5001 (the true total plus the header
    /// row) and each row's <c>aria-rowindex</c> is its true ordinal plus one.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC11 - the List View's table semantics do not exist yet (story Task 5 (c))")]
    public async Task With_150_of_5000_loaded_the_table_states_the_true_total_and_each_rows_true_ordinal()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = SeedList(api, Northwind, Atlas, 5_000);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenListAsync(api, page, Northwind, Atlas);

            for (var loaded = 2 * PageSize; loaded <= 3 * PageSize; loaded += PageSize)
            {
                await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { NameRegex = ShowMoreName }).ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
                await WaitForRowCountAsync(page, loaded);
            }

            var indexes = await page.EvaluateAsync<string[]>($"() => [...document.querySelectorAll('{Rows}')].map(row => row.getAttribute('aria-rowindex'))");

            Assert.Equal("5001", await page.Locator("main table").GetAttributeAsync("aria-rowcount"));
            Assert.Equal(Enumerable.Range(2, 3 * PageSize).Select(index => index.ToString(CultureInfo.InvariantCulture)), indexes);
        }
    }

    /// <summary>
    /// AC16, UX-DR27 - a Viewer's List View is a Member's: the same rows, filters, sort and paging, and no control anywhere
    /// in <c>main</c> is disabled.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC16 - the List View page does not exist yet (story Task 5)")]
    public async Task A_Viewer_gets_the_same_List_and_no_disabled_control()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = SeedList(api, Harbour, Atlas, 60);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenListAsync(api, page, Harbour, Atlas);
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            Assert.Equal(PageSize, await page.Locator(Rows).CountAsync());
            Assert.True(await page.GetByRole(AriaRole.Combobox, new PageGetByRoleOptions { NameRegex = SortName }).IsVisibleAsync());
            Assert.True(await page.GetByRole(AriaRole.Checkbox, new PageGetByRoleOptions { Name = "Doing", Exact = true }).IsVisibleAsync());
            Assert.True(await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { NameRegex = ShowMoreName }).IsVisibleAsync());
            Assert.Equal(0, await page.Locator("main [disabled], main [aria-disabled='true']").CountAsync());
        }
    }

    /// <summary>
    /// AC17, UX-DR31, <c>EXPERIENCE.md:164-165</c> - leaving the Space purges the filter, the sort and the rows synchronously
    /// before any render of the next Space: no frame ever holds a row of the old Space beside one of the new, the filter is
    /// unchecked on return, and nothing was written to <c>localStorage</c> or <c>sessionStorage</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC17 - the List View's purge does not exist yet (story Task 5 (c))")]
    [Trait("Requirement", "UX-DR31")]
    public async Task Leaving_the_Space_purges_the_filter_and_rows_before_any_render_and_stores_nothing()
    {
        var api = TwoSpaces(Northwind, Harbour);
        var theirs = SeedList(api, Northwind, Atlas, 30);
        _ = SeedList(api, Harbour, Atlas, 30);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenListAsync(api, page, Northwind, Atlas);

            var storedBefore = await page.EvaluateAsync<int>("() => localStorage.length + sessionStorage.length");

            await page.GetByRole(AriaRole.Checkbox, new PageGetByRoleOptions { Name = "Doing", Exact = true }).CheckAsync(new LocatorCheckOptions { Timeout = WaitMs });
            await page.EvaluateAsync(
                """
                ids => {
                    window.__mixed = 0;
                    const old = new Set(ids);
                    new MutationObserver(() => {
                        const rows = [...document.querySelectorAll('tr[data-task-id]')].map(row => row.dataset.taskId);
                        if (rows.some(id => old.has(id)) && rows.some(id => !old.has(id))) window.__mixed++;
                    }).observe(document.body, { childList: true, subtree: true });
                }
                """,
                theirs.Select(task => task.Id.ToString("D")).ToArray());

            await page.EvaluateAsync($"() => Blazor.navigateTo('/spaces/{Harbour:D}/projects/{Atlas:D}/list')");
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            await page.Locator(Rows).First.WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            Assert.Equal(0, await page.EvaluateAsync<int>("() => window.__mixed"));
            Assert.False(await page.GetByRole(AriaRole.Checkbox, new PageGetByRoleOptions { Name = "Doing", Exact = true }).IsCheckedAsync());
            Assert.Equal(storedBefore, await page.EvaluateAsync<int>("() => localStorage.length + sessionStorage.length"));
        }
    }

    /// <summary>
    /// AC17, UX-DR33 - filtering by a Label never puts its name, or a Status's name, in the address or the document title:
    /// routes carry ids only and the title stays fixed.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC17 - the List View's filters do not exist yet (story Task 5 (c))")]
    [Trait("Requirement", "UX-DR33")]
    public async Task No_Label_or_Status_name_reaches_the_address_or_the_title()
    {
        var api = TwoSpaces(Northwind, Harbour);
        var label = new FakeLabel(Guid.NewGuid(), "Confidential launch", LabelColours.Keys[0]);
        var tasks = SeedList(api, Northwind, Atlas, 10);

        api.Labels[Northwind] = [label];
        Replace(api, tasks[0] with { LabelIds = [label.Id] });

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenListAsync(api, page, Northwind, Atlas);

            var title = await page.TitleAsync();

            await page.GetByRole(AriaRole.Checkbox, new PageGetByRoleOptions { Name = label.Name, Exact = true }).CheckAsync(new LocatorCheckOptions { Timeout = WaitMs });
            await page.GetByRole(AriaRole.Checkbox, new PageGetByRoleOptions { Name = "Doing", Exact = true }).CheckAsync(new LocatorCheckOptions { Timeout = WaitMs });
            await WaitForTextAsync(page, NoTasksMatch);

            Assert.DoesNotContain("Confidential", page.Url, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Doing", page.Url, StringComparison.Ordinal);
            Assert.Equal(title, await page.TitleAsync());
        }
    }

    /// <summary>
    /// AD-3, Task 5 (c) - a page answered <c>404 not-available</c> (the Project went away) raises the refusal surface's "Not
    /// available." rather than appending nothing silently.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC12 - the List View's refusal handling does not exist yet (story Task 5 (c))")]
    public async Task A_page_answered_404_raises_the_refusal_surface()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = SeedList(api, Northwind, Atlas, 120);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenListAsync(api, page, Northwind, Atlas);

            _ = api.Boards.Remove((Northwind, Atlas));

            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { NameRegex = ShowMoreName }).ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await page.Locator("main").GetByText(NotAvailableCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
        }
    }

    /// <summary>
    /// AC20, Q10 (a), UX-DR40 (1.4.10, 1.4.12) - at a 320px viewport, and again under the text-spacing override, the List
    /// reflows each row into a stacked block: nothing in <c>main</c> needs horizontal scrolling.
    /// </summary>
    /// <param name="spacing">Whether the 1.4.12 text-spacing override is applied as well.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC20 - the List View's narrow layout does not exist yet (story Task 5 (c), Q10)")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "UX-DR40")]
    public async Task At_320px_no_row_needs_two_dimensional_scrolling(bool spacing)
    {
        var api = TwoSpaces(Northwind, Harbour);
        var labels = Enumerable.Range(0, 4).Select(index => new FakeLabel(Guid.NewGuid(), $"A long Label name {index}", LabelColours.Keys[0])).ToList();
        var tasks = SeedList(api, Northwind, Atlas, 8);

        api.Labels[Northwind] = labels;
        Replace(api, tasks[0] with { Title = "A Task title long enough to wrap across several lines at a narrow width", DueDate = FirstDue, LabelIds = [.. labels.Select(label => label.Id)] });

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(320, 800);
            await OpenListAsync(api, page, Northwind, Atlas);

            if (spacing)
            {
                await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = TextSpacingOverride });
            }

            var overflow = await page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth");

            Assert.True(overflow <= 0, $"The List at 320px scrolls sideways by {overflow}px (1.4.10).");
        }
    }

    /// <summary>
    /// AC19, UX-DR35 - in every browser locale the List renders its rows and, on the cold load, says "List loaded." - its
    /// one completion sentence - and its container is no longer <c>aria-busy</c>.
    /// </summary>
    /// <param name="locale">The browser locale.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC19 - the List View page does not exist yet (story Task 5 (c))")]
    [MemberData(nameof(BrowserLocales.All), MemberType = typeof(BrowserLocales))]
    [Trait("Requirement", "UX-DR35")]
    public async Task The_List_renders_and_says_List_loaded_in_every_browser_locale(string locale)
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = SeedList(api, Northwind, Atlas, 7);
        var (context, page) = await surface.OpenAsync(locale);

        await using (context)
        {
            await OpenListAsync(api, page, Northwind, Atlas);

            await page.WaitForFunctionAsync(
                "sentence => [...document.querySelectorAll('[aria-live=\"polite\"]')].some(region => region.textContent.includes(sentence))",
                ListLoaded,
                new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });

            Assert.Equal(7, await page.Locator(Rows).CountAsync());
            Assert.Equal(0, await page.Locator("main [aria-busy='true']").CountAsync());
        }
    }

    /// <summary>
    /// Q5 (a), Q7 (a), AC14, AC18 - the List opens sorted by Status (the Board's column order); choosing "Due date" replaces
    /// the rows wholesale - every old row element detached, none re-pointed at another Task - and the undated Tasks come
    /// last.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC18 - the List View's sort control does not exist yet (story Task 5 (c))")]
    public async Task Changing_the_sort_replaces_the_rows_and_Status_is_the_initial_order()
    {
        var api = TwoSpaces(Northwind, Harbour);
        var tasks = SeedList(api, Northwind, Atlas, 30);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenListAsync(api, page, Northwind, Atlas);

            var sort = page.GetByRole(AriaRole.Combobox, new PageGetByRoleOptions { NameRegex = SortName });
            var statusesInOrder = await page.EvaluateAsync<string[]>($"() => [...document.querySelectorAll('{Rows}')].map(row => row.dataset.taskId)");
            var columnOf = tasks.ToDictionary(task => task.Id.ToString("D"), task => task.StatusId, StringComparer.Ordinal);
            var order = api.Boards[(Northwind, Atlas)].Columns.Select(column => column.StatusId).ToList();

            Assert.Equal("Status", (await sort.Locator("option:checked").InnerTextAsync()).Trim());
            Assert.Equal(statusesInOrder.Select(id => order.IndexOf(columnOf[id])).Order(), statusesInOrder.Select(id => order.IndexOf(columnOf[id])));

            await page.EvaluateAsync($"() => {{ window.__tagged = [...document.querySelectorAll('{Rows}')]; for (const row of window.__tagged) row.__yelloTask = row.dataset.taskId; }}");
            await sort.SelectOptionAsync(new SelectOptionValue { Label = "Due date" });
            await page.WaitForFunctionAsync("() => window.__tagged.every(row => !row.isConnected)", null, new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });

            var rePointed = await page.EvaluateAsync<int>("() => window.__tagged.filter(row => row.dataset.taskId !== row.__yelloTask).length");
            var undatedLast = await page.EvaluateAsync<string[]>($"() => [...document.querySelectorAll('{Rows}')].map(row => row.dataset.taskId)");
            var dueOf = tasks.ToDictionary(task => task.Id.ToString("D"), task => task.DueDate, StringComparer.Ordinal);

            Assert.Equal(0, rePointed);
            Assert.Equal(undatedLast.Select(id => dueOf[id] is null).Order(), undatedLast.Select(id => dueOf[id] is null));
        }
    }

    /// <summary>
    /// The Project's Board in the fake with <paramref name="count"/> Tasks over three Statuses by ordinal (<c>i % 3</c>), due
    /// on one of seven days from 2026-11-02 at midnight UTC, every third one undated - the List's rows. Answers the Tasks in
    /// ordinal order.
    /// </summary>
    private static List<FakeTask> SeedList(FakeYelloApi api, Guid spaceId, Guid projectId, int count)
    {
        var columns = ThreeStatuses.Select(status => new FakeColumn(Guid.NewGuid(), status, 0, [])).ToList();
        var tasks = new List<FakeTask>(count);

        for (var ordinal = 1; ordinal <= count; ordinal++)
        {
            var column = columns[ordinal % 3];
            var task = new FakeTask(Guid.NewGuid(), string.Create(CultureInfo.InvariantCulture, $"Row {ordinal:D4}"), column.StatusId, string.Create(CultureInfo.InvariantCulture, $"a{column.Tasks.Count:D5}"), DateTimeOffset.UtcNow, FirstDue.AddDays(ordinal % 7), []);

            if (ordinal % 3 == 0)
            {
                task = task with { DueDate = null };
            }

            column.Tasks.Add(task);
            column.Total++;
            tasks.Add(task);
        }

        api.Boards[(spaceId, projectId)] = new FakeBoard(new FakeBoardProject(projectId, "Atlas"), columns);
        api.Projects[spaceId] = [new FakeProject(projectId, "Atlas", DateTimeOffset.UtcNow)];

        return tasks;
    }

    /// <summary>
    /// Replaces a seeded Task, matched by id, in whichever column of whichever Board holds it.
    /// </summary>
    private static void Replace(FakeYelloApi api, FakeTask task)
    {
        foreach (var column in api.Boards.Values.SelectMany(board => board.Columns))
        {
            var index = column.Tasks.FindIndex(candidate => candidate.Id == task.Id);

            if (index >= 0)
            {
                column.Tasks[index] = task;
            }
        }
    }

    private static ILocator Toggle(IPage page, string view) =>
        page.Locator("nav").GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = view, Exact = true });

    private static ILocator RowOf(IPage page, Guid taskId) =>
        page.Locator($"main table tbody tr[data-task-id='{taskId:D}']");

    private static Task WaitForRowCountAsync(IPage page, int count) =>
        page.WaitForFunctionAsync(
            "([rows, count]) => document.querySelectorAll(rows).length === count",
            new object[] { Rows, count },
            new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });

    private static Task WaitForTextAsync(IPage page, string text) =>
        page.Locator("main").GetByText(text).First.WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

    private static Task<string?> FocusedRowAsync(IPage page) =>
        page.EvaluateAsync<string?>("() => document.activeElement?.closest('tr[data-task-id]')?.dataset.taskId ?? null");

    private static Task WaitForFocusedRowAsync(IPage page, string taskId) =>
        page.WaitForFunctionAsync(
            "id => document.activeElement?.closest('tr[data-task-id]')?.dataset.taskId === id",
            taskId,
            new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });

    /// <summary>
    /// Waits until the List's keyboard module is attached: a cancelable <c>ArrowUp</c> on the rows' one tab stop - the first
    /// row, where <c>↑</c> moves nothing - is prevented. The Board's idiom (<see cref="KeyboardHarness.WaitForKeysAsync"/>).
    /// </summary>
    private static Task WaitForListKeysAsync(IPage page) =>
        page.WaitForFunctionAsync(
            """
            () => {
                const stop = document.querySelector('main table [tabindex="0"]');
                if (!stop) return false;
                stop.focus();
                return !stop.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowUp', bubbles: true, cancelable: true }));
            }
            """,
            null,
            new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });

    /// <summary>
    /// Polls the page's own <c>location.pathname</c> - never Playwright's URL wait (<c>TESTING-CONVENTIONS.md</c>).
    /// </summary>
    private static Task WaitForPathAsync(IPage page, string path) =>
        page.WaitForFunctionAsync("path => location.pathname === path", path, new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });

    private async Task OpenListAsync(FakeYelloApi api, IPage page, Guid spaceId, Guid projectId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(new Uri(surface.BaseAddress, $"spaces/{spaceId:D}/projects/{projectId:D}/list").ToString());
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
        await page.Locator(Rows).First.WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
    }
}
