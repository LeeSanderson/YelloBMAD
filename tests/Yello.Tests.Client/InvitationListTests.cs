using Microsoft.Playwright;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// Story 4.2 in a real browser: the pending Invitation list in Space settings' Invitations section - each row's address,
/// Role, "Sent by" name and date; Revoke on every row for an Owner and an Admin, asked first in place, never optimistic,
/// announced without an address; a <c>409</c> re-read; the list re-read after an issue; the empty state; the purge on
/// leaving the Space; and the 320px reflow. AC9 (and AC1, AC2, AC6 as the client shows them); FR-12, UX-DR12, UX-DR14,
/// UX-DR26, UX-DR27, UX-DR31, UX-DR38, UX-DR39, UX-DR41, NFR-9.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 4.2 ATDD run (2026-11-01).</b> Each was skipped with AC9 and what it
/// waits for. The API is <see cref="FakeYelloApi"/> at the network edge, taught by this run the list and the revoke
/// (<c>FakeYelloApi.Invitations.cs</c>); no Host, no database, no engine. Build <c>Yello.Client</c> in Debug before running
/// them. Same collection as <see cref="InvitationTests"/>, so no new fixture.
/// </para>
/// <para>
/// <b>Locators</b> were the ATDD run's assumptions, and the markup Task 6 built matches them unchanged: the
/// section is 4.1's <c>main .invitation-section</c>; the list is a <c>ul</c> in it under a heading "Pending invitations",
/// each row an <c>li</c> (<c>main .invitation-section li</c> - 4.1's form holds no list item); the row's Revoke is a button
/// named "Revoke" whose <c>aria-describedby</c> names the element holding the address; the confirm replaces it in place with
/// the warning, a danger "Revoke" and a "Cancel"; the date is a <c>time[datetime]</c>. Copy is Q15 (a)'s, asserted as
/// written. Text is read with <c>textContent</c>, never <c>innerText</c>, so CSS upper-casing (UX-DR42) cannot change it.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-12")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class InvitationListTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string ContextBar = "nav.context-bar";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string SwitcherTrigger = "nav.context-bar button[aria-controls]";
    private const string Section = "main .invitation-section";
    private const string Rows = "main .invitation-section li";

    private const string SettingsLink = "Space settings";
    private const string SectionHeading = "Invitations";
    private const string ListHeading = "Pending invitations";
    private const string RevokeAction = "Revoke";
    private const string CancelAction = "Cancel";
    private const string ConfirmCopy = "Revoke this invitation? The email already sent cannot be recalled.";
    private const string RevokedCopy = "Invitation revoked.";
    private const string NotPendingCopy = "This invitation is no longer pending.";
    private const string EmptyCopy = "No pending invitations.";
    private const string EmailLabel = "Email address";
    private const string RoleLegend = "Role";
    private const string SendAction = "Send invitation";

    private const string TextSpacingOverride =
        "* { line-height: 1.5 !important; letter-spacing: 0.12em !important; word-spacing: 0.16em !important; } "
        + "p { margin-block-end: 2em !important; }";

    /// <summary>
    /// A 1.4.10 reflow probe, as <c>InvitationTests.Reflow</c>.
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
    /// Records every non-empty text a polite or status region changes to: <c>window.__said</c>. As
    /// <c>InvitationTests.RecordEverythingSaid</c>.
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
    /// The text of every element <c>aria-describedby</c> names on <c>e</c>.
    /// </summary>
    private const string DescribedText =
        "e => (e.getAttribute('aria-describedby') || '').split(' ').filter(Boolean).map(id => (document.getElementById(id) || {}).textContent || '').join(' ')";

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();

    /// <summary>
    /// AC9, AC1, AC2, UX-DR14, Q6 (a), Q15 (a) - an Owner and an Admin see each pending Invitation as a row: its address,
    /// its Role's display name, "Sent by {issuer}" and its issue date in a <c>time</c> element whose <c>datetime</c> is the
    /// instant the server sent; every row - the one the Owner issued included - offers "Revoke", and each Revoke is
    /// described by its row's address, so several are not identical controls to a screen reader.
    /// </summary>
    /// <param name="role">The caller's Role.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("owner")]
    [InlineData("admin")]
    [Trait("Requirement", "UX-DR14")]
    [Trait("Requirement", "UX-DR27")]
    public async Task An_Owner_and_an_Admin_see_each_pending_Invitations_address_Role_issuer_and_date(string role)
    {
        var api = TwoSpaces(role);
        var (byOwner, byAdmin) = (Listed("member", "Olu Owner"), Listed("viewer", "Ada Admin"));

        api.PendingInvitations[Northwind] = [byAdmin, byOwner];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await OpenSettingsAsync(api, page, Northwind);
            await Row(page, byOwner).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(1, await page.Locator(Section).GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = ListHeading, Exact = true }).CountAsync());
            Assert.Equal(2, await page.Locator(Rows).CountAsync());

            foreach (var (listed, roleName) in new[] { (byOwner, "Member"), (byAdmin, "Viewer") })
            {
                var row = Row(page, listed);
                var text = await row.TextContentAsync() ?? string.Empty;

                Assert.Contains(roleName, text, StringComparison.Ordinal);
                Assert.Contains($"Sent by {listed.IssuerName}", text, StringComparison.Ordinal);
                Assert.Equal(listed.IssuedAt, DateTimeOffset.Parse(await row.Locator("time").GetAttributeAsync("datetime") ?? string.Empty, System.Globalization.CultureInfo.InvariantCulture));

                var revoke = row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = RevokeAction, Exact = true });

                Assert.Equal(1, await revoke.CountAsync());
                Assert.Contains(listed.EmailAddress, await revoke.EvaluateAsync<string>(DescribedText), StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// AC9, AC1, UX-DR27, Q16 (a) - a Member and a Viewer have no Invitation list: the context bar offers no Space settings
    /// link, the page never asks for the list, and Space settings reached by hand shows no row and no Revoke. Anchored in the same case: as Admin of Northwind the list was
    /// read and shown. Plant: the section rendered for a Member.
    /// </summary>
    /// <param name="role">The lesser Role.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("member")]
    [InlineData("viewer")]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "AD-23")]
    public async Task A_Member_and_a_Viewer_never_ask_for_or_see_the_Invitation_list(string role)
    {
        var api = TwoSpaces("admin");
        var shown = Listed("member", "Olu Owner");
        var hidden = Listed("admin", "Olu Owner");

        api.Roles[Harbour] = role;
        api.PendingInvitations[Northwind] = [shown];
        api.PendingInvitations[Harbour] = [hidden];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await OpenSettingsAsync(api, page, Northwind);
            await Row(page, shown).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.NotEmpty(api.InvitationListReads(Northwind));

            await ChooseInSwitcherAsync(page, "Harbour Ops");
            await page.Locator(ContextBar, new PageLocatorOptions { HasText = "Harbour Ops" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator(ContextBar).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = SettingsLink, Exact = true }).CountAsync());

            await page.EvaluateAsync("path => Blazor.navigateTo(path)", $"/spaces/{Harbour:D}/settings");
            await page.Locator("main h1").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Rows).CountAsync());
            Assert.Equal(0, await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = RevokeAction }).CountAsync());
            Assert.DoesNotContain(hidden.EmailAddress, await page.EvaluateAsync<string>("() => document.body.textContent"), StringComparison.Ordinal);
            Assert.Empty(api.InvitationListReads(Harbour));
        }
    }

    /// <summary>
    /// AC9, Q9 (a), UX-DR12, UX-DR26 - Revoke asks first, in place: the row's control is replaced by the warning, a danger
    /// "Revoke" and "Cancel" - never a dialog - and nothing is sent; Cancel sends nothing, restores the row and returns
    /// focus to that row's Revoke.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR12")]
    [Trait("Requirement", "UX-DR26")]
    public async Task Revoke_asks_first_in_place_and_Cancel_sends_nothing_and_returns_focus()
    {
        var api = TwoSpaces("owner");
        var listed = Listed("member", "Olu Owner");

        api.PendingInvitations[Northwind] = [listed, Listed("viewer", "Olu Owner")];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await OpenSettingsAsync(api, page, Northwind);

            var row = Row(page, listed);

            await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = RevokeAction, Exact = true }).ClickAsync();
            await row.GetByText(ConfirmCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(1, await row.Locator(".button-danger").CountAsync());
            Assert.Equal(1, await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = CancelAction, Exact = true }).CountAsync());
            Assert.Equal(0, await page.GetByRole(AriaRole.Dialog).CountAsync());
            Assert.Empty(api.InvitationRevokes(Northwind));

            await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = CancelAction, Exact = true }).ClickAsync();
            await row.GetByText(ConfirmCopy).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });
            await page.WaitForFunctionAsync(
                "address => { const e = document.activeElement; return !!e && e.tagName === 'BUTTON' && !!e.closest('li') && e.closest('li').textContent.includes(address); }",
                listed.EmailAddress,
                new PageWaitForFunctionOptions { Timeout = Timeout });

            Assert.Empty(api.InvitationRevokes(Northwind));
            Assert.Equal(2, await page.Locator(Rows).CountAsync());
        }
    }

    /// <summary>
    /// AC9, AC6, AD-18, UX-DR26 - confirming sends one <c>DELETE</c> to the Invitation's path under an
    /// <c>Idempotency-Key</c>; not optimistic: while the answer is held the row is still listed, and it leaves only after the
    /// <c>204</c>. Plant 12: the row removed before the answer.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "UX-DR26")]
    public async Task Confirming_sends_DELETE_under_a_key_and_the_row_stays_until_the_204()
    {
        var api = TwoSpaces("admin");
        var listed = Listed("member", "Olu Owner");

        api.PendingInvitations[Northwind] = [listed];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await OpenSettingsAsync(api, page, Northwind);

            var held = api.HoldRevokeAsync();

            await ConfirmRevokeAsync(page, listed);
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            Assert.Equal(1, await Row(page, listed).CountAsync());

            var sent = Assert.Single(api.InvitationRevokes(Northwind));

            Assert.Equal(FakeYelloApi.InvitationPathOf(Northwind, listed.Id), sent.Path);
            Assert.False(string.IsNullOrWhiteSpace(sent.Header(Idempotency.HeaderName)), "The revoke carried no Idempotency-Key.");

            api.ReleaseRevoke();

            await Row(page, listed).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });
        }
    }

    /// <summary>
    /// AC9, UX-DR38, UX-DR39 - a successful revoke announces "Invitation revoked." and nothing said names an address (no
    /// <c>@</c>); focus moves to the next row's Revoke (here, where there is one), never to <c>body</c>. Plant 13: the announcement
    /// including the address.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR38")]
    [Trait("Requirement", "UX-DR39")]
    public async Task A_revoke_is_announced_without_an_address_and_focus_never_falls_to_the_body()
    {
        var api = TwoSpaces("owner");
        var (first, next) = (Listed("member", "Olu Owner"), Listed("viewer", "Olu Owner"));

        api.PendingInvitations[Northwind] = [first, next];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await OpenSettingsAsync(api, page, Northwind);
            await page.EvaluateAsync(RecordEverythingSaid);

            await ConfirmRevokeAsync(page, first);
            await Row(page, first).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });
            await WaitUntilAsync(async () => Array.Exists(await SaidAsync(page), sentence => sentence.Contains(RevokedCopy, StringComparison.Ordinal)), "\"Invitation revoked.\" was never announced.");

            Assert.All(await SaidAsync(page), sentence => Assert.DoesNotContain("@", sentence, StringComparison.Ordinal));
            Assert.DoesNotContain("@", string.Join(' ', await page.Locator("[aria-live='polite'], [role='status']").AllTextContentsAsync()), StringComparison.Ordinal);
            Assert.False(await page.EvaluateAsync<bool>("() => !document.activeElement || document.activeElement === document.body"), "Focus fell to the body after the revoke.");

            // The nearest surviving control (UX-DR39): the next row's Revoke.
            await page.WaitForFunctionAsync(
                "address => { const e = document.activeElement; return !!e && e.tagName === 'BUTTON' && !!e.closest('li') && e.closest('li').textContent.includes(address); }",
                next.EmailAddress,
                new PageWaitForFunctionOptions { Timeout = Timeout });
        }
    }

    /// <summary>
    /// AC9, AC4, Q2 (a), UX-DR38 - a <c>409 invitation-not-pending</c> re-reads the list (the row is no longer outstanding)
    /// and says "This invitation is no longer pending." with no address. The fake drops the row while the revoke is held, as
    /// an acceptance committing under it would.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-12")]
    [Trait("Requirement", "UX-DR38")]
    public async Task A_409_on_revoke_rereads_the_list_and_says_the_invitation_is_no_longer_pending()
    {
        var api = TwoSpaces("owner");
        var listed = Listed("member", "Olu Owner");

        api.PendingInvitations[Northwind] = [listed];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await OpenSettingsAsync(api, page, Northwind);
            await page.EvaluateAsync(RecordEverythingSaid);

            var reads = api.InvitationListReads(Northwind).Count;
            var held = api.HoldRevokeAsync();

            await ConfirmRevokeAsync(page, listed);
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            api.WithdrawInvitation(Northwind, listed.Id);

            api.ReleaseRevokeRefused(409, ProblemTypes.InvitationNotPending);

            await WaitUntilAsync(() => api.InvitationListReads(Northwind).Count > reads, "The list was not re-read after the 409.");
            await Row(page, listed).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });
            await WaitUntilAsync(async () => Array.Exists(await SaidAsync(page), sentence => sentence.Contains(NotPendingCopy, StringComparison.Ordinal)), "\"This invitation is no longer pending.\" was never said.");

            Assert.All(await SaidAsync(page), sentence => Assert.DoesNotContain("@", sentence, StringComparison.Ordinal));

            // The row left with the re-read and none took its place: focus goes to the list heading, never body (UX-DR39).
            await page.WaitForFunctionAsync(
                "() => { const e = document.activeElement; return !!e && e.tagName === 'H3'; }",
                null,
                new PageWaitForFunctionOptions { Timeout = Timeout });
        }
    }

    /// <summary>
    /// AC9, 4.1 Q5 - the issue's <c>204</c> has no body, so a successful issue re-reads the list, and the new Invitation
    /// appears once the server has listed it; there is no optimistic row.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-10")]
    [Trait("Requirement", "UX-DR26")]
    public async Task After_a_successful_issue_the_list_rereads_and_shows_the_new_row()
    {
        var api = TwoSpaces("owner");
        var address = $"invitee-{Guid.NewGuid():N}@example.test";

        api.ListIssuedInvitations = true;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await OpenSettingsAsync(api, page, Northwind);
            await page.Locator(Section).GetByText(EmptyCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var reads = api.InvitationListReads(Northwind).Count;
            var section = page.Locator(Section);

            await section.GetByLabel(EmailLabel, new LocatorGetByLabelOptions { Exact = true }).FillAsync(address);
            await section.GetByRole(AriaRole.Group, new LocatorGetByRoleOptions { Name = RoleLegend, Exact = true })
                .GetByRole(AriaRole.Radio, new LocatorGetByRoleOptions { Name = "Member", Exact = true }).CheckAsync();
            await section.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = SendAction, Exact = true }).ClickAsync();

            await page.Locator(Rows, new PageLocatorOptions { HasText = address }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.True(api.InvitationListReads(Northwind).Count > reads, "The list was not re-read after the issue.");
            Assert.Single(api.InvitationsIssued(Northwind));
        }
    }

    /// <summary>
    /// AC9, AC6, Q15 (a), UX-DR26 - a Space with no pending Invitation says "No pending invitations." and nothing else: no
    /// row, no count, no prompt. Anchored: the list was read.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR26")]
    public async Task An_empty_list_says_No_pending_invitations()
    {
        var api = TwoSpaces("admin");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await OpenSettingsAsync(api, page, Northwind);
            await page.Locator(Section).GetByText(EmptyCopy, new LocatorGetByTextOptions { Exact = true }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.NotEmpty(api.InvitationListReads(Northwind));
            Assert.Equal(0, await page.Locator(Rows).CountAsync());
            Assert.Equal(0, await page.Locator(Section).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = RevokeAction }).CountAsync());
        }
    }

    /// <summary>
    /// AC9, UX-DR31 - leaving the Space purges its Invitation addresses: after switching to Harbour (where the caller is
    /// also an Admin) and opening its settings, while Harbour's list read is held nothing from Northwind's list is in the
    /// DOM and nothing is rendered from cache; once it answers, Harbour's own row shows (the anchor). Plant 14: the purge
    /// registration skipped (record as masked if a re-render hides it, as 3.4 found for Labels).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR31")]
    [Trait("Requirement", "NFR-1")]
    public async Task Leaving_the_Space_purges_its_Invitation_addresses_before_the_next_Space_answers()
    {
        var api = TwoSpaces("owner");
        var (departed, arrived) = (Listed("member", "Olu Owner"), Listed("viewer", "Hal Harbour"));

        api.Roles[Harbour] = "admin";
        api.PendingInvitations[Northwind] = [departed];
        api.PendingInvitations[Harbour] = [arrived];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await OpenSettingsAsync(api, page, Northwind);
            await Row(page, departed).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var held = api.HoldInvitationListAsync();

            await ChooseInSwitcherAsync(page, "Harbour Ops");
            await page.Locator(ContextBar, new PageLocatorOptions { HasText = "Harbour Ops" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.EvaluateAsync("path => Blazor.navigateTo(path)", $"/spaces/{Harbour:D}/settings");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            Assert.DoesNotContain(departed.EmailAddress, await page.EvaluateAsync<string>("() => document.body.textContent"), StringComparison.Ordinal);
            Assert.Equal(0, await page.Locator(Rows).CountAsync());

            api.ReleaseInvitationList();

            await Row(page, arrived).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.DoesNotContain(departed.EmailAddress, await page.EvaluateAsync<string>("() => document.body.textContent"), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC9, UX-DR41, WCAG 1.4.10 - at 320×256 (1280×1024 at 400%), the list holding a long address and a long issuer name,
    /// with the confirm open on that row, reflows: nothing in <c>main</c> or the context bar scrolls sideways and the page
    /// does not, plain and under 1.4.12 text spacing. Anchored: the confirm is on screen before the probe runs.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR41")]
    [Trait("Requirement", "NFR-9")]
    public async Task The_Invitation_list_reflows_at_320px_with_a_long_address_and_the_confirm_open()
    {
        var api = TwoSpaces("owner");
        var listed = new ListedInvitation(
            Guid.NewGuid(),
            $"a.rather.long.local.part.for.an.invitee.{Guid.NewGuid():N}@a-long-subdomain.of.an.example.test",
            "member",
            "Maximiliana Konstantinopoulou-Abernathy",
            DateTimeOffset.UtcNow.AddHours(-3));

        api.PendingInvitations[Northwind] = [listed];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(320, 256);
            await OpenSettingsAsync(api, page, Northwind);

            var row = Row(page, listed);

            await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = RevokeAction, Exact = true }).ClickAsync();
            await row.GetByText(ConfirmCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Empty(await page.EvaluateAsync<string[]>(Reflow));

            await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = TextSpacingOverride });

            Assert.Empty(await page.EvaluateAsync<string[]>(Reflow));
        }
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
    /// A pending Invitation as the list sends it, to a fresh address (never a shared one), issued a day ago.
    /// </summary>
    private static ListedInvitation Listed(string role, string issuerName) =>
        new(Guid.NewGuid(), $"invitee-{Guid.NewGuid():N}@example.test", role, issuerName, DateTimeOffset.UtcNow.AddDays(-1));

    private static ILocator Row(IPage page, ListedInvitation listed) =>
        page.Locator(Rows, new PageLocatorOptions { HasText = listed.EmailAddress });

    /// <summary>
    /// Presses the row's Revoke, waits for the in-place warning, and presses the danger Revoke inside it.
    /// </summary>
    private static async Task ConfirmRevokeAsync(IPage page, ListedInvitation listed)
    {
        var row = Row(page, listed);

        await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = RevokeAction, Exact = true }).ClickAsync();
        await row.GetByText(ConfirmCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await row.Locator(".button-danger").ClickAsync();
    }

    private static async Task<string[]> SaidAsync(IPage page) =>
        await page.EvaluateAsync<string[]>("() => window.__said || []");

    /// <summary>
    /// Polls a condition on the fake with a deadline, never a sleep.
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
    /// Opens the Space, follows the context bar's "Space settings" link and waits for the Invitations heading.
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
