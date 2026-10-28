using System.Text.Json;
using Microsoft.Playwright;
using Xunit;
using static Yello.Tests.Client.DragHarness;

namespace Yello.Tests.Client;

/// <summary>
/// Story 2.7's touch path in a real browser: a long-press lifts only after the threshold, confirmed by the lifted card and
/// a haptic pulse; a finger that moves beyond the slop in any direction before then is a pan, which scrolls and never
/// lifts; jitter inside the slop still lifts; a second finger cancels; a long-press never opens the card's menu. AC2-AC4,
/// AC10, AC11, AC13; UX-DR23, UX-DR6.
/// </summary>
/// <remarks>
/// <para>
/// <b>Touch is driven through the Chrome DevTools Protocol</b> (<c>Input.dispatchTouchEvent</c>, Q14 (a)) on a context
/// created with touch support: Playwright's own touchscreen only taps. Chromium is the only browser the suite runs.
/// </para>
/// <para>
/// <b>Time is read from the page, never slept.</b> A lift is polled for; "never lifted" is asserted once the page's clock
/// is twice the threshold past the press. Cases that must move before the threshold lengthen it through its token first
/// (the module reads the token at each press, Q2 (a)), so a loaded machine cannot turn a pan into a lift.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "UX-DR23")]
[Collection(nameof(TouchTimingCollection))]
public sealed class TouchDragTests(ClientSurfaceFixture surface)
{
    private const int LongThreshold = 1000;

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();
    private static readonly Guid Beacon = Guid.NewGuid();

