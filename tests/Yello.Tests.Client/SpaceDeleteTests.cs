using Microsoft.Playwright;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Spaces;

namespace Yello.Tests.Client;

/// <summary>
/// Story 3.3 in a real browser: deleting a Space from Space settings, and the client's states that a deletion makes
/// reachable - the route afterwards, the switcher with nothing to list, and the refusal page at <c>/</c>. AC1-AC3, AC6-AC8,
/// AC12, AC14-AC16; FR-7, §6.2, UX-DR12, UX-DR27, UX-DR32, UX-DR33, UX-DR35-UX-DR38, UX-DR40, UX-DR41, NFR-9.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 3.3 ATDD run (2026-10-31).</b> The API is <see cref="FakeYelloApi"/> at
/// the network edge, taught by this run the deletion (held and released like 2.5's), Q2 (a)'s counts read and a failing
/// switcher (<c>FakeYelloApi.SpaceDeletion.cs</c>); no Host, no database, no engine. One case is green today and stays
/// active as a standing gate: <see cref="An_Admin_has_no_delete_affordance_at_all"/> (an Admin has never had one); its
/// plant - the section rendered for an Admin - is the dev phase's.
/// </para>
/// <para>
/// <b>Locators are by role, label and the copy the story recommends</b>, so they are assumptions to correct to the markup
/// Task 6 builds, with each correction recorded in the Debug Log: the section's opener and the confirm's danger button are
/// both named "Delete this Space"; the confirm is a <c>.destructive-confirm</c> in <c>main</c> (2.5's class) whose first
/// heading is "Delete {name}?" and whose next text is "This cannot be undone." (Q5's order); the counts read "It holds {p}
/// Projects and {t} Tasks." and "{n} other people will lose access to it." / "No one else has access to it." (Q5, with
/// singular forms as 2.5's <c>ProjectHoldsTasks</c>); the field's label is "Type the Space name to confirm" and its mismatch
/// message "Type the Space name exactly as shown." (Q5); the announcement is "Space deleted." (Q6 (a)); the switcher's
/// sentences are "You do not belong to any Space yet." and "Your Spaces could not be listed." (Q7 (a)); the refusal page
/// at <c>/</c> says "Choose a Space from the switcher above." (Q8 (a)). Copy that exists is asserted as written ("Not
/// sent.", "Only the Owner can delete the Space.", "No Spaces yet", "Not available.").
/// </para>
/// <para>
/// <b>Focus after a deletion</b> is asserted on the destination page's <c>h1</c>, as AC12 states it. If the dev phase
/// finds the route home counts as a switch (3.2: focus to the context bar), that is a contradiction to record, not a
/// locator to loosen.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-7")]
[Trait("Requirement", "UX-DR37")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class SpaceDeleteTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string ContextBar = "nav.context-bar";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string SwitcherTrigger = "nav.context-bar button[aria-controls]";
    private const string Confirm = "main .destructive-confirm";
    private const string DangerButton = ".button-danger";
    private const string DisabledAnywhere = "main [disabled], main [aria-disabled='true'], nav.context-bar [disabled]";
    private const string SecondLayer = "dialog, [role='dialog'], [role='alertdialog'], [aria-modal='true'], .scrim";

    private const string SettingsLink = "Space settings";
    private const string RenameAction = "Rename";
    private const string LabelsHeading = "Labels";
    private const string DeleteSpace = "Delete this Space";
    private const string Cancel = "Cancel";
    private const string TypedNameLabel = "Type the Space name to confirm";
    private const string MismatchCopy = "Type the Space name exactly as shown.";
    private const string CannotBeUndone = "This cannot be undone.";
    private const string NoOneElse = "No one else has access to it.";
    private const string SpaceDeletedCopy = "Space deleted.";
    private const string NotSentCopy = "Not sent.";
    private const string OwnerOnlyCopy = "Only the Owner can delete the Space.";
    private const string NoSpaceHeading = "No Spaces yet";
    private const string CreateAction = "Create a Space";
    private const string NoSpaceInSwitcher = "You do not belong to any Space yet.";
    private const string SwitcherFailed = "Your Spaces could not be listed.";
    private const string ChooseFromSwitcher = "Choose a Space from the switcher above.";
    private const string NotAvailableCopy = "Not available.";
    private const string BackToYello = "Back to Yello.";

    private const string NorthwindName = "Northwind Redesign";
    private const string HarbourName = "Harbour Ops";

    private const string InsufficientRoleType = "urn:yello:problem:insufficient-role";

    private static readonly string[] ReusableFieldNames = [string.Empty, "NAME", "SPACE", "SPACENAME", "SPACE-NAME", "SPACE_NAME", "TITLE", "CONFIRM"];

    /// <summary>
    /// Records every non-empty text any polite region or status region changes to, anywhere in the document, from now on:
    /// <c>window.__said</c>. Regions that appear later (the destination's) are seen too, because the observer watches the
    /// body; the text already in a region when this runs is not recorded.
    /// </summary>
    private const string RecordEverythingSaid =
        """
        () => {
            window.__said = [];
            const last = new WeakMap();
            const regions = () => document.querySelectorAll('[aria-live="polite"], [role="status"]');
            for (const region of regions()) last.set(region, region.textContent.trim());
            new MutationObserver(() => {
                for (const region of regions()) {
                    const text = region.textContent.trim();
                    if (text && last.get(region) !== text) window.__said.push(text);
                    last.set(region, text);
                }
            }).observe(document.body, { childList: true, subtree: true, characterData: true });
        }
        """;

    /// <summary>
    /// A 1.4.10 reflow probe, as <c>SpaceCreateAndRenameTests.Reflow</c>.
    /// </summary>
    private const string Reflow =
        """
        () => {
            const problems = [];
            const page = document.scrollingElement;
            if (page.scrollWidth > page.clientWidth + 1) problems.push('the page scrolls sideways by ' + (page.scrollWidth - page.clientWidth) + 'px');
            for (const element of document.querySelectorAll('main *, nav.context-bar, nav.context-bar *')) {
                const style = getComputedStyle(element);
                if (style.display === 'none') continue;
                if (['auto', 'scroll'].includes(style.overflowX) && element.scrollWidth > element.clientWidth + 1) {
                    problems.push('scrolls sideways: ' + element.tagName + '.' + element.className);
                }
            }
            return problems;
        }
        """;

    /// <summary>
    /// Whether the focused element shows a ring that is not inset (<c>base.css :focus-visible</c> is outline-based).
    /// </summary>
    private const string FocusedHasVisibleRing =
        """
        () => {
            const element = document.activeElement;
            if (!element || element === document.body) return false;
            const style = getComputedStyle(element);
            return style.outlineStyle !== 'none' && parseFloat(style.outlineWidth) > 0 && parseFloat(style.outlineOffset) >= 0;
        }
        """;

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();

    /// <summary>
    /// AC1, UX-DR27 - the Owner's Space settings carries the delete section: one "Delete this Space" control in
    /// <c>main</c>, after the name and Label sections, and nothing in <c>main</c> is disabled.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1 - Space settings has no delete section yet (story 3.3 Task 6)")]
    [Trait("Requirement", "UX-DR27")]
    public async Task The_Owner_is_offered_Delete_this_Space_in_Space_settings()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            var opener = Opener(page);

            await opener.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(1, await opener.CountAsync());
            Assert.Equal(0, await page.Locator(DisabledAnywhere).CountAsync());
            Assert.Empty(api.SpaceDeletions(Northwind));
        }
    }

    /// <summary>
    /// AC1, UX-DR27 - an Admin has no delete affordance at all: no "Delete this Space" control or text in the DOM - absent,
    /// not hidden, not disabled. Anchored: the same Admin is offered "Rename" on the same page. <b>Green today, and a
    /// standing gate</b>: no Role has had the affordance yet. Its plant (render the section for an Admin) is the dev phase's,
    /// once the section exists.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    public async Task An_Admin_has_no_delete_affordance_at_all()
    {
        var api = TwoSpaces();
        api.Roles[Northwind] = "admin";

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            Assert.Equal(1, await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = RenameAction, Exact = true }).CountAsync());
            Assert.Equal(0, await page.Locator("main").GetByText(DeleteSpace).CountAsync());
            Assert.Equal(0, await page.Locator(Confirm).CountAsync());
            Assert.Equal(0, await page.Locator(DisabledAnywhere).CountAsync());
            Assert.Equal(0, api.DeletionSummaryReads(Northwind));
        }
    }

    /// <summary>
    /// AC7, EXPERIENCE.md:67, DESIGN.md:519 - the confirm replaces the settings panel's content in place: while it is open
    /// there is no rename control and no Label section in the DOM, and no second layer (no <c>dialog</c>, no modal role, no
    /// scrim). Cancel restores both sections and returns focus to "Delete this Space"; nothing was sent.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC7 - the delete confirm does not exist yet (story 3.3 Task 6)")]
    [Trait("Requirement", "UX-DR12")]
    public async Task The_confirm_replaces_the_settings_content_in_place_and_Cancel_restores_it()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            // The anchor: both sections are there before.
            Assert.Equal(1, await RenameButton(page).CountAsync());
            Assert.Equal(1, await LabelsSection(page).CountAsync());

            var confirm = await OpenConfirmAsync(page);

            Assert.Equal(0, await RenameButton(page).CountAsync());
            Assert.Equal(0, await LabelsSection(page).CountAsync());
            Assert.Equal(0, await page.Locator(SecondLayer).CountAsync());

            await confirm.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = Cancel, Exact = true }).ClickAsync();
            await confirm.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.Equal(1, await RenameButton(page).CountAsync());
            Assert.Equal(1, await LabelsSection(page).CountAsync());
            Assert.True(await Opener(page).EvaluateAsync<bool>("e => e === document.activeElement"), "Focus did not return to Delete this Space.");
            Assert.Empty(api.SpaceDeletions(Northwind));
        }
    }

    /// <summary>
    /// AC16, UX-DR12, Q5 - opening the confirm moves focus to the typed-name field; Esc restores the panel's content and
    /// returns focus to "Delete this Space", sending nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC16 - the delete confirm does not exist yet (story 3.3 Task 6; Q5 focus)")]
    [Trait("Requirement", "NFR-9")]
    [Trait("Requirement", "UX-DR12")]
    public async Task Escape_closes_the_confirm_and_returns_focus_to_Delete_this_Space()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            var confirm = await OpenConfirmAsync(page);
            var field = TypedName(page);

            await page.WaitForFunctionAsync("() => document.activeElement && document.activeElement.tagName === 'INPUT'", null, new PageWaitForFunctionOptions { Timeout = Timeout });
            Assert.True(await field.EvaluateAsync<bool>("e => e === document.activeElement"), "Opening the confirm did not focus the typed-name field.");

            await page.Keyboard.PressAsync("Escape");
            await confirm.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.Equal(1, await RenameButton(page).CountAsync());
            Assert.True(await Opener(page).EvaluateAsync<bool>("e => e === document.activeElement"), "Focus did not return to Delete this Space.");
            Assert.Empty(api.SpaceDeletions(Northwind));
        }
    }

    /// <summary>
    /// AC2, AC8, §6.2, UX-DR36, UX-DR37, Q2 (a), Q5 - the confirm's heading names the Space ("Delete Northwind Redesign?");
    /// the very next text is "This cannot be undone.", with nothing before it; then the counts as served and the
    /// other-Accounts sentence. The counts are read when the confirm opens (never cached): a second opening reads them again.
    /// </summary>
    /// <param name="projects">The Projects served.</param>
    /// <param name="tasks">The Tasks served.</param>
    /// <param name="others">The other Members served.</param>
    /// <param name="counts">The counts sentence.</param>
    /// <param name="othersSentence">The other-Accounts sentence.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC2, AC8 - the delete confirm and its counts read do not exist yet (story 3.3 Tasks 4, 6; Q2 (a), Q5)")]
    [InlineData(2, 5, 2, "It holds 2 Projects and 5 Tasks.", "2 other people will lose access to it.")]
    [InlineData(1, 1, 0, "It holds 1 Project and 1 Task.", NoOneElse)]
    [Trait("Requirement", "UX-DR36")]
    public async Task The_confirm_names_the_Space_says_it_cannot_be_undone_first_and_states_the_counts_read_when_it_opens(
        int projects,
        int tasks,
        int others,
        string counts,
        string othersSentence)
    {
        var api = TwoSpaces();
        api.DeletionSummaries[Northwind] = (projects, tasks, others);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            Assert.Equal(0, api.DeletionSummaryReads(Northwind));

            var confirm = await OpenConfirmAsync(page);

            await confirm.GetByText(counts).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var lines = (await confirm.InnerTextAsync())
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToList();

            Assert.Equal("Delete Northwind Redesign?", lines[0]);
            Assert.Equal(CannotBeUndone, lines[1]);
            Assert.Contains(counts, lines);
            Assert.Contains(othersSentence, lines);
            Assert.Equal(1, await confirm.GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = "Delete Northwind Redesign?", Exact = true }).CountAsync());
            Assert.Equal(1, await confirm.Locator(DangerButton).CountAsync());
            Assert.Equal(1, api.DeletionSummaryReads(Northwind));

            await confirm.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = Cancel, Exact = true }).ClickAsync();
            await OpenConfirmAsync(page);

            Assert.Equal(2, api.DeletionSummaryReads(Northwind));
            Assert.Empty(api.SpaceDeletions(Northwind));
        }
    }

    /// <summary>
    /// AC3, UX-DR33 - the typed-name field has a real label, is <c>autocomplete="off"</c>, carries a field name no browser
    /// would match to a saved value, and that name is fresh per opening.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 - the delete confirm's typed-name field does not exist yet (story 3.3 Task 6)")]
    [Trait("Requirement", "UX-DR33")]
    public async Task The_typed_name_field_is_labelled_not_autofillable_and_named_afresh_per_opening()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            var confirm = await OpenConfirmAsync(page);
            var first = await AssertNotAutofillableAsync(TypedName(page));

            await confirm.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = Cancel, Exact = true }).ClickAsync();
            await OpenConfirmAsync(page);

            var second = await AssertNotAutofillableAsync(TypedName(page));

            Assert.NotEqual(first, second);
        }
    }

    /// <summary>
    /// AC3, Q3 (a), Q5 - pressing "Delete this Space" with a name that does not match (here, another case) sends nothing and
    /// says "Type the Space name exactly as shown."; the button is never disabled, before or after. The anchor: the exact
    /// name, with spaces either side, is sent.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 - the delete confirm does not exist yet (story 3.3 Task 6; Q5 match rule)")]
    [Trait("Requirement", "UX-DR37")]
    public async Task A_name_that_does_not_match_sends_nothing_and_Delete_is_never_disabled()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            var confirm = await OpenConfirmAsync(page);
            var danger = confirm.Locator(DangerButton);

            Assert.Null(await danger.GetAttributeAsync("disabled"));

            await TypedName(page).FillAsync("northwind redesign");
            await danger.ClickAsync();
            await confirm.GetByText(MismatchCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Empty(api.SpaceDeletions(Northwind));
            Assert.Null(await danger.GetAttributeAsync("disabled"));
            Assert.Null(await danger.GetAttributeAsync("aria-disabled"));

            await TypedName(page).FillAsync("  " + NorthwindName + "  ");
            await danger.ClickAsync();
            await WaitForPathAsync(page, $"/spaces/{Harbour:D}");

            Assert.Single(api.SpaceDeletions(Northwind));
        }
    }

    /// <summary>
    /// AC12, EXPERIENCE.md:335 - the deletion is never optimistic: while its answer is out the confirm stays exactly as it
    /// is, marked <c>aria-busy</c>, nothing is disabled, the tab is still on settings and the switcher still lists the
    /// Space.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC12 - the delete confirm does not exist yet (story 3.3 Task 6)")]
    [Trait("Requirement", "UX-DR26")]
    public async Task While_the_deletion_is_out_the_confirm_stays_busy_and_nothing_is_removed()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            var confirm = await OpenConfirmAsync(page);
            var held = api.HoldSpaceDeleteAsync();

            await TypedName(page).FillAsync(NorthwindName);
            await confirm.Locator(DangerButton).ClickAsync();
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            Assert.Equal("true", await confirm.GetAttributeAsync("aria-busy"));
            Assert.Equal(0, await page.Locator(DisabledAnywhere).CountAsync());
            Assert.Equal(NorthwindName, await TypedName(page).InputValueAsync());
            Assert.EndsWith($"/spaces/{Northwind:D}/settings", new Uri(page.Url).AbsolutePath, StringComparison.Ordinal);
            Assert.Contains(NorthwindName, await page.Locator(ContextBar).InnerTextAsync(), StringComparison.Ordinal);

            api.ReleaseSpaceDelete();
            await WaitForPathAsync(page, $"/spaces/{Harbour:D}");
        }
    }

    /// <summary>
    /// AC12, UX-DR32, UX-DR38, Q6 (a) - on <c>204</c> with another Space listed: the tab lands on that Space's home (by
    /// <c>/</c>, with <c>replace</c>, so Back does not return to the deleted Space's settings), focus is on the page's
    /// heading, "Space deleted." is said once and no sentence after the deletion names it, nothing of the deleted Space is
    /// in the DOM, the switcher no longer lists it, and no web storage holds anything.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC12 - the deletion's outcome on the client does not exist yet (story 3.3 Task 6; Q6 (a))")]
    [Trait("Requirement", "UX-DR32")]
    [Trait("Requirement", "UX-DR38")]
    public async Task Deleting_with_Spaces_remaining_lands_on_the_next_listed_Space_and_says_so_without_a_name()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await page.EvaluateAsync(RecordEverythingSaid);

            var confirm = await OpenConfirmAsync(page);

            await TypedName(page).FillAsync(NorthwindName);
            await confirm.Locator(DangerButton).ClickAsync();

            await WaitForPathAsync(page, $"/spaces/{Harbour:D}");
            await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await WaitUntilSaidAsync(page, SpaceDeletedCopy);
            await WaitForFocusOnHeadingAsync(page);

            var said = await SaidAsync(page);

            Assert.Single(said, sentence => sentence.Contains(SpaceDeletedCopy, StringComparison.Ordinal));
            Assert.DoesNotContain(said, sentence => sentence.Contains(NorthwindName, StringComparison.Ordinal));
            Assert.DoesNotContain(NorthwindName, await page.EvaluateAsync<string>("() => document.body.innerHTML"), StringComparison.Ordinal);

            var rows = await OpenSwitcherRowsAsync(page);

            Assert.Equal([HarbourName], (await rows.AllInnerTextsAsync()).Select(text => text.Trim()));
            Assert.Equal(0, await page.EvaluateAsync<int>("async () => localStorage.length + sessionStorage.length + (await indexedDB.databases()).length"));

            await page.GoBackAsync();

            Assert.DoesNotContain($"/spaces/{Northwind:D}/settings", new Uri(page.Url).AbsolutePath, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC6, AC12, UX-DR35, EXPERIENCE.md:253 - deleting the last Space lands on the no-Space state: "No Spaces yet" with
    /// focus on it, its one "Create a Space" action, and nothing created.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC6, AC12 - the deletion's outcome on the client does not exist yet (story 3.3 Task 6)")]
    [Trait("Requirement", "UX-DR35")]
    [Trait("Assumption", "PRD-12-2")]
    public async Task Deleting_the_last_Space_lands_on_the_no_Space_state_with_focus_on_its_heading()
    {
        var api = new FakeYelloApi { SignedIn = true };
        api.Switcher.Add((Northwind, NorthwindName));
        api.Roles[Northwind] = "owner";

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            var confirm = await OpenConfirmAsync(page);

            await TypedName(page).FillAsync(NorthwindName);
            await confirm.Locator(DangerButton).ClickAsync();

            var heading = page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = NoSpaceHeading });

            await heading.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await WaitForPathAsync(page, "/");
            await page.WaitForFunctionAsync("() => document.activeElement && document.activeElement.tagName === 'H1'", null, new PageWaitForFunctionOptions { Timeout = Timeout });

            Assert.True(await heading.EvaluateAsync<bool>("e => e === document.activeElement"), "Focus is not on the no-Space heading.");
            Assert.Equal(1, await page.Locator("main").GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = CreateAction, Exact = true }).CountAsync());
            Assert.Empty(api.Switcher);
            Assert.DoesNotContain(api.Requests, request => request.Method == "POST" && request.Path == SpaceRoutes.Directory);
        }
    }

    /// <summary>
    /// AC12, Q1 (a) - a <c>404</c> on the deletion means the Space is already gone for this Account (another tab, or
    /// someone else): the client routes exactly as for a <c>204</c> - leaves, purges, goes home to the next listed Space -
    /// but says nothing, since this tab deleted nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC12 - the deletion's outcome on the client does not exist yet (story 3.3 Task 6; Q1 (a))")]
    [Trait("Requirement", "UX-DR32")]
    public async Task A_404_on_the_deletion_goes_home_as_a_204_does_and_says_nothing()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await page.EvaluateAsync(RecordEverythingSaid);

            var confirm = await OpenConfirmAsync(page);
            var held = api.HoldSpaceDeleteAsync();

            await TypedName(page).FillAsync(NorthwindName);
            await confirm.Locator(DangerButton).ClickAsync();
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            // Someone else deleted it first: the switcher no longer lists it, and this deletion finds nothing.
            _ = api.Switcher.Remove((Northwind, NorthwindName));
            _ = api.Roles.Remove(Northwind);
            api.ReleaseSpaceDeleteRefused(404, ProblemTypes.NotAvailable);

            await WaitForPathAsync(page, $"/spaces/{Harbour:D}");
            await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.DoesNotContain(await SaidAsync(page), sentence => sentence.Contains(SpaceDeletedCopy, StringComparison.Ordinal));
            Assert.DoesNotContain(NorthwindName, await page.EvaluateAsync<string>("() => document.body.innerHTML"), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC12, NFR-5, AD-18 - a deletion that gets no answer, or a <c>5xx</c>, keeps the confirm open with "Not sent." and
    /// removes nothing; pressing "Delete this Space" again sends the same <c>Idempotency-Key</c>, and that answer goes home.
    /// </summary>
    /// <param name="unanswered">Whether the connection is reset rather than answered <c>503</c>.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC12 - the deletion's outcome on the client does not exist yet (story 3.3 Task 6; Q1 key reuse)")]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "NFR-5")]
    [Trait("Requirement", "AR-25")]
    public async Task A_deletion_with_no_answer_or_a_5xx_keeps_the_confirm_and_its_retry_reuses_the_key(bool unanswered)
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            var confirm = await OpenConfirmAsync(page);
            var danger = confirm.Locator(DangerButton);
            var held = api.HoldSpaceDeleteAsync();

            await TypedName(page).FillAsync(NorthwindName);
            await danger.ClickAsync();
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            if (unanswered)
            {
                api.ReleaseSpaceDeleteUnanswered();
            }
            else
            {
                api.ReleaseSpaceDeleteRefused(503, ProblemTypes.ServiceUnavailable);
            }

            await confirm.GetByText(NotSentCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(1, await page.Locator(Confirm).CountAsync());
            Assert.Contains(api.Switcher, space => space.Id == Northwind);

            await danger.ClickAsync();
            await WaitForPathAsync(page, $"/spaces/{Harbour:D}");

            var sent = api.SpaceDeletions(Northwind);

            Assert.Equal(2, sent.Count);
            Assert.False(string.IsNullOrEmpty(sent[0].Header(Idempotency.HeaderName)), "The deletion carried no Idempotency-Key.");
            Assert.Equal(sent[0].Header(Idempotency.HeaderName), sent[1].Header(Idempotency.HeaderName));
        }
    }

    /// <summary>
    /// AC1, AC9, UX-DR26 - a <c>403 insufficient-role</c> on the deletion (the Role was lowered since the page loaded) is
    /// said in the section in the copy that exists, "Only the Owner can delete the Space."; the tab stays and nothing is
    /// removed.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1, AC9 - the deletion's outcome on the client does not exist yet (story 3.3 Task 6)")]
    [Trait("Requirement", "UX-DR26")]
    public async Task A_403_on_the_deletion_says_only_the_Owner_can_delete_the_Space()
    {
        var api = TwoSpaces();
        api.NextSpaceDeleteRefusal = (403, InsufficientRoleType, "delete-space");

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            var confirm = await OpenConfirmAsync(page);

            await TypedName(page).FillAsync(NorthwindName);
            await confirm.Locator(DangerButton).ClickAsync();
            await page.Locator("main").GetByText(OwnerOnlyCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.EndsWith($"/spaces/{Northwind:D}/settings", new Uri(page.Url).AbsolutePath, StringComparison.Ordinal);
            Assert.Contains(api.Switcher, space => space.Id == Northwind);
        }
    }

    /// <summary>
    /// AC14, Q7 (a), <c>deferred-work.md:356</c> - an Account with no Space opens the switcher: the panel says "You do not
    /// belong to any Space yet." outside the list, keeps its "Create a Space" foot link, and the list holds no row.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC14 - the open switcher with an empty answer is an empty, unexplained panel (story 3.3 Task 6, Q7 (a))")]
    [Trait("Requirement", "UX-DR35")]
    [Trait("Requirement", "UX-DR9")]
    public async Task The_switcher_with_no_Space_says_so_outside_the_list_and_keeps_Create_a_Space()
    {
        var api = new FakeYelloApi { SignedIn = true };
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(surface.BaseAddress.ToString());
            await page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = NoSpaceHeading }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var list = await OpenSwitcherAsync(page);

            await page.Locator(ContextBar).GetByText(NoSpaceInSwitcher).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator($"[id='{list}']").GetByText(NoSpaceInSwitcher).CountAsync());
            Assert.Equal(0, await page.Locator($"[id='{list}'] li").CountAsync());
            Assert.Equal(1, await page.Locator(ContextBar).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = CreateAction, Exact = true }).CountAsync());
            Assert.Equal(0, await page.Locator(ContextBar).GetByText(SwitcherFailed).CountAsync());
        }
    }

    /// <summary>
    /// AC14, Q7 (a) - when the switcher could not be asked and nothing is held, the open panel says "Your Spaces could not
    /// be listed." - never the no-Space sentence, which would be a false statement about the Account - and the list holds
    /// no row.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC14 - the open switcher whose refresh failed is an empty, unexplained panel (story 3.3 Task 6, Q7 (a))")]
    [Trait("Requirement", "UX-DR9")]
    public async Task The_switcher_that_could_not_be_asked_says_so_and_never_claims_no_Space()
    {
        var api = TwoSpaces();
        api.SwitcherFails = true;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(surface.BaseAddress.ToString());
            await page.Locator("main h1", new PageLocatorOptions { HasText = NotAvailableCopy }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var list = await OpenSwitcherAsync(page);

            await page.Locator(ContextBar).GetByText(SwitcherFailed).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator(ContextBar).GetByText(NoSpaceInSwitcher).CountAsync());
            Assert.Equal(0, await page.Locator($"[id='{list}'] li").CountAsync());
        }
    }

    /// <summary>
    /// AC15, Q8 (a), <c>deferred-work.md:679</c> - on <c>/</c>, when the switcher could not be asked, the refusal page keeps
    /// the uniform "Not available." heading but offers no link to the page it is on; it points to the context bar's
    /// switcher instead.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC15 - the refusal page at / links Back to Yello to itself (story 3.3 Task 6, Q8 (a))")]
    [Trait("Requirement", "UX-DR32")]
    public async Task The_refusal_page_at_home_when_the_switcher_failed_points_to_the_switcher_not_to_itself()
    {
        var api = TwoSpaces();
        api.SwitcherFails = true;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(surface.BaseAddress.ToString());

            await AssertTheHomeRefusalPointsToTheSwitcherAsync(page);
        }
    }

    /// <summary>
    /// AC15, Q8 (a) - the same page, reached the other way: every listed Space refused this tab (3.2 AC12's flow), so the
    /// route back stops on <c>/</c>. "Back to Yello." still appears on each Space's own refusal (the anchor), and not on
    /// <c>/</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC15 - the refusal page at / links Back to Yello to itself (story 3.3 Task 6, Q8 (a))")]
    [Trait("Requirement", "UX-DR32")]
    public async Task The_refusal_page_at_home_when_every_Space_refused_points_to_the_switcher_not_to_itself()
    {
        var api = TwoSpaces();
        api.ArrivalRefusals[Northwind] = 409;
        api.ArrivalRefusals[Harbour] = 403;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(Address($"spaces/{Northwind:D}"));

            var back = page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = BackToYello });

            await back.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await back.ClickAsync();
            await WaitForPathAsync(page, $"/spaces/{Harbour:D}");
            await back.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await back.ClickAsync();
            await WaitForPathAsync(page, "/");

            await AssertTheHomeRefusalPointsToTheSwitcherAsync(page);
        }
    }

    /// <summary>
    /// AC16, NFR-9, UX-DR40, UX-DR41 - the whole deletion by keyboard alone, at 1280x800 and at 320x256: Enter on "Delete
    /// this Space" opens the confirm with focus in the field; the name is typed; Tab reaches "Delete this Space" then
    /// "Cancel", each with a visible ring that is not inset; at 320x256 nothing scrolls sideways and the Role chip stays;
    /// Shift+Tab and Enter delete, and the tab lands on the next listed Space.
    /// </summary>
    /// <param name="width">The viewport width.</param>
    /// <param name="height">The viewport height.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC16 - the delete confirm does not exist yet (story 3.3 Task 6)")]
    [InlineData(1280, 800)]
    [InlineData(320, 256)]
    [Trait("Requirement", "NFR-9")]
    [Trait("Requirement", "UX-DR41")]
    [Trait("Requirement", "UX-DR40")]
    public async Task The_deletion_works_by_keyboard_alone_with_a_visible_ring_and_no_sideways_scroll(int width, int height)
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(width, height);
            await OpenSettingsAsync(api, page, Northwind);

            await Opener(page).FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            await page.Locator(Confirm).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.WaitForFunctionAsync("() => document.activeElement && document.activeElement.tagName === 'INPUT'", null, new PageWaitForFunctionOptions { Timeout = Timeout });

            await page.Keyboard.TypeAsync(NorthwindName);

            await page.Keyboard.PressAsync("Tab");
            Assert.Equal(DeleteSpace, await FocusedTextAsync(page));
            Assert.True(await page.EvaluateAsync<bool>(FocusedHasVisibleRing), "Delete this Space shows no ring, or an inset one.");

            await page.Keyboard.PressAsync("Tab");
            Assert.Equal(Cancel, await FocusedTextAsync(page));
            Assert.True(await page.EvaluateAsync<bool>(FocusedHasVisibleRing), "Cancel shows no ring, or an inset one.");

            Assert.Empty(await page.EvaluateAsync<string[]>(Reflow));
            Assert.True(await page.Locator(RoleChip).IsVisibleAsync(), "The Role chip is not visible with the confirm open.");

            await page.Keyboard.PressAsync("Shift+Tab");
            await page.Keyboard.PressAsync("Enter");

            await WaitForPathAsync(page, $"/spaces/{Harbour:D}");
            Assert.Single(api.SpaceDeletions(Northwind));
        }
    }

    /// <summary>
    /// A signed-in Account who is Owner of Northwind Redesign and Member of Harbour Ops, listed in that order, with
    /// Northwind's counts stated (2 Projects, 5 Tasks, 2 other Members).
    /// </summary>
    private static FakeYelloApi TwoSpaces()
    {
        var api = new FakeYelloApi { SignedIn = true };

        api.Switcher.Add((Northwind, NorthwindName));
        api.Switcher.Add((Harbour, HarbourName));
        api.Roles[Northwind] = "owner";
        api.Roles[Harbour] = "member";
        api.DeletionSummaries[Northwind] = (2, 5, 2);

        return api;
    }

    private static ILocator Opener(IPage page) =>
        page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = DeleteSpace, Exact = true });

    private static ILocator RenameButton(IPage page) =>
        page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = RenameAction, Exact = true });

    private static ILocator LabelsSection(IPage page) =>
        page.Locator("main").GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = LabelsHeading, Exact = true });

    private static ILocator TypedName(IPage page) =>
        page.GetByLabel(TypedNameLabel, new PageGetByLabelOptions { Exact = true });

    private static async Task<ILocator> OpenConfirmAsync(IPage page)
    {
        await Opener(page).ClickAsync();

        var confirm = page.Locator(Confirm);

        await confirm.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await TypedName(page).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

        return confirm;
    }

    /// <summary>
    /// AC3: the input is <c>autocomplete="off"</c> and its field name is not one a browser would match to a saved value.
    /// Anchored: exactly one input is found first.
    /// </summary>
    /// <returns>The field name, so a caller can compare openings.</returns>
    private static async Task<string> AssertNotAutofillableAsync(ILocator input)
    {
        await input.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

        Assert.Equal(1, await input.CountAsync());
        Assert.Equal("off", await input.GetAttributeAsync("autocomplete"));

        var name = await input.GetAttributeAsync("name") ?? string.Empty;

        Assert.DoesNotContain(name.ToUpperInvariant(), ReusableFieldNames);

        return name;
    }

    /// <summary>
    /// AC15: the uniform heading, no link anywhere in <c>main</c> whose target is <c>/</c> itself, and the sentence that
    /// points to the switcher; the context bar's shell is there to point to.
    /// </summary>
    private static async Task AssertTheHomeRefusalPointsToTheSwitcherAsync(IPage page)
    {
        await page.Locator("main h1", new PageLocatorOptions { HasText = NotAvailableCopy }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.Locator("main").GetByText(ChooseFromSwitcher).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

        Assert.Equal("/", await page.EvaluateAsync<string>("() => location.pathname"));
        Assert.Equal(0, await page.EvaluateAsync<int>("() => [...document.querySelectorAll('main a')].filter(a => new URL(a.href).pathname === '/').length"));
        Assert.Equal(1, await page.Locator(SwitcherTrigger).CountAsync());
    }

    private static async Task<string> FocusedTextAsync(IPage page) =>
        await page.EvaluateAsync<string>("() => document.activeElement ? document.activeElement.textContent.trim() : ''");

    private static async Task WaitForFocusOnHeadingAsync(IPage page)
    {
        try
        {
            await page.WaitForFunctionAsync(
                "() => document.activeElement && document.activeElement.tagName === 'H1' && !!document.activeElement.closest('main')",
                null,
                new PageWaitForFunctionOptions { Timeout = Timeout });
        }
        catch (TimeoutException)
        {
            var focused = await page.EvaluateAsync<string>("() => document.activeElement ? document.activeElement.outerHTML.slice(0, 120) : 'nothing'");

            Assert.Fail($"Focus is not on the destination's heading after the deletion; it is on {focused}.");
        }
    }

    private static async Task<List<string>> SaidAsync(IPage page) => [.. await page.EvaluateAsync<string[]>("() => window.__said")];

    private static async Task WaitUntilSaidAsync(IPage page, string sentence)
    {
        try
        {
            await page.WaitForFunctionAsync(
                "sentence => window.__said.some(said => said.includes(sentence))",
                sentence,
                new PageWaitForFunctionOptions { Timeout = Timeout, PollingInterval = 50 });
        }
        catch (TimeoutException)
        {
            Assert.Fail($"'{sentence}' was not said; said: [{string.Join(" | ", await SaidAsync(page))}].");
        }
    }

    private static async Task WaitForPathAsync(IPage page, string path)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(Timeout);

        while (await page.EvaluateAsync<string>("() => location.pathname") != path)
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, $"The page never reached {path}; it is on {new Uri(page.Url).AbsolutePath}.");
        }
    }

    /// <summary>
    /// Opens the switcher and returns its list's element id, without waiting for a row - an empty list is a state these
    /// cases assert.
    /// </summary>
    private static async Task<string> OpenSwitcherAsync(IPage page)
    {
        var trigger = page.Locator(SwitcherTrigger);
        var list = await trigger.GetAttributeAsync("aria-controls");

        Assert.False(string.IsNullOrEmpty(list), "The switcher trigger names no list in aria-controls.");

        if (await trigger.GetAttributeAsync("aria-expanded") != "true")
        {
            await trigger.ClickAsync();
        }

        await page.Locator($"{SwitcherTrigger}[aria-expanded='true']").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

        return list;
    }

    private static async Task<ILocator> OpenSwitcherRowsAsync(IPage page)
    {
        var list = await OpenSwitcherAsync(page);
        var rows = page.Locator($"[id='{list}'] a");

        await rows.First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

        return rows;
    }

    private string Address(string path) => new Uri(surface.BaseAddress, path).ToString();

    private async Task OpenSettingsAsync(FakeYelloApi api, IPage page, Guid spaceId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address($"spaces/{spaceId:D}"));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.Locator(ContextBar).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = SettingsLink, Exact = true }).ClickAsync();
        await RenameButton(page).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }
}
