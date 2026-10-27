using System.Globalization;
using System.Text.Json;
using Microsoft.Playwright;
using Xunit;
using Yello.Contracts;

namespace Yello.Tests.Client;

/// <summary>
/// Story 2.6 in a real browser: moving a Task to another Status and position by an explicit control - the Move control
/// in Task detail and the card's context menu - never by a gesture. The picker names the destination Status, then the
/// position, with nothing selected; the move is optimistic, announced with its ordinal and the column's true total, and
/// reverted out loud when refused; a Viewer is offered nothing. AC1, AC2, AC7, AC14, AC15; FR-29, UX-DR21, UX-DR24,
/// UX-DR25, UX-DR27, UX-DR11, UX-DR12, AR-20 with AD-11, NFR-9. Written red by the story 2.6 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>The API is <see cref="FakeYelloApi"/></b> with story 2.6's move route (<see cref="FakeYelloApi.MovePathOf"/>, Q1
/// (a)): a move can be held (<see cref="FakeYelloApi.HoldTaskMoveAsync"/>) and released admitted, refused or unanswered -
/// the TESTING-CONVENTIONS shape for an optimistic write: hold, assert the new state, release, assert. The fake places
/// the Task after its named neighbour (or at the top for <c>null</c>) and writes the moved Task's key alone.
/// </para>
/// <para>
/// <b>The locators and copy are assumptions</b>, to be corrected to the markup Task 9 builds and the correction recorded
/// (story Q4-Q9): the panel's control is a button named "Move", after the Status control and before "Delete Task" (Q5);
/// the card carries, for a Role that can move, a button named "Actions for {title}" opening a <c>[role=menu]</c> with
/// <c>[role=menuitem]</c>s "Open" and "Move" (Q6 (a)), also opened by the <c>contextmenu</c> event on the card, by
/// <c>Enter</c> on the actions control, and by the <c>ContextMenu</c> key and <c>Shift+F10</c> on the card; the picker is
/// one <c>.move-picker</c> rendered in place (never a second dialog) holding <c>[role=option]</c> rows - first the
/// Statuses, the current one labelled "Current" and none selected, then the positions: "Top of column", then
/// "Position {n}, after {title}" per Task of that column (the moved Task excluded), the last also labelled "Bottom of
/// column" (Q4 (a)); the announcements read "Moved to {Status}, position {n} of {total}." and "Returned to {Status},
/// position {n}." (Q8); the Q3 refusal reads "That position has changed. Choose it again.". Copy that already exists
/// ("Viewers cannot edit Tasks.", "That Status is not one of this Project's Statuses.", "Not sent.", "Not available.") is
/// asserted as written.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-29")]
[Trait("Requirement", "UX-DR21")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class MoveTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string RoleChip = "nav.context-bar .role-chip";

    private const string Column = "main .board-column";
    private const string ColumnCount = ".column-count";
    private const string Card = ".task-card";
    private const string CardTitle = ".task-card__title";
    private const string Dialog = "[role='dialog']";
    private const string Picker = ".move-picker";
    private const string PickerOption = ".move-picker [role='option']";
    private const string Menu = "[role='menu']";
    private const string LiveRegion = "[aria-live='polite']";
    private const string DisabledAnywhere = "main [disabled], main [aria-disabled='true'], [role='dialog'] [disabled]";

    private const string MoveControl = "Move";
    private const string OpenItem = "Open";
    private const string StatusLabel = "Status";
    private const string DeleteTask = "Delete Task";
    private const string CurrentLabel = "Current";
    private const string TopOfColumn = "Top of column";
    private const string BottomOfColumn = "Bottom of column";

    private const string PositionChangedCopy = "That position has changed. Choose it again.";
    private const string ManageTasksCopy = "Viewers cannot edit Tasks.";
    private const string InvalidStatusCopy = "That Status is not one of this Project's Statuses.";
    private const string NotSentCopy = "Not sent.";
    private const string NotAvailableCopy = "Not available.";

    private const string InsufficientRoleType = "urn:yello:problem:insufficient-role";
    private const string InvalidTaskStatusType = "urn:yello:problem:invalid-task-status";

    private static readonly string[] StatusNames = ["Todo", "Doing", "Done"];

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();
    private static readonly Guid Beacon = Guid.NewGuid();

    private bool _installed;

    /// <summary>
    /// E2E-601. AC1, AC14, UX-DR11, UX-DR12, Q5 - Task detail carries a "Move" control after the Status control and
    /// before "Delete Task"; it opens the picker in place (still one <c>[role=dialog]</c>), listing the Project's Statuses
    /// in order, the current one labelled "Current", and nothing selected or checked - the choice is consequential. Nothing
    /// is sent by opening it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1 - Task detail has no Move control yet (Task 9, Q5)")]
    [Trait("Requirement", "UX-DR11")]
    [Trait("Requirement", "UX-DR12")]
    public async Task Move_in_Task_detail_opens_a_picker_of_Statuses_with_nothing_selected()
    {
        var api = TwoSpaces();
        var tasks = SeedBoard(api, Northwind, Atlas, ["Draft the brief"], ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks["Draft the brief"].Id);

            var dialog = page.Locator(Dialog);
            var move = PanelMoveControl(page);
            var status = dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = StatusLabel });
            var delete = dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = DeleteTask, Exact = true });

            Assert.True(await FollowsAsync(status, move), "The Move control is not after the Status control.");
            Assert.True(await FollowsAsync(move, delete), "The Move control is not before Delete Task.");

            await move.ClickAsync(new LocatorClickOptions { Timeout = Timeout });

            var options = dialog.Locator(PickerOption);

            await options.First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(1, await page.Locator(Dialog).CountAsync());
            Assert.Equal(StatusNames.Length, await options.CountAsync());

            for (var index = 0; index < StatusNames.Length; index++)
            {
                Assert.StartsWith(StatusNames[index], (await options.Nth(index).InnerTextAsync()).Trim(), StringComparison.Ordinal);
            }

            Assert.Contains(CurrentLabel, await options.Filter(new LocatorFilterOptions { HasText = "Todo" }).InnerTextAsync(), StringComparison.Ordinal);
            Assert.Equal(0, await dialog.Locator($"{Picker} [aria-selected='true'], {Picker} [aria-checked='true']").CountAsync());
            Assert.Empty(Moves(api));
        }
    }

    /// <summary>
    /// E2E-602. AC1, AC14, UX-DR21, UX-DR24, Q6 (a) - the card's context menu opens by every route the story names: a
    /// pointer click on the visible actions control, a right-click (<c>contextmenu</c>) on the card, <c>Enter</c> on the
    /// actions control, and the <c>ContextMenu</c> key and <c>Shift+F10</c> on the focused card. It holds "Open" and
    /// "Move"; "Move" opens the same picker, nothing selected. Nothing is sent.
    /// </summary>
    /// <param name="route">How the menu is opened.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC1 - the card has no context menu yet (Task 9, Q6)")]
    [InlineData("click")]
    [InlineData("contextmenu")]
    [InlineData("enter")]
    [InlineData("contextmenu-key")]
    [InlineData("shift-f10")]
    [Trait("Requirement", "UX-DR24")]
    public async Task The_cards_context_menu_opens_by_pointer_and_by_keyboard_and_offers_Move(string route)
    {
        var api = TwoSpaces();
        var tasks = SeedBoard(api, Northwind, Atlas, ["Draft the brief"], ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenCardMenuAsync(page, tasks["Draft the brief"], route);

            var menu = page.Locator(Menu);
            var items = (await menu.Locator("[role='menuitem']").AllInnerTextsAsync()).Select(item => item.Trim()).ToList();

            Assert.Equal([OpenItem, MoveControl], items);

            await menu.GetByRole(AriaRole.Menuitem, new LocatorGetByRoleOptions { Name = MoveControl, Exact = true }).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            await page.Locator(PickerOption).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Dialog).CountAsync());
            Assert.Equal(0, await page.Locator($"{Picker} [aria-selected='true'], {Picker} [aria-checked='true']").CountAsync());
            Assert.Empty(Moves(api));
        }
    }

    /// <summary>
    /// E2E-603. AC1, AC14, Q4 (a) - choosing a Status moves the picker to its position step: "Top of column" first, then
    /// one option per Task of that column named by the ordinal the moved Task would take and its neighbour - "Position 2,
    /// after Review", "Position 3, after Ship" - the last also labelled "Bottom of column". Within the Task's own column the
    /// moved Task is not offered as its own neighbour.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1 - the Move picker has no position step yet (Task 9, Q4)")]
    public async Task The_position_step_offers_the_top_then_each_slot_by_ordinal_and_neighbour()
    {
        var api = TwoSpaces();
        var tasks = SeedBoard(api, Northwind, Atlas, ["Draft the brief", "Book the room"], ["Review", "Ship"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks["Draft the brief"].Id);
            await PanelMoveControl(page).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            await ChooseStatusStepAsync(page, "Doing");

            var positions = (await page.Locator(PickerOption).AllInnerTextsAsync()).Select(option => option.Trim()).ToList();

            Assert.Equal(3, positions.Count);
            Assert.StartsWith(TopOfColumn, positions[0], StringComparison.Ordinal);
            Assert.StartsWith("Position 2, after Review", positions[1], StringComparison.Ordinal);
            Assert.StartsWith("Position 3, after Ship", positions[2], StringComparison.Ordinal);
            Assert.Contains(BottomOfColumn, positions[2], StringComparison.Ordinal);

            // Back to the Status step (Esc steps back innermost-first), then the Task's own column.
            await page.Keyboard.PressAsync("Escape");
            await ChooseStatusStepAsync(page, "Todo");

            var own = (await page.Locator(PickerOption).AllInnerTextsAsync()).Select(option => option.Trim()).ToList();

            Assert.StartsWith(TopOfColumn, own[0], StringComparison.Ordinal);
            Assert.DoesNotContain(own, option => option.Contains("after Draft the brief", StringComparison.Ordinal));
            Assert.Empty(Moves(api));
        }
    }

    /// <summary>
    /// E2E-604. AC3, AC7, AC15, AR-20 with AD-11, UX-DR25 - a move is optimistic: while the <c>POST</c> is held the card is
    /// already in its chosen slot (after Review, before Ship) and both column totals have moved; the request names the
    /// destination Status and the neighbour, carries an <c>Idempotency-Key</c>, and never a position key. On <c>200</c> the
    /// move is announced politely with the ordinal and the column's true total: "Moved to Doing, position 2 of 3.".
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC15 - the client sends no move yet (Task 9, Q7)")]
    [Trait("Requirement", "AR-20")]
    [Trait("Requirement", "AD-11")]
    [Trait("Requirement", "UX-DR25")]
    public async Task A_move_renders_at_once_with_both_totals_and_is_announced_with_ordinal_and_total_on_admission()
    {
        var api = TwoSpaces();
        var tasks = SeedBoard(api, Northwind, Atlas, ["Draft the brief", "Book the room"], ["Review", "Ship"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks["Draft the brief"].Id);

            var held = api.HoldTaskMoveAsync();

            await MoveFromPanelAsync(page, "Doing", "Position 2, after Review");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            // Held: already in its slot, both totals moved.
            Assert.Equal(["Review", "Draft the brief", "Ship"], await TitlesInAsync(page, "Doing"));
            Assert.Equal(["Book the room"], await TitlesInAsync(page, "Todo"));
            Assert.Equal("3", await TotalOfAsync(page, "Doing"));
            Assert.Equal("1", await TotalOfAsync(page, "Todo"));

            var sent = Assert.Single(Moves(api));

            Assert.Equal(FakeYelloApi.MovePathOf(Northwind, tasks["Draft the brief"].Id), sent.Path);
            Assert.NotNull(sent.Header(Idempotency.HeaderName));

            using var body = JsonDocument.Parse(sent.Body ?? "{}");

            Assert.Equal(api.Boards[(Northwind, Atlas)].Columns[1].StatusId, body.RootElement.GetProperty("statusId").GetGuid());
            Assert.Equal(tasks["Review"].Id, body.RootElement.GetProperty("afterTaskId").GetGuid());
            Assert.False(body.RootElement.TryGetProperty("positionKey", out _), "The move carried a position key.");

            api.ReleaseTaskMove();

            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Moved to Doing, position 2 of 3." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            Assert.Equal(["Review", "Draft the brief", "Ship"], await TitlesInAsync(page, "Doing"));
        }
    }

    /// <summary>
    /// E2E-605. AC15, UX-DR32, UX-DR25, Q3 (a), Q8 - a refused move puts the card back in its last admitted slot and both
    /// totals with it, says the refusal in the client's own words where the move was made (the panel), and announces the
    /// restoration in the first column's name, at position 1.
    /// </summary>
    /// <param name="status">The refusal's status.</param>
    /// <param name="type">Its problem type.</param>
    /// <param name="capability">The capability a <c>403</c> names.</param>
    /// <param name="copy">What the panel says.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC15 - the client sends no move yet, so nothing reverts (Task 9, Q8)")]
    [InlineData(403, InsufficientRoleType, "manage-tasks", ManageTasksCopy)]
    [InlineData(409, FakeYelloApi.TaskPositionUnavailableType, null, PositionChangedCopy)]
    [InlineData(400, InvalidTaskStatusType, null, InvalidStatusCopy)]
    [Trait("Requirement", "UX-DR32")]
    [Trait("Requirement", "UX-DR25")]
    public async Task A_refused_move_puts_the_card_and_both_totals_back_and_announces_the_return(int status, string type, string? capability, string copy)
    {
        var api = TwoSpaces();
        var tasks = SeedBoard(api, Northwind, Atlas, ["Draft the brief", "Book the room"], ["Review", "Ship"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks["Draft the brief"].Id);

            var held = api.HoldTaskMoveAsync();

            await MoveFromPanelAsync(page, "Doing", TopOfColumn);
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            Assert.Equal("3", await TotalOfAsync(page, "Doing"));

            api.ReleaseTaskMoveRefused(status, type, capability);

            await page.Locator(Dialog).GetByText(copy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Returned to Todo, position 1." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            Assert.Equal(["Draft the brief", "Book the room"], await TitlesInAsync(page, "Todo"));
            Assert.Equal(["Review", "Ship"], await TitlesInAsync(page, "Doing"));
            Assert.Equal("2", await TotalOfAsync(page, "Todo"));
            Assert.Equal("2", await TotalOfAsync(page, "Doing"));
            Assert.Equal(0, await page.Locator("[role='alert']").CountAsync());
        }
    }

    /// <summary>
    /// E2E-606. AC15, NFR-5, AR-25 - a move that gets no answer says "Not sent.", reverts the card and the totals, and
    /// announces the return; choosing the same destination again resends the identical body under the same
    /// <c>Idempotency-Key</c>, and that answer lands the card.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC15 - the client sends no move yet (Task 9, Q7, Q8)")]
    [Trait("Requirement", "NFR-5")]
    [Trait("Requirement", "AR-25")]
    public async Task An_unanswered_move_says_not_sent_reverts_and_its_resend_reuses_the_key()
    {
        var api = TwoSpaces();
        var tasks = SeedBoard(api, Northwind, Atlas, ["Draft the brief", "Book the room"], ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks["Draft the brief"].Id);

            var held = api.HoldTaskMoveAsync();

            await MoveFromPanelAsync(page, "Doing", TopOfColumn);
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);
            api.ReleaseTaskMoveUnanswered();

            await page.Locator(Dialog).GetByText(NotSentCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Returned to Todo, position 1." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            Assert.Equal(["Draft the brief", "Book the room"], await TitlesInAsync(page, "Todo"));

            await MoveFromPanelAsync(page, "Doing", TopOfColumn);
            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Moved to Doing, position 1 of 2." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            var sent = Moves(api);

            Assert.Equal(2, sent.Count);
            Assert.NotNull(sent[0].Header(Idempotency.HeaderName));
            Assert.Equal(sent[0].Header(Idempotency.HeaderName), sent[1].Header(Idempotency.HeaderName));
            Assert.Equal(sent[0].Body, sent[1].Body);
            Assert.Equal(["Draft the brief", "Review"], await TitlesInAsync(page, "Doing"));
        }
    }

    /// <summary>
    /// E2E-607. AC15, AD-3 - a move answered <c>404</c> follows the Board's existing pattern for a <c>404</c>: the page's
    /// refusal surface, "Not available.", and the panel does not stay open over a Task that is not there.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC15 - the client sends no move yet (Task 9)")]
    [Trait("Requirement", "UX-DR32")]
    public async Task A_move_answered_404_follows_the_Boards_not_found_pattern()
    {
        var api = TwoSpaces();
        var tasks = SeedBoard(api, Northwind, Atlas, ["Draft the brief"], ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks["Draft the brief"].Id);

            api.NextTaskMoveRefusal = (404, ProblemTypes.NotAvailable, null);

            await MoveFromPanelAsync(page, "Doing", TopOfColumn);
            await page.Locator("main").GetByText(NotAvailableCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Dialog).CountAsync());
            Assert.Single(Moves(api));
        }
    }

    /// <summary>
    /// E2E-608. AC14, UX-DR12, UX-DR39, Q9 - dismissing moves nothing and says nothing: <c>Esc</c> closes the innermost
    /// layer first (the picker, leaving the panel open, or the card's menu), focus returns to the control that opened it,
    /// and no <c>POST</c> is sent.
    /// </summary>
    /// <param name="fromPanel">Whether the picker was opened from Task detail (otherwise from the card's menu).</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC14 - there is no Move picker to dismiss yet (Task 9, Q9)")]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "UX-DR12")]
    [Trait("Requirement", "UX-DR39")]
    public async Task Dismissing_the_picker_moves_nothing_and_returns_focus_to_its_invoker(bool fromPanel)
    {
        var api = TwoSpaces();
        var tasks = SeedBoard(api, Northwind, Atlas, ["Draft the brief"], ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);

            if (fromPanel)
            {
                await OpenPanelAsync(page, tasks["Draft the brief"].Id);
                await PanelMoveControl(page).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
                await page.Locator(PickerOption).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
                await page.Keyboard.PressAsync("Escape");
                await page.Locator(Picker).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

                Assert.Equal(1, await page.Locator(Dialog).CountAsync());
                Assert.Equal(MoveControl, await ActiveTextAsync(page));
            }
            else
            {
                await OpenCardMenuAsync(page, tasks["Draft the brief"], "click");
                await page.Keyboard.PressAsync("Escape");
                await page.Locator(Menu).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

                Assert.Equal(
                    "Actions for Draft the brief",
                    await page.EvaluateAsync<string>("() => document.activeElement ? (document.activeElement.getAttribute('aria-label') || document.activeElement.textContent.trim()) : ''"));
            }

            Assert.Empty(Moves(api));
            Assert.Equal(["Draft the brief"], await TitlesInAsync(page, "Todo"));
        }
    }

    /// <summary>
    /// E2E-609. AC14, UX-DR39, Q9 - after a committed move focus is never on <c>&lt;body&gt;</c>: from Task detail it stays
    /// on the panel's Move control, the panel still open; from the card's menu it goes to the moved card's title button in
    /// its new column.
    /// </summary>
    /// <param name="fromPanel">Whether the move was made from Task detail (otherwise from the card's menu).</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC14 - the client sends no move yet (Task 9, Q9)")]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "UX-DR39")]
    public async Task After_a_move_focus_goes_where_the_move_was_made(bool fromPanel)
    {
        var api = TwoSpaces();
        var tasks = SeedBoard(api, Northwind, Atlas, ["Draft the brief"], ["Review"]);
        var moved = tasks["Draft the brief"];
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);

            if (fromPanel)
            {
                await OpenPanelAsync(page, moved.Id);
                await MoveFromPanelAsync(page, "Doing", TopOfColumn);
            }
            else
            {
                await OpenCardMenuAsync(page, moved, "click");
                await page.Locator(Menu).GetByRole(AriaRole.Menuitem, new LocatorGetByRoleOptions { Name = MoveControl, Exact = true }).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
                await ChooseStatusStepAsync(page, "Doing");
                await page.Locator(PickerOption, new PageLocatorOptions { HasText = TopOfColumn }).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            }

            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Moved to Doing, position 1 of 2." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            if (fromPanel)
            {
                Assert.Equal(1, await page.Locator(Dialog).CountAsync());
                Assert.Equal(MoveControl, await ActiveTextAsync(page));
            }
            else
            {
                Assert.Equal(
                    moved.Id.ToString("D", CultureInfo.InvariantCulture),
                    await page.EvaluateAsync<string>("() => document.activeElement ? (document.activeElement.getAttribute('data-task-id') || 'none') : 'none'"));
                Assert.Equal("Doing", await page.EvaluateAsync<string>("() => { const column = document.activeElement.closest('.board-column'); return column ? column.querySelector('h2').textContent.trim() : 'none'; }"));
            }
        }
    }

    /// <summary>
    /// E2E-610. AC3, AC6, AC7, FR-29 - reordering within one column from the card's menu: "Top of column" puts the second
    /// card first, announced as moved to the first column, position 1 of 2; the order survives a reload (the Board read again from the
    /// server), and only one move was sent.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC6 - the client cannot reorder within a column yet (Task 9)")]
    public async Task Reordering_within_a_column_is_announced_and_survives_a_reload()
    {
        var api = TwoSpaces();
        var tasks = SeedBoard(api, Northwind, Atlas, ["Draft the brief", "Book the room"], []);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenCardMenuAsync(page, tasks["Book the room"], "click");
            await page.Locator(Menu).GetByRole(AriaRole.Menuitem, new LocatorGetByRoleOptions { Name = MoveControl, Exact = true }).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            await ChooseStatusStepAsync(page, "Todo");
            await page.Locator(PickerOption, new PageLocatorOptions { HasText = TopOfColumn }).ClickAsync(new LocatorClickOptions { Timeout = Timeout });

            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Moved to Todo, position 1 of 2." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            Assert.Equal(["Book the room", "Draft the brief"], await TitlesInAsync(page, "Todo"));

            await OpenBoardAsync(api, page, Northwind, Atlas);

            Assert.Equal(["Book the room", "Draft the brief"], await TitlesInAsync(page, "Todo"));
            Assert.Single(Moves(api));
        }
    }

    /// <summary>
    /// E2E-611. AC2, AC14, UX-DR21, UX-DR41 - at 320, 768 and 1280 px the card's actions control and the panel's Move
    /// control are rendered and visible with the pointer away from them - never revealed only on hover (not hidden by
    /// <c>display</c>, <c>visibility</c> or <c>opacity</c>).
    /// </summary>
    /// <param name="width">The viewport width.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC2 - neither Move control exists yet (Task 9)")]
    [InlineData(320)]
    [InlineData(768)]
    [InlineData(1280)]
    [Trait("Requirement", "UX-DR41")]
    public async Task The_Move_controls_are_visible_without_hover_at_every_breakpoint(int width)
    {
        var api = TwoSpaces();
        var tasks = SeedBoard(api, Northwind, Atlas, ["Draft the brief"], []);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(width, 900);
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await page.Mouse.MoveAsync(0, 0);

            var actions = ActionsControl(page, "Draft the brief");

            Assert.True(await actions.IsVisibleAsync(), $"The actions control is not visible at {width}px without hover.");
            Assert.NotEqual("0", await actions.EvaluateAsync<string>("element => getComputedStyle(element).opacity"));

            await OpenPanelAsync(page, tasks["Draft the brief"].Id);
            await page.Mouse.MoveAsync(0, 0);

            var move = PanelMoveControl(page);

            Assert.True(await move.IsVisibleAsync(), $"The panel's Move control is not visible at {width}px without hover.");
            Assert.NotEqual("0", await move.EvaluateAsync<string>("element => getComputedStyle(element).opacity"));
        }
    }

    /// <summary>
    /// E2E-612. AC2, AC14, UX-DR27 (extends E2E-003) - a Viewer is offered no move: no actions control on any card, no Move
    /// control in Task detail, and a right-click on a card opens nothing of ours (no <c>[role=menu]</c>) - absent, not
    /// disabled, not hidden by CSS - and nothing in <c>main</c> or the panel is disabled. Anchored: the Owner of the other
    /// Space is offered both.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2 - the Owner's anchor needs the Move controls (Task 9)")]
    [Trait("Requirement", "UX-DR27")]
    public async Task A_Viewer_is_offered_no_move_control_and_nothing_is_disabled()
    {
        var api = TwoSpaces();
        var owners = SeedBoard(api, Northwind, Atlas, ["Owner's Task"], []);
        var viewers = SeedBoard(api, Harbour, Beacon, ["Viewer's Task"], []);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            // Anchor: the Owner has both.
            await OpenBoardAsync(api, page, Northwind, Atlas);
            Assert.Equal(1, await ActionsControl(page, "Owner's Task").CountAsync());
            await OpenPanelAsync(page, owners["Owner's Task"].Id);
            Assert.Equal(1, await PanelMoveControl(page).CountAsync());

            // The Viewer: neither, and nothing disabled.
            await OpenBoardAsync(api, page, Harbour, Beacon);
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Actions for Viewer's Task" }).CountAsync());

            await CardOf(page, viewers["Viewer's Task"].Id).ClickAsync(new LocatorClickOptions { Button = MouseButton.Right, Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Menu).CountAsync());

            await page.Keyboard.PressAsync("Escape");
            await OpenPanelAsync(page, viewers["Viewer's Task"].Id);

            Assert.Equal(0, await PanelMoveControl(page).CountAsync());
            Assert.Equal(0, await page.Locator(Picker).CountAsync());
            Assert.DoesNotContain("Actions for", await page.EvaluateAsync<string>("() => document.body.innerHTML"), StringComparison.Ordinal);
            Assert.Equal(0, await page.Locator(DisabledAnywhere).CountAsync());
        }
    }

    /// <summary>
    /// E2E-613. AC15, UX-DR38 - nothing about a move is announced on load: once the Board has rendered, no live region
    /// holds a move or a return sentence. <b>Green today</b> (no move sentence exists); a standing gate that Task 9's
    /// announcements must keep - they are said on a deliberate action only.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR38")]
    public async Task Nothing_about_a_move_is_announced_on_load()
    {
        var api = TwoSpaces();
        _ = SeedBoard(api, Northwind, Atlas, ["Draft the brief", "Book the room"], ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);

            // Anchor: the live regions exist from the first render (2.2 review), so "holds nothing" is about something.
            Assert.True(await page.Locator(LiveRegion).CountAsync() > 0, "The Board rendered no live region.");

            var announced = string.Join(" | ", await page.Locator(LiveRegion).AllTextContentsAsync());

            Assert.DoesNotContain("Moved to", announced, StringComparison.Ordinal);
            Assert.DoesNotContain("Returned to", announced, StringComparison.Ordinal);
            Assert.DoesNotContain("position", announced, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// E2E-614. AC15, UX-DR6 - under <c>prefers-reduced-motion: reduce</c> nothing is animated: the moved card, held in its
    /// new slot, declares no transition and no animation, and neither does any card that did not move.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC15 - the client sends no move yet (Task 9, motion)")]
    [Trait("Requirement", "UX-DR6")]
    public async Task Under_reduced_motion_a_move_animates_nothing()
    {
        var api = TwoSpaces();
        var tasks = SeedBoard(api, Northwind, Atlas, ["Draft the brief", "Book the room"], ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks["Draft the brief"].Id);

            var held = api.HoldTaskMoveAsync();

            await MoveFromPanelAsync(page, "Doing", TopOfColumn);
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            var moving = await page.EvaluateAsync<string[]>(
                """
                () => [...document.querySelectorAll('.task-card')]
                    .filter(card => {
                        const style = getComputedStyle(card);
                        const durations = (style.transitionDuration + ',' + style.animationDuration).split(',').map(value => parseFloat(value) || 0);
                        return durations.some(value => value > 0) || style.animationName !== 'none';
                    })
                    .map(card => card.textContent.trim())
                """);

            Assert.Empty(moving);

            api.ReleaseTaskMove();
        }
    }

    /// <summary>
    /// E2E-615. AC7, Q8 (b) - a Status change from the Status picker is a move too, and says the same sentence on
    /// admission: it lands last, so "Moved to Done, position 1 of 1." - one move sentence on the Board. Changes 2.3's
    /// "Moved to {0}." deliberately (<c>TaskDetailTests.Choosing_a_Status_moves_the_card_and_both_totals_at_once</c> is
    /// updated with it).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC7 - the Status change still announces 2.3's sentence at edit time (Task 9, Q8 (b))")]
    [Trait("Requirement", "UX-DR25")]
    public async Task A_Status_change_announces_the_move_sentence_on_admission()
    {
        var api = TwoSpaces();
        var tasks = SeedBoard(api, Northwind, Atlas, ["Draft the brief"], []);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks["Draft the brief"].Id);

            var held = api.HoldTaskEditAsync();
            var dialog = page.Locator(Dialog);

            await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = StatusLabel }).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            await dialog.Locator("[role='listbox'] [role='option']", new LocatorLocatorOptions { HasText = "Done" }).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            // Not yet admitted: nothing said.
            Assert.DoesNotContain("Moved to Done", string.Join(" | ", await page.Locator(LiveRegion).AllTextContentsAsync()), StringComparison.Ordinal);

            api.ReleaseTaskEdit();

            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Moved to Done, position 1 of 1." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });
        }
    }

    /// <summary>
    /// E2E-616. AC1, Q6 (a) - the card menu's "Open" opens Task detail for that Task, exactly as its title button does.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1 - the card has no context menu yet (Task 9, Q6)")]
    [Trait("Requirement", "UX-DR24")]
    public async Task The_card_menus_Open_opens_Task_detail()
    {
        var api = TwoSpaces();
        var tasks = SeedBoard(api, Northwind, Atlas, ["Draft the brief"], []);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenCardMenuAsync(page, tasks["Draft the brief"], "click");
            await page.Locator(Menu).GetByRole(AriaRole.Menuitem, new LocatorGetByRoleOptions { Name = OpenItem, Exact = true }).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            await page.Locator(Dialog).GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = "Draft the brief" })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Menu).CountAsync());
            Assert.Empty(Moves(api));
        }
    }

    /// <summary>
    /// A signed-in Account who is Owner of Northwind Redesign and Viewer of Harbour Ops, listed in that order.
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
    /// A Project and its Board: three columns in the default order, the first count of Tasks in the first and
    /// <paramref name="doing"/> in the second, keyed in list order. Answers the seeded Tasks by title, as seeded - a move
    /// replaces the fake's own entries, and a case keeps the ids it seeded.
    /// </summary>
    private static Dictionary<string, FakeTask> SeedBoard(FakeYelloApi api, Guid spaceId, Guid projectId, string[] todo, string[] doing)
    {
        var name = spaceId == Northwind ? "Atlas" : "Beacon";
        var columns = StatusNames.Select(status => new FakeColumn(Guid.NewGuid(), status, 0, [])).ToList();
        var seeded = new Dictionary<string, FakeTask>(StringComparer.Ordinal);

        foreach (var (titles, column) in new[] { (todo, columns[0]), (doing, columns[1]) })
        {
            foreach (var title in titles)
            {
                var task = new FakeTask(Guid.NewGuid(), title, column.StatusId, $"a{column.Tasks.Count:D5}", DateTimeOffset.UtcNow);

                column.Tasks.Add(task);
                column.Total++;
                seeded[title] = task;
            }
        }

        api.Boards[(spaceId, projectId)] = new FakeBoard(new FakeBoardProject(projectId, name), columns);
        api.Projects[spaceId] = [new FakeProject(projectId, name, DateTimeOffset.UtcNow)];

        return seeded;
    }

    private static List<FakeRequest> Moves(FakeYelloApi api) =>
        [.. api.Requests.Where(request => request.Method == "POST" && request.Path.EndsWith("/move", StringComparison.Ordinal))];

    private static ILocator CardOf(IPage page, Guid taskId) =>
        page.Locator(Card, new PageLocatorOptions { Has = page.Locator($"button[data-task-id='{taskId:D}']") });

    private static ILocator ColumnNamed(IPage page, string status) =>
        page.Locator(Column, new PageLocatorOptions { Has = page.Locator("h2", new PageLocatorOptions { HasText = status }) });

    private static ILocator ActionsControl(IPage page, string title) =>
        page.Locator(Card).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = $"Actions for {title}", Exact = true });

    private static ILocator PanelMoveControl(IPage page) =>
        page.Locator(Dialog).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = MoveControl, Exact = true });

    private static async Task<List<string>> TitlesInAsync(IPage page, string status) =>
        [.. (await ColumnNamed(page, status).Locator(CardTitle).AllTextContentsAsync()).Select(title => title.Trim())];

    private static async Task<string> TotalOfAsync(IPage page, string status) =>
        (await ColumnNamed(page, status).Locator(ColumnCount).InnerTextAsync()).Trim();

    private static Task<string> ActiveTextAsync(IPage page) =>
        page.EvaluateAsync<string>("() => document.activeElement ? document.activeElement.textContent.trim() : ''");

    /// <summary>
    /// Whether <paramref name="after"/> comes after <paramref name="before"/> in document order.
    /// </summary>
    private static async Task<bool> FollowsAsync(ILocator before, ILocator after)
    {
        var handle = await after.ElementHandleAsync(new LocatorElementHandleOptions { Timeout = Timeout });

        return await before.EvaluateAsync<bool>("(first, second) => !!(first.compareDocumentPosition(second) & Node.DOCUMENT_POSITION_FOLLOWING)", handle);
    }

    private static async Task OpenPanelAsync(IPage page, Guid taskId)
    {
        await page.Locator($"{Card} button[data-task-id='{taskId:D}']").ClickAsync(new LocatorClickOptions { Timeout = Timeout });
        await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    /// <summary>
    /// Opens a card's context menu by <paramref name="route"/> and waits for it. Keyboard routes start from a reset focus.
    /// </summary>
    private static async Task OpenCardMenuAsync(IPage page, FakeTask task, string route)
    {
        var card = CardOf(page, task.Id);
        var title = card.Locator($"button[data-task-id='{task.Id:D}']");

        await page.EvaluateAsync("() => { if (document.activeElement) document.activeElement.blur(); }");

        switch (route)
        {
            case "click":
                await ActionsControl(page, task.Title).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
                break;
            case "contextmenu":
                await card.ClickAsync(new LocatorClickOptions { Button = MouseButton.Right, Timeout = Timeout });
                break;
            case "enter":
                await ActionsControl(page, task.Title).FocusAsync(new LocatorFocusOptions { Timeout = Timeout });
                await page.Keyboard.PressAsync("Enter");
                break;
            case "contextmenu-key":
                await title.FocusAsync(new LocatorFocusOptions { Timeout = Timeout });
                await page.Keyboard.PressAsync("ContextMenu");
                break;
            case "shift-f10":
                await title.FocusAsync(new LocatorFocusOptions { Timeout = Timeout });
                await page.Keyboard.PressAsync("Shift+F10");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(route), route, "Not a route to the card's menu.");
        }

        await page.Locator(Menu).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    /// <summary>
    /// In an open picker, chooses the Status and waits for the position step.
    /// </summary>
    private static async Task ChooseStatusStepAsync(IPage page, string status)
    {
        await page.Locator(PickerOption, new PageLocatorOptions { HasText = status }).First.ClickAsync(new LocatorClickOptions { Timeout = Timeout });
        await page.Locator(PickerOption, new PageLocatorOptions { HasText = TopOfColumn }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    /// <summary>
    /// From the open Task detail panel: Move, the Status, then the position - the commit.
    /// </summary>
    private static async Task MoveFromPanelAsync(IPage page, string status, string position)
    {
        await PanelMoveControl(page).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
        await ChooseStatusStepAsync(page, status);
        await page.Locator(PickerOption, new PageLocatorOptions { HasText = position }).First.ClickAsync(new LocatorClickOptions { Timeout = Timeout });
    }

    private string Address(string path) => new Uri(surface.BaseAddress, path.TrimStart('/')).ToString();

    /// <summary>
    /// Installs the fake on the page once per case: a case that visits two Boards reuses the routes it installed.
    /// </summary>
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
}
