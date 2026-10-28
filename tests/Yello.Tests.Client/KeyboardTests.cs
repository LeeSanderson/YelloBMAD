using System.Text.Json;
using Microsoft.Playwright;
using Xunit;
using Yello.Contracts;
using static Yello.Tests.Client.DragHarness;
using static Yello.Tests.Client.KeyboardHarness;

namespace Yello.Tests.Client;

/// <summary>
/// Story 2.8 in a real browser: the whole Board from the keyboard. One roving tab stop; the arrows by logical direction with
/// a sticky origin index; Space picks a mover's Task up, the arrows carry it - the drop zone shown, each step said - and Space
/// drops it through story 2.6's move, Esc cancels; Space keeps its meaning everywhere else, and a Viewer traverses but never
/// carries. Alongside: the column's accessible count, repeated sentences said again, the pickers' and menu's keyboard
/// patterns, chained moves that do not refuse themselves, and what a carry ended by the Board says. AC1-AC17; UX-DR24,
/// NFR-9, FR-29, UX-DR25, UX-DR27, UX-DR38, UX-DR42, AD-29; test design X-4 (P1).
/// </summary>
/// <remarks>
/// <para>
/// <b>The API is <see cref="FakeYelloApi"/></b> with story 2.6's move harness: a drop is held, asserted optimistic, then
/// released admitted, refused or unanswered (<c>TESTING-CONVENTIONS.md</c>). No new fixture, no container.
/// </para>
/// <para>
/// <b>Keys are Playwright's keyboard</b>, pressed after the module is attached (<see cref="KeyboardHarness.WaitForKeysAsync"/>)
/// and from a known focus. Focus is polled, never slept on. Whether a key was prevented is read by a window listener that
/// runs after the Board's (<see cref="KeyboardHarness.RecordKeys"/>); what was said, by a <c>MutationObserver</c> on the
/// polite regions (<see cref="KeyboardHarness.RecordAnnouncements"/>). Right to left is forced by <c>dir="rtl"</c> on the
/// document: no product locale is right to left.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P1")]
[Trait("Requirement", "UX-DR24")]
[Trait("Requirement", "NFR-9")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class KeyboardTests(ClientSurfaceFixture surface)
{
    private const string Dialog = "[role='dialog']";
    private const string Menu = "[role='menu']";
    private const string Carried = ".task-card--carried";
    private const string DisabledAnywhere = "main [disabled], main [aria-disabled='true']";

    private const string ManageTasksCopy = "Viewers cannot edit Tasks.";
    private const string InvalidStatusCopy = "That Status is not one of this Project's Statuses.";
    private const string PositionChangedCopy = "That position has changed. Choose it again.";
    private const string NotSentCopy = "Not sent.";
    private const string NotAvailableCopy = "Not available.";

    private const string InsufficientRoleType = "urn:yello:problem:insufficient-role";
    private const string InvalidTaskStatusType = "urn:yello:problem:invalid-task-status";

    private static readonly string[] FourStatuses = ["Todo", "Doing", "Testing", "Done"];

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();
    private static readonly Guid Beacon = Guid.NewGuid();

    /// <summary>
    /// AC1, AC7, open questions 1-3 (a) - Tab reaches the Board's composite after the first column's "+" and lands on ONE
    /// card; the next Tab reaches that card's actions control and the one after leaves the row of columns; Shift+Tab walks
    /// back. Every <c>tabindex="0"</c> in the row is the active card's, and the stop follows the arrows.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Tab_reaches_one_card_and_then_leaves_the_Board()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief", "Book the room"], ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await ResetFocusAsync(page);

            var visited = new List<string>();

            for (var press = 0; press < 40 && !visited.Contains("Draft the brief"); press++)
            {
                await page.Keyboard.PressAsync("Tab");
                visited.Add(await FocusedAsync(page));
            }

            Assert.Equal("Draft the brief", visited[^1]);
            Assert.Equal("BUTTON:Add a Task", visited[^2]);
            Assert.Equal(["Draft the brief", "Draft the brief"], await TabStopsAsync(page));

            await PressAndFocusAsync(page, "Tab", "actions:Draft the brief");
            await page.Keyboard.PressAsync("Tab");

            Assert.False(await page.EvaluateAsync<bool>("() => !!document.activeElement && !!document.activeElement.closest('.board__columns')"), "A second card was a tab stop.");

            await FocusCardAsync(page, "Draft the brief");
            await PressAndFocusAsync(page, "Shift+Tab", "BUTTON:Add a Task");

            await FocusCardAsync(page, "Draft the brief");
            await PressAndFocusAsync(page, "ArrowDown", "Book the room");

            Assert.Equal(["Book the room", "Book the room"], await TabStopsAsync(page));
        }
    }

    /// <summary>
    /// AC1 - <c>↓</c> and <c>↑</c> move between the Tasks of a column and stop at its ends; the column does not scroll by the
    /// keys' default.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Up_and_down_move_within_a_column_and_stop_at_its_ends()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["First", "Second", "Third"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(RecordKeys);
            await FocusCardAsync(page, "First");

            foreach (var (key, expected) in new[] { ("ArrowDown", "Second"), ("ArrowDown", "Third"), ("ArrowDown", "Third"), ("ArrowUp", "Second"), ("ArrowUp", "First"), ("ArrowUp", "First") })
            {
                await PressAndFocusAsync(page, key, expected);
            }

            // Every arrow was the Board's - prevented, so no column or page scrolled by its default - even at the ends.
            Assert.Equal([true, true, true, true, true, true], await ArrowsAsync(page));
            Assert.Equal(0, await page.EvaluateAsync<double>("() => window.scrollY"));
        }
    }

    /// <summary>
    /// AC2 - across columns of 5, 1, 0 and 3 Tasks, a run started at the fifth Task lands on the first of one, on the empty
    /// column itself, on the third of three, and back on the fifth: the origin index is remembered and clamped, never
    /// re-derived per hop.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Left_and_right_keep_the_sticky_origin_across_columns_of_differing_lengths()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, FourStatuses, ["T1", "T2", "T3", "T4", "T5"], ["D1"], [], ["E1", "E2", "E3"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1600, 900);
            await OpenAsync(api, page, Northwind, Atlas);
            await FocusCardAsync(page, "T1");

            foreach (var (key, expected) in new[]
            {
                ("ArrowDown", "T2"), ("ArrowDown", "T3"), ("ArrowDown", "T4"), ("ArrowDown", "T5"),
                ("ArrowRight", "D1"), ("ArrowRight", "column:Testing"), ("ArrowRight", "E3"),
                ("ArrowLeft", "column:Testing"), ("ArrowLeft", "D1"), ("ArrowLeft", "T5"),
            })
            {
                await PressAndFocusAsync(page, key, expected);
            }
        }
    }

    /// <summary>
    /// AC1, UX-DR42 - under a right-to-left document the horizontal arrows mirror: <c>ArrowLeft</c> goes to the next column in
    /// the Status order and <c>ArrowRight</c> to the previous; the Status order itself never reverses.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR42")]
    public async Task Under_right_to_left_the_horizontal_arrows_mirror_and_the_Status_order_does_not()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Alpha"], ["Bravo"], ["Charlie"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync("() => { document.documentElement.dir = 'rtl'; }");
            await FocusCardAsync(page, "Alpha");

            await PressAndFocusAsync(page, "ArrowLeft", "Bravo");
            await PressAndFocusAsync(page, "ArrowLeft", "Charlie");
            await PressAndFocusAsync(page, "ArrowRight", "Bravo");

            Assert.Equal(ThreeStatuses, (await page.Locator("main .board-column h2").AllTextContentsAsync()).Select(name => name.Trim()).ToArray());
        }
    }

    /// <summary>
    /// AC3, AC5, AC9, AC10, FR-29 - Space picks the Task up (the card marked carried), <c>→</c> then <c>↓</c> carry it - the
    /// drop zone following, each step said - and Space drops it with the move held: the card is already in its new slot with
    /// both totals moved, the <c>POST …/move</c> names the Status and the Task above, never a key, never a <c>PATCH</c>, and
    /// focus is on the moved card. On admission the move sentence is said.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-29")]
    [Trait("Requirement", "UX-DR25")]
    [Trait("Requirement", "AD-29")]
    public async Task A_keyboard_carry_across_columns_is_one_optimistic_move_through_the_move_route()
    {
        var api = TwoSpaces(Northwind, Harbour);
        var tasks = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief", "Book the room"], ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(RecordAnnouncements);
            await FocusCardAsync(page, "Draft the brief");

            await page.Keyboard.PressAsync(" ");
            await CardNamed(page, "Draft the brief").And(page.Locator(Carried)).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            await page.Keyboard.PressAsync("ArrowRight");
            await WaitForDropZoneAsync(page, "Doing", "Review");
            await page.Keyboard.PressAsync("ArrowDown");
            await WaitForDropZoneAsync(page, "Doing", null);

            Assert.Equal("Draft the brief", await FocusedAsync(page));
            Assert.Empty(Moves(api));

            var held = api.HoldTaskMoveAsync();

            await page.Keyboard.PressAsync(" ");
            await held.WaitAsync(TimeSpan.FromMilliseconds(WaitMs), TestContext.Current.CancellationToken);

            Assert.Equal(["Review", "Draft the brief"], await TitlesInAsync(page, "Doing"));
            Assert.Equal(["Book the room"], await TitlesInAsync(page, "Todo"));
            Assert.Equal("2", await TotalOfAsync(page, "Doing"));
            Assert.Equal("1", await TotalOfAsync(page, "Todo"));
            Assert.Equal(0, await page.Locator(Carried).CountAsync());
            Assert.Equal(0, await page.Locator(DropZone).CountAsync());

            var sent = Assert.Single(Moves(api));

            Assert.Equal("POST", sent.Method);
            Assert.Equal(FakeYelloApi.MovePathOf(Northwind, tasks["Draft the brief"].Id), sent.Path);
            Assert.NotNull(sent.Header(Idempotency.HeaderName));

            using (var body = JsonDocument.Parse(sent.Body ?? "{}"))
            {
                Assert.Equal(StatusOf(api, Northwind, Atlas, "Doing"), body.RootElement.GetProperty("statusId").GetGuid());
                Assert.Equal(tasks["Review"].Id, body.RootElement.GetProperty("afterTaskId").GetGuid());
                Assert.False(body.RootElement.TryGetProperty("positionKey", out _), "The drop carried a position key.");
            }

            await WaitForFocusAsync(page, "Draft the brief");

            api.ReleaseTaskMove();

            await WaitUntilSaidAsync(page, "Moved to Doing, position 2 of 2.");

            Assert.Equal(
                ["Picked up Draft the brief. Todo, position 1 of 2.", "Doing, position 1 of 2.", "Doing, position 2 of 2.", "Moved to Doing, position 2 of 2."],
                await SaidAsync(page));
            Assert.Equal("Draft the brief", await FocusedAsync(page));
        }
    }

    /// <summary>
    /// AC5, AC10 - picked up at position 3, the Task enters a column of 1 at its bottom (position 2 of 2), a column of 5 at
    /// position 3 (the sticky ordinal), and <c>↑</c> adjusts it to position 2 before the drop, which names the Task above.
    /// Each step is said, in order.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR25")]
    public async Task A_carried_Task_lands_at_the_same_ordinal_clamped_to_each_column()
    {
        var api = TwoSpaces(Northwind, Harbour);
        var tasks = Seed(api, Northwind, Atlas, ThreeStatuses, ["T1", "T2", "T3", "T4"], ["D1"], ["E1", "E2", "E3", "E4", "E5"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 1100);
            await OpenAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(RecordAnnouncements);
            await FocusCardAsync(page, "T3");

            foreach (var key in new[] { " ", "ArrowRight", "ArrowRight", "ArrowUp", " " })
            {
                await page.Keyboard.PressAsync(key);
            }

            await WaitUntilSaidAsync(page, "Moved to Done, position 2 of 6.");

            Assert.Equal(
                ["Picked up T3. Todo, position 3 of 4.", "Doing, position 2 of 2.", "Done, position 3 of 6.", "Done, position 2 of 6.", "Moved to Done, position 2 of 6."],
                await SaidAsync(page));

            var sent = Assert.Single(Moves(api));

            using var body = JsonDocument.Parse(sent.Body ?? "{}");

            Assert.Equal(tasks["E1"].Id, body.RootElement.GetProperty("afterTaskId").GetGuid());
        }
    }

    /// <summary>
    /// AC5 - carried into an empty column the Task takes its top, and the drop names no Task above (<c>afterTaskId: null</c>).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Into_an_empty_column_a_carried_Task_lands_at_the_top()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief"], [], []);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(RecordAnnouncements);
            await FocusCardAsync(page, "Draft the brief");

            await page.Keyboard.PressAsync(" ");
            await page.Keyboard.PressAsync("ArrowRight");
            await WaitForDropZoneAsync(page, "Doing", null);
            await page.Keyboard.PressAsync(" ");

            await WaitUntilSaidAsync(page, "Moved to Doing, position 1 of 1.");

            var sent = Assert.Single(Moves(api));

            using var body = JsonDocument.Parse(sent.Body ?? "{}");

            Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("afterTaskId").ValueKind);
        }
    }

    /// <summary>
    /// AC6, AC9, story 2.7 Q5 - a drop on the Task's own slot, and Esc mid-carry, send nothing and say the return; focus stays
    /// on the card, nothing else opens, and the carry's Esc goes no further than the Board (the innermost meaning).
    /// </summary>
    /// <param name="escape">Whether the carry is cancelled by Esc after a step (otherwise dropped where it was).</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dropping_on_its_own_slot_or_Esc_sends_nothing_and_says_the_return(bool escape)
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief", "Book the room"], ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(RecordAnnouncements);
            await page.EvaluateAsync(RecordKeys);
            await FocusCardAsync(page, "Book the room");

            await page.Keyboard.PressAsync(" ");
            await page.Locator(Carried).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            if (escape)
            {
                await page.Keyboard.PressAsync("ArrowRight");
                await WaitForDropZoneAsync(page, "Doing", null);
                await page.Keyboard.PressAsync("Escape");
            }
            else
            {
                await page.Keyboard.PressAsync(" ");
            }

            await WaitUntilSaidAsync(page, "Returned to Todo, position 2.");

            Assert.Empty(Moves(api));
            Assert.Equal(["Draft the brief", "Book the room"], await TitlesInAsync(page, "Todo"));
            Assert.Equal(0, await page.Locator(Carried).CountAsync());
            Assert.Equal(0, await page.Locator(DropZone).CountAsync());
            Assert.Equal(0, await page.Locator(Dialog).CountAsync());
            Assert.Equal(0, await page.Locator(Menu).CountAsync());
            Assert.Equal(0, await EscapesAsync(page));
            Assert.Equal("Book the room", await FocusedAsync(page));
        }
    }

    /// <summary>
    /// AC9 - a refused keyboard drop follows 2.6's outcome: the card and both totals go back, the refusal is said on the
    /// Board notice, the return is announced, and focus stays on the card by its identity; <c>409</c> reads the Board again.
    /// </summary>
    /// <param name="status">The refusal's status.</param>
    /// <param name="type">Its problem type.</param>
    /// <param name="capability">The capability a <c>403</c> names.</param>
    /// <param name="copy">What the Board says.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(403, InsufficientRoleType, "manage-tasks", ManageTasksCopy)]
    [InlineData(400, InvalidTaskStatusType, null, InvalidStatusCopy)]
    [InlineData(409, FakeYelloApi.TaskPositionUnavailableType, null, PositionChangedCopy)]
    [Trait("Requirement", "AD-29")]
    public async Task A_refused_keyboard_drop_puts_the_card_back_and_keeps_focus_on_it(int status, string type, string? capability, string copy)
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief", "Book the room"], ["Review", "Ship"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await FocusCardAsync(page, "Draft the brief");

            await page.Keyboard.PressAsync(" ");
            await page.Keyboard.PressAsync("ArrowRight");
            await page.Keyboard.PressAsync("ArrowDown");
            await WaitForDropZoneAsync(page, "Doing", "Ship");

            var held = api.HoldTaskMoveAsync();
            var reads = BoardReads(api);

            await page.Keyboard.PressAsync(" ");
            await held.WaitAsync(TimeSpan.FromMilliseconds(WaitMs), TestContext.Current.CancellationToken);

            Assert.Equal(["Review", "Draft the brief", "Ship"], await TitlesInAsync(page, "Doing"));

            api.ReleaseTaskMoveRefused(status, type, capability);

            await page.Locator(BoardNotice, new PageLocatorOptions { HasText = copy }).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Returned to Todo, position 1." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = WaitMs });

            Assert.Equal(["Draft the brief", "Book the room"], await TitlesInAsync(page, "Todo"));
            Assert.Equal("2", await TotalOfAsync(page, "Doing"));
            await WaitForFocusAsync(page, "Draft the brief");

            if (status == 409)
            {
                Assert.True(BoardReads(api) > reads, "A 409 did not read the Board again.");
            }
        }
    }

    /// <summary>
    /// AC9 - a keyboard drop that gets no answer says "Not sent." on the Board and reverts; one answered <c>404</c> follows the
    /// Board's refusal surface.
    /// </summary>
    /// <param name="notFound">Whether the answer is a <c>404</c> (otherwise no answer).</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_unanswered_or_not_found_keyboard_drop_follows_the_moves_outcomes(bool notFound)
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief", "Book the room"], ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await FocusCardAsync(page, "Draft the brief");

            await page.Keyboard.PressAsync(" ");
            await page.Keyboard.PressAsync("ArrowRight");
            await WaitForDropZoneAsync(page, "Doing", "Review");

            if (notFound)
            {
                api.NextTaskMoveRefusal = (404, ProblemTypes.NotAvailable, null);

                await page.Keyboard.PressAsync(" ");
                await page.Locator("main").GetByText(NotAvailableCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

                Assert.Single(Moves(api));
                return;
            }

            var held = api.HoldTaskMoveAsync();

            await page.Keyboard.PressAsync(" ");
            await held.WaitAsync(TimeSpan.FromMilliseconds(WaitMs), TestContext.Current.CancellationToken);
            api.ReleaseTaskMoveUnanswered();

            await page.Locator(BoardNotice, new PageLocatorOptions { HasText = NotSentCopy }).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Returned to Todo, position 1." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = WaitMs });

            Assert.Equal(["Draft the brief", "Book the room"], await TitlesInAsync(page, "Todo"));
        }
    }

    /// <summary>
    /// AC4 - Space keeps its native meaning off a mover's card: on the actions control it opens the menu, on "+" it opens the
    /// creation field, and on a column it is not prevented; only on a mover's card is it prevented, and there it picks up.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Space_keeps_its_native_meaning_everywhere_but_a_movers_card()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief"], [], []);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(RecordKeys);

            var card = CardNamed(page, "Draft the brief");

            await card.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Actions for Draft the brief" }).FocusAsync();
            await page.Keyboard.PressAsync(" ");
            await page.Locator(Menu).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            await page.Keyboard.PressAsync("Escape");
            await page.Locator(Menu).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = WaitMs });

            await ColumnNamed(page, "Todo").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Add a Task", Exact = true }).FocusAsync();
            await page.Keyboard.PressAsync(" ");
            await ColumnNamed(page, "Todo").Locator("input[type='text']").WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            await ColumnNamed(page, "Doing").EvaluateAsync("column => column.focus()");
            await page.Keyboard.PressAsync(" ");

            Assert.Equal([false, false, false], await SpacesAsync(page));
            Assert.Equal(0, await page.Locator(Carried).CountAsync());

            await FocusCardAsync(page, "Draft the brief");
            await page.Keyboard.PressAsync(" ");
            await page.Locator(Carried).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            Assert.Equal([false, false, false, true], await SpacesAsync(page));
            Assert.Equal(0, await page.Locator(Dialog).CountAsync());

            await page.Keyboard.PressAsync("Escape");
        }
    }

    /// <summary>
    /// AC11, UX-DR27 - a Viewer traverses the Board exactly as a Member does, and Space on a card keeps its native meaning: it
    /// opens the Task. No pick-up, carried card, drop zone or move ever happens, and nothing is disabled.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    public async Task A_Viewer_traverses_the_Board_and_Space_opens_the_Task_without_a_pick_up()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Harbour, Beacon, ThreeStatuses, ["First", "Second"], ["Third"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Harbour, Beacon);
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            await page.EvaluateAsync(RecordKeys);
            await FocusCardAsync(page, "First");

            await PressAndFocusAsync(page, "ArrowDown", "Second");
            await PressAndFocusAsync(page, "ArrowRight", "Third");

            await page.Keyboard.PressAsync(" ");
            await page.Locator(Dialog).GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = "Third" }).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            Assert.Equal([false], await SpacesAsync(page));
            Assert.Equal(0, await page.Locator(Carried).CountAsync());
            Assert.Equal(0, await page.Locator(DropZone).CountAsync());
            Assert.Empty(Moves(api));
            Assert.Equal(0, await page.Locator(DisabledAnywhere).CountAsync());
        }
    }

    /// <summary>
    /// AC8, X-4 - the keyboard-parity matrix: every Board operation available by pointer has a keyboard route, from a Tab into
    /// the Board - open a Task (Enter), open its menu (Shift+F10), create a Task ("+" by Shift+Tab, Enter, type, Enter), move
    /// within a column and across columns (Space, an arrow, Space), and reach every Task (the arrows).
    /// </summary>
    /// <param name="operation">The pointer operation.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("open-task")]
    [InlineData("open-card-menu")]
    [InlineData("create-task")]
    [InlineData("move-within-a-column")]
    [InlineData("move-across-columns")]
    [InlineData("reach-every-task")]
    [Trait("Requirement", "FR-29")]
    public async Task Every_pointer_operation_on_the_Board_has_a_keyboard_route(string operation)
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief", "Book the room"], ["Review"], ["Ship"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(RecordAnnouncements);
            await ResetFocusAsync(page);
            await TabIntoTheBoardAsync(page, "Draft the brief");

            switch (operation)
            {
                case "open-task":
                    await page.Keyboard.PressAsync("Enter");
                    await page.Locator(Dialog).GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = "Draft the brief" }).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
                    break;

                case "open-card-menu":
                    await page.Keyboard.PressAsync("Shift+F10");
                    await page.Locator(Menu).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
                    break;

                case "create-task":
                    await PressAndFocusAsync(page, "Shift+Tab", "BUTTON:Add a Task");
                    await page.Keyboard.PressAsync("Enter");
                    await page.WaitForFunctionAsync("() => document.activeElement && document.activeElement.matches('input[type=text]')", null, new PageWaitForFunctionOptions { Timeout = WaitMs });
                    await page.Keyboard.TypeAsync("By keyboard");
                    await page.Keyboard.PressAsync("Enter");
                    await CardNamed(page, "By keyboard").WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
                    break;

                case "move-within-a-column":
                    await page.Keyboard.PressAsync(" ");
                    await page.Keyboard.PressAsync("ArrowDown");
                    await page.Keyboard.PressAsync(" ");
                    await WaitUntilSaidAsync(page, "Moved to Todo, position 2 of 2.");
                    Assert.Equal(["Book the room", "Draft the brief"], await TitlesInAsync(page, "Todo"));
                    break;

                case "move-across-columns":
                    await page.Keyboard.PressAsync(" ");
                    await page.Keyboard.PressAsync("ArrowRight");
                    await page.Keyboard.PressAsync(" ");
                    await WaitUntilSaidAsync(page, "Moved to Doing, position 1 of 2.");
                    Assert.Equal(["Draft the brief", "Review"], await TitlesInAsync(page, "Doing"));
                    break;

                default:
                    var reached = new HashSet<string>(StringComparer.Ordinal) { await FocusedAsync(page) };

                    foreach (var (key, expected) in new[] { ("ArrowDown", "Book the room"), ("ArrowRight", "Review"), ("ArrowRight", "Ship") })
                    {
                        await PressAndFocusAsync(page, key, expected);
                        reached.Add(expected);
                    }

                    Assert.Equal(["Book the room", "Draft the brief", "Review", "Ship"], reached.Order(StringComparer.Ordinal).ToArray());
                    break;
            }
        }
    }

    /// <summary>
    /// AC13, AD-29 - each column is named by its Status and described by its TRUE total with its unit - "7 Tasks" for a
    /// column showing two of seven, "1 Task", "0 Tasks" - the chip's bare number hidden from assistive technology; each card's
    /// list item states its position and the column's true total.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-29")]
    public async Task Each_column_is_named_by_its_Status_and_described_by_its_true_total()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief", "Book the room"], ["Review"], []);
        api.Boards[(Northwind, Atlas)].Columns[0].Total = 7;
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);

            var columns = await page.EvaluateAsync<string[]>(
                """
                () => [...document.querySelectorAll('main section.board-column')].map(section => {
                    const name = document.getElementById(section.getAttribute('aria-labelledby')).textContent.trim();
                    const description = document.getElementById(section.getAttribute('aria-describedby')).textContent.trim();
                    const chip = section.querySelector('.column-count').getAttribute('aria-hidden');
                    const cards = [...section.querySelectorAll('li.task-card')].map(card => card.getAttribute('aria-posinset') + '/' + card.getAttribute('aria-setsize'));
                    return [name, description, chip, ...cards].join(' ');
                })
                """);

            Assert.Equal(["Todo 7 Tasks true 1/7 2/7", "Doing 1 Task true 1/1", "Done 0 Tasks true"], columns);
        }
    }

    /// <summary>
    /// AC14 - on the Board, the same sentence twice in a row is said twice: two "Task added." and two "Returned to …" from
    /// two cancelled carries each change what the polite regions hold.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR25")]
    public async Task On_the_Board_a_sentence_said_twice_is_announced_twice()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(RecordAnnouncements);

            await ColumnNamed(page, "Todo").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Add a Task", Exact = true }).ClickAsync();

            var field = ColumnNamed(page, "Todo").Locator("input[type='text']");

            foreach (var title in new[] { "Second", "Third" })
            {
                await field.FillAsync(title);
                await field.PressAsync("Enter");
                await CardNamed(page, title).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            }

            await WaitUntilSaidAsync(page, "Task added.", 2);
            await field.PressAsync("Escape");

            for (var carry = 0; carry < 2; carry++)
            {
                await FocusCardAsync(page, "Draft the brief");
                await page.Keyboard.PressAsync(" ");
                await page.Locator(Carried).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
                await page.Keyboard.PressAsync("Escape");
                await page.Locator(Carried).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = WaitMs });
            }

            await WaitUntilSaidAsync(page, "Returned to Todo, position 1.", 2);
        }
    }

    /// <summary>
    /// AC14 - on Space home, two Projects deleted one after the other are each announced: "Project deleted." twice.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task On_Space_home_a_second_Project_deleted_is_announced_again()
    {
        var api = TwoSpaces(Northwind, Harbour);

        api.ServeProjectTaskCounts = true;
        api.Projects[Northwind] = [new FakeProject(Atlas, "Atlas", DateTimeOffset.UtcNow), new FakeProject(Beacon, "Beacon", DateTimeOffset.UtcNow)];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(new Uri(surface.BaseAddress, $"spaces/{Northwind:D}").ToString());
            await page.Locator("main li", new PageLocatorOptions { HasText = "Beacon" }).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            await page.EvaluateAsync(RecordAnnouncements);

            foreach (var name in new[] { "Atlas", "Beacon" })
            {
                var row = page.Locator("main li", new PageLocatorOptions { HasText = name });

                await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete Project" }).ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
                await row.Locator(".destructive-confirm .button-danger").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
                await row.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = WaitMs });
            }

            await WaitUntilSaidAsync(page, "Project deleted.", 2);
        }
    }

    /// <summary>
    /// AC14 - in Space settings, two Labels deleted one after the other are each announced: "Label deleted." twice.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task In_Space_settings_a_second_Label_deleted_is_announced_again()
    {
        var api = TwoSpaces(Northwind, Harbour);

        api.Labels[Northwind] = [new FakeLabel(Guid.NewGuid(), "Alpha", "red"), new FakeLabel(Guid.NewGuid(), "Bravo", "blue")];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(new Uri(surface.BaseAddress, $"spaces/{Northwind:D}/settings").ToString());
            await page.Locator("main li", new PageLocatorOptions { HasText = "Bravo" }).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            await page.EvaluateAsync(RecordAnnouncements);

            foreach (var name in new[] { "Alpha", "Bravo" })
            {
                var row = page.Locator("main li", new PageLocatorOptions { HasText = name });

                await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete Label" }).ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
                await page.Locator(".destructive-confirm .button-danger").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
                await row.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = WaitMs });
            }

            await WaitUntilSaidAsync(page, "Label deleted.", 2);
        }
    }

    /// <summary>
    /// AC15 - the Status picker is one tab stop: <c>↓</c>/<c>↑</c> move between its options and wrap, Home and End go to the
    /// first and last, focusing never selects, and Space chooses without scrolling the page (it is prevented).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR11")]
    public async Task The_Status_picker_moves_by_arrows_Home_and_End_and_Space_chooses()
    {
        var api = TwoSpaces(Northwind, Harbour);
        var tasks = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(RecordKeys);
            await page.Locator($".task-card button[data-task-id='{tasks["Draft the brief"].Id:D}']").ClickAsync();

            var dialog = page.Locator(Dialog);

            await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Status" }).ClickAsync();

            var options = dialog.Locator("[role='listbox'] [role='option']");

            await options.First.WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            Assert.Equal(1, await dialog.Locator("[role='listbox'] [role='option'][tabindex='0']").CountAsync());
            Assert.Equal(0, await dialog.Locator("[role='listbox'] [aria-selected='true']").CountAsync());

            await page.Keyboard.PressAsync("Tab");
            await WaitForFocusAsync(page, "LI:Todo");

            foreach (var (key, expected) in new[] { ("ArrowDown", "LI:Doing"), ("End", "LI:Done"), ("ArrowDown", "LI:Todo"), ("ArrowUp", "LI:Done"), ("Home", "LI:Todo"), ("ArrowDown", "LI:Doing") })
            {
                await PressAndFocusAsync(page, key, expected);
            }

            Assert.Equal(0, await dialog.Locator("[role='listbox'] [aria-selected='true']").CountAsync());
            Assert.DoesNotContain(api.Requests, request => request.Method == "PATCH");

            await page.Keyboard.PressAsync(" ");
            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Moved to Doing, position 1 of 1." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = WaitMs });

            Assert.Equal([true], await SpacesAsync(page));
        }
    }

    /// <summary>
    /// AC15 - the Label picker is one tab stop: the arrows move between Labels, Space toggles the focused one without scrolling
    /// the page, and only the applied Labels are selected.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR11")]
    public async Task The_Label_picker_moves_by_arrows_and_Space_toggles()
    {
        var api = TwoSpaces(Northwind, Harbour);
        var alpha = new FakeLabel(Guid.NewGuid(), "Alpha", "red");
        var bravo = new FakeLabel(Guid.NewGuid(), "Bravo", "blue");

        api.Labels[Northwind] = [alpha, bravo];

        var tasks = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief"]);
        var (column, task) = api.FindTask(tasks["Draft the brief"].Id)!.Value;

        column.Tasks[0] = task with { LabelIds = [alpha.Id] };

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(RecordKeys);
            await page.Locator($".task-card button[data-task-id='{task.Id:D}']").ClickAsync();

            var dialog = page.Locator(Dialog);

            await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Labels" }).ClickAsync();
            await dialog.Locator("[role='listbox'] [role='option']").First.WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            Assert.Equal(1, await dialog.Locator("[role='listbox'] [role='option'][tabindex='0']").CountAsync());
            Assert.Equal(["true", "false"], await dialog.Locator("[role='listbox'] [role='option']").EvaluateAllAsync<string[]>("options => options.map(option => option.getAttribute('aria-selected'))"));

            await dialog.Locator("[role='listbox'] [role='option'][tabindex='0']").FocusAsync();
            await PressAndFocusAsync(page, "ArrowDown", "LI:Bravo");
            await page.Keyboard.PressAsync(" ");

            await page.WaitForFunctionAsync(
                "() => [...document.querySelectorAll('[role=dialog] [role=listbox] [role=option]')].every(option => option.getAttribute('aria-selected') === 'true')",
                null,
                new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });

            Assert.Equal([true], await SpacesAsync(page));
            Assert.Equal("LI:Bravo", await FocusedAsync(page));
        }
    }

    /// <summary>
    /// AC15 - the card's menu follows the menu pattern (<c>↓</c>/<c>↑</c> wrap, Home/End), and the Move picker it leads to the
    /// listbox pattern: from the list, <c>↓</c> reaches the first option, End the last, and Space chooses a Status, then a
    /// position - the move.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR11")]
    [Trait("Requirement", "UX-DR21")]
    public async Task The_card_menu_and_the_Move_picker_follow_their_keyboard_patterns()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief"], ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(RecordKeys);
            await FocusCardAsync(page, "Draft the brief");

            await page.Keyboard.PressAsync("Shift+F10");
            await WaitForFocusAsync(page, "BUTTON:Open");

            Assert.Equal(1, await page.Locator($"{Menu} [role='menuitem'][tabindex='0']").CountAsync());

            foreach (var (key, expected) in new[] { ("ArrowDown", "BUTTON:Move"), ("ArrowDown", "BUTTON:Open"), ("End", "BUTTON:Move"), ("Home", "BUTTON:Open"), ("ArrowUp", "BUTTON:Move") })
            {
                await PressAndFocusAsync(page, key, expected);
            }

            await page.Keyboard.PressAsync("Enter");
            await page.Locator(".move-picker [role='option']").First.WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            // Focus opens on the list itself: Space there chooses nothing and scrolls nothing.
            await page.WaitForFunctionAsync("() => document.activeElement?.matches('.move-picker')", null, new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });
            await page.Keyboard.PressAsync(" ");

            Assert.Equal(0, await page.Locator(".move-picker [role='option']", new PageLocatorOptions { HasText = "Top of column" }).CountAsync());

            await PressAndFocusAsync(page, "ArrowDown", "LI:Todo Current");
            await PressAndFocusAsync(page, "End", "LI:Done");
            await PressAndFocusAsync(page, "ArrowUp", "LI:Doing");

            Assert.Equal(1, await page.Locator(".move-picker [role='option'][tabindex='0']").CountAsync());
            Assert.Equal(0, await page.Locator(".move-picker [aria-selected='true']").CountAsync());

            await page.Keyboard.PressAsync(" ");
            await page.Locator(".move-picker [role='option']", new PageLocatorOptions { HasText = "Top of column" }).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            await PressAndFocusAsync(page, "ArrowDown", "LI:Top of column");
            await page.Keyboard.PressAsync(" ");

            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Moved to Doing, position 1 of 2." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = WaitMs });

            Assert.Equal([true, true, true], await SpacesAsync(page));
        }
    }

    /// <summary>
    /// AC16 - Task A's move held, Task B carried to directly after A: B's move waits for A's answer rather than being sent
    /// while the server has not admitted A there - it is sent once A's is in, and admitted, with nothing refused.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_move_chained_after_one_still_out_waits_for_it_and_is_admitted()
    {
        var api = TwoSpaces(Northwind, Harbour);
        var tasks = Seed(api, Northwind, Atlas, ThreeStatuses, ["Alpha", "Bravo", "Charlie"], ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(RecordAnnouncements);

            var held = api.HoldTaskMoveAsync();

            await FocusCardAsync(page, "Alpha");
            await page.Keyboard.PressAsync(" ");
            await page.Keyboard.PressAsync("ArrowRight");
            await WaitForDropZoneAsync(page, "Doing", "Review");
            await page.Keyboard.PressAsync(" ");
            await held.WaitAsync(TimeSpan.FromMilliseconds(WaitMs), TestContext.Current.CancellationToken);

            await FocusCardAsync(page, "Bravo");
            await page.Keyboard.PressAsync(" ");
            await page.Keyboard.PressAsync("ArrowRight");
            await WaitForDropZoneAsync(page, "Doing", "Alpha");
            await page.Keyboard.PressAsync("ArrowDown");
            await WaitForDropZoneAsync(page, "Doing", "Review");
            await page.Keyboard.PressAsync(" ");

            await page.WaitForFunctionAsync(
                "() => [...document.querySelectorAll('main .board-column')][1].querySelectorAll('.task-card').length === 3",
                null,
                new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });

            Assert.Equal(["Alpha", "Bravo", "Review"], await TitlesInAsync(page, "Doing"));
            Assert.Single(Moves(api));

            api.ReleaseTaskMove();

            await WaitUntilSaidAsync(page, "Moved to Doing, position 2 of 3.");

            var sent = Moves(api);

            Assert.Equal(2, sent.Count);

            using (var body = JsonDocument.Parse(sent[1].Body ?? "{}"))
            {
                Assert.Equal(tasks["Alpha"].Id, body.RootElement.GetProperty("afterTaskId").GetGuid());
            }

            Assert.Equal(string.Empty, (await page.Locator(BoardNotice).TextContentAsync())?.Trim());
            Assert.Equal(["Alpha", "Bravo", "Review"], await TitlesInAsync(page, "Doing"));
        }
    }

    /// <summary>
    /// AC17, open question 10 (a) - a carry under a Board read again (another Task's move refused <c>409</c>) ends with
    /// nothing sent for it, and its return is said.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_carry_under_a_Board_read_again_ends_and_says_the_return()
    {
        var api = TwoSpaces(Northwind, Harbour);
        var tasks = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief", "Book the room"], ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(RecordAnnouncements);

            var held = api.HoldTaskMoveAsync();

            await FocusCardAsync(page, "Book the room");
            await page.Keyboard.PressAsync(" ");
            await page.Keyboard.PressAsync("ArrowRight");
            await WaitForDropZoneAsync(page, "Doing", null);
            await page.Keyboard.PressAsync(" ");
            await held.WaitAsync(TimeSpan.FromMilliseconds(WaitMs), TestContext.Current.CancellationToken);

            await FocusCardAsync(page, "Draft the brief");
            await page.Keyboard.PressAsync(" ");
            await page.Locator(Carried).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            api.ReleaseTaskMoveRefused(409, FakeYelloApi.TaskPositionUnavailableType);

            await WaitUntilSaidAsync(page, "Returned to Todo, position 1.");
            await page.Locator(Carried).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = WaitMs });

            Assert.Equal(0, await page.Locator(DropZone).CountAsync());
            Assert.DoesNotContain(Moves(api), move => move.Path == FakeYelloApi.MovePathOf(Northwind, tasks["Draft the brief"].Id));
        }
    }

    /// <summary>
    /// AC17, open question 10 (a) - a pointer lift under a Board read again ends too, with nothing sent for it, and its return
    /// is said, as a keyboard carry's is.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_pointer_lift_under_a_Board_read_again_ends_and_says_the_return()
    {
        var api = TwoSpaces(Northwind, Harbour);
        var tasks = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief", "Book the room"], ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(RecordAnnouncements);

            var held = api.HoldTaskMoveAsync();

            await FocusCardAsync(page, "Book the room");
            await page.Keyboard.PressAsync(" ");
            await page.Keyboard.PressAsync("ArrowRight");
            await WaitForDropZoneAsync(page, "Doing", null);
            await page.Keyboard.PressAsync(" ");
            await held.WaitAsync(TimeSpan.FromMilliseconds(WaitMs), TestContext.Current.CancellationToken);

            await LiftWithMouseAsync(page, "Draft the brief");

            api.ReleaseTaskMoveRefused(409, FakeYelloApi.TaskPositionUnavailableType);

            await WaitUntilSaidAsync(page, "Returned to Todo, position 1.");
            await page.Locator(Lifted).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = WaitMs });
            await page.Mouse.UpAsync();

            Assert.DoesNotContain(Moves(api), move => move.Path == FakeYelloApi.MovePathOf(Northwind, tasks["Draft the brief"].Id));
        }
    }

    /// <summary>
    /// AC6, open question 4 (a), Task 4 - during a carry Shift+F10 and the context-menu key do nothing, and Tab ends the carry
    /// as a cancel: nothing sent, the return said, the card in its slot, and focus goes on.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task During_a_carry_the_menu_keys_do_nothing_and_Tab_cancels_it()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief", "Book the room"], ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(RecordAnnouncements);
            await FocusCardAsync(page, "Book the room");

            await page.Keyboard.PressAsync(" ");
            await page.Keyboard.PressAsync("ArrowRight");
            await WaitForDropZoneAsync(page, "Doing", null);

            await page.Keyboard.PressAsync("Shift+F10");
            await page.Keyboard.PressAsync("ContextMenu");

            Assert.Equal(0, await page.Locator(Menu).CountAsync());
            Assert.Equal(1, await page.Locator(Carried).CountAsync());
            Assert.Equal("Book the room", await FocusedAsync(page));

            await PressAndFocusAsync(page, "Tab", "actions:Book the room");
            await WaitUntilSaidAsync(page, "Returned to Todo, position 2.");

            Assert.Empty(Moves(api));
            Assert.Equal(0, await page.Locator(Carried).CountAsync());
            Assert.Equal(0, await page.Locator(DropZone).CountAsync());
            Assert.Equal(0, await page.Locator(Menu).CountAsync());
            Assert.Equal(["Draft the brief", "Book the room"], await TitlesInAsync(page, "Todo"));
        }
    }

    /// <summary>
    /// AC17, open question 10 (a) - a carry ended by leaving the Space (the purge) says nothing: the Space has gone.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_carry_ended_by_leaving_the_Space_says_nothing()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief"], ["Review"]);
        _ = Seed(api, Harbour, Beacon, ThreeStatuses, ["Elsewhere"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(RecordAnnouncements);
            await FocusCardAsync(page, "Draft the brief");
            await page.Keyboard.PressAsync(" ");
            await page.Locator(Carried).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            // In-app navigation with focus still on the carried card, so the carry is ended by the purge and nothing else.
            await page.EvaluateAsync("id => Blazor.navigateTo('/spaces/' + id)", Harbour.ToString("D"));
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            Assert.DoesNotContain(await SaidAsync(page), said => said.StartsWith("Returned", StringComparison.Ordinal));
            Assert.Empty(Moves(api));
        }
    }

    /// <summary>
    /// AC10, UX-DR38 - nothing is announced on plain traversal or on load: after "Board loaded." the arrows say nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR38")]
    public async Task Nothing_is_announced_on_traversal_or_on_load()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief", "Book the room"], ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync(RecordAnnouncements);
            await FocusCardAsync(page, "Draft the brief");

            await PressAndFocusAsync(page, "ArrowDown", "Book the room");
            await PressAndFocusAsync(page, "ArrowRight", "Review");
            await PressAndFocusAsync(page, "ArrowLeft", "Book the room");

            Assert.Empty(await SaidAsync(page));
            Assert.Equal("Board loaded.", string.Join(string.Empty, await page.Locator(LiveRegion).AllTextContentsAsync()).Trim());
        }
    }

    private static int BoardReads(FakeYelloApi api) =>
        api.Requests.Count(request => request.Method == "GET" && request.Path.EndsWith("/board", StringComparison.Ordinal));

    /// <summary>
    /// Presses Tab from the top of the document until focus is on <paramref name="title"/>'s card.
    /// </summary>
    private static async Task TabIntoTheBoardAsync(IPage page, string title)
    {
        for (var press = 0; press < 40; press++)
        {
            await page.Keyboard.PressAsync("Tab");

            if (await FocusedAsync(page) == title)
            {
                return;
            }
        }

        Assert.Fail($"Tab never reached '{title}'.");
    }

    /// <summary>
    /// Opens the Board and waits until its keyboard is attached.
    /// </summary>
    private async Task OpenAsync(FakeYelloApi api, IPage page, Guid spaceId, Guid projectId)
    {
        await api.InstallAsync(page);
        await OpenBoardAsync(page, surface.BaseAddress, spaceId, projectId);
        await WaitForKeysAsync(page);
    }
}
