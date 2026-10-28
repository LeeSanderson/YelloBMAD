using System.Globalization;
using Microsoft.Playwright;

namespace Yello.Tests.Client;

/// <summary>
/// What story 2.7's pointer and touch cases share: a Board seeded with named Tasks, the drag driven by a mouse or by a
/// touch screen through the Chrome DevTools Protocol, and the page-side observer that records every lift with the
/// page's own clock (Q14 (a)). <see cref="DragTests"/> and <see cref="TouchDragTests"/> use it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Never a sleep as the oracle</b> (<c>TESTING-CONVENTIONS.md</c>). A lift is waited for by polling the card's
/// <c>task-card--lifted</c> class; "never lifted" is asserted after the page's own <c>performance.now()</c> has moved past
/// a window, read by <see cref="IPage.WaitForFunctionAsync(string, object?, PageWaitForFunctionOptions?)"/>, against a
/// <c>MutationObserver</c> that saw every class change from the first press.
/// </para>
/// <para>
/// <b>The long-press threshold is read from the token at each press</b> (story 2.7 Q2 (a)), so a case that must move
/// before the threshold lengthens it with a style tag rather than racing 320ms against a loaded machine.
/// </para>
/// </remarks>
internal static class DragHarness
{
    /// <summary>
    /// The wait for anything the page should do.
    /// </summary>
    public const float WaitMs = 30_000;

    /// <summary>
    /// The locale every case uses.
    /// </summary>
    public const string EnglishLocale = "en-GB";

    /// <summary>
    /// The Board's polite live region.
    /// </summary>
    public const string LiveRegion = "[aria-live='polite']";

    /// <summary>
    /// The Board's notice, where a refusal of a move made on the Board is said.
    /// </summary>
    public const string BoardNotice = ".board__notice";

    /// <summary>
    /// The lifted card.
    /// </summary>
    public const string Lifted = ".task-card--lifted";

    /// <summary>
    /// The drop zone.
    /// </summary>
    public const string DropZone = ".drop-zone";

    /// <summary>
    /// The context bar's Role chip, present once the Space has answered.
    /// </summary>
    public const string RoleChip = "nav.context-bar .role-chip";

    /// <summary>
    /// Records, from the first press on, the page time of each press and of each lift - any element gaining the lifted
    /// class - and every <c>navigator.vibrate</c> call.
    /// </summary>
    public const string ObserveLifts =
        """
        () => {
            window.__lifts = [];
            window.__downs = [];
            window.__vibrations = [];
            navigator.vibrate = pattern => { window.__vibrations.push(pattern); return true; };
            document.addEventListener('pointerdown', () => window.__downs.push(performance.now()), true);
            new MutationObserver(records => {
                for (const record of records) {
                    if (record.target.classList && record.target.classList.contains('task-card--lifted')) {
                        window.__lifts.push(performance.now());
                    }
                }
            }).observe(document.body, { subtree: true, attributes: true, attributeFilter: ['class'] });
        }
        """;

    /// <summary>
    /// The three Statuses most cases use.
    /// </summary>
    public static readonly string[] ThreeStatuses = ["Todo", "Doing", "Done"];

    /// <summary>
    /// Five Statuses - wider than a 1024px view, so the Board scrolls sideways.
    /// </summary>
    public static readonly string[] FiveStatuses = ["Todo", "Doing", "Testing", "Blocked", "Done"];

    /// <summary>
    /// A signed-in Account who is Owner of <paramref name="owned"/> and Viewer of <paramref name="viewed"/>.
    /// </summary>
    public static FakeYelloApi TwoSpaces(Guid owned, Guid viewed)
    {
        var api = new FakeYelloApi { SignedIn = true };

        api.Switcher.Add((owned, "Northwind Redesign"));
        api.Switcher.Add((viewed, "Harbour Ops"));
        api.Roles[owned] = "owner";
        api.Roles[viewed] = "viewer";

        return api;
    }

