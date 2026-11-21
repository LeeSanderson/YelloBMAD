using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;
using Yello.Contracts.Projects;
using Yello.Contracts.Spaces;

namespace Yello.Tests.Client;

/// <summary>
/// The Default Statuses section of Space settings in a real browser: an Owner adds, renames and moves a default and each
/// change is announced; a rename some Projects conflict with opens a dialog that names each of them with its own name and
/// Task count, pre-focuses neither answer, and sends the one decision; a name a Project already uses is refused inline naming
/// it; a Project's Board takes the change on its next read; the section reflows at 320px; and a Member or a Viewer never sees
/// any of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The API is <see cref="FakeYelloApi"/></b> with the Space-level writes added (<c>FakeYelloApi.SpaceDefaultStatuses.cs</c>):
/// no Host, no database, no container. Three Projects share the Space's three defaults; "Atlas" renamed the first default
/// "Intake" and holds two Tasks in it, "Beacon" renamed it "Backlog", "Comet" changed nothing.
/// </para>
/// <para>
/// <b>The locators and copy are this run's assumptions</b>, from the story and the mockup (<c>project-settings.html</c> §2): the
/// section <c>main .space-default-statuses</c> headed "Default Statuses", before the Labels section; one
/// <c>.space-status-row</c> per default holding its name in <c>.space-status-row__name</c> and the buttons "Rename", "Move up"
/// and "Move down"; a "New Status name" field and an "Add a Status" button; the polite announcements "{name} moved to position
/// {n} of {total}.", "Renamed "{old}" to "{new}" in every Project." and "Renamed "{old}" to "{new}". {n} Projects kept their own
/// names."; a <c>[role='dialog']</c> headed "{n} Projects renamed this Status themselves." with a table of Project, name and
/// Tasks and the buttons "Rename these too", "Keep their names" and "Cancel". Quotes are matched loosely. A different locator
/// in the built page is corrected here and recorded in the Debug Log, never by weakening the assertion.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-24")]
[Trait("Requirement", "FR-27")]
[Trait("Requirement", "UX-DR16")]
public sealed class SpaceDefaultStatusesTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string Section = "main .space-default-statuses";
    private const string Row = ".space-status-row";
    private const string RowName = ".space-status-row__name";
    private const string Dialog = "[role='dialog']";
    private const string Polite = "[role='status'][aria-live='polite']";
    private const string ForOwnersAndAdminsCopy = "Space settings is for Owners and Admins.";

    private static readonly string[] StatusNames = ["Todo", "Doing", "Done"];

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();
    private static readonly Guid Beacon = Guid.NewGuid();
    private static readonly Guid Comet = Guid.NewGuid();

    /// <summary>
    /// An Owner's Space settings holds the section, with the three defaults in Space order, before the Labels section; a Member's
    /// or a Viewer's Space settings is refused as before and holds neither the section nor any of its controls.
    /// </summary>
    /// <param name="role">The Role held in the second Space.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("member")]
    [InlineData("viewer")]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "FR-16")]
    public async Task An_Owner_has_the_section_before_Labels_and_a_Member_or_a_Viewer_never_has_it(string role)
    {
        var api = Seeded(harbourRole: role);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            Assert.Equal(StatusNames, await RowNamesAsync(page));
            Assert.Equal(1, await page.Locator(Section).GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = "Default Statuses", Exact = true }).CountAsync());
            Assert.True(
                await page.EvaluateAsync<bool>(
                    "selector => { const section = document.querySelector(selector); const labels = [...document.querySelectorAll('main h2, main h3')].find(h => h.textContent.trim() === 'Labels'); return !!section && !!labels && !!(section.compareDocumentPosition(labels) & Node.DOCUMENT_POSITION_FOLLOWING); }",
                    Section),
                "The Default Statuses section is not before the Labels section.");

            await page.EvaluateAsync("path => Blazor.navigateTo(path)", $"/spaces/{Harbour:D}/settings");
            await page.Locator(".role-narration").GetByText(ForOwnersAndAdminsCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Section).CountAsync());
            Assert.Equal(0, await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Rename these too" }).CountAsync());
            Assert.Equal(0, await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Add a Status" }).CountAsync());
            Assert.Empty(api.SpaceDefaultStatusWrites);
        }
    }

    /// <summary>
    /// An Owner adds "Review", renames "Doing" to "Underway" - which no Project renamed, so no dialog opens - and moves "Done"
    /// up; each sends one write, re-renders from the answer and is announced politely.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR42")]
    [Trait("Requirement", "NFR-9")]
    public async Task Adding_renaming_and_moving_a_default_are_each_sent_once_and_announced()
    {
        var api = Seeded();

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            var section = page.Locator(Section);

            await section.GetByRole(AriaRole.Textbox, new LocatorGetByRoleOptions { Name = "New Status name" }).FillAsync("Review");
            await section.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Add a Status", Exact = true }).ClickAsync();
            await section.Locator(Row).Nth(3).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await Announcement(page, "Review").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            await RenameAsync(page, "Doing", "Underway");
            await Announcement(page, "Renamed .Doing. to .Underway. in every Project\\.").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Dialog).CountAsync());

            await RowOf(page, "Done").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Move up" }).ClickAsync();
            await Announcement(page, "Done moved to position 2 of 4\\.").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            Assert.Equal(["Todo", "Done", "Underway", "Review"], await RowNamesAsync(page));
            Assert.Equal(3, api.SpaceDefaultStatusWrites.Count);
        }
    }

    /// <summary>
    /// A rename to "Queued" that two Projects conflict with opens the dialog: its heading counts them, its table names each
    /// with its own name and its Task count, focus is on the heading, neither answer is focused or a submit, and Escape closes it
    /// having sent nothing more and returns focus to the rename field.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR42")]
    [Trait("Requirement", "NFR-8")]
    public async Task A_conflicting_rename_opens_a_dialog_naming_each_Project_with_nothing_pre_focused()
    {
        var api = Seeded();

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await RenameAsync(page, "Todo", "Queued");

            var dialog = page.Locator(Dialog);

            await dialog.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(1, await dialog.GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = "2 Projects renamed this Status themselves." }).CountAsync());
            Assert.Matches(new Regex("renaming the Space default .Todo. to .Queued.", RegexOptions.None, TimeSpan.FromSeconds(1)), await dialog.InnerTextAsync());

            var rows = (await dialog.Locator("tbody tr").AllInnerTextsAsync()).Select(row => Regex.Replace(row, "\\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim()).Order(StringComparer.Ordinal).ToList();

            Assert.Equal(["Atlas Intake 2", "Beacon Backlog 0"], rows);
            Assert.True(
                await page.EvaluateAsync<bool>("selector => { const dialog = document.querySelector(selector); const active = document.activeElement; return !!dialog && dialog.contains(active) && /^H[1-6]$/.test(active.tagName); }", Dialog),
                "Focus is not on the dialog's heading.");
            Assert.Equal(0, await dialog.Locator("button[type='submit'], button:focus").CountAsync());
            Assert.Equal(1, await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Rename these too" }).CountAsync());
            Assert.Equal(1, await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Keep their names" }).CountAsync());

            await page.Keyboard.PressAsync("Escape");
            await dialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.Single(api.SpaceDefaultStatusWrites);
            Assert.Equal(["Todo", "Doing", "Done"], await RowNamesAsync(page));
            Assert.True(
                await page.EvaluateAsync<bool>($"() => document.activeElement?.tagName === 'INPUT' && !!document.activeElement.closest('{Row}')"),
                "Focus did not return to the rename field.");
        }
    }

    /// <summary>
    /// Each answer in the dialog sends the rename once more with its one decision, closes the dialog, renders the new name and
    /// announces the outcome: "in every Project" when the Projects' names are replaced, and how many kept their own otherwise.
    /// </summary>
    /// <param name="replace">Whether "Rename these too" is chosen rather than "Keep their names".</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "UX-DR42")]
    public async Task Each_answer_sends_its_decision_once_and_is_announced(bool replace)
    {
        var api = Seeded();

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await RenameAsync(page, "Todo", "Queued");

            var dialog = page.Locator(Dialog);

            await dialog.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = replace ? "Rename these too" : "Keep their names" }).ClickAsync();
            await Announcement(page, replace ? "Renamed .Todo. to .Queued. in every Project\\." : "Renamed .Todo. to .Queued.\\. 2 Projects kept their own names\\.")
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Dialog).CountAsync());
            Assert.Equal(2, api.SpaceDefaultStatusWrites.Count);
            Assert.Contains($"\"replaceProjectNames\":{(replace ? "true" : "false")}", api.SpaceDefaultStatusWrites[1].Body ?? string.Empty, StringComparison.Ordinal);
            Assert.Equal("Queued", (await RowNamesAsync(page))[0]);
            Assert.Equal(replace ? "Queued" : "Intake", api.ProjectStatuses[(Northwind, Atlas)][0].Name);
        }
    }

    /// <summary>
    /// An add of a name one Project already holds is refused inline, in copy of its own that names that Project, and adds no
    /// row.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_name_a_Project_already_uses_is_refused_inline_naming_the_Project()
    {
        var api = Seeded();

        api.ProjectStatuses[(Northwind, Atlas)].Add(new FakeProjectStatus(Guid.NewGuid(), "Parked", ProjectStatusSources.AddedHere, DefaultName: null, TaskCount: 0));

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            var section = page.Locator(Section);

            await section.GetByRole(AriaRole.Textbox, new LocatorGetByRoleOptions { Name = "New Status name" }).FillAsync("parked");
            await section.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Add a Status", Exact = true }).ClickAsync();
            await section.GetByText("Atlas").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(StatusNames, await RowNamesAsync(page));
            Assert.DoesNotContain("Not available.", await section.InnerTextAsync(), StringComparison.Ordinal);
            Assert.Single(api.SpaceDefaultStatusWrites);
        }
    }

    /// <summary>
    /// After "Doing" is renamed "Underway" at Space level, the plain Project's Board, read afresh, names that column "Underway".
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_Projects_Board_names_the_renamed_column_on_its_next_read()
    {
        var api = Seeded();

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await RenameAsync(page, "Doing", "Underway");
            await Announcement(page, "Underway").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });
            await page.EvaluateAsync("path => Blazor.navigateTo(path)", $"/spaces/{Northwind:D}/projects/{Comet:D}");
            await page.Locator("main .board-column").Nth(2).Locator("h2, h3").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(
                ["Todo", "Underway", "Done"],
                (await page.Locator("main .board-column").Locator("h2, h3").AllTextContentsAsync()).Select(name => name.Trim()));
        }
    }

    /// <summary>
    /// At 320 by 640 CSS pixels the section and the open dialog reflow: nothing scrolls sideways and every control stays on
    /// screen.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-9")]
    public async Task The_section_and_the_dialog_reflow_at_320_pixels_wide()
    {
        var api = Seeded();

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(320, 640);
            await OpenSettingsAsync(api, page, Northwind);

            await AssertFitsAsync(page, Section, "The Default Statuses section");

            await RenameAsync(page, "Todo", "Queued");
            await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await AssertFitsAsync(page, Dialog, "The rename dialog");
        }
    }

    /// <summary>
    /// Two Spaces: Northwind, owned, with three Projects sharing its three defaults, and Harbour, held in
    /// <paramref name="harbourRole"/>.
    /// </summary>
    private static FakeYelloApi Seeded(string harbourRole = "viewer")
    {
        var api = new FakeYelloApi { SignedIn = true };

        api.Switcher.Add((Northwind, "Northwind Redesign"));
        api.Switcher.Add((Harbour, "Harbour Ops"));
        api.Roles[Northwind] = "owner";
        api.Roles[Harbour] = harbourRole;

        var defaults = StatusNames.Select((name, position) => new StatusResponse(Guid.NewGuid(), name, position)).ToList();

        api.SpaceDefaultStatuses[Northwind] = defaults;
        api.Projects[Northwind] =
        [
            new FakeProject(Atlas, "Atlas", DateTimeOffset.UtcNow),
            new FakeProject(Beacon, "Beacon", DateTimeOffset.UtcNow),
            new FakeProject(Comet, "Comet", DateTimeOffset.UtcNow),
        ];

        foreach (var (projectId, name) in new[] { (Atlas, "Atlas"), (Beacon, "Beacon"), (Comet, "Comet") })
        {
            api.Boards[(Northwind, projectId)] = new FakeBoard(
                new FakeBoardProject(projectId, name),
                [.. defaults.Select(status => new FakeColumn(status.Id, status.Name, 0, []))]);
            api.SeedProjectStatuses(Northwind, projectId);
        }

        var atlas = api.ProjectStatuses[(Northwind, Atlas)];
        var beacon = api.ProjectStatuses[(Northwind, Beacon)];

        atlas[0] = atlas[0] with { Name = "Intake", DefaultName = StatusNames[0], TaskCount = 2 };
        beacon[0] = beacon[0] with { Name = "Backlog", DefaultName = StatusNames[0] };

        return api;
    }

    private static ILocator RowOf(IPage page, string name) =>
        page.Locator(Section).Locator(Row, new LocatorLocatorOptions { Has = page.Locator(RowName, new PageLocatorOptions { HasTextRegex = new Regex($"^\\s*{Regex.Escape(name)}\\s*$", RegexOptions.None, TimeSpan.FromSeconds(1)) }) });

    private static ILocator Announcement(IPage page, string pattern) =>
        page.Locator(Polite, new PageLocatorOptions { HasTextRegex = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1)) });

    private static async Task RenameAsync(IPage page, string from, string to)
    {
        var row = RowOf(page, from);

        await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Rename" }).ClickAsync();

        var field = row.GetByRole(AriaRole.Textbox);

        await field.FillAsync(to);
        await field.PressAsync("Enter");
    }

    private static async Task<string[]> RowNamesAsync(IPage page) =>
        [.. (await page.Locator(Section).Locator(RowName).AllInnerTextsAsync()).Select(name => name.Trim())];

    private static async Task AssertFitsAsync(IPage page, string selector, string what)
    {
        Assert.True(
            await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1"),
            $"{what} makes the page scroll sideways at 320px.");

        foreach (var right in await page.Locator(selector).Locator("button, input").EvaluateAllAsync<double[]>("controls => controls.map(c => c.getBoundingClientRect().right)"))
        {
            Assert.True(right <= 320, $"{what} has a control ending at {right}px, past the 320px viewport.");
        }
    }

    private string Address(string path) => new Uri(surface.BaseAddress, path.TrimStart('/')).ToString();

    private async Task OpenSettingsAsync(FakeYelloApi api, IPage page, Guid spaceId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address($"/spaces/{spaceId:D}/settings"));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.Locator(Section).Locator(Row).Nth(2).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }
}
