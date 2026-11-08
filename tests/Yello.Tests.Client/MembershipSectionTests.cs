using Microsoft.Playwright;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.ApiTokens;
using Yello.Contracts.Labels;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// Story 4.4 in a real browser: Space settings' Members section - every Membership with its display name, its address (here
/// and nowhere else), its Role as a fixed chip or a "Change Role" control; the in-place Role picker with nothing checked;
/// the change sent once and never optimistic; the paged list that appends; the narrated exit on a lost authority; and the
/// three client ledger items the story owns (focus while a row action is in flight, the narration region rendered empty,
/// a Label delete answered <c>404</c>). AC10 (and AC1-AC4 as the client shows them); FR-13, UX-DR11, UX-DR14, UX-DR27,
/// UX-DR34, UX-DR38, UX-DR39; ledger <c>deferred-work.md:546</c>, <c>:550</c>, <c>:762</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 4.4 ATDD run (2026-11-02)</b>, activated by its dev phase.
/// The API is <see cref="FakeYelloApi"/> at the network edge, taught by this run the list and the Role change
/// (<c>FakeYelloApi.Memberships.cs</c>); no Host, no database, no engine. Build <c>Yello.Client</c> in Debug before running
/// them. Same collection as <see cref="InvitationListTests"/>, so no new fixture.
/// </para>
/// <para>
/// <b>Locators are this run's assumptions</b>, from AC10, Q12 (a), Q19 (a) and the mockup (<c>space-settings.html:112-179</c>):
/// the section is <c>main .membership-section</c> under a heading "Members", placed above the Invitations section; each row an
/// <c>li</c> in it; a row's control a button "Change Role" (described by the row's name), replaced in place by a
/// <c>fieldset</c> whose legend names the current Role, a radio per offerable Role, "Change" and "Cancel"; a fixed Role a
/// <c>.role-chip</c>; the caller's own row carrying "You". Copy is Q19 (a)'s, asserted as written. Text is read with
/// <c>textContent</c>, never <c>innerText</c>. A markup the dev phase builds differently is corrected here first and recorded
/// in the Debug Log.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-13")]
[Trait("Requirement", "UX-DR14")]
public sealed class MembershipSectionTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string ContextBar = "nav.context-bar";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string SwitcherTrigger = "nav.context-bar button[aria-controls]";
    private const string Section = "main .membership-section";
    private const string Rows = "main .membership-section li";

    private const string SettingsLink = "Space settings";
    private const string SectionHeading = "Members";
    private const string ChangeRole = "Change Role";
    private const string ChangeAction = "Change";
    private const string CancelAction = "Cancel";
    private const string ShowMore = "Show more";
    private const string AdminLimitCopy = "Admins can change Roles between Member and Viewer only.";

    /// <summary>
    /// Records every non-empty text a polite or status region changes to: <c>window.__said</c>. As
    /// <c>InvitationListTests.RecordEverythingSaid</c>.
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
    /// Installed before any page script (ledger <c>:550</c>): the text each <c>.role-narration</c> element holds at the
    /// moment it is inserted, in <c>window.__narrationAtInsert</c>.
    /// </summary>
    private const string RecordNarrationAtInsert =
        """
        window.__narrationAtInsert = [];
        new MutationObserver(records => {
            for (const record of records) {
                for (const node of record.addedNodes) {
                    if (!(node instanceof Element)) continue;
                    const found = node.matches('.role-narration') ? [node] : [...node.querySelectorAll('.role-narration')];
                    for (const region of found) window.__narrationAtInsert.push(region.textContent.trim());
                }
            }
        }).observe(document, { childList: true, subtree: true });
        """;

    /// <summary>
    /// A 1.4.10 reflow probe, as <c>InvitationListTests.Reflow</c>.
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

    private static readonly string[] AdminLimits = [AdminLimitCopy, "Admins can remove Members and Viewers only."];

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();

    /// <summary>
    /// AC1, AC4, AC10, Q12 (a) - the Owner sees every Membership, with "{n} of 100" in the heading's meta: its own row a
    /// fixed Owner chip marked "You" with no control, and every other row - Admin, Member, Viewer - a "Change Role" control.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    public async Task The_Owner_sees_Change_Role_on_every_other_row_and_a_fixed_chip_on_its_own()
    {
        var api = TwoSpaces("owner");
        var (owner, admin, member, viewer) = (Row("Olu Owner", "owner", isYou: true), Row("Ada Admin", "admin"), Row("Mo Member", "member"), Row("Vi Viewer", "viewer"));

        api.SpaceMemberships[Northwind] = [owner, admin, member, viewer];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await RowOf(page, viewer).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(4, await page.Locator(Rows).CountAsync());
            Assert.Contains("4 of 100", await page.Locator(Section).TextContentAsync() ?? string.Empty, StringComparison.Ordinal);

            var own = RowOf(page, owner);

            Assert.Equal(0, await ChangeRoleButton(own).CountAsync());
            Assert.Equal(1, await own.Locator(".role-chip").CountAsync());
            Assert.Contains("You", await own.TextContentAsync() ?? string.Empty, StringComparison.Ordinal);

            foreach (var other in new[] { admin, member, viewer })
            {
                Assert.Equal(1, await ChangeRoleButton(RowOf(page, other)).CountAsync());
            }
        }
    }

    /// <summary>
    /// AC4, UX-DR27, PRD section 12 #4 - an Admin sees "Change Role" only on Member and Viewer rows, offering exactly Member
    /// and Viewer; Admin and Owner rows are fixed chips; and no element in the section carries <c>disabled</c> or
    /// <c>aria-disabled</c>. Plant 14: a disabled control for an Admin.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Assumption", "PRD-12-4")]
    public async Task An_Admin_sees_Member_and_Viewer_only_on_Member_and_Viewer_rows_and_nothing_disabled()
    {
        var api = TwoSpaces("admin");
        var (owner, self, otherAdmin, member, viewer) = (Row("Olu Owner", "owner"), Row("Ada Admin", "admin", isYou: true), Row("Abe Admin", "admin"), Row("Mo Member", "member"), Row("Vi Viewer", "viewer"));

        api.SpaceMemberships[Northwind] = [owner, self, otherAdmin, member, viewer];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await RowOf(page, viewer).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            foreach (var fixedRow in new[] { owner, self, otherAdmin })
            {
                Assert.Equal(0, await ChangeRoleButton(RowOf(page, fixedRow)).CountAsync());
            }

            foreach (var changeable in new[] { member, viewer })
            {
                await ChangeRoleButton(RowOf(page, changeable)).ClickAsync();

                var picker = RowOf(page, changeable).Locator("fieldset");

                await picker.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

                Assert.Equal(["Member", "Viewer"], await RadioNamesAsync(picker));

                await RowOf(page, changeable).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = CancelAction, Exact = true }).ClickAsync();
            }

            Assert.Equal(0, await page.Locator($"{Section} [disabled], {Section} [aria-disabled]").CountAsync());
        }
    }

    /// <summary>
    /// UX-DR11, Q12 (a) - "Change Role" replaces the row's control in place (no dialog) with a radio group whose legend names
    /// the current Role, and <b>no option is checked</b>: the choice is consequential, so there is no default. Cancel restores
    /// the control and sends nothing. Plant 15: the current Role pre-checked.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR11")]
    public async Task The_Role_picker_opens_in_place_with_no_option_checked_and_Cancel_sends_nothing()
    {
        var api = TwoSpaces("owner");
        var member = Row("Mo Member", "member");

        api.SpaceMemberships[Northwind] = [Row("Olu Owner", "owner", isYou: true), member];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await ChangeRoleButton(RowOf(page, member)).ClickAsync();

            var picker = RowOf(page, member).Locator("fieldset");

            await picker.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator("[role=dialog], dialog[open]").CountAsync());
            Assert.Contains("Currently Member", await picker.Locator("legend").TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
            Assert.Equal(["Admin", "Member", "Viewer"], await RadioNamesAsync(picker));
            Assert.Equal(0, await picker.Locator("input[type=radio]:checked").CountAsync());

            await RowOf(page, member).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = CancelAction, Exact = true }).ClickAsync();
            await ChangeRoleButton(RowOf(page, member)).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Empty(api.RoleChanges(Northwind));
        }
    }

    /// <summary>
    /// AC10, UX-DR38, UX-DR39 - a change is sent once with an <c>Idempotency-Key</c> and <c>{ role }</c>, is never optimistic
    /// (the row keeps "Member" while the answer is held), and on <c>200</c> the row shows the new Role, the announcement says
    /// "{DisplayName} is now a {Role}." with no <c>@</c> anywhere in what was said, and focus returns to the row's "Change
    /// Role" control.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR38")]
    [Trait("Requirement", "UX-DR39")]
    [Trait("Requirement", "AD-23")]
    public async Task A_change_is_sent_once_with_a_key_waits_for_the_answer_and_is_announced_without_an_address()
    {
        var api = TwoSpaces("owner");
        var member = Row("Mo Member", "member");

        api.SpaceMemberships[Northwind] = [Row("Olu Owner", "owner", isYou: true), member];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await page.EvaluateAsync(RecordEverythingSaid);

            var held = api.HoldRoleChangeAsync();

            await ChooseRoleAsync(page, member, "Viewer");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            Assert.Contains("Member", await RowOf(page, member).TextContentAsync() ?? string.Empty, StringComparison.Ordinal);

            api.ReleaseRoleChange();

            await page.GetByText("Mo Member is now a Viewer.").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            var change = Assert.Single(api.RoleChanges(Northwind));

            Assert.False(string.IsNullOrEmpty(change.Header(Idempotency.HeaderName)), "The change carried no Idempotency-Key.");
            Assert.Contains("\"viewer\"", change.Body ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(await SaidAsync(page), said => said.Contains('@', StringComparison.Ordinal));
            Assert.True(
                await ChangeRoleButton(RowOf(page, member)).EvaluateAsync<bool>("e => e === document.activeElement"),
                "Focus did not return to the row's Change Role control after the answer.");
        }
    }

    /// <summary>
    /// AC2, UX-DR34, AD-23 - the address appears in the Members section and nowhere else on the page (the context bar, the
    /// Invitations section, any announcement), and leaving the Space purges it: after a switch to Harbour Ops it is nowhere
    /// in the document.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR34")]
    [Trait("Requirement", "AD-23")]
    public async Task The_address_appears_only_in_the_Members_section_and_is_gone_after_a_Space_switch()
    {
        var api = TwoSpaces("owner");
        var member = Row("Mo Member", "member");

        api.SpaceMemberships[Northwind] = [Row("Olu Owner", "owner", isYou: true), member];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await RowOf(page, member).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var everywhere = await page.EvaluateAsync<int>("address => document.body.textContent.split(address).length - 1", member.EmailAddress);
            var inSection = await page.Locator(Section).EvaluateAsync<int>("(e, address) => e.textContent.split(address).length - 1", member.EmailAddress);

            Assert.True(inSection >= 1 && everywhere == inSection, $"The address appears {everywhere} time(s) on the page and {inSection} in the Members section.");

            await ChooseInSwitcherAsync(page, "Harbour Ops");
            await page.Locator(ContextBar, new PageLocatorOptions { HasText = "Harbour Ops" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.EvaluateAsync<int>("address => document.documentElement.outerHTML.split(address).length - 1", member.EmailAddress));
        }
    }

    /// <summary>
    /// AC1, Q8 (a), AD-29, UX-DR26 - 100 Memberships load as 50, then "Show more" appends the next 50 with the cursor the
    /// first page answered; the rows already shown are kept (the focused one keeps focus), never recycled or replaced; the
    /// meta says "100 of 100" throughout; and "Show more" is gone after the last page. No infinite scroll.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "UX-DR26")]
    public async Task Two_pages_load_and_the_second_appends_without_replacing_focused_rows()
    {
        var api = TwoSpaces("owner");
        var rows = Enumerable.Range(0, 100).Select(index => index == 0 ? Row("Olu Owner", "owner", isYou: true) : Row($"Member {index:D3}", "member")).ToList();

        api.SpaceMemberships[Northwind] = rows;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await RowOf(page, rows[49]).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(MembershipWire.PageSize, await page.Locator(Rows).CountAsync());
            Assert.Contains("100 of 100", await page.Locator(Section).TextContentAsync() ?? string.Empty, StringComparison.Ordinal);

            await ChangeRoleButton(RowOf(page, rows[10])).FocusAsync();
            await page.EvaluateAsync("() => { window.__kept = document.activeElement; }");
            await page.Locator(Section).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = ShowMore, Exact = true }).ClickAsync();
            await RowOf(page, rows[99]).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(100, await page.Locator(Rows).CountAsync());
            Assert.True(await page.EvaluateAsync<bool>("() => document.contains(window.__kept)"), "A row shown before Show more was replaced rather than kept.");
            Assert.Equal(2, api.MembershipListReads(Northwind).Count);
            Assert.Contains("after=", api.MembershipListReads(Northwind)[1].Query, StringComparison.Ordinal);
            Assert.Equal(0, await page.Locator(Section).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = ShowMore, Exact = true }).CountAsync());
        }
    }

    /// <summary>
    /// An Admin whose own authority was lost meanwhile: the change answers <c>403</c> carrying the
    /// Member Role, and Space settings is no longer permitted, so the Role narration says "You're now a Member. Space
    /// settings is no longer available.", takes focus, and the page routes to Space home, where the narration keeps focus.
    /// The Admin-limit copy never appears. Anchor: as Admin the section and its control were shown. The capability-shaped
    /// copy is the other arm, when the Role still permits the section.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR29")]
    [Trait("Requirement", "NFR-2")]
    public async Task An_Admin_demoted_meanwhile_is_narrated_out_of_Space_settings_on_the_403()
    {
        var api = TwoSpaces("admin");
        var member = Row("Mo Member", "member");

        api.SpaceMemberships[Northwind] = [Row("Olu Owner", "owner"), Row("Ada Admin", "admin", isYou: true), member];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await ChangeRoleButton(RowOf(page, member)).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            api.Roles[Northwind] = "member";
            api.NextRoleChangeRefusal = (403, ProblemTypes.InsufficientRole, MembershipWire.CapabilityCode);

            await ChooseRoleAsync(page, member, "Viewer");
            await page.Locator(".role-narration", new PageLocatorOptions { HasText = "You're now a Member. Space settings is no longer available." })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.WaitForFunctionAsync("path => location.pathname === path", $"/spaces/{Northwind:D}", new PageWaitForFunctionOptions { Timeout = Timeout, PollingInterval = 50 });

            Assert.True(await page.Locator(".role-narration").EvaluateAsync<bool>("e => e === document.activeElement"), "Focus left the narration.");
            Assert.DoesNotContain(AdminLimitCopy, await page.Locator("main").TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC3, UX-DR34 - two Memberships named alike ("Sam Lee") render two distinct visible labels, each the name and a
    /// Membership-scoped discriminator, and neither label carries an <c>@</c>; a unique name renders bare.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR34")]
    public async Task Two_Memberships_named_alike_render_distinct_labels()
    {
        var api = TwoSpaces("owner");
        var (first, second, unique) = (Row("Sam Lee", "member"), Row("Sam Lee", "viewer"), Row("Ana Diaz", "member"));

        api.SpaceMemberships[Northwind] = [Row("Olu Owner", "owner", isYou: true), first, second, unique];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await RowOf(page, unique).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var labels = new List<string>();

            foreach (var alike in new[] { first, second })
            {
                labels.Add((await RowOf(page, alike).Locator(".membership-row__name").TextContentAsync() ?? string.Empty).Trim());
            }

            Assert.NotEqual(labels[0], labels[1]);
            Assert.All(labels, label => Assert.Matches("^Sam Lee · [0-9A-F]{4}$", label));
            Assert.Equal("Ana Diaz", (await RowOf(page, unique).Locator(".membership-row__name").TextContentAsync() ?? string.Empty).Trim());
        }
    }

    /// <summary>
    /// With the look-alike folding rule, names that look the same at avatar size collide too - "Ann" and "Ann " (a trailing
    /// space), and "ann" and "Ann" - so each row of a look-alike pair carries a discriminator, the name is shown as its owner
    /// wrote it, and no label carries an <c>@</c>. "Ann" and "Anne" stay bare: the fold is not a similarity match.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR34")]
    [Trait("Requirement", "AD-23")]
    public async Task Look_alike_names_in_the_Members_list_each_carry_a_discriminator()
    {
        var api = TwoSpaces("owner");
        var (spaced, plain, lower) = (Row("Ann ", "member"), Row("Ann", "viewer"), Row("ann", "member"));
        var distinct = Row("Anne", "member");

        api.SpaceMemberships[Northwind] = [Row("Olu Owner", "owner", isYou: true), spaced, plain, lower, distinct];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await RowOf(page, distinct).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var labels = new List<string>();

            foreach (var alike in new[] { spaced, plain, lower })
            {
                labels.Add(await RowOf(page, alike).Locator(".membership-row__name").TextContentAsync() ?? string.Empty);
            }

            Assert.Equal(3, labels.Distinct(StringComparer.Ordinal).Count());
            Assert.All(labels, label => Assert.Matches("^\\s*[Aa]nn\\s* · [0-9A-F]{4}\\s*$", label));
            Assert.All(labels, label => Assert.DoesNotContain("@", label, StringComparison.Ordinal));
            Assert.Equal("Anne", (await RowOf(page, distinct).Locator(".membership-row__name").TextContentAsync() ?? string.Empty).Trim());
        }
    }

    /// <summary>
    /// AC1, UX-DR27, Lee's Q4 (a) - a Member and a Viewer have no Members section: Space settings reached by hand shows none,
    /// and the page never asks for the list. Anchored in the same case: as Owner of Northwind the list was read and shown.
    /// </summary>
    /// <param name="role">The lesser Role.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("member")]
    [InlineData("viewer")]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "AD-23")]
    public async Task A_Member_and_a_Viewer_never_ask_for_or_see_the_Members_list(string role)
    {
        var api = TwoSpaces("owner");
        var shown = Row("Mo Member", "member");

        api.Roles[Harbour] = role;
        api.SpaceMemberships[Northwind] = [Row("Olu Owner", "owner", isYou: true), shown];
        api.SpaceMemberships[Harbour] = [Row("Hal Host", "owner"), Row("Hidden Person", "member")];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await RowOf(page, shown).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.NotEmpty(api.MembershipListReads(Northwind));

            await ChooseInSwitcherAsync(page, "Harbour Ops");
            await page.Locator(ContextBar, new PageLocatorOptions { HasText = "Harbour Ops" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.EvaluateAsync("path => Blazor.navigateTo(path)", $"/spaces/{Harbour:D}/settings");
            await page.Locator("main h1").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Section).CountAsync());
            Assert.Empty(api.MembershipListReads(Harbour));
            Assert.DoesNotContain("Hidden Person", await page.Locator("main").TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Ledger <c>deferred-work.md:762</c>, Q14 (a), UX-DR39 - while a row action's answer is held, focus is never on
    /// <c>body</c>: the in-flight status element that replaces the confirming button takes focus. One case per row type -
    /// the Membership change, the Invitation revoke, the Label delete and the API Token revoke - each held, asserted and
    /// released; each was red under the old markup (the dev phase records it). Plant 18: the status span without focus.
    /// </summary>
    /// <param name="row">Which row type.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("membership")]
    [InlineData("invitation")]
    [InlineData("label")]
    [InlineData("api-token")]
    [Trait("Requirement", "UX-DR39")]
    public async Task Focus_never_falls_to_body_while_a_row_action_is_in_flight(string row)
    {
        var api = TwoSpaces("owner");
        var member = Row("Mo Member", "member");
        var invitation = new ListedInvitation(Guid.NewGuid(), $"invitee-{Guid.NewGuid():N}@example.test", "member", "Olu Owner", DateTimeOffset.UtcNow.AddDays(-1));
        var labelId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();

        api.SpaceMemberships[Northwind] = [Row("Olu Owner", "owner", isYou: true), member];
        api.PendingInvitations[Northwind] = [invitation];
        api.Labels[Northwind] = [new FakeLabel(labelId, "Unused", "rust")];
        api.ApiTokens.Add(new FakeApiToken(tokenId, "nightly export", Northwind, "Northwind Redesign", DateTimeOffset.UtcNow));

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            Action? release = null;

            switch (row)
            {
                case "membership":
                {
                    var held = api.HoldRoleChangeAsync();

                    await ChooseRoleAsync(page, member, "Viewer");
                    await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);
                    release = api.ReleaseRoleChange;
                    break;
                }

                case "invitation":
                {
                    var held = api.HoldRevokeAsync();
                    var revoking = page.Locator("main .invitation-section li", new PageLocatorOptions { HasText = invitation.EmailAddress });

                    await revoking.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Revoke", Exact = true }).ClickAsync();
                    await revoking.Locator(".button-danger").ClickAsync();
                    await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);
                    release = api.ReleaseRevoke;
                    break;
                }

                case "label":
                {
                    var (arrived, releaseHeld) = await HoldNextAsync(page, "DELETE", LabelRoutes.LabelOf(Northwind, labelId));
                    var deleting = page.Locator("main li", new PageLocatorOptions { HasText = "Unused" });

                    await deleting.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete Label" }).ClickAsync();
                    await page.Locator(".destructive-confirm .button-danger").ClickAsync();
                    await arrived.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);
                    release = releaseHeld;
                    break;
                }

                case "api-token":
                {
                    await page.EvaluateAsync("path => Blazor.navigateTo(path)", "/account");
                    await page.GetByText("nightly export").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

                    var (arrived, releaseHeld) = await HoldNextAsync(page, "DELETE", ApiTokenRoutes.RevokeOf(tokenId));

                    await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Revoke" }).First.ClickAsync();
                    await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Revoke" }).Last.ClickAsync();
                    await arrived.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);
                    release = releaseHeld;
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(row), row, "Not a row type this case knows.");
            }

            var focused = await page.EvaluateAsync<string>("() => document.activeElement ? document.activeElement.tagName + '.' + document.activeElement.className : 'none'");

            release();

            Assert.False(focused.StartsWith("BODY", StringComparison.Ordinal), $"While the {row} action was in flight, focus was on {focused}.");
        }
    }

    /// <summary>
    /// The Role narration under the context bar is in the first
    /// rendered DOM <b>empty</b>, and holds the narrated sentence afterwards, so a screen reader announces it (the
    /// <c>BoardView</c> announcement-region pattern). Reached through the AC10 narrated exit. 3.2's purge behaviour is
    /// unchanged. Plant 17: the region rendered filled.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR29")]
    public async Task The_Role_narration_region_is_inserted_empty_and_filled_after_the_first_render()
    {
        var api = TwoSpaces("admin");
        var member = Row("Mo Member", "member");

        api.SpaceMemberships[Northwind] = [Row("Olu Owner", "owner"), Row("Ada Admin", "admin", isYou: true), member];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.AddInitScriptAsync(RecordNarrationAtInsert);
            await OpenSettingsAsync(api, page, Northwind);
            await ChangeRoleButton(RowOf(page, member)).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            api.Roles[Northwind] = "member";
            api.NextRoleChangeRefusal = (403, ProblemTypes.InsufficientRole, MembershipWire.CapabilityCode);

            await ChooseRoleAsync(page, member, "Viewer");
            await page.Locator(".role-narration", new PageLocatorOptions { HasText = "Space settings is no longer available." })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var atInsert = await page.EvaluateAsync<string[]>("() => window.__narrationAtInsert");

            Assert.True(atInsert.Length > 0 && Array.TrueForAll(atInsert, text => text.Length == 0), $"The narration region was inserted holding [{string.Join(" | ", atInsert)}]; it must be inserted empty and filled after.");
        }
    }

    /// <summary>
    /// Ledger <c>deferred-work.md:546</c>, Q16 (a) - deleting a Label already gone (answered the boundary <c>404</c>) re-reads
    /// the Label list; the list answers without it, so the row is dropped and "That Label no longer exists." is announced,
    /// and the Space refusal surface is <b>not</b> raised (the page stays on Space settings with its sections). The Task and
    /// Project deletions are unchanged (their own cases in <c>DeleteTests</c> stay green).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR39")]
    public async Task A_Label_delete_answered_404_rereads_the_list_drops_the_row_and_announces()
    {
        var api = TwoSpaces("owner");
        var labelId = Guid.NewGuid();

        api.Labels[Northwind] = [new FakeLabel(labelId, "Gone elsewhere", "rust")];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await page.EvaluateAsync(RecordEverythingSaid);

            var row = page.Locator("main li", new PageLocatorOptions { HasText = "Gone elsewhere" });

            await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete Label" }).ClickAsync();

            var reads = api.Requests.Count(request => request.Method == "GET" && request.Path == LabelRoutes.LabelsOf(Northwind));

            api.Labels[Northwind] = [];

            await page.Locator(".destructive-confirm .button-danger").ClickAsync();
            await page.GetByText("That Label no longer exists.").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            Assert.True(api.Requests.Count(request => request.Method == "GET" && request.Path == LabelRoutes.LabelsOf(Northwind)) > reads, "The 404 did not re-read the Label list.");
            Assert.Equal(0, await row.CountAsync());
            Assert.EndsWith($"/spaces/{Northwind:D}/settings", new Uri(page.Url).AbsolutePath, StringComparison.Ordinal);
            Assert.Equal(1, await page.Locator(Section).GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = SectionHeading, Exact = true }).CountAsync());
        }
    }

    /// <summary>
    /// AC10, NFR-9, ledger <c>:20</c> (rolling) - the Members section reflows at 320 by 256 CSS pixels with no sideways
    /// scroll, and keeps doing so under the 1.4.12 text-spacing override, with a long name and a long address in a row.
    /// </summary>
    /// <param name="spaced">Whether the text-spacing override is applied.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "NFR-9")]
    public async Task The_Members_section_reflows_at_320_pixels_with_and_without_text_spacing(bool spaced)
    {
        var api = TwoSpaces("owner");
        var longest = Row("Maximiliana Wolfeschlegelsteinhausenberger-Ortiz", "member") with
        {
            EmailAddress = $"maximiliana.wolfeschlegelsteinhausenberger-{Guid.NewGuid():N}@a-rather-long-domain.example.test",
        };

        api.SpaceMemberships[Northwind] = [Row("Olu Owner", "owner", isYou: true), longest];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(320, 256);
            await OpenSettingsAsync(api, page, Northwind);
            await RowOf(page, longest).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            if (spaced)
            {
                await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = TextSpacingOverride });
            }

            Assert.Empty(await page.EvaluateAsync<string[]>(Reflow));
        }
    }

    /// <summary>
    /// The Owner sees a Remove control on every row but its own - Admin, Member and Viewer alike - and its own row carries
    /// none. The control is named for the row's label.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "UX-DR27")]
    public async Task The_Owner_sees_Remove_on_every_row_but_its_own()
    {
        var api = TwoSpaces("owner");
        var (owner, admin, member, viewer) = (Row("Olu Owner", "owner", isYou: true), Row("Ada Admin", "admin"), Row("Mo Member", "member"), Row("Vi Viewer", "viewer"));

        api.SpaceMemberships[Northwind] = [owner, admin, member, viewer];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await RowOf(page, viewer).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await RemoveButton(page, owner).CountAsync());

            foreach (var other in new[] { admin, member, viewer })
            {
                Assert.Equal(1, await RemoveButton(page, other).CountAsync());
            }
        }
    }

    /// <summary>
    /// An Admin sees Remove on Member and Viewer rows only: none on the Owner's row, another Admin's or its own, and those
    /// controls are absent rather than disabled - no element in the section carries <c>disabled</c> or
    /// <c>aria-disabled</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Assumption", "PRD-12-4")]
    public async Task An_Admin_sees_Remove_on_Member_and_Viewer_rows_only_and_nothing_disabled()
    {
        var api = TwoSpaces("admin");
        var (owner, self, otherAdmin, member, viewer) = (Row("Olu Owner", "owner"), Row("Ada Admin", "admin", isYou: true), Row("Abe Admin", "admin"), Row("Mo Member", "member"), Row("Vi Viewer", "viewer"));

        api.SpaceMemberships[Northwind] = [owner, self, otherAdmin, member, viewer];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await RowOf(page, viewer).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(1, await RemoveButton(page, member).CountAsync());
            Assert.Equal(1, await RemoveButton(page, viewer).CountAsync());

            foreach (var fixedRow in new[] { owner, self, otherAdmin })
            {
                Assert.Equal(0, await RemoveButton(page, fixedRow).CountAsync());
            }

            Assert.Equal(0, await page.Locator($"{Section} [disabled], {Section} [aria-disabled]").CountAsync());
        }
    }

    /// <summary>
    /// The removal confirm sits on the blast-radius ladder between Task and Project: it replaces the row's controls in place
    /// (no dialog), names the person by the row's label and never by address, says what removal does, and states whether
    /// they are in this Space now - read fresh when the confirm opens, never taken from the list. Neither "Remove" nor
    /// "Cancel" is focused as a default: focus goes to the confirm's text. Esc restores the Remove control, focused, and
    /// sends nothing.
    /// </summary>
    /// <param name="inSpaceNow">What the single-row read answers.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "UX-DR36")]
    [Trait("Requirement", "UX-DR11")]
    [Trait("Requirement", "AD-23")]
    public async Task The_remove_confirm_names_the_person_states_whether_they_are_here_now_and_has_no_default(bool inSpaceNow)
    {
        var api = TwoSpaces("owner");
        var member = Row("Mo Member", "member");

        api.SpaceMemberships[Northwind] = [Row("Olu Owner", "owner", isYou: true), member];
        api.MembershipPresence[member.Id] = inSpaceNow;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await RemoveButton(page, member).ClickAsync();

            var row = RowOf(page, member);
            var live = inSpaceNow ? "Mo Member is in this Space now." : "Mo Member has not been in this Space recently.";

            await row.GetByText(live).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var text = await row.TextContentAsync() ?? string.Empty;

            Assert.Contains("Remove Mo Member from this Space? They lose access to everything in it at once.", text, StringComparison.Ordinal);
            Assert.DoesNotContain("@", (await row.Locator(".destructive-confirm").TextContentAsync()) ?? string.Empty, StringComparison.Ordinal);
            Assert.Equal(0, await page.Locator("[role=dialog], dialog[open]").CountAsync());
            Assert.Single(api.MembershipReads(Northwind));
            Assert.False(
                await page.EvaluateAsync<bool>("() => !!document.activeElement && document.activeElement.tagName === 'BUTTON'"),
                "A button holds focus as the confirm's default; focus belongs on the confirm's text.");

            await page.Keyboard.PressAsync("Escape");
            await RemoveButton(page, member).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.True(await RemoveButton(page, member).EvaluateAsync<bool>("e => e === document.activeElement"), "Esc did not return focus to the Remove control.");
            Assert.Empty(api.Removals(Northwind));
        }
    }

    /// <summary>
    /// A removal is sent once, with an <c>Idempotency-Key</c>, and is never optimistic: the row stays while the answer is
    /// held. On <c>204</c> the row is dropped from the rows shown without re-reading the list, the shown total falls by one,
    /// "{label} was removed." is announced with no address anywhere in what was said, and focus moves to the next row's first
    /// control, never to <c>body</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR38")]
    [Trait("Requirement", "UX-DR39")]
    [Trait("Requirement", "AD-29")]
    [Trait("Requirement", "AD-23")]
    public async Task A_removal_is_sent_once_with_a_key_drops_the_row_and_moves_focus_to_the_next_row()
    {
        var api = TwoSpaces("owner");
        var (member, viewer) = (Row("Mo Member", "member"), Row("Vi Viewer", "viewer"));

        api.SpaceMemberships[Northwind] = [Row("Olu Owner", "owner", isYou: true), member, viewer];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await page.EvaluateAsync(RecordEverythingSaid);

            var listReads = api.MembershipListReads(Northwind).Count;
            var held = api.HoldRemovalAsync();

            await ConfirmRemovalAsync(page, member);
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            Assert.Equal(1, await RowOf(page, member).CountAsync());

            api.ReleaseRemoval();

            await page.GetByText("Mo Member was removed.").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            var removal = Assert.Single(api.Removals(Northwind));

            Assert.False(string.IsNullOrEmpty(removal.Header(Idempotency.HeaderName)), "The removal carried no Idempotency-Key.");
            Assert.Equal(0, await RowOf(page, member).CountAsync());
            Assert.Equal(listReads, api.MembershipListReads(Northwind).Count);
            Assert.Contains("2 of 100", await page.Locator(Section).TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(await SaidAsync(page), said => said.Contains('@', StringComparison.Ordinal));
            Assert.True(await RowOf(page, viewer).EvaluateAsync<bool>("e => e.contains(document.activeElement)"), "Focus did not move to the next row after the removal.");
        }
    }

    /// <summary>
    /// An Admin refused <c>403</c> while it still holds Admin - the target became an Admin meanwhile - re-reads its Role,
    /// stays on Space settings and is told what Admins may remove: "Admins can remove Members and Viewers only." The row
    /// stays.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR29")]
    [Trait("Assumption", "PRD-12-4")]
    public async Task A_removal_refused_403_to_an_Admin_still_holding_Admin_says_what_Admins_may_remove()
    {
        var api = TwoSpaces("admin");
        var member = Row("Mo Member", "member");

        api.SpaceMemberships[Northwind] = [Row("Olu Owner", "owner"), Row("Ada Admin", "admin", isYou: true), member];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            api.NextRemovalRefusal = (403, ProblemTypes.InsufficientRole, MembershipWire.RemoveCapabilityCode);

            await ConfirmRemovalAsync(page, member);
            await page.Locator(Section).GetByText("Admins can remove Members and Viewers only.").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.EndsWith($"/spaces/{Northwind:D}/settings", new Uri(page.Url).AbsolutePath, StringComparison.Ordinal);
            Assert.Equal(1, await RowOf(page, member).CountAsync());
        }
    }

    /// <summary>
    /// A row action answered <c>404</c> on the second page re-reads the first page only to learn whether the Space still
    /// answers; it does, so that one row is dropped from the rows shown, every row of the appended page stays, the total
    /// comes from the fresh read, "That Membership no longer exists." is announced, and focus is not on <c>body</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-29")]
    [Trait("Requirement", "UX-DR39")]
    public async Task A_row_on_the_second_page_answered_404_is_dropped_and_the_appended_page_stays()
    {
        var api = TwoSpaces("owner");
        var rows = Enumerable.Range(0, 100).Select(index => index == 0 ? Row("Olu Owner", "owner", isYou: true) : Row($"Member {index:D3}", "member")).ToList();

        api.SpaceMemberships[Northwind] = rows;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await RowOf(page, rows[49]).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.Locator(Section).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = ShowMore, Exact = true }).ClickAsync();
            await RowOf(page, rows[99]).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var gone = rows[75];

            await RemoveButton(page, gone).ClickAsync();
            await RowOf(page, gone).Locator(".destructive-confirm").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            // Gone elsewhere between the confirm opening and the answer.
            api.SpaceMemberships[Northwind] = [.. rows.Where(row => row.Id != gone.Id)];

            await RowOf(page, gone).Locator(".button-danger").ClickAsync();
            await page.GetByText("That Membership no longer exists.").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });

            Assert.Equal(0, await RowOf(page, gone).CountAsync());
            Assert.Equal(99, await page.Locator(Rows).CountAsync());
            Assert.Equal(1, await RowOf(page, rows[76]).CountAsync());
            Assert.Equal(1, await RowOf(page, rows[99]).CountAsync());
            Assert.Contains("99 of 100", await page.Locator(Section).TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
            Assert.False(await page.EvaluateAsync<bool>("() => document.activeElement === document.body"), "Focus fell to body after the 404.");
        }
    }

    /// <summary>
    /// Row actions are per row: with one row's Role change in flight, another row's removal is sent at once, not dropped.
    /// Both are answered and both rows reflect their answers - the first shows its new Role, the second is gone. The server
    /// serialises them on its own lock.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR39")]
    public async Task A_second_rows_action_proceeds_while_another_rows_is_in_flight()
    {
        var api = TwoSpaces("owner");
        var (changed, removed) = (Row("Mo Member", "member"), Row("Vi Viewer", "viewer"));

        api.SpaceMemberships[Northwind] = [Row("Olu Owner", "owner", isYou: true), changed, removed];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);

            var changeHeld = api.HoldRoleChangeAsync();
            var removalHeld = api.HoldRemovalAsync();

            await ChooseRoleAsync(page, changed, "Viewer");
            await changeHeld.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);
            await ConfirmRemovalAsync(page, removed);
            await removalHeld.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            api.ReleaseRoleChange();
            api.ReleaseRemoval();

            await page.GetByText("Mo Member is now a Viewer.").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = Timeout });
            await RowOf(page, removed).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.Single(api.RoleChanges(Northwind));
            Assert.Single(api.Removals(Northwind));
        }
    }

    /// <summary>
    /// The removal confirm reflows at 320 by 256 CSS pixels with no sideways scroll, with and without the 1.4.12
    /// text-spacing override, for a long name.
    /// </summary>
    /// <param name="spaced">Whether the text-spacing override is applied.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "NFR-9")]
    public async Task The_remove_confirm_reflows_at_320_pixels_with_and_without_text_spacing(bool spaced)
    {
        var api = TwoSpaces("owner");
        var longest = Row("Maximiliana Wolfeschlegelsteinhausenberger-Ortiz", "member");

        api.SpaceMemberships[Northwind] = [Row("Olu Owner", "owner", isYou: true), longest];
        api.MembershipPresence[longest.Id] = true;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(320, 256);
            await OpenSettingsAsync(api, page, Northwind);
            await RemoveButton(page, longest).ClickAsync();
            await RowOf(page, longest).Locator(".destructive-confirm").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            if (spaced)
            {
                await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = TextSpacingOverride });
            }

            Assert.Empty(await page.EvaluateAsync<string[]>(Reflow));
        }
    }

    /// <summary>
    /// An Admin demoted to Member meanwhile presses Remove on a Member's row; the confirm's fresh read answers
    /// <c>403 insufficient-role</c> carrying <c>member</c>. The Role is reconciled before the refusal's copy is chosen, so
    /// neither Admin-limit sentence ever enters the page: the narrated exit to Space home runs instead. Plant: choose the
    /// copy from the stale Role first - the log then holds the Admin limit.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR29")]
    [Trait("Requirement", "NFR-2")]
    public async Task A_demoted_Admins_refused_confirm_read_never_shows_the_Admin_limit()
    {
        var api = TwoSpaces("admin");
        var member = Row("Mo Member", "member");

        api.SpaceMemberships[Northwind] = [Row("Olu Owner", "owner"), Row("Ada Admin", "admin", isYou: true), member];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await RemoveButton(page, member).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.EvaluateAsync(
                """
                limits => {
                    window.__limits = [];
                    new MutationObserver(() => {
                        const text = document.body.textContent;
                        for (const limit of limits) if (text.includes(limit)) window.__limits.push(limit);
                    }).observe(document.body, { childList: true, subtree: true, characterData: true });
                }
                """,
                AdminLimits);

            api.Roles[Northwind] = "member";

            await RemoveButton(page, member).ClickAsync();
            await page.Locator(".role-narration", new PageLocatorOptions { HasText = "You're now a Member. Space settings is no longer available." })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.WaitForFunctionAsync("path => location.pathname === path", $"/spaces/{Northwind:D}", new PageWaitForFunctionOptions { Timeout = Timeout, PollingInterval = 50 });

            Assert.Empty(await page.EvaluateAsync<string[]>("() => window.__limits"));
        }
    }

    /// <summary>
    /// A late refused confirm read for one row never closes another row's confirm: Remove on row A is pressed and its fresh
    /// read held; Remove on row B opens B's confirm; then A's read is released as a <c>403</c> under the same Admin Role. B's
    /// confirm is still open, and its Remove still there.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR36")]
    public async Task A_late_refused_confirm_read_never_closes_another_rows_confirm()
    {
        var api = TwoSpaces("admin");
        var (first, second) = (Row("Mo Member", "member"), Row("Vi Viewer", "viewer"));

        api.SpaceMemberships[Northwind] = [Row("Olu Owner", "owner"), Row("Ada Admin", "admin", isYou: true), first, second];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Northwind);
            await RemoveButton(page, first).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var held = api.HoldSpaceReadAsync(Northwind, $"/memberships/{first.Id:D}");

            await RemoveButton(page, first).ClickAsync();
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            await RemoveButton(page, second).ClickAsync();

            var secondConfirm = RowOf(page, second).Locator(".destructive-confirm");

            await secondConfirm.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            api.ReleaseSpaceRead((403, $$"""{"type":"{{ProblemTypes.InsufficientRole}}","capability":"{{MembershipWire.CapabilityCode}}"}"""));

            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            Assert.True(await secondConfirm.IsVisibleAsync(), "A's late refusal closed B's confirm.");
            Assert.Equal(1, await secondConfirm.Locator(".button-danger").CountAsync());
        }
    }

    /// <summary>
    /// A row's Remove control, named for the row's label (every test name here is unique, so the label is the name).
    /// </summary>
    private static ILocator RemoveButton(IPage page, ListedMembership membership) =>
        RowOf(page, membership).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = $"Remove {membership.DisplayName}", Exact = true });

    /// <summary>
    /// Opens the row's removal confirm and presses its "Remove".
    /// </summary>
    private static async Task ConfirmRemovalAsync(IPage page, ListedMembership membership)
    {
        await RemoveButton(page, membership).ClickAsync();

        var confirm = RowOf(page, membership).Locator(".destructive-confirm");

        await confirm.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await confirm.Locator(".button-danger").ClickAsync();
    }

    /// <summary>
    /// A signed-in Account holding <paramref name="northwindRole"/> in Northwind Redesign and Member of Harbour Ops.
    /// </summary>
    private static FakeYelloApi TwoSpaces(string northwindRole)
    {
        var api = new FakeYelloApi { SignedIn = true };

        api.Switcher.Add((Northwind, "Northwind Redesign"));
        api.Switcher.Add((Harbour, "Harbour Ops"));
        api.Roles[Northwind] = northwindRole;
        api.Roles[Harbour] = "member";

        return api;
    }

    /// <summary>
    /// A Membership as the list sends it, with a fresh address (never a shared one).
    /// </summary>
    private static ListedMembership Row(string displayName, string role, bool isYou = false) =>
        new(Guid.NewGuid(), displayName, $"member-{Guid.NewGuid():N}@example.test", role, isYou);

    private static ILocator RowOf(IPage page, ListedMembership membership) =>
        page.Locator(Rows, new PageLocatorOptions { HasText = membership.EmailAddress });

    private static ILocator ChangeRoleButton(ILocator row) =>
        row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = ChangeRole, Exact = true });

    private static async Task<string[]> RadioNamesAsync(ILocator picker)
    {
        var names = new List<string>();

        foreach (var radio in await picker.GetByRole(AriaRole.Radio).AllAsync())
        {
            names.Add((await radio.EvaluateAsync<string>("e => (e.labels && e.labels[0] ? e.labels[0].textContent : e.getAttribute('aria-label') || '').trim()")).Trim());
        }

        return [.. names];
    }

    /// <summary>
    /// Opens the row's picker, checks <paramref name="roleName"/> and presses "Change".
    /// </summary>
    private static async Task ChooseRoleAsync(IPage page, ListedMembership membership, string roleName)
    {
        var row = RowOf(page, membership);

        await ChangeRoleButton(row).ClickAsync();
        await row.GetByRole(AriaRole.Radio, new LocatorGetByRoleOptions { Name = roleName, Exact = true }).CheckAsync();
        await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = ChangeAction, Exact = true }).ClickAsync();
    }

    private static async Task<string[]> SaidAsync(IPage page) =>
        await page.EvaluateAsync<string[]>("() => window.__said || []");

    /// <summary>
    /// Holds the next request with <paramref name="method"/> to <paramref name="path"/> at the network edge, ahead of the fake,
    /// until released - for the rows whose action the fake has no hold for (the Label delete, the Token revoke). Playwright
    /// runs the most recently registered handler first; the release falls back to the fake.
    /// </summary>
    private static async Task<(Task Arrived, Action Release)> HoldNextAsync(IPage page, string method, string path)
    {
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var taken = 0;

        await page.RouteAsync("**/api/v1/**", async route =>
        {
            if (route.Request.Method == method && new Uri(route.Request.Url).AbsolutePath == path && Interlocked.Exchange(ref taken, 1) == 0)
            {
                arrived.TrySetResult();
                await released.Task;
            }

            await route.FallbackAsync();
        });

        return (arrived.Task, () => released.TrySetResult());
    }

    private static async Task ChooseInSwitcherAsync(IPage page, string name)
    {
        var trigger = page.Locator(SwitcherTrigger);
        var list = await trigger.GetAttributeAsync("aria-controls");

        Assert.False(string.IsNullOrEmpty(list), "The switcher trigger names no list in aria-controls.");

        if (await trigger.GetAttributeAsync("aria-expanded") != "true")
        {
            await trigger.ClickAsync();
        }

        await page.Locator($"[id='{list}'] a", new PageLocatorOptions { HasText = name }).ClickAsync();
    }

    private string Address(string path) => new Uri(surface.BaseAddress, path).ToString();

    /// <summary>
    /// Opens the Space, follows the context bar's "Space settings" link and waits for the Members heading.
    /// </summary>
    private async Task OpenSettingsAsync(FakeYelloApi api, IPage page, Guid spaceId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address($"spaces/{spaceId:D}"));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.Locator(ContextBar).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = SettingsLink, Exact = true }).ClickAsync();
        await page.Locator(Section).GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = SectionHeading, Exact = true })
            .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }
}
