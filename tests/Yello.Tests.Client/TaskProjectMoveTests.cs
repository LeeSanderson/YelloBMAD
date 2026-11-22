using System.Text.Json;
using Microsoft.Playwright;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// Moving a Task into another Project from Task detail, in a real browser: a "Move to Project" control beside Move, absent for
/// a Viewer; a picker in place that lists the Space's other Projects with nothing selected, and asks for a Status only when
/// the destination lacks the Task's; a commit that is not optimistic, holds focus on its in-flight line, and on admission
/// takes the card off the Board, closes the panel and puts focus on the column that held it; and every refusal said in the
/// panel with nothing moved.
/// </summary>
/// <remarks>
/// <para>
/// <b>The API is <see cref="FakeYelloApi"/></b>, deciding the move in the Host's order (<c>FakeYelloApi.ProjectMove.cs</c>),
/// with a hold so a case can assert the in-flight state; the Projects come from the Space's Project list and each
/// destination's set from its seeded Status read.
/// </para>
/// <para>
/// <b>The markup is the story's recommendation, as this scaffold assumes it</b>: a button named "Move to Project" in the
/// panel; one <c>.project-move-picker</c> rendered in place below it holding <c>[role=option]</c> rows, one tab stop per step;
/// the Status step headed "“{Status}” is not in {Project}. Choose a Status:"; an in-flight <c>[role=status]</c> line
/// "Moving to {Project}…"; the panel's refusal region <c>.task-detail__refusal</c>. A different class name in the component
/// is corrected here and recorded.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-41")]
[Trait("Requirement", "UX-DR11")]
public sealed class TaskProjectMoveTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string RoleChip = "nav.context-bar .role-chip";

    private const string Column = "main .board-column";
    private const string Card = ".task-card";
    private const string CardTitle = ".task-card__title";
    private const string Dialog = "[role='dialog']";
    private const string Picker = ".project-move-picker";
    private const string PickerOption = ".project-move-picker [role='option']";
    private const string Refusal = ".task-detail__refusal";
    private const string LiveRegion = "[aria-live='polite']";
    private const string DisabledAnywhere = "main [disabled], main [aria-disabled='true'], [role='dialog'] [disabled]";

    private const string MoveToProject = "Move to Project";
    private const string NoOtherProjectCopy = "There is no other Project in this Space.";
    private const string NotSentCopy = "Not sent.";
    private const string NothingMoved = "Nothing was moved";

    private const string Red = "red: AC14 - Task detail has no Move to Project control or ProjectMovePicker (Q7 (a), Q9 (a), Q10 (a))";

    private static readonly string[] StatusNames = ["Todo", "Doing", "Done"];

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();
    private static readonly Guid Beacon = Guid.NewGuid();
    private static readonly Guid Comet = Guid.NewGuid();
    private static readonly Guid Dune = Guid.NewGuid();

    private bool _installed;

    /// <summary>
    /// A Member is offered "Move to Project" in Task detail; a Viewer is not - absent from the page, never disabled - and
    /// nothing in the Board or the panel is disabled for the Viewer.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC12, AC14 - Task detail has no Move to Project control (Q10 (a))")]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "FR-16")]
    public async Task The_control_is_offered_to_a_Member_and_absent_for_a_Viewer()
    {
        var api = TwoSpaces(northwindRole: "member");
        var tasks = SeedSpace(api, Northwind, ["Draft the brief"], Atlas, Beacon);
        var viewers = SeedSpace(api, Harbour, ["Viewer's Task"], Dune, Guid.NewGuid());
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks["Draft the brief"].Id);

            Assert.Equal(1, await MoveToProjectControl(page).CountAsync());

            await page.Keyboard.PressAsync("Escape");
            await OpenBoardAsync(api, page, Harbour, Dune);
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await OpenPanelAsync(page, viewers["Viewer's Task"].Id);

            Assert.Equal(0, await MoveToProjectControl(page).CountAsync());
            Assert.DoesNotContain(MoveToProject, await page.EvaluateAsync<string>("() => document.body.innerHTML"), StringComparison.Ordinal);
            Assert.Equal(0, await page.Locator(DisabledAnywhere).CountAsync());
        }
    }

    /// <summary>
    /// The control opens the picker in place - still one dialog - listing the Space's other Projects by name, never the
    /// Task's own, with nothing selected and one tab stop. Opening it sends nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = Red)]
    [Trait("Requirement", "UX-DR21")]
    public async Task The_first_step_lists_the_other_Projects_with_nothing_selected_and_one_tab_stop()
    {
        var api = TwoSpaces();
        var tasks = SeedSpace(api, Northwind, ["Draft the brief"], Atlas, Beacon, Comet);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks["Draft the brief"].Id);
            await OpenPickerAsync(page);

            Assert.Equal(1, await page.Locator(Dialog).CountAsync());
            Assert.Equal(["Beacon", "Comet"], await OptionsAsync(page));
            Assert.Equal(0, await page.Locator($"{Picker} [aria-selected='true'], {Picker} [aria-checked='true']").CountAsync());
            Assert.Equal(1, await page.Locator($"{PickerOption}[tabindex='0']").CountAsync());
            Assert.Empty(api.ProjectMoves);
        }
    }

    /// <summary>
    /// A destination whose set holds the Task's Status commits on the Project choice: one request naming the Project, with an
    /// <c>Idempotency-Key</c> and no position; then the card leaves the Board, the panel closes, focus is on the column that
    /// held the Task, and the move is announced politely.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = Red)]
    [Trait("Requirement", "UX-DR25")]
    [Trait("Requirement", "AD-15")]
    public async Task A_destination_holding_the_Status_commits_on_the_Project_choice()
    {
        var api = TwoSpaces();
        var tasks = SeedSpace(api, Northwind, ["Draft the brief", "Book the room"], Atlas, Beacon, Comet);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks["Draft the brief"].Id);
            await OpenPickerAsync(page);
            await ChooseAsync(page, "Beacon");

            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Moved “Draft the brief” to Beacon." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Dialog).CountAsync());
            Assert.Equal(["Book the room"], await TitlesInAsync(page, "Todo"));
            Assert.Equal("Todo", await FocusedColumnAsync(page));

            var sent = Assert.Single(api.ProjectMoves);

            Assert.Equal(FakeYelloApi.ProjectMovePathOf(Northwind, tasks["Draft the brief"].Id), sent.Path);
            Assert.NotNull(sent.Header(Idempotency.HeaderName));

            using var body = JsonDocument.Parse(sent.Body ?? "{}");

            Assert.Equal(Beacon, body.RootElement.GetProperty("projectId").GetGuid());
            Assert.False(body.RootElement.TryGetProperty("positionKey", out _), "The move carried a position key.");
            Assert.False(body.RootElement.TryGetProperty("afterTaskId", out _), "The move carried a neighbour.");
        }
    }

    /// <summary>
    /// A destination whose set lacks the Task's Status asks for one: a Status step headed with the Status and the Project,
    /// listing the destination's Statuses in Board order with nothing selected. Choosing one commits it, and the announcement
    /// names the Status.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = Red)]
    [Trait("Requirement", "FR-26")]
    [Trait("Requirement", "AR-24")]
    public async Task A_destination_lacking_the_Status_asks_for_one_with_nothing_selected()
    {
        var api = TwoSpaces();
        var tasks = SeedSpace(api, Northwind, ["Draft the brief"], Atlas, Beacon, Comet);
        var doing = WithoutTheFirstStatus(api, Northwind, Comet);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks["Draft the brief"].Id);
            await OpenPickerAsync(page);
            await ChooseAsync(page, "Comet");

            await page.Locator(Picker, new PageLocatorOptions { HasText = "“Todo” is not in Comet. Choose a Status:" })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(["Doing", "Done"], await OptionsAsync(page));
            Assert.Equal(0, await page.Locator($"{Picker} [aria-selected='true'], {Picker} [aria-checked='true']").CountAsync());
            Assert.Empty(api.ProjectMoves);

            await ChooseAsync(page, "Doing");

            await page.Locator(LiveRegion, new PageLocatorOptions { HasText = "Moved “Draft the brief” to Comet, in Doing." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            using var body = JsonDocument.Parse(Assert.Single(api.ProjectMoves).Body ?? "{}");

            Assert.Equal(Comet, body.RootElement.GetProperty("projectId").GetGuid());
            Assert.Equal(doing, body.RootElement.GetProperty("statusId").GetGuid());
        }
    }

    /// <summary>
    /// <c>Esc</c> steps back innermost first: from the Status step to the Project step, then out of the picker with focus back
    /// on the control. Nothing is sent.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = Red)]
    [Trait("Requirement", "UX-DR12")]
    public async Task Esc_steps_back_from_the_Status_step_to_the_Projects_then_closes_on_the_control()
    {
        var api = TwoSpaces();
        var tasks = SeedSpace(api, Northwind, ["Draft the brief"], Atlas, Beacon, Comet);
        _ = WithoutTheFirstStatus(api, Northwind, Comet);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks["Draft the brief"].Id);
            await OpenPickerAsync(page);
            await ChooseAsync(page, "Comet");
            await page.Locator(PickerOption, new PageLocatorOptions { HasText = "Doing" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await page.Keyboard.PressAsync("Escape");
            await page.Locator(PickerOption, new PageLocatorOptions { HasText = "Beacon" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(["Beacon", "Comet"], await OptionsAsync(page));

            await page.Keyboard.PressAsync("Escape");
            await page.Locator(Picker).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.Equal(1, await page.Locator(Dialog).CountAsync());
            Assert.Equal(MoveToProject, await ActiveTextAsync(page));
            Assert.Empty(api.ProjectMoves);
        }
    }

    /// <summary>
    /// The commit is not optimistic: while it is out the card is still on the Board, the picker is replaced by a status line
    /// "Moving to {Project}…" that holds focus, and a second press sends nothing. On release the card leaves.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = Red)]
    [Trait("Requirement", "AR-20")]
    public async Task In_flight_the_card_stays_the_status_line_holds_focus_and_a_second_press_sends_nothing()
    {
        var api = TwoSpaces();
        var tasks = SeedSpace(api, Northwind, ["Draft the brief"], Atlas, Beacon);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks["Draft the brief"].Id);

            var held = api.HoldProjectMoveAsync();

            await OpenPickerAsync(page);
            await ChooseAsync(page, "Beacon");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            var line = page.Locator($"{Dialog} [role='status']", new PageLocatorOptions { HasText = "Moving to Beacon…" });

            await line.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.True(await line.EvaluateAsync<bool>("element => element === document.activeElement || element.contains(document.activeElement)"), "The in-flight line does not hold focus.");
            Assert.Equal(["Draft the brief"], await TitlesInAsync(page, "Todo"));

            await page.Keyboard.PressAsync("Enter");

            if (await MoveToProjectControl(page).CountAsync() == 1)
            {
                await MoveToProjectControl(page).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            }

            Assert.Single(api.ProjectMoves);

            api.ReleaseProjectMove();

            await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.Empty(await TitlesInAsync(page, "Todo"));
            Assert.Single(api.ProjectMoves);
        }
    }

    /// <summary>
    /// A refused move keeps the panel open with nothing moved: a destination whose set changed since it was read re-reads it
    /// and shows the Status step; a destination that is gone re-reads the Project list and shows the first step again without
    /// it; a full destination says so in the panel.
    /// </summary>
    /// <param name="refusal">The refusal the held move is released with.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = Red)]
    [InlineData("status-destination-required")]
    [InlineData("invalid-task-project")]
    [InlineData("bound-reached")]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "UX-DR32")]
    public async Task A_refused_move_keeps_the_panel_open_says_why_and_moves_nothing(string refusal)
    {
        var api = TwoSpaces();
        var tasks = SeedSpace(api, Northwind, ["Draft the brief"], Atlas, Beacon, Comet);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks["Draft the brief"].Id);

            var held = api.HoldProjectMoveAsync();

            await OpenPickerAsync(page);
            await ChooseAsync(page, "Beacon");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            switch (refusal)
            {
                case "status-destination-required":
                    _ = WithoutTheFirstStatus(api, Northwind, Beacon);
                    api.ReleaseProjectMoveRefused(409, ProblemTypes.StatusDestinationRequired);
                    await page.Locator(Picker, new PageLocatorOptions { HasText = "“Todo” is not in Beacon. Choose a Status:" })
                        .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
                    Assert.Equal(["Doing", "Done"], await OptionsAsync(page));
                    break;

                case "invalid-task-project":
                    api.Projects[Northwind].RemoveAll(project => project.Id == Beacon);
                    api.ReleaseProjectMoveRefused(400, ProjectMoveWire.InvalidTaskProject);
                    await page.Locator(PickerOption, new PageLocatorOptions { HasText = "Comet" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
                    Assert.Equal(["Comet"], await OptionsAsync(page));
                    break;

                default:
                    api.ReleaseProjectMoveRefused(409, ProblemTypes.BoundReached);
                    await page.Locator(Refusal, new PageLocatorOptions { HasText = "Beacon is full (5,000 Tasks). Nothing was moved." })
                        .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
                    break;
            }

            Assert.Equal(1, await page.Locator(Dialog).CountAsync());
            Assert.Equal(["Draft the brief"], await TitlesInAsync(page, "Todo"));
            Assert.Single(api.ProjectMoves);
        }
    }

    /// <summary>
    /// A move that never got an answer does not claim nothing moved: the panel stays open and says the request was not sent.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = Red)]
    [Trait("Requirement", "UX-DR32")]
    public async Task A_move_that_got_no_answer_does_not_claim_nothing_moved()
    {
        var api = TwoSpaces();
        var tasks = SeedSpace(api, Northwind, ["Draft the brief"], Atlas, Beacon);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks["Draft the brief"].Id);

            var held = api.HoldProjectMoveAsync();

            await OpenPickerAsync(page);
            await ChooseAsync(page, "Beacon");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            api.ReleaseProjectMoveUnanswered();

            await page.Locator(Refusal, new PageLocatorOptions { HasText = NotSentCopy }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(1, await page.Locator(Dialog).CountAsync());
            Assert.DoesNotContain(NothingMoved, await page.Locator(Dialog).InnerTextAsync(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// With no other Project in the Space the control is still offered, and its picker says there is none in one line; Esc
    /// closes it with focus back on the control.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = Red)]
    [Trait("Requirement", "UX-DR21")]
    public async Task With_no_other_Project_the_picker_says_so_and_Esc_closes_it()
    {
        var api = TwoSpaces();
        var tasks = SeedSpace(api, Northwind, ["Draft the brief"], Atlas);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks["Draft the brief"].Id);
            await MoveToProjectControl(page).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            await page.Locator(Picker, new PageLocatorOptions { HasText = NoOtherProjectCopy }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await page.Keyboard.PressAsync("Escape");
            await page.Locator(Picker).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.Equal(MoveToProject, await ActiveTextAsync(page));
            Assert.Empty(api.ProjectMoves);
        }
    }

    /// <summary>
    /// At 320px the control and both steps are visible and fit the viewport, with no horizontal scroll.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = Red)]
    [Trait("Requirement", "UX-DR41")]
    public async Task The_picker_is_usable_at_320px()
    {
        var api = TwoSpaces();
        var tasks = SeedSpace(api, Northwind, ["Draft the brief"], Atlas, Beacon, Comet);
        _ = WithoutTheFirstStatus(api, Northwind, Comet);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(320, 800);
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks["Draft the brief"].Id);
            await OpenPickerAsync(page);
            await AssertFitsAsync(page);

            await ChooseAsync(page, "Comet");
            await page.Locator(PickerOption, new PageLocatorOptions { HasText = "Doing" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await AssertFitsAsync(page);
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

    private static ILocator MoveToProjectControl(IPage page) =>
        page.Locator(Dialog).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = MoveToProject, Exact = true });

    private static ILocator ColumnNamed(IPage page, string status) =>
        page.Locator(Column, new PageLocatorOptions { Has = page.Locator("h2", new PageLocatorOptions { HasText = status }) });

    private static async Task<List<string>> TitlesInAsync(IPage page, string status) =>
        [.. (await ColumnNamed(page, status).Locator(CardTitle).AllTextContentsAsync()).Select(title => title.Trim())];

    private static async Task<List<string>> OptionsAsync(IPage page) =>
        [.. (await page.Locator(PickerOption).AllInnerTextsAsync()).Select(option => option.Trim())];

    private static Task<string> ActiveTextAsync(IPage page) =>
        page.EvaluateAsync<string>("() => document.activeElement ? document.activeElement.textContent.trim() : ''");

    private static Task<string> FocusedColumnAsync(IPage page) =>
        page.EvaluateAsync<string>("() => { const column = document.activeElement && document.activeElement.closest('.board-column'); const heading = column && column.querySelector('h2'); return heading ? heading.textContent.trim() : ''; }");

    private static async Task OpenPanelAsync(IPage page, Guid taskId)
    {
        await page.Locator($"{Card} button[data-task-id='{taskId:D}']").ClickAsync(new LocatorClickOptions { Timeout = Timeout });
        await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    private static async Task OpenPickerAsync(IPage page)
    {
        await MoveToProjectControl(page).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
        await page.Locator(PickerOption).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    private static Task ChooseAsync(IPage page, string option) =>
        page.Locator(PickerOption, new PageLocatorOptions { HasText = option }).First.ClickAsync(new LocatorClickOptions { Timeout = Timeout });

    private static async Task AssertFitsAsync(IPage page)
    {
        var options = page.Locator(PickerOption);

        for (var index = 0; index < await options.CountAsync(); index++)
        {
            var box = await options.Nth(index).BoundingBoxAsync();

            Assert.NotNull(box);
            Assert.True(box.X >= 0 && box.X + box.Width <= 320, $"Option {index} spans {box.X}..{box.X + box.Width}px at 320px.");
        }

        Assert.True(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= 320"), "The page scrolls horizontally at 320px.");
    }

    private string Address(string path) => new Uri(surface.BaseAddress, path.TrimStart('/')).ToString();

    private async Task OpenBoardAsync(FakeYelloApi api, IPage page, Guid spaceId, Guid projectId)
    {
        if (!_installed)
        {
            _installed = true;
            await api.InstallAsync(page);
        }

        await page.GotoAsync(Address($"spaces/{spaceId:D}/projects/{projectId:D}"));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.Locator(Card).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }
}
