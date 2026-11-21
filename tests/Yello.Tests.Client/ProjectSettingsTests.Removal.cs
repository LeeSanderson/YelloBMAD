using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;
using Yello.Contracts;

namespace Yello.Tests.Client;

/// <summary>
/// Removing a Status in the Project settings editor, in a real browser: an unoccupied Status goes directly and is
/// announced; an occupied one opens a dialog stating the scope, whose destination picker starts on a placeholder that is not
/// a Status and lists only the Statuses left after the removal; in flight a progress region names the scope and holds focus;
/// a refusal says nothing was moved; and removed Space defaults are listed with a control to add each back.
/// </summary>
/// <remarks>
/// <para>
/// <b>The API is <see cref="FakeYelloApi"/></b> with the removal paths added (<c>FakeYelloApi.StatusRemoval.cs</c>).
/// </para>
/// <para>
/// <b>The locators and copy</b> follow the mockup (<c>project-settings.html</c> §1, §4): per row a button named "Remove"; a <c>[role='dialog']</c> holding "Removing "{name}" moves {n} Tasks.", a native
/// <c>select</c> (role <c>combobox</c>) whose first option is "Choose…" with an empty value, a commit button starting "Remove
/// and move", a "Cancel" button, and the field message "Choose where these Tasks go."; in flight a <c>[role='status']</c>
/// region "Moving {n} Tasks to "{destination}"."; done, the polite announcement "Removed "{name}"." (plus "{n} Tasks moved to
/// "{destination}"." when Tasks moved); refused, "Nothing was moved."; a write's <c>404</c> for a Status that left, "That
/// Status is no longer in this Project."; and per removed default a button named "Add back". Quotes are matched loosely, so
/// straight or typographic ones both pass.
/// </para>
/// </remarks>
public sealed partial class ProjectSettingsTests
{
    private const string Dialog = "[role='dialog']";
    private const string Placeholder = "Choose…";
    private const string ChooseCopy = "Choose where these Tasks go.";
    private const string NothingMovedCopy = "Nothing was moved.";
    private const string NoLongerHereCopy = "That Status is no longer in this Project.";

