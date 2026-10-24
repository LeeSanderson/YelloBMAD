using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using Xunit;
using Yello.Client.Localisation;
using Yello.Domain.Memberships;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 1.7's structural gates: the switcher returns Space identity only, AD-24's two
/// Account-scoped surfaces, the Role chip and its copy, and the browser-owned surfaces. AC2, AC3,
/// AC5, AC6, AC8, AC9; UX-DR28, UX-DR32, UX-DR33.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the ATDD run on 2026-10-24, before any of story 1.7
/// existed.</b> The story's new types are reached <b>by name</b>, so this class compiles today; on
/// activation replace each lookup with the typed reference it stands for, so a rename becomes a
/// compile error. The names are the story's own where it gives one (<c>SpaceIdentity</c>,
/// <c>AccountScopedSurface</c>, <c>RoleChip.razor</c>, <c>RoleOwner</c>,
/// <c>ContextBarAccessibleName</c>) and this run's where it does not
/// (<c>SpaceIdentityResponse</c>, the four <c>RolePermits*</c> keys). A different name chosen in
/// dev-story is corrected here and recorded in the Debug Log.
/// </para>
/// <para>
/// <b>Two gates are green today and are left active</b> as standing gates, each with a permanent
/// detector check beside it so the absence assertion is not vacuous:
/// <see cref="No_client_code_sets_the_document_title"/> and
/// <see cref="No_client_code_remembers_a_last_Space"/>. Both were also planted in production source
/// by the ATDD run; the results are in its checklist.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-9")]
[Trait("Requirement", "AR-29")]
[Trait("Requirement", "AD-24")]
public sealed partial class SpaceSwitcherGateTests
{
    private const string SwitcherResponseName = "Yello.Contracts.Spaces.SpaceIdentityResponse";
    private const string SpaceIdentityName = "Yello.Application.Spaces.SpaceIdentity";
    private const string AccountScopedSurfaceName = "Yello.Host.Spaces.AccountScopedSurface";

    private static readonly ResourceManager Copy = new(ClientCopy.ResourceName, typeof(ClientCopy).Assembly);

    private static readonly DirectoryInfo ClientRoot = new(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client"));

    private static readonly FileInfo HostPage = new(Path.Combine(ClientRoot.FullName, "wwwroot", "index.html"));

    /// <summary>
    /// AC2 / I-11 (static half) - the switcher's wire DTO and the application's Space identity carry
    /// exactly an id and a name. Nothing else may ever be added: this surface returns Space identity
    /// only (AD-24).
    /// </summary>
    [Fact(Skip = "red: AC2 - SpaceIdentityResponse and SpaceIdentity do not exist yet (story 1.7 Tasks 2 and 3)")]
    [Trait("Requirement", "UX-DR9")]
    public void The_switcher_response_and_the_Space_identity_carry_exactly_an_id_and_a_name()
    {
        foreach (var (assembly, name) in new[]
        {
            (ProductionAssemblies.Contracts, SwitcherResponseName),
            (ProductionAssemblies.Application, SpaceIdentityName),
        })
        {
            var type = assembly.GetType(name, throwOnError: false);

            Assert.True(type is not null, $"{name} is not in {assembly.GetName().Name} - story 1.7 writes it.");

            var members = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => (property.Name, property.PropertyType))
                .OrderBy(member => member.Name, StringComparer.Ordinal)
                .ToList();

            Assert.Equal([("Id", typeof(Guid)), ("Name", typeof(string))], members);
        }
    }

