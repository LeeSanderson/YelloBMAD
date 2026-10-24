using System.Collections;
using System.Reflection;
using Xunit;
using Yello.Contracts.Spaces;

namespace Yello.Tests.Isolation;

/// <summary>
/// The case list is data, and both of its gaps are failures: a row with no expectation for a surface
/// (AC1, AD-4), and a mapped route with no row (AC8, B6, R17, T6).
/// </summary>
/// <remarks>
/// <para>
/// <b>Red-phase scaffolds.</b> The case list is story 1.9 Task 4's
/// <c>Yello.Tests.Isolation.IsolationCases</c>, reached here <b>by name</b> because it does not
/// exist yet. Each read fails naming the Task that makes it real. On activation, replace the
/// reflection with typed references. The member names (<c>All</c>, <c>CrossProduct</c>, <c>Id</c>,
/// <c>Route</c>, <c>Method</c>, <c>Expectations</c>, <c>PendingUntil</c>, <c>PendingReason</c>) are
/// the story's; a different name chosen in dev-story is corrected here and recorded in the Debug
/// Log.
/// </para>
/// <para>
/// <b>No fixture.</b> Gates 1, 3 and 4 read data. Gate 2 starts a Host through the scaffold contract,
/// on the story's shared engine once Task 2 has built it - never one of its own.
/// </para>
/// </remarks>
[Trait("Suite", "Isolation")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-35")]
[Trait("Requirement", "NFR-1")]
[Trait("Requirement", "AD-4")]
public sealed class IsolationCaseListTests
{
    private const string CaseListTypeName = "Yello.Tests.Isolation.IsolationCases";

    private const string SurfaceTypeName = "Yello.Tests.Isolation.IsolationSurface";

    /// <summary>
    /// 1.9-GATE-001 / AC1 - every case declares an expectation for every surface; there is no "not
    /// applicable", only the answer that surface gets.
    /// </summary>
    [Fact(Skip = "red: AC1 - the case list IsolationCases and the IsolationSurface enum do not exist yet (story 1.9 Task 4)")]
    public void Every_case_declares_an_expectation_for_every_surface()
    {
        var surfaces = SurfaceMembers();
        var cases = Cases();

        // Vacuity guard: a gate over an empty list, or over a one-member enum, passes by construction.
        Assert.NotEmpty(cases);
        Assert.True(surfaces.Count >= 2, $"IsolationSurface has {surfaces.Count} member(s); AD-4 names two surfaces.");

        var gaps = cases
            .SelectMany(@case => surfaces
                .Where(surface => !ExpectationSurfaces(@case).Contains(surface, StringComparer.Ordinal))
                .Select(surface => $"{Text(@case, "Id")} / {surface}"))
            .ToList();

        Assert.True(gaps.Count == 0, "A case exists for one surface and not the other - a gap, not a choice (AD-4): " + string.Join(", ", gaps));
    }

    /// <summary>
    /// 1.9-GATE-002 / AC8 - every Space-scoped and Account-scoped route the real Host maps is named by
    /// at least one case, so a route added without an isolation row fails the suite. The anonymous
    /// and session routes are the Revocation suite's 401 cases and are not isolation rows.
    /// </summary>
    [Fact(Skip = "red: AC8 - needs the real Host's route table (Task 3 harness, Q4 (a)) and the case list (Task 4)")]
    public async Task Every_Space_scoped_and_Account_scoped_route_the_Host_maps_is_named_by_a_case()
    {
        await using var world = await IsolationWorld.StartAsync();

        var mapped = (await world.MappedRoutesAsync())
            .Where(route => IsSpaceScoped(route.Pattern) || IsAccountScoped(route.Pattern))
            .ToList();

        // Vacuity guard: the comparison set must hold a route known to exist before anything is asserted.
        Assert.Contains(mapped, route => route.Pattern == SpaceRoutes.Active && route.Method == HttpMethod.Get.Method);

        var named = Cases()
            .Select(@case => (Method: Text(@case, "Method"), Pattern: Text(@case, "Route")))
            .ToHashSet();

        var uncovered = mapped
            .Where(route => !named.Contains((route.Method, route.Pattern)))
            .Select(route => $"{route.Method} {route.Pattern}")
            .ToList();

        Assert.True(uncovered.Count == 0, "A route is mapped with no isolation case on it (AC8): " + string.Join(", ", uncovered));
    }

    /// <summary>
    /// 1.9-GATE-003 / AC8 - a row deferred to a later story names that story and a reason, and the
    /// story is still <c>backlog</c>, so a pending row cannot outlive its story silently.
    /// </summary>
    [Fact(Skip = "red: AC8 - the case list and its pending rows (ISO-TIMING-IN-SPACE -> 2.2, InsufficientRole -> 2.1) do not exist yet (Task 4)")]
    public void Every_pending_row_names_a_story_that_is_still_in_backlog()
    {
        var statuses = SprintStatuses();
        var pending = Cases().Where(@case => !string.IsNullOrEmpty(TextOrNull(@case, "PendingUntil"))).ToList();

        // Vacuity guard: story 1.9 lands at least two pending rows (2.1 and 2.2), and the status file was read.
        Assert.True(pending.Count >= 2, $"Expected the 2.1 and 2.2 pending rows; found {pending.Count}.");
        Assert.Contains(statuses, entry => entry.Key.StartsWith("1-9-", StringComparison.Ordinal));

        var stale = new List<string>();

        foreach (var @case in pending)
        {
            var id = Text(@case, "Id");
            var story = Text(@case, "PendingUntil");
            var prefix = story.Replace('.', '-') + "-";
            var key = statuses.Keys.SingleOrDefault(candidate => candidate.StartsWith(prefix, StringComparison.Ordinal));

            if (string.IsNullOrWhiteSpace(TextOrNull(@case, "PendingReason")))
            {
                stale.Add($"{id}: pending until {story} with no reason");
            }

            if (key is null)
            {
                stale.Add($"{id}: pending until {story}, which is not a story in sprint-status.yaml");
            }
            else if (!string.Equals(statuses[key], "backlog", StringComparison.Ordinal))
            {
                stale.Add($"{id}: pending until {story}, which is now '{statuses[key]}' - convert the row");
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
    /// <c>active cases x surfaces</c>, and each row is named "case id / surface" so a failure names its
    /// case rather than an index.
    /// </summary>
    [Fact(Skip = "red: AC1 - IsolationCases.CrossProduct does not exist yet (Task 4)")]
    public void The_cross_product_is_generated_from_the_list_and_names_each_row_by_case_and_surface()
    {
        var surfaces = SurfaceMembers();
        var active = Cases().Where(@case => string.IsNullOrEmpty(TextOrNull(@case, "PendingUntil"))).ToList();
        var rows = Member(CaseListType(), "CrossProduct") as IEnumerable
            ?? throw new InvalidOperationException("IsolationCases.CrossProduct is not enumerable (Task 4).");

        var names = rows.Cast<object>()
            .Select(row => row is ITheoryDataRow theoryRow ? theoryRow.TestDisplayName ?? string.Empty : string.Empty)
            .ToList();

        Assert.NotEmpty(active);
        Assert.Equal(active.Count * surfaces.Count, names.Count);

        var expected = active
            .SelectMany(@case => surfaces.Select(surface => $"{Text(@case, "Id")} / {surface}"))
            .Order(StringComparer.Ordinal);

        Assert.Equal(expected, names.Order(StringComparer.Ordinal));
    }

    private static bool IsSpaceScoped(string pattern) =>
        pattern.Contains("{" + SpaceRoutes.SpaceIdParameter, StringComparison.Ordinal);

    private static bool IsAccountScoped(string pattern) =>
        pattern == SpaceRoutes.Directory || pattern.StartsWith("/api/v1/account/", StringComparison.Ordinal);

    private static Type CaseListType() =>
        typeof(IsolationCaseListTests).Assembly.GetType(CaseListTypeName)
        ?? throw new InvalidOperationException($"{CaseListTypeName} does not exist (story 1.9 Task 4).");

    private static List<object> Cases() =>
        Member(CaseListType(), "All") is IEnumerable all
            ? [.. all.Cast<object>()]
            : throw new InvalidOperationException("IsolationCases.All is not enumerable (Task 4).");

    private static List<string> SurfaceMembers()
    {
        var surface = typeof(IsolationCaseListTests).Assembly.GetType(SurfaceTypeName)
            ?? throw new InvalidOperationException($"{SurfaceTypeName} does not exist (story 1.9 Task 3).");

        return [.. Enum.GetNames(surface)];
    }

    private static List<string> ExpectationSurfaces(object @case) =>
        Member(@case.GetType(), "Expectations", @case) is IDictionary expectations
            ? [.. expectations.Keys.Cast<object>().Select(key => key.ToString() ?? string.Empty)]
            : throw new InvalidOperationException($"{Text(@case, "Id")} has no Expectations dictionary (Task 4).");

    private static string Text(object @case, string name) =>
        TextOrNull(@case, name) ?? throw new InvalidOperationException($"A case has no {name} (Task 4).");

    private static string? TextOrNull(object @case, string name) =>
        Member(@case.GetType(), name, @case)?.ToString();

    private static object? Member(Type type, string name, object? instance = null)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        return type.GetProperty(name, flags)?.GetValue(instance) ?? type.GetField(name, flags)?.GetValue(instance);
    }

    /// <summary>
    /// <c>development_status</c> of <c>sprint-status.yaml</c>, as story key to status.
    /// </summary>
    private static Dictionary<string, string> SprintStatuses()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);

        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Yello.slnx")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);

        var file = Path.Combine(root.FullName, "_bmad-output", "implementation-artifacts", "sprint-status.yaml");

        return File.ReadAllLines(file)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && char.IsDigit(line[0]) && line.Contains(':', StringComparison.Ordinal))
            .Select(line => line.Split(':', 2))
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Split('#')[0].Trim(), StringComparer.Ordinal);
    }
}
