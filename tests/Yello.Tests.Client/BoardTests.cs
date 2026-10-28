using Microsoft.Playwright;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Spaces;
using Yello.Contracts.Tasks;

namespace Yello.Tests.Client;

/// <summary>
/// Story 2.2 in a real browser: the Board of a Project - columns by Status in effective order, the true total
/// in each column's chip, Task cards that are their title, creation into the first column, the refusals a
/// creation can meet rendered inline in copy of their own, and the Board treated as Space-scoped content
/// (rendered only after the server answers, purged with its Space, never named in the title or the URL).
/// Also the two re-arrival cases (AC14) and the skip control (Q9). AC1-AC7, AC13-AC15; UX-DR8, UX-DR26,
/// UX-DR27, UX-DR31, UX-DR33, UX-DR35, UX-DR37. Written red by the story 2.2 ATDD run; activated by its dev phase.
/// </summary>
/// <remarks>
/// <para>
/// <b>The API is <see cref="FakeYelloApi"/> at the network edge</b> with the Board and Task routes added
/// (no Host, no database, no container); a held Board answer (<see cref="FakeYelloApi.HoldBoardAsync"/>) makes
/// "before the server answers" observable.
/// </para>
/// <para>
/// <b>The locators are the Board's markup</b>, as the ATDD run assumed it and the dev phase built it: a column is <c>.board-column</c>, its chip <c>.column-count</c> and a card
/// <c>.task-card</c> (the DESIGN.md component names the design gates also use); the create affordance and
/// the empty state's action are buttons named "Add a Task"; the creation field is the one text input in the
/// first column; the cold-load announcement reads "Board loaded."; the skip control is a button named "Skip to
/// content". The copy the story fixes ("Nothing here yet.", "Task added.", the bound and idempotency
/// sentences) is asserted as written.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-19")]
[Trait("Requirement", "FR-28")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class BoardTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string ContextBar = "nav.context-bar";
    private const string SwitcherTrigger = "nav.context-bar button[aria-controls]";

    private const string Column = "main .board-column";
    private const string ColumnCount = ".column-count";
    private const string Card = ".task-card";

    private const string EmptyState = "Nothing here yet.";
    private const string AddTask = "Add a Task";
    private const string Added = "Task added.";
    private const string Loaded = "Board loaded.";
    private const string SkipControl = "Skip to content";
    private const string BoundCopy = "This Project has 5000 Tasks, the maximum.";
    private const string InFlightCopy = "That was already sent. Reload to see it.";
    private const string ReusedCopy = "That request was already used for something else. Try again.";
    private const string NotAvailableCopy = "Not available.";

    private static readonly string[] StatusNames = ["Todo", "Doing", "Done"];

    private static readonly string[] ReusableFieldNames = [string.Empty, "NAME", "TITLE", "TASK", "TASKTITLE", "TASK-TITLE"];

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();
    private static readonly Guid Beacon = Guid.NewGuid();

    /// <summary>
    /// AC1, UX-DR35 - a Project holding no Tasks shows a column for each Status of its effective set, in its
    /// effective order, states "Nothing here yet." and offers one primary action to add a Task - and nothing
    /// about Statuses or configuration.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_empty_Board_shows_its_columns_in_order_and_one_action_to_add_a_Task()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas", []);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await page.GetByText(EmptyState).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(StatusNames, await ColumnNamesAsync(page));
            Assert.Equal(1, await page.Locator("main", new PageLocatorOptions { HasText = EmptyState }).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = AddTask, Exact = true }).CountAsync());

            var text = await page.Locator("main").InnerTextAsync();

            Assert.DoesNotContain("configure", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Add a Task and get on with it", text, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC15, UX-DR31 - while the Board answer is held, nothing of the Board renders: no Task, no empty state,
    /// and skeleton columns with no text marked <c>aria-busy="true"</c>; released, the Tasks appear, the
    /// skeleton goes, and completion is announced politely, once.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR31")]
    public async Task The_Board_renders_only_after_the_server_answers()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas", ["Draft the brief"]);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);

            var arrived = api.HoldBoardAsync(Atlas);

            await page.GotoAsync(Address(ClientBoard(Northwind, Atlas)));
            await arrived.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            var main = await page.Locator("main").InnerTextAsync();

            Assert.DoesNotContain("Draft the brief", main, StringComparison.Ordinal);
            Assert.DoesNotContain(EmptyState, main, StringComparison.Ordinal);
            Assert.True(await page.Locator("main [aria-busy='true']").CountAsync() >= 1, "Nothing in main says the Board is loading.");
            Assert.True(await page.Locator("main [aria-busy='true'] .board-column").CountAsync() >= 1, "The busy region holds no skeleton columns.");

            api.ReleaseBoard(Atlas);

            await page.Locator(Card, new PageLocatorOptions { HasText = "Draft the brief" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            Assert.Equal(0, await page.Locator("main [aria-busy='true']").CountAsync());

            await page.Locator("[role='status'][aria-live='polite']", new PageLocatorOptions { HasText = Loaded })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });
        }
    }

    /// <summary>
    /// AC3, UX-DR27, Q11 (a) - a Viewer's Board is a Member's Board without the create affordance: the same
    /// columns and cards, no "Add a Task" anywhere, and nothing disabled. Anchor: the Owner of the other Space,
    /// with the same Board, has the affordance. Story 2.6 extends it: the Viewer's cards carry no actions control, and
    /// <c>MoveTests.A_Viewer_is_offered_no_move_control_and_nothing_is_disabled</c> holds the rest of the Move control's
    /// absence. The drag handle arrives in 2.7.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    public async Task A_Viewer_sees_the_same_Board_with_no_create_affordance_and_nothing_disabled()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas", ["Shared card"]);
        Seed(api, Harbour, Beacon, "Beacon", ["Shared card"]);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await page.Locator(Card, new PageLocatorOptions { HasText = "Shared card" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.True(await AddTaskControls(page).CountAsync() >= 1, "Anchor: the Owner has no create affordance.");
            Assert.True(await page.Locator(".task-card__actions").CountAsync() >= 1, "Anchor: the Owner's cards have no actions control.");

            await page.EvaluateAsync("path => Blazor.navigateTo(path)", ClientBoard(Harbour, Beacon));
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.Locator(Card, new PageLocatorOptions { HasText = "Shared card" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(StatusNames, await ColumnNamesAsync(page));
            Assert.Equal(0, await AddTaskControls(page).CountAsync());
            Assert.Equal(0, await page.Locator(".task-card__actions").CountAsync());
            Assert.Equal(0, await page.Locator("main input").CountAsync());
            Assert.Equal(0, await page.Locator("main [disabled], main [aria-disabled='true']").CountAsync());
        }
    }

    /// <summary>
    /// AC5, AD-29 - a column header shows the Status name, then the server's true total, then the create
    /// affordance; the chip shows the total even when the answer carries fewer Tasks than it (2.9's paging),
    /// so it is never the number of rows rendered.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-29")]
    public async Task A_column_header_shows_the_name_then_the_servers_total_then_the_create_affordance()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas", ["One", "Two"]);
        api.Boards[(Northwind, Atlas)].Columns[0].Total = 7;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await page.Locator(Card, new PageLocatorOptions { HasText = "Two" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var first = page.Locator(Column).First;

            Assert.Equal(2, await first.Locator(Card).CountAsync());
            Assert.Equal("7", (await first.Locator(ColumnCount).TextContentAsync())?.Trim());

            // Name, then total, then the affordance, in document order (textContent: the uppercase is CSS's).
            var order = await first.EvaluateAsync<string[]>(
                """
                column => [...column.querySelectorAll('h2, h3, .column-count, button')]
                    .filter(element => !element.closest('.task-card'))
                    .map(element => element.matches('.column-count') ? 'count' : element.matches('button') ? 'button' : 'name')
                """);

            Assert.Equal(["name", "count", "button"], order.Distinct().ToArray());
        }
    }

    /// <summary>
    /// AC6 - a Task card's only content is its title (no badge, no count, no streak), and it carries no shadow.
    /// Its one control, since story 2.3, is the title itself as the button that opens the Task.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR8")]
    public async Task A_Task_card_is_its_title_alone_and_carries_no_shadow()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas", ["Draft the brief"]);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);

            var card = page.Locator(Card, new PageLocatorOptions { HasText = "Draft the brief" });
            await card.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal("Draft the brief", (await card.TextContentAsync())?.Trim());
            Assert.Equal("none", await card.EvaluateAsync<string>("card => getComputedStyle(card).boxShadow"));
            // Widened by story 2.3 (open question 7 (a)): the native button that opens the Task detail panel, named by
            // the title. Widened again by story 2.6 (open question 6 (a)), deliberately: for a Role that can move Tasks -
            // this Owner - the card's actions control, named for the Task, which opens its menu. Nothing else in the card
            // is a control, and the actions control adds no text to the card.
            Assert.Equal(2, await card.Locator("button, a, [tabindex]").CountAsync());
            Assert.Equal(1, await card.Locator("button[data-task-id]").CountAsync());
            Assert.Equal(1, await card.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Draft the brief", Exact = true }).CountAsync());
            Assert.Equal(1, await card.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Actions for Draft the brief", Exact = true }).CountAsync());
        }
    }

    /// <summary>
    /// AC2, AC15, AD-18, UX-DR33 - adding a Task appends its card to the first column, raises that column's total
    /// by one, keeps the field open and empty for the next, and announces "Task added." politely; the request
    /// carries an <c>Idempotency-Key</c>; the title input is <c>autocomplete="off"</c> with a field name autofill
    /// would not reuse; and neither the title nor a Project name reaches <c>document.title</c> or the URL.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "UX-DR33")]
    public async Task Adding_a_Task_appends_it_to_the_first_column_and_keeps_the_field_open()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas", []);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await page.GetByText(EmptyState).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await AddTaskAsync(page, "Draft the brief");

            var first = page.Locator(Column).First;

            await first.Locator(Card, new LocatorLocatorOptions { HasText = "Draft the brief" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal("1", (await first.Locator(ColumnCount).TextContentAsync())?.Trim());
            Assert.Equal(string.Empty, await first.Locator("input[type='text']").InputValueAsync());

            var post = Assert.Single(api.Requests, request => request.Method == "POST" && request.Path == TaskRoutes.CreateIn(Northwind, Atlas));

            Assert.False(string.IsNullOrWhiteSpace(post.Header(Idempotency.HeaderName)), "The creation carried no Idempotency-Key.");
            Assert.Contains(Added, string.Join(' ', await page.Locator("[aria-live='polite']").AllInnerTextsAsync()), StringComparison.Ordinal);
            Assert.Equal("Yello", await page.TitleAsync());
            Assert.DoesNotContain("Draft", page.Url, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Atlas", page.Url, StringComparison.OrdinalIgnoreCase);

            await AssertTitleInputsAreNotAutofilledAsync(page);
        }
    }

    /// <summary>
    /// AC7, UX-DR35 - the 5,001st Task is refused visibly, inline in the creation surface, with the bound
    /// stated plainly; nothing is added.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-30")]
    public async Task The_5001st_Task_renders_the_bound_inline_and_adds_nothing()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas", ["Already here"]);
        api.TaskBoundReached = true;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await page.Locator(Card, new PageLocatorOptions { HasText = "Already here" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await AddTaskAsync(page, "One too many");

            await page.Locator("main", new PageLocatorOptions { HasText = BoundCopy }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            Assert.Equal(0, await page.Locator(Card, new PageLocatorOptions { HasText = "One too many" }).CountAsync());
        }
    }

    /// <summary>
    /// AC13, UX-DR32, UX-DR37, <c>deferred-work.md:460</c> - a creation answered
    /// <c>409 idempotency-key-in-flight</c> or <c>422 idempotency-key-reused</c> renders copy of its own, inline
    /// in the creation surface - never the generic "Not available." - and adds no card.
    /// </summary>
    /// <param name="status">The refusal's status.</param>
    /// <param name="inFlight">Whether it is the in-flight refusal (otherwise the reused-key one).</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(409, true)]
    [InlineData(422, false)]
    [Trait("Requirement", "UX-DR32")]
    public async Task An_idempotency_refusal_renders_its_own_copy_not_Not_available(int status, bool inFlight)
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas", ["Already here"]);
        api.NextTaskCreateRefusal = (status, inFlight ? ProblemTypes.IdempotencyKeyInFlight : ProblemTypes.IdempotencyKeyReused);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await page.Locator(Card, new PageLocatorOptions { HasText = "Already here" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await AddTaskAsync(page, "Sent before");

            await page.Locator("main", new PageLocatorOptions { HasText = inFlight ? InFlightCopy : ReusedCopy }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.DoesNotContain(NotAvailableCopy, await page.Locator("main").InnerTextAsync(), StringComparison.Ordinal);
            Assert.Equal(0, await page.Locator(Card, new PageLocatorOptions { HasText = "Sent before" }).CountAsync());
        }
    }

    /// <summary>
    /// AC14, <c>deferred-work.md:481</c> - navigating to the same Board again does not unmount it: an open Task
    /// creation keeps its text, while the Role is still re-read from the server.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_open_Task_creation_survives_navigating_to_the_same_Board()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas", ["Already here"]);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await page.Locator(Card, new PageLocatorOptions { HasText = "Already here" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await AddTaskControls(page).First.ClickAsync();
            await page.Locator(Column).First.Locator("input[type='text']").FillAsync("Half typed");

            var activeReads = ActiveReads(api);

            await page.EvaluateAsync("path => Blazor.navigateTo(path)", ClientBoard(Northwind, Atlas));
            await WaitUntilAsync(() => ActiveReads(api) > activeReads, "The Role was not re-read from the server on re-arrival.");

            Assert.Equal("Half typed", await page.Locator(Column).First.Locator("input[type='text']").InputValueAsync());
        }
    }

    /// <summary>
    /// AC14, UX-DR27 - the Role re-read on re-arrival is applied in place: a Member made a Viewer meanwhile
    /// loses the open Task field and every create affordance, while the Board itself stays.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    public async Task A_Role_lowered_since_arrival_closes_the_open_Task_creation_on_re_arrival()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas", ["Already here"]);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await page.Locator(Card, new PageLocatorOptions { HasText = "Already here" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await AddTaskControls(page).First.ClickAsync();
            await page.Locator(Column).First.Locator("input[type='text']").FillAsync("Half typed");

            api.Roles[Northwind] = "viewer";

            await page.EvaluateAsync("path => Blazor.navigateTo(path)", ClientBoard(Northwind, Atlas));
            await page.Locator(Column).First.Locator("input[type='text']").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.Equal(0, await AddTaskControls(page).CountAsync());
            Assert.Equal(1, await page.Locator(Card, new PageLocatorOptions { HasText = "Already here" }).CountAsync());
        }
    }

    /// <summary>
    /// AC14, <c>deferred-work.md:481</c> - choosing the Space one is already in, in the switcher, does not drop an
    /// open Project create form on Space home or its text.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_open_Project_create_on_Space_home_survives_choosing_the_same_Space()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(Address($"spaces/{Northwind:D}"));
            await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.GetByText("No Projects yet.").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Create a Project" }).ClickAsync();
            await page.Locator("main input[type='text']").First.FillAsync("Half named");

            var activeReads = ActiveReads(api);

            await ChooseInSwitcherAsync(page, "Northwind Redesign");
            await WaitUntilAsync(() => ActiveReads(api) > activeReads, "The Role was not re-read from the server on re-arrival.");

            Assert.Equal("Half named", await page.Locator("main input[type='text']").First.InputValueAsync());
        }
    }

    /// <summary>
    /// AC15, UX-DR31, UX-DR33 - leaving the Space purges the Board before the destination answers: nothing of
    /// the departed Board's Tasks or Project name stays on screen, and no name ever reached the title.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR31")]
    public async Task Leaving_the_Space_purges_the_Board_before_the_destination_answers()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas only here", ["Northwind card"]);
        api.Projects[Harbour] = [new FakeProject(Beacon, "Harbour project", DateTimeOffset.UtcNow)];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await page.Locator(Card, new PageLocatorOptions { HasText = "Northwind card" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var held = api.HoldProjectsAsync(Harbour);
            await ChooseInSwitcherAsync(page, "Harbour Ops");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            var body = await page.Locator("body").InnerTextAsync();

            Assert.DoesNotContain("Northwind card", body, StringComparison.Ordinal);
            Assert.DoesNotContain("Atlas only here", body, StringComparison.Ordinal);
            Assert.Equal("Yello", await page.TitleAsync());

            api.ReleaseProjects(Harbour);
            await page.GetByText("Harbour project").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        }
    }

    /// <summary>
    /// Task 10, 2.1 Q10 - a Project row on Space home is a link to its Board: the link text is the Project name,
    /// the address holds ids only, and following it renders the Board.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_Project_row_links_to_its_Board_by_id()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas", ["Draft the brief"]);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(Address($"spaces/{Northwind:D}"));

            var link = page.Locator("main").GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Atlas", Exact = true });
            await link.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.EndsWith(ClientBoard(Northwind, Atlas), await link.GetAttributeAsync("href") ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            await link.ClickAsync();
            await page.Locator(Card, new PageLocatorOptions { HasText = "Draft the brief" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.DoesNotContain("Atlas", page.Url, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Task 10 (DESIGN.md:454) - on the Board the context bar names the Project, taken from this Board answer -
    /// not from Space home's list, which here names it differently - and clears it on leaving the Board.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_context_bar_names_the_Project_from_the_Boards_own_answer()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas as the Board names it", []);
        api.Projects[Northwind] = [new FakeProject(Atlas, "Atlas as the list names it", DateTimeOffset.UtcNow)];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1440, 900);
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await page.Locator(ContextBar, new PageLocatorOptions { HasText = "Atlas as the Board names it" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.DoesNotContain("Atlas as the list names it", await page.Locator(ContextBar).InnerTextAsync(), StringComparison.Ordinal);

            await page.EvaluateAsync("path => Blazor.navigateTo(path)", ClientSpaceHome(Northwind));
            await page.GetByText("Atlas as the list names it").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.DoesNotContain("Atlas as the Board names it", await page.Locator(ContextBar).InnerTextAsync(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC15, Q9 (a), <c>deferred-work.md:338</c> - on Space home and on the Board, the first tab stop is a skip
    /// control; pressing it moves focus to the page's heading, past the context bar.
    /// </summary>
    /// <param name="onBoard">Whether the page is the Board (otherwise Space home).</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "NFR-9")]
    public async Task The_first_tab_stop_skips_the_context_bar_to_the_page_heading(bool onBoard)
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas", ["Draft the brief"]);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(Address(onBoard ? ClientBoard(Northwind, Atlas) : ClientSpaceHome(Northwind)));
            await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.Locator("main h1").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            // Focus starts at the top of the document. blur() alone leaves Chromium's sequential-focus
            // starting point on the heading the arrival focused, so the next Tab would continue from there.
            await page.EvaluateAsync("() => { document.activeElement?.blur(); document.body.tabIndex = -1; document.body.focus(); document.body.removeAttribute('tabindex'); }");
            await page.Keyboard.PressAsync("Tab");

            Assert.Equal(SkipControl, (await page.EvaluateAsync<string>("() => document.activeElement?.textContent ?? ''")).Trim());

            await page.Keyboard.PressAsync("Enter");

            Assert.True(
                await page.EvaluateAsync<bool>("() => document.activeElement === document.querySelector('main h1')"),
                "The skip control did not move focus to the page heading.");
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
    /// A Project on Space home and its Board: three columns in the default order, the titles in the first
    /// column, each column's total equal to its Tasks unless a case changes it.
    /// </summary>
    private static void Seed(FakeYelloApi api, Guid spaceId, Guid projectId, string name, string[] titles)
    {
        var columns = StatusNames.Select(status => new FakeColumn(Guid.NewGuid(), status, 0, [])).ToList();

        foreach (var title in titles)
        {
            columns[0].Tasks.Add(new FakeTask(Guid.NewGuid(), title, columns[0].StatusId, $"a{columns[0].Tasks.Count:D5}", DateTimeOffset.UtcNow));
            columns[0].Total++;
        }

        api.Boards[(spaceId, projectId)] = new FakeBoard(new FakeBoardProject(projectId, name), columns);
        api.Projects[spaceId] = [new FakeProject(projectId, name, DateTimeOffset.UtcNow)];
    }

    private static ILocator AddTaskControls(IPage page) =>
        page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = AddTask, Exact = true });

    private static async Task<string[]> ColumnNamesAsync(IPage page) =>
        [.. (await page.Locator(Column).Locator("h2, h3").AllTextContentsAsync()).Select(name => name.Trim())];

    /// <summary>
    /// Opens the creation field from the first create affordance, types the title and submits.
    /// </summary>
    private static async Task AddTaskAsync(IPage page, string title)
    {
        await AddTaskControls(page).First.ClickAsync();

        var field = page.Locator(Column).First.Locator("input[type='text']");
        await field.FillAsync(title);
        await field.PressAsync("Enter");
    }

    /// <summary>
    /// UX-DR33: every Task title input is <c>autocomplete="off"</c> and carries a field name a browser would not
    /// match to a saved value. Anchored: at least one input is present.
    /// </summary>
    private static async Task AssertTitleInputsAreNotAutofilledAsync(IPage page)
    {
        var inputs = page.Locator("main input[type='text']");

        Assert.True(await inputs.CountAsync() >= 1, "No title input is present, so nothing was checked.");

        foreach (var input in await inputs.AllAsync())
        {
            Assert.Equal("off", await input.GetAttributeAsync("autocomplete"));
            Assert.DoesNotContain((await input.GetAttributeAsync("name") ?? string.Empty).ToUpperInvariant(), ReusableFieldNames);
        }
    }

    private static int ActiveReads(FakeYelloApi api) =>
        api.Requests.Count(request => request.Method == "GET" && request.Path.StartsWith(SpaceRoutes.Directory + "/", StringComparison.Ordinal) && request.Path.Split('/').Length == 5);

    /// <summary>
    /// Polls a condition with a deadline, never a sleep.
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition, string because)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(Timeout);

        while (!condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, because);
            await Task.Yield();
        }
    }

    private static string ClientSpaceHome(Guid spaceId) => $"/spaces/{spaceId:D}";

    /// <summary>
    /// The client's Board page for one Project - <c>ClientRoutes.Board</c>, which this assembly cannot reference: ids only.
    /// </summary>
    private static string ClientBoard(Guid spaceId, Guid projectId) => $"/spaces/{spaceId:D}/projects/{projectId:D}";

    private string Address(string path) => new Uri(surface.BaseAddress, path.TrimStart('/')).ToString();

    private async Task OpenBoardAsync(FakeYelloApi api, IPage page, Guid spaceId, Guid projectId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address(ClientBoard(spaceId, projectId)));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    private static async Task ChooseInSwitcherAsync(IPage page, string name)
    {
        var trigger = page.Locator(SwitcherTrigger);
        var list = await trigger.GetAttributeAsync("aria-controls");

        await trigger.ClickAsync();

        var row = page.Locator($"[id='{list}'] a", new PageLocatorOptions { HasText = name });
        await row.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await row.ClickAsync();
    }
}