    /// <summary>
    /// AC3 / AD-24 - there are exactly two Account-scoped surfaces, the switcher and Account
    /// settings. A third is an amendment to AD-24, not a code change.
    /// </summary>
    [Fact(Skip = "red: AC3 - AccountScopedSurface does not exist yet (story 1.7 Task 3)")]
    [Trait("Requirement", "AR-3")]
    public void The_Account_scoped_surfaces_are_exactly_the_two_AD_24_names()
    {
        var surface = ProductionAssemblies.Host.GetType(AccountScopedSurfaceName, throwOnError: false);

        Assert.True(surface is not null, $"{AccountScopedSurfaceName} is not in Yello.Host - story 1.7 Task 3 writes it.");
        Assert.True(surface.IsEnum, $"{AccountScopedSurfaceName} must be a closed enum.");

        Assert.Equal(["AccountSettings", "SpaceSwitcher"], Enum.GetNames(surface).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// AC8 / UX-DR33 - <c>document.title</c> is a fixed <c>Yello</c>: the host page's title is exactly
    /// that, and nothing in the client sets it - no <c>PageTitle</c>, no <c>HeadContent</c>, no
    /// <c>document.title</c> interop.
    /// </summary>
    /// <remarks>
    /// <b>Green today, a standing gate.</b> The client has never set a title, so this is an absence
    /// assertion over an empty set; <see cref="The_document_title_scan_detects_a_planted_title"/> keeps
    /// its detector honest, and the ATDD run planted a <c>PageTitle</c> naming the Space in
    /// <c>NotAvailable.razor</c>.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "UX-DR33")]
    public void No_client_code_sets_the_document_title()
    {
        var titles = TitleElement.Matches(File.ReadAllText(HostPage.FullName));

        Assert.Equal(["Yello"], titles.Select(title => title.Groups["text"].Value));

        var sources = ClientSources().ToList();

        // Vacuity guard: the scan reads the components and the host page.
        Assert.Contains(sources, file => file.Name == "MainLayout.razor");
        Assert.Contains(sources, file => file.Name == "index.html");

        var offenders = sources
            .Where(file => SetsTheTitle(File.ReadAllText(file.FullName)))
            .Select(file => Path.GetRelativePath(RepositoryLayout.Root.FullName, file.FullName))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "The document title is browser-owned and fixed at 'Yello' (UX-DR33): a Space, Project or "
            + "Task name there reaches history, bookmarks and the tab strip of a shared screen. Set by: "
            + string.Join(", ", offenders));
    }

    /// <summary>
    /// The detector behind <see cref="No_client_code_sets_the_document_title"/>, fired on planted
    /// signals so the absence above cannot pass by reading nothing.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR33")]
    public void The_document_title_scan_detects_a_planted_title()
    {
        Assert.True(SetsTheTitle("<PageTitle>@Space.Name</PageTitle>"));
        Assert.True(SetsTheTitle("<HeadContent><title>x</title></HeadContent>"));
        Assert.True(SetsTheTitle("await js.InvokeVoidAsync(\"eval\", \"document.title = name\");"));
        Assert.False(SetsTheTitle("<h1 class=\"type-page-title\">Projects</h1>"));
    }

    /// <summary>
    /// AC8 / Q10 - scroll restoration is disabled, once, by an inline statement in the host page that
    /// runs before the WebAssembly runtime loads - so it holds on every Space-scoped route.
    /// </summary>
    [Fact(Skip = "red: AC8 - index.html does not set history.scrollRestoration yet (story 1.7 Task 7, open question 10)")]
    [Trait("Requirement", "UX-DR33")]
    public void The_host_page_disables_scroll_restoration_before_the_runtime_loads()
    {
        var page = File.ReadAllText(HostPage.FullName);

        var manual = ManualScrollRestoration.Match(page);
        var runtime = page.IndexOf("_framework/blazor.webassembly", StringComparison.Ordinal);

        Assert.True(manual.Success, "index.html does not set history.scrollRestoration to 'manual'.");
        Assert.True(runtime > manual.Index, "history.scrollRestoration is set after the runtime script, not before it.");
    }

    /// <summary>
    /// AC5 / X-8 (static half) - the Role chip is display-only: a <c>span</c>, with no handler, no
    /// link, no button and no tab stop.
    /// </summary>
    [Fact(Skip = "red: AC5 - RoleChip.razor does not exist yet (story 1.7 Task 7)")]
    [Trait("Requirement", "UX-DR28")]
    public void The_Role_chip_is_never_interactive()
    {
        var chip = new FileInfo(Path.Combine(ClientRoot.FullName, "Components", "RoleChip.razor"));

        Assert.True(chip.Exists, $"{chip.FullName} does not exist - story 1.7 Task 7 writes it.");

        var markup = RazorComment.Replace(File.ReadAllText(chip.FullName), string.Empty);

        Assert.Contains("<span", markup, StringComparison.Ordinal);

        string[] interactive = ["@onclick", "@onkeydown", "href", "tabindex", "<button", "<a ", "<a>", "role=\"button\"", "role=\"link\""];

        var found = interactive.Where(token => markup.Contains(token, StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.True(found.Count == 0, $"RoleChip.razor is interactive ({string.Join(", ", found)}); the chip is display-only and never a control.");
    }

    /// <summary>
    /// AC5 / AC6 - every Role has its name in the client copy, verbatim and in sentence case: the
    /// uppercase is <c>text-transform</c>'s, so a screen reader does not spell the Role out letter by
    /// letter and the Turkish, Azeri and Greek exclusions in <c>base.css</c> can withhold it.
    /// </summary>
    [Fact(Skip = "red: AC5/AC6 - the Role copy (RoleOwner, RoleAdmin, RoleMember, RoleViewer) does not exist yet (story 1.7 Task 7)")]
    [Trait("Requirement", "UX-DR28")]
    [Trait("Requirement", "UX-DR37")]
    public void Every_Role_has_its_name_in_sentence_case_in_the_client_copy()
    {
        Assert.All(Enum.GetValues<Role>(), role =>
        {
            var value = Copy.GetString("Role" + role, CultureInfo.InvariantCulture);

            Assert.Equal(role.ToString(), value);
        });
    }

    /// <summary>
    /// AC6 / Q3 - the accessible name states the Role and what it permits, and every permit phrase is
    /// true of the capability matrix - so a matrix change that makes a phrase false fails the build.
    /// </summary>
    /// <remarks>
    /// Admin's and Viewer's phrases are the spec's, verbatim (<c>epics.md:826</c>); Owner's and
    /// Member's are derived (Q3). Each clause below is the matrix fact its phrase claims. Viewer's
    /// "read only" admits <see cref="Capability.IssueApiTokenForOneself"/>, which is the Account's own
    /// credential rather than anything in the Space.
    /// </remarks>
    [Fact(Skip = "red: AC6 - the permit phrases and ContextBarAccessibleName do not exist yet (story 1.7 Task 7, open question 3)")]
    [Trait("Requirement", "UX-DR28")]
    [Trait("Requirement", "NFR-9")]
    public void Every_permit_phrase_agrees_with_the_capability_matrix()
    {
        var owner = Permits(Role.Owner);
        var admin = Permits(Role.Admin);
        var member = Permits(Role.Member);
        var viewer = Permits(Role.Viewer);

        Assert.Equal("can manage Members and settings", admin);
        Assert.Equal("read only in this Space", viewer);

        // Owner: "... including ownership" - and Owner alone may offer it.
        Assert.Contains("ownership", owner, StringComparison.Ordinal);
        Assert.Equal([Role.Owner], RolesThatMay(Capability.OfferOwnershipTransfer));

        // Admin: "Members and settings".
        Assert.All(
            new[] { Capability.ChangeMembershipRole, Capability.RemoveMembership, Capability.RenameSpace },
            capability => Assert.Contains(Role.Admin, RolesThatMay(capability)));

        // Member: "create and edit Projects and Tasks" - and nothing about settings.
        Assert.Contains("Projects and Tasks", member, StringComparison.Ordinal);
        Assert.Contains(Role.Member, RolesThatMay(Capability.ManageProjects));
        Assert.Contains(Role.Member, RolesThatMay(Capability.ManageTasks));
        Assert.DoesNotContain(Role.Member, RolesThatMay(Capability.RenameSpace));

        // Viewer: "read only".
        var viewerMay = Enum.GetValues<Capability>()
            .Where(capability => RoleCapabilityMatrix.Decide(Role.Viewer, capability) != CapabilityDecision.Denied)
            .ToList();

        Assert.Equal([Capability.ReadProjectsAndTasks, Capability.IssueApiTokenForOneself], viewerMay);

        // The composed name, exactly as AC6 gives it.
        var format = Copy.GetString("ContextBarAccessibleName", CultureInfo.InvariantCulture);

        Assert.NotNull(format);
        Assert.Equal(
            "Northwind Redesign — Admin, can manage Members and settings",
            string.Format(CultureInfo.InvariantCulture, format, "Northwind Redesign", Copy.GetString("RoleAdmin", CultureInfo.InvariantCulture), admin));
    }

    /// <summary>
    /// AC9 / UX-DR32 - no client code remembers which Space was last visited: the route back is
    /// drawn from the current switcher response, never from memory.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What the scan looks for, exactly:</b> an identifier - no whitespace inside it - made of
    /// <c>last</c>, <c>previous</c>, <c>recent</c> or <c>remembered</c>, optionally <c>visited</c> or
    /// <c>active</c>, then <c>Space</c>, optionally <c>Id</c>, in any case and with an optional
    /// leading underscore: <c>lastSpaceId</c>, <c>_previousSpace</c>, <c>RecentActiveSpace</c>,
    /// <c>"last-space"</c> as a query key. Prose such as "never a remembered last Space" has spaces
    /// in it and does not match. Browser storage is <c>ClientStorageBanTests</c>' and is not repeated.
    /// </para>
    /// <para>
    /// <b>Green today, a standing gate</b>, with <see cref="The_last_Space_scan_detects_a_planted_field"/>
    /// beside it; the ATDD run also planted a <c>_lastSpaceId</c> field in <c>ClientRoutes.cs</c>.
    /// </para>
    /// </remarks>
    [Fact]
    [Trait("Requirement", "UX-DR32")]
    public void No_client_code_remembers_a_last_Space()
    {
        var sources = ClientSources().ToList();

        Assert.Contains(sources, file => file.Name == "ClientRoutes.cs");

        var offenders = sources
            .SelectMany(file => RemembersASpace.Matches(File.ReadAllText(file.FullName))
                .Select(match => $"{Path.GetRelativePath(RepositoryLayout.Root.FullName, file.FullName)}: '{match.Value}'"))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "The client remembers a Space (UX-DR32: the route back is drawn from the current switcher "
            + "response, never a remembered last Space): " + string.Join(", ", offenders));
    }

    /// <summary>
    /// The detector behind <see cref="No_client_code_remembers_a_last_Space"/>, fired on planted
    /// signals.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR32")]
    public void The_last_Space_scan_detects_a_planted_field()
    {
        Assert.Matches(RemembersASpace, "private Guid? _lastSpaceId;");
        Assert.Matches(RemembersASpace, "var previousSpace = route;");
        Assert.Matches(RemembersASpace, "[SupplyParameterFromQuery(Name = \"last-space\")]");
        Assert.DoesNotMatch(RemembersASpace, "// never a remembered last Space");
        Assert.DoesNotMatch(RemembersASpace, "var activeSpace = directory.First();");
    }

    private static string? Permits(Role role) =>
        Copy.GetString("RolePermits" + role, CultureInfo.InvariantCulture);

    private static List<Role> RolesThatMay(Capability capability) =>
        [.. Enum.GetValues<Role>().Where(role => RoleCapabilityMatrix.Decide(role, capability) != CapabilityDecision.Denied)];

    private static bool SetsTheTitle(string text) =>
        text.Contains("<PageTitle", StringComparison.Ordinal)
        || text.Contains("<HeadContent", StringComparison.Ordinal)
        || DocumentTitle.IsMatch(text);

    /// <summary>
    /// Every file the client is built or served from: components, C#, and what <c>wwwroot</c> serves
    /// directly. Build output excluded.
    /// </summary>
    private static IEnumerable<FileInfo> ClientSources() =>
        ClientRoot.EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(file => file.Extension is ".razor" or ".cs" or ".html" or ".js")
            .Where(file => !file.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    [GeneratedRegex("<title>(?<text>[^<]*)</title>", RegexOptions.None, 1000)]
    private static partial Regex TitleElement { get; }

    [GeneratedRegex(@"document\s*\.\s*title", RegexOptions.None, 1000)]
    private static partial Regex DocumentTitle { get; }

    [GeneratedRegex(@"history\s*\.\s*scrollRestoration\s*=\s*['""]manual['""]", RegexOptions.None, 1000)]
    private static partial Regex ManualScrollRestoration { get; }

    [GeneratedRegex(@"@\*.*?\*@", RegexOptions.Singleline, 1000)]
    private static partial Regex RazorComment { get; }

    [GeneratedRegex(@"\b_?(last|previous|recent|remembered)[-_]?(visited|active)?[-_]?space(id)?\b", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex RemembersASpace { get; }
}
