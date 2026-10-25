using System.Text.Json;
using Microsoft.Playwright;
using Xunit;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// Story 2.4 in a real browser: Label chips on the card (three, then <c>+N</c>, wrapping before they scroll), the Label
/// picker in the Task detail panel (an optimistic Task edit, reverted per attribute), the Labels section of Space
/// settings (Owner and Admin only, deletion confirmed in place), and Labels as Space-scoped client state. AC1, AC2,
/// AC3, AC5, AC7, AC13, AC14; UX-DR5, UX-DR8, UX-DR11-13, UX-DR26-27, UX-DR29, UX-DR31-33, UX-DR36-37, UX-DR41. Written
/// red by the story 2.4 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>The API is <see cref="FakeYelloApi"/></b> with story 2.4's Label routes and the <c>labelIds</c> member on the
/// Task edit (Q1 (a)); <see cref="FakeYelloApi.HoldTaskEditAsync"/> holds the next edit so a case can assert what is on
/// screen while it is outstanding, as 2.3's cases do.
/// </para>
/// <para>
/// <b>The locators are assumptions</b>, to be corrected to the markup Task 10 builds and the correction recorded: a chip
/// is <c>.label-chip</c> inside the card's <c>.task-card__meta</c>; the overflow reads <c>+N</c> and its accessible name
/// is "{N} more Labels" (Q5); the panel's picker is a button named "Labels" opening a <c>[role=listbox]</c> with
/// <c>aria-multiselectable="true"</c> of <c>[role=option]</c> rows (Task 10); Space home links "Space settings" for an
/// Owner or Admin (Q7) at <c>/spaces/{id}/settings</c>; the define form has a "Label name" field, a "Colour" radio group
/// of eight named swatches (Q14) and an "Add Label" button; each listed Label has a "Delete Label" control opening a
/// <c>.destructive-confirm</c> in place whose <c>.button-danger</c> confirms (UX-DR12/36). Copy that exists is asserted
/// as written; copy the dev phase writes is asserted only as "its own, not Not available.".
/// </para>
/// <para>
/// <b>Story 2.2's <c>BoardTests.A_Task_card_is_its_title_alone_and_carries_no_shadow</c></b> says the card's text is
/// only its title; Task 10 amends it to "the title is the only text outside the metadata row" in the change that adds
/// the chips, and records it. The card keeps exactly one control (E2E-201 asserts it).
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-22")]
[Trait("Requirement", "FR-20")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class LabelTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string SwitcherTrigger = "nav.context-bar button[aria-controls]";

    private const string Card = ".task-card";
    private const string MetaRow = ".task-card__meta";
    private const string Chip = ".label-chip";
    private const string Dialog = "[role='dialog']";
    private const string Picker = "[role='listbox']";
    private const string Option = "[role='option']";
    private const string Disabled = "main [disabled], main [aria-disabled='true'], [role='dialog'] [disabled], [role='dialog'] [aria-disabled='true']";

    private const string LabelsControl = "Labels";
    private const string SpaceSettingsLink = "Space settings";
    private const string LabelNameField = "Label name";
    private const string ColourLegend = "Colour";
    private const string AddLabel = "Add Label";
    private const string DeleteLabel = "Delete Label";

    private const string ManageLabelsCopy = "Only Owners and Admins can manage Labels.";
    private const string ManageTasksCopy = "Viewers cannot edit Tasks.";
    private const string ReusedCopy = "That request was already used for something else. Try again.";
    private const string InFlightCopy = "That was already sent. Reload to see it.";
    private const string NotSentCopy = "Not sent.";
    private const string NotAvailableCopy = "Not available.";
    private const string NowAMemberCopy = "You're now a Member. Space settings is no longer available.";

    private const string InsufficientRoleType = "urn:yello:problem:insufficient-role";
    private const string IdempotencyKeyReusedType = "urn:yello:problem:idempotency-key-reused";
    private const string IdempotencyKeyInFlightType = "urn:yello:problem:idempotency-key-in-flight";

    private static readonly string[] StatusNames = ["Todo", "Doing", "Done"];

    private static readonly string[] ReusableFieldNames = [string.Empty, "NAME", "LABEL", "LABELNAME", "LABEL-NAME", "TITLE"];

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();
    private static readonly Guid Beacon = Guid.NewGuid();

    /// <summary>
    /// AC5, UX-DR8, Q5, Q12 - a Task carrying five Labels shows three chips, in the server's order, then a <c>+2</c> whose
    /// accessible name is "2 more Labels"; the card still has exactly one control (the opening button), so the overflow
    /// is text, not a second control.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC5 - no Label chips on the card yet (story 2.4 Task 10; Q5)")]
    [Trait("Requirement", "UX-DR8")]
    public async Task A_card_shows_three_chips_in_server_order_then_plus_N()
    {
        var api = TwoSpaces();
        var labels = SeedLabels(api, Northwind, "Alpha", "Bravo", "Charlie", "Delta", "Echo");
        var tasks = Seed(api, Northwind, Atlas, "Five Labels");

        ApplyLabels(api, tasks[0].Id, [.. labels.Select(label => label.Id)]);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);

            var card = CardOf(page, tasks[0].Id);
            var meta = card.Locator(MetaRow);

            await meta.Locator(Chip).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(["Alpha", "Bravo", "Charlie"], (await meta.Locator(Chip).AllInnerTextsAsync()).Select(text => text.Trim()));
            Assert.Contains("+2", await meta.InnerTextAsync(), StringComparison.Ordinal);
            Assert.True(
                await meta.EvaluateAsync<bool>("m => m.textContent.includes('2 more Labels') || !!m.querySelector(\"[aria-label='2 more Labels']\")"),
                "The +2 has no accessible name \"2 more Labels\".");
            Assert.Equal(1, await card.Locator("button, a, input, [role='button'], [tabindex]:not([tabindex='-1'])").CountAsync());
        }
    }

    /// <summary>
    /// AC5, AC13, UX-DR41, WCAG 1.4.10 - at a 320px viewport, a card carrying six long Labels has no horizontal overflow
    /// in its metadata row or in the card: the row wraps to more than one line instead. Anchored: the chips rendered.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC5 - no Label chips on the card yet (story 2.4 Task 10)")]
    [Trait("Requirement", "UX-DR41")]
    [Trait("Requirement", "NFR-9")]
    public async Task At_320px_a_card_with_six_long_Labels_wraps_and_never_scrolls_sideways()
    {
        var api = TwoSpaces();
        var labels = SeedLabels(api, Northwind, [.. Enumerable.Range(1, 6).Select(index => $"An unusually long Label name number {index}")]);
        var tasks = Seed(api, Northwind, Atlas, "Crowded");

        ApplyLabels(api, tasks[0].Id, [.. labels.Select(label => label.Id)]);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(320, 800);
            await OpenBoardAsync(api, page, Northwind, Atlas);

            var card = CardOf(page, tasks[0].Id);
            var meta = card.Locator(MetaRow);

            await meta.Locator(Chip).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.True(await meta.EvaluateAsync<bool>("m => m.scrollWidth <= m.clientWidth + 1"), "The metadata row overflows sideways.");
            Assert.True(await card.EvaluateAsync<bool>("c => c.scrollWidth <= c.clientWidth + 1"), "The card overflows sideways.");
            Assert.True(
                await meta.EvaluateAsync<bool>("m => new Set([...m.children].map(child => Math.round(child.getBoundingClientRect().top))).size > 1"),
                "The metadata row did not wrap.");
        }
    }

    /// <summary>
    /// AC7, UX-DR11 - the panel's Label picker is a multi-select listbox offering exactly the active Space's Labels: the
    /// applied ones selected, every other one not - nothing pre-selected that is not already applied.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC7 - no Label picker in the panel yet (story 2.4 Task 10)")]
    [Trait("Requirement", "UX-DR11")]
    public async Task The_picker_offers_exactly_the_Spaces_Labels_with_the_applied_ones_selected()
    {
        var api = TwoSpaces();
        var labels = SeedLabels(api, Northwind, "Alpha", "Bravo", "Charlie");

        _ = SeedLabels(api, Harbour, "Harbour only");

        var tasks = Seed(api, Northwind, Atlas, "Picked");

        ApplyLabels(api, tasks[0].Id, [labels[1].Id]);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);
            await OpenPickerAsync(page);

            var listbox = page.Locator(Dialog).Locator(Picker);
            var options = listbox.Locator(Option);

            Assert.Equal("true", await listbox.GetAttributeAsync("aria-multiselectable"));
            Assert.Equal(["Alpha", "Bravo", "Charlie"], (await options.AllInnerTextsAsync()).Select(text => text.Trim()));
            Assert.Equal(
                ["false", "true", "false"],
                await options.EvaluateAllAsync<string[]>("rows => rows.map(row => row.getAttribute('aria-selected') ?? 'false')"));
        }
    }

    /// <summary>
    /// AC7, AD-11, AD-18, Q1 (a) - choosing a Label renders its chip on the card and in the panel at once, while the edit
    /// is held; the request carries only <c>labelIds</c>, the whole new set, and an <c>Idempotency-Key</c>; once admitted
    /// the chip stays.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC7 - no Label picker or labelIds edit yet (story 2.4 Task 10; Q1)")]
    [Trait("Requirement", "AD-11")]
    [Trait("Requirement", "AD-18")]
    public async Task Applying_a_Label_renders_at_once_and_the_server_admits_it_afterwards()
    {
        var api = TwoSpaces();
        var labels = SeedLabels(api, Northwind, "Alpha", "Bravo");
        var tasks = Seed(api, Northwind, Atlas, "Applied");

        ApplyLabels(api, tasks[0].Id, [labels[0].Id]);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            var held = api.HoldTaskEditAsync();

            await ToggleAsync(page, "Bravo");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            Assert.Equal(["Alpha", "Bravo"], await ChipsOnCardAsync(page, tasks[0].Id));
            Assert.Contains("Bravo", await page.Locator(Dialog).Locator(Chip).AllInnerTextsAsync());

            var edit = Assert.Single(Edits(api));

            Assert.Equal(["labelIds"], Members(edit));
            Assert.Equal(new[] { labels[0].Id, labels[1].Id }.Order(), LabelIdsSent(edit).Order());
            Assert.False(string.IsNullOrEmpty(edit.Header("Idempotency-Key")));

            api.ReleaseTaskEdit();
            await WaitUntilAsync(() => api.FindTask(tasks[0].Id)?.Task.LabelIds?.Count == 2, "The edit was never applied.");

            Assert.Equal(["Alpha", "Bravo"], await ChipsOnCardAsync(page, tasks[0].Id));
        }
    }

    /// <summary>
    /// AC7, AC14, UX-DR32, UX-DR37 - a refused Labels edit reverts the Labels on the card and in the panel to the last
    /// admitted set and says why inline, never as a toast: <c>400 invalid-task-labels</c> with copy of its own,
    /// <c>403 manage-tasks</c>, and the two idempotency refusals with their existing sentences.
    /// </summary>
    /// <param name="status">The refusal's status.</param>
    /// <param name="problemType">Its type.</param>
    /// <param name="capability">A <c>403</c>'s capability.</param>
    /// <param name="copy">The sentence expected, or <c>null</c> for "its own copy, not Not available.".</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC7 - no Label picker or Labels revert yet (story 2.4 Task 10)")]
    [InlineData(400, "urn:yello:problem:invalid-task-labels", null, null)]
    [InlineData(403, InsufficientRoleType, "manage-tasks", ManageTasksCopy)]
    [InlineData(409, IdempotencyKeyInFlightType, null, InFlightCopy)]
    [InlineData(422, IdempotencyKeyReusedType, null, ReusedCopy)]
    [Trait("Requirement", "AD-11")]
    [Trait("Requirement", "UX-DR32")]
    public async Task A_refused_Labels_edit_is_reverted_and_its_reason_is_said_inline(int status, string problemType, string? capability, string? copy)
    {
        var api = TwoSpaces();
        var labels = SeedLabels(api, Northwind, "Alpha", "Bravo");
        var tasks = Seed(api, Northwind, Atlas, "Refused");

        ApplyLabels(api, tasks[0].Id, [labels[0].Id]);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            var held = api.HoldTaskEditAsync();

            await ToggleAsync(page, "Bravo");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            Assert.Equal(["Alpha", "Bravo"], await ChipsOnCardAsync(page, tasks[0].Id));

            api.ReleaseTaskEditRefused(status, problemType, capability);

            await WaitUntilAsync(async () => (await ChipsOnCardAsync(page, tasks[0].Id)).SequenceEqual(["Alpha"]), "The card's Labels were not reverted.");

            var panel = await page.Locator(Dialog).InnerTextAsync();

            Assert.DoesNotContain("Bravo", string.Join('|', await page.Locator(Dialog).Locator(Chip).AllInnerTextsAsync()), StringComparison.Ordinal);

            if (copy is null)
            {
                Assert.DoesNotContain(NotAvailableCopy, panel, StringComparison.Ordinal);
                Assert.NotEqual(0, await page.Locator(Dialog).Locator("[role='status']:not(:empty), [role='alert']:not(:empty)").CountAsync());
            }
            else
            {
                Assert.Contains(copy, panel, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// AC7, Q8 of 2.3 - a Labels edit that gets no answer (the connection reset) is reverted and said to be "Not sent.";
    /// a <c>404</c> instead reverts and hands over to the page's refusal surface, the panel gone.
    /// </summary>
    /// <param name="notFound">Whether the edit is answered <c>404</c> (otherwise not at all).</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC7 - no Label picker or Labels revert yet (story 2.4 Task 10)")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "AD-11")]
    public async Task An_unanswered_or_not_found_Labels_edit_is_reverted(bool notFound)
    {
        var api = TwoSpaces();
        var labels = SeedLabels(api, Northwind, "Alpha");
        var tasks = Seed(api, Northwind, Atlas, "Lost");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            var held = api.HoldTaskEditAsync();

            await ToggleAsync(page, labels[0].Name);
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            if (notFound)
            {
                api.ReleaseTaskEditRefused(404, "urn:yello:problem:not-available");
                await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });
                await page.GetByText(NotAvailableCopy).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            }
            else
            {
                api.ReleaseTaskEditUnanswered();
                await page.Locator(Dialog).GetByText(NotSentCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

                Assert.Empty(await ChipsOnCardAsync(page, tasks[0].Id));
            }
        }
    }

    /// <summary>
    /// AC7, Q8 of 2.3 (per-attribute revert) - a Labels refusal reverts only the Labels: a title change admitted while
    /// the Labels edit was held stays on the card and in the panel.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC7 - no TaskAttribute.Labels in the replica yet (story 2.4 Task 10)")]
    [Trait("Requirement", "AD-11")]
    public async Task A_Labels_refusal_reverts_only_the_Labels()
    {
        var api = TwoSpaces();
        var labels = SeedLabels(api, Northwind, "Alpha");
        var tasks = Seed(api, Northwind, Atlas, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            var held = api.HoldTaskEditAsync();

            await ToggleAsync(page, labels[0].Name);
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            var title = page.Locator(Dialog).GetByLabel("Title", new LocatorGetByLabelOptions { Exact = true });

            await title.FillAsync("Send the brief");
            await title.PressAsync("Enter");
            await WaitUntilAsync(() => api.FindTask(tasks[0].Id)?.Task.Title == "Send the brief", "The title edit was never admitted.");

            api.ReleaseTaskEditRefused(400, "urn:yello:problem:invalid-task-labels");

            await WaitUntilAsync(async () => (await ChipsOnCardAsync(page, tasks[0].Id)).Count == 0, "The Labels were not reverted.");

            Assert.Contains("Send the brief", await CardOf(page, tasks[0].Id).InnerTextAsync(), StringComparison.Ordinal);
            Assert.Equal("Send the brief", await title.InputValueAsync());
        }
    }

    /// <summary>
    /// AC1, AC14, UX-DR27, UX-DR32, Q7 - a Member's Space home has no "Space settings" link (absent, not disabled), and the
    /// settings address reached by hand shows the within-Space capability copy and no define form. Anchor: the Owner's
    /// Space home has the link.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1 - no Space settings page or link yet (story 2.4 Task 10; Q7)")]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "UX-DR32")]
    public async Task A_Member_has_no_Label_definition_affordance()
    {
        var api = TwoSpaces();

        api.Roles[Harbour] = "member";
        _ = Seed(api, Northwind, Atlas, "Owned");
        _ = Seed(api, Harbour, Beacon, "Membered");

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceHomeAsync(api, page, Northwind);
            await page.Locator("main").GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = SpaceSettingsLink }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await page.EvaluateAsync("path => Blazor.navigateTo(path)", ClientSpaceHome(Harbour));
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Member" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.Locator("main").GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Beacon" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator("main").GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = SpaceSettingsLink }).CountAsync());

            await page.EvaluateAsync("path => Blazor.navigateTo(path)", ClientSettings(Harbour));
            await page.Locator("main").GetByText(ManageLabelsCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator("main").GetByLabel(LabelNameField).CountAsync());
            Assert.Equal(0, await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = AddLabel }).CountAsync());
            Assert.Equal(0, await page.Locator(Disabled).CountAsync());
        }
    }

    /// <summary>
    /// AC2, UX-DR27 - a Viewer's panel shows the Task's Labels as text and offers no picker and nothing disabled. Anchor:
    /// the Owner's panel on the other Space's Board has the "Labels" control.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2 - no Labels field in the panel yet (story 2.4 Task 10)")]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "AD-1")]
    public async Task A_Viewer_sees_the_Labels_as_text_and_no_picker()
    {
        var api = TwoSpaces();
        var owned = Seed(api, Northwind, Atlas, "Owned card");
        var viewedLabels = SeedLabels(api, Harbour, "Read only");
        var viewed = Seed(api, Harbour, Beacon, "Viewed card");

        ApplyLabels(api, viewed[0].Id, [viewedLabels[0].Id]);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, owned[0].Id);

            Assert.Equal(1, await LabelsButton(page).CountAsync());

            await page.EvaluateAsync("path => Blazor.navigateTo(path)", ClientBoard(Harbour, Beacon));
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await OpenPanelAsync(page, viewed[0].Id);

            var dialog = page.Locator(Dialog);

            Assert.Contains("Read only", await dialog.InnerTextAsync(), StringComparison.Ordinal);
            Assert.Equal(0, await LabelsButton(page).CountAsync());
            Assert.Equal(0, await dialog.Locator(Picker).CountAsync());
            Assert.Equal(0, await page.Locator(Disabled).CountAsync());
        }
    }

    /// <summary>
    /// AC14, UX-DR31 - leaving the Space purges its Labels before any render: switching Spaces from the Labels surface, or
    /// from an open panel on the Board, leaves no previous Label name anywhere in the DOM.
    /// </summary>
    /// <param name="fromSettings">Whether the switch is made from Space settings (otherwise from the open panel).</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC14 - no Label list state to purge yet (story 2.4 Task 10)")]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "UX-DR31")]
    public async Task Leaving_the_Space_purges_its_Labels(bool fromSettings)
    {
        var api = TwoSpaces();
        var labels = SeedLabels(api, Northwind, "A Label that belongs to Northwind");
        var tasks = Seed(api, Northwind, Atlas, "Carrier");

        _ = Seed(api, Harbour, Beacon, "Elsewhere");
        ApplyLabels(api, tasks[0].Id, [labels[0].Id]);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            if (fromSettings)
            {
                await OpenSettingsAsync(api, page, Northwind);
            }
            else
            {
                await OpenBoardAsync(api, page, Northwind, Atlas);
                await OpenPanelAsync(page, tasks[0].Id);
            }

            await page.GetByText(labels[0].Name).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await ChooseInSwitcherAsync(page, "Harbour Ops");
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.DoesNotContain(labels[0].Name, await page.EvaluateAsync<string>("() => document.body.innerHTML"), StringComparison.Ordinal);
            Assert.Equal("Yello", await page.TitleAsync());
        }
    }

    /// <summary>
    /// AC3, UX-DR12, UX-DR26, UX-DR36, Q11 - deleting a Label opens a destructive confirm in place (never a second dialog
    /// stacked on one), naming the Label; deletion is not optimistic; a Label still in use is refused
    /// <c>409 label-in-use</c>, stays listed, and the surface says why with copy of its own. Anchor: an unused Label
    /// deletes and leaves the list.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 - no Labels surface or delete confirm yet (story 2.4 Task 10; Q11)")]
    [Trait("Requirement", "UX-DR36")]
    [Trait("Requirement", "UX-DR26")]
    public async Task Deleting_a_Label_confirms_in_place_and_an_applied_one_is_kept_with_a_reason()
    {
        var api = TwoSpaces();
        var labels = SeedLabels(api, Northwind, "In use", "Unused");
        var tasks = Seed(api, Northwind, Atlas, "Carrier");

        ApplyLabels(api, tasks[0].Id, [labels[0].Id]);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            await ConfirmDeletionAsync(page, "Unused");
            await WaitUntilAsync(async () => await RowOf(page, "Unused").CountAsync() == 0, "The unused Label was not removed after its deletion.");

            await RowOf(page, "In use").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = DeleteLabel }).ClickAsync();

            var confirm = page.Locator(".destructive-confirm");

            await confirm.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Contains("In use", await confirm.InnerTextAsync(), StringComparison.Ordinal);
            Assert.True(await page.Locator(Dialog).CountAsync() <= 1, "The confirm stacked a second dialog.");

            await confirm.Locator(".button-danger").ClickAsync();
            await WaitUntilAsync(() => api.Requests.Count(request => request.Method == "DELETE" && request.Path.Contains("/labels/", StringComparison.Ordinal)) == 2, "The second deletion was never sent.");

            await RowOf(page, "In use").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var main = await page.Locator("main").InnerTextAsync();

            Assert.DoesNotContain(NotAvailableCopy, main, StringComparison.Ordinal);
            Assert.Contains(api.Labels[Northwind], label => label.Name == "In use");
        }
    }

    /// <summary>
    /// AC1, AC4, AC14, UX-DR33, Q8, Q13, Q14 - the define form: the name input is <c>autocomplete="off"</c> with a field
    /// name autofill would not reuse; the colour is a radio group of exactly the eight palette entries, each named, one
    /// chosen (Q8), with no free colour input; "Add Label" posts exactly <c>{name, colour}</c> and waits for the server -
    /// the Label is listed only once the held definition is answered.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1 - no Space settings page or define form yet (story 2.4 Task 10; Q7, Q8, Q14)")]
    [Trait("Requirement", "UX-DR13")]
    [Trait("Requirement", "UX-DR33")]
    public async Task The_define_form_offers_the_palette_and_waits_for_the_server()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            var name = page.Locator("main").GetByLabel(LabelNameField, new LocatorGetByLabelOptions { Exact = true });
            var colours = page.Locator("main").GetByRole(AriaRole.Group, new LocatorGetByRoleOptions { Name = ColourLegend }).GetByRole(AriaRole.Radio);

            Assert.Equal("off", await name.GetAttributeAsync("autocomplete"));
            Assert.DoesNotContain((await name.GetAttributeAsync("name") ?? string.Empty).ToUpperInvariant(), ReusableFieldNames);
            Assert.Equal(LabelScaffold.PaletteKeys.Count, await colours.CountAsync());
            Assert.All(await colours.EvaluateAllAsync<string[]>("radios => radios.map(radio => radio.labels.length ? radio.labels[0].textContent.trim() : '')"), label => Assert.False(string.IsNullOrEmpty(label)));
            Assert.Equal(1, await colours.EvaluateAllAsync<int>("radios => radios.filter(radio => radio.checked).length"));
            Assert.Equal(0, await page.Locator("main input[type='color']").CountAsync());

            var held = api.HoldLabelCreateAsync();

            await name.FillAsync("Needs design");
            await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = AddLabel, Exact = true }).ClickAsync();
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            Assert.Equal(0, await RowOf(page, "Needs design").CountAsync());

            var posted = Assert.Single(api.Requests, request => request.Method == "POST" && request.Path == LabelScaffold.LabelsOf(Northwind));

            using (var document = JsonDocument.Parse(posted.Body ?? "{}"))
            {
                Assert.Equal(["colour", "name"], document.RootElement.EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal));
                Assert.Contains(document.RootElement.GetProperty("colour").GetString(), LabelScaffold.PaletteKeys);
            }

            api.ReleaseLabelCreate();

            await RowOf(page, "Needs design").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        }
    }

    /// <summary>
    /// AC14, UX-DR29, UX-DR37 - a Role re-read lower (Owner to Member) while on Space settings is narrated, then routed to
    /// Space home: the narration names what happened, and the define form is gone.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC14 - no Space settings page yet (story 2.4 Task 10; Q7)")]
    [Trait("Requirement", "UX-DR29")]
    [Trait("Requirement", "UX-DR37")]
    public async Task A_Role_reread_to_Member_on_Space_settings_is_narrated_then_routed_to_Space_home()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            api.Roles[Northwind] = "member";

            await page.EvaluateAsync("path => Blazor.navigateTo(path)", ClientSettings(Northwind));
            await page.GetByText(NowAMemberCopy).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await WaitForPathAsync(page, ClientSpaceHome(Northwind));

            Assert.Equal(0, await page.Locator("main").GetByLabel(LabelNameField).CountAsync());
        }
    }

    /// <summary>
    /// AC14, UX-DR27, UX-DR37 - a Role re-read to Viewer with the panel open removes the Label picker in place; the panel
    /// stays and the Task's Labels stay as text.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC14 - no Label picker yet (story 2.4 Task 10)")]
    [Trait("Requirement", "UX-DR37")]
    public async Task A_Role_reread_to_Viewer_removes_the_picker_in_place()
    {
        var api = TwoSpaces();
        var labels = SeedLabels(api, Northwind, "Stays visible");
        var tasks = Seed(api, Northwind, Atlas, "Demoted view");

        ApplyLabels(api, tasks[0].Id, [labels[0].Id]);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            Assert.Equal(1, await LabelsButton(page).CountAsync());

            api.Roles[Northwind] = "viewer";

            await page.EvaluateAsync("path => Blazor.navigateTo(path)", ClientBoard(Northwind, Atlas));
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await LabelsButton(page).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.Equal(1, await page.Locator(Dialog).CountAsync());
            Assert.Contains("Stays visible", await page.Locator(Dialog).InnerTextAsync(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC14, UX-DR33 - after a Label is defined and applied, no Label name reaches <c>document.title</c>, the address, or
    /// browser storage. Anchor: the Label is on screen.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC14 - no Labels surface or picker yet (story 2.4 Task 10)")]
    [Trait("Requirement", "UX-DR33")]
    [Trait("Priority", "P1")]
    public async Task No_Label_name_reaches_the_tab_the_address_or_storage()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "Stored nowhere");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            await page.Locator("main").GetByLabel(LabelNameField, new LocatorGetByLabelOptions { Exact = true }).FillAsync("Confidential tag");
            await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = AddLabel, Exact = true }).ClickAsync();
            await RowOf(page, "Confidential tag").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await page.EvaluateAsync("path => Blazor.navigateTo(path)", ClientBoard(Northwind, Atlas));
            await OpenPanelAsync(page, tasks[0].Id);
            await ToggleAsync(page, "Confidential tag");
            await WaitUntilAsync(() => api.FindTask(tasks[0].Id)?.Task.LabelIds?.Count == 1, "The Label was never applied.");

            Assert.Equal("Yello", await page.TitleAsync());
            Assert.DoesNotContain("Confidential", await page.EvaluateAsync<string>("() => location.href"), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "Confidential",
                await page.EvaluateAsync<string>("() => JSON.stringify(Object.assign({}, localStorage)) + JSON.stringify(Object.assign({}, sessionStorage))"),
                StringComparison.OrdinalIgnoreCase);
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
            columns[0].Tasks.Add(new FakeTask(Guid.NewGuid(), title, columns[0].StatusId, $"a{columns[0].Tasks.Count:D5}", DateTimeOffset.UtcNow, LabelIds: []));
            columns[0].Total++;
        }

        var projectName = spaceId == Northwind ? "Atlas" : "Beacon";

        api.Boards[(spaceId, projectId)] = new FakeBoard(new FakeBoardProject(projectId, projectName), columns);
        api.Projects[spaceId] = [new FakeProject(projectId, projectName, DateTimeOffset.UtcNow)];

        return [.. columns[0].Tasks];
    }

    /// <summary>
    /// The Space's Labels, defined in the fake in the order given; answered by name then id.
    /// </summary>
    private static List<FakeLabel> SeedLabels(FakeYelloApi api, Guid spaceId, params string[] names)
    {
        var labels = names.Select((name, index) => new FakeLabel(Guid.NewGuid(), name, LabelScaffold.PaletteKeys[index % LabelScaffold.PaletteKeys.Count])).ToList();

        api.Labels[spaceId] = labels;

        return [.. labels];
    }

    /// <summary>
    /// Applies Labels to a seeded Task in the fake, in the server's order (name, then id).
    /// </summary>
    private static void ApplyLabels(FakeYelloApi api, Guid taskId, Guid[] labelIds)
    {
        var (column, task) = api.FindTask(taskId)!.Value;
        var ordered = api.Labels.Values.SelectMany(labels => labels)
            .Where(label => labelIds.Contains(label.Id))
            .OrderBy(label => label.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(label => label.Id)
            .Select(label => label.Id)
            .ToList();

        column.Tasks[column.Tasks.FindIndex(candidate => candidate.Id == taskId)] = task with { LabelIds = ordered };
    }

    private static ILocator CardOf(IPage page, Guid taskId) =>
        page.Locator(Card, new PageLocatorOptions { Has = page.Locator($"button[data-task-id='{taskId:D}']") });

    private static ILocator LabelsButton(IPage page) =>
        page.Locator(Dialog).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = LabelsControl, Exact = true });

    private static ILocator RowOf(IPage page, string labelName) =>
        page.Locator("main li", new PageLocatorOptions { HasText = labelName });

    private static async Task<List<string>> ChipsOnCardAsync(IPage page, Guid taskId) =>
        [.. (await CardOf(page, taskId).Locator($"{MetaRow} {Chip}").AllInnerTextsAsync()).Select(text => text.Trim())];

    private static async Task OpenPanelAsync(IPage page, Guid taskId)
    {
        await page.Locator($"{Card} button[data-task-id='{taskId:D}']").ClickAsync();
        await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    private static async Task OpenPickerAsync(IPage page)
    {
        await LabelsButton(page).ClickAsync();
        await page.Locator(Dialog).Locator(Picker).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    /// <summary>
    /// Opens the picker if it is closed and toggles one Label (Q1 (a): the whole new set is committed).
    /// </summary>
    private static async Task ToggleAsync(IPage page, string labelName)
    {
        if (await page.Locator(Dialog).Locator(Picker).CountAsync() == 0)
        {
            await OpenPickerAsync(page);
        }

        await page.Locator(Dialog).Locator($"{Picker} {Option}", new LocatorLocatorOptions { HasText = labelName }).ClickAsync();
    }

    private static async Task ConfirmDeletionAsync(IPage page, string labelName)
    {
        await RowOf(page, labelName).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = DeleteLabel }).ClickAsync();

        var confirm = page.Locator(".destructive-confirm");

        await confirm.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await confirm.Locator(".button-danger").ClickAsync();
    }

    private static List<FakeRequest> Edits(FakeYelloApi api) =>
        [.. api.Requests.Where(request => request.Method == "PATCH" && request.Path.Contains("/tasks/", StringComparison.Ordinal))];

    private static string[] Members(FakeRequest edit)
    {
        using var document = JsonDocument.Parse(edit.Body ?? "{}");

        return [.. document.RootElement.EnumerateObject().Select(member => member.Name)];
    }

    private static List<Guid> LabelIdsSent(FakeRequest edit)
    {
        using var document = JsonDocument.Parse(edit.Body ?? "{}");

        return [.. document.RootElement.GetProperty("labelIds").EnumerateArray().Select(id => id.GetGuid())];
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
    /// Polls an asynchronous condition (a DOM read) with a deadline, never a sleep.
    /// </summary>
    private static async Task WaitUntilAsync(Func<Task<bool>> condition, string because)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(Timeout);

        while (!await condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, because);
        }
    }

    /// <summary>
    /// Polls the page's own <c>location.pathname</c> - never Playwright's URL wait (TESTING-CONVENTIONS).
    /// </summary>
    private static async Task WaitForPathAsync(IPage page, string path)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(Timeout);

        while (await page.EvaluateAsync<string>("() => location.pathname") != path)
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, $"The page never reached {path}.");
        }
    }

    private static string ClientSpaceHome(Guid spaceId) => $"/spaces/{spaceId:D}";

    private static string ClientBoard(Guid spaceId, Guid projectId) => $"/spaces/{spaceId:D}/projects/{projectId:D}";

    /// <summary>
    /// Q7 (a): Space settings, ids only.
    /// </summary>
    private static string ClientSettings(Guid spaceId) => $"/spaces/{spaceId:D}/settings";

    private string Address(string path) => new Uri(surface.BaseAddress, path.TrimStart('/')).ToString();

    private async Task OpenBoardAsync(FakeYelloApi api, IPage page, Guid spaceId, Guid projectId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address(ClientBoard(spaceId, projectId)));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.Locator(Card).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    private async Task OpenSpaceHomeAsync(FakeYelloApi api, IPage page, Guid spaceId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address(ClientSpaceHome(spaceId)));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    private async Task OpenSettingsAsync(FakeYelloApi api, IPage page, Guid spaceId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address(ClientSettings(spaceId)));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.Locator("main").GetByLabel(LabelNameField, new LocatorGetByLabelOptions { Exact = true }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
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
