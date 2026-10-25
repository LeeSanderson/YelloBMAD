using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;
using Yello.Contracts;

namespace Yello.Tests.Client;

/// <summary>
/// Story 2.3 in a real browser: the Task detail panel - opened from a card, one level deep over the Board,
/// focus trapped and returned, no description and no save - and the product's first optimistic write: a title,
/// Status or due date change renders at once, the server admits or refuses it afterwards, and a refusal
/// reverts that attribute to its last admitted value and says so truthfully. AC1-AC3, AC5-AC7, AC12, AC14;
/// UX-DR10, UX-DR11, UX-DR26, UX-DR27, UX-DR31-33, UX-DR39, UX-DR40, UX-DR42. Written red by the story 2.3 ATDD
/// run.
/// </summary>
/// <remarks>
/// <para>
/// <b>The API is <see cref="FakeYelloApi"/></b> with story 2.3's <c>GET</c> and <c>PATCH</c> on the Task's own
/// path; <see cref="FakeYelloApi.HoldTaskEditAsync"/> holds the next edit so a case can assert what is on
/// screen while it is outstanding (AC1's required ordering - the one 2.1's rename case rejected), then release
/// it admitted, refused or unanswered.
/// </para>
/// <para>
/// <b>The locators are assumptions</b>, to be corrected to the markup Task 9 builds and the correction recorded:
/// a card opens from a <c>button[data-task-id]</c> inside <c>.task-card</c> (Q7 (a)); the panel is the one
/// <c>[role=dialog]</c>; its fields are labelled "Title", "Status" and "Due date"; the Status picker is a button
/// named "Status" opening a <c>[role=listbox]</c> of <c>[role=option]</c> rows (Q11); the clear control is "Clear
/// due date"; the close control is "Close"; the Status announcement reads "Moved to {Status}." (Q10); a
/// no-answer reads "Not sent." (Q8). The copy that already exists ("Viewers cannot edit Tasks.", the title
/// rule, the idempotency sentence, "Not available.") is asserted as written.
/// </para>
/// <para>
/// <b>Story 2.2's <c>BoardTests.A_Task_card_is_its_title_alone_and_carries_no_shadow</c> asserts a card holds no
/// button</b>; Q7 (a) puts one there. The dev phase widens that case to "no control but the opening button" in
/// the same change that adds it, and records it.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-20")]
[Trait("Requirement", "AR-20")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class TaskDetailTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string SwitcherTrigger = "nav.context-bar button[aria-controls]";

    private const string Column = "main .board-column";
    private const string ColumnCount = ".column-count";
    private const string Card = ".task-card";
    private const string Dialog = "[role='dialog']";

    private const string TitleLabel = "Title";
    private const string StatusLabel = "Status";
    private const string DueDateLabel = "Due date";
    private const string ClearDueDate = "Clear due date";
    private const string Close = "Close";

    private const string TitleInvalidCopy = "A Task title is 1 to 200 characters.";
    private const string ManageTasksCopy = "Viewers cannot edit Tasks.";
    private const string ReusedCopy = "That request was already used for something else. Try again.";
    private const string InFlightCopy = "That was already sent. Reload to see it.";
    private const string NotSentCopy = "Not sent.";
    private const string NotAvailableCopy = "Not available.";

    private const string InvalidTaskTitleType = "urn:yello:problem:invalid-task-title";
    private const string InsufficientRoleType = "urn:yello:problem:insufficient-role";
    private const string IdempotencyKeyReusedType = "urn:yello:problem:idempotency-key-reused";
    private const string IdempotencyKeyInFlightType = "urn:yello:problem:idempotency-key-in-flight";

    private static readonly string[] StatusNames = ["Todo", "Doing", "Done"];

    private static readonly string[] ReusableFieldNames = [string.Empty, "NAME", "TITLE", "TASK", "TASKTITLE", "TASK-TITLE"];

    private static readonly string[] Banned = ["Saving", "Saved", "Save"];

    private static readonly Regex IsoInstantWithOffset = new(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();
    private static readonly Guid Beacon = Guid.NewGuid();

    /// <summary>
    /// AC5, AC14, UX-DR10, UX-DR12, Q6 (a) - opening a card opens exactly one panel over the Board: a modal
    /// dialog labelled by its own <c>h2</c>, the Task's title; focus moves inside it; the Board stays beneath;
    /// and neither the address nor <c>document.title</c> changes.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR10")]
    [Trait("Requirement", "UX-DR33")]
    public async Task Opening_a_card_opens_one_panel_over_the_Board_with_focus_inside_and_no_change_of_address()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);

            var pathBefore = await page.EvaluateAsync<string>("() => location.pathname");

            await OpenPanelAsync(page, tasks[0].Id);

            var dialog = page.Locator(Dialog);

            Assert.Equal(1, await dialog.CountAsync());
            Assert.Equal("true", await dialog.GetAttributeAsync("aria-modal"));
            Assert.Equal(
                "H2|Draft the brief",
                await dialog.EvaluateAsync<string>("d => { const h = document.getElementById(d.getAttribute('aria-labelledby')); return h ? h.tagName + '|' + h.textContent.trim() : ''; }"));
            Assert.True(await dialog.EvaluateAsync<bool>("d => d.contains(document.activeElement)"), "Focus did not move into the panel.");
            Assert.Equal(3, await page.Locator(Column).CountAsync());
            Assert.Equal(pathBefore, await page.EvaluateAsync<string>("() => location.pathname"));
            Assert.Equal("Yello", await page.TitleAsync());
        }
    }

    /// <summary>
    /// AC5, UX-DR39, UX-DR40 - while the panel is open, Tab and Shift+Tab never leave it; <c>Esc</c> closes it;
    /// and focus returns to the originating card's opening control, found by its <c>data-task-id</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR39")]
    [Trait("Requirement", "UX-DR40")]
    public async Task Focus_is_trapped_in_the_panel_and_returns_to_the_card_on_Esc()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "First", "Second");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[1].Id);

            var dialog = page.Locator(Dialog);

            foreach (var key in Enumerable.Repeat("Tab", 12).Concat(Enumerable.Repeat("Shift+Tab", 12)))
            {
                await page.Keyboard.PressAsync(key);
                Assert.True(await dialog.EvaluateAsync<bool>("d => d.contains(document.activeElement)"), $"{key} moved focus out of the panel.");
            }

            await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = Close, Exact = true }).FocusAsync();
            await page.Keyboard.PressAsync("Escape");
            await dialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.Equal(
                tasks[1].Id.ToString("D"),
                await page.EvaluateAsync<string?>("() => document.activeElement && document.activeElement.getAttribute('data-task-id')"));
        }
    }

    /// <summary>
    /// AC5, UX-DR26 - the panel is never stacked two deep: activating another card while one is open replaces
    /// the panel's content, and there is still one dialog.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR26")]
    public async Task Opening_another_card_replaces_the_panel_and_never_stacks_a_second()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "First", "Second");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            // The panel is modal, so the second card is activated programmatically - the only way to reach it.
            await page.EvaluateAsync("id => document.querySelector(`.task-card button[data-task-id='${id}']`).click()", tasks[1].Id.ToString("D"));
            await page.Locator(Dialog).GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = "Second", Exact = true }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(1, await page.Locator(Dialog).CountAsync());
        }
    }

    /// <summary>
    /// AC6 - Task detail in this epic has no description editor, no heading for one and no placeholder. Anchor:
    /// the title field is present, so the panel rendered its attributes.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-12")]
    public async Task The_panel_has_no_description_editor()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            var dialog = page.Locator(Dialog);

            Assert.Equal(1, await TitleField(page).CountAsync());
            Assert.Equal(0, await dialog.Locator("textarea, [contenteditable], [role='textbox'][aria-multiline='true']").CountAsync());
            Assert.DoesNotContain("description", await dialog.InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("description", await dialog.EvaluateAsync<string>("d => d.innerHTML"), StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// AC2, UX-DR26 - there is no save button and no auto-save indicator: while an edit is outstanding and after
    /// it is admitted, nothing is named or reads "Save", "Saving" or "Saved", and nothing spins. Anchor: the edit
    /// was sent.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR26")]
    public async Task There_is_no_save_button_and_no_save_indicator_before_or_after_an_edit()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            var held = api.HoldTaskEditAsync();

            await CommitTitleAsync(page, "Send the brief");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            await AssertNoSaveAffordanceAsync(page);

            api.ReleaseTaskEdit();
            await WaitUntilAsync(() => api.FindTask(tasks[0].Id)?.Task.Title == "Send the brief", "The edit was never applied.");

            await AssertNoSaveAffordanceAsync(page);
        }
    }

    /// <summary>
    /// AC1, AD-11, AD-18, Q1 (a) - a title edit renders on the card and in the panel at once, while the request is
    /// still outstanding; the request carries only the changed member and an <c>Idempotency-Key</c>; once admitted,
    /// the new title stays.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-11")]
    [Trait("Requirement", "AD-18")]
    public async Task A_title_edit_renders_at_once_and_the_server_admits_it_afterwards()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            var held = api.HoldTaskEditAsync();

            await CommitTitleAsync(page, "Send the brief");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            // Outstanding: the replica already shows it.
            Assert.Equal("Send the brief", (await CardOf(page, tasks[0].Id).Locator(".task-card__title").TextContentAsync())?.Trim());
            Assert.Equal("Send the brief", await TitleField(page).InputValueAsync());

            var edit = Assert.Single(Edits(api));

            Assert.Equal(["title"], Members(edit));
            Assert.False(string.IsNullOrEmpty(edit.Header(Idempotency.HeaderName)), "The edit carried no Idempotency-Key.");

            api.ReleaseTaskEdit();
            await WaitUntilAsync(() => api.FindTask(tasks[0].Id)?.Task.Title == "Send the brief", "The edit was never applied.");

            Assert.Equal("Send the brief", (await CardOf(page, tasks[0].Id).Locator(".task-card__title").TextContentAsync())?.Trim());
        }
    }

    /// <summary>
    /// AC1, UX-DR11, Q3, Q10, Q11 - the Status picker offers exactly the Project's effective set, marks the
    /// current Status as current without pre-selecting any destination, and choosing one commits it: the card
    /// moves to the end of its new column and both column totals change at once, while the request is
    /// outstanding; the move is announced politely, once.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR11")]
    [Trait("Requirement", "AD-11")]
    public async Task Choosing_a_Status_moves_the_card_and_both_totals_at_once()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "Moving", "Staying");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            var dialog = page.Locator(Dialog);

            await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = StatusLabel }).ClickAsync();

            var options = dialog.Locator("[role='listbox'] [role='option']");

            await options.First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(StatusNames, (await options.AllTextContentsAsync()).Select(name => name.Trim()));
            Assert.Equal(0, await dialog.Locator("[role='option'][aria-selected='true']").CountAsync());
            Assert.Equal(1, await dialog.Locator("[role='option'][aria-current='true'], [role='option'][aria-checked='true']", new LocatorLocatorOptions { HasText = "Todo" }).CountAsync());
            Assert.Equal(1, await page.Locator("[role='dialog']").CountAsync());

            var held = api.HoldTaskEditAsync();

            await options.Filter(new LocatorFilterOptions { HasText = "Done" }).ClickAsync();
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            var columns = page.Locator(Column);

            Assert.Equal("Moving", (await columns.Nth(2).Locator(Card).Last.Locator(".task-card__title").TextContentAsync())?.Trim());
            Assert.Equal("1", (await columns.Nth(0).Locator(ColumnCount).TextContentAsync())?.Trim());
            Assert.Equal("1", (await columns.Nth(2).Locator(ColumnCount).TextContentAsync())?.Trim());
            Assert.Equal(["statusId"], Members(Assert.Single(Edits(api))));

            await page.Locator("[role='status'][aria-live='polite']", new PageLocatorOptions { HasText = "Moved to Done." })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            api.ReleaseTaskEdit();
        }
    }

    /// <summary>
    /// AC12, UX-DR32, AD-11 - a refused title edit is reverted on the card and in the panel to the last value the
    /// server admitted, and the panel says why in the copy the problem's type decides - inline, never a toast,
    /// never server prose.
    /// </summary>
    /// <param name="status">The refusal's status.</param>
    /// <param name="problemType">The refusal's type.</param>
    /// <param name="capability">A <c>403</c>'s capability.</param>
    /// <param name="copy">The copy the panel renders.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(400, InvalidTaskTitleType, null, TitleInvalidCopy)]
    [InlineData(403, InsufficientRoleType, "manage-tasks", ManageTasksCopy)]
    [InlineData(422, IdempotencyKeyReusedType, null, ReusedCopy)]
    [InlineData(409, IdempotencyKeyInFlightType, null, InFlightCopy)]
    [Trait("Requirement", "UX-DR32")]
    [Trait("Requirement", "AD-11")]
    public async Task A_refused_title_edit_is_reverted_and_its_reason_is_said_inline(int status, string problemType, string? capability, string copy)
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            var held = api.HoldTaskEditAsync();

            await CommitTitleAsync(page, "Send the brief");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            Assert.Equal("Send the brief", await TitleField(page).InputValueAsync());

            api.ReleaseTaskEditRefused(status, problemType, capability);

            await page.Locator(Dialog).GetByText(copy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal("Draft the brief", await TitleField(page).InputValueAsync());
            Assert.Equal("Draft the brief", (await CardOf(page, tasks[0].Id).Locator(".task-card__title").TextContentAsync())?.Trim());
            Assert.Equal(1, await page.GetByText(copy).CountAsync());
            Assert.Equal(0, await page.Locator("[role='alert']").CountAsync());
        }
    }

    /// <summary>
    /// AC12, AD-3 - a <c>404</c> on an edit reverts and then renders the page's one refusal surface, "Not
    /// available."; the panel does not stay open over a Task that is not there.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR32")]
    public async Task A_404_on_an_edit_renders_the_pages_refusal_surface()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            var held = api.HoldTaskEditAsync();

            await CommitTitleAsync(page, "Send the brief");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            api.ReleaseTaskEditRefused(404, ProblemTypes.NotAvailable);

            await page.Locator("main").GetByText(NotAvailableCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Dialog).CountAsync());
            Assert.DoesNotContain("Send the brief", await page.Locator("body").InnerTextAsync(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC12 - a refused Status change puts the card back in its column, in its place, and both totals back, and
    /// the panel says why. The refusal here is a Role re-read lower on the server (<c>403</c>).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-11")]
    public async Task A_refused_Status_change_puts_the_card_and_both_totals_back()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "Moving", "Staying");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            var held = api.HoldTaskEditAsync();

            await ChooseStatusAsync(page, "Done");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            var columns = page.Locator(Column);

            Assert.Equal("1", (await columns.Nth(2).Locator(ColumnCount).TextContentAsync())?.Trim());

            api.ReleaseTaskEditRefused(403, InsufficientRoleType, "manage-tasks");

            await page.Locator(Dialog).GetByText(ManageTasksCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(["Moving", "Staying"], (await columns.Nth(0).Locator(".task-card__title").AllTextContentsAsync()).Select(title => title.Trim()));
            Assert.Equal("2", (await columns.Nth(0).Locator(ColumnCount).TextContentAsync())?.Trim());
            Assert.Equal("0", (await columns.Nth(2).Locator(ColumnCount).TextContentAsync())?.Trim());
            Assert.Equal(0, await columns.Nth(2).Locator(Card).CountAsync());
        }
    }

    /// <summary>
    /// AC12, Q8 - an edit that is never answered (the connection resets) is reverted to the last admitted value
    /// and the panel says "Not sent." inline; the client is never the arbiter.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-11")]
    public async Task An_unanswered_edit_is_reverted_and_said_to_be_not_sent()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            var held = api.HoldTaskEditAsync();

            await CommitTitleAsync(page, "Send the brief");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            api.ReleaseTaskEditUnanswered();

            await page.Locator(Dialog).GetByText(NotSentCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal("Draft the brief", await TitleField(page).InputValueAsync());
            Assert.Equal("Draft the brief", (await CardOf(page, tasks[0].Id).Locator(".task-card__title").TextContentAsync())?.Trim());
        }
    }

    /// <summary>
    /// Q8, AD-18 - changes to one attribute while its edit is out wait behind it, and only the latest waiting
    /// value is sent once the first is admitted: one tab never has two writes racing for one field. The card shows
    /// the latest value throughout. Anchor: the first edit was sent and admitted.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-11")]
    [Trait("Requirement", "AD-18")]
    public async Task Changes_to_one_attribute_while_its_edit_is_out_wait_and_only_the_latest_is_sent()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            var held = api.HoldTaskEditAsync();

            await CommitTitleAsync(page, "First change");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            await CommitTitleAsync(page, "Second change");
            await CommitTitleAsync(page, "Third change");

            await CardOf(page, tasks[0].Id).Locator(".task-card__title", new LocatorLocatorOptions { HasText = "Third change" })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Single(Edits(api));

            api.ReleaseTaskEdit();
            await WaitUntilAsync(() => api.FindTask(tasks[0].Id)?.Task.Title == "Third change", "The latest waiting title was never sent.");

            Assert.Equal(["First change", "Third change"], Edits(api).Select(TitleOf));
            Assert.Equal("Third change", await TitleField(page).InputValueAsync());
        }
    }

    /// <summary>
    /// AC12, Q8 - each attribute reverts to its own last admitted value: a title edit held while a due-date edit
    /// is admitted, then refused, puts the title back and leaves the admitted due date in place - not the display
    /// as it stood before either edit.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-11")]
    public async Task A_refusal_reverts_only_its_own_attribute_to_its_last_admitted_value()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale, "Europe/London");

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            var held = api.HoldTaskEditAsync();

            await CommitTitleAsync(page, "Send the brief");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            await DueDateField(page).FillAsync("2026-11-20");
            await WaitUntilAsync(() => api.FindTask(tasks[0].Id)?.Task.DueDate is not null, "The due-date edit was never admitted.");

            api.ReleaseTaskEditRefused(400, InvalidTaskTitleType);

            await page.Locator(Dialog).GetByText(TitleInvalidCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal("Draft the brief", await TitleField(page).InputValueAsync());
            Assert.Equal("2026-11-20", await DueDateField(page).InputValueAsync());
        }
    }

    /// <summary>
    /// AC3, UX-DR27 - a Viewer opens the panel too, and sees the title, Status and due date as text: no input,
    /// no picker, no clear control, no button but Close, and nothing disabled. Anchor: the Owner's panel on the
    /// other Space's Board has the title field.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "AD-1")]
    public async Task A_Viewer_sees_the_attributes_as_text_with_no_edit_affordance_and_nothing_disabled()
    {
        var api = TwoSpaces();
        var owned = Seed(api, Northwind, Atlas, "Shared card");
        var viewed = Seed(api, Harbour, Beacon, "Shared card");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, owned[0].Id);

            Assert.Equal(1, await TitleField(page).CountAsync());

            await page.EvaluateAsync("path => Blazor.navigateTo(path)", ClientBoard(Harbour, Beacon));
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await OpenPanelAsync(page, viewed[0].Id);

            var dialog = page.Locator(Dialog);

            Assert.Contains("Shared card", await dialog.InnerTextAsync(), StringComparison.Ordinal);
            Assert.Equal(0, await dialog.Locator("input, select, textarea, [role='listbox'], [aria-haspopup]").CountAsync());
            Assert.Equal([Close], (await dialog.GetByRole(AriaRole.Button).AllInnerTextsAsync()).Select(name => name.Trim()));
            Assert.Equal(0, await page.Locator("main [disabled], main [aria-disabled='true'], [role='dialog'] [disabled], [role='dialog'] [aria-disabled='true']").CountAsync());
        }
    }

    /// <summary>
    /// AC14, UX-DR37 - a Role re-read lower while the panel is open (the re-arrival that follows a navigation to
    /// the same Board) removes the edit controls in place: the panel stays, its title field goes.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR37")]
    public async Task A_Role_reread_lower_removes_the_edit_controls_in_place()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            Assert.Equal(1, await TitleField(page).CountAsync());

            api.Roles[Northwind] = "viewer";

            await page.EvaluateAsync("path => Blazor.navigateTo(path)", ClientBoard(Northwind, Atlas));
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await TitleField(page).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.Equal(1, await page.Locator(Dialog).CountAsync());
            Assert.Contains("Draft the brief", await page.Locator(Dialog).InnerTextAsync(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC14, UX-DR31 - leaving the Space purges the panel with the Board: after switching Spaces, no dialog is
    /// open and the Task's title is nowhere on screen; <c>document.title</c> is still <c>Yello</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR31")]
    public async Task Leaving_the_Space_closes_the_panel_with_the_Board()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "A title that belongs to Northwind");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            await ChooseInSwitcherAsync(page, "Harbour Ops");
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Dialog).CountAsync());
            Assert.DoesNotContain("A title that belongs to Northwind", await page.Locator("body").InnerTextAsync(), StringComparison.Ordinal);
            Assert.Equal("Yello", await page.TitleAsync());
        }
    }

    /// <summary>
    /// AC7, UX-DR42, Q4 (a) - a due date renders in the viewer's own locale and zone: the same instant
    /// (02:00 UTC on 3 November) is the 2nd in Los Angeles and the 3rd in Tokyo, on the card and in the panel's
    /// date field.
    /// </summary>
    /// <param name="locale">The browser's locale.</param>
    /// <param name="zone">The browser's zone.</param>
    /// <param name="localDate">The date field's value in that zone.</param>
    /// <param name="day">The day of the month the card shows.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("en-US", "America/Los_Angeles", "2026-11-02", "2")]
    [InlineData("en-GB", "Asia/Tokyo", "2026-11-03", "3")]
    [Trait("Requirement", "UX-DR42")]
    [Trait("Requirement", "AR-34")]
    public async Task A_due_date_renders_in_the_viewers_locale_and_zone(string locale, string zone, string localDate, string day)
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "Dated");
        var (column, task) = api.FindTask(tasks[0].Id)!.Value;

        column.Tasks[0] = task with { DueDate = new DateTimeOffset(2026, 11, 3, 2, 0, 0, TimeSpan.Zero) };

        var (context, page) = await surface.OpenAsync(locale, zone);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);

            var meta = await CardOf(page, tasks[0].Id).Locator(".task-card__meta").InnerTextAsync();

            Assert.Matches($@"(^|\D){day}(\D|$)", meta);

            await OpenPanelAsync(page, tasks[0].Id);

            Assert.Equal(localDate, await DueDateField(page).InputValueAsync());
        }
    }

    /// <summary>
    /// AC7, Q4 (a) - choosing a date commits the start of that day in the editor's zone as an ISO 8601 instant
    /// with its offset: 3 November chosen in Tokyo is 15:00 UTC on 2 November.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-34")]
    public async Task Choosing_a_date_commits_the_start_of_that_day_in_the_editors_zone_with_its_offset()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "Dated");
        var (context, page) = await surface.OpenAsync(EnglishLocale, "Asia/Tokyo");

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            await DueDateField(page).FillAsync("2026-11-03");
            await WaitUntilAsync(() => Edits(api).Any(), "No edit was sent for the date.");

            using var document = JsonDocument.Parse(Assert.Single(Edits(api)).Body ?? "{}");
            var sent = document.RootElement.GetProperty("dueDate").GetString() ?? string.Empty;

            Assert.Matches(IsoInstantWithOffset, sent);
            Assert.Equal(new DateTimeOffset(2026, 11, 2, 15, 0, 0, TimeSpan.Zero), DateTimeOffset.Parse(sent, CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// AC7, Q1 (a) - "Clear due date" commits <c>dueDate: null</c> and nothing else, and the field empties.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-20")]
    public async Task Clearing_the_due_date_commits_null()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "Dated");
        var (column, task) = api.FindTask(tasks[0].Id)!.Value;

        column.Tasks[0] = task with { DueDate = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero) };

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            await page.Locator(Dialog).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = ClearDueDate, Exact = true }).ClickAsync();
            await WaitUntilAsync(() => Edits(api).Any(), "No edit was sent for the clear.");

            using var document = JsonDocument.Parse(Assert.Single(Edits(api)).Body ?? "{}");

            Assert.Equal(["dueDate"], document.RootElement.EnumerateObject().Select(member => member.Name));
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("dueDate").ValueKind);
            Assert.Equal(string.Empty, await DueDateField(page).InputValueAsync());
        }
    }

    /// <summary>
    /// AC14, UX-DR33 - the panel's title input is <c>autocomplete="off"</c> with a field name autofill would not
    /// reuse, and after an edit neither the title nor the Project name reaches <c>document.title</c> or the
    /// address.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR33")]
    public async Task The_title_input_is_not_autofilled_and_no_title_reaches_the_tab_or_the_address()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            var field = TitleField(page);

            Assert.Equal("off", await field.GetAttributeAsync("autocomplete"));
            Assert.DoesNotContain((await field.GetAttributeAsync("name") ?? string.Empty).ToUpperInvariant(), ReusableFieldNames);

            await CommitTitleAsync(page, "Send the brief");
            await WaitUntilAsync(() => api.FindTask(tasks[0].Id)?.Task.Title == "Send the brief", "The edit was never applied.");

            Assert.Equal("Yello", await page.TitleAsync());

            var address = await page.EvaluateAsync<string>("() => location.href");

            Assert.DoesNotContain("brief", address, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Atlas", address, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Q9 (a), <c>EXPERIENCE.md:318</c> - the innermost meaning of <c>Esc</c> wins: inside the title field it
    /// reverts the field and sends nothing; a second <c>Esc</c> closes the panel.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR40")]
    [Trait("Priority", "P1")]
    public async Task Esc_in_the_title_field_reverts_it_first_and_a_second_Esc_closes_the_panel()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            var field = TitleField(page);

            await field.FillAsync("Half typed");
            await field.PressAsync("Escape");

            Assert.Equal("Draft the brief", await field.InputValueAsync());
            Assert.Equal(1, await page.Locator(Dialog).CountAsync());
            Assert.Empty(Edits(api));

            await field.PressAsync("Escape");
            await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.Empty(Edits(api));
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
    /// A Project on Space home and its Board: three columns in the default order, the titles in the first column.
    /// </summary>
    private static List<FakeTask> Seed(FakeYelloApi api, Guid spaceId, Guid projectId, params string[] titles)
    {
        var columns = StatusNames.Select(status => new FakeColumn(Guid.NewGuid(), status, 0, [])).ToList();

        foreach (var title in titles)
        {
            columns[0].Tasks.Add(new FakeTask(Guid.NewGuid(), title, columns[0].StatusId, $"a{columns[0].Tasks.Count:D5}", DateTimeOffset.UtcNow));
            columns[0].Total++;
        }

        api.Boards[(spaceId, projectId)] = new FakeBoard(new FakeBoardProject(projectId, spaceId == Northwind ? "Atlas" : "Beacon"), columns);
        api.Projects[spaceId] = [new FakeProject(projectId, spaceId == Northwind ? "Atlas" : "Beacon", DateTimeOffset.UtcNow)];

        // A copy: an edit replaces or moves the fake's own entries, and a case keeps the ids it seeded.
        return [.. columns[0].Tasks];
    }

    private static ILocator CardOf(IPage page, Guid taskId) =>
        page.Locator(Card, new PageLocatorOptions { Has = page.Locator($"button[data-task-id='{taskId:D}']") });

    private static ILocator TitleField(IPage page) =>
        page.Locator(Dialog).GetByLabel(TitleLabel, new LocatorGetByLabelOptions { Exact = true });

    private static ILocator DueDateField(IPage page) =>
        page.Locator(Dialog).GetByLabel(DueDateLabel, new LocatorGetByLabelOptions { Exact = true });

    private static async Task OpenPanelAsync(IPage page, Guid taskId)
    {
        await page.Locator($"{Card} button[data-task-id='{taskId:D}']").ClickAsync();
        await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    /// <summary>
    /// Types a new title into the panel's field and commits it with Enter (Q9 (a)).
    /// </summary>
    private static async Task CommitTitleAsync(IPage page, string title)
    {
        var field = TitleField(page);

        await field.FillAsync(title);
        await field.PressAsync("Enter");
    }

    private static async Task ChooseStatusAsync(IPage page, string status)
    {
        var dialog = page.Locator(Dialog);

        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = StatusLabel }).ClickAsync();
        await dialog.Locator("[role='listbox'] [role='option']", new LocatorLocatorOptions { HasText = status }).ClickAsync();
    }

    private static List<FakeRequest> Edits(FakeYelloApi api) =>
        [.. api.Requests.Where(request => request.Method == "PATCH" && request.Path.Contains("/tasks/", StringComparison.Ordinal))];

    private static string TitleOf(FakeRequest edit)
    {
        using var document = JsonDocument.Parse(edit.Body ?? "{}");

        return document.RootElement.GetProperty("title").GetString() ?? string.Empty;
    }

    private static string[] Members(FakeRequest edit)
    {
        using var document = JsonDocument.Parse(edit.Body ?? "{}");

        return [.. document.RootElement.EnumerateObject().Select(member => member.Name)];
    }

    /// <summary>
    /// UX-DR26: nothing on the page is a save control or a save indicator - no button named "Save", no text
    /// reading "Saving" or "Saved", no progress or busy marker inside the panel.
    /// </summary>
    private static async Task AssertNoSaveAffordanceAsync(IPage page)
    {
        var text = await page.Locator("body").InnerTextAsync();

        Assert.All(Banned, word => Assert.DoesNotContain(word, text, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { NameRegex = new Regex("save", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)) }).CountAsync());
        Assert.Equal(0, await page.Locator($"{Dialog} [role='progressbar'], {Dialog} [aria-busy='true']").CountAsync());
    }

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

    /// <summary>
    /// The client's Board page for one Project: ids only.
    /// </summary>
    private static string ClientBoard(Guid spaceId, Guid projectId) => $"/spaces/{spaceId:D}/projects/{projectId:D}";

    private string Address(string path) => new Uri(surface.BaseAddress, path.TrimStart('/')).ToString();

    private async Task OpenBoardAsync(FakeYelloApi api, IPage page, Guid spaceId, Guid projectId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address(ClientBoard(spaceId, projectId)));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.Locator(Card).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
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
