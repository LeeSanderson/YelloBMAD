using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;
using Yello.Contracts.Labels;

namespace Yello.Tests.Client;

/// <summary>
/// Story 3.4 in a real browser (written by its ATDD run, 2026-10-31): AC5 - after a switch from a populated Space, nothing
/// of the departed Space (Task title, Project name, Label, Status name or count) is in the DOM or in client memory - and
/// AC13 - every exit from a Space purges the departed Space before the destination renders, whoever navigates.
/// </summary>
/// <remarks>
/// <para>
/// <b>AC6, Q4 (a): the API counterpart.</b> These cases are browser-only. Their API side is the isolation rows for the
/// same two-Space Account: <c>ISO-TWO-SPACES-STATUSES</c>, <c>-PROJECTS</c>, <c>-BOARD</c>, <c>-BOARD-COLUMN</c>,
/// <c>-LIST</c>, <c>-LABELS</c> and <c>-SWITCHER</c> (<c>IsolationCases.SeveralSpaces.cs</c>).
/// </para>
/// <para>
/// <b>Counts the destination never shows.</b> The departed Board's columns total 13, 17 and 19 and its Project counts 49;
/// the destination's total 1 each and 3. A count chip or a text node holding any departed number is a survivor.
/// </para>
/// <para>
/// <b>"Client memory", Q2 (a).</b> Reachable managed state is proved by four layers together: the purge registrations
/// and their gates (<c>ActiveSpaceStateTests</c>), AC12's static gate (<c>ClientStaticStateGateTests</c>), the DOM
/// absences here with the departed page held mounted until the switch (planted by skipping each registration in turn -
/// <c>BoardView</c>, <c>ProjectList</c>, <c>LabelSection</c>), and the time-boxed heap-scan spike, held skipped below.
/// </para>
/// <para>
/// <b>The API is <see cref="FakeYelloApi"/></b> at the network edge, in <see cref="ClientSurfaceCollection"/>: no Host,
/// no database, no new engine.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "UX-DR31")]
[Trait("Requirement", "NFR-1")]
[Trait("Requirement", "SM-1")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class SpaceSwitchMemoryTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string ContextBar = "nav.context-bar";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string SwitcherTrigger = "nav.context-bar button[aria-controls]";
    private const string Card = ".task-card";
    private const string CountChip = ".column-count";
    private const string SettingsLink = "Space settings";

    private const string DepartedName = "Quarry Ledger";
    private const string DestinationName = "Orchard Row";
    private const string DepartedProject = "Slate Inventory";
    private const string DestinationProject = "Pear Harvest";

    /// <summary>
    /// Records <c>{path, html}</c> on every DOM mutation in <c>window.__frames</c>, so a case can ask what was on screen at
    /// each path - the destination's first render included - rather than only at the end.
    /// </summary>
    private const string RecordFrames =
        """
        () => {
            window.__frames = [];
            new MutationObserver(() => window.__frames.push({ path: location.pathname, html: document.body.innerHTML }))
                .observe(document.body, { childList: true, subtree: true, characterData: true });
        }
        """;

    private static readonly string[] DepartedStatuses = ["Quarried", "Cut", "Dressed"];
    private static readonly string[] DestinationStatuses = ["Blossom", "Fruit", "Picked"];
    private static readonly string[] DepartedTitles = ["Weigh the north seam", "Log the granite blocks", "Chalk the cutting lines"];
    private static readonly string[] DestinationTitles = ["Net the young trees", "Prop the heavy boughs", "Grade the windfalls"];
    private static readonly string[] DepartedLabels = ["Blasting permit", "Weighbridge"];
    private static readonly string[] DestinationLabels = ["Frost watch", "Cider batch"];

    /// <summary>
    /// The departed Board's column totals and its Project's Task count: numbers the destination never shows.
    /// </summary>
    private static readonly int[] DepartedTotals = [13, 17, 19];
    private static readonly string[] DepartedCounts = ["13", "17", "19", "49"];

    private static readonly Guid Departed = Guid.NewGuid();
    private static readonly Guid Destination = Guid.NewGuid();
    private static readonly Guid DepartedProjectId = Guid.NewGuid();
    private static readonly Guid DestinationProjectId = Guid.NewGuid();

    /// <summary>
    /// What the departed Space held. Its name is not here: the switcher still lists it after a switch, as it should.
    /// </summary>
    private static readonly string[] DepartedContent = [DepartedProject, .. DepartedTitles, .. DepartedLabels, .. DepartedStatuses];

    /// <summary>
    /// What a page reached by leaving the Space altogether (sign-out, Session end, deletion) must not hold - its name too.
    /// </summary>
    private static readonly string[] DepartedMarkers = [DepartedName, .. DepartedContent];

    /// <summary>
    /// AC5 - with the departed Board mounted until the switch, nothing of it - titles, Labels, Status names, the
    /// Project's name, or a column count - is in the DOM while the destination is held or once it has rendered.
    /// Anchored on every one of them being on screen before the switch. Plant: skip <c>BoardView</c>'s purge
    /// registration (Q2 (a)).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Nothing_of_a_departed_Board_survives_a_switch_in_the_DOM_its_counts_included()
    {
        var api = SeededSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(Address($"spaces/{Departed:D}/projects/{DepartedProjectId:D}"));
            await page.Locator(Card).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await AssertPresentAsync(page, [DepartedName, DepartedProject, DepartedTitles[0], DepartedLabels[0], DepartedStatuses[0]]);
            Assert.Equal(["13", "17", "19"], (await page.Locator(CountChip).AllInnerTextsAsync()).Select(text => text.Trim()).Order(StringComparer.Ordinal));

            var arrived = api.HoldAsync(Destination);

            await ChooseInSwitcherAsync(page, DestinationName);
            await arrived.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            await AssertNothingOfTheDepartedSpaceAsync(page);

            api.Release(Destination);
            await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.Locator("main").GetByText(DestinationProject).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await AssertNothingOfTheDepartedSpaceAsync(page);
        }
    }

    /// <summary>
    /// AC5 - from the departed Space's home, its Project's deletion confirm open and stating its 49 Tasks, nothing of it
    /// survives a switch: not the Project's name, and not its count. Plant: skip <c>ProjectList</c>'s purge registration.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Nothing_of_a_departed_Project_list_survives_a_switch_its_Task_count_included()
    {
        var api = SeededSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(Address($"spaces/{Departed:D}"));
            await page.Locator("main").GetByText(DepartedProject).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            // The Project's Task count is shown in its deletion confirm (story 2.5), read afresh when it opens: left open,
            // it is the count the switch must not carry.
            await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete Project", Exact = true }).ClickAsync();
            await page.Locator("main .project-list__warning").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Matches(WholeNumber("49"), await page.Locator("main").InnerTextAsync());

            var arrived = api.HoldAsync(Destination);

            await ChooseInSwitcherAsync(page, DestinationName);
            await arrived.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            await AssertNothingOfTheDepartedSpaceAsync(page);

            api.Release(Destination);
            await page.Locator("main").GetByText(DestinationProject).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await AssertNothingOfTheDepartedSpaceAsync(page);
        }
    }

    /// <summary>
    /// AC5 - from the departed Space's settings (its Labels listed), nothing of it survives a switch. Plant: skip
    /// <c>LabelSection</c>'s purge registration.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Nothing_of_a_departed_Spaces_Labels_survives_a_switch_from_its_settings()
    {
        var api = SeededSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Departed);
            await page.Locator("main").GetByText(DepartedLabels[0]).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var arrived = api.HoldAsync(Destination);

            await ChooseInSwitcherAsync(page, DestinationName);
            await arrived.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            await AssertNothingOfTheDepartedSpaceAsync(page);

            api.Release(Destination);
            await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await AssertNothingOfTheDepartedSpaceAsync(page);
        }
    }

    /// <summary>
    /// AC5, Q2 (a)'s time-boxed spike: after a switch and a forced collection, the WebAssembly runtime's linear memory
    /// holds none of the departed Space's Task titles as UTF-16. Anchor: the title is found in the heap before the switch.
    /// Freed memory is not zeroed, so a false positive is possible; the spike decides whether this can be a reliable 3/3
    /// signal (then it becomes a Nightly case) or is recorded as unreliable and ledgered to Lee with the measurement. A Nightly
    /// case moves to a file of its own with <c>Tier=Nightly</c> and <c>Explicit = true</c>: a file carrying the trait must
    /// mark every test in it Explicit (<c>DeploymentShapeTests.Every_tiered_test_is_Explicit</c>).
    /// <para>
    /// <b>Measured by story 3.4 (2026-10-31), once, un-skipped for the run and restored after it.</b> The accessor works
    /// in Blazor 10.0.11: <c>getDotnetRuntime(0).Module.HEAPU8</c> is reachable, and the anchor held (the title was found
    /// while the Board was on screen). After the switch the title was still found. That is no evidence of a leak: the page
    /// cannot force a .NET collection without a test hook in the client (ruled out by story 1.9 Q4), and memory a collection
    /// frees is not zeroed, so a hit cannot tell garbage from reachable state. The case stays skipped and is not a Nightly
    /// case; the ledger hands the question to Lee.
    /// </para>
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "AC5, Q2 (a) spike measured by story 3.4: unreliable. The heap is readable and the anchor holds, but the departed title is still found after the switch, because no managed collection can be forced from the page without a test hook and freed memory is not zeroed. Ledgered to Lee (deferred-work.md)")]
    public async Task The_departed_Spaces_Task_titles_are_not_in_the_WebAssembly_heap_after_a_switch()
    {
        var api = SeededSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(Address($"spaces/{Departed:D}/projects/{DepartedProjectId:D}"));
            await page.Locator(Card).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.True(await HeapHoldsAsync(page, DepartedTitles[1]), "Anchor: the departed title is not in the heap while its Board is on screen.");

            await ChooseInSwitcherAsync(page, DestinationName);
            await page.Locator("main").GetByText(DestinationProject).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.False(await HeapHoldsAsync(page, DepartedTitles[1]), "The departed Space's Task title is still in the WebAssembly heap after the switch.");
        }
    }

    /// <summary>
    /// AC13 - sign-out (<c>Program.cs</c>'s sign-out exit): no frame rendered at <c>/sign-in</c> holds anything of the Space
    /// left. Anchored on the departed Board being on screen, and on at least one sign-in frame being recorded.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Signing_out_purges_the_departed_Space_before_the_sign_in_page_renders()
    {
        var api = SeededSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(Address($"spaces/{Departed:D}/projects/{DepartedProjectId:D}"));
            await page.Locator(Card).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await AssertPresentAsync(page, [DepartedTitles[0], DepartedProject]);
            await page.EvaluateAsync(RecordFrames);

            await page.Locator(ContextBar).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Sign out" }).ClickAsync();
            await WaitForPathAsync(page, "/sign-in");
            await page.Locator("#password").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await AssertNoFrameAtHoldsTheDepartedSpaceAsync(page, path => path == "/sign-in");
        }
    }

    /// <summary>
    /// AC13 - the deletion's exit (<c>SpaceDeleteSection</c>'s <c>NavigateTo("/")</c>): no frame rendered after the
    /// navigation leaves the deleted Space's routes holds its name or its Labels. Anchored on the Labels on screen in the
    /// deleted Space's settings, and on frames being recorded at the destination. Plant: remove
    /// <c>Navigating(null)</c> from <c>SpaceDeleteSection.Leave</c>. Green when the ATDD run activated it (3.3's explicit
    /// call holds), so it stands as a regression gate; it does not by itself say whether the location-changing hook runs
    /// for <c>NavigateTo</c> - that observation is still AC13's first step.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Assumption", "PRD-12-2")]
    public async Task Deleting_a_Space_purges_it_before_the_destination_renders()
    {
        var api = SeededSpaces();

        api.DeletionSummaries[Departed] = (1, 3, 0);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page, Departed);
            await page.Locator("main").GetByText(DepartedLabels[0]).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.EvaluateAsync(RecordFrames);

            await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete this Space", Exact = true }).ClickAsync();
            await page.GetByLabel("Type the Space name to confirm", new PageGetByLabelOptions { Exact = true }).FillAsync(DepartedName);
            await page.Locator("main .destructive-confirm .button-danger").ClickAsync();

            await WaitForPathAsync(page, $"/spaces/{Destination:D}");
            await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await AssertNoFrameAtHoldsTheDepartedSpaceAsync(page, path => !path.StartsWith($"/spaces/{Departed:D}", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// AC13 - Session end (<c>Program.cs</c>'s Session-end exit): a <c>401</c> to a client that believed it held a Session
    /// purges and routes to sign-in; no frame at the sign-in route holds anything of the Space left. Corrected by story 3.4:
    /// clearing <c>SignedIn</c> alone ended nothing, because the fake still answered the Space's reads; the fake's
    /// <c>SessionEnded</c> answers them <c>401</c>, as the Host does.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_Session_ending_purges_the_departed_Space_before_the_sign_in_page_renders()
    {
        var api = SeededSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(Address($"spaces/{Departed:D}/projects/{DepartedProjectId:D}"));
            await page.Locator(Card).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.EvaluateAsync(RecordFrames);

            api.SignedIn = false;
            api.SessionEnded = true;
            await page.EvaluateAsync("path => Blazor.navigateTo(path)", $"/spaces/{Departed:D}/settings");
            await page.Locator("#password").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await AssertNoFrameAtHoldsTheDepartedSpaceAsync(page, path => path.StartsWith("/sign-in", StringComparison.Ordinal));
        }
    }

    private static FakeYelloApi SeededSpaces()
    {
        var api = new FakeYelloApi { SignedIn = true, ServeProjectTaskCounts = true };

        api.Switcher.Add((Departed, DepartedName));
        api.Switcher.Add((Destination, DestinationName));
        api.Roles[Departed] = "owner";
        api.Roles[Destination] = "member";

        SeedSpace(api, Departed, (DepartedProjectId, DepartedProject), DepartedStatuses, DepartedTitles, DepartedLabels, DepartedTotals);
        SeedSpace(api, Destination, (DestinationProjectId, DestinationProject), DestinationStatuses, DestinationTitles, DestinationLabels, [1, 1, 1]);

        return api;
    }

    /// <summary>
    /// One Space as the fake serves it: one Project whose Board has a column per Status, each with one of the titles and the
    /// stated total (more than it holds, so the count is a number of its own), the Labels applied to the first Task.
    /// </summary>
    private static void SeedSpace(FakeYelloApi api, Guid spaceId, (Guid Id, string Name) project, string[] statuses, string[] titles, string[] labelNames, int[] totals)
    {
        var labels = labelNames.Select((name, index) => new FakeLabel(Guid.NewGuid(), name, LabelColours.Keys[index % LabelColours.Keys.Count])).ToList();
        var columns = new List<FakeColumn>();

        for (var index = 0; index < statuses.Length; index++)
        {
            var statusId = Guid.NewGuid();
            IReadOnlyList<Guid> applied = index == 0 ? [.. labels.Select(label => label.Id)] : [];
            var task = new FakeTask(Guid.NewGuid(), titles[index], statusId, "a00000", DateTimeOffset.UtcNow, LabelIds: applied);

            columns.Add(new FakeColumn(statusId, statuses[index], totals[index], [task]));
        }

        api.Labels[spaceId] = labels;
        api.Boards[(spaceId, project.Id)] = new FakeBoard(new FakeBoardProject(project.Id, project.Name), columns);
        api.Projects[spaceId] = [new FakeProject(project.Id, project.Name, DateTimeOffset.UtcNow)];
    }

    private static async Task AssertNothingOfTheDepartedSpaceAsync(IPage page)
    {
        var body = await page.EvaluateAsync<string>("() => document.body.innerHTML");

        foreach (var marker in DepartedContent)
        {
            Assert.False(body.Contains(marker, StringComparison.Ordinal), $"'{marker}' of the departed Space is still in the DOM.");
        }

        var chips = (await page.Locator(CountChip).AllInnerTextsAsync()).Select(text => text.Trim()).ToList();

        Assert.Empty(chips.Intersect(DepartedCounts, StringComparer.Ordinal));

        var text = await page.Locator("body").InnerTextAsync();

        Assert.All(DepartedCounts, count => Assert.DoesNotMatch(WholeNumber(count), text));
    }

    private static async Task AssertNoFrameAtHoldsTheDepartedSpaceAsync(IPage page, Func<string, bool> atDestination)
    {
        var frames = await page.EvaluateAsync<string[][]>("() => window.__frames.map(frame => [frame.path, frame.html])");
        var there = frames.Select(frame => new Frame(frame[0], frame[1])).Where(frame => atDestination(frame.Path)).ToList();

        Assert.NotEmpty(there);

        foreach (var frame in there)
        {
            foreach (var marker in DepartedMarkers)
            {
                Assert.False(frame.Html.Contains(marker, StringComparison.Ordinal), $"A frame at {frame.Path} rendered '{marker}' of the Space just left.");
            }
        }
    }

    private static async Task<bool> HeapHoldsAsync(IPage page, string text) =>
        await page.EvaluateAsync<bool>(
            """
            async text => {
                const runtime = globalThis.getDotnetRuntime ? globalThis.getDotnetRuntime(0) : null;
                const heap = runtime?.Module?.HEAPU8 ?? globalThis.Module?.HEAPU8;
                if (!heap) throw new Error('No WebAssembly heap is reachable from the page.');
                if (globalThis.gc) globalThis.gc();
                const needle = new Uint8Array(text.length * 2);
                for (let i = 0; i < text.length; i++) { needle[i * 2] = text.charCodeAt(i) & 0xff; needle[i * 2 + 1] = text.charCodeAt(i) >> 8; }
                outer: for (let i = 0; i <= heap.length - needle.length; i++) {
                    for (let j = 0; j < needle.length; j++) if (heap[i + j] !== needle[j]) continue outer;
                    return true;
                }
                return false;
            }
            """,
            text);

    private static async Task AssertPresentAsync(IPage page, IEnumerable<string> names)
    {
        var body = await page.EvaluateAsync<string>("() => document.body.innerHTML");

        foreach (var name in names)
        {
            Assert.True(body.Contains(name, StringComparison.Ordinal), $"Anchor: '{name}' is not on screen to begin with.");
        }
    }

    private static Regex WholeNumber(string number) => new($@"(?<!\d){number}(?!\d)", RegexOptions.None, TimeSpan.FromSeconds(5));

    private static Task WaitForPathAsync(IPage page, string path) =>
        page.WaitForFunctionAsync("path => location.pathname === path", path, new PageWaitForFunctionOptions { Timeout = Timeout, PollingInterval = 50 });

    private static async Task ChooseInSwitcherAsync(IPage page, string name)
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
        await rows.Filter(new LocatorFilterOptions { HasText = name }).ClickAsync();
    }

    private string Address(string path) => new Uri(surface.BaseAddress, path).ToString();

    private async Task OpenSettingsAsync(FakeYelloApi api, IPage page, Guid spaceId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address($"spaces/{spaceId:D}"));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.Locator(ContextBar).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = SettingsLink, Exact = true }).ClickAsync();
    }

    /// <summary>
    /// One recorded frame: the path, and the body's markup at that mutation.
    /// </summary>
    /// <param name="Path">The page's path.</param>
    /// <param name="Html">The body's markup.</param>
    private sealed record Frame(string Path, string Html);
}
