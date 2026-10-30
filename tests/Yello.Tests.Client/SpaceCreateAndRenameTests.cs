using Microsoft.Playwright;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Spaces;

namespace Yello.Tests.Client;

/// <summary>
/// Story 3.1 in a real browser: creating a Space from the switcher's foot and from the no-Space state, renaming the
/// active Space in Space settings, the context bar's Space settings link, and the name inputs' autofill rule. AC3, AC4,
/// AC6, AC8, AC13-AC16; UX-DR9, UX-DR27, UX-DR31, UX-DR33, UX-DR35, UX-DR39, UX-DR41.
/// </summary>
/// <remarks>
/// <para>
/// <b>RED-PHASE SCAFFOLDS (story 3.1 ATDD run, 2026-10-29).</b> Every case is skipped with its AC: the create page,
/// the switcher's foot link, the rename section and the context bar link do not exist yet (Tasks 6, 7, 8). The API is
/// <see cref="FakeYelloApi"/> at the network edge, which this run taught <c>POST /api/v1/spaces</c> and
/// <c>PATCH /api/v1/spaces/{id}</c> (held and released like <c>HoldRenameAsync</c>); no Host, no database, no engine.
/// </para>
/// <para>
/// <b>Locators are by role and accessible name, from the copy the story fixes</b> ("Create a Space", "Space name",
/// "Rename", "Space settings", the bound sentence already in <c>ClientCopy.resx</c>). The create page's route is not
/// fixed by the story ("for example <c>/spaces/new</c>"), so no case types it: each reaches the page through its
/// entry point. <b>Assumed open questions</b>, each the story's recommendation: Q3 (a) - a page linked from the
/// switcher's foot and the no-Space state; Q4 (a) - a context bar link for Owner and Admin; Q7 (a) - a rename updates
/// the bar without counting as a switch; Q8 - announcements carry no Space name.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-5")]
[Trait("Requirement", "FR-6")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class SpaceCreateAndRenameTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string ContextBar = "nav.context-bar";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string SwitcherTrigger = "nav.context-bar button[aria-controls]";
    private const string BarAnnouncement = "nav.context-bar [role=status]";

    private const string CreateAction = "Create a Space";
    private const string SpaceNameLabel = "Space name";
    private const string RenameAction = "Rename";
    private const string SettingsLink = "Space settings";
    private const string BoundCopy = "This Account has 50 Spaces, the maximum.";
    private const string EmptySpace = "No Projects yet.";
    private const string NoSpaceHeading = "No Spaces yet";

    /// <summary>
    /// A 1.4.10 reflow probe (as <c>SmallViewportTests.Reflow</c>): the page never scrolls sideways and nothing in
    /// <c>main</c> or the context bar has a horizontal scroll.
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

    private const string TextSpacingOverride =
        "* { line-height: 1.5 !important; letter-spacing: 0.12em !important; word-spacing: 0.16em !important; } "
        + "p { margin-block-end: 2em !important; }";

    private const string TextOnlyZoom = "html { font-size: 200% !important; }";

    private static readonly string[] ReusableFieldNames = [string.Empty, "NAME", "SPACE", "SPACENAME", "SPACE-NAME", "SPACE_NAME", "TITLE"];

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();

    /// <summary>
    /// AC13, UX-DR31, UX-DR39, Q3 (a) - "Create a Space" sits at the switcher panel's foot, outside its list (the rows
    /// still carry a name and nothing else); the created Space is listed by a directory refresh <b>before</b> the client
    /// asks for it, the person lands in its empty home, focus moves to the context bar and the new Space and "Owner" are
    /// announced politely; the creation carried an <c>Idempotency-Key</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC13 - the create page and the switcher's foot link do not exist yet (story 3.1 Task 6)")]
    [Trait("Requirement", "UX-DR39")]
    [Trait("Requirement", "UX-DR31")]
    [Trait("Requirement", "AD-18")]
    public async Task Creating_a_Space_from_the_switcher_lands_in_it_as_a_switch_after_the_directory_lists_it()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);

            var list = await OpenSwitcherAsync(page);
            var foot = page.Locator(ContextBar).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = CreateAction, Exact = true });

            Assert.Equal(1, await foot.CountAsync());
            Assert.Equal(0, await page.Locator($"[id='{list}']").GetByText(CreateAction).CountAsync());
            Assert.Equal(2, await page.Locator($"[id='{list}'] a").CountAsync());

            await foot.ClickAsync();
            await SubmitNewSpaceAsync(page, "Quayside");

            await page.Locator(ContextBar, new PageLocatorOptions { HasText = "Quayside" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.GetByText(EmptySpace).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var created = api.Switcher.Single(space => space.Name == "Quayside").Id;
            var requests = api.Requests.ToList();
            var post = requests.FindIndex(request => request.Method == "POST" && request.Path == SpaceRoutes.Directory);
            var refresh = requests.FindIndex(post + 1, request => request.Method == "GET" && request.Path == SpaceRoutes.Directory);
            var arrival = requests.FindIndex(request => request.Method == "GET" && request.Path == SpaceRoutes.ActiveOf(created));

            Assert.True(post >= 0 && refresh > post && arrival > refresh, $"Expected POST, then the directory refresh, then the arrival: {post}, {refresh}, {arrival}.");
            Assert.False(string.IsNullOrWhiteSpace(requests[post].Header(Idempotency.HeaderName)), "The creation carried no Idempotency-Key.");
            Assert.EndsWith($"/spaces/{created:D}", new Uri(page.Url).AbsolutePath, StringComparison.Ordinal);

            await page.WaitForFunctionAsync(
                $"() => document.activeElement && document.activeElement.matches('{ContextBar}')",
                null,
                new PageWaitForFunctionOptions { Timeout = Timeout });

            var announced = await page.Locator(BarAnnouncement).InnerTextAsync();

            Assert.Contains("Quayside", announced, StringComparison.Ordinal);
            Assert.Contains("Owner", announced, StringComparison.Ordinal);
            Assert.DoesNotContain("Northwind", announced, StringComparison.Ordinal);
            Assert.Equal("Yello", await page.TitleAsync());

            var rows = page.Locator($"[id='{await OpenSwitcherAsync(page)}'] a");

            Assert.Contains("Quayside", await rows.AllInnerTextsAsync());
        }
    }

    /// <summary>
    /// AC15, UX-DR35, FR-7 - an Account with no Membership sees "No Spaces yet", its sentence and exactly one action, to
    /// create a Space; nothing is created for it, and following the action sends no creation until the person submits.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC15 - the no-Space state offers no action yet (story 3.1 Task 6; deferred-work.md:334)")]
    [Trait("Requirement", "UX-DR35")]
    [Trait("Requirement", "FR-7")]
    public async Task The_no_Space_state_offers_exactly_one_action_to_create_a_Space_and_creates_nothing()
    {
        var api = new FakeYelloApi { SignedIn = true };
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(surface.BaseAddress.ToString());
            await page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = NoSpaceHeading }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var actions = page.Locator("#app").GetByRole(AriaRole.Link).Or(page.Locator("#app").GetByRole(AriaRole.Button));

            Assert.Equal(1, await actions.CountAsync());
            Assert.Equal(CreateAction, (await actions.InnerTextAsync()).Trim());

            await actions.ClickAsync();
            await page.GetByLabel(SpaceNameLabel, new PageGetByLabelOptions { Exact = true }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.DoesNotContain(api.Requests, request => request.Method == "POST" && request.Path == SpaceRoutes.Directory);
            Assert.Empty(api.Switcher);
        }
    }

    /// <summary>
    /// AC6, AC15, UX-DR35 - the 51st Space is refused in place, with the bound stated plainly ("This Account has 50
    /// Spaces, the maximum."), no navigation and nothing added to the switcher.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC6 - the create page does not exist yet (story 3.1 Task 6)")]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "AR-30")]
    public async Task The_51st_Space_renders_the_bound_in_place_and_navigates_nowhere()
    {
        var api = TwoSpaces();
        api.SpaceBoundReached = true;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenCreatePageAsync(api, page);

            var at = new Uri(page.Url).AbsolutePath;

            await SubmitNewSpaceAsync(page, "One too many");
            await page.Locator("main", new PageLocatorOptions { HasText = BoundCopy }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(at, new Uri(page.Url).AbsolutePath);
            Assert.Equal(2, api.Switcher.Count);
            Assert.Equal(1, await page.GetByLabel(SpaceNameLabel, new PageGetByLabelOptions { Exact = true }).CountAsync());
        }
    }

    /// <summary>
    /// AC9, AC15, UX-DR37 - a name the rule refuses (blank after trimming) renders a message beside the field, which is
    /// marked invalid; nothing is created and the page stays.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - the create page does not exist yet (story 3.1 Tasks 6, 8)")]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "UX-DR37")]
    public async Task An_invalid_Space_name_is_refused_beside_the_field()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenCreatePageAsync(api, page);

            var at = new Uri(page.Url).AbsolutePath;

            await SubmitNewSpaceAsync(page, "   ");

            var field = page.GetByLabel(SpaceNameLabel, new PageGetByLabelOptions { Exact = true });

            await page.Locator("main [aria-invalid='true']").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal("true", await field.GetAttributeAsync("aria-invalid"));
            Assert.False(string.IsNullOrEmpty(await field.GetAttributeAsync("aria-describedby")), "The invalid field names no message.");
            Assert.Equal(at, new Uri(page.Url).AbsolutePath);
            Assert.Equal(2, api.Switcher.Count);
        }
    }

    /// <summary>
    /// AC13, AC9, Q2 (a), Task 6 - a creation whose answer was lost may have been applied, so submitting again re-sends
    /// with the same <c>Idempotency-Key</c>, and one Space appears.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC13 - the create page does not exist yet (story 3.1 Task 6, Q2 (a))")]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "AD-18")]
    public async Task A_creation_resent_after_a_lost_answer_reuses_its_key_and_one_Space_appears()
    {
        var api = TwoSpaces();
        api.LoseNextSpaceCreateAnswer = true;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenCreatePageAsync(api, page);

            await SubmitNewSpaceAsync(page, "Lighthouse");

            // The first answer was lost; the page offers the same submission again rather than navigating.
            await page.Locator("main [role='status'], main [role='alert']").First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });
            await page.GetByLabel(SpaceNameLabel, new PageGetByLabelOptions { Exact = true }).PressAsync("Enter");

            await page.Locator(ContextBar, new PageLocatorOptions { HasText = "Lighthouse" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var keys = api.Requests
                .Where(request => request.Method == "POST" && request.Path == SpaceRoutes.Directory)
                .Select(request => request.Header(Idempotency.HeaderName))
                .ToList();

            Assert.Equal(2, keys.Count);
            Assert.False(string.IsNullOrEmpty(keys[0]));
            Assert.Equal(keys[0], keys[1]);
            Assert.Single(api.Switcher, space => space.Name == "Lighthouse");
        }
    }

    /// <summary>
    /// AC3, AC14, Q7 (a), Q8 - an Owner or an Admin renames in Space settings: the form stays open while the answer is
    /// out, then closes; the context bar's visible and accessible names show the new name without a reload; nothing moves
    /// focus to the context bar (a rename is not a switch); the polite announcement names no Space; the title stays
    /// <c>Yello</c>; the switcher's next opening lists the new name.
    /// </summary>
    /// <param name="role">The caller's Role.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC14 - Space settings has no rename section yet (story 3.1 Task 7)")]
    [InlineData("owner")]
    [InlineData("admin")]
    [Trait("Requirement", "UX-DR33")]
    [Trait("Requirement", "UX-DR38")]
    public async Task Renaming_updates_the_context_bar_in_place_and_announces_no_name(string role)
    {
        var api = TwoSpaces();
        api.Roles[Northwind] = role;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await page.EvaluateAsync("() => { window.__notReloaded = true; }");

            var opener = page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = RenameAction, Exact = true });
            await opener.ClickAsync();

            var field = page.GetByLabel(SpaceNameLabel, new PageGetByLabelOptions { Exact = true });
            await field.FillAsync("Northwind Retainer");

            var arrived = api.HoldSpaceRenameAsync();
            await field.PressAsync("Enter");
            await arrived.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            // Not optimistic: the field stays open, and the bar still names the Space by its old name.
            Assert.Equal(1, await field.CountAsync());
            Assert.Contains("Northwind Redesign", await page.Locator(ContextBar).InnerTextAsync(), StringComparison.Ordinal);

            api.ReleaseSpaceRename();

            await page.Locator(ContextBar, new PageLocatorOptions { HasText = "Northwind Retainer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await field.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.Contains("Northwind Retainer", await page.Locator(ContextBar).GetAttributeAsync("aria-label") ?? string.Empty, StringComparison.Ordinal);
            Assert.True(await page.EvaluateAsync<bool>("() => window.__notReloaded === true"), "The rename reloaded the page.");
            Assert.False(await page.EvaluateAsync<bool>($"() => !!document.activeElement && document.activeElement.matches('{ContextBar}')"), "A rename moved focus to the context bar, as a switch does.");

            var polite = string.Join(' ', await page.Locator("[aria-live='polite'], [role='status']").AllInnerTextsAsync());

            Assert.DoesNotContain("Northwind Retainer", polite, StringComparison.Ordinal);
            Assert.Contains("renamed", polite, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Yello", await page.TitleAsync());

            var rows = page.Locator($"[id='{await OpenSwitcherAsync(page)}'] a");

            Assert.Contains("Northwind Retainer", await rows.AllInnerTextsAsync());
        }
    }

    /// <summary>
    /// AC3, AC4, UX-DR9, UX-DR27, UX-DR28, Q4 (a) - Space settings is reachable from the context bar for an Owner (the
    /// anchor) and the link is absent, not disabled, for a Member or a Viewer, who also have no rename control; the Role
    /// chip is never the link; nothing in <c>main</c> is disabled.
    /// </summary>
    /// <param name="role">The lesser Role.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC4 - the context bar has no Space settings link yet (story 3.1 Task 7)")]
    [InlineData("member")]
    [InlineData("viewer")]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "UX-DR9")]
    public async Task Space_settings_is_linked_from_the_context_bar_for_an_Owner_and_absent_for_a_lesser_Role(string role)
    {
        var api = TwoSpaces();
        api.Roles[Harbour] = role;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSpaceAsync(api, page, Northwind);

            var link = page.Locator(ContextBar).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = SettingsLink, Exact = true });

            await link.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            Assert.Equal($"/spaces/{Northwind:D}/settings", await link.GetAttributeAsync("href"));
            Assert.Equal(0, await page.Locator("nav.context-bar .role-chip a, nav.context-bar a .role-chip, nav.context-bar a.role-chip").CountAsync());

            await ChooseInSwitcherAsync(page, "Harbour Ops");
            await page.Locator(ContextBar, new PageLocatorOptions { HasText = "Harbour Ops" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await link.CountAsync());
            Assert.Equal(0, await page.Locator(ContextBar).GetByText(SettingsLink).CountAsync());
            Assert.Equal(0, await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = RenameAction, Exact = true }).CountAsync());
            Assert.Equal(0, await page.Locator("main [disabled], main [aria-disabled='true'], nav.context-bar [disabled], nav.context-bar [aria-disabled='true']").CountAsync());
        }
    }

    /// <summary>
    /// AC16, Task 7 - Esc closes the open rename form, sends nothing, and returns focus to "Rename".
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC16 - Space settings has no rename section yet (story 3.1 Task 7)")]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "NFR-9")]
    public async Task Escape_closes_the_rename_form_without_a_request_and_returns_focus_to_Rename()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            var opener = page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = RenameAction, Exact = true });
            await opener.ClickAsync();

            var field = page.GetByLabel(SpaceNameLabel, new PageGetByLabelOptions { Exact = true });
            await field.FillAsync("Never sent");
            await field.PressAsync("Escape");

            await field.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.True(await opener.EvaluateAsync<bool>("e => e === document.activeElement"), "Focus did not return to Rename.");
            Assert.DoesNotContain(api.Requests, request => request.Method == "PATCH" && request.Path == SpaceRoutes.ActiveOf(Northwind));
            Assert.Contains("Northwind Redesign", await page.Locator(ContextBar).InnerTextAsync(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC8, UX-DR33, <c>deferred-work.md:332</c> - the gate first FINDS both Space name inputs (the create page's and
    /// the rename section's), then asserts each is <c>autocomplete="off"</c> with a field name no browser would reuse;
    /// and the create page's name is fresh per render, so a saved value can never be matched to it. A gate over zero
    /// inputs fails.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC8 - neither Space name input exists yet (story 3.1 Tasks 6, 7)")]
    [Trait("Requirement", "UX-DR33")]
    public async Task Both_Space_name_inputs_are_found_and_neither_can_be_autofilled()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenCreatePageAsync(api, page);
            var firstCreateName = await AssertNotAutofillableAsync(page.GetByLabel(SpaceNameLabel, new PageGetByLabelOptions { Exact = true }));

            await page.GotoAsync(Address($"spaces/{Northwind:D}/settings"));
            await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = RenameAction, Exact = true }).ClickAsync();
            var renameName = await AssertNotAutofillableAsync(page.GetByLabel(SpaceNameLabel, new PageGetByLabelOptions { Exact = true }));

            await OpenSwitcherAsync(page);
            await page.Locator(ContextBar).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = CreateAction, Exact = true }).ClickAsync();
            var secondCreateName = await AssertNotAutofillableAsync(page.GetByLabel(SpaceNameLabel, new PageGetByLabelOptions { Exact = true }));

            Assert.NotEqual(firstCreateName, secondCreateName);
            Assert.NotEqual(firstCreateName, renameName);
        }
    }

    /// <summary>
    /// AC16, UX-DR40, UX-DR41, 2.11 AC16 - at 320px, with the 1.4.12 text-spacing override and with 200% text-only zoom,
    /// a 100-character name in the create page and in the rename section reflows with no sideways page scroll, and the
    /// Owner's context bar keeps the Role chip beside the new Space settings link.
    /// </summary>
    /// <param name="zoom">Whether to apply 200% text-only zoom instead of the text-spacing override.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC16 - the create page, the rename section and the context bar link do not exist yet (story 3.1 Tasks 6, 7)")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "UX-DR41")]
    [Trait("Requirement", "UX-DR40")]
    [Trait("Requirement", "NFR-9")]
    public async Task The_new_surfaces_reflow_at_320px_with_a_100_character_name(bool zoom)
    {
        var longName = string.Concat(Enumerable.Repeat("Northwind ", 10));
        var api = new FakeYelloApi { SignedIn = true };
        api.Switcher.Add((Northwind, longName));
        api.Roles[Northwind] = "owner";

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(320, 256);
            await OpenSettingsAsync(api, page, Northwind);
            await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = zoom ? TextOnlyZoom : TextSpacingOverride });

            Assert.Equal(1, await page.Locator(RoleChip).CountAsync());
            Assert.True(await page.Locator(RoleChip).IsVisibleAsync(), "The Role chip is not visible at 320px.");
            Assert.Equal(1, await page.Locator(ContextBar).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = SettingsLink, Exact = true }).CountAsync());

            await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = RenameAction, Exact = true }).ClickAsync();
            await page.GetByLabel(SpaceNameLabel, new PageGetByLabelOptions { Exact = true }).FillAsync(longName);
            Assert.Empty(await page.EvaluateAsync<string[]>(Reflow));

            await OpenSwitcherAsync(page);
            await page.Locator(ContextBar).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = CreateAction, Exact = true }).ClickAsync();
            await page.GetByLabel(SpaceNameLabel, new PageGetByLabelOptions { Exact = true }).FillAsync(longName);
            await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = zoom ? TextOnlyZoom : TextSpacingOverride });
            Assert.Empty(await page.EvaluateAsync<string[]>(Reflow));
        }
    }

    /// <summary>
    /// A signed-in Account who is Owner of Northwind Redesign and Member of Harbour Ops, listed in that order.
    /// </summary>
    private static FakeYelloApi TwoSpaces()
    {
        var api = new FakeYelloApi { SignedIn = true };

        api.Switcher.Add((Northwind, "Northwind Redesign"));
        api.Switcher.Add((Harbour, "Harbour Ops"));
        api.Roles[Northwind] = "owner";
        api.Roles[Harbour] = "member";

        return api;
    }

    /// <summary>
    /// AC8: the input is <c>autocomplete="off"</c> and its field name is not one a browser would match to a saved value.
    /// Anchored: exactly one input is found first.
    /// </summary>
    /// <returns>The field name, so a caller can compare renders.</returns>
    private static async Task<string> AssertNotAutofillableAsync(ILocator input)
    {
        await input.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

        Assert.Equal(1, await input.CountAsync());
        Assert.Equal("off", await input.GetAttributeAsync("autocomplete"));

        var name = await input.GetAttributeAsync("name") ?? string.Empty;

        Assert.DoesNotContain(name.ToUpperInvariant(), ReusableFieldNames);

        return name;
    }

    private static async Task SubmitNewSpaceAsync(IPage page, string name)
    {
        var field = page.GetByLabel(SpaceNameLabel, new PageGetByLabelOptions { Exact = true });

        await field.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await field.FillAsync(name);
        await field.PressAsync("Enter");
    }

    private string Address(string path) => new Uri(surface.BaseAddress, path).ToString();

    private async Task OpenSpaceAsync(FakeYelloApi api, IPage page, Guid spaceId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address($"spaces/{spaceId:D}"));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    private async Task OpenSettingsAsync(FakeYelloApi api, IPage page, Guid spaceId)
    {
        await OpenSpaceAsync(api, page, spaceId);
        await page.Locator(ContextBar).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = SettingsLink, Exact = true }).ClickAsync();
        await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = RenameAction, Exact = true })
            .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    /// <summary>
    /// Reaches the create page the way a person does - from the switcher's foot - since the story fixes no route.
    /// </summary>
    private async Task OpenCreatePageAsync(FakeYelloApi api, IPage page)
    {
        await OpenSpaceAsync(api, page, Northwind);
        await OpenSwitcherAsync(page);
        await page.Locator(ContextBar).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = CreateAction, Exact = true }).ClickAsync();
        await page.GetByLabel(SpaceNameLabel, new PageGetByLabelOptions { Exact = true }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    /// <summary>
    /// Opens the switcher and returns its list's element id.
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

        await page.Locator($"[id='{list}'] a").First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

        return list;
    }

    private static async Task ChooseInSwitcherAsync(IPage page, string name)
    {
        var list = await OpenSwitcherAsync(page);
        var row = page.Locator($"[id='{list}'] a", new PageLocatorOptions { HasText = name });

        await row.ClickAsync();
    }
}
