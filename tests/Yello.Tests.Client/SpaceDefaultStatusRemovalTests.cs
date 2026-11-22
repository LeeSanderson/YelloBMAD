using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;
using Yello.Contracts.Projects;
using Yello.Contracts.Spaces;

namespace Yello.Tests.Client;

/// <summary>
/// Removing a Space default in Space settings, in a real browser: each default row has a "Remove" for an Owner or Admin and
/// none exists for a Member or a Viewer; an unoccupied default goes directly; an occupied one opens the report-and-ask dialog
/// with nothing chosen anywhere; choosing a Space-wide destination lists the Projects that cannot take it, each with its
/// count and an unset picker over its own set; a commit with a picker unset sends nothing and says why; in flight a progress
/// region names the scope and holds focus; a refusal at commit says nothing was moved and names the Project that blocked it;
/// a set left empty is refused inline naming the Project; the trap wraps both ways; and the rename's "keep" answer no longer
/// states a count.
/// </summary>
/// <remarks>
/// <para>
/// <b>The API is <see cref="FakeYelloApi"/></b> with the Space-level removal added
/// (<c>FakeYelloApi.SpaceDefaultStatusRemoval.cs</c>), deciding in the Host's order. Three Projects share the Space's three
/// defaults: "Atlas" holds three Tasks in "Queued"; "Beacon" removed "Doing", added "Parked" and holds two Tasks in "Queued";
/// "Comet" removed "Queued". Removing "Queued" to "Doing" therefore moves five Tasks across two Projects, Beacon is blocked, and
/// Comet is unaffected.
/// </para>
/// <para>
/// <b>The locators and copy are this run's assumptions</b>, from the story and the mockup (<c>project-settings.html</c>
/// §3, §4): per default row a button named "Remove"; a <c>[role='dialog']</c> headed "Removing "{name}" moves {n} Tasks." with
/// the line "Choose where Tasks in "{name}" go. This applies everywhere it can."; the Space-wide picker a native
/// <c>select</c> outside the table, each blocked Project's picker a <c>select</c> in its <c>tbody tr</c>, both starting on
/// "Choose…" with an empty value; the line "{k} Project(s) don't/doesn't have "{destination}" after this change."; the
/// commit "Remove and move {n} Tasks" with "{m} destination(s) still needed" beside it; in flight a
/// <c>[role='status']</c> region "Moving {n} Tasks across {p} Projects."; done, the polite "Removed "{name}". {n} Tasks
/// moved."; refused, "Nothing was moved." and "{Project} no longer has "{destination}". Choose a destination for it.".
/// Quotes and apostrophes are matched loosely. A different locator in the built page is corrected here and recorded in the
/// Debug Log, never by weakening the assertion.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-27")]
[Trait("Requirement", "UX-DR16")]
public sealed class SpaceDefaultStatusRemovalTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string Section = "main .space-default-statuses";
    private const string Row = ".space-status-row";
    private const string RowName = ".space-status-row__name";
    private const string Dialog = "[role='dialog']";
    private const string SpaceWidePicker = "select:not(table select)";
    private const string Polite = "[role='status'][aria-live='polite']";
    private const string Placeholder = "Choose…";
    private const string ForOwnersAndAdminsCopy = "Space settings is for Owners and Admins.";

    private static readonly string[] StatusNames = ["Queued", "Doing", "Done"];

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();
    private static readonly Guid Beacon = Guid.NewGuid();
    private static readonly Guid Comet = Guid.NewGuid();

    /// <summary>
    /// An Owner's Default Statuses rows each carry a "Remove" described by the row's Status; a Member's or a Viewer's Space
    /// settings is refused as before and holds no "Remove" for a default and no removal dialog.
    /// </summary>
    /// <param name="role">The Role held in the second Space.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("member")]
    [InlineData("viewer")]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "FR-16")]
    public async Task An_Owner_has_a_Remove_per_default_and_a_Member_or_a_Viewer_has_none(string role)
    {
        var api = Seeded(harbourRole: role);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            foreach (var name in StatusNames)
            {
                var remove = RemoveOf(page, name);

                Assert.Equal(1, await remove.CountAsync());
                Assert.Contains(name, await remove.EvaluateAsync<string>("b => (b.getAttribute('aria-describedby') || '').split(' ').map(id => document.getElementById(id)?.textContent ?? '').join(' ')"), StringComparison.Ordinal);
            }

            await page.EvaluateAsync("path => Blazor.navigateTo(path)", $"/spaces/{Harbour:D}/settings");
            await page.Locator(".role-narration").GetByText(ForOwnersAndAdminsCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Section).CountAsync());
            Assert.Equal(0, await page.Locator($"main {Row}").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Remove", Exact = true }).CountAsync());
            Assert.Equal(0, await page.Locator(Dialog).CountAsync());
            Assert.Empty(api.SpaceStatusRemovals);
        }
    }

    /// <summary>
    /// "Remove" on "Done", which no Task holds, sends one removal with no destination, opens no dialog, re-renders without the
    /// row, announces it politely and moves focus to the section's heading.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR42")]
    public async Task An_unoccupied_default_is_removed_directly_announced_and_focus_goes_to_the_heading()
    {
        var api = Seeded();

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await RemoveOf(page, "Done").ClickAsync();
            await Announcement(page, "Removed .Done.\\.").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            Assert.Equal(["Queued", "Doing"], await RowNamesAsync(page));
            Assert.Equal(0, await page.Locator(Dialog).CountAsync());
            Assert.Single(api.SpaceStatusRemovals);
            Assert.Contains("\"destinationStatusId\":null", api.SpaceStatusRemovals[0].Body ?? string.Empty, StringComparison.Ordinal);
            Assert.True(await HeadingHasFocusAsync(page), "Focus did not move to the Default Statuses heading.");
        }
    }

    /// <summary>
    /// "Remove" on "Queued", which five Tasks hold, sends the removal with no destination; its <c>409</c> report opens the
    /// dialog stating the scope; the Space-wide picker starts on "Choose…", offers only the defaults left, and no blocked
    /// Project is listed yet.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR42")]
    public async Task An_occupied_default_opens_the_dialog_with_the_scope_and_nothing_chosen()
    {
        var api = Seeded();

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            var dialog = await OpenDialogAsync(page, "Queued");

            Assert.Matches(Pattern("Removing .Queued. moves 5 Tasks\\."), await dialog.InnerTextAsync());
            Assert.Matches(Pattern("Choose where Tasks in .Queued. go\\. This applies everywhere it can\\."), await dialog.InnerTextAsync());

            var picker = dialog.Locator(SpaceWidePicker);

            Assert.Equal([Placeholder, "Doing", "Done"], await OptionsAsync(picker));
            Assert.Equal(string.Empty, await picker.InputValueAsync());
            Assert.Equal(0, await dialog.Locator("tbody tr").CountAsync());
            Assert.Single(api.SpaceStatusRemovals);
        }
    }

    /// <summary>
    /// Choosing "Doing" lists the one Project that removed it - Beacon, with its two Tasks - and its picker starts on
    /// "Choose…" over Beacon's own set after the removal, an added Status included; Atlas, which holds "Doing", and Comet,
    /// which removed "Queued", are not listed.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Choosing_a_destination_lists_each_blocked_Project_with_its_count_and_an_unset_picker()
    {
        var api = Seeded();

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            var dialog = await OpenDialogAsync(page, "Queued");

            await dialog.Locator(SpaceWidePicker).SelectOptionAsync(new SelectOptionValue { Label = "Doing" });
            await dialog.GetByText(Pattern("1 Project doesn.t have .Doing. after this change")).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var rows = dialog.Locator("tbody tr");

            Assert.Equal(["Beacon 2"], await RowTextsAsync(rows));

            var beacon = rows.First.Locator("select");

            Assert.Equal([Placeholder, "Done", "Parked"], await OptionsAsync(beacon));
            Assert.Equal(string.Empty, await beacon.InputValueAsync());
            Assert.Single(api.SpaceStatusRemovals);
        }
    }

    /// <summary>
    /// The commit stays enabled. Pressed with nothing chosen it sends nothing, marks the Space-wide picker invalid and focuses
    /// it; pressed again with only the Space-wide destination chosen it sends nothing, marks Beacon's picker invalid, focuses
    /// it, and "1 destination still needed" is shown beside the commit.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR42")]
    public async Task A_commit_with_a_picker_unset_sends_nothing_marks_it_and_focuses_it()
    {
        var api = Seeded();

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            var dialog = await OpenDialogAsync(page, "Queued");
            var commit = Commit(dialog);
            var spaceWide = dialog.Locator(SpaceWidePicker);

            Assert.True(await commit.IsEnabledAsync());

            await commit.ClickAsync();
            await page.WaitForFunctionAsync("selector => document.querySelector(selector)?.getAttribute('aria-invalid') === 'true'", $"{Dialog} {SpaceWidePicker}", new PageWaitForFunctionOptions { Timeout = Timeout });

            Assert.True(await spaceWide.EvaluateAsync<bool>("picker => document.activeElement === picker"), "Focus is not on the unset Space-wide picker.");

            await spaceWide.SelectOptionAsync(new SelectOptionValue { Label = "Doing" });
            await commit.ClickAsync();

            var beacon = dialog.Locator("tbody tr").First.Locator("select");

            await page.WaitForFunctionAsync("selector => document.querySelector(selector)?.getAttribute('aria-invalid') === 'true'", $"{Dialog} tbody tr select", new PageWaitForFunctionOptions { Timeout = Timeout });

            Assert.True(await beacon.EvaluateAsync<bool>("picker => document.activeElement === picker"), "Focus is not on Beacon's unset picker.");
            Assert.Matches(Pattern("1 destination still needed"), await dialog.InnerTextAsync());
            Assert.Single(api.SpaceStatusRemovals);
        }
    }

    /// <summary>
    /// With "Doing" and Beacon's "Parked" chosen, the commit is replaced by a progress region naming the scope, which takes
    /// focus, shows no percentage and no bar, and ignores Escape while the removal is out; once answered, the dialog is gone,
    /// the announcement says how many Tasks moved, focus is on the section's heading, and the one request carried the
    /// destination and Beacon's exception.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR42")]
    [Trait("Requirement", "NFR-9")]
    public async Task In_flight_the_progress_region_holds_focus_and_done_announces_the_Tasks_moved()
    {
        var api = Seeded();

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            var dialog = await OpenDialogAsync(page, "Queued");

            await ChooseAsync(dialog, "Doing", ("Beacon", "Parked"));

            var arrived = api.HoldSpaceStatusRemovalAsync();

            await Commit(dialog).ClickAsync();
            await arrived;

            var progress = page.Locator("[role='status']", new PageLocatorOptions { HasTextRegex = Pattern("Moving 5 Tasks across 2 Projects\\.") });

            await progress.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await Commit(dialog).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.True(await progress.EvaluateAsync<bool>("region => document.activeElement === region"), "Focus is not on the progress region.");
            Assert.DoesNotContain("%", await progress.InnerTextAsync(), StringComparison.Ordinal);
            Assert.Equal(0, await page.Locator("[role='progressbar'], progress").CountAsync());

            await page.Keyboard.PressAsync("Escape");

            Assert.Equal(1, await page.Locator(Dialog).CountAsync());

            api.ReleaseSpaceStatusRemoval();
            await Announcement(page, "Removed .Queued.\\. 5 Tasks moved\\.").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Dialog).CountAsync());
            Assert.Equal(["Doing", "Done"], await RowNamesAsync(page));
            Assert.True(await HeadingHasFocusAsync(page), "Focus did not move to the Default Statuses heading.");
            Assert.Equal(2, api.SpaceStatusRemovals.Count);

            var sent = api.SpaceStatusRemovals[1].Body ?? string.Empty;

            Assert.Contains(Beacon.ToString("D"), sent, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(2, api.ProjectStatuses[(Northwind, Beacon)].Single(entry => entry.Name == "Parked").TaskCount);
            Assert.Equal(3, api.ProjectStatuses[(Northwind, Atlas)].Single(entry => entry.Name == "Doing").TaskCount);
        }
    }

    /// <summary>
    /// Between the report and the commit, Atlas removed "Doing" too. The commit is refused <c>409</c> naming Atlas: the dialog
    /// says nothing was moved, names Atlas as no longer having "Doing", lists it with an unset picker, and every default is
    /// still in the section.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_refusal_at_commit_says_nothing_was_moved_and_re_renders_the_table_naming_the_Project()
    {
        var api = Seeded();

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            var dialog = await OpenDialogAsync(page, "Queued");

            await ChooseAsync(dialog, "Doing", ("Beacon", "Parked"));
            api.ProjectStatuses[(Northwind, Atlas)].RemoveAll(entry => entry.Name == "Doing");

            await Commit(dialog).ClickAsync();
            await dialog.GetByText("Nothing was moved.").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Matches(Pattern("Atlas no longer has .Doing.\\. Choose a destination for it\\."), await dialog.InnerTextAsync());

            var atlas = dialog.Locator("tbody tr", new LocatorLocatorOptions { HasText = "Atlas" });

            Assert.Equal(1, await atlas.CountAsync());
            Assert.Equal(string.Empty, await atlas.Locator("select").InputValueAsync());
            Assert.Equal(StatusNames, await RowNamesAsync(page));
        }
    }

    /// <summary>
    /// When a Project holds only the default being removed, "Remove" is refused inline, naming that Project; no dialog opens
    /// and the row stays.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_set_left_empty_is_refused_inline_naming_the_Project()
    {
        var api = Seeded();

        api.ProjectStatuses[(Northwind, Comet)] = [new FakeProjectStatus(DefaultIdOf(api, "Queued"), "Queued", ProjectStatusSources.SpaceDefault, DefaultName: null, TaskCount: 0)];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await RemoveOf(page, "Queued").ClickAsync();
            await page.Locator(Section).GetByText("Comet").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Dialog).CountAsync());
            Assert.Equal(StatusNames, await RowNamesAsync(page));
            Assert.Single(api.SpaceStatusRemovals);
        }
    }

    /// <summary>
    /// Focus is trapped in the dialog both ways - Tab past its last control lands on its first, Shift+Tab before its first on
    /// its last - and Cancel or Escape before commit closes it having sent nothing more, returning focus to the row's Remove.
    /// </summary>
    /// <param name="key">"Escape", or "Cancel" to press the Cancel button.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("Cancel")]
    [InlineData("Escape")]
    [Trait("Requirement", "UX-DR42")]
    public async Task The_trap_wraps_both_ways_and_Cancel_or_Escape_sends_nothing_and_returns_focus(string key)
    {
        var api = Seeded();

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            var dialog = await OpenDialogAsync(page, "Queued");

            for (var press = 0; press < 12; press++)
            {
                await page.Keyboard.PressAsync("Tab");
                Assert.True(await FocusIsInAsync(page), $"Tab {press + 1} left the dialog.");
            }

            for (var press = 0; press < 12; press++)
            {
                await page.Keyboard.PressAsync("Shift+Tab");
                Assert.True(await FocusIsInAsync(page), $"Shift+Tab {press + 1} left the dialog.");
            }

            if (key == "Escape")
            {
                await page.Keyboard.PressAsync("Escape");
            }
            else
            {
                await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Cancel", Exact = true }).ClickAsync();
            }

            await dialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.True(await RemoveOf(page, "Queued").EvaluateAsync<bool>("button => document.activeElement === button"), "Focus did not return to the row's Remove.");
            Assert.Single(api.SpaceStatusRemovals);
            Assert.Equal(StatusNames, await RowNamesAsync(page));
        }
    }

    /// <summary>
    /// At 320 by 640 CSS pixels the section and the open dialog, with its blocked table, reflow: nothing scrolls sideways and
    /// every control stays on screen.
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

            var dialog = await OpenDialogAsync(page, "Queued");

            await dialog.Locator(SpaceWidePicker).SelectOptionAsync(new SelectOptionValue { Label = "Doing" });
            await dialog.Locator("tbody tr").First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await AssertFitsAsync(page, Dialog, "The removal dialog");
        }
    }

    /// <summary>
    /// The rename's "keep their names" answer no longer states a count the client cannot know: it announces "Renamed "Queued" to
    /// "Waiting". Projects that renamed it kept their own names."
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR42")]
    public async Task The_rename_keep_answer_is_announced_without_a_count()
    {
        var api = Seeded();
        var atlas = api.ProjectStatuses[(Northwind, Atlas)];

        atlas[0] = atlas[0] with { Name = "Intake", DefaultName = "Queued" };

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            var row = RowOf(page, "Queued");

            await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Rename" }).ClickAsync();
            await row.GetByRole(AriaRole.Textbox).FillAsync("Waiting");
            await row.GetByRole(AriaRole.Textbox).PressAsync("Enter");
            await page.Locator(Dialog).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Keep their names" }).ClickAsync();
            await Announcement(page, "Renamed .Queued. to .Waiting.\\. Projects that renamed it kept their own names\\.").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            Assert.DoesNotMatch(Pattern("\\d+ Projects? kept"), string.Join(' ', await page.Locator(Polite).AllInnerTextsAsync()));
        }
    }

    /// <summary>
    /// Two Spaces: Northwind, owned, with Atlas, Beacon and Comet over its three defaults, and Harbour, held in
    /// <paramref name="harbourRole"/>. Beacon removed "Doing" and added "Parked"; Comet removed "Queued"; Atlas holds three
    /// Tasks in "Queued" and Beacon two.
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

        atlas[0] = atlas[0] with { TaskCount = 3 };
        beacon[0] = beacon[0] with { TaskCount = 2 };
        beacon.RemoveAll(entry => entry.Name == "Doing");
        beacon.Add(new FakeProjectStatus(Guid.NewGuid(), "Parked", ProjectStatusSources.AddedHere, DefaultName: null, TaskCount: 0));
        api.ProjectStatuses[(Northwind, Comet)].RemoveAll(entry => entry.Name == "Queued");

        return api;
    }

    private static Guid DefaultIdOf(FakeYelloApi api, string name) => api.SpaceDefaultStatuses[Northwind].Single(status => status.Name == name).Id;

    private static Regex Pattern(string regex) => new(regex, RegexOptions.None, TimeSpan.FromSeconds(1));

    private static ILocator RowOf(IPage page, string name) =>
        page.Locator(Section).Locator(Row, new LocatorLocatorOptions { Has = page.Locator(RowName, new PageLocatorOptions { HasTextRegex = new Regex($"^\\s*{Regex.Escape(name)}\\s*$", RegexOptions.None, TimeSpan.FromSeconds(1)) }) });

    private static ILocator RemoveOf(IPage page, string name) =>
        RowOf(page, name).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Remove", Exact = true });

    private static ILocator Commit(ILocator dialog) =>
        dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { NameRegex = Pattern("^Remove and move") });

    private static ILocator Announcement(IPage page, string regex) =>
        page.Locator(Polite, new PageLocatorOptions { HasTextRegex = Pattern(regex) });

    private static async Task<ILocator> OpenDialogAsync(IPage page, string name)
    {
        await RemoveOf(page, name).ClickAsync();

        var dialog = page.Locator(Dialog);

        await dialog.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

        return dialog;
    }

    private static async Task ChooseAsync(ILocator dialog, string destination, params (string Project, string Status)[] exceptions)
    {
        await dialog.Locator(SpaceWidePicker).SelectOptionAsync(new SelectOptionValue { Label = destination });

        foreach (var (project, status) in exceptions)
        {
            var row = dialog.Locator("tbody tr", new LocatorLocatorOptions { HasText = project });

            await row.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await row.Locator("select").SelectOptionAsync(new SelectOptionValue { Label = status });
        }
    }

    private static async Task<string[]> OptionsAsync(ILocator picker) =>
        [.. (await picker.Locator("option").AllTextContentsAsync()).Select(option => option.Trim())];

    // Chromium's innerText of a select is every option's text, so the picker's cell is left out; its options are asserted on
    // their own.
    private static async Task<string[]> RowTextsAsync(ILocator rows) =>
        [
            .. (await rows.EvaluateAllAsync<string[]>("rows => rows.map(row => [...row.cells].filter(cell => !cell.querySelector('select')).map(cell => cell.innerText).join(' '))"))
                .Select(row => Regex.Replace(row, "\\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim())
                .Where(row => row.Length > 0),
        ];

    private static async Task<string[]> RowNamesAsync(IPage page) =>
        [.. (await page.Locator(Section).Locator(RowName).AllInnerTextsAsync()).Select(name => name.Trim())];

    private static Task<bool> HeadingHasFocusAsync(IPage page) =>
        page.EvaluateAsync<bool>($"() => {{ const a = document.activeElement; return !!a && !!a.closest('{Section}') && /^H[1-6]$/.test(a.tagName) && a.textContent.trim() === 'Default Statuses'; }}");

    private static Task<bool> FocusIsInAsync(IPage page) =>
        page.EvaluateAsync<bool>($"() => {{ const d = document.querySelector(\"{Dialog}\"); return !!d && d.contains(document.activeElement); }}");

    private static async Task AssertFitsAsync(IPage page, string selector, string what)
    {
        Assert.True(
            await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1"),
            $"{what} makes the page scroll sideways at 320px.");

        foreach (var right in await page.Locator(selector).Locator("button, input, select").EvaluateAllAsync<double[]>("controls => controls.map(c => c.getBoundingClientRect().right)"))
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
