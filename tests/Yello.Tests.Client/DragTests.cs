using System.Text.Json;
using Microsoft.Playwright;
using Xunit;
using Yello.Contracts;
using static Yello.Tests.Client.DragHarness;

namespace Yello.Tests.Client;

/// <summary>
/// Story 2.7 in a real browser, by pointer: press and drag a Task to lift it, a dashed drop zone marking the destination,
/// and the drop committing through story 2.6's move - optimistic, announced, reverted out loud when refused. Dragging back
/// to the origin, releasing outside every column and <c>Esc</c> move nothing; a press that never moved is still a click;
/// a Viewer gets no drag at all. AC1, AC5-AC7, AC9-AC15; UX-DR22, UX-DR6, UX-DR4, UX-DR5, UX-DR8, UX-DR21, UX-DR25,
/// UX-DR27, FR-29.
/// </summary>
/// <remarks>
/// <para>
/// <b>The API is <see cref="FakeYelloApi"/></b> with story 2.6's move harness: a drop can be held, then released admitted,
/// refused or unanswered - hold, assert the new state, release, assert (<c>TESTING-CONVENTIONS.md</c>).
/// </para>
/// <para>
/// <b>The pointer is Playwright's mouse</b>: press, move past the drag-start distance (the 10px slop token, Q3 (a)), move
/// in steps, release. The touch path - long-press, slop and pan - is <see cref="TouchDragTests"/>, in a collection of its
/// own because it measures time.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "UX-DR22")]
[Trait("Requirement", "FR-29")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class DragTests(ClientSurfaceFixture surface)
{
    private const string Dialog = "[role='dialog']";
    private const string Menu = "[role='menu']";
    private const string DisabledAnywhere = "main [disabled], main [aria-disabled='true']";

    private const string ManageTasksCopy = "Viewers cannot edit Tasks.";
    private const string InvalidStatusCopy = "That Status is not one of this Project's Statuses.";
    private const string PositionChangedCopy = "That position has changed. Choose it again.";
    private const string NotSentCopy = "Not sent.";
    private const string NotAvailableCopy = "Not available.";

    private const string InsufficientRoleType = "urn:yello:problem:insufficient-role";
    private const string InvalidTaskStatusType = "urn:yello:problem:invalid-task-status";

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();
    private static readonly Guid Beacon = Guid.NewGuid();

    /// <summary>
    /// AC1, AC9, AC12, AC14 - a pointer drag across columns: mid-drag the card is lifted (2px border, the hard offset
    /// shadow, a -1deg rotation) and a dashed drop zone sits in the destination before Ship; dropped with the move held, the
    /// card is already after Review with both totals moved, and the <c>POST …/move</c> names the Status and Review, never a
    /// key; on admission "Moved to Doing, position 2 of 3." is announced.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR4")]
    [Trait("Requirement", "UX-DR5")]
    [Trait("Requirement", "UX-DR25")]
    public async Task A_pointer_drag_lifts_marks_the_drop_zone_and_moves_the_Task_through_the_move_route()
    {
        var api = TwoSpaces(Northwind, Harbour);
        var tasks = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief", "Book the room"], ["Review", "Ship"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);

            await LiftWithMouseAsync(page, "Draft the brief");
            await MoveMouseToAsync(page, await PointInAsync(CardNamed(page, "Review"), 0.8f));
            await WaitForDropZoneAsync(page, "Doing", "Ship");

            var lifted = await page.Locator(Lifted).EvaluateAsync<JsonElement>(
                "card => { const s = getComputedStyle(card); return { border: s.borderTopWidth, shadow: s.boxShadow, rotate: s.rotate, transform: s.transform, title: card.querySelector('.task-card__title').textContent.trim() }; }");

            Assert.Equal("Draft the brief", lifted.GetProperty("title").GetString());
            Assert.Equal("2px", lifted.GetProperty("border").GetString());
            Assert.Contains("6px", lifted.GetProperty("shadow").GetString(), StringComparison.Ordinal);
            Assert.Equal("-1deg", lifted.GetProperty("rotate").GetString());

            var zone = page.Locator(DropZone);

            Assert.Equal(1, await zone.CountAsync());
            Assert.Equal("dashed", await zone.EvaluateAsync<string>("z => getComputedStyle(z).borderTopStyle"));
            Assert.Empty(Moves(api));

            var held = api.HoldTaskMoveAsync();

            await page.Mouse.UpAsync();
            await held.WaitAsync(TimeSpan.FromMilliseconds(WaitMs), TestContext.Current.CancellationToken);

            Assert.Equal(["Review", "Draft the brief", "Ship"], await TitlesInAsync(page, "Doing"));
            Assert.Equal(["Book the room"], await TitlesInAsync(page, "Todo"));
            Assert.Equal("3", await TotalOfAsync(page, "Doing"));
            Assert.Equal("1", await TotalOfAsync(page, "Todo"));
            Assert.Equal(0, await page.Locator(Lifted).CountAsync());
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

            api.ReleaseTaskMove();

            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Moved to Doing, position 2 of 3." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = WaitMs });

            Assert.Equal(0, await page.Locator(Dialog).CountAsync());
        }
    }

    /// <summary>
    /// AC9 - reordering within a column by drag: the top (<c>afterTaskId</c> null), between two (the neighbour above), and
    /// the bottom (the last Task) - each neighbour counted without the dragged Task.
    /// </summary>
    /// <param name="dragged">The Task dragged.</param>
    /// <param name="over">The card the pointer is dropped over.</param>
    /// <param name="fraction">Where in that card: above its middle is before it, below after it.</param>
    /// <param name="zoneBefore">The card the drop zone is drawn before, or <c>null</c> when it is last.</param>
    /// <param name="after">The neighbour the move must name, or <c>null</c> for the top.</param>
    /// <param name="order">The column's order once dropped.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("Plan", "Draft", 0.2f, "Draft", null, new[] { "Plan", "Draft", "Book" })]
    [InlineData("Draft", "Book", 0.8f, "Plan", "Book", new[] { "Book", "Draft", "Plan" })]
    [InlineData("Draft", "Plan", 0.8f, null, "Plan", new[] { "Book", "Plan", "Draft" })]
    public async Task Reordering_within_a_column_names_the_neighbour_above_the_drop_zone(string dragged, string over, float fraction, string? zoneBefore, string? after, string[] order)
    {
        var api = TwoSpaces(Northwind, Harbour);
        var tasks = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft", "Book", "Plan"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);

            await LiftWithMouseAsync(page, dragged);
            await MoveMouseToAsync(page, await PointInAsync(CardNamed(page, over), fraction));
            await WaitForDropZoneAsync(page, "Todo", zoneBefore);
            await page.Mouse.UpAsync();

            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Moved to Todo" })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = WaitMs });

            Assert.Equal(order, await TitlesInAsync(page, "Todo"));

            var sent = Assert.Single(Moves(api));

            using var body = JsonDocument.Parse(sent.Body ?? "{}");

            Assert.Equal(StatusOf(api, Northwind, Atlas, "Todo"), body.RootElement.GetProperty("statusId").GetGuid());

            if (after is null)
            {
                Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("afterTaskId").ValueKind);
            }
            else
            {
                Assert.Equal(tasks[after].Id, body.RootElement.GetProperty("afterTaskId").GetGuid());
            }
        }
    }

    /// <summary>
    /// AC1, AC9 - an empty column is a drop target: the drop zone appears in it, and the drop names no neighbour.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_empty_column_takes_a_drop()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief"], []);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);

            await LiftWithMouseAsync(page, "Draft the brief");

            var done = await BoxAsync(ColumnNamed(page, "Done"));

            await MoveMouseToAsync(page, (done.X + (done.Width / 2), done.Y + (done.Height / 2)));
            await WaitForDropZoneAsync(page, "Done", null);
            await page.Mouse.UpAsync();

            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Moved to Done, position 1 of 1." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = WaitMs });

            using var body = JsonDocument.Parse(Assert.Single(Moves(api)).Body ?? "{}");

            Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("afterTaskId").ValueKind);
        }
    }

    /// <summary>
    /// AC6, AC14, Q5 (a) - dragged back to its own slot, or released outside every column, nothing is sent, the card and the
    /// totals are as they were, the cancel after the lift is announced as the return to the first column at position 1, and the release
    /// opens neither Task detail nor the card's menu (AC11).
    /// </summary>
    /// <param name="outside">Whether the release is outside every column (otherwise back on the origin slot).</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "UX-DR25")]
    public async Task Dropping_on_the_origin_or_outside_every_column_moves_nothing(bool outside)
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief", "Book the room"], ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);

            var origin = await GrabPointAsync(page, "Draft the brief");

            await LiftWithMouseAsync(page, "Draft the brief");
            await MoveMouseToAsync(page, await PointInAsync(CardNamed(page, "Review"), 0.8f));
            await WaitForDropZoneAsync(page, "Doing", null);

            if (outside)
            {
                var heading = await BoxAsync(page.Locator("main h1"));

                await MoveMouseToAsync(page, (heading.X + 4, heading.Y + (heading.Height / 2)));
                await page.Locator(DropZone).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = WaitMs });
            }
            else
            {
                await MoveMouseToAsync(page, origin);
                await WaitForDropZoneAsync(page, "Todo", "Book the room");
            }

            await page.Mouse.UpAsync();

            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Returned to Todo, position 1." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = WaitMs });

            Assert.Empty(Moves(api));
            Assert.Equal(["Draft the brief", "Book the room"], await TitlesInAsync(page, "Todo"));
            Assert.Equal("2", await TotalOfAsync(page, "Todo"));
            Assert.Equal("1", await TotalOfAsync(page, "Doing"));
            Assert.Equal(0, await page.Locator(Lifted).CountAsync());
            Assert.Equal(0, await page.Locator(Dialog).CountAsync());
            Assert.Equal(0, await page.Locator(Menu).CountAsync());
        }
    }

    /// <summary>
    /// AC13, Q10 - <c>Esc</c> mid-drag ends it with nothing moved and nothing sent; the release that follows opens nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Escape_mid_drag_cancels_it()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief"], ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);

            var origin = await GrabPointAsync(page, "Draft the brief");

            await LiftWithMouseAsync(page, "Draft the brief");
            await MoveMouseToAsync(page, await PointInAsync(CardNamed(page, "Review"), 0.8f));
            await page.Locator(DropZone).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            await page.Keyboard.PressAsync("Escape");
            await page.Locator(Lifted).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = WaitMs });

            // Released back on the title it was pressed on: without the drag's click suppression this is a click on it.
            await MoveMouseToAsync(page, origin);
            await page.Mouse.UpAsync();
            await page.EvaluateAsync("() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(() => resolve(true))))");

            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Returned to Todo, position 1." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = WaitMs });

            Assert.Equal(0, await page.Locator(DropZone).CountAsync());
            Assert.Empty(Moves(api));
            Assert.Equal(["Draft the brief"], await TitlesInAsync(page, "Todo"));
            Assert.Equal(0, await page.Locator(Dialog).CountAsync());
        }
    }

    /// <summary>
    /// AC9, Q6 (a) - a dropped move that is refused follows 2.6's outcome: the card and both totals go back, the refusal is
    /// said on the Board notice in the client's own words, and the return is announced.
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
    [Trait("Requirement", "UX-DR32")]
    public async Task A_refused_drop_puts_the_card_back_and_says_why_on_the_Board(int status, string type, string? capability, string copy)
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief", "Book the room"], ["Review", "Ship"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);

            await DragDraftAfterReviewAsync(page);

            var held = api.HoldTaskMoveAsync();

            await page.Mouse.UpAsync();
            await held.WaitAsync(TimeSpan.FromMilliseconds(WaitMs), TestContext.Current.CancellationToken);

            Assert.Equal("3", await TotalOfAsync(page, "Doing"));

            api.ReleaseTaskMoveRefused(status, type, capability);

            await page.Locator(BoardNotice, new PageLocatorOptions { HasText = copy }).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Returned to Todo, position 1." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = WaitMs });

            Assert.Equal(["Draft the brief", "Book the room"], await TitlesInAsync(page, "Todo"));
            Assert.Equal(["Review", "Ship"], await TitlesInAsync(page, "Doing"));
            Assert.Equal("2", await TotalOfAsync(page, "Doing"));
            Assert.Equal(0, await page.Locator(Dialog).CountAsync());
        }
    }

    /// <summary>
    /// AC9 - a drop whose neighbour went while it was out is refused <c>409 task-position-unavailable</c> and the Board is
    /// read again: the column shows the server's Tasks and true total.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_drop_refused_because_its_position_changed_reads_the_Board_again()
    {
        var api = TwoSpaces(Northwind, Harbour);
        var tasks = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief", "Book the room"], ["Review", "Ship"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);

            await DragDraftAfterReviewAsync(page);

            var held = api.HoldTaskMoveAsync();

            await page.Mouse.UpAsync();
            await held.WaitAsync(TimeSpan.FromMilliseconds(WaitMs), TestContext.Current.CancellationToken);

            var doing = api.Boards[(Northwind, Atlas)].Columns[1];

            doing.Tasks.RemoveAll(task => task.Id == tasks["Review"].Id);
            doing.Total--;

            api.ReleaseTaskMove();

            await page.Locator(BoardNotice, new PageLocatorOptions { HasText = PositionChangedCopy }).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Returned to Todo, position 1." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = WaitMs });

            Assert.Equal(["Ship"], await TitlesInAsync(page, "Doing"));
            Assert.Equal("1", await TotalOfAsync(page, "Doing"));
        }
    }

    /// <summary>
    /// AC9 - a drop that gets no answer says "Not sent." on the Board and reverts; one answered <c>404</c> follows the
    /// Board's refusal surface.
    /// </summary>
    /// <param name="notFound">Whether the answer is a <c>404</c> (otherwise no answer).</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_unanswered_or_not_found_drop_follows_the_moves_outcomes(bool notFound)
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief", "Book the room"], ["Review", "Ship"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);

            await DragDraftAfterReviewAsync(page);

            if (notFound)
            {
                api.NextTaskMoveRefusal = (404, ProblemTypes.NotAvailable, null);

                await page.Mouse.UpAsync();
                await page.Locator("main").GetByText(NotAvailableCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

                Assert.Single(Moves(api));
                return;
            }

            var held = api.HoldTaskMoveAsync();

            await page.Mouse.UpAsync();
            await held.WaitAsync(TimeSpan.FromMilliseconds(WaitMs), TestContext.Current.CancellationToken);
            api.ReleaseTaskMoveUnanswered();

            await page.Locator(BoardNotice, new PageLocatorOptions { HasText = NotSentCopy }).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Returned to Todo, position 1." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = WaitMs });

            Assert.Equal(["Draft the brief", "Book the room"], await TitlesInAsync(page, "Todo"));
        }
    }

    /// <summary>
    /// AC11 - a press that never moved past the drag-start distance is still a click: the title opens Task detail, with a
    /// wobble of a few pixels too. Nothing lifts and nothing is sent.
    /// </summary>
    /// <param name="wobble">How far the pointer wanders between press and release, in px.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task A_press_that_does_not_move_opens_Task_detail(int wobble)
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);

            var (x, y) = await GrabPointAsync(page, "Draft the brief");

            await page.Mouse.MoveAsync(x, y);
            await page.Mouse.DownAsync();
            await page.Mouse.MoveAsync(x + wobble, y, new MouseMoveOptions { Steps = 2 });
            await page.Mouse.UpAsync();

            await page.Locator(Dialog).GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = "Draft the brief" })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            Assert.Equal(0, await page.Locator(Lifted).CountAsync());
            Assert.Empty(Moves(api));
        }
    }

    /// <summary>
    /// AC10, UX-DR27 - a Viewer's card does not drag: pressed and dragged by the mouse nothing lifts, no drop zone appears
    /// and nothing is sent; the card carries no grab cursor and no <c>touch-action</c> override; nothing is disabled.
    /// Anchored: the Owner's card in the other Space carries both.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    public async Task A_Viewers_card_does_not_drag_and_nothing_is_disabled()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Owner's Task"]);
        _ = Seed(api, Harbour, Beacon, ThreeStatuses, ["Viewer's Task"], ["Other"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);

            var owners = await CardStyleAsync(CardNamed(page, "Owner's Task"));

            Assert.Equal("grab", owners.Cursor);
            Assert.NotEqual("auto", owners.TouchAction);

            await OpenBoardAsync(page, surface.BaseAddress, Harbour, Beacon);
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            await page.EvaluateAsync(ObserveLifts);

            var viewers = await CardStyleAsync(CardNamed(page, "Viewer's Task"));

            Assert.NotEqual("grab", viewers.Cursor);
            Assert.Equal("auto", viewers.TouchAction);

            var (x, y) = await GrabPointAsync(page, "Viewer's Task");

            await page.Mouse.MoveAsync(x, y);
            await page.Mouse.DownAsync();
            await page.Mouse.MoveAsync(x, y + 16, new MouseMoveOptions { Steps = 4 });
            await MoveMouseToAsync(page, await PointInAsync(CardNamed(page, "Other"), 0.8f));
            await WaitPastLastPressAsync(page, 1000);

            Assert.Equal(0, await LiftCountAsync(page));
            Assert.Equal(0, await page.Locator(DropZone).CountAsync());

            await page.Mouse.UpAsync();

            Assert.Equal(["Viewer's Task"], await TitlesInAsync(page, "Todo"));
            Assert.Empty(Moves(api));
            Assert.Equal(0, await page.Locator(DisabledAnywhere).CountAsync());
        }
    }

    /// <summary>
    /// AC10, UX-DR21 - with the drag attached, the card's actions control and the panel's Move control are still rendered
    /// and visible without hover at 320, 768 and 1280px, and a mover's card can still be lifted.
    /// </summary>
    /// <param name="width">The viewport width.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(320)]
    [InlineData(768)]
    [InlineData(1280)]
    [Trait("Requirement", "UX-DR21")]
    public async Task The_explicit_controls_stay_visible_beside_the_drag_at_every_breakpoint(int width)
    {
        var api = TwoSpaces(Northwind, Harbour);
        var tasks = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(width, 900);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);
            await page.Mouse.MoveAsync(0, 0);

            var actions = page.Locator(".task-card").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Actions for Draft the brief", Exact = true });

            Assert.True(await actions.IsVisibleAsync(), $"The actions control is not visible at {width}px without hover.");

            await LiftWithMouseAsync(page, "Draft the brief");
            await page.Keyboard.PressAsync("Escape");
            await page.Mouse.UpAsync();
            await page.Locator(Lifted).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = WaitMs });

            await page.Locator($".task-card button[data-task-id='{tasks["Draft the brief"].Id:D}']").ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
            await page.Mouse.MoveAsync(0, 0);

            var move = page.Locator(Dialog).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Move", Exact = true });

            Assert.True(await move.IsVisibleAsync(), $"The panel's Move control is not visible at {width}px without hover.");
        }
    }

    /// <summary>
    /// AC14, UX-DR38 - nothing is announced on the lift, on crossing columns or on hovering a drop zone: the live regions
    /// hold exactly what they held before the press until the drop.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR38")]
    public async Task Nothing_is_announced_on_lift_or_on_crossing_columns()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief", "Book the room"], ["Review"], ["Ship"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);

            var before = await AnnouncedAsync(page);

            await LiftWithMouseAsync(page, "Draft the brief");
            Assert.Equal(before, await AnnouncedAsync(page));

            await MoveMouseToAsync(page, await PointInAsync(CardNamed(page, "Review"), 0.8f));
            await WaitForDropZoneAsync(page, "Doing", null);
            Assert.Equal(before, await AnnouncedAsync(page));

            await MoveMouseToAsync(page, await PointInAsync(CardNamed(page, "Ship"), 0.2f));
            await WaitForDropZoneAsync(page, "Done", "Ship");
            Assert.Equal(before, await AnnouncedAsync(page));

            await page.Keyboard.PressAsync("Escape");
            await page.Mouse.UpAsync();
        }
    }

    /// <summary>
    /// AC5, Q13 - near a column's bottom edge mid-drag that column scrolls; near the view's inline edge the Board scrolls
    /// sideways. Polled, never timed.
    /// </summary>
    /// <param name="board">Whether the Board's row is the scroller (otherwise a column).</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Near_an_edge_mid_drag_the_column_or_the_Board_scrolls(bool board)
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, FiveStatuses, Many(30));
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(board ? 768 : 1280, 900);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);

            await LiftWithMouseAsync(page, "Card 01");

            if (board)
            {
                var viewport = page.ViewportSize ?? throw new InvalidOperationException("No viewport.");
                var column = await BoxAsync(ColumnNamed(page, "Todo"));

                await MoveMouseToAsync(page, (viewport.Width - 6, column.Y + 60));
                await page.WaitForFunctionAsync("() => document.querySelector('.board__columns').scrollLeft > 0", null, new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });
            }
            else
            {
                var column = await BoxAsync(ColumnNamed(page, "Todo"));

                await MoveMouseToAsync(page, (column.X + (column.Width / 2), column.Y + column.Height - 6));
                await page.WaitForFunctionAsync("() => document.querySelector('main .board-column').scrollTop > 0", null, new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });
            }

            await page.Keyboard.PressAsync("Escape");
            await page.Mouse.UpAsync();

            Assert.Empty(Moves(api));
        }
    }

    /// <summary>
    /// AC7, Q14 - under <c>prefers-reduced-motion: reduce</c> nothing animates during or after a drag - no card and no drop
    /// zone declares a transition or an animation - while the lifted look (the 2px border, the shadow, the rotation) is
    /// still there: it is state, not motion.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR6")]
    public async Task Under_reduced_motion_a_drag_animates_nothing_and_still_shows_the_lift()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief", "Book the room"], ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);

            await LiftWithMouseAsync(page, "Draft the brief");
            await MoveMouseToAsync(page, await PointInAsync(CardNamed(page, "Review"), 0.8f));
            await page.Locator(DropZone).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            Assert.Empty(await MovingAsync(page));
            Assert.Equal("2px", await page.Locator(Lifted).EvaluateAsync<string>("card => getComputedStyle(card).borderTopWidth"));
            Assert.NotEqual("none", await page.Locator(Lifted).EvaluateAsync<string>("card => getComputedStyle(card).boxShadow"));
            Assert.Equal("-1deg", await page.Locator(Lifted).EvaluateAsync<string>("card => getComputedStyle(card).rotate"));

            var held = api.HoldTaskMoveAsync();

            await page.Mouse.UpAsync();
            await held.WaitAsync(TimeSpan.FromMilliseconds(WaitMs), TestContext.Current.CancellationToken);

            Assert.Empty(await MovingAsync(page));

            api.ReleaseTaskMove();
        }
    }

    private static async Task DragDraftAfterReviewAsync(IPage page)
    {
        await LiftWithMouseAsync(page, "Draft the brief");
        await MoveMouseToAsync(page, await PointInAsync(CardNamed(page, "Review"), 0.8f));
        await WaitForDropZoneAsync(page, "Doing", "Ship");
    }

    private static async Task<(string Cursor, string TouchAction)> CardStyleAsync(ILocator card)
    {
        var style = await card.EvaluateAsync<JsonElement>("card => { const s = getComputedStyle(card); return { cursor: s.cursor, touchAction: s.touchAction }; }");

        return (style.GetProperty("cursor").GetString() ?? string.Empty, style.GetProperty("touchAction").GetString() ?? string.Empty);
    }

    private static Task<string[]> MovingAsync(IPage page) =>
        page.EvaluateAsync<string[]>(
            """
            () => [...document.querySelectorAll('.task-card, .drop-zone')]
                .filter(element => {
                    const style = getComputedStyle(element);
                    const durations = (style.transitionDuration + ',' + style.animationDuration).split(',').map(value => parseFloat(value) || 0);
                    return durations.some(value => value > 0) || style.animationName !== 'none';
                })
                .map(element => element.className)
            """);
}
