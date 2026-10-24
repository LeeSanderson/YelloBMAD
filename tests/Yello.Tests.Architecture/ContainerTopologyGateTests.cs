using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// AC12 - the suites fit the machine they run on: SQL Server engines are shared rather than started
/// per test class, the number started is stated and gated, and each engine is memory-capped. Story
/// 1.9 Task 2; <c>deferred-work.md:8-12, 250, 296</c>; <c>test-design-architecture.md:604-609</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Red-phase scaffolds.</b> Today a full run starts thirteen engines: ten class fixtures that each
/// wrap one, and three direct constructions. These gates go green when Task 2 makes the engine an
/// xunit v3 assembly fixture (one per test process) with a database per class.
/// </para>
/// <para>
/// <b>Read from source, comments blanked</b>, because this suite has no edge to the test projects and
/// must not gain one. Each gate finds its subject before it asserts.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-35")]
[Trait("Requirement", "NFR-1")]
public sealed partial class ContainerTopologyGateTests
{
    /// <summary>
    /// The most engines a full <c>dotnet test Yello.slnx</c> may start: Slices, Isolation, Revocation
    /// and Client one each, and one private engine kept where a test's subject needs one it can stop
    /// (story 1.9 Task 2's target). Raising it is a decision, and <c>TESTING-CONVENTIONS.md</c> must
    /// state the new number.
    /// </summary>
    private const int EngineBudget = 5;

    /// <summary>
    /// Test classes allowed an engine of their own, each with the reason its subject needs one.
    /// Empty until Task 2 decides; every entry needs a reason a reviewer can check.
    /// </summary>
    private static readonly Dictionary<string, string> PrivateEngines = new(StringComparer.Ordinal);

