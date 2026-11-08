using System.Text.Json;
using Microsoft.Playwright;
using Xunit;
using Yello.Contracts.Spaces;

namespace Yello.Tests.Client;

/// <summary>
/// A Role that changes while the tab is open, learned from the Role header on the tab's next answer of any kind: a drop is
/// narrated assertively before any affordance is removed, focus moves to the narration only when the focused control is
/// going away, a rise is announced politely, the Role chip and the context bar's accessible name follow, the demoted Admin
/// in Space settings is told, focused and routed to Space home with no half-rendered frame, and the narration reaches only
/// the tab whose Space changed. Nothing is polled.
/// </summary>
/// <remarks>
/// <para>
/// <b>How a change arrives.</b> <see cref="FakeYelloApi"/> echoes <c>Roles[space]</c> in the Role header on every
/// Space-scoped <c>2xx</c> and <c>403 insufficient-role</c> (<c>FakeYelloApi.RoleHeader.cs</c>), so a case changes
/// <c>api.Roles[space]</c> and then makes the tab send one ordinary request. No case re-arrives in the Space to learn it.
/// </para>
/// <para>
/// <b>Order is proven by a log, never by waiting.</b> <see cref="LogNarrationAndRemovals"/> installs a <c>MutationObserver</c>
/// before the change that records, in order, each text the narration element takes and each affordance node removed; the
/// deciding assertion is that the narration's text comes first.
/// </para>
/// <para>
/// <b>Locator assumptions</b> (correct here first if the markup differs, and record it): the narration element is
/// <c>.role-narration</c> with <c>role="alert"</c> and <c>tabindex="-1"</c>, directly under the context bar; the rise is
/// said in the context bar's existing polite region. The copy is asserted as written.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "UX-DR29")]
[Trait("Requirement", "UX-DR38")]
public sealed class RoleDropTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string ContextBar = "nav.context-bar";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string SwitcherTrigger = "nav.context-bar button[aria-controls]";
    private const string Card = ".task-card";
    private const string Dialog = "[role='dialog']";
    private const string Narration = ".role-narration";
    private const string BarPolite = "nav.context-bar [role='status'], nav.context-bar [aria-live='polite']";

    private const string NowAViewer = "You're now a Viewer. Read only in this Space.";
    private const string NowAMember = "You're now a Member. You can create and edit Projects and Tasks.";
    private const string SettingsGone = "You're now a Member. Space settings is no longer available.";
    private const string NorthwindName = "Northwind Redesign";
    private const string HarbourName = "Harbour Ops";

    /// <summary>
    /// Logs, in order, each non-empty text the narration element takes (<c>narration:...</c>) and each removal of an
    /// affordance node (<c>removed:...</c>), in <c>window.__roleLog</c>. Installed before the change, so the first entry of
    /// each kind is the first moment it happened.
    /// </summary>
    private const string LogNarrationAndRemovals =
        """
        selectors => {
            window.__roleLog = [];
            const affordance = node => node.nodeType === 1 && selectors.some(s => node.matches(s) || node.querySelector(s));
            new MutationObserver(records => {
                for (const record of records) {
                    for (const node of record.removedNodes) {
                        if (affordance(node)) window.__roleLog.push('removed:' + (node.className || node.tagName));
                    }
                    const narration = document.querySelector('.role-narration');
                    const text = narration ? narration.textContent.trim() : '';
                    if (text && window.__roleLog.at(-1) !== 'narration:' + text) window.__roleLog.push('narration:' + text);
                }
            }).observe(document.body, { childList: true, subtree: true, characterData: true });
        }
        """;

    /// <summary>
    /// Records every non-empty text a polite region changes to, and every text an alert takes: <c>window.__said</c> as
    /// <c>polite:...</c> or <c>alert:...</c>.
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

    private static readonly string[] BoardAffordances = [".board-column__add", ".task-card__actions", ".task-card--movable"];

    private static readonly string[] SpaceHomeAffordances = ["a.context-bar__settings"];

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private static readonly string[] StatusNames = ["Todo", "Doing", "Done"];

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();

    /// <summary>
    /// A Member on the Board becomes a Viewer; the next request (a same-page navigation's re-read) answers with the Viewer Role, and the
    /// narration "You're now a Viewer. Read only in this Space." is rendered <b>before</b> the first affordance - the add
    /// control, a card's actions, a drag handle - is removed. The narration names no Space, and afterwards no affordance is
    /// left. Plant: apply the Role before raising the drop - the log's order inverts.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "AD-9")]
    public async Task A_drop_on_the_Board_is_narrated_before_any_affordance_is_removed()
    {
        var api = Spaces("member");
        _ = SeedBoard(api);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page);
            await page.EvaluateAsync(LogNarrationAndRemovals, BoardAffordances);

            api.Roles[Northwind] = "viewer";

            await RequestAgainAsync(page);
            await page.Locator(Narration, new PageLocatorOptions { HasText = NowAViewer }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.Locator(".task-card__actions").First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            var log = await page.EvaluateAsync<string[]>("() => window.__roleLog");
            var narrated = Array.FindIndex(log, entry => entry == "narration:" + NowAViewer);
            var firstRemoval = Array.FindIndex(log, entry => entry.StartsWith("removed:", StringComparison.Ordinal));

            Assert.True(narrated >= 0 && firstRemoval >= 0 && narrated < firstRemoval, "The narration did not come before the first removal: " + string.Join(" | ", log));
            Assert.DoesNotContain(NorthwindName, await page.Locator(Narration).TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
            Assert.Equal("alert", await page.Locator(Narration).GetAttributeAsync("role"));

            foreach (var affordance in BoardAffordances)
            {
                Assert.Equal(0, await page.Locator(affordance).CountAsync());
            }
        }
    }

    /// <summary>
    /// Focus inside a control the drop removes - the Task panel's Title field - moves to the narration before the removal,
    /// never to <c>body</c>. The request that carries the drop is the title edit itself, sent with Enter, so focus is in the
    /// field when the answer lands.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR39")]
    public async Task Focus_in_a_removed_control_moves_to_the_narration_and_never_to_the_body()
    {
        var api = Spaces("member");
        var tasks = SeedBoard(api);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page);
            await OpenPanelAsync(page, tasks[0].Id);

            var title = page.Locator(Dialog).GetByLabel("Title", new LocatorGetByLabelOptions { Exact = true });

            await title.FillAsync("Edited as the drop lands");

            api.Roles[Northwind] = "viewer";

            await title.PressAsync("Enter");
            await page.Locator(Narration, new PageLocatorOptions { HasText = NowAViewer }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await title.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.True(await page.Locator(Narration).EvaluateAsync<bool>("e => e === document.activeElement"), "Focus is not on the narration.");
            Assert.Equal("-1", await page.Locator(Narration).GetAttributeAsync("tabindex"));
        }
    }

    /// <summary>
    /// Focus on something the drop keeps - a card - stays where it is: the narration is assertive and needs no focus to be
    /// heard. The request that carries the drop is a same-Space navigation's re-read, which keeps the Board and its cards.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR39")]
    public async Task Focus_on_a_control_the_drop_keeps_stays_where_it_is()
    {
        var api = Spaces("member");
        var tasks = SeedBoard(api);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page);

            var card = page.Locator($"{Card} button[data-task-id='{tasks[1].Id:D}']");

            await card.FocusAsync();

            api.Roles[Northwind] = "viewer";

            await page.EvaluateAsync("path => Blazor.navigateTo(path)", $"/spaces/{Northwind:D}/projects/{Atlas:D}");
            await page.Locator(Narration, new PageLocatorOptions { HasText = NowAViewer }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.True(await card.EvaluateAsync<bool>("e => e === document.activeElement"), "Focus left the card, which the drop keeps.");
        }
    }

    /// <summary>
    /// An Admin on Space home becomes a Member; the next request (a Project created, which a Member may still do) answers
    /// with the Member Role; "You're now a Member." is narrated before the context bar's Space-settings link is removed.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    public async Task An_Admin_on_Space_home_demoted_to_Member_loses_the_settings_link_after_the_narration()
    {
        var api = Spaces("admin");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenHomeAsync(api, page);
            await page.Locator(SpaceHomeAffordances[0]).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.EvaluateAsync(LogNarrationAndRemovals, SpaceHomeAffordances);

            api.Roles[Northwind] = "member";

            await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Create a Project" }).First.ClickAsync();

            var field = page.Locator("main input[type='text']").First;

            await field.FillAsync("Made as the drop lands");
            await field.PressAsync("Enter");
            await page.Locator(Narration, new PageLocatorOptions { HasText = NowAMember }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.Locator(SpaceHomeAffordances[0]).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            var log = await page.EvaluateAsync<string[]>("() => window.__roleLog");

            Assert.True(
                Array.FindIndex(log, entry => entry == "narration:" + NowAMember) < Array.FindIndex(log, entry => entry.StartsWith("removed:", StringComparison.Ordinal)),
                "The settings link went before the narration: " + string.Join(" | ", log));
        }
    }

    /// <summary>
    /// A rise - Viewer to Member - applies at once: no alert, a polite "You're now a Member." in the context bar's region,
    /// focus unmoved, and the Member's controls appear.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR28")]
    public async Task A_rise_is_said_politely_and_the_controls_appear()
    {
        var api = Spaces("viewer");
        _ = SeedBoard(api);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page);
            await page.EvaluateAsync(RecordEverythingSaid);

            api.Roles[Northwind] = "member";

            await RequestAgainAsync(page);
            await page.Locator(BarPolite, new PageLocatorOptions { HasText = NowAMember }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.Locator(".task-card__actions").First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var said = await page.EvaluateAsync<string[]>("() => window.__said");

            Assert.DoesNotContain(said, sentence => sentence.StartsWith("alert:", StringComparison.Ordinal));
            Assert.Equal(1, said.Count(sentence => sentence == "polite:" + NowAMember));
        }
    }

    /// <summary>
    /// The Role chip and the context bar's accessible name follow a header-driven drop (Member to Viewer) and a rise back
    /// (Viewer to Member), each to the new Role and what it permits; the chip stays non-interactive; and a drop is said once,
    /// by the narration - the bar's polite switch announcement says nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR28")]
    [Trait("Requirement", "X-8")]
    public async Task The_chip_and_the_accessible_name_follow_a_drop_and_a_rise()
    {
        var api = Spaces("member");
        _ = SeedBoard(api);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page);
            await page.EvaluateAsync(RecordEverythingSaid);

            api.Roles[Northwind] = "viewer";

            await RequestAgainAsync(page);
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal($"{NorthwindName} — Viewer, read only in this Space", await page.Locator(ContextBar).GetAttributeAsync("aria-label"));
            Assert.Equal(0, await page.Locator("nav.context-bar .role-chip a, nav.context-bar a .role-chip, nav.context-bar button .role-chip, nav.context-bar .role-chip button").CountAsync());

            api.Roles[Northwind] = "member";

            await RequestAgainAsync(page);
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Member" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal($"{NorthwindName} — Member, can create and edit Projects and Tasks", await page.Locator(ContextBar).GetAttributeAsync("aria-label"));

            var said = await page.EvaluateAsync<string[]>("() => window.__said");

            Assert.Equal(1, said.Count(sentence => sentence.Contains(NowAViewer, StringComparison.Ordinal)));
        }
    }

    /// <summary>
    /// The demoted Admin in Space settings, learned from an ordinary answer (the Space renamed, <c>200</c> carrying
    /// <c>member</c>): told exactly "You're now a Member. Space settings is no longer available.", focus on the narration,
    /// then routed to Space home - in that order - and at no recorded frame is the settings <c>main</c> empty or showing the
    /// <c>manage-labels</c> refusal paragraph. On Space home the narration persists and the "Projects" heading does not take
    /// focus from it. Plants: route before narrating; leave focus on the heading; re-render settings under the lower Role.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR39")]
    [Trait("Requirement", "UX-DR29")]
    public async Task The_demoted_Admin_in_Space_settings_is_told_focused_then_routed_and_never_half_rendered()
    {
        var api = Spaces("admin");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenHomeAsync(api, page);
            await page.Locator(ContextBar).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Space settings", Exact = true }).ClickAsync();
            await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Rename", Exact = true }).ClickAsync();
            await page.EvaluateAsync(
                """
                () => {
                    window.__frames = [];
                    new MutationObserver(() => {
                        const main = document.querySelector('main');
                        window.__frames.push({
                            path: location.pathname,
                            empty: !main || main.textContent.trim() === '',
                            refusal: !!document.querySelector('.space-settings__refusal'),
                            narrated: !!document.querySelector('.role-narration') && document.querySelector('.role-narration').textContent.trim() !== '',
                            focused: document.activeElement && document.activeElement.matches('.role-narration'),
                        });
                    }).observe(document.body, { childList: true, subtree: true, characterData: true, attributes: true });
                }
                """);

            api.Roles[Northwind] = "member";

            var field = page.GetByLabel("Space name", new PageGetByLabelOptions { Exact = true });

            await field.FillAsync(NorthwindName);
            await field.PressAsync("Enter");
            await page.WaitForFunctionAsync("path => location.pathname === path", $"/spaces/{Northwind:D}", new PageWaitForFunctionOptions { Timeout = Timeout, PollingInterval = 50 });
            await page.Locator(Narration, new PageLocatorOptions { HasText = SettingsGone }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var frames = JsonSerializer.Deserialize<Frame[]>(await page.EvaluateAsync<string>("() => JSON.stringify(window.__frames)"), WebJson) ?? [];
            var settings = $"/spaces/{Northwind:D}/settings";
            var firstHome = Array.FindIndex(frames, frame => frame.Path != settings);

            Assert.True(firstHome > 0, "The page never left Space settings.");
            Assert.True(Array.Exists(frames[..firstHome], frame => frame.Narrated && frame.Focused), "The narration was not rendered and focused while still on Space settings.");
            Assert.DoesNotContain(frames, frame => frame.Path == settings && (frame.Empty || frame.Refusal));
            Assert.True(await page.Locator(Narration).EvaluateAsync<bool>("e => e === document.activeElement"), "On Space home the narration lost focus.");
        }
    }

    /// <summary>
    /// Two tabs of one Account: tab 1 on Northwind's Board, tab 2 on Harbour. Northwind's Role drops; tab 1 narrates on its next
    /// request; tab 2, which also requests, never shows the narration and its chip is unchanged. Plant: a
    /// <c>BroadcastChannel</c> post of the narration - tab 2 then narrates.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR38")]
    [Trait("Requirement", "UX-DR31")]
    public async Task A_drop_is_narrated_only_in_the_tab_showing_that_Space()
    {
        var api = Spaces("member");
        _ = SeedBoard(api);
        var (context, first) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            var second = await context.NewPageAsync();

            await OpenBoardAsync(api, first);
            await api.InstallAsync(second);
            await second.GotoAsync(Address($"spaces/{Harbour:D}"));
            await second.Locator(RoleChip, new PageLocatorOptions { HasText = "Member" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await second.EvaluateAsync(RecordEverythingSaid);

            api.Roles[Northwind] = "viewer";

            await RequestAgainAsync(first);
            await first.Locator(Narration, new PageLocatorOptions { HasText = NowAViewer }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            // Tab 2 makes a request of its own after the drop: a same-Space navigation re-reads Harbour.
            var before = api.RequestCount;

            await second.RunAndWaitForResponseAsync(
                () => second.EvaluateAsync("path => Blazor.navigateTo(path)", $"/spaces/{Harbour:D}"),
                response => response.Url.Contains($"/spaces/{Harbour:D}", StringComparison.Ordinal),
                new PageRunAndWaitForResponseOptions { Timeout = Timeout });
            await second.WaitForLoadStateAsync(LoadState.NetworkIdle);

            Assert.True(api.RequestCount > before, "Tab 2 sent nothing after the drop, so its silence proves nothing.");
            Assert.Equal(0, await second.Locator(Narration, new PageLocatorOptions { HasText = "You're now" }).CountAsync());
            Assert.DoesNotContain(await second.EvaluateAsync<string[]>("() => window.__said"), sentence => sentence.Contains("You're now", StringComparison.Ordinal));
            Assert.Equal("Member", (await second.Locator(RoleChip).TextContentAsync() ?? string.Empty).Trim());
        }
    }

    /// <summary>
    /// A late answer for the Space the tab has left is dropped: a Board read in Northwind is held, the tab switches to
    /// Harbour, and the held read is released carrying <c>viewer</c> for Northwind - no narration in Harbour, and Harbour's
    /// chip is unchanged. Plant: remove the Space check from the client's one Role entry point - this case goes red, unless
    /// the switch aborts the held request before its answer reaches the handler; confirm the plant reddens it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR38")]
    [Trait("Requirement", "UX-DR31")]
    public async Task A_late_answer_for_the_Space_the_tab_left_is_never_narrated()
    {
        var api = Spaces("member");
        _ = SeedBoard(api);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenHomeAsync(api, page);
            await page.EvaluateAsync(RecordEverythingSaid);

            var held = api.HoldSpaceReadAsync(Northwind, "/board");

            await page.Locator("main").GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Atlas" }).First.ClickAsync();
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            await ChooseInSwitcherAsync(page, HarbourName);
            await page.Locator(ContextBar, new PageLocatorOptions { HasText = HarbourName }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            api.Roles[Northwind] = "viewer";
            api.ReleaseSpaceRead();

            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            Assert.Equal(0, await page.Locator(Narration, new PageLocatorOptions { HasText = "You're now" }).CountAsync());
            Assert.DoesNotContain(await page.EvaluateAsync<string[]>("() => window.__said"), sentence => sentence.Contains("You're now", StringComparison.Ordinal));
            Assert.Equal("Member", (await page.Locator(RoleChip).TextContentAsync() ?? string.Empty).Trim());
        }
    }

    /// <summary>
    /// Nothing is polled: with the page's timers fast-forwarded a minute while the tab sits idle on the Board, the client
    /// sends no request. Anchor: loading the Board sent requests. Plant: a client <c>PeriodicTimer</c> re-reading the Role.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-9")]
    [Trait("Requirement", "AD-10")]
    public async Task An_idle_tab_sends_no_request()
    {
        var api = Spaces("member");
        _ = SeedBoard(api);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.Clock.InstallAsync();
            await OpenBoardAsync(api, page);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var loaded = api.RequestCount;

            Assert.True(loaded > 0, "Loading the Board sent nothing, so an idle count proves nothing.");

            await page.Clock.RunForAsync(60_000);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            Assert.Equal(loaded, api.RequestCount);
            Assert.DoesNotContain(api.Requests.Skip(loaded), request => request.Path.StartsWith(SpaceRoutes.Directory, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Northwind, where the Account holds <paramref name="role"/>, and Harbour, where it is a Member.
    /// </summary>
    private static FakeYelloApi Spaces(string role)
    {
        var api = new FakeYelloApi { SignedIn = true };

        api.Switcher.Add((Northwind, NorthwindName));
        api.Switcher.Add((Harbour, HarbourName));
        api.Roles[Northwind] = role;
        api.Roles[Harbour] = "member";
        api.Projects[Northwind] = [new FakeProject(Atlas, "Atlas", DateTimeOffset.UtcNow)];

        return api;
    }

    /// <summary>
    /// Atlas's Board in Northwind: three columns, three Tasks in the first.
    /// </summary>
    private static List<FakeTask> SeedBoard(FakeYelloApi api)
    {
        var columns = StatusNames.Select(status => new FakeColumn(Guid.NewGuid(), status, 0, [])).ToList();

        foreach (var title in new[] { "First", "Second", "Third" })
        {
            columns[0].Tasks.Add(new FakeTask(Guid.NewGuid(), title, columns[0].StatusId, $"a{columns[0].Tasks.Count:D5}", DateTimeOffset.UtcNow, LabelIds: []));
            columns[0].Total++;
        }

        api.Boards[(Northwind, Atlas)] = new FakeBoard(new FakeBoardProject(Atlas, "Atlas"), columns);

        return [.. columns[0].Tasks];
    }

    /// <summary>
    /// Sends the tab's next ordinary request: a navigation to the address it already shows re-reads the Space in place.
    /// Opening a Task panel would not do - the panel reads the Board's replica and sends nothing.
    /// </summary>
    private static Task RequestAgainAsync(IPage page) =>
        page.EvaluateAsync("() => Blazor.navigateTo(location.pathname)");

    private static async Task OpenPanelAsync(IPage page, Guid taskId)
    {
        await page.Locator($"{Card} button[data-task-id='{taskId:D}']").ClickAsync(new LocatorClickOptions { Timeout = Timeout });
        await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
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

    private async Task OpenBoardAsync(FakeYelloApi api, IPage page)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address($"spaces/{Northwind:D}/projects/{Atlas:D}"));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.Locator(Card).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    /// <summary>
    /// One recorded frame of the Space-settings exit.
    /// </summary>
    private sealed record Frame(string Path, bool Empty, bool Refusal, bool Narrated, bool Focused);
}
