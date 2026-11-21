using Microsoft.Playwright;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Projects;

namespace Yello.Tests.Client;

/// <summary>
/// Project settings in a real browser: the Status delta editor that lists a Project's effective set in effective order, says
/// where each entry comes from, and adds, renames and reorders - with a keyboard path for the reorder, refusals inline in
/// copy of their own, a 320px layout, and nothing of it for a Member or a Viewer. The Board follows the Project's order.
/// </summary>
/// <remarks>
/// <para>
/// <b>The API is <see cref="FakeYelloApi"/></b> with the Project Status paths added (<c>FakeYelloApi.ProjectStatuses.cs</c>):
/// no Host, no database, no container.
/// </para>
/// <para>
/// <b>The locators and copy are this run's assumptions</b>, from the story and the mockup (<c>project-settings.html</c> §1):
/// the page at <c>/spaces/{s}/projects/{p}/settings</c>, reached from the Board by a link named "Project settings"; the
/// editor <c>main .status-delta-editor</c>, one <c>.status-delta-row</c> per Status holding its name in
/// <c>.status-delta-row__name</c> and its source chip <c>.status-source</c> ("Space default", "Added here", "Space default ·
/// renamed here (was {name})"); per row the buttons "Move up", "Move down" and "Rename"; a text field named "New Status
/// name" and a button "Add a Status"; the polite announcement "{name} moved to position {n} of {total}."; and the name-taken
/// refusal "That name is already used in this Project.". A different locator in the built page is corrected here and
/// recorded in the Debug Log, never by weakening the assertion.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-25")]
[Trait("Requirement", "UX-DR15")]
public sealed partial class ProjectSettingsTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string Column = "main .board-column";
    private const string Editor = "main .status-delta-editor";
    private const string Row = ".status-delta-row";
    private const string RowName = ".status-delta-row__name";
    private const string SourceChip = ".status-source";
    private const string SettingsLink = "Project settings";
    private const string NameTakenCopy = "That name is already used in this Project.";

    private static readonly string[] StatusNames = ["Todo", "Doing", "Done"];

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();
    private static readonly Guid Beacon = Guid.NewGuid();

    /// <summary>
    /// An Owner follows "Project settings" from the Board to the editor, which lists the effective set in effective order,
    /// each row naming its source - a renamed default says so and states the default's own name - and offering one control
    /// named "Remove", never a bare "×".
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    public async Task The_Board_links_an_Owner_to_the_editor_which_names_each_Statuses_source()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas");
        Shape(api, Northwind, Atlas);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = SettingsLink, Exact = true }).ClickAsync();
            await page.Locator(Editor).Locator(Row).Nth(3).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(["Intake", "Doing", "Done", "Parked"], await RowNamesAsync(page));
            Assert.Equal(
                ["Space default · renamed here (was Todo)", "Space default", "Space default", "Added here"],
                [.. (await page.Locator(Editor).Locator(SourceChip).AllInnerTextsAsync()).Select(chip => chip.Trim())]);
            Assert.Equal(4, await page.Locator(Editor).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Remove", Exact = true }).CountAsync());
            Assert.Equal(0, await page.Locator(Editor).GetByText("×").CountAsync());
        }
    }

    /// <summary>
    /// A Member's and a Viewer's Board carry no "Project settings" link at all - absent from the DOM, not disabled - and the
    /// page reached by its address renders the set read-only: rows, and no button, no field and nothing disabled.
    /// </summary>
    /// <param name="role">The caller's Role code.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("member")]
    [InlineData("viewer")]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "FR-16")]
    public async Task A_Member_or_a_Viewer_has_no_settings_link_and_the_page_offers_no_control(string role)
    {
        var api = TwoSpaces();
        api.Roles[Northwind] = role;
        Seed(api, Northwind, Atlas, "Atlas");
        Shape(api, Northwind, Atlas);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await page.Locator(Column).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = SettingsLink }).CountAsync());

            await page.EvaluateAsync("path => Blazor.navigateTo(path)", ClientSettings(Northwind, Atlas));
            await page.Locator(Editor).Locator(Row).Nth(3).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(4, await page.Locator(Editor).Locator(Row).CountAsync());
            Assert.Equal(0, await page.Locator(Editor).Locator("button, input").CountAsync());
            Assert.Equal(0, await page.Locator("main [disabled], main [aria-disabled='true']").CountAsync());
            Assert.Empty(api.ProjectStatusWrites);
        }
    }

    /// <summary>
    /// "Move up" on the added Status sends one place after the Status above its new slot, re-renders from the answer, and
    /// announces the new position politely; back on the Board the columns follow the Project's order.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-9")]
    [Trait("Requirement", "UX-DR42")]
    public async Task Move_up_reorders_announces_the_new_position_and_the_Board_follows()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas");
        Shape(api, Northwind, Atlas);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind, Atlas);

            var parked = page.Locator(Editor).Locator(Row, new LocatorLocatorOptions { HasText = "Parked" });

            await parked.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Move up" }).ClickAsync();
            await page.Locator("[role='status'][aria-live='polite']", new PageLocatorOptions { HasText = "Parked moved to position 3 of 4." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            Assert.Equal(["Intake", "Doing", "Parked", "Done"], await RowNamesAsync(page));
            Assert.Single(api.ProjectStatusWrites);
            Assert.Contains(api.ProjectStatuses[(Northwind, Atlas)][1].Id.ToString("D"), api.ProjectStatusWrites[0].Body ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            await page.EvaluateAsync("path => Blazor.navigateTo(path)", ClientBoard(Northwind, Atlas));
            await page.Locator(Column).Nth(3).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(["Intake", "Doing", "Parked", "Done"], await ColumnNamesAsync(page));
        }
    }

    /// <summary>
    /// The small-viewport pager's tabs and the Task detail panel's Status picker take the Project's order and names from the
    /// Board's own answer: with "Parked" placed first and the first default renamed "Intake", both list Parked, Intake,
    /// Doing, Done.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR11")]
    public async Task The_pager_and_the_Status_picker_follow_the_Projects_order()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas");
        Shape(api, Northwind, Atlas);

        var set = api.ProjectStatuses[(Northwind, Atlas)];
        var parked = set[^1];

        set.RemoveAt(set.Count - 1);
        set.Insert(0, parked);

        var columns = set.Select(entry => new FakeColumn(entry.Id, entry.Name, 0, [])).ToList();
        var task = new FakeTask(Guid.NewGuid(), "Shaped work", columns[1].StatusId, "a00000", DateTimeOffset.UtcNow);

        columns[1].Tasks.Add(task);
        columns[1].Total++;
        api.Boards[(Northwind, Atlas)] = api.Boards[(Northwind, Atlas)] with { Columns = columns };

        string[] expected = ["Parked", "Intake", "Doing", "Done"];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(375, 800);
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await page.Locator("main [role='tab']").Nth(3).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var tabs = await page.Locator("main [role='tab']").AllTextContentsAsync();

            Assert.Equal(expected, tabs.Select(text => expected.First(name => text.Trim().StartsWith(name, StringComparison.Ordinal))));

            await page.SetViewportSizeAsync(1280, 800);
            await page.Locator($".task-card button[data-task-id='{task.Id:D}']").ClickAsync();

            var dialog = page.Locator("[role='dialog']");

            await dialog.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Status" }).ClickAsync();

            var options = dialog.Locator("[role='listbox'] [role='option']");

            await options.First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(expected, (await options.AllTextContentsAsync()).Select(name => name.Trim()));
        }
    }

    /// <summary>
    /// The add form sends the name and renders the answer: the new Status is last, marked "Added here".
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Adding_a_Status_renders_it_last_as_added_here()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas");
        Shape(api, Northwind, Atlas);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind, Atlas);
            await page.GetByRole(AriaRole.Textbox, new PageGetByRoleOptions { Name = "New Status name" }).FillAsync("Blocked");
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add a Status", Exact = true }).ClickAsync();
            await page.Locator(Editor).Locator(Row).Nth(4).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(["Intake", "Doing", "Done", "Parked", "Blocked"], await RowNamesAsync(page));
            Assert.Equal("Added here", (await page.Locator(Editor).Locator(Row).Nth(4).Locator(SourceChip).InnerTextAsync()).Trim());
        }
    }

    /// <summary>
    /// A rename refused <c>409 status-name-taken</c> renders its own copy in the inline error region, and the row keeps the
    /// name the server last admitted.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_refused_rename_says_why_inline_and_keeps_the_admitted_name()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas");
        Shape(api, Northwind, Atlas);
        api.NextProjectStatusRefusal = (409, ProblemTypes.StatusNameTaken);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind, Atlas);

            var parked = page.Locator(Editor).Locator(Row, new LocatorLocatorOptions { HasText = "Parked" });

            await parked.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Rename" }).ClickAsync();

            var field = parked.GetByRole(AriaRole.Textbox);

            await field.FillAsync("Done");
            await field.PressAsync("Enter");
            await page.GetByText(NameTakenCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Contains("Parked", await RowNamesAsync(page));
        }
    }

    /// <summary>
    /// At 320 by 640 CSS pixels the editor reflows: nothing scrolls sideways and every row's controls stay on screen.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-9")]
    public async Task The_editor_reflows_at_320_pixels_wide()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas");
        Shape(api, Northwind, Atlas);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(320, 640);
            await OpenSettingsAsync(api, page, Northwind, Atlas);

            Assert.True(
                await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1"),
                "The Project settings page scrolls sideways at 320px.");

            foreach (var box in await page.Locator(Editor).Locator("button").EvaluateAllAsync<double[]>("buttons => buttons.map(b => b.getBoundingClientRect().right)"))
            {
                Assert.True(box <= 320, $"A control ends at {box}px, past the 320px viewport.");
            }
        }
    }

    /// <summary>
    /// The editor's Space-scoped content is purged on a Space switch: after moving in-app to the other Space's home, no Status
    /// name that only the first Project held is anywhere on the page.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR31")]
    public async Task Switching_Space_purges_the_editors_Statuses()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas");
        Seed(api, Harbour, Beacon, "Beacon");
        Shape(api, Northwind, Atlas);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind, Atlas);
            await page.EvaluateAsync("path => Blazor.navigateTo(path)", $"/spaces/{Harbour:D}");
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var text = await page.Locator("body").InnerTextAsync();

            Assert.DoesNotContain("Parked", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Intake", text, StringComparison.Ordinal);
        }
    }

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
    /// A Project on Space home and its Board: three columns in the default order, no Tasks.
    /// </summary>
    private static void Seed(FakeYelloApi api, Guid spaceId, Guid projectId, string name)
    {
        api.Boards[(spaceId, projectId)] = new FakeBoard(
            new FakeBoardProject(projectId, name),
            [.. StatusNames.Select(status => new FakeColumn(Guid.NewGuid(), status, 0, []))]);
        api.Projects[spaceId] = [new FakeProject(projectId, name, DateTimeOffset.UtcNow)];
    }

    /// <summary>
    /// The Project's set: the first default renamed "Intake" here, then "Parked" added at the end - and the Board following.
    /// </summary>
    private static void Shape(FakeYelloApi api, Guid spaceId, Guid projectId)
    {
        api.SeedProjectStatuses(spaceId, projectId);

        var set = api.ProjectStatuses[(spaceId, projectId)];

        set[0] = set[0] with { Name = "Intake", DefaultName = StatusNames[0] };
        set.Add(new FakeProjectStatus(Guid.NewGuid(), "Parked", ProjectStatusSources.AddedHere, DefaultName: null, TaskCount: 0));

        api.Boards[(spaceId, projectId)] = api.Boards[(spaceId, projectId)] with
        {
            Columns = [.. set.Select(entry => new FakeColumn(entry.Id, entry.Name, 0, []))],
        };
    }

    private static async Task<string[]> RowNamesAsync(IPage page) =>
        [.. (await page.Locator(Editor).Locator(RowName).AllInnerTextsAsync()).Select(name => name.Trim())];

    private static async Task<string[]> ColumnNamesAsync(IPage page) =>
        [.. (await page.Locator(Column).Locator("h2, h3").AllTextContentsAsync()).Select(name => name.Trim())];

    private static string ClientBoard(Guid spaceId, Guid projectId) => $"/spaces/{spaceId:D}/projects/{projectId:D}";

    private static string ClientSettings(Guid spaceId, Guid projectId) => $"/spaces/{spaceId:D}/projects/{projectId:D}/settings";

    private string Address(string path) => new Uri(surface.BaseAddress, path.TrimStart('/')).ToString();

    private async Task OpenBoardAsync(FakeYelloApi api, IPage page, Guid spaceId, Guid projectId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address(ClientBoard(spaceId, projectId)));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    private async Task OpenSettingsAsync(FakeYelloApi api, IPage page, Guid spaceId, Guid projectId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address(ClientSettings(spaceId, projectId)));
        await page.Locator(Editor).Locator(Row).Nth(3).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }
}
