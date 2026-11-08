using Microsoft.Playwright;
using Xunit;
using Yello.Contracts;

namespace Yello.Tests.Client;

/// <summary>
/// Leaving a Space, and being removed from one, as the person who goes sees it: the "Leave this Space" control at the foot
/// of Space home for every Role but the Owner, its in-place confirm, the purge and the route home after a leave, and the
/// refusal surface a removed Member's open tab meets on its next request.
/// </summary>
/// <remarks>
/// <para>
/// The API is <see cref="FakeYelloApi"/> at the network edge (<c>FakeYelloApi.Memberships.cs</c> answers the leave as the
/// Host will); no Host and no database. The suite publishes <c>Yello.Client</c> itself, so no separate client build is
/// needed. It shares the assembly's fixtures, so it starts no new engine.
/// </para>
/// <para>
/// <b>Locators are assumptions</b> from the story's copy: a button "Leave this Space" in <c>main</c> on Space home, replaced
/// in place by a <c>.destructive-confirm</c> holding the consequence, a <c>.button-danger</c> "Leave" and a "Cancel". Markup
/// built differently must be matched here too. Text is read with <c>textContent</c>.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-14")]
public sealed class LeaveSpaceTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string ContextBar = "nav.context-bar";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string SwitcherTrigger = "nav.context-bar button[aria-controls]";
    private const string Confirm = "main .destructive-confirm";
    private const string DangerButton = ".button-danger";
    private const string Heading = "main h1";

    private const string LeaveControl = "Leave this Space";
    private const string LeaveAction = "Leave";
    private const string CancelAction = "Cancel";
    private const string LeaveConsequence = "Leave this Space? You lose access to everything in it at once. To come back, someone must invite you again.";
    private const string LeftCopy = "You left the Space.";
    private const string NotSentCopy = "Not sent.";
    private const string OwnerExitsCopy = "The Owner leaves by having an Ownership Offer accepted, or by deleting the Space.";
    private const string NotAvailableCopy = "Not available.";
    private const string NorthwindName = "Northwind Redesign";
    private const string HarbourName = "Harbour Ops";

    /// <summary>
    /// Records every non-empty text a polite or status region changes to: <c>window.__said</c>.
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
    /// A 1.4.10 reflow probe, as <c>MembershipSectionTests.Reflow</c>.
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

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();

    /// <summary>
    /// "Leave this Space" is offered at the foot of Space home to an Admin, a Member and a Viewer, and is absent - not
    /// disabled - for the Owner, whose exits are an accepted Ownership Offer or deleting the Space.
    /// </summary>
    /// <param name="role">The viewer's Role in Northwind.</param>
    /// <param name="offered">Whether the control is offered.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("owner", false)]
    [InlineData("admin", true)]
    [InlineData("member", true)]
    [InlineData("viewer", true)]
    [Trait("Requirement", "UX-DR27")]
    public async Task Leave_this_Space_is_offered_to_every_Role_but_the_Owner(string role, bool offered)
    {
        var api = TwoSpaces(role);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenHomeAsync(api, page, Northwind);
            await page.Locator(Heading).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(offered ? 1 : 0, await LeaveButton(page).CountAsync());
            Assert.Equal(0, await page.Locator("main [disabled], main [aria-disabled='true']").CountAsync());
        }
    }

    /// <summary>
    /// "Leave this Space" is replaced in place by its confirm - never a dialog - stating the consequence, with "Leave" and
    /// "Cancel" and neither focused as the default. Cancel restores the control, focused, and nothing is sent.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR11")]
    [Trait("Requirement", "UX-DR36")]
    public async Task The_leave_confirm_opens_in_place_with_no_default_and_Cancel_sends_nothing()
    {
        var api = TwoSpaces("member");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenHomeAsync(api, page, Northwind);
            await LeaveButton(page).ClickAsync();

            var confirm = page.Locator(Confirm);

            await confirm.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Contains(LeaveConsequence, await confirm.TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
            Assert.Equal(1, await confirm.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = LeaveAction, Exact = true }).CountAsync());
            Assert.Equal(0, await page.Locator("[role=dialog], dialog[open]").CountAsync());
            Assert.False(
                await page.EvaluateAsync<bool>("() => !!document.activeElement && document.activeElement.tagName === 'BUTTON'"),
                "A button holds focus as the confirm's default; focus belongs on the confirm's text.");

            await confirm.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = CancelAction, Exact = true }).ClickAsync();
            await LeaveButton(page).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.True(await LeaveButton(page).EvaluateAsync<bool>("e => e === document.activeElement"), "Cancel did not return focus to the Leave control.");
            Assert.Empty(api.Leaves(Northwind));
        }
    }

    /// <summary>
    /// A leave is sent once with an <c>Idempotency-Key</c> and is never optimistic: the page stays on the Space while the
    /// answer is held. On <c>204</c> the client purges the Space and routes home, as after a Space deletion: the tab lands on
    /// the next listed Space, "You left the Space." is said with no Space name, nothing of the Space left is in the DOM, and
    /// the switcher no longer lists it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR32")]
    [Trait("Requirement", "UX-DR38")]
    public async Task A_leave_is_sent_once_with_a_key_then_purges_the_Space_and_routes_home()
    {
        var api = TwoSpaces("member");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenHomeAsync(api, page, Northwind);
            await page.EvaluateAsync(RecordEverythingSaid);

            var held = api.HoldLeaveAsync();

            await ConfirmLeaveAsync(page);
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            Assert.EndsWith($"/spaces/{Northwind:D}", new Uri(page.Url).AbsolutePath, StringComparison.Ordinal);

            api.ReleaseLeave();

            await WaitForPathAsync(page, $"/spaces/{Harbour:D}");
            await WaitUntilSaidAsync(page, LeftCopy);

            var leave = Assert.Single(api.Leaves(Northwind));
            var said = await SaidAsync(page);

            Assert.False(string.IsNullOrEmpty(leave.Header(Idempotency.HeaderName)), "The leave carried no Idempotency-Key.");
            Assert.DoesNotContain(said, sentence => sentence.Contains(NorthwindName, StringComparison.Ordinal));
            Assert.DoesNotContain(NorthwindName, await page.EvaluateAsync<string>("() => document.body.innerHTML"), StringComparison.Ordinal);
            Assert.Equal([HarbourName], await SwitcherNamesAsync(page));
        }
    }

    /// <summary>
    /// A leave whose answer was lost after it committed shows "Not sent." and keeps its key; pressing Leave again resends
    /// under the same key, meets the boundary <c>404</c> (the caller is no longer a Member), and is read as "left": the
    /// purge, the route home and "You left the Space.".
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "UX-DR38")]
    public async Task A_leave_resent_under_its_key_that_meets_404_is_read_as_left()
    {
        var api = TwoSpaces("viewer");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenHomeAsync(api, page, Northwind);
            await page.EvaluateAsync(RecordEverythingSaid);

            var held = api.HoldLeaveAsync();

            await ConfirmLeaveAsync(page);
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);
            api.ReleaseLeaveAppliedButUnanswered();

            await page.Locator("main").GetByText(NotSentCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.Locator(Confirm).Locator(DangerButton).ClickAsync();

            await WaitForPathAsync(page, $"/spaces/{Harbour:D}");
            await WaitUntilSaidAsync(page, LeftCopy);

            var sent = api.Leaves(Northwind);

            Assert.Equal(2, sent.Count);
            Assert.False(string.IsNullOrEmpty(sent[0].Header(Idempotency.HeaderName)), "The leave carried no Idempotency-Key.");
            Assert.Equal(sent[0].Header(Idempotency.HeaderName), sent[1].Header(Idempotency.HeaderName));
        }
    }

    /// <summary>
    /// The Owner's leave is unreachable from the interface, but should the Host answer <c>409 ownership-held</c> the page says
    /// how the Owner leaves, naming no third exit, and stays on the Space.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-14")]
    public async Task A_leave_answered_409_ownership_held_says_how_the_Owner_leaves()
    {
        var api = TwoSpaces("member");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenHomeAsync(api, page, Northwind);

            api.NextLeaveRefusal = (409, ProblemTypes.OwnershipHeld, null);

            await ConfirmLeaveAsync(page);
            await page.Locator("main").GetByText(OwnerExitsCopy).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.EndsWith($"/spaces/{Northwind:D}", new Uri(page.Url).AbsolutePath, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The Leave control and its confirm reflow at 320 by 256 CSS pixels with no sideways scroll, with and without the
    /// 1.4.12 text-spacing override.
    /// </summary>
    /// <param name="spaced">Whether the text-spacing override is applied.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "NFR-9")]
    public async Task The_leave_confirm_reflows_at_320_pixels_with_and_without_text_spacing(bool spaced)
    {
        var api = TwoSpaces("member");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(320, 256);
            await OpenHomeAsync(api, page, Northwind);
            await LeaveButton(page).ClickAsync();
            await page.Locator(Confirm).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            if (spaced)
            {
                await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = TextSpacingOverride });
            }

            Assert.Empty(await page.EvaluateAsync<string[]>(Reflow));
        }
    }

    /// <summary>
    /// A Member removed while its tab is open learns on its next request, through the one refusal surface: navigating within
    /// the app to the Space it was removed from renders a refusal structurally identical to a Space that never existed -
    /// "Not available." and the route back, never the word "removed". The anchor: the Space rendered for it first.
    /// <b>Green today, left active</b>: the one refusal surface already answers a Space the server stops listing, so this
    /// holds the removal to it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-15")]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "UX-DR32")]
    public async Task A_removed_Members_next_request_meets_the_same_refusal_as_a_Space_that_never_existed()
    {
        var api = TwoSpaces("member");
        var unknown = Guid.NewGuid();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenHomeAsync(api, page, Northwind);
            await ChooseInSwitcherAsync(page, HarbourName);
            await page.Locator(ContextBar, new PageLocatorOptions { HasText = HarbourName }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            // Removed by an Owner meanwhile: the Host no longer lists the Space for this Account.
            api.Switcher.RemoveAt(api.Switcher.ToList().FindIndex(space => space.Id == Northwind));
            _ = api.Roles.Remove(Northwind);

            var removed = await RefusalAfterNavigatingToAsync(page, $"/spaces/{Northwind:D}");
            var never = await RefusalAfterNavigatingToAsync(page, $"/spaces/{unknown:D}");

            Assert.Contains(NotAvailableCopy, removed, StringComparison.Ordinal);
            Assert.DoesNotContain("removed", removed, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(never, removed);
        }
    }

    /// <summary>
    /// A leave that commits after the tab has switched Space is not lost: the leave is held, the tab switches to Harbour,
    /// and the held leave is answered <c>204</c>. The switcher stops listing Northwind, nothing is said in Harbour - no "You
    /// left the Space.", which would speak of another Space - and Harbour's page is untouched. Plant: send the leave with the
    /// Space's purge token - the answer is dropped and the switcher still lists Northwind.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR38")]
    [Trait("Requirement", "UX-DR31")]
    public async Task A_leave_answered_after_a_switch_drops_the_left_Space_and_says_nothing_in_the_other()
    {
        var api = TwoSpaces("member");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenHomeAsync(api, page, Northwind);
            await page.EvaluateAsync(RecordEverythingSaid);

            var held = api.HoldLeaveAsync();

            await ConfirmLeaveAsync(page);
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);
            await ChooseInSwitcherAsync(page, HarbourName);
            await page.Locator(ContextBar, new PageLocatorOptions { HasText = HarbourName }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            api.ReleaseLeave();

            await UntilSwitcherListsAsync(page, [HarbourName]);

            Assert.DoesNotContain(await SaidAsync(page), sentence => sentence.Contains(LeftCopy, StringComparison.Ordinal));
            Assert.EndsWith($"/spaces/{Harbour:D}", new Uri(page.Url).AbsolutePath, StringComparison.Ordinal);
            Assert.Contains(HarbourName, await page.Locator(ContextBar).InnerTextAsync(), StringComparison.Ordinal);
            Assert.Single(api.Leaves(Northwind));
        }
    }

    /// <summary>
    /// A leave whose answer is lost after the tab has switched Space changes nothing in the Space now shown: nothing about
    /// the leave is said - neither "You left the Space." nor "Not sent." - and Harbour's page stays. The switcher is not asserted: it asks the server
    /// when opened, and the fake applied the leave there.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR38")]
    public async Task A_leave_left_unanswered_after_a_switch_changes_nothing_in_the_other_Space()
    {
        var api = TwoSpaces("member");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenHomeAsync(api, page, Northwind);
            await page.EvaluateAsync(RecordEverythingSaid);

            var held = api.HoldLeaveAsync();

            await ConfirmLeaveAsync(page);
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);
            await ChooseInSwitcherAsync(page, HarbourName);
            await page.Locator(ContextBar, new PageLocatorOptions { HasText = HarbourName }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            api.ReleaseLeaveAppliedButUnanswered();

            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var said = await SaidAsync(page);

            Assert.DoesNotContain(said, sentence => sentence.Contains(LeftCopy, StringComparison.Ordinal) || sentence.Contains(NotSentCopy, StringComparison.Ordinal));
            Assert.EndsWith($"/spaces/{Harbour:D}", new Uri(page.Url).AbsolutePath, StringComparison.Ordinal);
            Assert.Equal(0, await page.Locator("main").GetByText(NotSentCopy).CountAsync());
        }
    }

    /// <summary>
    /// Reads the switcher's names until they are exactly <paramref name="expected"/>, or fails at the deadline with the
    /// last names read.
    /// </summary>
    private static async Task UntilSwitcherListsAsync(IPage page, IReadOnlyList<string> expected)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(Timeout);
        IReadOnlyList<string> names = [];

        while (DateTimeOffset.UtcNow < deadline)
        {
            names = await SwitcherNamesAsync(page);

            if (names.SequenceEqual(expected, StringComparer.Ordinal))
            {
                return;
            }

            await page.Keyboard.PressAsync("Escape");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        Assert.Fail($"The switcher lists [{string.Join(", ", names)}]; expected [{string.Join(", ", expected)}].");
    }

    /// <summary>
    /// A signed-in Account holding <paramref name="northwindRole"/> in Northwind Redesign and Member of Harbour Ops, listed
    /// in that order.
    /// </summary>
    private static FakeYelloApi TwoSpaces(string northwindRole)
    {
        var api = new FakeYelloApi { SignedIn = true };

        api.Switcher.Add((Northwind, NorthwindName));
        api.Switcher.Add((Harbour, HarbourName));
        api.Roles[Northwind] = northwindRole;
        api.Roles[Harbour] = "member";

        return api;
    }

    private static ILocator LeaveButton(IPage page) =>
        page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = LeaveControl, Exact = true });

    private static async Task ConfirmLeaveAsync(IPage page)
    {
        await LeaveButton(page).ClickAsync();

        var confirm = page.Locator(Confirm);

        await confirm.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await confirm.Locator(DangerButton).ClickAsync();
    }

    private static async Task<string[]> SaidAsync(IPage page) =>
        await page.EvaluateAsync<string[]>("() => window.__said || []");

    private static async Task WaitUntilSaidAsync(IPage page, string sentence)
    {
        try
        {
            await page.WaitForFunctionAsync(
                "sentence => (window.__said || []).some(said => said.includes(sentence))",
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
        try
        {
            await page.WaitForFunctionAsync("path => location.pathname === path", path, new PageWaitForFunctionOptions { Timeout = Timeout, PollingInterval = 50 });
        }
        catch (TimeoutException)
        {
            Assert.Fail($"The page never reached {path}; it is on {new Uri(page.Url).AbsolutePath}.");
        }
    }

    private static async Task<IReadOnlyList<string>> SwitcherNamesAsync(IPage page)
    {
        var trigger = page.Locator(SwitcherTrigger);
        var list = await trigger.GetAttributeAsync("aria-controls");

        Assert.False(string.IsNullOrEmpty(list), "The switcher trigger names no list in aria-controls.");

        if (await trigger.GetAttributeAsync("aria-expanded") != "true")
        {
            await trigger.ClickAsync();
        }

        var rows = page.Locator($"[id='{list}'] a");

        await rows.First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

        return [.. (await rows.AllTextContentsAsync()).Select(text => text.Trim())];
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

    /// <summary>
    /// Navigates within the running app (no reload), waits for the refusal heading to hold focus, and reads the refusal's
    /// structure.
    /// </summary>
    private static async Task<string> RefusalAfterNavigatingToAsync(IPage page, string path)
    {
        await page.EvaluateAsync("path => Blazor.navigateTo(path)", path);
        await WaitForPathAsync(page, path);
        await page.Locator(Heading, new PageLocatorOptions { HasText = NotAvailableCopy }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.WaitForFunctionAsync(
            $"() => document.activeElement && document.activeElement.matches('{Heading}')",
            null,
            new PageWaitForFunctionOptions { Timeout = Timeout });

        return await RefusalFingerprint.ReadAsync(page);
    }

    private string Address(string path) => new Uri(surface.BaseAddress, path).ToString();

    private async Task OpenHomeAsync(FakeYelloApi api, IPage page, Guid spaceId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address($"spaces/{spaceId:D}"));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }
}