    /// <summary>
    /// A Project and its Board: one column per Status in <paramref name="statuses"/>, the n-th list of titles in the n-th
    /// column, keyed in list order. Returns the seeded Tasks by title.
    /// </summary>
    public static Dictionary<string, FakeTask> Seed(FakeYelloApi api, Guid spaceId, Guid projectId, string[] statuses, params string[][] titles)
    {
        var columns = statuses.Select(status => new FakeColumn(Guid.NewGuid(), status, 0, [])).ToList();
        var seeded = new Dictionary<string, FakeTask>(StringComparer.Ordinal);

        for (var index = 0; index < titles.Length; index++)
        {
            var column = columns[index];

            foreach (var title in titles[index])
            {
                var task = new FakeTask(Guid.NewGuid(), title, column.StatusId, $"a{column.Tasks.Count:D5}", DateTimeOffset.UtcNow);

                column.Tasks.Add(task);
                column.Total++;
                seeded[title] = task;
            }
        }

        api.Boards[(spaceId, projectId)] = new FakeBoard(new FakeBoardProject(projectId, "Atlas"), columns);
        api.Projects[spaceId] = [new FakeProject(projectId, "Atlas", DateTimeOffset.UtcNow)];

        return seeded;
    }

    /// <summary>
    /// <paramref name="count"/> titles, "Card 01" onwards - a column taller than the view.
    /// </summary>
    public static string[] Many(int count) => [.. Enumerable.Range(1, count).Select(n => string.Create(CultureInfo.InvariantCulture, $"Card {n:D2}"))];

    /// <summary>
    /// The Status id of the column named <paramref name="status"/>.
    /// </summary>
    public static Guid StatusOf(FakeYelloApi api, Guid spaceId, Guid projectId, string status) =>
        api.Boards[(spaceId, projectId)].Columns.First(column => column.Name == status).StatusId;

    /// <summary>
    /// Every move sent.
    /// </summary>
    public static List<FakeRequest> Moves(FakeYelloApi api) =>
        [.. api.Requests.Where(request => request.Path.Contains("/tasks/", StringComparison.Ordinal) && request.Method is "POST" or "PATCH" && !request.Path.EndsWith("/tasks", StringComparison.Ordinal))];

    /// <summary>
    /// Opens the Board and waits for its first card.
    /// </summary>
    public static async Task OpenBoardAsync(IPage page, Uri baseAddress, Guid spaceId, Guid projectId)
    {
        await page.GotoAsync(new Uri(baseAddress, $"spaces/{spaceId:D}/projects/{projectId:D}").ToString());
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
        await page.Locator(".task-card").First.WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
    }

    /// <summary>
    /// The column whose heading is <paramref name="status"/>.
    /// </summary>
    public static ILocator ColumnNamed(IPage page, string status) =>
        page.Locator("main .board-column", new PageLocatorOptions { Has = page.Locator("h2", new PageLocatorOptions { HasText = status }) });

    /// <summary>
    /// The card whose title is <paramref name="title"/>.
    /// </summary>
    public static ILocator CardNamed(IPage page, string title) =>
        page.Locator(".task-card", new PageLocatorOptions { Has = page.Locator(".task-card__title", new PageLocatorOptions { HasTextRegex = new System.Text.RegularExpressions.Regex("^" + System.Text.RegularExpressions.Regex.Escape(title) + "$") }) });

    /// <summary>
    /// The titles in a column, in order.
    /// </summary>
    public static async Task<List<string>> TitlesInAsync(IPage page, string status) =>
        [.. (await ColumnNamed(page, status).Locator(".task-card__title").AllTextContentsAsync()).Select(title => title.Trim())];

    /// <summary>
    /// A column's count chip.
    /// </summary>
    public static async Task<string> TotalOfAsync(IPage page, string status) =>
        (await ColumnNamed(page, status).Locator(".column-count").InnerTextAsync()).Trim();

    /// <summary>
    /// Everything the polite live regions hold.
    /// </summary>
    public static async Task<string> AnnouncedAsync(IPage page) =>
        string.Join(" | ", await page.Locator(LiveRegion).AllTextContentsAsync());

    /// <summary>
    /// A point on the card's title, a little in from its start: where a person grabs the card.
    /// </summary>
    public static async Task<(float X, float Y)> GrabPointAsync(IPage page, string title)
    {
        var box = await BoxAsync(CardNamed(page, title).Locator(".task-card__title"));

        return (box.X + Math.Min(12, box.Width / 2), box.Y + (box.Height / 2));
    }