    /// <summary>
    /// 1.9-ARCH-001 - no test class constructs the SQL Server fixture directly or takes it as a class
    /// fixture, outside <see cref="PrivateEngines"/>. Plant (Task 12): a new direct construction is
    /// caught.
    /// </summary>
    [Fact(Skip = "red: AC12 - ten class fixtures and three direct constructions each start an engine today (story 1.9 Task 2)")]
    public void No_test_class_starts_an_engine_of_its_own_outside_the_named_list()
    {
        var shared = TestSources().Where(file => file.Path.StartsWith("tests/Yello.Tests.Shared/", StringComparison.Ordinal)).ToList();

        // Vacuity guard: the engine's own declaration is found, so the scan reads the tree it means to.
        Assert.Contains(shared, file => EngineDeclaration.IsMatch(file.Source));

        var offenders = TestSources()
            .Where(file => !file.Path.StartsWith("tests/Yello.Tests.Shared/", StringComparison.Ordinal))
            .Where(file => DirectEngine.IsMatch(file.Source))
            .Select(file => file.Path)
            .Where(path => !PrivateEngines.ContainsKey(Path.GetFileNameWithoutExtension(path)))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "A test class starts a SQL Server engine of its own; take the per-process engine (assembly fixture) and a "
            + "database on it, or name the class in PrivateEngines with the reason: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// 1.9-ARCH-002 - every suite that uses the engine declares exactly one assembly fixture for it,
    /// the engines a full run starts are within <see cref="EngineBudget"/>, and
    /// <c>TESTING-CONVENTIONS.md</c> states that budget.
    /// </summary>
    [Fact(Skip = "red: AC12 - no suite declares the engine as an assembly fixture yet (story 1.9 Task 2)")]
    public void Each_suite_starts_at_most_one_engine_and_the_run_stays_within_the_stated_budget()
    {
        // Shared is the engine; this suite names the engine in its own patterns and starts none.
        var suites = TestSources()
            .Where(file => !file.Path.StartsWith("tests/Yello.Tests.Shared/", StringComparison.Ordinal))
            .Where(file => !file.Path.StartsWith("tests/Yello.Tests.Architecture/", StringComparison.Ordinal))
            .GroupBy(file => file.Path.Split('/')[1], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var usesEngine = suites.Where(suite => suite.Value.Exists(file => UsesEngine.IsMatch(file.Source))).Select(suite => suite.Key).ToList();

        // Vacuity guard: Slices uses the engine today, so the scan must see at least that.
        Assert.Contains("Yello.Tests.Slices", usesEngine);

        var declarations = usesEngine.ToDictionary(
            suite => suite,
            suite => suites[suite].Sum(file => AssemblyFixtureOfEngine.Count(file.Source)),
            StringComparer.Ordinal);

        var wrong = declarations.Where(entry => entry.Value != 1).Select(entry => $"{entry.Key}: {entry.Value}").ToList();

        Assert.True(wrong.Count == 0, "A suite using the engine must declare exactly one assembly fixture for it: " + string.Join(", ", wrong));

        var engines = declarations.Count + PrivateEngines.Count;

        Assert.True(engines <= EngineBudget, $"A full run starts {engines} engines; the budget is {EngineBudget}.");

        var conventions = File.ReadAllText(Path.Combine(RepositoryLayout.Root.FullName, "tests", "TESTING-CONVENTIONS.md"));

        Assert.Matches(StatedBudget(EngineBudget), conventions);
    }

    /// <summary>
    /// 1.9-ARCH-003 - the engine is started with a memory cap of at most 2 GB
    /// (<c>MSSQL_MEMORY_LIMIT_MB</c>), so a full run cannot take the machine's free memory.
    /// </summary>
    [Fact(Skip = "red: AC12 - the engine sets no memory limit today (story 1.9 Task 2)")]
    [Trait("Priority", "P1")]
    public void The_engine_is_memory_capped()
    {
        var limits = TestSources()
            .Where(file => file.Path.StartsWith("tests/Yello.Tests.Shared/", StringComparison.Ordinal))
            .SelectMany(file => MemoryLimit.Matches(file.Source))
            .Select(match => int.Parse(match.Groups["megabytes"].Value, CultureInfo.InvariantCulture))
            .ToList();

        var limit = Assert.Single(limits);

        Assert.InRange(limit, 1, 2048);
    }

    private static IEnumerable<(string Path, string Source)> TestSources() =>
        RepositoryLayout.EnumerateSourceFiles("*.cs")
            .Select(file => (Path: RepositoryLayout.RelativePath(file), File: file))
            .Where(entry => entry.Path.StartsWith("tests/", StringComparison.Ordinal))
            .Select(entry => (entry.Path, Source: CommentPattern.Replace(File.ReadAllText(entry.File.FullName), " ")));

    private static Regex StatedBudget(int budget) =>
        new($@"(?i)at most\s+\**{budget}\**\s+(SQL Server\s+)?engines", RegexOptions.None, TimeSpan.FromSeconds(1));

    [GeneratedRegex(@"\bclass\s+(SqlServerContainerFixture|SqlServerEngine)\b", RegexOptions.None, 5000)]
    private static partial Regex EngineDeclaration { get; }

    [GeneratedRegex(
        @"new\s+SqlServerContainerFixture\s*\(|SqlServerContainerFixture\s+\w+\s*=\s*new\s*\(|IClassFixture\s*<\s*SqlServerContainerFixture\s*>|ICollectionFixture\s*<\s*SqlServerContainerFixture\s*>",
        RegexOptions.None,
        5000)]
    private static partial Regex DirectEngine { get; }

    [GeneratedRegex(@"\b(SqlServerContainerFixture|SqlServerEngine|MigratedDatabaseFixture)\b", RegexOptions.None, 5000)]
    private static partial Regex UsesEngine { get; }

    [GeneratedRegex(@"\[\s*assembly\s*:\s*AssemblyFixture\s*\(\s*typeof\s*\(\s*([\w.]+\.)?(SqlServerContainerFixture|SqlServerEngine)\s*\)\s*\)\s*\]", RegexOptions.None, 5000)]
    private static partial Regex AssemblyFixtureOfEngine { get; }

    [GeneratedRegex(@"""MSSQL_MEMORY_LIMIT_MB""\s*,\s*""(?<megabytes>\d+)""", RegexOptions.None, 5000)]
    private static partial Regex MemoryLimit { get; }

    [GeneratedRegex(@"/\*.*?\*/|//[^\r\n]*", RegexOptions.Singleline, 5000)]
    private static partial Regex CommentPattern { get; }
}
