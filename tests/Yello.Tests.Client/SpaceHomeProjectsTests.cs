using Microsoft.Playwright;
using Xunit;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// Story 2.1 in a real browser: Space home lists the active Space's Projects, states the empty Space
/// plainly, creates and renames, refuses the 51st visibly, and treats the list as Space-scoped content -
/// rendered only after the server answers, purged with its Space, never in the title, and with every
/// control the Role lacks absent rather than disabled. AC1-AC5, AC13; UX-DR26, UX-DR27, UX-DR31, UX-DR33,
/// UX-DR35, UX-DR37.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 2.1 ATDD run on 2026-10-25.</b> Every case is skipped
/// with <c>red: AC&lt;n&gt;</c> until Task 10 builds the page. The API is <see cref="FakeYelloApi"/> at
/// the network edge, extended with the three Project routes (no Host, no database, no container), and a
/// held list answer (<see cref="FakeYelloApi.HoldProjectsAsync"/>) is what makes "before the server
/// answers" observable.
/// </para>
/// <para>
/// <b>Locators are by role and accessible name</b>, from the copy the story fixes ("No Projects yet.",
/// "Create a Project", "Rename", the bound sentence). If Task 10 names a control differently, correct the
/// locator and the copy together, and record it in the Debug Log.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-17")]
[Trait("Requirement", "FR-18")]
[Trait("Requirement", "UX-DR35")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class SpaceHomeProjectsTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string SwitcherTrigger = "nav.context-bar button[aria-controls]";

    private const string EmptyState = "No Projects yet.";
    private const string CreateAction = "Create a Project";
    private const string RenameAction = "Rename";
    private const string BoundCopy = "This Space has 50 Projects, the maximum.";

    private static readonly string[] ReusableFieldNames = [string.Empty, "NAME", "TITLE", "PROJECT", "PROJECTNAME", "PROJECT-NAME"];

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();

    /// <summary>
    /// AC1, UX-DR35 - an active Space holding no Projects states "No Projects yet." and offers exactly
    /// one action, to create a Project, and nothing that asks for configuration.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1 - Space home renders only its heading until story 2.1 Task 10")]
    public async Task An_empty_Space_states_no_Projects_yet_and_offers_one_action_to_create_one()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);

            await page.GetByText(EmptyState).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var main = page.Locator("main");
            var text = await main.InnerTextAsync();

            Assert.Equal(1, await main.GetByRole(AriaRole.Button).CountAsync() + await main.GetByRole(AriaRole.Link).CountAsync());
            Assert.Equal(1, await main.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = CreateAction }).CountAsync());
            Assert.DoesNotContain("Status", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("column", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("configure", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// AC13, UX-DR31, UX-DR35 - while the list answer is held, nothing of the list renders and the region
    /// is busy; released, the Projects appear.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC13 - Space home requests no Project list until story 2.1 Task 10")]
    [Trait("Requirement", "UX-DR31")]
    public async Task The_Project_list_renders_only_after_the_server_answers()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, "Atlas", "Beacon");

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);

            var arrived = api.HoldProjectsAsync(Northwind);

            await page.GotoAsync(Address($"spaces/{Northwind:D}"));
            await arrived.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            var main = await page.Locator("main").InnerTextAsync();

            Assert.DoesNotContain("Atlas", main, StringComparison.Ordinal);
            Assert.DoesNotContain(EmptyState, main, StringComparison.Ordinal);
            Assert.True(await page.Locator("main [aria-busy='true']").CountAsync() >= 1, "Nothing in main says the list is loading.");

            api.ReleaseProjects(Northwind);

            await page.GetByText("Beacon").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            Assert.Equal(0, await page.Locator("main [aria-busy='true']").CountAsync());
        }
    }

    /// <summary>
    /// AC2, AC3, AC4, UX-DR27 - a Viewer sees the same Projects a Member sees, and no create or rename
    /// control is present at all. Anchor: the Owner of the other Space, with the same Projects, sees both.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2 - Space home renders no Project list or controls until story 2.1 Task 10 (Q12)")]
    [Trait("Requirement", "UX-DR27")]
    public async Task A_Viewer_sees_the_Projects_and_no_create_or_rename_control()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, "Atlas", "Beacon");
        Seed(api, Harbour, "Atlas", "Beacon");

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);
            await page.GetByText("Beacon").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(1, await CreateControls(page).CountAsync());
            Assert.Equal(2, await RenameControls(page).CountAsync());

            await ChooseInSwitcherAsync(page, "Harbour Ops");
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.GetByText("Beacon").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Contains("Atlas", await page.Locator("main").InnerTextAsync(), StringComparison.Ordinal);
            Assert.Equal(0, await CreateControls(page).CountAsync());
            Assert.Equal(0, await RenameControls(page).CountAsync());
            Assert.Equal(0, await page.Locator("main [disabled], main [aria-disabled='true']").CountAsync());
        }
    }

    /// <summary>
    /// AC2, AC9, AC13, UX-DR33 - creating a Project adds its row and announces it; the request carries a
    /// fresh <c>Idempotency-Key</c>; the name input has <c>autocomplete="off"</c> and a field name a
    /// browser's autofill would not reuse; and the title stays <c>Yello</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2 - Space home has no create surface until story 2.1 Task 10")]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "UX-DR33")]
    public async Task Creating_a_Project_adds_its_row_announces_it_and_sends_an_idempotency_key()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);
            await page.GetByText(EmptyState).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await CreateAsync(page, "Lighthouse");

            await page.Locator("main li", new PageLocatorOptions { HasText = "Lighthouse" })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var post = Assert.Single(api.Requests, request => request.Method == "POST" && request.Path == ProjectRouteScaffold.ListIn(Northwind));

            Assert.False(string.IsNullOrWhiteSpace(post.Header(ProjectRouteScaffold.IdempotencyKeyHeader)), "The create carried no Idempotency-Key.");
            var announced = string.Join(' ', await page.Locator("[aria-live='polite']").AllInnerTextsAsync());

            Assert.Contains("Lighthouse", announced, StringComparison.Ordinal);
            Assert.Equal("Yello", await page.TitleAsync());

            await AssertNameInputsAreNotAutofilledAsync(page);
        }
    }

    /// <summary>
    /// AC5, UX-DR35 (<c>EXPERIENCE.md:276</c>) - the 51st Project is refused visibly, inline in the creation
    /// surface, with the bound stated plainly; nothing is added.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC5 - Space home has no create surface until story 2.1 Task 10")]
    [Trait("Requirement", "AR-30")]
    public async Task The_51st_Project_renders_the_bound_inline_and_adds_nothing()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, "Atlas");
        api.ProjectBoundReached = true;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);
            await page.GetByText("Atlas").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await CreateAsync(page, "One too many");

            await page.Locator("main", new PageLocatorOptions { HasText = BoundCopy }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator("main li", new PageLocatorOptions { HasText = "One too many" }).CountAsync());
        }
    }

    /// <summary>
    /// AC13, UX-DR31 - switching to another Space with its list held leaves nothing of the departed Space's
    /// Projects on screen; released, the destination's appear.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC13 - the Project list is not registered with ActiveSpaceState's purge until story 2.1 Task 10")]
    [Trait("Requirement", "UX-DR31")]
    public async Task A_switch_purges_the_departed_Spaces_Projects_before_the_destination_answers()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, "Northwind only");
        Seed(api, Harbour, "Harbour only");

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);
            await page.GetByText("Northwind only").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var held = api.HoldProjectsAsync(Harbour);
            await ChooseInSwitcherAsync(page, "Harbour Ops");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            var main = await page.Locator("main").InnerTextAsync();

            Assert.DoesNotContain("Northwind only", main, StringComparison.Ordinal);
            Assert.DoesNotContain("Harbour only", main, StringComparison.Ordinal);

            api.ReleaseProjects(Harbour);
            await page.GetByText("Harbour only").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        }
    }

    /// <summary>
    /// AC3, UX-DR26 - rename is an explicit control on each row; <c>Esc</c> cancels and sends nothing; the
    /// commit control reads "Rename", never "Save"; and the committed name replaces the row's.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 - Space home has no rename control until story 2.1 Task 10 (Q11)")]
    [Trait("Requirement", "UX-DR26")]
    public async Task Rename_is_an_explicit_control_and_Escape_cancels_without_a_request()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, "Working title");

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);

            var row = page.Locator("main li", new PageLocatorOptions { HasText = "Working title" });
            await row.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = RenameAction }).ClickAsync();
            await page.Keyboard.TypeAsync(" draft");
            await page.Keyboard.PressAsync("Escape");

            Assert.Equal(1, await page.Locator("main li", new PageLocatorOptions { HasText = "Working title" }).CountAsync());
            Assert.DoesNotContain(api.Requests, request => request.Method == "PATCH");

            await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = RenameAction }).ClickAsync();
            await AssertNameInputsAreNotAutofilledAsync(page);

            var field = page.Locator("main li input[type='text']");
            await field.FillAsync("Final title");
            await page.Locator("main li").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = RenameAction, Exact = true }).Last.ClickAsync();

            await page.Locator("main li", new PageLocatorOptions { HasText = "Final title" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Single(api.Requests, request => request.Method == "PATCH");
            Assert.Equal(0, await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save" }).CountAsync());
        }
    }

    /// <summary>
    /// A signed-in Account who is Owner of Northwind Redesign and Viewer of Harbour Ops, listed in that
    /// order, with no Projects yet.
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

    private static void Seed(FakeYelloApi api, Guid spaceId, params string[] names) =>
        api.Projects[spaceId] = [.. names.Select(name => new FakeProject(Guid.NewGuid(), name, DateTimeOffset.UtcNow))];

    private static ILocator CreateControls(IPage page) =>
        page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = CreateAction });

    private static ILocator RenameControls(IPage page) =>
        page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = RenameAction, Exact = true });

    /// <summary>
    /// Opens the creation surface if it is behind its action, names the Project and submits.
    /// </summary>
    private static async Task CreateAsync(IPage page, string name)
    {
        await CreateControls(page).First.ClickAsync();

        var field = page.Locator("main input[type='text']").First;
        await field.FillAsync(name);
        await field.PressAsync("Enter");
    }

    /// <summary>
    /// UX-DR33: every Project name input is <c>autocomplete="off"</c> and carries a field name a browser
    /// would not match to a saved value. Anchored: at least one input is present.
    /// </summary>
    private static async Task AssertNameInputsAreNotAutofilledAsync(IPage page)
    {
        var inputs = page.Locator("main input[type='text']");

        Assert.True(await inputs.CountAsync() >= 1, "No name input is present, so nothing was checked.");

        foreach (var input in await inputs.AllAsync())
        {
            Assert.Equal("off", await input.GetAttributeAsync("autocomplete"));

            var name = await input.GetAttributeAsync("name") ?? string.Empty;

            Assert.DoesNotContain(name.ToUpperInvariant(), ReusableFieldNames);
        }
    }

    private string Address(string path) => new Uri(surface.BaseAddress, path).ToString();

    private async Task OpenSpaceAsync(FakeYelloApi api, IPage page, Guid spaceId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address($"spaces/{spaceId:D}"));
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
