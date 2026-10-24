using Xunit;
using Yello.Contracts.Accounts;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// The case list is data, and both of its gaps are failures: a row with no expectation for a surface
/// (AC1, AD-4), and a mapped route with no row (AC8, B6, R17, T6).
/// </summary>
/// <remarks>
/// <para>
/// <b>Activated by story 1.9 Task 4.</b> The scaffolds reached the list by name through reflection
/// because it did not exist; they now read <see cref="IsolationCases"/> directly. The member names the
/// scaffold proposed (<c>All</c>, <c>CrossProduct</c>, <c>Id</c>, <c>Route</c>, <c>Method</c>,
/// <c>Expectations</c>, <c>PendingUntil</c>, <c>PendingReason</c>) are the ones the list has.
/// </para>
/// <para>
/// <b>No fixture.</b> Gates 1, 3 and 4 read data. Gate 2 reads the real Host's route table in process
/// (<see cref="HostRouteTable"/>), which needs no database.
/// </para>
/// </remarks>
[Trait("Suite", "Isolation")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-35")]
[Trait("Requirement", "NFR-1")]
[Trait("Requirement", "AD-4")]
public sealed class IsolationCaseListTests
{
    /// <summary>
    /// 1.9-GATE-001 / AC1 - every case declares an expectation for every surface; there is no "not
    /// applicable", only the answer that surface gets.
    /// </summary>
    [Fact]
    public void Every_case_declares_an_expectation_for_every_surface()
    {
        var surfaces = Enum.GetValues<IsolationSurface>();

        // Vacuity guard: a gate over an empty list, or over a one-member enum, passes by construction.
        Assert.NotEmpty(IsolationCases.All);
        Assert.True(surfaces.Length >= 2, $"IsolationSurface has {surfaces.Length} member(s); AD-4 names two surfaces.");

        Assert.Equal([], Gaps(IsolationCases.All));
    }

    /// <summary>
    /// The <c>/api</c> routes that are not isolation rows, each with why. They carry no Space and read
    /// nothing Account-wide: they establish or end a credential, or are the unmatched fallback, which is
    /// outside the route group and named by <c>ISO-UNMATCHED-UNDER-SPACE</c> on the paths it answers.
    /// Anything else under <c>/api</c> - a Space route whatever its parameter is called, an Account route
    /// wherever it lives - needs a row.
    /// </summary>
    private static readonly Dictionary<(string Method, string Pattern), string> NotIsolationRoutes = new()
    {
        [("POST", AccountRoutes.Register)] = "anonymous registration; carries no credential",
        [("POST", SessionRoutes.Authenticate)] = "sign-in; establishes the credential",
        [("DELETE", SessionRoutes.Authenticate)] = "sign-out; Lee's decisions 1 and 2 are Revocation rows",
        [("GET", SessionRoutes.Current)] = "the caller's own Session; carries no Space",
        [("GET", SessionRoutes.AntiforgeryToken)] = "an anti-forgery token for any caller",
        [("*", IsolationCases.UnmatchedApi)] = "the unmatched fallback; does no resolution",
    };

    /// <summary>
    /// 1.9-GATE-002 / AC8 - every <c>/api</c> route the real Host maps, other than the named
    /// credential-establishing routes and the unmatched fallback, is named by at least one case, so a
    /// route added without an isolation row fails the suite whatever its parameter is called and
    /// wherever it lives.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Every_api_route_the_Host_maps_but_the_named_credential_routes_is_named_by_a_case()
    {
        var all = await HostRouteTable.ReadAsync();
        var mapped = all
            .Where(route => route.Pattern.StartsWith("/api/", StringComparison.Ordinal) && !NotIsolationRoutes.ContainsKey(route))
            .ToList();

        // Vacuity guard: the comparison set must hold a route known to exist before anything is asserted.
        Assert.Contains(mapped, route => route.Pattern == SpaceRoutes.Active && route.Method == HttpMethod.Get.Method);

        // The exclusions are routes the Host still maps, so a renamed route cannot leave a stale
        // exclusion behind while its new pattern slips past.
        Assert.Equal([], NotIsolationRoutes.Keys.Where(route => !all.Contains(route)).Select(route => $"{route.Method} {route.Pattern}"));

        Assert.Equal([], Uncovered(mapped, IsolationCases.All));
    }

    /// <summary>
    /// 1.9-GATE-003 / AC8 - a row deferred to a later story names that story and a reason, and the story
    /// is still <c>backlog</c>, so a pending row cannot outlive its story silently.
    /// </summary>
    [Fact]
    public void Every_pending_row_names_a_story_that_is_still_in_backlog()
    {
        var statuses = SprintStatuses();
        var pending = IsolationCases.All.Where(@case => @case.PendingUntil is not null).ToList();

        // Vacuity guard: story 1.9 lands at least two pending rows (2.1 and 2.2), and the status file was read.
        Assert.True(pending.Count >= 2, $"Expected the 2.1 and 2.2 pending rows; found {pending.Count}.");
        Assert.Contains(statuses, entry => entry.Key.StartsWith("1-9-", StringComparison.Ordinal));

        var stale = new List<string>();

        foreach (var @case in pending)
        {
            var story = @case.PendingUntil!;
            var prefix = story.Replace('.', '-') + "-";
            var key = statuses.Keys.SingleOrDefault(candidate => candidate.StartsWith(prefix, StringComparison.Ordinal));

            if (string.IsNullOrWhiteSpace(@case.PendingReason))
            {
                stale.Add($"{@case.Id}: pending until {story} with no reason");
            }

            if (key is null)
            {
                stale.Add($"{@case.Id}: pending until {story}, which is not a story in sprint-status.yaml");
            }
            else if (!string.Equals(statuses[key], "backlog", StringComparison.Ordinal))
            {
                stale.Add($"{@case.Id}: pending until {story}, which is now '{statuses[key]}' - convert the row");
            }
            else
            {
                // Still backlog: the row is correctly pending.
            }
        }

        Assert.True(stale.Count == 0, string.Join(Environment.NewLine, stale));
    }

    /// <summary>
    /// 1.9-GATE-004 / AC1, AC8 - the theory data is generated from the list as exactly
    /// <c>active cases x surfaces</c>, each row named "case id / surface" so a failure names its case
    /// rather than an index; and every runner's rows together are exactly that cross product, so no
    /// active row is generated without a theory that runs it.
    /// </summary>
    [Fact]
    public void The_cross_product_is_generated_from_the_list_and_names_each_row_by_case_and_surface()
    {
        var surfaces = Enum.GetValues<IsolationSurface>();
        var active = IsolationCases.All.Where(@case => @case.PendingUntil is null).ToList();
        var names = IsolationCases.CrossProduct.Select(row => row.TestDisplayName ?? string.Empty).ToList();

        Assert.NotEmpty(active);
        Assert.Equal(active.Count * surfaces.Length, names.Count);

        var expected = active
            .SelectMany(@case => surfaces.Select(surface => $"{@case.Id} / {surface}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expected, names.Order(StringComparer.Ordinal));

        var byRunner = IsolationCases.BoundaryRows
            .Concat(IsolationCases.TimingRows)
            .Concat(IsolationCases.PooledRows)
            .Select(row => row.TestDisplayName ?? string.Empty)
            .Order(StringComparer.Ordinal);

        Assert.Equal(expected, byRunner);
    }

    /// <summary>
    /// "Case id / surface" for every surface a case declares no expectation for - the generic gate
    /// both suites share.
    /// </summary>
    /// <param name="cases">The list.</param>
    /// <returns>The gaps.</returns>
    internal static IReadOnlyList<string> Gaps(IEnumerable<IsolationCase> cases) =>
        SurfaceGaps.Find(cases, @case => @case.Id, @case => @case.Expectations);

    /// <summary>
    /// "METHOD pattern" for every mapped route no case names.
    /// </summary>
    /// <param name="mapped">The routes.</param>
    /// <param name="cases">The list.</param>
    /// <returns>The uncovered routes.</returns>
    internal static List<string> Uncovered(IEnumerable<(string Method, string Pattern)> mapped, IEnumerable<IsolationCase> cases)
    {
        var named = cases.Select(@case => (@case.Method.Method, @case.Route)).ToHashSet();

        return [.. mapped
            .Where(route => !named.Contains((route.Method, route.Pattern)))
            .Select(route => $"{route.Method} {route.Pattern}")];
    }

    /// <summary>
    /// <c>development_status</c> of <c>sprint-status.yaml</c>, as story key to status.
    /// </summary>
    private static Dictionary<string, string> SprintStatuses()
    {
        var file = Path.Combine(HostProcess.RepositoryRoot().FullName, "_bmad-output", "implementation-artifacts", "sprint-status.yaml");

        return File.ReadAllLines(file)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && char.IsDigit(line[0]) && line.Contains(':', StringComparison.Ordinal))
            .Select(line => line.Split(':', 2))
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Split('#')[0].Trim(), StringComparer.Ordinal);
    }
}
