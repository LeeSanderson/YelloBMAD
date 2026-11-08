using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// A Viewer's Space, swept whole in one tab: Space home, the Board with a Task panel open, the List View, the Space settings
/// address typed directly, and Account settings. On every surface no create, edit, move, delete, invite, assign,
/// Label-manage, settings or Membership affordance is present, and nothing is disabled at rest; the two writes a Viewer keeps
/// - "Leave this Space" and issuing itself a Token - are there. The same sweep as the Owner finds every affordance class at
/// least once, so the list cannot go stale unnoticed.
/// </summary>
/// <remarks>
/// <para>
/// <b>One affordance list</b> (<see cref="Affordances"/>), the union of what the per-surface Viewer cases already look for. A
/// new Role-gated affordance joins it, and the Owner anchor then proves it is found.
/// </para>
/// <para>
/// <b>The API is <see cref="FakeYelloApi"/></b>; the Role is the fake's <c>Roles[space]</c>. A surface is read once it has
/// settled: its own anchor element has rendered and the network has gone idle, so an absence is never read off a page still
/// loading. Each surface is reached by a typed address, as a person following a link from elsewhere would.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-16")]
[Trait("Requirement", "UX-DR27")]
public sealed class ViewerSpaceTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string ContextBar = "nav.context-bar";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string Card = ".task-card";
    private const string Dialog = "[role='dialog']";
    private const string ListRows = "main table tbody tr[data-task-id]";
    private const string Narration = ".role-narration";
    private const string DisabledAnywhere = "[disabled], [aria-disabled='true']";
    private const string NorthwindName = "Northwind Redesign";

    private static readonly string[] StatusNames = ["Todo", "Doing", "Done"];

    private static readonly Regex RemoveName = new("^Remove ", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();

    /// <summary>
    /// Every Role-gated affordance class, by the locator the per-surface cases use for it.
    /// </summary>
    private static readonly (string Name, Func<IPage, ILocator> Find)[] Affordances =
    [
        ("Create a Project", page => page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Create a Project" })),
        ("Rename", page => page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Rename", Exact = true })),
        ("Delete Project", page => page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete Project", Exact = true })),
        ("Add a Task", page => page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Add a Task", Exact = true })),
        ("card actions", page => page.Locator(".task-card__actions")),
        ("drag handle", page => page.Locator(".task-card--movable")),
        ("Delete Task", page => page.Locator(Dialog).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete Task", Exact = true })),
        ("Move", page => page.Locator(Dialog).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Move", Exact = true })),
        ("Labels picker", page => page.Locator(Dialog).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Labels", Exact = true })),
        ("Assignee picker", page => page.Locator(Dialog).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Assignee" })),
        ("panel field", page => page.Locator(Dialog).Locator("input, select, textarea, [role='listbox'], [aria-haspopup]")),
        ("Space settings link", page => page.Locator(ContextBar).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Space settings", Exact = true })),
        ("Label name field", page => page.Locator("main").GetByLabel("Label name")),
        ("invite field", page => page.Locator("main input[type='email']")),
        ("Members section", page => page.Locator("main .membership-section")),
        ("Change Role", page => page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Change Role", Exact = true })),
        ("Remove", page => page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { NameRegex = RemoveName })),
        ("Delete this Space", page => page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete this Space", Exact = true })),
    ];

    /// <summary>
    /// A Viewer who joined at Viewer meets no affordance from the list on any surface, and nothing disabled at rest anywhere
    /// - in <c>main</c>, the context bar or the panel. "Leave this Space" is on Space home and "Generate" (issue a Token) is
    /// in Account settings: the two writes the matrix leaves a Viewer.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_Viewer_meets_no_affordance_and_nothing_disabled_on_any_surface()
    {
        var api = Spaces("viewer");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            var sweep = await SweepAsync(api, page);

            Assert.True(sweep.Found.Count == 0, "A Viewer met: " + string.Join("; ", sweep.Found));
            Assert.True(sweep.Disabled.Count == 0, "Disabled at rest: " + string.Join("; ", sweep.Disabled));
            Assert.True(sweep.LeaveOffered, "Space home offers a Viewer no \"Leave this Space\".");
            Assert.True(sweep.TokenOffered, "Account settings offers a Viewer no way to issue a Token.");
        }
    }

    /// <summary>
    /// The anchor: the same sweep as the Owner finds every affordance class at least once, so an affordance the list names
    /// by a stale locator fails here rather than passing the Viewer's absence by matching nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_same_sweep_as_the_Owner_finds_every_affordance_class()
    {
        var api = Spaces("owner");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            var sweep = await SweepAsync(api, page);
            var missing = Affordances.Select(affordance => affordance.Name).Where(name => !sweep.Found.Exists(found => found.StartsWith(name + " on ", StringComparison.Ordinal))).ToList();

            Assert.True(missing.Count == 0, "The Owner's sweep never met: " + string.Join(", ", missing));
            Assert.True(sweep.Disabled.Count == 0, "Disabled at rest: " + string.Join("; ", sweep.Disabled));
        }
    }

    /// <summary>
    /// A Member or a Viewer typing the Space settings address is taken to Space home and told, once, why: "Space settings
    /// is for Owners and Admins." No settings section, refusal paragraph or affordance renders on the way, and the sentence
    /// names no Space.
    /// </summary>
    /// <param name="role">The Role in Northwind.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("member")]
    [InlineData("viewer")]
    [Trait("Requirement", "UX-DR29")]
    [Trait("Requirement", "UX-DR39")]
    public async Task A_Member_or_a_Viewer_typing_the_Space_settings_address_is_taken_to_Space_home_and_told(string role)
    {
        var api = Spaces(role);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(Address($"spaces/{Northwind:D}/settings"));
            await page.WaitForFunctionAsync("path => location.pathname === path", $"/spaces/{Northwind:D}", new PageWaitForFunctionOptions { Timeout = Timeout, PollingInterval = 50 });

            var narration = page.Locator(Narration, new PageLocatorOptions { HasText = "Space settings is for Owners and Admins." });

            await narration.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.DoesNotContain(NorthwindName, await narration.TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
            Assert.Equal(0, await page.Locator(".space-settings__refusal").CountAsync());
            Assert.Equal(0, await page.Locator("main .membership-section, main .invitation-section").CountAsync());
        }
    }

    /// <summary>
    /// Visits every surface in turn and records, per surface, each affordance met and each element disabled at rest; and
    /// whether the two Viewer writes were offered.
    /// </summary>
    private async Task<Sweep> SweepAsync(FakeYelloApi api, IPage page)
    {
        var sweep = new Sweep();

        await api.InstallAsync(page);

        await VisitAsync(page, $"spaces/{Northwind:D}", page.Locator("main").GetByText("Atlas").First);
        await RecordAsync(page, "Space home", sweep);
        sweep.LeaveOffered = await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Leave this Space", Exact = true }).CountAsync() == 1;

        await VisitAsync(page, $"spaces/{Northwind:D}/projects/{Atlas:D}", page.Locator(Card).First);
        await RecordAsync(page, "the Board", sweep);

        await page.Locator($"{Card} button[data-task-id]").First.ClickAsync(new LocatorClickOptions { Timeout = Timeout });
        await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await RecordAsync(page, "the Task panel", sweep);

        await VisitAsync(page, $"spaces/{Northwind:D}/projects/{Atlas:D}/list", page.Locator(ListRows).First);
        await RecordAsync(page, "the List View", sweep);

        await VisitAsync(page, $"spaces/{Northwind:D}/settings", page.Locator("main h1").First);
        await RecordAsync(page, "the typed Space settings address", sweep);

        await VisitAsync(page, "account", page.Locator("main").GetByText("API Tokens").First);
        await RecordAsync(page, "Account settings", sweep);
        sweep.TokenOffered = await page.Locator("main").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Generate", Exact = true }).CountAsync() == 1;

        return sweep;
    }

    private async Task VisitAsync(IPage page, string path, ILocator settled)
    {
        await page.GotoAsync(Address(path));
        await page.Locator(RoleChip).Or(page.Locator(ContextBar)).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await settled.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private static async Task RecordAsync(IPage page, string where, Sweep sweep)
    {
        foreach (var (name, find) in Affordances)
        {
            if (await find(page).CountAsync() > 0)
            {
                sweep.Found.Add($"{name} on {where}");
            }
        }

        foreach (var disabled in await page.Locator(DisabledAnywhere).AllAsync())
        {
            sweep.Disabled.Add($"{where}: {await disabled.EvaluateAsync<string>("e => e.outerHTML.slice(0, 120)")}");
        }
    }

    /// <summary>
    /// Northwind, where the Account holds <paramref name="role"/>, with one Project and a Board of three Tasks, three other
    /// Members for the settings list, and Harbour as a second Space.
    /// </summary>
    private static FakeYelloApi Spaces(string role)
    {
        var api = new FakeYelloApi { SignedIn = true };

        api.Switcher.Add((Northwind, NorthwindName));
        api.Switcher.Add((Harbour, "Harbour Ops"));
        api.Roles[Northwind] = role;
        api.Roles[Harbour] = "member";

        var columns = StatusNames.Select(status => new FakeColumn(Guid.NewGuid(), status, 0, [])).ToList();

        foreach (var title in new[] { "First", "Second", "Third" })
        {
            columns[0].Tasks.Add(new FakeTask(Guid.NewGuid(), title, columns[0].StatusId, $"a{columns[0].Tasks.Count:D5}", DateTimeOffset.UtcNow, LabelIds: []));
            columns[0].Total++;
        }

        api.Boards[(Northwind, Atlas)] = new FakeBoard(new FakeBoardProject(Atlas, "Atlas"), columns);
        api.Projects[Northwind] = [new FakeProject(Atlas, "Atlas", DateTimeOffset.UtcNow)];
        api.SpaceAssignees[Northwind] = [(Guid.NewGuid(), "Mo Member")];
        api.SpaceMemberships[Northwind] =
        [
            new ListedMembership(Guid.NewGuid(), "Olu Owner", $"owner-{Guid.NewGuid():N}@example.test", "owner", IsYou: role == "owner"),
            new ListedMembership(Guid.NewGuid(), "Mo Member", $"member-{Guid.NewGuid():N}@example.test", "member", IsYou: false),
            new ListedMembership(Guid.NewGuid(), "Vi Viewer", $"viewer-{Guid.NewGuid():N}@example.test", "viewer", IsYou: role == "viewer"),
        ];

        return api;
    }

    private string Address(string path) => new Uri(surface.BaseAddress, path).ToString();

    private sealed class Sweep
    {
        public List<string> Found { get; } = [];

        public List<string> Disabled { get; } = [];

        public bool LeaveOffered { get; set; }

        public bool TokenOffered { get; set; }
    }
}
