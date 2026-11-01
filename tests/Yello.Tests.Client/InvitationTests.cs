using System.Text.Json;
using Microsoft.Playwright;
using Xunit;
using Yello.Contracts;

namespace Yello.Tests.Client;

/// <summary>
/// Story 4.1 in a real browser: the invite form in Space settings - absent for a Member and a Viewer, a Role picker that
/// offers Admin, Member and Viewer with none chosen and never Owner, an email field no browser will autofill, a request of
/// exactly <c>{emailAddress, role}</c> under an <c>Idempotency-Key</c>, and each answer said in its place. AC1, AC2, AC3
/// (the client mirror) and AC10; FR-10, FR-42, UX-DR27, UX-DR33, UX-DR37, UX-DR38, UX-DR41, NFR-9.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 4.1 ATDD run (2026-11-01)</b> and activated by its dev phase, which
/// recorded each locator it corrected. The API
/// is <see cref="FakeYelloApi"/> at the network edge, taught by this run the issue route
/// (<c>FakeYelloApi.Invitations.cs</c>, held and released like 3.3's deletion); no Host, no database, no engine. Build
/// <c>Yello.Client</c> in Debug before running them.
/// </para>
/// <para>
/// <b>Locators are by role, label and the copy Q9 (a) recommends</b>, so they are assumptions to correct to the markup Task
/// 7 builds: the section is <c>main .invitation-section</c> (the <c>.space-name-section</c> precedent) headed
/// "Invitations"; the field is labelled "Email address"; the picker is a <c>fieldset</c> whose <c>legend</c> is "Role"
/// (so its role is <c>group</c>, named "Role") holding three radios labelled "Admin", "Member" and "Viewer", in that
/// order; the button is "Send invitation"; the progress line is a <c>role="status"</c> reading "Sending."; the form is
/// <c>novalidate</c>, so the client's own rule speaks rather than the browser's. Refusal copy beside the field is the
/// <c>FormField</c> message its <c>aria-describedby</c> names; beside the Role group it is inside the <c>fieldset</c> or
/// named by its <c>aria-describedby</c>. A lost answer reads "Not sent." (2.5's sentence). Copy that exists is asserted
/// as written ("Only Owners and Admins can issue or revoke Invitations.", "Not available.", "Not sent.").
/// </para>
/// <para>
/// <b>Decided, not assumed:</b> Q2 (Lee, 2026-11-01) - an Owner and an Admin both offer Admin, Member and Viewer. The
/// new problem types are named as the story names them (<c>already-a-member</c>, <c>invitation-pending</c>,
/// <c>role-not-offerable</c>), read from <c>ProblemTypes</c> so a contract change breaks these cases too.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-10")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class InvitationTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string ContextBar = "nav.context-bar";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string SwitcherTrigger = "nav.context-bar button[aria-controls]";
    private const string Section = "main .invitation-section";
    private const string EmailInput = "main .invitation-section input[type='email']";
    private const string Disabled = "main [disabled], main [aria-disabled='true'], nav.context-bar [disabled], nav.context-bar [aria-disabled='true']";

    private const string SettingsLink = "Space settings";
    private const string SectionHeading = "Invitations";
    private const string EmailLabel = "Email address";
    private const string RoleLegend = "Role";
    private const string SendAction = "Send invitation";
    private const string SendingCopy = "Sending.";
    private const string SentCopy = "Invitation sent.";
    private const string AlreadyAMemberCopy = "This address already belongs to a Member of this Space.";
    private const string PendingCopy = "This address already has a pending invitation.";
    private const string RoleNotOfferableCopy = "Choose Admin, Member or Viewer.";
    private const string InvalidAddressCopy = "Enter an email address.";
    private const string NoRoleCopy = "Choose a Role.";
    private const string RefusedRoleCopy = "Only Owners and Admins can issue or revoke Invitations.";
    private const string ManageLabelsCopy = "Only Owners and Admins can manage Labels.";
    private const string NotSentCopy = "Not sent.";
    private const string NotAvailableCopy = "Not available.";


    private const string TextSpacingOverride =
        "* { line-height: 1.5 !important; letter-spacing: 0.12em !important; word-spacing: 0.16em !important; } "
        + "p { margin-block-end: 2em !important; }";

    /// <summary>
    /// The two disclosable `409`s on the address and the sentence each is said with beside the field.
    /// </summary>
    public static TheoryData<string, string> AddressConflicts => new()
    {
        { ProblemTypes.AlreadyAMember, AlreadyAMemberCopy },
        { ProblemTypes.InvitationPending, PendingCopy },
    };

    /// <summary>
    /// A 1.4.10 reflow probe, as <c>SpaceCreateAndRenameTests.Reflow</c>: the page never scrolls sideways and nothing in
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

    /// <summary>
    /// Records every non-empty text any polite region or status region changes to, from now on: <c>window.__said</c>. As
    /// <c>SpaceDeleteTests.RecordEverythingSaid</c>.
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
    /// The text an element carries plus the text of every element its <c>aria-describedby</c> names: what "beside" means
    /// for a control, as the <c>FormField</c> message pattern draws it.
    /// </summary>
    private const string OwnAndDescribedText =
        "e => [e.textContent, ...(e.getAttribute('aria-describedby') || '').split(' ').filter(Boolean).map(id => (document.getElementById(id) || {}).textContent || '')].join(' ')";

    private static readonly string[] ReusableFieldNames =
        [string.Empty, "EMAIL", "E-MAIL", "EMAILADDRESS", "EMAIL-ADDRESS", "EMAIL_ADDRESS", "ADDRESS", "USERNAME", "LOGIN", "INVITEE"];

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();

    /// <summary>
    /// AC1, AC10, UX-DR27 - an Owner and an Admin find the Invitations section in Space settings, with its email field, its
    /// Role group and "Send invitation". The anchor of every absence case below.
    /// </summary>
    /// <param name="role">The caller's Role.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("owner")]
    [InlineData("admin")]
    [Trait("Requirement", "UX-DR27")]
    public async Task An_Owner_and_an_Admin_find_the_invite_form_in_Space_settings(string role)
    {
        var api = TwoSpaces(role);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await OpenSettingsAsync(api, page, Northwind);

            var section = page.Locator(Section);

            Assert.Equal(1, await section.GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = SectionHeading, Exact = true }).CountAsync());
            Assert.Equal(1, await EmailField(page).CountAsync());
            Assert.Equal(1, await RoleGroup(page).CountAsync());
            Assert.Equal(1, await SendButton(page).CountAsync());
            Assert.Equal(0, await page.Locator(Disabled).CountAsync());
        }
    }

    /// <summary>
    /// AC1, AC10, UX-DR27 - a Member and a Viewer have no invite affordance: no Space settings link in the context bar, and
    /// the settings address reached by hand renders no Invitations heading, no email field, no Role group and no "Send
    /// invitation" - absent from the DOM, never disabled or hidden. Anchored in the same case: the Admin of the other
    /// Space, on the same page, has all of it. Plant 13: the section rendered for a Member turns this red.
    /// </summary>
    /// <param name="role">The lesser Role.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("member")]
    [InlineData("viewer")]
    [Trait("Requirement", "UX-DR27")]
    public async Task A_Member_and_a_Viewer_have_no_invite_affordance_at_all(string role)
    {
        var api = TwoSpaces("admin");
        api.Roles[Harbour] = role;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await OpenSettingsAsync(api, page, Northwind);

            Assert.Equal(1, await EmailField(page).CountAsync());
            Assert.Equal(1, await SendButton(page).CountAsync());

            await ChooseInSwitcherAsync(page, "Harbour Ops");
            await page.Locator(ContextBar, new PageLocatorOptions { HasText = "Harbour Ops" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator(ContextBar).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = SettingsLink, Exact = true }).CountAsync());

            await page.EvaluateAsync("path => Blazor.navigateTo(path)", $"/spaces/{Harbour:D}/settings");
            await page.Locator("main").GetByText(ManageLabelsCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Section).CountAsync());
            Assert.Equal(0, await page.Locator("main").GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = SectionHeading, Exact = true }).CountAsync());
            Assert.Equal(0, await page.Locator("main").GetByLabel(EmailLabel).CountAsync());
            Assert.Equal(0, await page.Locator("main input[type='email'], main input[type='radio']").CountAsync());
            Assert.Equal(0, await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = SendAction }).CountAsync());
            Assert.Equal(0, await page.Locator(Disabled).CountAsync());
            Assert.Empty(api.InvitationsIssued(Harbour));
        }
    }

    /// <summary>
    /// AC2, AC10, FR-42, Q2 (Lee) - for an Owner and for an Admin alike, the Role picker is a <c>fieldset</c> and
    /// <c>legend</c> radio group named "Role" offering exactly three radios - Admin, Member and Viewer - and never Owner.
    /// Anchored: the three are counted before Owner's absence is. Plant 12: an Owner option added turns this red.
    /// </summary>
    /// <param name="role">The caller's Role.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("owner")]
    [InlineData("admin")]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "UX-DR27")]
    public async Task The_Role_picker_offers_Admin_Member_and_Viewer_and_never_Owner(string role)
    {
        var api = TwoSpaces(role);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(375, 667);
            await OpenSettingsAsync(api, page, Northwind);

            var group = RoleGroup(page);
            var radios = group.GetByRole(AriaRole.Radio);

            Assert.Equal("FIELDSET", await group.EvaluateAsync<string>("e => e.tagName"));
            Assert.Equal(RoleLegend, (await group.Locator("legend").InnerTextAsync()).Trim());
            Assert.Equal(3, await radios.CountAsync());
            Assert.Equal(
                ["Admin", "Member", "Viewer"],
                await radios.EvaluateAllAsync<string[]>("radios => radios.map(radio => radio.labels.length ? radio.labels[0].textContent.trim() : '')"));
            Assert.Equal(0, await group.GetByRole(AriaRole.Radio, new LocatorGetByRoleOptions { Name = "Owner" }).CountAsync());
            Assert.Equal(0, await page.Locator(Section).GetByText("Owner").CountAsync());
            Assert.Equal(0, await page.Locator($"{Section} input[type='radio'][value='owner' i]").CountAsync());
        }
    }

    /// <summary>
    /// AC2, AC10 (<c>DESIGN.md:473</c>, <c>EXPERIENCE.md:211</c>) - inviting at a Role is consequential, so no radio is
    /// checked when the form first renders. Anchored: the three radios are found first.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-10")]
    public async Task No_Role_is_chosen_when_the_invite_form_first_renders()
    {
        var api = TwoSpaces("owner");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await OpenSettingsAsync(api, page, Northwind);

            var radios = RoleGroup(page).GetByRole(AriaRole.Radio);

            Assert.Equal(3, await radios.CountAsync());
            Assert.Equal(0, await radios.EvaluateAllAsync<int>("radios => radios.filter(radio => radio.checked || radio.hasAttribute('checked')).length"));
        }
    }

    /// <summary>
    /// AC10, UX-DR33 - the email field is <c>type="email"</c> and <c>autocomplete="off"</c> (the address is someone
    /// else's, so the browser must not offer the person's own - a recorded variance from <c>Register.razor</c>), with a
    /// field name no browser would match to a saved value, fresh per render: leaving Space settings and coming back gives
    /// another name. The Space-name precedent (<c>SpaceCreateAndRenameTests</c>).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR33")]
    public async Task The_email_field_is_an_email_input_that_no_browser_can_autofill()
    {
        var api = TwoSpaces("owner");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await OpenSettingsAsync(api, page, Northwind);

            var field = EmailField(page);

            Assert.Equal(1, await field.CountAsync());
            Assert.Equal("email", await field.GetAttributeAsync("type"));
            Assert.Equal("off", await field.GetAttributeAsync("autocomplete"));

            var first = await field.GetAttributeAsync("name") ?? string.Empty;

            Assert.DoesNotContain(first.ToUpperInvariant(), ReusableFieldNames);

            await page.EvaluateAsync("path => Blazor.navigateTo(path)", $"/spaces/{Northwind:D}");
            await field.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });
            await page.Locator(ContextBar).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = SettingsLink, Exact = true }).ClickAsync();
            await field.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var second = await field.GetAttributeAsync("name") ?? string.Empty;

            Assert.DoesNotContain(second.ToUpperInvariant(), ReusableFieldNames);
            Assert.NotEqual(first, second);
        }
    }

    /// <summary>
    /// AC1, AC10, AD-18 - "Send invitation" posts exactly once to the Space's invitations route, with a body of exactly
    /// <c>emailAddress</c> (as typed) and <c>role</c> (the chosen Role's stable code) and an <c>Idempotency-Key</c>; the
    /// next deliberate submit takes a fresh key.
    /// </summary>
    /// <param name="choice">The radio chosen.</param>
    /// <param name="code">The Role code the body must carry.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("Admin", "admin")]
    [InlineData("Member", "member")]
    [InlineData("Viewer", "viewer")]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "AR-25")]
    public async Task Sending_an_invitation_posts_exactly_the_address_and_the_Role_code_under_a_key(string choice, string code)
    {
        var api = TwoSpaces("owner");
        var address = RandomAddress();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await OpenSettingsAsync(api, page, Northwind);

            await SubmitAsync(page, address, choice);
            await WaitUntilAsync(() => api.InvitationsIssued(Northwind).Count == 1, "The invitation was never posted.");
            await WaitUntilAsync(async () => string.IsNullOrEmpty(await EmailField(page).InputValueAsync()), "The form was not cleared after the 204.");

            var posted = Assert.Single(api.InvitationsIssued(Northwind));

            Assert.Equal(["emailAddress", "role"], Members(posted).Order(StringComparer.Ordinal));

            using (var document = JsonDocument.Parse(posted.Body ?? "{}"))
            {
                Assert.Equal(address, document.RootElement.GetProperty("emailAddress").GetString());
                Assert.Equal(code, document.RootElement.GetProperty("role").GetString());
            }

            Assert.False(string.IsNullOrWhiteSpace(posted.Header(Idempotency.HeaderName)), "The invitation carried no Idempotency-Key.");

            await SubmitAsync(page, RandomAddress(), choice);
            await WaitUntilAsync(() => api.InvitationsIssued(Northwind).Count == 2, "The second invitation was never posted.");

            var sent = api.InvitationsIssued(Northwind);

            Assert.NotEqual(sent[0].Header(Idempotency.HeaderName), sent[1].Header(Idempotency.HeaderName));
        }
    }

    /// <summary>
    /// AC10, AD-18, NFR-5 (the 2.5 deletes' precedent) - an issue that gets no answer keeps the form as it was, says "Not
    /// sent.", and sending again re-sends the identical body under the same <c>Idempotency-Key</c>; that answer clears the
    /// form.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "NFR-5")]
    public async Task An_invitation_resent_after_no_answer_reuses_its_key()
    {
        var api = TwoSpaces("owner");
        var address = RandomAddress();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await OpenSettingsAsync(api, page, Northwind);

            var held = api.HoldInvitationAsync();

            await SubmitAsync(page, address, "Member");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            api.ReleaseInvitationUnanswered();

            await page.Locator(Section).GetByText(NotSentCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(address, await EmailField(page).InputValueAsync());
            Assert.True(await RoleGroup(page).GetByRole(AriaRole.Radio, new LocatorGetByRoleOptions { Name = "Member", Exact = true }).IsCheckedAsync(), "The Role was cleared after no answer.");

            await SendButton(page).ClickAsync();
            await WaitUntilAsync(async () => string.IsNullOrEmpty(await EmailField(page).InputValueAsync()), "The resend's 204 did not clear the form.");

            var sent = api.InvitationsIssued(Northwind);

            Assert.Equal(2, sent.Count);
            Assert.False(string.IsNullOrEmpty(sent[0].Header(Idempotency.HeaderName)), "The invitation carried no Idempotency-Key.");
            Assert.Equal(sent[0].Header(Idempotency.HeaderName), sent[1].Header(Idempotency.HeaderName));
            Assert.Equal(sent[0].Body, sent[1].Body);
        }
    }

    /// <summary>
    /// AC10, UX-DR38 - not optimistic: while the issue is held the form keeps the address and "Sending." is shown; on the
    /// <c>204</c> the email field is cleared, no radio is checked, focus is on the email field, and the polite announcement
    /// says "Invitation sent." - and nothing said, then or since, contains an <c>@</c> or the address. Plant 14: the
    /// announcement naming the address turns this red.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR38")]
    [Trait("Requirement", "UX-DR25")]
    public async Task A_sent_invitation_clears_the_form_keeps_focus_and_announces_no_address()
    {
        var api = TwoSpaces("admin");
        var address = RandomAddress();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await OpenSettingsAsync(api, page, Northwind);
            await page.EvaluateAsync(RecordEverythingSaid);

            var held = api.HoldInvitationAsync();

            await SubmitAsync(page, address, "Viewer");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            await page.Locator($"{Section} [role='status']", new PageLocatorOptions { HasText = SendingCopy }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            Assert.Equal(address, await EmailField(page).InputValueAsync());

            api.ReleaseInvitation();

            await WaitUntilAsync(async () => string.IsNullOrEmpty(await EmailField(page).InputValueAsync()), "The email field was not cleared after the 204.");
            await page.WaitForFunctionAsync(
                $"() => !!document.activeElement && document.activeElement.matches(\"{EmailInput}\")",
                null,
                new PageWaitForFunctionOptions { Timeout = Timeout });
            await WaitUntilAsync(async () => Array.Exists(await SaidAsync(page), sentence => sentence.Contains(SentCopy, StringComparison.Ordinal)), "\"Invitation sent.\" was never announced.");

            var said = await SaidAsync(page);
            var polite = string.Join(' ', await page.Locator("[aria-live='polite'], [role='status']").AllTextContentsAsync());

            Assert.Equal(0, await RoleGroup(page).GetByRole(AriaRole.Radio).EvaluateAllAsync<int>("radios => radios.filter(radio => radio.checked).length"));
            Assert.All(said, sentence => Assert.DoesNotContain("@", sentence, StringComparison.Ordinal));
            Assert.DoesNotContain("@", polite, StringComparison.Ordinal);
            Assert.DoesNotContain(address, await page.EvaluateAsync<string>("() => document.body.innerText"), StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// AC4, AC13, AC10, UX-DR37 - the two disclosable <c>409</c>s are said beside the email field, which is marked invalid
    /// and names its message in <c>aria-describedby</c>; nothing is announced as sent and the address stays to be
    /// corrected.
    /// </summary>
    /// <param name="problemType">The refusal's problem type.</param>
    /// <param name="copy">The sentence expected beside the field.</param>
    /// <returns>A task.</returns>
    [Theory]
    [MemberData(nameof(AddressConflicts))]
    [Trait("Requirement", "UX-DR37")]
    public async Task A_409_on_the_address_is_said_beside_the_email_field(string problemType, string copy)
    {
        var api = TwoSpaces("owner");
        var address = RandomAddress();

        api.NextInvitationRefusal = (409, problemType, null);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await OpenSettingsAsync(api, page, Northwind);
            await page.EvaluateAsync(RecordEverythingSaid);

            await SubmitAsync(page, address, "Member");

            var field = EmailField(page);

            await page.Locator($"{EmailInput}[aria-invalid='true']").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Contains(copy, await field.EvaluateAsync<string>(OwnAndDescribedText), StringComparison.Ordinal);
            Assert.Equal(address, await field.InputValueAsync());
            Assert.DoesNotContain(await SaidAsync(page), sentence => sentence.Contains(SentCopy, StringComparison.Ordinal));
            Assert.Single(api.InvitationsIssued(Northwind));
        }
    }

    /// <summary>
    /// AC2, AC10, UX-DR37 - a <c>400 role-not-offerable</c> (the server refuses whatever the client offered) is said beside
    /// the Role group: inside its <c>fieldset</c> or named by its <c>aria-describedby</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "UX-DR37")]
    public async Task A_role_not_offerable_refusal_is_said_beside_the_Role_group()
    {
        var api = TwoSpaces("owner");

        api.NextInvitationRefusal = (400, ProblemTypes.RoleNotOfferable, null);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await OpenSettingsAsync(api, page, Northwind);

            await SubmitAsync(page, RandomAddress(), "Admin");

            var group = RoleGroup(page);

            await WaitUntilAsync(
                async () => (await group.EvaluateAsync<string>(OwnAndDescribedText)).Contains(RoleNotOfferableCopy, StringComparison.Ordinal),
                "\"Choose Admin, Member or Viewer.\" was not said beside the Role group.");

            Assert.DoesNotContain(RoleNotOfferableCopy, await EmailField(page).EvaluateAsync<string>(OwnAndDescribedText), StringComparison.Ordinal);
            Assert.Single(api.InvitationsIssued(Northwind));
        }
    }

    /// <summary>
    /// AC3 (the client mirror of the plausibility rule), AC2, AC10, UX-DR37 - the client's own checks speak before anything
    /// is sent: no Role chosen is "Choose a Role." beside the Role group; an empty or implausible address (no <c>@</c>, two
    /// <c>@</c>, whitespace) is "Enter an email address." beside the field. Nothing reaches the invitations route.
    /// </summary>
    /// <param name="addressShape">Which address to type: <c>valid</c>, <c>empty</c>, <c>no-at</c>, <c>two-at</c> or <c>space</c>.</param>
    /// <param name="chooseRole">Whether a Role is chosen first.</param>
    /// <param name="copy">The sentence expected.</param>
    /// <param name="besideRole">Whether the sentence belongs beside the Role group (otherwise beside the email field).</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("valid", false, NoRoleCopy, true)]
    [InlineData("empty", true, InvalidAddressCopy, false)]
    [InlineData("no-at", true, InvalidAddressCopy, false)]
    [InlineData("two-at", true, InvalidAddressCopy, false)]
    [InlineData("space", true, InvalidAddressCopy, false)]
    [Trait("Requirement", "UX-DR37")]
    public async Task The_client_refuses_a_missing_Role_or_an_implausible_address_and_sends_nothing(string addressShape, bool chooseRole, string copy, bool besideRole)
    {
        var api = TwoSpaces("owner");
        var unique = Guid.NewGuid().ToString("N");
        var address = addressShape switch
        {
            "valid" => RandomAddress(),
            "empty" => string.Empty,
            "no-at" => $"invitee-{unique}.example.test",
            "two-at" => $"invitee@{unique}@example.test",
            _ => $"invitee {unique}@example.test",
        };

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await OpenSettingsAsync(api, page, Northwind);

            await SubmitAsync(page, address, chooseRole ? "Member" : null);

            var beside = besideRole ? RoleGroup(page) : EmailField(page);

            await WaitUntilAsync(
                async () => (await beside.EvaluateAsync<string>(OwnAndDescribedText)).Contains(copy, StringComparison.Ordinal),
                $"\"{copy}\" was not said beside the {(besideRole ? "Role group" : "email field")}.");

            Assert.Empty(api.InvitationsIssued(Northwind));
        }
    }

    /// <summary>
    /// AC10, AD-20 - a <c>403 insufficient-role</c> (the Role lowered on the server since the page loaded) is said in the
    /// section in the existing capability copy for <c>issue-and-revoke-invitations</c>, never as "Not available.".
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR32")]
    public async Task A_403_on_the_issue_is_said_in_the_capability_copy()
    {
        var api = TwoSpaces("admin");

        api.NextInvitationRefusal = (403, ProblemTypes.InsufficientRole, "issue-and-revoke-invitations");

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await OpenSettingsAsync(api, page, Northwind);

            await SubmitAsync(page, RandomAddress(), "Member");
            await page.Locator(Section).GetByText(RefusedRoleCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.DoesNotContain(NotAvailableCopy, await page.Locator("main").InnerTextAsync(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC10, AD-20 - a <c>404</c> on the issue is the Space refusal: the section hands it to the page through
    /// <c>OnSpaceRefused</c>, as the name, Labels and delete sections do, and the page renders the one refusal surface,
    /// "Not available.", with the invite form gone.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR32")]
    public async Task A_404_on_the_issue_renders_the_Space_refusal_surface()
    {
        var api = TwoSpaces("owner");

        api.NextInvitationRefusal = (404, ProblemTypes.NotAvailable, null);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 800);
            await OpenSettingsAsync(api, page, Northwind);

            await SubmitAsync(page, RandomAddress(), "Member");
            await page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = NotAvailableCopy }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Section).CountAsync());
            Assert.Equal(0, await page.Locator(EmailInput).CountAsync());
        }
    }

    /// <summary>
    /// AC10, UX-DR41, WCAG 1.4.10 - at 320×256 (1280×1024 at 400%), the invite form holding a long address and showing its
    /// "Choose a Role." message reflows: nothing in the section, <c>main</c> or the context bar scrolls sideways, and the
    /// page does not. Anchored: the message is on screen before the probe runs.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR41")]
    [Trait("Requirement", "NFR-9")]
    public async Task The_invite_form_reflows_at_320px_and_never_scrolls_sideways()
    {
        var api = TwoSpaces("owner");
        var longAddress = $"a.rather.long.local.part.for.an.invitee.{Guid.NewGuid():N}@a-long-subdomain.of.an.example.test";
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(320, 256);
            await OpenSettingsAsync(api, page, Northwind);

            await SubmitAsync(page, longAddress, role: null);

            var group = RoleGroup(page);

            await WaitUntilAsync(
                async () => (await group.EvaluateAsync<string>(OwnAndDescribedText)).Contains(NoRoleCopy, StringComparison.Ordinal),
                "\"Choose a Role.\" was not said beside the Role group.");

            Assert.Empty(await page.EvaluateAsync<string[]>(Reflow));
            Assert.True(await page.Locator(Section).EvaluateAsync<bool>("s => s.scrollWidth <= s.clientWidth + 1"), "The invite section overflows sideways.");

            await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = TextSpacingOverride });

            Assert.Empty(await page.EvaluateAsync<string[]>(Reflow));
        }
    }

    /// <summary>
    /// A signed-in Account holding <paramref name="northwindRole"/> in Northwind Redesign and Member of Harbour Ops,
    /// listed in that order.
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
    /// A fresh address per call: never a shared email address (TESTING-CONVENTIONS).
    /// </summary>
    private static string RandomAddress() => $"invitee-{Guid.NewGuid():N}@example.test";

    private static ILocator EmailField(IPage page) =>
        page.Locator(Section).GetByLabel(EmailLabel, new LocatorGetByLabelOptions { Exact = true });

    private static ILocator RoleGroup(IPage page) =>
        page.Locator(Section).GetByRole(AriaRole.Group, new LocatorGetByRoleOptions { Name = RoleLegend, Exact = true });

    private static ILocator SendButton(IPage page) =>
        page.Locator(Section).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = SendAction, Exact = true });

    /// <summary>
    /// Types <paramref name="address"/>, chooses <paramref name="role"/> when one is given, and presses "Send invitation".
    /// </summary>
    private static async Task SubmitAsync(IPage page, string address, string? role)
    {
        var field = EmailField(page);

        await field.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await field.FillAsync(address);

        if (role is not null)
        {
            await RoleGroup(page).GetByRole(AriaRole.Radio, new LocatorGetByRoleOptions { Name = role, Exact = true }).CheckAsync();
        }

        await SendButton(page).ClickAsync();
    }

    private static string[] Members(FakeRequest request)
    {
        using var document = JsonDocument.Parse(request.Body ?? "{}");

        return [.. document.RootElement.EnumerateObject().Select(member => member.Name)];
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
