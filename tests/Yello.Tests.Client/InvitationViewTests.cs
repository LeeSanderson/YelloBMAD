using Microsoft.Playwright;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Accounts;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// Story 4.3 in a real browser: the Invitation view at <c>/invitations/{token}</c> - reachable without a Session, naming the
/// Space, the Role and the issuer under a fixed title with no preview metadata; never sending the accept on navigation;
/// accepting once, not optimistically, then landing in the Space; the uniform "no longer valid" copy; the different-Account
/// copy without an address and with sign-out; the bound's copy; registration chained to sign-in and the accept; sign-in
/// inline; "Not sent." with the key kept; the 320px reflow. AC13 (with AC2, AC3, AC4, AC6, AC7, AC9 as the client shows
/// them); FR-11, UX-DR18, UX-DR27, UX-DR31, UX-DR35, UX-DR38, UX-DR39, NFR-9.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 4.3 ATDD run (2026-11-01).</b> Each is skipped with AC13 (or the AC it
/// shows) and what it waits for. The API is <see cref="FakeYelloApi"/> at the network edge, taught the view, the accept and
/// registration by this run (<c>FakeYelloApi.InvitationAcceptance.cs</c>); no Host, no database, no engine. Build
/// <c>Yello.Client</c> in Debug before running them. Same collection as <see cref="InvitationTests"/>, so no new fixture.
/// </para>
/// <para>
/// <b>Locators are this run's assumptions</b>, written from the story's AC13 and Q15 (a) copy: the page's one
/// <c>h1</c>; the actions by their accessible names; the registration form's three labelled fields ("Display name", "Email
/// address", "Password", as <c>Register.razor</c>'s). Text is read with <c>textContent</c>, never <c>innerText</c>, so CSS
/// upper-casing (UX-DR42) cannot change it. A different locator in the built page is corrected here and recorded in the
/// Debug Log, never by weakening the assertion.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-11")]
[Trait("Requirement", "UX-DR18")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class InvitationViewTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string Heading = "main h1";

    private const string SpaceName = "Northwind Redesign";
    private const string IssuerName = "Ravi Anand";
    private const string InvitedAddress = "invited.person@example.test";
    private const string SignedInAddress = "someone.else@example.test";

    private const string OfferHeading = IssuerName + " invited you to " + SpaceName + ".";
    private const string JoinAsMember = "Join as Member.";
    private const string JoinAction = "Join " + SpaceName;
    private const string CreateAndJoinAction = "Create an Account and join";
    private const string SignInPrompt = "Already have an Account? Sign in";
    private const string OwnSpaceFooter = "You'll also get a Space of your own.";
    private const string JoiningCopy = "Joining.";
    private const string JoinedCopy = "You joined " + SpaceName + ".";
    private const string NotValidHeading = "This invitation is no longer valid.";
    private const string NotValidBody = "Ask whoever invited you to send a new one.";
    private const string GoToYello = "Go to Yello";
    private const string OtherAccountHeading = "This invitation is for a different Account.";
    private const string OtherAccountBody = "Sign out and sign in as the Account it was sent to.";
    private const string BoundCopy = "This Space has 100 Memberships, the maximum.";
    private const string NotSentCopy = "Not sent.";

    /// <summary>
    /// A 1.4.10 reflow probe over the page, as <c>InvitationListTests.Reflow</c>.
    /// </summary>
    private const string Reflow =
        """
        () => {
            const problems = [];
            const page = document.scrollingElement;
            if (page.scrollWidth > page.clientWidth + 1) problems.push('the page scrolls sideways by ' + (page.scrollWidth - page.clientWidth) + 'px');
            for (const element of document.querySelectorAll('main *')) {
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

    private static readonly Guid Northwind = Guid.NewGuid();

    /// <summary>
    /// AC2, UX-DR18, <c>EXPERIENCE.md:177</c> - with no Session, the link opens the offer: one <c>h1</c> "{Issuer} invited
    /// you to {Space}.", "Join as Member.", the footer "You'll also get a Space of your own.", "Create an Account and join"
    /// and the inline sign-in prompt; no context bar; <c>document.title</c> is exactly <c>Yello</c>; no <c>og:</c> or
    /// <c>twitter:</c> meta; a <c>robots noindex</c> meta; the URL carries the token and no Space name or id. The view read
    /// carried the token in its header, not its URL (Lee's Q4 (a)).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2 / AC13 - InvitationView.razor at /invitations/{Token} does not exist; Task 7, Q15 (a) copy")]
    [Trait("Requirement", "UX-DR18")]
    [Trait("Requirement", "NFR-9")]
    public async Task The_offer_names_the_Space_the_Role_and_the_issuer_under_a_fixed_title_with_no_preview_metadata()
    {
        var (api, token) = Offered(signedIn: false);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenViewAsync(api, page, token);
            await page.Locator(Heading, new PageLocatorOptions { HasText = OfferHeading }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var main = await page.Locator("main").TextContentAsync() ?? string.Empty;

            Assert.Equal(1, await page.Locator("main h1").CountAsync());
            Assert.Contains(JoinAsMember, main, StringComparison.Ordinal);
            Assert.Contains(OwnSpaceFooter, main, StringComparison.Ordinal);
            Assert.Equal(1, await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = CreateAndJoinAction, Exact = true }).CountAsync());
            Assert.Contains(SignInPrompt, main, StringComparison.Ordinal);
            Assert.Equal(0, await page.Locator("nav.context-bar").CountAsync());
            Assert.Equal("Yello", await page.TitleAsync());
            Assert.Equal(0, await page.Locator("meta[property^='og:'], meta[name^='og:'], meta[name^='twitter:'], meta[property^='twitter:']").CountAsync());
            Assert.Equal("noindex", await page.Locator("meta[name='robots']").GetAttributeAsync("content"));

            var url = page.Url;

            Assert.DoesNotContain(SpaceName.Split(' ')[0], url, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Northwind.ToString("D"), url, StringComparison.OrdinalIgnoreCase);

            var read = Assert.Single(api.InvitationOfferReads);

            Assert.Equal(token, read.Header(InvitationWire.TokenHeaderName));
            Assert.DoesNotContain(token, read.Path + read.Query, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC4, AC13, FR-11 <c>:278</c>, plant 10 - opening the link as the addressee sends no accept: the view is read, "Join
    /// {Space}" is offered, and the fake has recorded no accept. Anchor: clicking it sends exactly one accept, carrying
    /// <c>{ invitationToken }</c> and an <c>Idempotency-Key</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC4 / AC13 - InvitationView.razor does not exist; Task 7, Q11 (a) Idempotency-Key per click")]
    [Trait("Requirement", "AD-28")]
    [Trait("Requirement", "AD-18")]
    public async Task Opening_the_link_sends_no_accept_and_clicking_Join_sends_exactly_one()
    {
        var (api, token) = Offered(signedIn: true);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenViewAsync(api, page, token);

            var join = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = JoinAction, Exact = true });

            await join.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.NotEmpty(api.InvitationOfferReads);
            Assert.Empty(api.InvitationAccepts);

            await join.ClickAsync();
            await WaitForPathAsync(page, $"/spaces/{Northwind:D}");

            var accept = Assert.Single(api.InvitationAccepts);

            Assert.Contains(token, accept.Body ?? string.Empty, StringComparison.Ordinal);
            Assert.False(string.IsNullOrEmpty(accept.Header(Idempotency.HeaderName)), "The accept carried no Idempotency-Key.");
        }
    }

    /// <summary>
    /// AC13, UX-DR31, UX-DR38, Q19 (a) - the accept is never optimistic: while it is held the page stays on the invitation,
    /// says "Joining." and will not resend; on the <c>200</c> it lands on the Space's home through the client's navigation,
    /// the switcher lists the new Space, and "You joined {Space}." is said - with no address in anything said.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC13 - InvitationView.razor does not exist; Task 7, Q15 (a) copy, Q19 (a) landing")]
    [Trait("Requirement", "UX-DR31")]
    [Trait("Requirement", "UX-DR38")]
    public async Task The_accept_waits_for_its_answer_then_lands_in_the_joined_Space_and_says_so()
    {
        var (api, token) = Offered(signedIn: true);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenViewAsync(api, page, token);
            await page.EvaluateAsync(RecordEverythingSaid);

            var held = api.HoldAcceptAsync();

            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = JoinAction, Exact = true }).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            await held;

            Assert.StartsWith("/invitations/", await page.EvaluateAsync<string>("() => location.pathname"), StringComparison.Ordinal);
            Assert.Contains(JoiningCopy, await page.Locator("main").TextContentAsync() ?? string.Empty, StringComparison.Ordinal);

            api.ReleaseAccept();

            await WaitForPathAsync(page, $"/spaces/{Northwind:D}");
            await page.WaitForFunctionAsync("sentence => (window.__said || []).some(said => said.includes(sentence))", JoinedCopy, new PageWaitForFunctionOptions { Timeout = Timeout });

            Assert.Single(api.InvitationAccepts);
            Assert.All(await page.EvaluateAsync<string[]>("() => window.__said"), said => Assert.DoesNotContain("@", said, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// AC6, UX-DR18, Lee's Q6 (a) - a token the server answers <c>404 invitation-not-valid</c> shows only "This invitation is
    /// no longer valid.", "Ask whoever invited you to send a new one." and a "Go to Yello" link: no Space, no issuer, no
    /// action. Anchor: a live token's offer names them (the first case).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC6 / AC13 - InvitationView.razor and the invitation-not-valid copy do not exist; Task 7, Q15 (a) copy")]
    [Trait("Requirement", "UX-DR18")]
    public async Task A_token_no_longer_valid_shows_the_uniform_copy_and_nothing_of_the_Space()
    {
        var (api, _) = Offered(signedIn: false);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenViewAsync(api, page, InvitationWire.UnrecognisedToken());
            await page.Locator(Heading, new PageLocatorOptions { HasText = NotValidHeading }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var main = await page.Locator("main").TextContentAsync() ?? string.Empty;

            Assert.Contains(NotValidBody, main, StringComparison.Ordinal);
            Assert.Equal(1, await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = GoToYello, Exact = true }).CountAsync());
            Assert.DoesNotContain(SpaceName, main, StringComparison.Ordinal);
            Assert.DoesNotContain(IssuerName, main, StringComparison.Ordinal);
            Assert.Equal(0, await page.Locator("main button").CountAsync());
        }
    }

    /// <summary>
    /// AC7, UX-DR18, <c>EXPERIENCE.md:265</c>, plant 9 - a different Account signed in is told "This invitation is for a
    /// different Account." and "Sign out and sign in as the Account it was sent to.", offered "Sign out" and "Cancel", and
    /// the page holds neither address and no <c>@</c>. Signing out (server-side, <c>SessionsValidAfter</c>) returns to the same
    /// invitation, which re-renders the offer for no Session.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC7 / AC13 - InvitationView.razor and the different-Account copy do not exist; Task 7, Lee's Q5 (a), Q15 (a) copy")]
    [Trait("Requirement", "UX-DR18")]
    [Trait("Requirement", "UX-DR38")]
    public async Task A_different_Account_is_told_without_either_address_and_can_sign_out_back_to_the_invitation()
    {
        var (api, token) = Offered(signedIn: true);

        api.AddresseeSignedIn = false;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenViewAsync(api, page, token);
            await page.Locator(Heading, new PageLocatorOptions { HasText = OtherAccountHeading }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var body = await page.Locator("body").TextContentAsync() ?? string.Empty;

            Assert.Contains(OtherAccountBody, body, StringComparison.Ordinal);
            Assert.DoesNotContain("@", body, StringComparison.Ordinal);
            Assert.DoesNotContain(InvitedAddress.Split('@')[0], body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(SignedInAddress.Split('@')[0], body, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, await page.Locator("main .button-secondary", new PageLocatorOptions { HasText = "Cancel" }).CountAsync());

            await page.Locator("main .button-primary", new PageLocatorOptions { HasText = "Sign out" }).ClickAsync();
            await api.SignOutSeen.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);
            await WaitForPathAsync(page, InvitationWire.LinkPath + token);
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = CreateAndJoinAction, Exact = true }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        }
    }

    /// <summary>
    /// AC9, UX-DR35 - an accept refused <c>409 bound-reached</c> for <c>memberships-per-space</c> says "This Space has 100
    /// Memberships, the maximum." on the view, and the page stays on the invitation.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 / AC13 - InvitationView.razor does not exist; Task 7 (RefusalCopy's memberships-per-space)")]
    [Trait("Requirement", "UX-DR35")]
    [Trait("Requirement", "AR-30")]
    public async Task A_full_Space_says_its_bound_on_the_view()
    {
        var (api, token) = Offered(signedIn: true);

        api.NextAcceptRefusal = (409, ProblemTypes.BoundReached, InvitationWire.MembershipsPerSpaceBoundCode, InvitationWire.MembershipsPerSpace);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenViewAsync(api, page, token);
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = JoinAction, Exact = true }).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            await page.GetByText(BoundCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.StartsWith("/invitations/", await page.EvaluateAsync<string>("() => location.pathname"), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC3, AC13, Lee's Q3 (a) - "Create an Account and join" shows the three-field form with the email field empty (the
    /// invited address is never echoed); submitting it chains <c>POST /api/v1/accounts</c>, then sign-in, then the accept,
    /// in that order, re-fetching the anti-forgery token after sign-in; the person lands in the joined Space.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 / AC13 - InvitationView.razor and its registration chain do not exist; Task 7, Lee's Q3 (a)")]
    [Trait("Requirement", "FR-4")]
    [Trait("Requirement", "AD-22")]
    public async Task Registering_from_the_view_chains_register_then_sign_in_then_accept()
    {
        var (api, token) = Offered(signedIn: false);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenViewAsync(api, page, token);
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = CreateAndJoinAction, Exact = true }).ClickAsync(new LocatorClickOptions { Timeout = Timeout });

            var email = page.GetByLabel("Email address", new PageGetByLabelOptions { Exact = true });

            await email.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(string.Empty, await email.InputValueAsync());

            await page.GetByLabel("Display name", new PageGetByLabelOptions { Exact = true }).FillAsync("New Person");
            await email.FillAsync(InvitedAddress);
            await page.GetByLabel("Password", new PageGetByLabelOptions { Exact = true }).FillAsync("a-long-enough-password-1!");
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = CreateAndJoinAction, Exact = true }).ClickAsync();

            await WaitForPathAsync(page, $"/spaces/{Northwind:D}");

            var order = api.Requests
                .Where(request => request.Method == "POST")
                .Select(request => request.Path)
                .Where(path => path == AccountRoutes.Register || path == SessionRoutes.Authenticate || path == InvitationWire.AcceptanceRoute)
                .ToList();

            Assert.Equal([AccountRoutes.Register, SessionRoutes.Authenticate, InvitationWire.AcceptanceRoute], order);

            var requests = api.Requests.ToList();
            var signIn = requests.FindIndex(request => request.Method == "POST" && request.Path == SessionRoutes.Authenticate);
            var accept = requests.FindIndex(request => request.Method == "POST" && request.Path == InvitationWire.AcceptanceRoute);

            Assert.Contains(requests.Skip(signIn).Take(accept - signIn), request => request.Path == SessionRoutes.AntiforgeryToken);
        }
    }

    /// <summary>
    /// AC13, Q16 (a) - "Already have an Account? Sign in" signs in inline on the view (no navigation, no return-URL
    /// parameter): the page stays at the invitation URL, the view is re-read with the Session (the addressee indicator now
    /// applies) and "Join {Space}" is offered; no request's URL ever carries the token.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC13 - InvitationView.razor and its inline sign-in do not exist; Task 7, Q16 (a)")]
    [Trait("Requirement", "UX-DR39")]
    [Trait("Requirement", "NFR-6")]
    public async Task Signing_in_from_the_view_happens_inline_and_the_token_stays_out_of_every_request_URL()
    {
        var (api, token) = Offered(signedIn: false);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenViewAsync(api, page, token);
            await page.GetByText("Sign in", new PageGetByTextOptions { Exact = true }).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            await page.GetByLabel("Email address", new PageGetByLabelOptions { Exact = true }).FillAsync(InvitedAddress);
            await page.GetByLabel("Password", new PageGetByLabelOptions { Exact = true }).FillAsync("a-long-enough-password-1!");
            await page.Locator("main form").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Sign in", Exact = true }).ClickAsync();

            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = JoinAction, Exact = true }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(InvitationWire.LinkPath + token, await page.EvaluateAsync<string>("() => location.pathname"));
            Assert.True(api.InvitationOfferReads.Count >= 2, "The view was not re-read after sign-in.");
            Assert.All(api.Requests, request => Assert.DoesNotContain(token, request.Path + request.Query, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// AC13, NFR-9, UX-DR39 - focus starts on the heading region once the offer has loaded; an accept that gets no answer
    /// says "Not sent." and the resend carries the same <c>Idempotency-Key</c>, so a lost answer is never a second act.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC13 - InvitationView.razor does not exist; Task 7")]
    [Trait("Requirement", "UX-DR39")]
    [Trait("Requirement", "AD-18")]
    public async Task Focus_starts_on_the_heading_and_an_unanswered_accept_says_Not_sent_and_keeps_its_key()
    {
        var (api, token) = Offered(signedIn: true);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenViewAsync(api, page, token);
            await page.Locator(Heading, new PageLocatorOptions { HasText = OfferHeading }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.WaitForFunctionAsync("() => document.activeElement && document.activeElement !== document.body && (document.activeElement.matches('main h1') || document.activeElement.contains(document.querySelector('main h1')))", null, new PageWaitForFunctionOptions { Timeout = Timeout });

            var held = api.HoldAcceptAsync();
            var join = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = JoinAction, Exact = true });

            await join.ClickAsync();
            await held;
            api.ReleaseAcceptUnanswered();

            await page.GetByText(NotSentCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await join.ClickAsync();
            await WaitForPathAsync(page, $"/spaces/{Northwind:D}");

            var keys = api.InvitationAccepts.Select(request => request.Header(Idempotency.HeaderName)).ToList();

            Assert.Equal(2, keys.Count);
            Assert.Single(keys.Distinct(StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// AC13, NFR-9, WCAG 1.4.10 - at 320 by 256 (1280 by 1024 at 400%) the offer reflows: no sideways page scroll and no
    /// element in <c>main</c> that scrolls sideways.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC13 - InvitationView.razor does not exist; Task 7")]
    [Trait("Requirement", "NFR-9")]
    public async Task The_view_reflows_at_320_by_256()
    {
        var (api, token) = Offered(signedIn: false);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(320, 256);
            await OpenViewAsync(api, page, token);
            await page.Locator(Heading, new PageLocatorOptions { HasText = OfferHeading }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var problems = await page.EvaluateAsync<string[]>(Reflow);

            Assert.True(problems.Length == 0, string.Join("; ", problems));
        }
    }

    /// <summary>
    /// A fake holding one live Invitation's offer to Northwind as a Member, issued by Ravi Anand; signed in (as the addressee)
    /// or not.
    /// </summary>
    internal static (FakeYelloApi Api, string Token) Offered(bool signedIn)
    {
        var api = new FakeYelloApi { SignedIn = signedIn };
        var token = InvitationWire.UnrecognisedToken();

        api.Offers[token] = (new OfferedInvitation(SpaceName, "member", IssuerName, AddressedToYou: null), Northwind);

        return (api, token);
    }

    private static Task WaitForPathAsync(IPage page, string path) =>
        page.WaitForFunctionAsync("path => location.pathname === path", path, new PageWaitForFunctionOptions { Timeout = Timeout });

    private async Task OpenViewAsync(FakeYelloApi api, IPage page, string token)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(new Uri(surface.BaseAddress, InvitationWire.LinkPath.TrimStart('/') + token).ToString());
    }
}
