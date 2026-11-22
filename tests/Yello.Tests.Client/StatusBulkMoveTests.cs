using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;
using Yello.Contracts;

namespace Yello.Tests.Client;

/// <summary>
/// Moving every Task of one Status into another Project, in a real browser: a "Move to Project" control in each non-empty
/// column's head and in a List View filtered to exactly one Status, absent for a Viewer; one accent-bordered bar drawn above
/// the columns or the rows, focused on appearance, naming its scope, holding the single move's Project picker with nothing
/// selected and the only Cancel; a commit that holds focus on "Moving {n} Tasks.", blocks only the source column and shows no progress;
/// focus on the emptied column, or the List's heading, when it settles; and every refusal said in the bar with nothing moved.
/// </summary>
/// <remarks>
/// <para>
/// <b>The API is <see cref="FakeYelloApi"/></b>, deciding the move in the Host's order (<c>FakeYelloApi.StatusBulkMove.cs</c>)
/// with a hold, so a case can assert the in-flight state; the Projects come from the Space's Project list and each
/// destination's set from its seeded Status read.
/// </para>
/// <para>
/// <b>The markup the cases read</b>, as recommended before it is built; correct each to what is built and record the
/// correction in the Debug Log: in a column's head (<c>.board-column__head</c>) a button whose text is "Move to Project" and whose
/// accessible name is "Move {n} Tasks in “{Status}” to another Project"; in the List View (<c>.task-list</c>) a button
/// "Move to Project" outside the table; one <c>.bulk-move-bar</c>, never inside a <c>.board-column</c>, holding a
/// <c>[role=status]</c> scope line, the <c>.project-move-picker</c> with its <c>[role=option]</c> rows, and a button
/// "Cancel"; the List View's heading is its <c>h1</c>.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-41")]
[Trait("Requirement", "UX-DR17")]
[Trait("Requirement", "UX-DR39")]
public sealed class StatusBulkMoveTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string RoleChip = "nav.context-bar .role-chip";

    private const string Column = "main .board-column";
    private const string ColumnHead = ".board-column__head";
    private const string Columns = "main .board__columns";
    private const string Card = ".task-card";
    private const string CardTitle = ".task-card__title";
    private const string Bar = ".bulk-move-bar";
    private const string BarOption = ".bulk-move-bar .project-move-picker [role='option']";
    private const string BarStatus = ".bulk-move-bar [role='status']";
    private const string LiveRegion = "[aria-live='polite']";
    private const string ListRows = "main table tbody tr[data-task-id]";
    private const string ListHeading = ".task-list h1";

    private const string MoveToProject = "Move to Project";
    private const string NotSentCopy = "Not sent.";
    private const string NothingMoved = "Nothing was moved";

    private static readonly Regex ColumnControlName = new("^Move \\d+ Tasks? in “.+” to another Project$", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly string[] StatusNames = ["Todo", "Doing", "Done"];

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();
    private static readonly Guid Beacon = Guid.NewGuid();
    private static readonly Guid Comet = Guid.NewGuid();
    private static readonly Guid Dune = Guid.NewGuid();

    private bool _installed;

    /// <summary>
    /// A Member is offered the column control on a non-empty column only, named for assistive technology by its count and
    /// Status; a Viewer is offered it nowhere - absent from the page, never disabled.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC10, AC15 - the column control is not built yet (Q8)")]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "FR-16")]
    public async Task The_column_control_is_offered_to_a_Member_on_a_non_empty_column_and_absent_for_a_Viewer()
    {
        var api = TwoSpaces(northwindRole: "member");
        _ = SeedSpace(api, Northwind, ["Draft the brief", "Book the room"], Atlas, Beacon);
        _ = SeedSpace(api, Harbour, ["Viewer's Task"], Dune, Guid.NewGuid());
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);

            Assert.Equal(1, await ColumnControl(page, "Todo").CountAsync());
            Assert.Equal("Move 2 Tasks in “Todo” to another Project", await ColumnControl(page, "Todo").GetAttributeAsync("aria-label"));
            Assert.Equal(0, await ColumnControl(page, "Doing").CountAsync());
            Assert.Equal(0, await ColumnControl(page, "Done").CountAsync());

            await OpenBoardAsync(api, page, Harbour, Dune);
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator(ColumnHead).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { NameRegex = ColumnControlName }).CountAsync());
            Assert.Equal(0, await page.Locator($"{ColumnHead} [disabled], {ColumnHead} [aria-disabled='true']").CountAsync());
        }
    }

    /// <summary>
    /// Initiating draws one bar above the columns, never inside one; focus is on it; its scope line is a status naming the
    /// count and the Status; it lists the other Projects with nothing selected and one tab stop. Opening it sends nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC10 - the bulk move bar is not built yet (Q12)")]
    [Trait("Requirement", "UX-DR21")]
    public async Task Initiating_draws_the_bar_above_the_columns_focused_and_naming_its_scope_with_nothing_selected()
    {
        var api = TwoSpaces();
        _ = SeedSpace(api, Northwind, ["Draft the brief", "Book the room"], Atlas, Beacon, Comet);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenBarAsync(page, "Todo");

            Assert.Equal(1, await page.Locator(Bar).CountAsync());
            Assert.Equal(0, await page.Locator($"{Column} {Bar}").CountAsync());
            Assert.True(
                await page.EvaluateAsync<bool>($"() => !!(document.querySelector('{Bar}').compareDocumentPosition(document.querySelector('{Columns}')) & Node.DOCUMENT_POSITION_FOLLOWING)"),
                "The bar is not drawn before the columns.");
            Assert.True(await FocusIsInAsync(page, Bar), "Focus is not on the bar when it appears.");
            Assert.Equal("2 Tasks in “Todo”.", (await page.Locator(BarStatus).InnerTextAsync()).Trim());
            Assert.Equal(["Beacon", "Comet"], await OptionsAsync(page));
            Assert.Equal(0, await page.Locator($"{Bar} [aria-selected='true'], {Bar} [aria-checked='true']").CountAsync());
            Assert.Equal(1, await page.Locator($"{BarOption}[tabindex='0']").CountAsync());
            Assert.Equal(1, await page.Locator(Bar).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Cancel", Exact = true }).CountAsync());
            Assert.Empty(api.StatusBulkMoves);
        }
    }

    /// <summary>
    /// A destination holding the Status commits on the Project choice. In flight the scope line reads "Moving 2 Tasks." and
    /// holds focus, nothing shows a percentage or a progress bar, Cancel is gone, only the source column is busy, and a second
    /// press sends nothing. On <c>200</c> the column is empty, focus is on it, and the announcement takes its count from the
    /// answer - three, since a third Task joined the Status after the bar named two.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC10 - the bulk move bar is not built yet (Q3)")]
    [Trait("Requirement", "UX-DR25")]
    [Trait("Requirement", "AR-20")]
    public async Task A_destination_holding_the_Status_commits_in_flight_without_progress_and_focuses_the_emptied_column()
    {
        var api = TwoSpaces();
        var tasks = SeedSpace(api, Northwind, ["Draft the brief", "Book the room"], Atlas, Beacon, Comet);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenBarAsync(page, "Todo");

            var held = api.HoldStatusBulkMoveAsync();

            AddTask(api, Northwind, Atlas, "Created meanwhile");
            await ChooseAsync(page, "Beacon");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            var line = page.Locator(BarStatus, new PageLocatorOptions { HasText = "Moving 2 Tasks." });

            await line.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.True(await line.EvaluateAsync<bool>("element => element === document.activeElement || element.contains(document.activeElement)"), "The in-flight line does not hold focus.");
            Assert.Equal(0, await page.Locator($"{Bar} progress, {Bar} [role='progressbar']").CountAsync());
            Assert.DoesNotContain("%", await page.Locator(Bar).InnerTextAsync(), StringComparison.Ordinal);
            Assert.Equal(0, await page.Locator(Bar).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Cancel", Exact = true }).CountAsync());
            Assert.Equal("true", await ColumnNamed(page, "Todo").GetAttributeAsync("aria-busy"));
            Assert.NotEqual("true", await ColumnNamed(page, "Doing").GetAttributeAsync("aria-busy"));
            Assert.Equal(0, await page.Locator("[inert]").CountAsync());
            Assert.Equal(["Draft the brief", "Book the room"], await TitlesInAsync(page, "Todo"));

            await page.Keyboard.PressAsync("Enter");
            await page.Keyboard.PressAsync("Escape");

            Assert.Single(api.StatusBulkMoves);

            api.ReleaseStatusBulkMove();

            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Moved 3 Tasks to Beacon." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });
            await page.Locator(Bar).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.Empty(await TitlesInAsync(page, "Todo"));
            Assert.Equal("Todo", await FocusedColumnAsync(page));

            var sent = Assert.Single(api.StatusBulkMoves);

            Assert.Equal(FakeYelloApi.StatusBulkMovePathOf(Northwind, Atlas, tasks["Draft the brief"].StatusId), sent.Path);
            Assert.NotNull(sent.Header(Idempotency.HeaderName));

            using var body = JsonDocument.Parse(sent.Body ?? "{}");

            Assert.Equal(Beacon, body.RootElement.GetProperty("projectId").GetGuid());
            Assert.False(body.RootElement.TryGetProperty("taskIds", out _), "The bulk move carried a list of Tasks.");
            Assert.False(body.RootElement.TryGetProperty("positionKey", out _), "The bulk move carried a position key.");
        }
    }

    /// <summary>
    /// A destination lacking the Status asks for one with nothing selected; choosing one commits it, and the announcement
    /// names the Status.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC10 - the bulk move bar is not built yet (Q5)")]
    [Trait("Requirement", "FR-26")]
    [Trait("Requirement", "AR-24")]
    public async Task A_destination_lacking_the_Status_asks_for_one_with_nothing_selected()
    {
        var api = TwoSpaces();
        _ = SeedSpace(api, Northwind, ["Draft the brief", "Book the room"], Atlas, Beacon, Comet);
        var doing = WithoutTheFirstStatus(api, Northwind, Comet);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenBarAsync(page, "Todo");
            await ChooseAsync(page, "Comet");

            await page.Locator(Bar, new PageLocatorOptions { HasText = "“Todo” is not in Comet. Choose a Status:" })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(["Doing", "Done"], await OptionsAsync(page));
            Assert.Equal(0, await page.Locator($"{Bar} [aria-selected='true'], {Bar} [aria-checked='true']").CountAsync());
            Assert.Empty(api.StatusBulkMoves);

            await ChooseAsync(page, "Doing");

            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Moved 2 Tasks to Comet, in Doing." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            using var body = JsonDocument.Parse(Assert.Single(api.StatusBulkMoves).Body ?? "{}");

            Assert.Equal(Comet, body.RootElement.GetProperty("projectId").GetGuid());
            Assert.Equal(doing, body.RootElement.GetProperty("statusId").GetGuid());
        }
    }

    /// <summary>
    /// Esc steps back from the Status step to the Projects, then closes the bar; Cancel closes it from the Project step. Either
    /// way focus is on the originating column and nothing was sent.
    /// </summary>
    /// <param name="how">How the bar is closed.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC10 - the bulk move bar is not built yet (Q3)")]
    [InlineData("escape")]
    [InlineData("cancel")]
    [Trait("Requirement", "UX-DR12")]
    public async Task Esc_steps_back_and_Cancel_closes_with_focus_on_the_originating_column_and_nothing_sent(string how)
    {
        var api = TwoSpaces();
        _ = SeedSpace(api, Northwind, ["Draft the brief", "Book the room"], Atlas, Beacon, Comet);
        _ = WithoutTheFirstStatus(api, Northwind, Comet);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenBarAsync(page, "Todo");

            if (how == "escape")
            {
                await ChooseAsync(page, "Comet");
                await page.Locator(BarOption, new PageLocatorOptions { HasText = "Doing" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

                await page.Keyboard.PressAsync("Escape");
                await page.Locator(BarOption, new PageLocatorOptions { HasText = "Beacon" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

                Assert.Equal(["Beacon", "Comet"], await OptionsAsync(page));

                await page.Keyboard.PressAsync("Escape");
            }
            else
            {
                await page.Locator(Bar).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Cancel", Exact = true }).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            }

            await page.Locator(Bar).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.Equal("Todo", await FocusedColumnAsync(page));
            Assert.Equal(["Draft the brief", "Book the room"], await TitlesInAsync(page, "Todo"));
            Assert.Empty(api.StatusBulkMoves);
        }
    }

    /// <summary>
    /// A refused move says why and that nothing moved: a destination whose set changed re-reads it and shows the Status step;
    /// a destination that is gone re-reads the Projects and shows the first step with the reason; a destination that cannot
    /// take them all says how many it cannot take and its limit; a Status that left the Project closes the bar with focus on
    /// the column. The cards stay on the Board in every case.
    /// </summary>
    /// <param name="refusal">The refusal the held move is released with.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC11 - the bulk move bar is not built yet")]
    [InlineData("status-destination-required")]
    [InlineData("invalid-task-project")]
    [InlineData("bound-reached")]
    [InlineData("not-available")]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "UX-DR32")]
    public async Task A_refused_move_says_why_and_that_nothing_moved(string refusal)
    {
        var api = TwoSpaces();
        _ = SeedSpace(api, Northwind, ["Draft the brief", "Book the room"], Atlas, Beacon, Comet);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenBarAsync(page, "Todo");

            var held = api.HoldStatusBulkMoveAsync();

            await ChooseAsync(page, "Beacon");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            switch (refusal)
            {
                case "status-destination-required":
                    _ = WithoutTheFirstStatus(api, Northwind, Beacon);
                    api.ReleaseStatusBulkMoveRefused(409, ProblemTypes.StatusDestinationRequired);
                    await page.Locator(Bar, new PageLocatorOptions { HasText = "“Todo” is not in Beacon. Choose a Status:" })
                        .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
                    Assert.Equal(["Doing", "Done"], await OptionsAsync(page));
                    break;

                case "invalid-task-project":
                    api.Projects[Northwind].RemoveAll(project => project.Id == Beacon);
                    api.ReleaseStatusBulkMoveRefused(400, ProblemTypes.InvalidTaskProject);
                    await page.Locator(Bar, new PageLocatorOptions { HasText = NothingMoved }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
                    await page.Locator(BarOption, new PageLocatorOptions { HasText = "Comet" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
                    Assert.Equal(["Comet"], await OptionsAsync(page));
                    break;

                case "bound-reached":
                    api.ReleaseStatusBulkMoveRefused(409, ProblemTypes.BoundReached);
                    await page.Locator(Bar, new PageLocatorOptions { HasText = "Beacon cannot take 2 more Tasks (at most 5,000). Nothing was moved." })
                        .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
                    break;

                default:
                    api.ReleaseStatusBulkMoveRefused(404, ProblemTypes.NotAvailable);
                    await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "“Todo” is no longer in this Project. Nothing was moved." })
                        .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });
                    await page.Locator(Bar).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });
                    Assert.Equal("Todo", await FocusedColumnAsync(page));
                    break;
            }

            Assert.Equal(["Draft the brief", "Book the room"], await TitlesInAsync(page, "Todo"));
            Assert.Single(api.StatusBulkMoves);
        }
    }

    /// <summary>
    /// A move that never got an answer does not claim nothing moved: the bar stays and says the request was not sent.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC11 - the bulk move bar is not built yet")]
    [Trait("Requirement", "UX-DR32")]
    public async Task A_move_that_got_no_answer_does_not_claim_nothing_moved()
    {
        var api = TwoSpaces();
        _ = SeedSpace(api, Northwind, ["Draft the brief", "Book the room"], Atlas, Beacon);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenBarAsync(page, "Todo");

            var held = api.HoldStatusBulkMoveAsync();

            await ChooseAsync(page, "Beacon");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            api.ReleaseStatusBulkMoveUnanswered();

            await page.Locator(Bar, new PageLocatorOptions { HasText = NotSentCopy }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.DoesNotContain(NothingMoved, await page.Locator(Bar).InnerTextAsync(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// In the List View the control appears only for a managing Role with exactly one Status chosen and no other filter; it
    /// opens the bar above the rows; Cancel puts focus back on the control; a commit re-reads the List and puts focus on its
    /// heading.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9, AC10, AC15 - the List View control is not built yet (Q9)")]
    [Trait("Requirement", "FR-30")]
    [Trait("Requirement", "UX-DR27")]
    public async Task The_List_View_offers_the_move_for_exactly_one_Status_and_no_other_filter()
    {
        var api = TwoSpaces();
        _ = SeedSpace(api, Northwind, ["Draft the brief", "Book the room"], Atlas, Beacon);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenListAsync(api, page, Northwind, Atlas);

            Assert.Equal(0, await ListControl(page).CountAsync());

            await StatusFilter(page, "Todo").CheckAsync(new LocatorCheckOptions { Timeout = Timeout });
            await ListControl(page).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await StatusFilter(page, "Doing").CheckAsync(new LocatorCheckOptions { Timeout = Timeout });
            await ListControl(page).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            await StatusFilter(page, "Doing").UncheckAsync(new LocatorUncheckOptions { Timeout = Timeout });
            await ListControl(page).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            await page.Locator(BarOption).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.True(
                await page.EvaluateAsync<bool>($"() => !!(document.querySelector('{Bar}').compareDocumentPosition(document.querySelector('main table')) & Node.DOCUMENT_POSITION_FOLLOWING)"),
                "The bar is not drawn before the rows.");

            await page.Locator(Bar).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Cancel", Exact = true }).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            await page.Locator(Bar).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.Equal(MoveToProject, await ActiveTextAsync(page));
            Assert.Empty(api.StatusBulkMoves);

            var readsBefore = api.ListRequests.Count;

            await ListControl(page).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            await ChooseAsync(page, "Beacon");
            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Moved 2 Tasks to Beacon." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            Assert.True(api.ListRequests.Count > readsBefore, "The List was not read again after the move.");
            Assert.Equal(0, await page.Locator(ListRows).CountAsync());
            Assert.True(await FocusIsInAsync(page, ListHeading), "Focus is not on the List's heading after the move.");
            Assert.Single(api.StatusBulkMoves);
        }
    }

    /// <summary>
    /// With a Label filter set beside the one Status the control is absent - what moves would differ from what is shown - and
    /// for a Viewer it is absent whatever is filtered.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9, AC15 - the List View control is not built yet (Q9)")]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "FR-16")]
    public async Task The_List_View_control_is_absent_beside_another_filter_and_for_a_Viewer()
    {
        var api = TwoSpaces();
        _ = SeedSpace(api, Northwind, ["Draft the brief"], Atlas, Beacon);
        _ = SeedSpace(api, Harbour, ["Viewer's Task"], Dune, Guid.NewGuid());
        var label = Guid.NewGuid();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        api.Labels[Northwind] = [new FakeLabel(label, "Urgent", Yello.Contracts.Labels.LabelColours.Keys[0])];

        await using (context)
        {
            await OpenListAsync(api, page, Northwind, Atlas);
            await StatusFilter(page, "Todo").CheckAsync(new LocatorCheckOptions { Timeout = Timeout });
            await ListControl(page).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.GetByRole(AriaRole.Checkbox, new PageGetByRoleOptions { Name = "Urgent", Exact = true }).CheckAsync(new LocatorCheckOptions { Timeout = Timeout });
            await ListControl(page).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            await OpenListAsync(api, page, Harbour, Dune);
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await StatusFilter(page, "Todo").CheckAsync(new LocatorCheckOptions { Timeout = Timeout });
            await page.Locator(ListRows).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await ListControl(page).CountAsync());
            Assert.DoesNotContain(MoveToProject, await page.EvaluateAsync<string>("() => document.querySelector('main').innerHTML"), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// At 320px the Board is one column with the Status pager: the selected column's control opens the bar, which fits the
    /// viewport with no horizontal scroll.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC10 - the bulk move bar is not built yet")]
    [Trait("Requirement", "UX-DR41")]
    public async Task The_bar_is_usable_at_320px_with_the_Status_pager()
    {
        var api = TwoSpaces();
        _ = SeedSpace(api, Northwind, ["Draft the brief", "Book the room"], Atlas, Beacon, Comet);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(320, 800);
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenBarAsync(page, "Todo");

            var box = await page.Locator(Bar).BoundingBoxAsync();

            Assert.NotNull(box);
            Assert.True(box.X >= 0 && box.X + box.Width <= 320, $"The bar spans {box.X}..{box.X + box.Width}px at 320px.");

            for (var index = 0; index < await page.Locator(BarOption).CountAsync(); index++)
            {
                var option = await page.Locator(BarOption).Nth(index).BoundingBoxAsync();

                Assert.NotNull(option);
                Assert.True(option.X >= 0 && option.X + option.Width <= 320, $"Option {index} spans {option.X}..{option.X + option.Width}px at 320px.");
            }

            Assert.True(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= 320"), "The page scrolls horizontally at 320px.");
        }
    }

    /// <summary>
    /// A signed-in Account who is <paramref name="northwindRole"/> of Northwind Redesign and Viewer of Harbour Ops.
    /// </summary>
    private static FakeYelloApi TwoSpaces(string northwindRole = "owner")
    {
        var api = new FakeYelloApi { SignedIn = true };

        api.Switcher.Add((Northwind, "Northwind Redesign"));
        api.Switcher.Add((Harbour, "Harbour Ops"));
        api.Roles[Northwind] = northwindRole;
        api.Roles[Harbour] = "viewer";

        return api;
    }

    /// <summary>
    /// A Space's Projects, named for their ids, each with a Board of the Space's three default columns - the same Status ids
    /// in every Project, as Space defaults are - and its set seeded so its Status read is served. The first Project holds
    /// <paramref name="titles"/> in its first column; the others are empty.
    /// </summary>
    private static Dictionary<string, FakeTask> SeedSpace(FakeYelloApi api, Guid spaceId, string[] titles, Guid first, params Guid[] others)
    {
        var statusIds = StatusNames.Select(_ => Guid.NewGuid()).ToList();
        var seeded = new Dictionary<string, FakeTask>(StringComparer.Ordinal);
        var projects = new List<FakeProject>();

        foreach (var projectId in new[] { first }.Concat(others))
        {
            var columns = StatusNames.Select((status, index) => new FakeColumn(statusIds[index], status, 0, [])).ToList();

            if (projectId == first)
            {
                foreach (var title in titles)
                {
                    var task = new FakeTask(Guid.NewGuid(), title, columns[0].StatusId, $"a{columns[0].Tasks.Count:D5}", DateTimeOffset.UtcNow);

                    columns[0].Tasks.Add(task);
                    columns[0].Total++;
                    seeded[title] = task;
                }
            }

            api.Boards[(spaceId, projectId)] = new FakeBoard(new FakeBoardProject(projectId, NameOf(projectId)), columns);
            api.SeedProjectStatuses(spaceId, projectId);
            projects.Add(new FakeProject(projectId, NameOf(projectId), DateTimeOffset.UtcNow));
        }

        api.Projects[spaceId] = projects;

        return seeded;
    }

    /// <summary>
    /// A Task that joins the first column of one Project behind the page's back, as a concurrent creation would.
    /// </summary>
    private static void AddTask(FakeYelloApi api, Guid spaceId, Guid projectId, string title)
    {
        var column = api.Boards[(spaceId, projectId)].Columns[0];

        column.Tasks.Add(new FakeTask(Guid.NewGuid(), title, column.StatusId, $"a{column.Tasks.Count:D5}", DateTimeOffset.UtcNow));
        column.Total++;
    }

    /// <summary>
    /// Takes the first default out of one Project's set and Board, as a removal there would; answers the Status left first.
    /// </summary>
    private static Guid WithoutTheFirstStatus(FakeYelloApi api, Guid spaceId, Guid projectId)
    {
        var set = api.ProjectStatuses[(spaceId, projectId)];
        var removed = set[0].Id;

        set.RemoveAt(0);

        var board = api.Boards[(spaceId, projectId)];

        api.Boards[(spaceId, projectId)] = board with { Columns = [.. board.Columns.Where(column => column.StatusId != removed)] };

        return set[0].Id;
    }

    private static string NameOf(Guid projectId) =>
        new Dictionary<Guid, string> { [Atlas] = "Atlas", [Beacon] = "Beacon", [Comet] = "Comet", [Dune] = "Dune" }.GetValueOrDefault(projectId, "Elsewhere");

    private static ILocator ColumnNamed(IPage page, string status) =>
        page.Locator(Column, new PageLocatorOptions { Has = page.Locator("h2", new PageLocatorOptions { HasText = status }) });

    private static ILocator ColumnControl(IPage page, string status) =>
        ColumnNamed(page, status).Locator(ColumnHead).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { NameRegex = ColumnControlName });

    private static ILocator ListControl(IPage page) =>
        page.Locator(".task-list").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = MoveToProject, Exact = true });

    private static ILocator StatusFilter(IPage page, string status) =>
        page.GetByRole(AriaRole.Checkbox, new PageGetByRoleOptions { Name = status, Exact = true });

    private static async Task<List<string>> TitlesInAsync(IPage page, string status) =>
        [.. (await ColumnNamed(page, status).Locator(CardTitle).AllTextContentsAsync()).Select(title => title.Trim())];

    private static async Task<List<string>> OptionsAsync(IPage page) =>
        [.. (await page.Locator(BarOption).AllInnerTextsAsync()).Select(option => option.Trim())];

    private static Task<string> ActiveTextAsync(IPage page) =>
        page.EvaluateAsync<string>("() => document.activeElement ? document.activeElement.textContent.trim() : ''");

    private static Task<bool> FocusIsInAsync(IPage page, string selector) =>
        page.EvaluateAsync<bool>("selector => { const host = document.querySelector(selector); return !!host && (host === document.activeElement || host.contains(document.activeElement)); }", selector);

    private static Task<string> FocusedColumnAsync(IPage page) =>
        page.EvaluateAsync<string>("() => { const column = document.activeElement && document.activeElement.closest('.board-column'); const heading = column && column.querySelector('h2'); return heading ? heading.textContent.trim() : ''; }");

    private static async Task OpenBarAsync(IPage page, string status)
    {
        await ColumnControl(page, status).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
        await page.Locator(BarOption).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    private static Task ChooseAsync(IPage page, string option) =>
        page.Locator(BarOption, new PageLocatorOptions { HasText = option }).First.ClickAsync(new LocatorClickOptions { Timeout = Timeout });

    private string Address(string path) => new Uri(surface.BaseAddress, path.TrimStart('/')).ToString();

    private async Task InstallOnceAsync(FakeYelloApi api, IPage page)
    {
        if (!_installed)
        {
            _installed = true;
            await api.InstallAsync(page);
        }
    }

    private async Task OpenBoardAsync(FakeYelloApi api, IPage page, Guid spaceId, Guid projectId)
    {
        await InstallOnceAsync(api, page);
        await page.GotoAsync(Address($"spaces/{spaceId:D}/projects/{projectId:D}"));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.Locator(Card).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    private async Task OpenListAsync(FakeYelloApi api, IPage page, Guid spaceId, Guid projectId)
    {
        await InstallOnceAsync(api, page);
        await page.GotoAsync(Address($"spaces/{spaceId:D}/projects/{projectId:D}/list"));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.Locator(ListRows).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }
}
