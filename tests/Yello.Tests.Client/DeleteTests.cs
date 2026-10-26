using Microsoft.Playwright;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Labels;

namespace Yello.Tests.Client;

/// <summary>
/// Story 2.5 in a real browser: deleting a Task from its detail panel and a Project from its row on Space home. The
/// confirm replaces its invoking container in place (never a second dialog), names what it destroys and says "This
/// cannot be undone."; the deletion is not optimistic; cancel destroys nothing and returns focus; a Viewer is offered
/// nothing. AC1, AC2, AC4, AC6, AC11-AC14; UX-DR12, UX-DR26, UX-DR27, UX-DR36, UX-DR37, UX-DR39, UX-DR40;
/// <c>deferred-work.md:510</c>. Written red by the story 2.5 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>The API is <see cref="FakeYelloApi"/></b> with story 2.5's two <c>DELETE</c> routes: a deletion can be held
/// (<see cref="FakeYelloApi.HoldTaskDeleteAsync"/>, <see cref="FakeYelloApi.HoldProjectDeleteAsync"/>) and released
/// applied, refused or unanswered - the TESTING-CONVENTIONS shape for a non-optimistic write: hold, assert nothing
/// removed, release, assert removed. The listing states <c>taskCount</c> when
/// <see cref="FakeYelloApi.ServeProjectTaskCounts"/> is on (Q2 (a)).
/// </para>
/// <para>
/// <b>The locators are assumptions</b>, to be corrected to the markup Tasks 9 and 10 build and the correction recorded
/// (story Q1, Q3, Q8, Q9, Q12): the panel's control is a button named "Delete Task", its last control (Q8); the confirm is a
/// <c>.destructive-confirm</c> inside the one <c>[role=dialog]</c> holding "Delete {title}?", "This cannot be undone.",
/// one <c>.button-danger</c> named "Delete Task" and a "Cancel"; focus opens on Cancel (Q9); after a deletion focus is
/// inside the column that held the Task, never on <c>&lt;body&gt;</c> or a card (Q3); the Project's control is a button
/// named "Delete Project" in its row on Space home (Q1 (a)), whose confirm replaces the row's content and states "It holds
/// no Tasks.", "It holds 1 Task." or "It holds {n} Tasks."; announcements read "Task deleted." and "Project deleted."
/// (Q11). Copy that already exists ("Viewers cannot edit Tasks.", the idempotency sentences, "Not sent.", "Not
/// available.", "No Projects yet.") is asserted as written.
/// </para>
/// <para>
/// <b>The Label confirm's case ran from the ATDD run on</b>: 2.4's confirm already met AC13's rendered half (the
/// sentence, one danger action, no icon). Every other case was activated by story 2.5's dev phase.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-23")]
[Trait("Requirement", "FR-17")]
[Trait("Requirement", "UX-DR12")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class DeleteTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string RoleChip = "nav.context-bar .role-chip";

    private const string Column = "main .board-column";
    private const string ColumnCount = ".column-count";
    private const string Card = ".task-card";
    private const string Dialog = "[role='dialog']";
    private const string Confirm = ".destructive-confirm";
    private const string DangerButton = ".button-danger";
    private const string DisabledAnywhere = "main [disabled], main [aria-disabled='true'], [role='dialog'] [disabled]";

    private const string DeleteTask = "Delete Task";
    private const string DeleteProject = "Delete Project";
    private const string DeleteLabel = "Delete Label";
    private const string Cancel = "Cancel";
    private const string TitleLabel = "Title";

    private const string CannotBeUndone = "This cannot be undone.";
    private const string TaskDeletedCopy = "Task deleted.";
    private const string ProjectDeletedCopy = "Project deleted.";
    private const string HoldsNoTasks = "It holds no Tasks.";
    private const string HoldsOneTask = "It holds 1 Task.";
    private const string EmptyState = "No Projects yet.";
    private const string NotSentCopy = "Not sent.";
    private const string NotAvailableCopy = "Not available.";
    private const string ManageTasksCopy = "Viewers cannot edit Tasks.";
    private const string ReusedCopy = "That request was already used for something else. Try again.";
    private const string InFlightCopy = "That was already sent. Reload to see it.";

    private const string InsufficientRoleType = "urn:yello:problem:insufficient-role";
    private const string IdempotencyKeyReusedType = "urn:yello:problem:idempotency-key-reused";
    private const string IdempotencyKeyInFlightType = "urn:yello:problem:idempotency-key-in-flight";

    private static readonly string[] StatusNames = ["Todo", "Doing", "Done"];

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();
    private static readonly Guid Beacon = Guid.NewGuid();

    private bool _installed;

    /// <summary>
    /// AC1, AC4, AC11, UX-DR12, Q9 - "Delete Task" replaces the panel's content in place: still exactly one
    /// <c>[role=dialog]</c>, the Task's attributes no longer in it, a confirm naming the Task by its title and saying
    /// "This cannot be undone.", exactly one danger action and a Cancel, and focus inside the confirm on Cancel. Nothing is
    /// sent until the danger action is chosen.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR36")]
    public async Task Delete_Task_replaces_the_panel_content_with_a_confirm_naming_the_Task()
    {
        var api = TwoSpaces();
        var tasks = SeedBoard(api, Northwind, Atlas, "Draft the brief", "Book the room");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);
            await AskToDeleteTaskAsync(page);

            var dialog = page.Locator(Dialog);
            var confirm = dialog.Locator(Confirm);
            var text = await confirm.InnerTextAsync();

            Assert.Equal(1, await dialog.CountAsync());
            Assert.Equal(0, await dialog.GetByLabel(TitleLabel, new LocatorGetByLabelOptions { Exact = true }).CountAsync());
            Assert.Contains("Delete Draft the brief?", text, StringComparison.Ordinal);
            Assert.Contains(CannotBeUndone, text, StringComparison.Ordinal);
            Assert.Equal(1, await confirm.Locator(DangerButton).CountAsync());
            Assert.Equal(1, await confirm.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = Cancel, Exact = true }).CountAsync());
            Assert.Equal(Cancel, await page.EvaluateAsync<string>("() => document.activeElement ? document.activeElement.textContent.trim() : ''"));
            Assert.Empty(Deletions(api));
        }
    }

    /// <summary>
    /// AC11, UX-DR26, UX-DR39, <c>deferred-work.md:510</c> - the deletion is not optimistic: while the <c>DELETE</c> is
    /// held the card and the panel stay; on <c>204</c> the panel closes, the card leaves its column, the column's total
    /// drops by one, "Task deleted." is announced politely, and focus is inside the column that held the Task - not on
    /// <c>&lt;body&gt;</c>, not on a card.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR26")]
    [Trait("Requirement", "UX-DR39")]
    public async Task A_Task_deletion_waits_for_the_server_then_removes_the_card_announces_and_focuses_its_column()
    {
        var api = TwoSpaces();
        var tasks = SeedBoard(api, Northwind, Atlas, "Draft the brief", "Book the room");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);
            await AskToDeleteTaskAsync(page);

            var held = api.HoldTaskDeleteAsync();

            await page.Locator(Dialog).Locator(Confirm).Locator(DangerButton).ClickAsync();
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            // Held: nothing has gone yet.
            Assert.Equal(1, await CardOf(page, tasks[0].Id).CountAsync());
            Assert.Equal(1, await page.Locator(Dialog).CountAsync());
            Assert.Equal("2", (await ColumnNamed(page, "Todo").Locator(ColumnCount).InnerTextAsync()).Trim());

            api.ReleaseTaskDelete();

            await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });
            await page.Locator("[aria-live='polite']", new PageLocatorOptions { HasText = TaskDeletedCopy }).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            Assert.Equal(0, await CardOf(page, tasks[0].Id).CountAsync());
            Assert.Equal("1", (await ColumnNamed(page, "Todo").Locator(ColumnCount).InnerTextAsync()).Trim());
            Assert.Equal(
                "column:Todo",
                await page.EvaluateAsync<string>(
                    """
                    () => {
                        const active = document.activeElement;
                        if (!active || active === document.body) return 'body';
                        if (active.closest('.task-card')) return 'card';
                        const column = active.closest('.board-column');
                        const heading = column ? column.querySelector('h2') : null;
                        return heading ? 'column:' + heading.textContent.trim() : 'elsewhere:' + active.tagName;
                    }
                    """));
        }
    }

    /// <summary>
    /// AC6, AC11, Q9 (2.3: the innermost meaning of <c>Esc</c> first) - <c>Esc</c> in the confirm, or its Cancel, brings
    /// the panel's content back with focus on "Delete Task" and sends nothing; a second <c>Esc</c> closes the panel.
    /// </summary>
    /// <param name="byEscape">Whether the confirm is dismissed by <c>Esc</c> (otherwise by its Cancel).</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "UX-DR39")]
    [Trait("Requirement", "UX-DR40")]
    public async Task Cancelling_the_Task_confirm_destroys_nothing_and_returns_focus_to_Delete_Task(bool byEscape)
    {
        var api = TwoSpaces();
        var tasks = SeedBoard(api, Northwind, Atlas, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);
            await AskToDeleteTaskAsync(page);

            var dialog = page.Locator(Dialog);

            if (byEscape)
            {
                await page.Keyboard.PressAsync("Escape");
            }
            else
            {
                await dialog.Locator(Confirm).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = Cancel, Exact = true }).ClickAsync();
            }

            await dialog.GetByLabel(TitleLabel, new LocatorGetByLabelOptions { Exact = true }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await dialog.Locator(Confirm).CountAsync());
            Assert.Equal(DeleteTask, await page.EvaluateAsync<string>("() => document.activeElement ? document.activeElement.textContent.trim() : ''"));
            Assert.Empty(Deletions(api));

            await page.Keyboard.PressAsync("Escape");
            await dialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.Equal(1, await CardOf(page, tasks[0].Id).CountAsync());
            Assert.Empty(Deletions(api));
        }
    }

    /// <summary>
    /// AC11 - a refused deletion is said in the confirm's own status region in the copy that already exists for its type,
    /// and nothing is removed: the card stays and its column's total is unchanged.
    /// </summary>
    /// <param name="status">The refusal's status.</param>
    /// <param name="type">Its problem type.</param>
    /// <param name="capability">The capability a <c>403</c> names.</param>
    /// <param name="copy">What the confirm says.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(403, InsufficientRoleType, "manage-tasks", ManageTasksCopy)]
    [InlineData(409, IdempotencyKeyInFlightType, null, InFlightCopy)]
    [InlineData(422, IdempotencyKeyReusedType, null, ReusedCopy)]
    [Trait("Requirement", "UX-DR26")]
    public async Task A_refused_Task_deletion_is_said_in_the_confirm_and_removes_nothing(int status, string type, string? capability, string copy)
    {
        var api = TwoSpaces();
        var tasks = SeedBoard(api, Northwind, Atlas, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);
            await AskToDeleteTaskAsync(page);

            api.NextTaskDeleteRefusal = (status, type, capability);

            await page.Locator(Dialog).Locator(Confirm).Locator(DangerButton).ClickAsync();
            await page.Locator(Dialog).GetByText(copy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(1, await CardOf(page, tasks[0].Id).CountAsync());
            Assert.Equal("1", (await ColumnNamed(page, "Todo").Locator(ColumnCount).InnerTextAsync()).Trim());
            Assert.Single(Deletions(api));
        }
    }

    /// <summary>
    /// AC11, NFR-5, 2.3 Q8 (keys reused on an identical resend after no answer; <c>deferred-work.md:530</c> was 2.4's Label
    /// delete taking a fresh key per click) - a deletion that gets no answer says "Not sent." in the confirm and removes
    /// nothing; sending it again carries the same <c>Idempotency-Key</c>, and that answer removes the card.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-5")]
    [Trait("Requirement", "AR-25")]
    public async Task An_unanswered_Task_deletion_says_not_sent_and_its_resend_reuses_the_key()
    {
        var api = TwoSpaces();
        var tasks = SeedBoard(api, Northwind, Atlas, "Draft the brief", "Book the room");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);
            await AskToDeleteTaskAsync(page);

            var held = api.HoldTaskDeleteAsync();
            var danger = page.Locator(Dialog).Locator(Confirm).Locator(DangerButton);

            await danger.ClickAsync();
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);
            api.ReleaseTaskDeleteUnanswered();

            await page.Locator(Dialog).GetByText(NotSentCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            Assert.Equal(1, await CardOf(page, tasks[0].Id).CountAsync());

            await danger.ClickAsync();
            await CardOf(page, tasks[0].Id).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            var sent = Deletions(api);

            Assert.Equal(2, sent.Count);
            Assert.NotNull(sent[0].Header(Idempotency.HeaderName));
            Assert.Equal(sent[0].Header(Idempotency.HeaderName), sent[1].Header(Idempotency.HeaderName));
        }
    }

    /// <summary>
    /// AC11, Q7 - an edit of the Task still in flight when its deletion is admitted answers <c>404</c> afterwards; that
    /// answer raises no refusal surface, no copy and no revert: the Board stays, with the other card on it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR26")]
    public async Task An_edit_answered_404_after_its_Tasks_deletion_raises_no_refusal()
    {
        var api = TwoSpaces();
        var tasks = SeedBoard(api, Northwind, Atlas, "Draft the brief", "Book the room");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            var editHeld = api.HoldTaskEditAsync();
            var title = page.Locator(Dialog).GetByLabel(TitleLabel, new LocatorGetByLabelOptions { Exact = true });

            await title.FillAsync("Draft the brief, again");
            await title.PressAsync("Enter");
            await editHeld.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            await AskToDeleteTaskAsync(page);
            await page.Locator(Dialog).Locator(Confirm).Locator(DangerButton).ClickAsync();
            await CardOf(page, tasks[0].Id).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            // The held edit is now answered: the Task is gone, so the fake answers it 404 not-available.
            api.ReleaseTaskEdit();

            await page.Locator("[aria-live='polite']", new PageLocatorOptions { HasText = TaskDeletedCopy }).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });
            await WaitUntilAsync(() => api.Requests.Count(request => request.Method == "PATCH") == 1, "The held edit was never answered.");

            Assert.Equal(1, await CardOf(page, tasks[1].Id).CountAsync());
            Assert.DoesNotContain(NotAvailableCopy, await page.Locator("body").InnerTextAsync(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC11, Q4 (<c>deferred-work.md:530</c>, owner 4.4) - a deletion answered <c>404</c> follows the Board's existing
    /// pattern for a <c>404</c>: the Space refusal surface, "Not available." - the indistinguishable not-found, not a
    /// claim that the Task was deleted elsewhere.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_Task_deletion_answered_404_follows_the_Boards_not_found_pattern()
    {
        var api = TwoSpaces();
        var tasks = SeedBoard(api, Northwind, Atlas, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);
            await AskToDeleteTaskAsync(page);

            api.NextTaskDeleteRefusal = (404, ProblemTypes.NotAvailable, null);

            await page.Locator(Dialog).Locator(Confirm).Locator(DangerButton).ClickAsync();
            await page.GetByText(NotAvailableCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Single(Deletions(api));
        }
    }

    /// <summary>
    /// AC2, AC12, AD-29, Q2 (a) - the Project confirm replaces its row's content in place, names the Project, states its
    /// true Task count read when the confirm opens - "It holds no Tasks." for none, "It holds 1 Task." for one, "It holds
    /// 3 Tasks." for three - and says "This cannot be undone.", with one danger action. Read when it opens: the listing is
    /// read again, so a count that changed since Space home loaded is the one stated.
    /// </summary>
    /// <param name="tasks">How many Tasks the Project holds.</param>
    /// <param name="sentence">The count's sentence.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(0, HoldsNoTasks)]
    [InlineData(1, HoldsOneTask)]
    [InlineData(3, "It holds 3 Tasks.")]
    [Trait("Requirement", "AD-29")]
    [Trait("Requirement", "UX-DR36")]
    public async Task The_Project_confirm_names_the_Project_and_its_true_Task_count(int tasks, string sentence)
    {
        var api = TwoSpaces();

        api.ServeProjectTaskCounts = true;
        SeedProjectHolding(api, Northwind, Atlas, "Atlas", tasks);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);

            var listingsBefore = ProjectListings(api);

            await AskToDeleteProjectAsync(page, "Atlas");

            var confirm = ProjectRow(page, "Atlas").Locator(Confirm);
            var text = await confirm.InnerTextAsync();

            Assert.Contains("Delete Atlas?", text, StringComparison.Ordinal);
            Assert.Contains(sentence, text, StringComparison.Ordinal);
            Assert.Contains(CannotBeUndone, text, StringComparison.Ordinal);
            Assert.Equal(1, await confirm.Locator(DangerButton).CountAsync());
            Assert.Equal(0, await page.Locator(Dialog).CountAsync());
            Assert.True(ProjectListings(api) > listingsBefore, "The confirm stated a count without reading it when it opened.");
            Assert.Empty(Deletions(api));
        }
    }

    /// <summary>
    /// AC12, AC6 - cancelling the Project confirm restores the row, focuses its "Delete Project" control, and sends
    /// nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR39")]
    public async Task Cancelling_the_Project_confirm_destroys_nothing_and_returns_focus_to_Delete_Project()
    {
        var api = TwoSpaces();

        api.ServeProjectTaskCounts = true;
        SeedProjectHolding(api, Northwind, Atlas, "Atlas", 2);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);
            await AskToDeleteProjectAsync(page, "Atlas");

            await ProjectRow(page, "Atlas").Locator(Confirm).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = Cancel, Exact = true }).ClickAsync();
            await ProjectRow(page, "Atlas").GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Atlas" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Confirm).CountAsync());
            Assert.Equal(DeleteProject, await page.EvaluateAsync<string>("() => document.activeElement ? document.activeElement.textContent.trim() : ''"));
            Assert.Empty(Deletions(api));
        }
    }

    /// <summary>
    /// AC12, UX-DR26, UX-DR39, Q3 - a Project deletion is not optimistic: held, the row stays; on <c>204</c> the row
    /// leaves the listing, "Project deleted." is announced, and focus lands on a heading of Space home, never on
    /// <c>&lt;body&gt;</c>. Deleting the last Project shows the empty state with its one action.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR26")]
    [Trait("Requirement", "UX-DR35")]
    public async Task A_Project_deletion_waits_for_the_server_then_removes_the_row_and_announces()
    {
        var api = TwoSpaces();

        api.ServeProjectTaskCounts = true;
        SeedProjectHolding(api, Northwind, Atlas, "Atlas", 1);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);
            await AskToDeleteProjectAsync(page, "Atlas");

            var held = api.HoldProjectDeleteAsync();

            await ProjectRow(page, "Atlas").Locator(Confirm).Locator(DangerButton).ClickAsync();
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            Assert.Equal(1, await ProjectRow(page, "Atlas").CountAsync());

            api.ReleaseProjectDelete();

            await page.GetByText(EmptyState).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.Locator("[aria-live='polite']", new PageLocatorOptions { HasText = ProjectDeletedCopy }).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            Assert.Equal(0, await ProjectRow(page, "Atlas").CountAsync());
            Assert.Matches(
                "^H[1-3]$",
                await page.EvaluateAsync<string>("() => document.activeElement && document.activeElement !== document.body && document.activeElement.closest('main') ? document.activeElement.tagName : 'BODY'"));
        }
    }

    /// <summary>
    /// AC1, AC14, UX-DR27 - a Viewer is offered no delete: no "Delete Task" in the Task detail panel and no
    /// "Delete Project" on Space home - absent, not disabled, not hidden by CSS - and nothing in <c>main</c> or the panel
    /// is disabled. Anchored: the Owner of the other Space is offered both.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    public async Task A_Viewer_is_offered_no_delete_control_and_nothing_is_disabled()
    {
        var api = TwoSpaces();
        var viewersTasks = SeedBoard(api, Harbour, Beacon, "Viewer's Task");
        var ownersTasks = SeedBoard(api, Northwind, Atlas, "Owner's Task");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            // Anchor: the Owner is offered both controls.
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, ownersTasks[0].Id);
            Assert.Equal(1, await DeleteTaskControl(page).CountAsync());

            await OpenSpaceAsync(api, page, Northwind);
            await ProjectRow(page, "Atlas").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            Assert.Equal(1, await ProjectRow(page, "Atlas").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = DeleteProject }).CountAsync());

            // The Viewer: neither, and nothing disabled.
            await OpenBoardAsync(api, page, Harbour, Beacon);
            await OpenPanelAsync(page, viewersTasks[0].Id);

            Assert.Equal(0, await DeleteTaskControl(page).CountAsync());
            Assert.DoesNotContain(DeleteTask, await page.EvaluateAsync<string>("() => document.body.innerHTML"), StringComparison.Ordinal);
            Assert.Equal(0, await page.Locator(DisabledAnywhere).CountAsync());

            await OpenSpaceAsync(api, page, Harbour);
            await ProjectRow(page, "Beacon").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = DeleteProject }).CountAsync());
            Assert.DoesNotContain(DeleteProject, await page.EvaluateAsync<string>("() => document.body.innerHTML"), StringComparison.Ordinal);
            Assert.Equal(0, await page.Locator(DisabledAnywhere).CountAsync());
        }
    }

    /// <summary>
    /// AC4, AC13 - the Task's and the Project's confirms carry the copy, not an icon: each says "This cannot be undone.",
    /// holds exactly one danger action, and holds no <c>img</c>, no <c>svg</c> and nothing classed as an icon.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR36")]
    [Trait("Requirement", "UX-DR5")]
    public async Task The_Task_and_Project_confirms_say_it_cannot_be_undone_with_one_danger_action_and_no_icon()
    {
        var api = TwoSpaces();

        api.ServeProjectTaskCounts = true;

        var tasks = SeedBoard(api, Northwind, Atlas, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);
            await AskToDeleteTaskAsync(page);
            await AssertConfirmCarriesCopyNotIconAsync(page.Locator(Dialog).Locator(Confirm));

            await OpenSpaceAsync(api, page, Northwind);
            await AskToDeleteProjectAsync(page, "Atlas");
            await AssertConfirmCarriesCopyNotIconAsync(ProjectRow(page, "Atlas").Locator(Confirm));
        }
    }

    /// <summary>
    /// AC4, AC13 - 2.4's Label confirm, which AC13 also covers, says "This cannot be undone.", holds exactly one danger
    /// action and no icon. <b>Green today</b> (2.4 built it so); a standing gate that Task 11's border and any shared
    /// confirm component (Q12) must keep.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR36")]
    [Trait("Requirement", "UX-DR5")]
    public async Task The_Label_confirm_says_it_cannot_be_undone_with_one_danger_action_and_no_icon()
    {
        var api = TwoSpaces();

        api.Labels[Northwind] = [new FakeLabel(Guid.NewGuid(), "Unused", LabelColours.Keys[0])];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await InstallOnceAsync(api, page);
            await page.GotoAsync(Address($"spaces/{Northwind:D}/settings"));
            await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var row = page.Locator("main li", new PageLocatorOptions { HasText = "Unused" });

            await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = DeleteLabel }).ClickAsync(new LocatorClickOptions { Timeout = Timeout });

            var confirm = row.Locator(Confirm);

            await confirm.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await AssertConfirmCarriesCopyNotIconAsync(confirm);
            Assert.Empty(Deletions(api));
        }
    }

    private static async Task AssertConfirmCarriesCopyNotIconAsync(ILocator confirm)
    {
        Assert.Contains(CannotBeUndone, await confirm.InnerTextAsync(), StringComparison.Ordinal);
        Assert.Equal(1, await confirm.Locator(DangerButton).CountAsync());
        Assert.Equal(0, await confirm.Locator("img, svg, [class*='icon']").CountAsync());
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
    /// A Project on Space home and its Board: three columns in the default order, the titles in the first column. Answers
    /// a copy of the seeded Tasks, since a deletion removes the fake's own entries.
    /// </summary>
    private static List<FakeTask> SeedBoard(FakeYelloApi api, Guid spaceId, Guid projectId, params string[] titles)
    {
        var name = spaceId == Northwind ? "Atlas" : "Beacon";
        var columns = StatusNames.Select(status => new FakeColumn(Guid.NewGuid(), status, 0, [])).ToList();

        foreach (var title in titles)
        {
            columns[0].Tasks.Add(new FakeTask(Guid.NewGuid(), title, columns[0].StatusId, $"a{columns[0].Tasks.Count:D5}", DateTimeOffset.UtcNow));
            columns[0].Total++;
        }

        api.Boards[(spaceId, projectId)] = new FakeBoard(new FakeBoardProject(projectId, name), columns);
        api.Projects[spaceId] = [new FakeProject(projectId, name, DateTimeOffset.UtcNow)];

        return [.. columns[0].Tasks];
    }

    /// <summary>
    /// A Project holding <paramref name="tasks"/> Tasks spread over its three columns, so its true count is a sum of
    /// totals rather than one column's.
    /// </summary>
    private static void SeedProjectHolding(FakeYelloApi api, Guid spaceId, Guid projectId, string name, int tasks)
    {
        var columns = StatusNames.Select(status => new FakeColumn(Guid.NewGuid(), status, 0, [])).ToList();

        for (var index = 0; index < tasks; index++)
        {
            var column = columns[index % columns.Count];

            column.Tasks.Add(new FakeTask(Guid.NewGuid(), $"Task {index}", column.StatusId, $"a{column.Tasks.Count:D5}", DateTimeOffset.UtcNow));
            column.Total++;
        }

        api.Boards[(spaceId, projectId)] = new FakeBoard(new FakeBoardProject(projectId, name), columns);
        api.Projects[spaceId] = [new FakeProject(projectId, name, DateTimeOffset.UtcNow)];
    }

    private static List<FakeRequest> Deletions(FakeYelloApi api) =>
        [.. api.Requests.Where(request => request.Method == "DELETE" && !request.Path.EndsWith("/sessions", StringComparison.Ordinal))];

    private static int ProjectListings(FakeYelloApi api) =>
        api.Requests.Count(request => request.Method == "GET" && request.Path.EndsWith("/projects", StringComparison.Ordinal));

    private static ILocator CardOf(IPage page, Guid taskId) =>
        page.Locator(Card, new PageLocatorOptions { Has = page.Locator($"button[data-task-id='{taskId:D}']") });

    private static ILocator ColumnNamed(IPage page, string status) =>
        page.Locator(Column, new PageLocatorOptions { Has = page.Locator("h2", new PageLocatorOptions { HasText = status }) });

    private static ILocator ProjectRow(IPage page, string name) =>
        page.Locator("main li", new PageLocatorOptions { HasText = name });

    private static ILocator DeleteTaskControl(IPage page) =>
        page.Locator(Dialog).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = DeleteTask, Exact = true });

    private static async Task OpenPanelAsync(IPage page, Guid taskId)
    {
        await page.Locator($"{Card} button[data-task-id='{taskId:D}']").ClickAsync(new LocatorClickOptions { Timeout = Timeout });
        await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    /// <summary>
    /// Chooses the panel's "Delete Task" and waits for the confirm to replace the panel's content.
    /// </summary>
    private static async Task AskToDeleteTaskAsync(IPage page)
    {
        await DeleteTaskControl(page).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
        await page.Locator(Dialog).Locator(Confirm).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    /// <summary>
    /// Chooses a Project row's "Delete Project" and waits for the confirm to replace the row's content.
    /// </summary>
    private static async Task AskToDeleteProjectAsync(IPage page, string name)
    {
        var row = ProjectRow(page, name);

        await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = DeleteProject }).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
        await row.Locator(Confirm).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
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

    private string Address(string path) => new Uri(surface.BaseAddress, path.TrimStart('/')).ToString();

    /// <summary>
    /// Installs the fake on the page once per case: a case that visits two surfaces reuses the routes it installed.
    /// </summary>
    private async Task InstallOnceAsync(FakeYelloApi api, IPage page)
    {
        if (!_installed)
        {
            _installed = true;
            await api.InstallAsync(page);
        }
    }

    private async Task OpenBoardAsync(FakeYelloApi api, IPage page, Guid spaceId, Guid projectId)
    {
        await InstallOnceAsync(api, page);
        await page.GotoAsync(Address($"spaces/{spaceId:D}/projects/{projectId:D}"));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.Locator(Card).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    private async Task OpenSpaceAsync(FakeYelloApi api, IPage page, Guid spaceId)
    {
        await InstallOnceAsync(api, page);
        await page.GotoAsync(Address($"spaces/{spaceId:D}"));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }
}