    /// <summary>
    /// AC2, Q12 - a long-press lifts only once the threshold has passed (measured on the page's own clock from the press),
    /// with one haptic pulse; the finger then drives the drag, and the drop is sent as 2.6's move.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR6")]
    public async Task A_long_press_lifts_after_the_threshold_with_a_haptic_pulse_and_drops_as_a_move()
    {
        var api = TwoSpaces(Northwind, Harbour);
        var tasks = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief", "Book the room"], ["Review", "Ship"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale, timezoneId: null, hasTouch: true);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);
            await page.EvaluateAsync(ObserveLifts);
            await LengthenThresholdAsync(page, LongThreshold);

            var session = await context.NewCDPSessionAsync(page);
            var start = await GrabPointAsync(page, "Draft the brief");

            await TouchAsync(session, "touchStart", start);

            Assert.Equal(0, await page.Locator(Lifted).CountAsync());

            await page.Locator(Lifted).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            var timing = await page.EvaluateAsync<JsonElement>("() => ({ down: window.__downs[0], lift: window.__lifts[0], vibrations: window.__vibrations.length })");

            Assert.True(timing.GetProperty("lift").GetDouble() - timing.GetProperty("down").GetDouble() >= LongThreshold - 20, "The lift came before the long-press threshold.");
            Assert.Equal(1, timing.GetProperty("vibrations").GetInt32());

            var target = await PointInAsync(CardNamed(page, "Review"), 0.8f);

            await SwipeAsync(session, start, target, 10);
            await WaitForDropZoneAsync(page, "Doing", "Ship");
            await TouchAsync(session, "touchEnd");

            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Moved to Doing, position 2 of 3." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = WaitMs });

            using var body = JsonDocument.Parse(Assert.Single(Moves(api)).Body ?? "{}");

            Assert.Equal(tasks["Review"].Id, body.RootElement.GetProperty("afterTaskId").GetGuid());
            Assert.Equal(0, await page.Locator("[role='menu'], [role='dialog']").CountAsync());
        }
    }

    /// <summary>
    /// AC3, AC4 - a finger that moves beyond the slop before the threshold, in any of the four directions, is a pan: the
    /// pending lift is cancelled, nothing ever lifts or is sent, and the column or the Board scrolls under the finger. The
    /// Board is 1024px wide with five columns, so it scrolls sideways - the case a single-axis rule breaks.
    /// </summary>
    /// <param name="dx">The pan's inline distance, in px.</param>
    /// <param name="dy">The pan's block distance, in px.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(-80, 0)]
    [InlineData(80, 0)]
    [InlineData(0, -80)]
    [InlineData(0, 80)]
    public async Task A_pan_beyond_the_slop_before_the_threshold_never_lifts_and_scrolls(int dx, int dy)
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, FiveStatuses, [], Many(30));
        var (context, page) = await surface.OpenAsync(EnglishLocale, timezoneId: null, hasTouch: true);

        await using (context)
        {
            await page.SetViewportSizeAsync(1024, 900);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);
            await page.EvaluateAsync(ObserveLifts);
            await LengthenThresholdAsync(page, LongThreshold);

            // Room to scroll either way on both axes.
            await page.EvaluateAsync("() => { document.querySelector('.board__columns').scrollLeft = 120; ColumnScroller().scrollTop = 200; function ColumnScroller() { return [...document.querySelectorAll('main .board-column')][1]; } }");

            var before = await ScrollAsync(page);
            var announcedBefore = await AnnouncedAsync(page);
            var start = await GrabPointAsync(page, "Card 08");
            var session = await context.NewCDPSessionAsync(page);

            await TouchAsync(session, "touchStart", start);
            await SwipeAsync(session, start, (start.X + dx, start.Y + dy), 8);
            await TouchAsync(session, "touchEnd");
            await WaitPastLastPressAsync(page, 2 * LongThreshold);

            Assert.Equal(0, await LiftCountAsync(page));
            Assert.Equal(0, await page.EvaluateAsync<int>("() => window.__vibrations.length"));
            Assert.Empty(Moves(api));

            // A pending lift a pan cancelled says nothing (AC14).
            Assert.Equal(announcedBefore, await AnnouncedAsync(page));

            var after = await ScrollAsync(page);

            if (dx != 0)
            {
                Assert.NotEqual(before.Board, after.Board);
            }
            else
            {
                Assert.NotEqual(before.Column, after.Column);
            }
        }
    }

    /// <summary>
    /// AC3 - the slop is the drag's own rule, axis-agnostic, not only the browser's: a finger that moves 13px before the
    /// threshold, in any of the four directions, never lifts. 13px is beyond the drag's 10px slop and inside Chromium's own
    /// touch slop, so no native pan starts and no <c>pointercancel</c> ends the pending lift for it - a longer move is
    /// cancelled by the browser as well, which would hide a single-axis rule (proved against that plant).
    /// </summary>
    /// <param name="dx">The move's inline distance, in px.</param>
    /// <param name="dy">The move's block distance, in px.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(-13, 0)]
    [InlineData(13, 0)]
    [InlineData(0, -13)]
    [InlineData(0, 13)]
    public async Task A_move_just_beyond_the_slop_in_any_direction_never_lifts(int dx, int dy)
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale, timezoneId: null, hasTouch: true);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);
            await page.EvaluateAsync(ObserveLifts);
            await LengthenThresholdAsync(page, LongThreshold);

            var start = await GrabPointAsync(page, "Draft the brief");
            var session = await context.NewCDPSessionAsync(page);

            await TouchAsync(session, "touchStart", start);
            await SwipeAsync(session, start, (start.X + dx, start.Y + dy), 3);
            await WaitPastLastPressAsync(page, 2 * LongThreshold);

            Assert.Equal(0, await LiftCountAsync(page));

            await TouchAsync(session, "touchEnd");

            Assert.Empty(Moves(api));
        }
    }

    /// <summary>
    /// AC3 - the slop is a real dead zone: a finger that jitters a few pixels inside it before the threshold still lifts.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Jitter_inside_the_slop_still_lifts()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale, timezoneId: null, hasTouch: true);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);
            await page.EvaluateAsync(ObserveLifts);
            await LengthenThresholdAsync(page, LongThreshold);

            var session = await context.NewCDPSessionAsync(page);
            var start = await GrabPointAsync(page, "Draft the brief");

            await TouchAsync(session, "touchStart", start);
            await TouchAsync(session, "touchMove", (start.X + 3, start.Y + 2));
            await TouchAsync(session, "touchMove", (start.X - 2, start.Y + 4));
            await page.Locator(Lifted).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            await TouchAsync(session, "touchEnd");

            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Returned to Todo, position 1." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = WaitMs });

            Assert.Empty(Moves(api));
        }
    }

    /// <summary>
    /// AC13 - a second finger mid-drag (a pinch or a two-finger pan) ends the drag with nothing moved and nothing sent.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_second_finger_mid_drag_cancels_it()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief"], ["Review"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale, timezoneId: null, hasTouch: true);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);

            var session = await context.NewCDPSessionAsync(page);
            var start = await GrabPointAsync(page, "Draft the brief");

            await TouchAsync(session, "touchStart", start);
            await page.Locator(Lifted).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            var target = await PointInAsync(CardNamed(page, "Review"), 0.8f);

            await SwipeAsync(session, start, target, 6);
            await page.Locator(DropZone).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            await TouchAsync(session, "touchStart", target, (target.X + 40, target.Y + 40));
            await page.Locator(Lifted).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = WaitMs });
            await TouchAsync(session, "touchEnd");

            Assert.Equal(0, await page.Locator(DropZone).CountAsync());
            Assert.Empty(Moves(api));
            Assert.Equal(["Draft the brief"], await TitlesInAsync(page, "Todo"));
        }
    }

    /// <summary>
    /// AC11, Q11 (a) - a long-press that lifts and is released where it began opens neither the card's menu nor Task
    /// detail; nothing is sent.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_long_press_opens_no_card_menu()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale, timezoneId: null, hasTouch: true);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);
            await page.EvaluateAsync(ObserveLifts);

            var session = await context.NewCDPSessionAsync(page);
            var start = await GrabPointAsync(page, "Draft the brief");

            await TouchAsync(session, "touchStart", start);
            await page.Locator(Lifted).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

            // Held well past the platform's own long-press, which is what raises the context menu.
            await WaitPastLastPressAsync(page, 1500);
            await TouchAsync(session, "touchEnd");

            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Returned to Todo, position 1." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = WaitMs });

            Assert.Equal(0, await page.Locator("[role='menu']").CountAsync());
            Assert.Equal(0, await page.Locator("[role='dialog']").CountAsync());
            Assert.Empty(Moves(api));
        }
    }

    /// <summary>
    /// AC8, AC11 - a touch drag swallows only its own click: the next keyboard activation after it, which no press comes
    /// before, still opens the Task. The keyboard path is never derived from the gesture.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR21")]
    public async Task After_a_touch_drag_the_keyboard_still_opens_a_Task()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, ["Draft the brief"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale, timezoneId: null, hasTouch: true);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);

            var session = await context.NewCDPSessionAsync(page);
            var start = await GrabPointAsync(page, "Draft the brief");

            await TouchAsync(session, "touchStart", start);
            await page.Locator(Lifted).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            await SwipeAsync(session, start, (start.X, start.Y + 30), 3);
            await TouchAsync(session, "touchEnd");
            await page.Locator(Lifted).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = WaitMs });

            await CardNamed(page, "Draft the brief").Locator(".task-card__open").FocusAsync();
            await page.Keyboard.PressAsync("Enter");

            await page.Locator("[role='dialog']").WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            Assert.Empty(Moves(api));
        }
    }

    /// <summary>
    /// AC10, UX-DR27 - a Viewer's card held past the threshold never lifts, pulses nothing and sends nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    public async Task A_long_press_on_a_Viewers_card_lifts_nothing()
    {
        var api = TwoSpaces(Northwind, Harbour);
        _ = Seed(api, Harbour, Beacon, ThreeStatuses, ["Viewer's Task"]);
        var (context, page) = await surface.OpenAsync(EnglishLocale, timezoneId: null, hasTouch: true);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Harbour, Beacon);
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
            await page.EvaluateAsync(ObserveLifts);

            var session = await context.NewCDPSessionAsync(page);
            var start = await GrabPointAsync(page, "Viewer's Task");

            await TouchAsync(session, "touchStart", start);
            await WaitPastLastPressAsync(page, 1000);
            await TouchAsync(session, "touchEnd");

            Assert.Equal(0, await LiftCountAsync(page));
            Assert.Equal(0, await page.EvaluateAsync<int>("() => window.__vibrations.length"));
            Assert.Empty(Moves(api));
        }
    }

    private static async Task<(double Board, double Column)> ScrollAsync(IPage page)
    {
        var scroll = await page.EvaluateAsync<JsonElement>("() => ({ board: document.querySelector('.board__columns').scrollLeft, column: [...document.querySelectorAll('main .board-column')][1].scrollTop })");

        return (scroll.GetProperty("board").GetDouble(), scroll.GetProperty("column").GetDouble());
    }
}