    /// <summary>
    /// "Remove" on the unoccupied added Status sends one removal with no destination, opens no dialog, re-renders without the
    /// row and announces it politely; the Board then has three columns.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-26")]
    [Trait("Requirement", "UX-DR42")]
    public async Task Removing_an_unoccupied_Status_removes_it_directly_and_announces_it()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas");
        Shape(api, Northwind, Atlas);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind, Atlas);
            await RowOf(page, "Parked").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Remove", Exact = true }).ClickAsync();
            await Announcement(page, "Removed.*Parked").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            Assert.Equal(["Intake", "Doing", "Done"], await RowNamesAsync(page));
            Assert.Equal(0, await page.Locator(Dialog).CountAsync());
            Assert.Single(api.StatusRemovals);
            Assert.Contains("\"destinationStatusId\":null", api.StatusRemovals[0].Body ?? string.Empty, StringComparison.Ordinal);

            await page.EvaluateAsync("path => Blazor.navigateTo(path)", ClientBoard(Northwind, Atlas));
            await page.Locator(Column).Locator("h2, h3").Nth(2).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(["Intake", "Doing", "Done"], await ColumnNamesAsync(page));
        }
    }

    /// <summary>
    /// "Remove" on an occupied Status opens a dialog stating the scope; its destination picker starts on the placeholder,
    /// which is not a Status, and offers only the Statuses left after the removal; committing with no choice says why in the
    /// field and sends nothing; Cancel closes the dialog and returns focus to the row's Remove control.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-26")]
    [Trait("Requirement", "UX-DR16")]
    public async Task An_occupied_Status_opens_a_dialog_with_no_destination_chosen_and_only_the_Statuses_left()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas");
        Shape(api, Northwind, Atlas);
        Occupy(api, Northwind, Atlas, "Doing", 3);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind, Atlas);

            var remove = RowOf(page, "Doing").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Remove", Exact = true });

            await remove.ClickAsync();

            var dialog = page.Locator(Dialog);

            await dialog.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Matches(Pattern("Removing .Doing. moves 3 Tasks\\."), await dialog.InnerTextAsync());

            var picker = dialog.GetByRole(AriaRole.Combobox);
            var options = (await picker.Locator("option").AllTextContentsAsync()).Select(option => option.Trim()).ToList();

            Assert.Equal([Placeholder, "Intake", "Done", "Parked"], options);
            Assert.Equal(string.Empty, await picker.InputValueAsync());

            await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { NameRegex = Pattern("^Remove and move") }).ClickAsync();
            await dialog.GetByText(ChooseCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Empty(api.StatusRemovals);

            await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Cancel", Exact = true }).ClickAsync();
            await dialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.True(await remove.EvaluateAsync<bool>("button => document.activeElement === button"), "Cancel did not return focus to the row's Remove control.");
            Assert.Contains("Doing", await RowNamesAsync(page));
        }
    }

    /// <summary>
    /// Committing a destination replaces the commit with a progress region that names the scope, takes focus and shows no
    /// percentage and no progress bar while the removal is out; once answered, the row is gone, the announcement says how many
    /// Tasks moved where, and focus is on the destination's row.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-26")]
    [Trait("Requirement", "UX-DR42")]
    [Trait("Requirement", "NFR-9")]
    public async Task In_flight_the_progress_region_holds_focus_and_names_the_scope_and_done_moves_focus_to_the_destination()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas");
        Shape(api, Northwind, Atlas);
        Occupy(api, Northwind, Atlas, "Doing", 3);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind, Atlas);
            await RowOf(page, "Doing").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Remove", Exact = true }).ClickAsync();

            var dialog = page.Locator(Dialog);

            await dialog.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await dialog.GetByRole(AriaRole.Combobox).SelectOptionAsync(new SelectOptionValue { Label = "Done" });

            var arrived = api.HoldStatusRemovalAsync();

            await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { NameRegex = Pattern("^Remove and move") }).ClickAsync();
            await arrived;

            var progress = page.Locator("[role='status']", new PageLocatorOptions { HasTextRegex = Pattern("Moving 3 Tasks to .Done.") });

            await progress.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.True(await progress.EvaluateAsync<bool>("region => document.activeElement === region"), "Focus is not on the progress region.");
            await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { NameRegex = Pattern("^Remove and move") }).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });
            Assert.True(await progress.EvaluateAsync<bool>("region => document.activeElement === region"), "Focus left the progress region when the commit went.");
            Assert.DoesNotContain("%", await progress.InnerTextAsync(), StringComparison.Ordinal);
            Assert.Equal(0, await page.Locator("[role='progressbar'], progress").CountAsync());

            api.ReleaseStatusRemoval();
            await Announcement(page, "Removed .Doing.\\. 3 Tasks moved to .Done.").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            Assert.Equal(["Intake", "Done", "Parked"], await RowNamesAsync(page));
            Assert.True(await RowOf(page, "Done").EvaluateAsync<bool>("row => row.contains(document.activeElement)"), "Focus did not move to the destination's row.");
            Assert.Single(api.StatusRemovals);
        }
    }

    /// <summary>
    /// A count gone stale: the page shows the added Status with no Tasks, but the server now counts two in it. "Remove" sends
    /// the direct removal, the server answers <c>409 status-destination-required</c>, and the page re-reads the set and opens
    /// the dialog with the server's count.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-26")]
    public async Task A_stale_count_answered_409_re_reads_the_set_and_opens_the_dialog()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas");
        Shape(api, Northwind, Atlas);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind, Atlas);

            var set = api.ProjectStatuses[(Northwind, Atlas)];

            set[^1] = set[^1] with { TaskCount = 2 };

            await RowOf(page, "Parked").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Remove", Exact = true }).ClickAsync();

            var dialog = page.Locator(Dialog);

            await dialog.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Matches(Pattern("Removing .Parked. moves 2 Tasks\\."), await dialog.InnerTextAsync());
            Assert.Single(api.StatusRemovals);
            Assert.Contains("Parked", await RowNamesAsync(page));
        }
    }

    /// <summary>
    /// A refused removal says nothing was moved, in the editor's inline error region, and the Status stays in the list.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-26")]
    public async Task A_refused_removal_says_nothing_was_moved_and_keeps_the_Status()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas");
        Shape(api, Northwind, Atlas);
        Occupy(api, Northwind, Atlas, "Doing", 3);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind, Atlas);
            await RowOf(page, "Doing").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Remove", Exact = true }).ClickAsync();

            var dialog = page.Locator(Dialog);

            await dialog.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await dialog.GetByRole(AriaRole.Combobox).SelectOptionAsync(new SelectOptionValue { Label = "Done" });
            api.NextProjectStatusRefusal = (400, ProblemTypes.InvalidStatusDestination);
            await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { NameRegex = Pattern("^Remove and move") }).ClickAsync();
            await page.GetByText(NothingMovedCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Contains("Doing", await RowNamesAsync(page));
        }
    }

    /// <summary>
    /// The Space defaults this Project removed are listed under the add form, each with "Add back"; pressing it sends one
    /// add-back and the default returns at its Space slot under its Space name.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-25")]
    public async Task A_removed_default_is_listed_and_added_back()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas");
        Shape(api, Northwind, Atlas);

        var set = api.ProjectStatuses[(Northwind, Atlas)];

        set.RemoveAt(1);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(Address(ClientSettings(Northwind, Atlas)));

            var addBack = page.Locator(Editor).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Add back" });

            await addBack.First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(1, await addBack.CountAsync());
            Assert.Contains("Doing", await page.Locator(Editor).InnerTextAsync(), StringComparison.Ordinal);

            await addBack.First.ClickAsync();
            await page.Locator(Editor).Locator(Row).Nth(3).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(["Intake", "Doing", "Done", "Parked"], await RowNamesAsync(page));
            Assert.Single(api.StatusRestores);
            Assert.Equal(0, await addBack.CountAsync());

            await page.WaitForFunctionAsync(
                "() => [...document.querySelectorAll('.status-delta-row')].some(row => row.querySelector('.status-delta-row__name')?.textContent.trim() === 'Doing' && row.contains(document.activeElement))",
                null,
                new PageWaitForFunctionOptions { Timeout = Timeout });
        }
    }

    /// <summary>
    /// The only Status, though Tasks hold it, opens no dialog (there is nowhere to send them): "Remove" sends the removal,
    /// the server refuses it as leaving the set empty, and the page says nothing was moved and keeps the Status.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-25")]
    public async Task Removing_the_only_Status_opens_no_dialog_and_is_refused_inline()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas");
        Shape(api, Northwind, Atlas);
        Occupy(api, Northwind, Atlas, "Doing", 3);
        api.ProjectStatuses[(Northwind, Atlas)].RemoveAll(status => status.Name != "Doing");

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(Address(ClientSettings(Northwind, Atlas)));
            await RowOf(page, "Doing").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await RowOf(page, "Doing").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Remove", Exact = true }).ClickAsync();
            await page.GetByText(NothingMovedCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Dialog).CountAsync());
            Assert.Single(api.StatusRemovals);
            Assert.Equal(["Doing"], await RowNamesAsync(page));
        }
    }

    /// <summary>
    /// A write answered <c>404</c> because the Status has left the set (another Admin removed it) re-reads the set: the page
    /// stays the editor, says so inline, and renders the set without that Status - never the page-level refusal.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-26")]
    public async Task A_404_for_a_Status_that_left_the_set_re_reads_and_says_so_inline()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas");
        Shape(api, Northwind, Atlas);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind, Atlas);

            var set = api.ProjectStatuses[(Northwind, Atlas)];

            set.RemoveAt(set.Count - 1);

            await RowOf(page, "Parked").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Remove", Exact = true }).ClickAsync();
            await page.GetByText(NoLongerHereCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(["Intake", "Doing", "Done"], await RowNamesAsync(page));
            Assert.Equal(1, await page.Locator(Editor).CountAsync());
        }
    }

    /// <summary>
    /// At 320 by 640 CSS pixels the open dialog reflows: nothing scrolls sideways and every control in it stays on screen.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-9")]
    public async Task The_removal_dialog_reflows_at_320_pixels_wide()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas");
        Shape(api, Northwind, Atlas);
        Occupy(api, Northwind, Atlas, "Doing", 3);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(320, 640);
            await OpenSettingsAsync(api, page, Northwind, Atlas);
            await RowOf(page, "Doing").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Remove", Exact = true }).ClickAsync();

            var dialog = page.Locator(Dialog);

            await dialog.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.True(
                await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1"),
                "The page scrolls sideways at 320px with the removal dialog open.");

            foreach (var right in await dialog.Locator("button, select").EvaluateAllAsync<double[]>("controls => controls.map(c => c.getBoundingClientRect().right)"))
            {
                Assert.True(right <= 320, $"A dialog control ends at {right}px, past the 320px viewport.");
            }
        }
    }

    /// <summary>
    /// Gives one Status of the Project <paramref name="count"/> Tasks, in the set's count and on its Board column.
    /// </summary>
    private static void Occupy(FakeYelloApi api, Guid spaceId, Guid projectId, string name, int count)
    {
        var set = api.ProjectStatuses[(spaceId, projectId)];
        var index = set.FindIndex(entry => entry.Name == name);

        set[index] = set[index] with { TaskCount = count };

        var column = api.Boards[(spaceId, projectId)].Columns.First(candidate => candidate.StatusId == set[index].Id);

        for (var i = 0; i < count; i++)
        {
            column.Tasks.Add(new FakeTask(Guid.NewGuid(), string.Create(CultureInfo.InvariantCulture, $"{name} work {i + 1}"), column.StatusId, string.Create(CultureInfo.InvariantCulture, $"a{i:D5}"), DateTimeOffset.UtcNow));
        }

        column.Total = count;
    }

    private static ILocator RowOf(IPage page, string name) =>
        page.Locator(Editor).Locator(Row).Filter(new LocatorFilterOptions { Has = page.Locator(RowName, new PageLocatorOptions { HasTextRegex = Pattern($"^\\s*{Regex.Escape(name)}\\s*$") }) });

    private static Regex Pattern(string text) => new(text, RegexOptions.None, TimeSpan.FromSeconds(1));

    private static ILocator Announcement(IPage page, string pattern) =>
        page.Locator("[role='status'][aria-live='polite']", new PageLocatorOptions { HasTextRegex = Pattern(pattern) });
}
