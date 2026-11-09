using Microsoft.Playwright;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// The recipient's side of an Ownership Offer in a real browser: the accent "Ownership offered" indicator in the context bar,
/// shown only while an offer names this Account in the active Space; the dialog it opens, which explains in full what being
/// Owner commits the recipient to and counts the expiry from the server's clock; Accept and Decline with neither a default;
/// the acceptance's answers (the re-read and the polite narration, the no-longer-open state, the unanswered resend); and the
/// former Owner's tab, which is told of the drop before the Ownership section goes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Red-phase scaffolds.</b> Every case is skipped until the indicator and the dialog exist. The API is
/// <see cref="FakeYelloApi"/> at the network edge, taught the recipient's routes in
/// <c>FakeYelloApi.OwnershipOfferAnswers.cs</c>; no Host, no database, no engine. Same assembly fixture as
/// <see cref="OwnershipSectionTests"/>, so no new fixture.
/// </para>
/// <para>
/// <b>Locators</b> follow the story's markup and copy: the indicator is <c>nav.context-bar button.offer-indicator</c>, placed
/// before the Role chip, with visible text "Ownership offered" and the accessible name "Ownership of {space} offered to you.
/// Open the offer."; the dialog is the page's one <c>[role=dialog][aria-modal=true]</c>, headed "{offerer} has offered you
/// ownership of {space}."; its buttons are "Accept ownership" and "Decline"; the no-longer-open state says "This offer is no
/// longer open." and "Nothing has changed about your access to this Space." with one button "Close". The unanswered
/// acceptance says "Not sent." (this run's assumption for <c>OwnershipOfferNotSent</c>'s value) and the same "Accept
/// ownership" press resends. Text is read with <c>textContent</c>.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-42")]
[Trait("Requirement", "UX-DR9")]
public sealed class OwnershipOfferTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string ContextBar = "nav.context-bar";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string Indicator = "nav.context-bar button.offer-indicator";
    private const string Dialog = "[role='dialog'][aria-modal='true']";
    private const string Narration = ".role-narration";
    private const string SwitcherTrigger = "nav.context-bar button[aria-controls]";
    private const string OwnershipSection = "main .ownership-section";
    private const string MembersSection = "main .membership-section";

    private const string SpaceName = "Northwind Redesign";
    private const string OtherSpaceName = "Harbour Ops";
    private const string OffererName = "Olu Owner";
    private const string IndicatorText = "Ownership offered";
    private const string IndicatorName = "Ownership of Northwind Redesign offered to you. Open the offer.";
    private const string Heading = "Olu Owner has offered you ownership of Northwind Redesign.";
    private const string AcceptAction = "Accept ownership";
    private const string DeclineAction = "Decline";
    private const string CloseAction = "Close";
    private const string NoLongerOpen = "This offer is no longer open.";
    private const string NothingChanged = "Nothing has changed about your access to this Space.";
    private const string NowTheOwner = "You're now the Owner. You can manage everything in this Space, including ownership.";
    private const string NowAnAdmin = "You're now an Admin. You can manage Members and settings.";
    private const string SettingsLink = "Space settings";

    /// <summary>
    /// The commitments the dialog must state in full (FR-42; the story's copy, Q4 (a)).
    /// </summary>
    private static readonly string[] Commitments =
    [
        "Accepting makes you the Owner. Olu Owner becomes an Admin and keeps their access.",
        "You could not remove your own Membership while you hold ownership.",
        "You could not delete your Yello Account until you pass ownership on, or delete this Space.",
        "Passing it on needs someone else to accept. Nobody can be made an Owner without agreeing.",
    ];

    /// <summary>
    /// Records every non-empty text a live region changes to: <c>window.__said</c>, as <c>polite:...</c> or <c>alert:...</c>.
    /// </summary>
    private const string RecordEverythingSaid =
        """
        () => {
            window.__said = [];
            const last = new WeakMap();
            const regions = () => document.querySelectorAll('[aria-live="polite"], [role="status"], [role="alert"], [aria-live="assertive"]');
            new MutationObserver(() => {
                for (const region of regions()) {
                    const text = region.textContent.trim();
                    const kind = region.getAttribute('role') === 'alert' || region.getAttribute('aria-live') === 'assertive' ? 'alert:' : 'polite:';
                    if (text && last.get(region) !== text) window.__said.push(kind + text);
                    last.set(region, text);
                }
            }).observe(document.body, { childList: true, subtree: true, characterData: true });
        }
        """;

    /// <summary>
    /// Logs, in order, each text the Role narration takes and each removal of a node matching a selector, with the observer
    /// callback each came in: <c>window.__roleLog</c> and <c>window.__roleBatch</c>.
    /// </summary>
    private const string LogNarrationAndRemovals =
        """
        selectors => {
            window.__roleLog = [];
            window.__roleBatch = [];
            let batch = 0;
            const push = entry => { window.__roleLog.push(entry); window.__roleBatch.push(batch); };
            const affordance = node => node.nodeType === 1 && selectors.some(s => node.matches(s) || node.querySelector(s));
            new MutationObserver(records => {
                batch++;
                for (const record of records) {
                    for (const node of record.removedNodes) {
                        if (affordance(node)) push('removed:' + (node.className || node.tagName));
                    }
                    const narration = document.querySelector('.role-narration');
                    const text = narration ? narration.textContent.trim() : '';
                    if (text && !window.__roleLog.includes('narration:' + text)) push('narration:' + text);
                }
            }).observe(document.body, { childList: true, subtree: true, characterData: true });
        }
        """;

    /// <summary>
    /// A 1.4.10 reflow probe over the page, the context bar and the dialog.
    /// </summary>
    private const string Reflow =
        """
        () => {
            const problems = [];
            const page = document.scrollingElement;
            if (page.scrollWidth > page.clientWidth + 1) problems.push('the page scrolls sideways by ' + (page.scrollWidth - page.clientWidth) + 'px');
            for (const element of document.querySelectorAll('nav.context-bar, nav.context-bar *, [role="dialog"], [role="dialog"] *')) {
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

    private static readonly string[] OwnershipSectionSelectors = [".ownership-section"];

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();

    /// <summary>
    /// The Roles that see no indicator when no offer names them: a Member not named, an Admin, and the Owner (who cannot be
    /// named).
    /// </summary>
    public static TheoryData<string> RolesWithNoOfferForThem => new() { "member", "admin", "owner" };

    /// <summary>
    /// The acceptance's refusals that end in the no-longer-open state.
    /// </summary>
    public static TheoryData<string> RefusalsThatCloseTheOffer => new() { "ownership-offer-not-pending", "owner-role-fixed" };

    /// <summary>
    /// A Member named by a pending offer sees the indicator in the context bar, <b>before</b> the Role chip, with the visible
    /// text "Ownership offered" and the accessible name naming the Space; it is a button (pressing it acts), and the page read
    /// the offer naming it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1 - the Offer indicator and the recipient's read do not exist yet")]
    [Trait("Requirement", "UX-DR41")]
    public async Task A_named_recipient_sees_the_Offer_indicator_before_the_Role_chip_with_its_accessible_name()
    {
        var api = Spaces("member", offered: true);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenHomeAsync(api, page);
            await page.Locator(Indicator).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(IndicatorText, (await page.Locator(Indicator).TextContentAsync() ?? string.Empty).Trim());
            Assert.Equal(1, await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = IndicatorName, Exact = true }).CountAsync());
            Assert.True(
                await page.Locator(Indicator).EvaluateAsync<bool>("(e, chip) => !!(e.compareDocumentPosition(document.querySelector(chip)) & Node.DOCUMENT_POSITION_FOLLOWING)", RoleChip),
                "The indicator does not come before the Role chip.");
            Assert.NotEmpty(api.MyOfferReads(Northwind));
        }
    }

    /// <summary>
    /// With no offer naming this Account the indicator is absent from the DOM - not hidden, not disabled - for a Member who is
    /// not named, an Admin and the Owner. Anchor: the page did read the offer naming it (and was answered <c>204</c>).
    /// </summary>
    /// <param name="role">The Account's Role in the Space.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC1 - the Offer indicator and the recipient's read do not exist yet")]
    [MemberData(nameof(RolesWithNoOfferForThem))]
    [Trait("Requirement", "UX-DR27")]
    public async Task No_Offer_indicator_is_rendered_without_an_offer_naming_the_Account(string role)
    {
        var api = Spaces(role, offered: false);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenHomeAsync(api, page);
            await WaitUntilAsync(() => api.MyOfferReads(Northwind).Count > 0);

            Assert.Equal(0, await page.Locator(Indicator).CountAsync());
            Assert.Equal(0, await page.GetByText(IndicatorText, new PageGetByTextOptions { Exact = true }).CountAsync());
        }
    }

    /// <summary>
    /// Nothing carries over between Spaces: after a switch to a Space with no offer, the indicator is gone.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1 - the Offer indicator and its per-Space state do not exist yet")]
    [Trait("Requirement", "AD-10")]
    public async Task The_Offer_indicator_is_gone_after_a_switch_to_a_Space_with_no_offer()
    {
        var api = Spaces("member", offered: true);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenHomeAsync(api, page);
            await page.Locator(Indicator).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await ChooseInSwitcherAsync(page, OtherSpaceName);
            await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await WaitUntilAsync(() => api.MyOfferReads(Harbour).Count > 0);

            Assert.Equal(0, await page.Locator(Indicator).CountAsync());
        }
    }

    /// <summary>
    /// The indicator follows the next answer this tab gets: the offer is answered elsewhere, and the next arrival in the Space
    /// (a switch away and back) reads <c>204</c> and the indicator is gone. No timer and no polling: before the re-arrival the
    /// page sends no read of its own.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1 (Q5 a) - the Offer indicator and its re-read on arrival do not exist yet")]
    [Trait("Requirement", "AD-10")]
    public async Task A_re_arrival_answered_204_removes_the_Offer_indicator()
    {
        var api = Spaces("member", offered: true);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenHomeAsync(api, page);
            await page.Locator(Indicator).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            api.OffersForMe.TryRemove(Northwind, out _);

            await ChooseInSwitcherAsync(page, OtherSpaceName);
            await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await ChooseInSwitcherAsync(page, SpaceName);
            await WaitUntilAsync(() => api.MyOfferReads(Northwind).Count > 1);
            await page.Locator(Indicator).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Indicator).CountAsync());
        }
    }

    /// <summary>
    /// At 320px the indicator survives as the Role chip does: visible, still named, and the bar causes no sideways scroll.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1 - the Offer indicator does not exist yet")]
    [Trait("Requirement", "UX-DR41")]
    [Trait("Requirement", "NFR-9")]
    public async Task At_320px_the_Offer_indicator_stays_visible_and_named_and_the_bar_does_not_reflow()
    {
        var api = Spaces("member", offered: true);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(320, 568);
            await OpenHomeAsync(api, page);
            await page.Locator(Indicator).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.True(await page.Locator(Indicator).IsVisibleAsync(), "The indicator is not visible at 320px.");
            Assert.Equal(1, await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = IndicatorName, Exact = true }).CountAsync());
            Assert.Empty(await page.EvaluateAsync<string[]>(Reflow));
        }
    }

    /// <summary>
    /// The dialog explains in full: a modal dialog labelled by its heading and described by its explanation; the heading names
    /// the offerer and the Space; the body states every commitment, including both FR-42 sentences (the Membership cannot be
    /// removed while ownership is held; Account deletion is refused until ownership is passed on or the Space deleted); and
    /// no address appears anywhere in it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2 - the offer dialog does not exist yet")]
    [Trait("Requirement", "UX-DR37")]
    [Trait("Requirement", "AD-23")]
    public async Task The_dialog_explains_both_commitments_and_names_the_offerer()
    {
        var api = Spaces("member", offered: true);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenDialogAsync(api, page);

            var dialog = page.Locator(Dialog);
            var text = await dialog.TextContentAsync() ?? string.Empty;
            var labelledBy = await dialog.GetAttributeAsync("aria-labelledby");
            var describedBy = await dialog.GetAttributeAsync("aria-describedby");

            Assert.False(string.IsNullOrEmpty(labelledBy), "The dialog names no heading in aria-labelledby.");
            Assert.False(string.IsNullOrEmpty(describedBy), "The dialog names no explanation in aria-describedby.");
            Assert.Equal(Heading, (await page.Locator($"[id='{labelledBy}']").TextContentAsync() ?? string.Empty).Trim());

            foreach (var commitment in Commitments)
            {
                Assert.Contains(commitment, text, StringComparison.Ordinal);
            }

            Assert.DoesNotContain("@", text, StringComparison.Ordinal);
            Assert.DoesNotContain("!", text, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The expiry is counted from the server's clock: the offer's <c>ExpiresAt</c> is six days and a few hours after the
    /// read's <c>ReadAt</c>, while the browser's clock is set more than a year away. The dialog says "Expires in 6 days" with a
    /// machine-readable <c>time</c>. Plant 14 (days from <c>DateTimeOffset.Now</c>) says something else.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2 - the offer dialog does not exist yet")]
    [Trait("Requirement", "AD-27")]
    [Trait("Requirement", "UX-DR42")]
    [Trait("Assumption", "PRD-12-3")]
    public async Task The_dialog_counts_the_expiry_from_the_server_clock_and_not_the_browsers()
    {
        var api = Spaces("member", offered: true);

        api.OffersForMe[Northwind] = (api.OffersForMe[Northwind].Id, OffererName, api.ServerNow.AddDays(6).AddHours(5));

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.Clock.SetFixedTimeAsync(api.ServerNow.AddDays(400).UtcDateTime);
            await OpenDialogAsync(api, page);

            Assert.Contains("Expires in 6 days", await page.Locator(Dialog).TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
            Assert.False(
                string.IsNullOrEmpty(await page.Locator($"{Dialog} time[datetime]").First.GetAttributeAsync("datetime")),
                "The expiry carries no machine-readable time.");
        }
    }

    /// <summary>
    /// <c>Esc</c> closes the dialog, sends nothing, and returns focus to the indicator that opened it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2 - the offer dialog does not exist yet")]
    [Trait("Requirement", "UX-DR39")]
    public async Task Esc_closes_the_dialog_and_returns_focus_to_the_indicator()
    {
        var api = Spaces("member", offered: true);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenDialogAsync(api, page);
            await page.Keyboard.PressAsync("Escape");
            await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.True(await page.Locator(Indicator).EvaluateAsync<bool>("e => e === document.activeElement"), "Focus did not return to the indicator.");
            Assert.Empty(api.AcceptancesSent(Northwind));
            Assert.Empty(api.DeclinesSent(Northwind));
        }
    }

    /// <summary>
    /// Focus is trapped: <c>Tab</c> and <c>Shift+Tab</c>, pressed more times than the dialog has controls, never leave it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2 - the offer dialog does not exist yet")]
    [Trait("Requirement", "UX-DR39")]
    public async Task Tab_cycles_within_the_dialog()
    {
        var api = Spaces("member", offered: true);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenDialogAsync(api, page);

            foreach (var key in Enumerable.Repeat("Tab", 6).Concat(Enumerable.Repeat("Shift+Tab", 6)))
            {
                await page.Keyboard.PressAsync(key);

                Assert.True(
                    await page.Locator(Dialog).EvaluateAsync<bool>("d => d.contains(document.activeElement)"),
                    $"Focus left the dialog after {key}.");
            }
        }
    }

    /// <summary>
    /// Neither choice is a default: on open, focus is on neither "Accept ownership" nor "Decline"; <c>Enter</c> straight after
    /// opening sends no acceptance and no decline, and neither does <c>Enter</c> after a click on the explanation. A mis-hit
    /// <c>Enter</c> must not transfer a Space. Plant 13 (<c>autofocus</c> on Accept) turns it red.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 - the offer dialog does not exist yet")]
    [Trait("Requirement", "UX-DR12")]
    public async Task Neither_choice_is_focused_and_Enter_sends_no_acceptance()
    {
        var api = Spaces("member", offered: true);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenDialogAsync(api, page);

            var dialog = page.Locator(Dialog);

            foreach (var name in new[] { AcceptAction, DeclineAction })
            {
                Assert.False(
                    await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = name, Exact = true }).EvaluateAsync<bool>("e => e === document.activeElement"),
                    $"The dialog pre-focuses {name}.");
            }

            await page.Keyboard.PressAsync("Enter");
            await dialog.GetByText(Commitments[1], new LocatorGetByTextOptions { Exact = false }).ClickAsync();
            await page.Keyboard.PressAsync("Enter");
            await RoundTripAsync(api, page);

            Assert.Empty(api.AcceptancesSent(Northwind));
            Assert.Empty(api.DeclinesSent(Northwind));
        }
    }

    /// <summary>
    /// Accept sends one acceptance with one <c>Idempotency-Key</c>; on its <c>204</c> the dialog closes, the indicator is
    /// removed, the Space is re-read, and the rise is narrated politely ("You're now the Owner. ..."), not as an alert; focus
    /// lands in the context bar, never on the body. Plant 15 (no re-read after the <c>204</c>) turns it red.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC12 - the offer dialog and the acceptance do not exist yet")]
    [Trait("Requirement", "AD-9")]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "UX-DR38")]
    [Trait("Requirement", "UX-DR39")]
    public async Task Accept_sends_once_with_one_key_rereads_and_narrates_the_new_Owner_politely()
    {
        var api = Spaces("member", offered: true);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenDialogAsync(api, page);
            await page.EvaluateAsync(RecordEverythingSaid);

            var arrivalsBefore = api.SpaceArrivalReads(Northwind).Count;

            await page.Locator(Dialog).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = AcceptAction, Exact = true }).ClickAsync();
            await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });
            await page.Locator(Narration, new PageLocatorOptions { HasText = NowTheOwner }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var sent = Assert.Single(api.AcceptancesSent(Northwind));
            var said = await page.EvaluateAsync<string[]>("() => window.__said || []");

            Assert.False(string.IsNullOrEmpty(sent.Header(Idempotency.HeaderName)), "The acceptance carries no Idempotency-Key.");
            Assert.Equal(0, await page.Locator(Indicator).CountAsync());
            Assert.True(api.SpaceArrivalReads(Northwind).Count > arrivalsBefore, "The Space was not re-read after the acceptance.");
            Assert.Contains("polite:" + NowTheOwner, said);
            Assert.DoesNotContain("alert:" + NowTheOwner, said);
            Assert.True(
                await page.EvaluateAsync<bool>("bar => !!document.activeElement && document.activeElement !== document.body && !!document.activeElement.closest(bar)", ContextBar),
                "Focus did not land in the context bar after the acceptance.");
        }
    }

    /// <summary>
    /// An acceptance that gets no answer says "Not sent." in the dialog and leaves it open; the same press resends under the
    /// same <c>Idempotency-Key</c> (a retry after no answer reuses the key), and the replay's <c>204</c> completes it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3, AC12 - the offer dialog and its unanswered state do not exist yet")]
    [Trait("Requirement", "AD-18")]
    public async Task An_unanswered_acceptance_says_Not_sent_and_resends_under_the_same_key()
    {
        var api = Spaces("member", offered: true);

        api.LoseNextAcceptAnswer = true;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenDialogAsync(api, page);

            var accept = page.Locator(Dialog).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = AcceptAction, Exact = true });

            await accept.ClickAsync();
            await page.Locator(Dialog).GetByText("Not sent.", new LocatorGetByTextOptions { Exact = false }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await accept.ClickAsync();
            await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            var sent = api.AcceptancesSent(Northwind);

            Assert.Equal(2, sent.Count);
            Assert.False(string.IsNullOrEmpty(sent[0].Header(Idempotency.HeaderName)), "The acceptance carries no Idempotency-Key.");
            Assert.Equal(sent[0].Header(Idempotency.HeaderName), sent[1].Header(Idempotency.HeaderName));
        }
    }

    /// <summary>
    /// An acceptance refused because the offer closed meanwhile (<c>409 ownership-offer-not-pending</c>), or because the swap
    /// met a second Owner (<c>409 owner-role-fixed</c>), replaces the dialog's body in place with the designed state - "This
    /// offer is no longer open." and "Nothing has changed about your access to this Space." - with one button, "Close"; it is
    /// not a toast, it names nobody, and the indicator is removed. The <c>owner-role-fixed</c> answer is followed by a re-read.
    /// Close returns focus to the context bar, since the indicator is gone.
    /// </summary>
    /// <param name="problemCode">The refusal's stable code.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC2, AC12 - the offer dialog and its no-longer-open state do not exist yet")]
    [MemberData(nameof(RefusalsThatCloseTheOffer))]
    [Trait("Requirement", "UX-DR39")]
    public async Task An_acceptance_refused_409_shows_the_offer_is_no_longer_open_and_removes_the_indicator(string problemCode)
    {
        var api = Spaces("member", offered: true);

        api.NextOfferAcceptRefusal = (409, "urn:yello:problem:" + problemCode);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenDialogAsync(api, page);

            var arrivalsBefore = api.SpaceArrivalReads(Northwind).Count;

            await page.Locator(Dialog).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = AcceptAction, Exact = true }).ClickAsync();
            await AssertNoLongerOpenAsync(page);

            if (problemCode == "owner-role-fixed")
            {
                await WaitUntilAsync(() => api.SpaceArrivalReads(Northwind).Count > arrivalsBefore);
            }

            await page.Locator(Dialog).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = CloseAction, Exact = true }).ClickAsync();
            await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Indicator).CountAsync());
            Assert.True(
                await page.EvaluateAsync<bool>("bar => !!document.activeElement && document.activeElement !== document.body && !!document.activeElement.closest(bar)", ContextBar),
                "Focus did not go to the context bar after Close.");
        }
    }

    /// <summary>
    /// Opening the dialog re-reads the offer. When the offer closed after the page last read it (the read now answers
    /// <c>204</c>), the dialog opens on the no-longer-open state, and offers no Accept and no Decline.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2 (Q5 a) - the offer dialog and its re-read on open do not exist yet")]
    [Trait("Requirement", "FR-42")]
    public async Task Opening_the_dialog_on_an_offer_that_has_closed_shows_the_no_longer_open_state()
    {
        var api = Spaces("member", offered: true);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenHomeAsync(api, page);
            await page.Locator(Indicator).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var readsBefore = api.MyOfferReads(Northwind).Count;

            api.OffersForMe.TryRemove(Northwind, out _);

            await page.Locator(Indicator).ClickAsync();
            await AssertNoLongerOpenAsync(page);

            Assert.True(api.MyOfferReads(Northwind).Count > readsBefore, "Opening the dialog did not re-read the offer.");
            Assert.Equal(0, await page.Locator(Dialog).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = AcceptAction, Exact = true }).CountAsync());
            Assert.Equal(0, await page.Locator(Dialog).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = DeclineAction, Exact = true }).CountAsync());
        }
    }

    /// <summary>
    /// Decline (Lee's Q3 (a)) sends one decline with one <c>Idempotency-Key</c> and no acceptance; the dialog closes and the
    /// indicator is removed; the Role is unchanged.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 (Q3 a) - the offer dialog and the decline do not exist yet")]
    [Trait("Requirement", "AD-18")]
    public async Task Decline_sends_one_decline_and_removes_the_indicator()
    {
        var api = Spaces("member", offered: true);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenDialogAsync(api, page);
            await page.Locator(Dialog).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = DeclineAction, Exact = true }).ClickAsync();
            await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            var sent = Assert.Single(api.DeclinesSent(Northwind));

            Assert.False(string.IsNullOrEmpty(sent.Header(Idempotency.HeaderName)), "The decline carries no Idempotency-Key.");
            Assert.Empty(api.AcceptancesSent(Northwind));
            Assert.Equal(0, await page.Locator(Indicator).CountAsync());
            Assert.Equal("member", api.Roles[Northwind]);
        }
    }

    /// <summary>
    /// The indicator and the open dialog reflow at 320 by 256 CSS pixels with no sideways scroll, and still do with the text
    /// spacing overrides applied (WCAG 1.4.10, 1.4.12).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1, AC2 - the Offer indicator and the offer dialog do not exist yet")]
    [Trait("Requirement", "UX-DR41")]
    [Trait("Requirement", "NFR-9")]
    public async Task The_indicator_and_the_dialog_reflow_at_320_by_256_with_text_spacing()
    {
        var api = Spaces("member", offered: true);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(320, 256);
            await OpenHomeAsync(api, page);
            await page.Locator(Indicator).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = TextSpacingOverride });

            Assert.Empty(await page.EvaluateAsync<string[]>(Reflow));

            await page.Locator(Indicator).ClickAsync();
            await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Empty(await page.EvaluateAsync<string[]>(Reflow));
        }
    }

    /// <summary>
    /// The former Owner's tab, open on Space settings: the acceptance happened elsewhere, and on its next request the answer
    /// carries <c>Yello-Role: admin</c>. The drop is narrated ("You're now an Admin. ...") <b>before</b> the Ownership section
    /// is removed, observed in the narration-and-removal log; afterwards the Ownership section is gone and Space settings
    /// itself stays (the Members section is still there), because an Admin keeps it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC7 - the acceptance does not exist yet, so no Owner is ever demoted to Admin")]
    [Trait("Requirement", "AD-9")]
    [Trait("Requirement", "UX-DR38")]
    public async Task The_former_Owner_in_Space_settings_is_told_before_the_Ownership_section_goes()
    {
        var api = Spaces("owner", offered: false);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenHomeAsync(api, page);
            await page.Locator(ContextBar).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = SettingsLink, Exact = true }).ClickAsync();
            await page.Locator(OwnershipSection).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.EvaluateAsync(LogNarrationAndRemovals, OwnershipSectionSelectors);

            api.Roles[Northwind] = "admin";

            await page.EvaluateAsync("() => Blazor.navigateTo(location.pathname)");
            await page.Locator(Narration, new PageLocatorOptions { HasText = NowAnAdmin }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.Locator(OwnershipSection).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            var log = await page.EvaluateAsync<string[]>("() => window.__roleLog");
            var batches = await page.EvaluateAsync<int[]>("() => window.__roleBatch");
            var narrated = Array.FindIndex(log, entry => entry == "narration:" + NowAnAdmin);
            var removed = Array.FindIndex(log, entry => entry.StartsWith("removed:", StringComparison.Ordinal));
            var described = string.Join(" | ", log.Select((entry, index) => $"{batches[index]}:{entry}"));

            Assert.True(narrated >= 0 && removed >= 0, "The log holds no narration or no removal: " + described);
            Assert.True(batches[narrated] < batches[removed], "The narration did not reach the page before the Ownership section went: " + described);
            Assert.True(await page.Locator(MembersSection).IsVisibleAsync(), "Space settings did not stay for the Admin.");
        }
    }

    /// <summary>
    /// Northwind, where the Account holds <paramref name="role"/> - with an offer from Olu Owner naming it when
    /// <paramref name="offered"/> - and Harbour Ops, where it is a Member with no offer. Northwind's Memberships are listed
    /// (an Owner, an Admin, a Member, a Viewer) so Space settings renders its sections.
    /// </summary>
    private static FakeYelloApi Spaces(string role, bool offered)
    {
        var api = new FakeYelloApi { SignedIn = true };

        api.Switcher.Add((Northwind, SpaceName));
        api.Switcher.Add((Harbour, OtherSpaceName));
        api.Roles[Northwind] = role;
        api.Roles[Harbour] = "member";
        api.SpaceMemberships[Northwind] =
        [
            Row(OffererName, "owner", isYou: role == "owner"),
            Row("Ada Admin", "admin", isYou: role == "admin"),
            Row("Mo Member", "member", isYou: role == "member"),
            Row("Vi Viewer", "viewer"),
        ];
        api.SpaceAssignees[Northwind] = [.. api.SpaceMemberships[Northwind].Select(row => (row.Id, row.DisplayName))];

        if (offered)
        {
            api.OffersForMe[Northwind] = (Guid.NewGuid(), OffererName, api.ServerNow.AddDays(7));
        }

        return api;
    }

    private static ListedMembership Row(string displayName, string role, bool isYou = false) =>
        new(Guid.NewGuid(), displayName, $"member-{Guid.NewGuid():N}@example.test", role, isYou);

    /// <summary>
    /// Waits until the dialog shows the no-longer-open state, in place, as the dialog's body and not a toast.
    /// </summary>
    private static async Task AssertNoLongerOpenAsync(IPage page)
    {
        var dialog = page.Locator(Dialog);

        await dialog.GetByText(NoLongerOpen, new LocatorGetByTextOptions { Exact = false }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

        var text = await dialog.TextContentAsync() ?? string.Empty;

        Assert.Contains(NothingChanged, text, StringComparison.Ordinal);
        Assert.DoesNotContain(OffererName, text, StringComparison.Ordinal);
        Assert.Equal(1, await dialog.GetByRole(AriaRole.Button).CountAsync());
        Assert.Equal(1, await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = CloseAction, Exact = true }).CountAsync());
    }

    /// <summary>
    /// A fenced read: any request the page had queued before this one has reached the fake once it answers.
    /// </summary>
    private static async Task RoundTripAsync(FakeYelloApi api, IPage page)
    {
        var before = api.RequestCount;

        await page.EvaluateAsync("() => Blazor.navigateTo(location.pathname)");
        await WaitUntilAsync(() => api.RequestCount > before);
    }

    /// <summary>
    /// Polls a condition the fake records, with a deadline; never a sleep.
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(Timeout);

        while (!condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "The condition was not met before the deadline.");
            await Task.Yield();
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

    private async Task OpenHomeAsync(FakeYelloApi api, IPage page)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address($"spaces/{Northwind:D}"));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    /// <summary>
    /// Opens Northwind, waits for the indicator and presses it, then waits for the dialog.
    /// </summary>
    private async Task OpenDialogAsync(FakeYelloApi api, IPage page)
    {
        await OpenHomeAsync(api, page);
        await page.Locator(Indicator).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.Locator(Indicator).ClickAsync();
        await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }
}