    /// <summary>
    /// A point in a card at <paramref name="fraction"/> of its height: above its middle puts the drop zone before it,
    /// below its middle after it.
    /// </summary>
    public static async Task<(float X, float Y)> PointInAsync(ILocator card, float fraction)
    {
        var box = await BoxAsync(card);

        return (box.X + (box.Width / 2), box.Y + (box.Height * fraction));
    }

    /// <summary>
    /// The element's box, which must be rendered.
    /// </summary>
    public static async Task<LocatorBoundingBoxResult> BoxAsync(ILocator locator)
    {
        await locator.WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });

        return await locator.BoundingBoxAsync() ?? throw new InvalidOperationException("The element has no box.");
    }

    /// <summary>
    /// Presses the mouse on <paramref name="title"/>'s card and moves past the drag-start distance, then waits for the lift.
    /// </summary>
    public static async Task LiftWithMouseAsync(IPage page, string title)
    {
        var (x, y) = await GrabPointAsync(page, title);

        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x, y + 16, new MouseMoveOptions { Steps = 4 });
        await page.Locator(Lifted).WaitForAsync(new LocatorWaitForOptions { Timeout = WaitMs });
    }

    /// <summary>
    /// Moves the held mouse to a point, in steps, as a hand does.
    /// </summary>
    public static Task MoveMouseToAsync(IPage page, (float X, float Y) point) =>
        page.Mouse.MoveAsync(point.X, point.Y, new MouseMoveOptions { Steps = 8 });

    /// <summary>
    /// Waits until the drop zone is in <paramref name="status"/>'s column, immediately before the card titled
    /// <paramref name="before"/> - or last in the column when it is <c>null</c>.
    /// </summary>
    public static Task WaitForDropZoneAsync(IPage page, string status, string? before) =>
        page.WaitForFunctionAsync(
            """
            ([status, before]) => {
                const column = [...document.querySelectorAll('main .board-column')].find(c => c.querySelector('h2').textContent.trim() === status);
                const zone = column && column.querySelector('.drop-zone');
                if (!zone) return false;
                let next = zone.nextElementSibling;
                while (next && next.classList.contains('task-card--lifted')) next = next.nextElementSibling;
                if (before === null) return next === null;
                return !!next && next.querySelector('.task-card__title').textContent.trim() === before;
            }
            """,
            new object?[] { status, before },
            new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });

    /// <summary>
    /// Waits until the page's own clock is <paramref name="milliseconds"/> past the last press: the window inside which a
    /// lift that should never happen would have happened.
    /// </summary>
    public static Task WaitPastLastPressAsync(IPage page, int milliseconds) =>
        page.WaitForFunctionAsync(
            "ms => window.__downs.length > 0 && performance.now() - window.__downs[window.__downs.length - 1] > ms",
            milliseconds,
            new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });

    /// <summary>
    /// How many lifts the page has seen since <see cref="ObserveLifts"/> ran.
    /// </summary>
    public static Task<int> LiftCountAsync(IPage page) => page.EvaluateAsync<int>("() => window.__lifts.length");

    /// <summary>
    /// Lengthens the long-press threshold through its token, so a case can move before it without racing the clock.
    /// </summary>
    public static Task LengthenThresholdAsync(IPage page, int milliseconds) =>
        page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = string.Create(CultureInfo.InvariantCulture, $":root {{ --motion-long-press-threshold: {milliseconds}ms; }}") });

    /// <summary>
    /// One touch event through the DevTools Protocol: <c>touchStart</c>, <c>touchMove</c> or <c>touchEnd</c>, with the
    /// points still on the screen.
    /// </summary>
    public static Task TouchAsync(ICDPSession session, string type, params (float X, float Y)[] points) =>
        session.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
        {
            ["type"] = type,
            ["touchPoints"] = points.Select(point => new Dictionary<string, object> { ["x"] = point.X, ["y"] = point.Y }).ToArray(),
        });

    /// <summary>
    /// Moves one finger from <paramref name="from"/> to <paramref name="to"/> in <paramref name="steps"/> touch moves.
    /// </summary>
    public static async Task SwipeAsync(ICDPSession session, (float X, float Y) from, (float X, float Y) to, int steps)
    {
        for (var step = 1; step <= steps; step++)
        {
            var x = from.X + ((to.X - from.X) * step / steps);
            var y = from.Y + ((to.Y - from.Y) * step / steps);

            await TouchAsync(session, "touchMove", (x, y));
        }
    }
}
