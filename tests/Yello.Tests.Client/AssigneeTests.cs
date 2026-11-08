using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;
using Yello.Contracts;

namespace Yello.Tests.Client;

/// <summary>
/// Assigning a Task in a real browser: the Assignee attribute in Task detail - a single-select picker of "Unassigned" then
/// every candidate by initials and name, never an address, with no default chosen; its own <c>PUT</c> per deliberate choice
/// with an <c>Idempotency-Key</c>; the card's avatar; the Viewer's read-only Assignee and absent control; the
/// <c>invalid-assignee</c> refusal; look-alike names; the List View's Assignee filter, sort and column; and the candidates
/// purged with the Space on a switch.
/// </summary>
/// <remarks>
/// <para>
/// <b>The API is <see cref="FakeYelloApi"/></b> with the candidates read and the assignment answered as the Host will answer
/// them (<c>FakeYelloApi.Assignees.cs</c>), seeded through <see cref="FakeYelloApi.SpaceAssignees"/>; the List answers the
/// <c>assignee</c> sort and the <c>assignee</c> / <c>unassigned</c> filter.
/// </para>
/// <para>
/// <b>The locators are assumptions</b>, to be corrected to the markup the dev phase builds and the correction recorded in the
/// Debug Log: the attribute's control is a button named "Assignee" opening a <c>[role=listbox]</c> of <c>[role=option]</c>
/// rows (the Status picker's pattern); the avatar is <c>.assignee-avatar</c> with the accessible name
/// "Assigned to {label}"; the Viewer's read-only row is a <c>dt</c> "Assignee" with its <c>dd</c>; the List's filter is a
/// fieldset named "Assignee" of checkboxes, its sort option "Assignee", its column header "Assignee". The copy is the story's,
/// asserted as written.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-21")]
[Trait("Requirement", "UX-DR34")]
public sealed class AssigneeTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string SwitcherTrigger = "nav.context-bar button[aria-controls]";
    private const string Card = ".task-card";
    private const string Dialog = "[role='dialog']";
    private const string Avatar = ".assignee-avatar";
    private const string ListRows = "main table tbody tr[data-task-id]";
    private const string Polite = "[role='status'][aria-live='polite']";

    private const string AssigneeLabel = "Assignee";
    private const string UnassignedCopy = "Unassigned";
    private const string NoLongerHereCopy = "That person is no longer in this Space.";


    private static readonly string[] StatusNames = ["Todo", "Doing", "Done"];

    private static readonly Regex SortName = new("^Sort", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();
    private static readonly Guid Beacon = Guid.NewGuid();

    /// <summary>
    /// UX-DR11, UX-DR34: the picker offers "Unassigned" first and then every candidate, each with its initials and its name,
    /// and nothing in it carries an <c>@</c>. The Task is unassigned, so no candidate is marked chosen when it opens: there is
    /// no default selection where the choice is consequential.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR11")]
    public async Task The_picker_offers_Unassigned_then_each_candidate_by_initials_and_name_with_none_chosen()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "Unowned");
        var (sam, priya) = (Guid.NewGuid(), Guid.NewGuid());

        api.SpaceAssignees[Northwind] = [(sam, "Sam Lee"), (priya, "priya raman")];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            var listbox = await OpenPickerAsync(page);
            var options = listbox.Locator("[role='option']");

            Assert.Equal(3, await options.CountAsync());
            Assert.Equal(UnassignedCopy, (await options.Nth(0).TextContentAsync() ?? string.Empty).Trim());
            Assert.Contains("SL", await options.Nth(1).TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
            Assert.Contains("Sam Lee", await options.Nth(1).TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
            Assert.Contains("PR", await options.Nth(2).TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
            Assert.Contains("priya raman", await options.Nth(2).TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
            Assert.Equal(0, await listbox.Locator("[role='option'][aria-selected='true']", new LocatorLocatorOptions { HasText = "Sam Lee" }).CountAsync());
            Assert.Equal(0, await listbox.Locator("[role='option'][aria-selected='true']", new LocatorLocatorOptions { HasText = "priya raman" }).CountAsync());
            Assert.DoesNotContain("@", await listbox.TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// choosing a candidate sends exactly one <c>PUT</c> to the Task's assignee path with <c>{ membershipId }</c> and an
    /// <c>Idempotency-Key</c>, never the Task's <c>PATCH</c>; it is announced politely as "Assigned to Sam Lee."; and the card's
    /// metadata row shows the avatar - the initials, the accessible name "Assigned to Sam Lee", no <c>title</c> tooltip, and no
    /// <c>@</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR8")]
    [Trait("Requirement", "UX-DR38")]
    [Trait("Requirement", "AD-18")]
    public async Task Choosing_a_candidate_sends_one_assignment_announces_it_and_the_card_shows_the_avatar()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "To be owned");
        var sam = Guid.NewGuid();

        api.SpaceAssignees[Northwind] = [(sam, "Sam Lee")];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);
            await ChooseAsync(page, "Sam Lee");

            await page.Locator(Polite, new PageLocatorOptions { HasText = "Assigned to Sam Lee." }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var sent = Assert.Single(api.Assignments(Northwind));

            Assert.Equal(sam, MembershipIdIn(sent));
            Assert.False(string.IsNullOrEmpty(sent.Header(Idempotency.HeaderName)), "The assignment carried no Idempotency-Key.");
            Assert.DoesNotContain(api.Requests, request => request.Method == "PATCH" && request.Path.Contains(tasks[0].Id.ToString("D"), StringComparison.Ordinal));
            Assert.Equal(sam, api.AssigneeOf(Northwind, tasks[0].Id));

            var avatar = CardOf(page, tasks[0].Id).Locator(Avatar);

            await avatar.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal("SL", (await avatar.TextContentAsync() ?? string.Empty).Trim());
            Assert.Equal("Assigned to Sam Lee", await avatar.EvaluateAsync<string>("e => e.getAttribute('aria-label') ?? ''"));
            Assert.Null(await avatar.GetAttributeAsync("title"));
            Assert.DoesNotContain("@", await CardOf(page, tasks[0].Id).TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The current Assignee is marked chosen when the picker opens, and choosing "Unassigned" sends <c>membershipId: null</c>,
    /// is announced "Unassigned.", and takes the avatar off the card.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR38")]
    public async Task Choosing_Unassigned_clears_the_Assignee_and_the_avatar()
    {
        var api = TwoSpaces();
        var sam = Guid.NewGuid();
        var tasks = Seed(api, Northwind, Atlas, "Owned");

        api.SpaceAssignees[Northwind] = [(sam, "Sam Lee")];
        Assign(api, tasks[0].Id, sam);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await CardOf(page, tasks[0].Id).Locator(Avatar).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await OpenPanelAsync(page, tasks[0].Id);

            var listbox = await OpenPickerAsync(page);

            Assert.Equal(1, await listbox.Locator("[role='option'][aria-selected='true']", new LocatorLocatorOptions { HasText = "Sam Lee" }).CountAsync());

            await listbox.Locator("[role='option']", new LocatorLocatorOptions { HasText = UnassignedCopy }).ClickAsync();
            await page.Locator(Polite, new PageLocatorOptions { HasText = "Unassigned." }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Null(MembershipIdIn(Assert.Single(api.Assignments(Northwind))));
            Assert.Null(api.AssigneeOf(Northwind, tasks[0].Id));
            await CardOf(page, tasks[0].Id).Locator(Avatar).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });
        }
    }

    /// <summary>
    /// UX-DR27, a Viewer has no assign control - absent, not disabled - and reads the Assignee among the Task's
    /// attributes as its label, or "Unassigned". Its card shows the avatar like anyone's.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    public async Task A_Viewer_reads_the_Assignee_and_has_no_assign_control()
    {
        var api = TwoSpaces();
        var sam = Guid.NewGuid();
        var tasks = Seed(api, Harbour, Beacon, "Watched", "Nobody's");

        api.SpaceAssignees[Harbour] = [(sam, "Sam Lee")];
        Assign(api, tasks[0].Id, sam);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Harbour, Beacon);
            await CardOf(page, tasks[0].Id).Locator(Avatar).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await OpenPanelAsync(page, tasks[0].Id);

            var dialog = page.Locator(Dialog);

            Assert.Equal(0, await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = AssigneeLabel }).CountAsync());
            Assert.Equal("Sam Lee", await ReadOnlyAssigneeAsync(dialog));

            await page.Keyboard.PressAsync("Escape");
            await OpenPanelAsync(page, tasks[1].Id);

            Assert.Equal(UnassignedCopy, await ReadOnlyAssigneeAsync(page.Locator(Dialog)));
            Assert.Empty(api.Assignments(Harbour));
        }
    }

    /// <summary>
    /// an assignment refused <c>400 invalid-assignee</c> (the person left meanwhile) reverts the attribute and the card,
    /// re-reads the candidates, and says "That person is no longer in this Space."
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-21")]
    public async Task An_invalid_assignee_refusal_reverts_rereads_the_candidates_and_says_so()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "Briefly owned");
        var gone = Guid.NewGuid();

        api.SpaceAssignees[Northwind] = [(gone, "Gone Soon")];
        api.NextAssignmentRefusal = (400, ProblemTypes.InvalidAssignee, null);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            var readsBefore = api.CandidateReads().Count;

            await ChooseAsync(page, "Gone Soon");
            await page.GetByText(NoLongerHereCopy).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Null(api.AssigneeOf(Northwind, tasks[0].Id));
            Assert.Equal(0, await CardOf(page, tasks[0].Id).Locator(Avatar).CountAsync());
            Assert.True(api.CandidateReads().Count > readsBefore, "The refusal did not re-read the candidates.");
        }
    }

    /// <summary>
    /// UX-DR34 with Lee's folding rule: two candidates whose names look the same ("Ann" and "Ann " with a trailing space) are
    /// each offered with a Membership-scoped discriminator, never with an address.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-23")]
    public async Task Look_alike_candidates_are_each_offered_with_a_discriminator()
    {
        var api = TwoSpaces();
        var tasks = Seed(api, Northwind, Atlas, "Which Ann");

        api.SpaceAssignees[Northwind] = [(Guid.NewGuid(), "Ann"), (Guid.NewGuid(), "Ann ")];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await OpenPanelAsync(page, tasks[0].Id);

            var options = (await OpenPickerAsync(page)).Locator("[role='option']");
            var labels = new List<string>();

            for (var index = 1; index < await options.CountAsync(); index++)
            {
                labels.Add(await options.Nth(index).TextContentAsync() ?? string.Empty);
            }

            Assert.Equal(2, labels.Count);
            Assert.All(labels, label => Assert.Matches("Ann\\s* · [0-9A-F]{4}", label));
            Assert.NotEqual(labels[0], labels[1]);
            Assert.All(labels, label => Assert.DoesNotContain("@", label, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// FR-30, UX-DR19: the List View offers the Assignee dimension - a fieldset "Assignee" of checkboxes, "Unassigned" first
    /// and then each candidate's label; an "Assignee" sort option; and an "Assignee" column showing the label or
    /// "Unassigned" - and no address anywhere on the page.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-30")]
    [Trait("Requirement", "UX-DR19")]
    public async Task The_List_offers_an_Assignee_filter_sort_and_column_with_no_address()
    {
        var api = TwoSpaces();
        var sam = Guid.NewGuid();
        var tasks = Seed(api, Northwind, Atlas, "Sam's", "Nobody's");

        api.SpaceAssignees[Northwind] = [(sam, "Sam Lee"), (Guid.NewGuid(), "Priya Raman")];
        Assign(api, tasks[0].Id, sam);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenListAsync(api, page, Northwind, Atlas);

            var fieldset = page.GetByRole(AriaRole.Group, new PageGetByRoleOptions { Name = AssigneeLabel, Exact = true });
            var boxes = fieldset.GetByRole(AriaRole.Checkbox);

            Assert.Equal(3, await boxes.CountAsync());
            Assert.Equal(UnassignedCopy, await boxes.Nth(0).EvaluateAsync<string>("e => (e.labels && e.labels[0] ? e.labels[0].textContent : '').trim()"));
            Assert.Equal(1, await page.GetByRole(AriaRole.Combobox, new PageGetByRoleOptions { NameRegex = SortName }).Locator("option", new LocatorLocatorOptions { HasText = AssigneeLabel }).CountAsync());
            Assert.Equal(1, await page.GetByRole(AriaRole.Columnheader, new PageGetByRoleOptions { Name = AssigneeLabel, Exact = true }).CountAsync());
            Assert.Contains("Sam Lee", await RowOf(page, tasks[0].Id).TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
            Assert.Contains(UnassignedCopy, await RowOf(page, tasks[1].Id).TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain("@", await page.Locator("main").TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// FR-30: ticking one candidate in the Assignee filter sends its Membership id as <c>assignee</c> and shows only its
    /// Tasks; ticking "Unassigned" too sends <c>unassigned=true</c> and adds the unassigned ones. Neither reaches the address
    /// bar or <c>document.title</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-30")]
    public async Task Filtering_the_List_by_an_Assignee_sends_its_id_and_shows_only_its_Tasks()
    {
        var api = TwoSpaces();
        var sam = Guid.NewGuid();
        var tasks = Seed(api, Northwind, Atlas, "Sam's", "Nobody's", "Also nobody's");

        api.SpaceAssignees[Northwind] = [(sam, "Sam Lee")];
        Assign(api, tasks[0].Id, sam);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenListAsync(api, page, Northwind, Atlas);

            var fieldset = page.GetByRole(AriaRole.Group, new PageGetByRoleOptions { Name = AssigneeLabel, Exact = true });

            await fieldset.GetByRole(AriaRole.Checkbox, new LocatorGetByRoleOptions { Name = "Sam Lee", Exact = true }).CheckAsync(new LocatorCheckOptions { Timeout = Timeout });
            await WaitForRowCountAsync(page, 1);

            Assert.Contains($"{Yello.Contracts.Lists.ListRoutes.AssigneeParameter}={sam:D}", api.ListRequests[^1].Query, StringComparison.Ordinal);

            await fieldset.GetByRole(AriaRole.Checkbox, new LocatorGetByRoleOptions { Name = UnassignedCopy, Exact = true }).CheckAsync(new LocatorCheckOptions { Timeout = Timeout });
            await WaitForRowCountAsync(page, 3);

            Assert.Contains($"{Yello.Contracts.Lists.ListRoutes.UnassignedParameter}=true", api.ListRequests[^1].Query, StringComparison.Ordinal);
            Assert.DoesNotContain("Sam", await page.EvaluateAsync<string>("() => location.href + ' ' + document.title"), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// UX-DR31, the candidates are Space-scoped state. After a switch from a Space whose Board shows an Assignee, no
    /// label or initials of the departed Space's candidates is anywhere in the DOM, and every candidates read after the switch
    /// names the new Space only.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR31")]
    [Trait("Requirement", "NFR-1")]
    public async Task Switching_Space_purges_the_departed_Spaces_candidates()
    {
        var api = TwoSpaces();
        var zed = Guid.NewGuid();
        var northwindTasks = Seed(api, Northwind, Atlas, "Departed");

        _ = Seed(api, Harbour, Beacon, "Arrived");
        api.SpaceAssignees[Northwind] = [(zed, "Zed Quorra")];
        api.SpaceAssignees[Harbour] = [(Guid.NewGuid(), "Harbour Hand")];
        Assign(api, northwindTasks[0].Id, zed);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenBoardAsync(api, page, Northwind, Atlas);
            await CardOf(page, northwindTasks[0].Id).Locator(Avatar).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var readsBefore = api.CandidateReads().Count;

            await ChooseInSwitcherAsync(page, "Harbour Ops");
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var html = await page.ContentAsync();

            Assert.DoesNotContain("Zed Quorra", html, StringComparison.Ordinal);
            Assert.DoesNotContain(">ZQ<", html, StringComparison.Ordinal);
            Assert.All(api.CandidateReads().Skip(readsBefore), read => Assert.Equal(Harbour, read));
        }
    }

    /// <summary>
    /// A signed-in Account who is Owner of Northwind Redesign and Viewer of Harbour Ops, listed in that order.
    /// </summary>
    private static FakeYelloApi TwoSpaces()
    {
        var api = new FakeYelloApi { SignedIn = true };

        api.Switcher.Add((Northwind, "Northwind Redesign"));
        api.Switcher.Add((Harbour, "Harbour Ops"));
        api.Roles[Northwind] = "owner";
        api.Roles[Harbour] = "viewer";

        return api;
    }

    /// <summary>
    /// A Project on Space home and its Board: three columns in the default order, the titles in the first column.
    /// </summary>
    private static List<FakeTask> Seed(FakeYelloApi api, Guid spaceId, Guid projectId, params string[] titles)
    {
        var columns = StatusNames.Select(status => new FakeColumn(Guid.NewGuid(), status, 0, [])).ToList();

        foreach (var title in titles)
        {
            columns[0].Tasks.Add(new FakeTask(Guid.NewGuid(), title, columns[0].StatusId, $"a{columns[0].Tasks.Count:D5}", DateTimeOffset.UtcNow, LabelIds: []));
            columns[0].Total++;
        }

        var name = spaceId == Northwind ? "Atlas" : "Beacon";

        api.Boards[(spaceId, projectId)] = new FakeBoard(new FakeBoardProject(projectId, name), columns);
        api.Projects[spaceId] = [new FakeProject(projectId, name, DateTimeOffset.UtcNow)];

        return [.. columns[0].Tasks];
    }

    /// <summary>
    /// Sets a seeded Task's Assignee in the fake, as an earlier assignment would have left it.
    /// </summary>
    private static void Assign(FakeYelloApi api, Guid taskId, Guid membershipId)
    {
        foreach (var column in api.Boards.Values.SelectMany(board => board.Columns))
        {
            var index = column.Tasks.FindIndex(candidate => candidate.Id == taskId);

            if (index >= 0)
            {
                column.Tasks[index] = column.Tasks[index] with { AssigneeMembershipId = membershipId };
            }
        }
    }

    private static Guid? MembershipIdIn(FakeRequest assignment)
    {
        using var document = JsonDocument.Parse(assignment.Body ?? "{}");

        var value = document.RootElement.GetProperty("membershipId");

        return value.ValueKind == JsonValueKind.Null ? null : value.GetGuid();
    }

    private static ILocator CardOf(IPage page, Guid taskId) =>
        page.Locator(Card, new PageLocatorOptions { Has = page.Locator($"button[data-task-id='{taskId:D}']") });

    private static ILocator RowOf(IPage page, Guid taskId) =>
        page.Locator($"main table tbody tr[data-task-id='{taskId:D}']");

    private static async Task OpenPanelAsync(IPage page, Guid taskId)
    {
        await page.Locator($"{Card} button[data-task-id='{taskId:D}']").ClickAsync();
        await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    private static async Task<ILocator> OpenPickerAsync(IPage page)
    {
        var dialog = page.Locator(Dialog);

        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = AssigneeLabel }).ClickAsync();

        var listbox = dialog.Locator("[role='listbox']");

        await listbox.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

        return listbox;
    }

    private static async Task ChooseAsync(IPage page, string label)
    {
        var listbox = await OpenPickerAsync(page);

        await listbox.Locator("[role='option']", new LocatorLocatorOptions { HasText = label }).ClickAsync();
    }

    /// <summary>
    /// The read-only Assignee a Viewer sees: the <c>dd</c> after the <c>dt</c> "Assignee", trimmed.
    /// </summary>
    private static Task<string> ReadOnlyAssigneeAsync(ILocator dialog) =>
        dialog.EvaluateAsync<string>(
            "(d, label) => { const dt = [...d.querySelectorAll('dt')].find(t => t.textContent.trim() === label); return dt && dt.nextElementSibling ? dt.nextElementSibling.textContent.trim() : ''; }",
            AssigneeLabel);

    private static Task WaitForRowCountAsync(IPage page, int count) =>
        page.WaitForFunctionAsync(
            "([rows, count]) => document.querySelectorAll(rows).length === count",
            new object[] { ListRows, count },
            new PageWaitForFunctionOptions { Timeout = Timeout, PollingInterval = 50 });

    private static async Task ChooseInSwitcherAsync(IPage page, string name)
    {
        var trigger = page.Locator(SwitcherTrigger);
        var list = await trigger.GetAttributeAsync("aria-controls");

        await trigger.ClickAsync();

        var row = page.Locator($"[id='{list}'] a", new PageLocatorOptions { HasText = name });
        await row.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await row.ClickAsync();
    }

    private string Address(string path) => new Uri(surface.BaseAddress, path.TrimStart('/')).ToString();

    private async Task OpenBoardAsync(FakeYelloApi api, IPage page, Guid spaceId, Guid projectId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address($"/spaces/{spaceId:D}/projects/{projectId:D}"));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.Locator(Card).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    private async Task OpenListAsync(FakeYelloApi api, IPage page, Guid spaceId, Guid projectId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address($"/spaces/{spaceId:D}/projects/{projectId:D}/list"));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.Locator(ListRows).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }
}
