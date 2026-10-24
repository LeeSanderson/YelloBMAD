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
/// <b>Story 1.9 Task 2 made them green.</b> A full run used to start thirteen engines (ten class
/// fixtures that each wrapped one, and three direct constructions). The engine is now an xunit v3
/// assembly fixture, <c>SqlServerEngine</c>, one per test process, with a database per class.
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
    /// Empty: story 1.9 found no class whose subject needs an engine it can stop or break - the three
    /// story-1.1 classes only need one they can reach, so they take the shared engine. Every entry
    /// added needs a reason a reviewer can check, and counts against <see cref="EngineBudget"/>.
    /// </summary>
    private static readonly Dictionary<string, string> PrivateEngines = new(StringComparer.Ordinal);

    /// <summary>
    /// Test classes that construct the fixture and never start it, each with why. These start no
    /// engine, so they are not <see cref="PrivateEngines"/> and do not count against the budget; a
    /// listed class that calls <c>InitializeAsync</c> is an offender again.
    /// </summary>
    private static readonly Dictionary<string, string> ConstructedNeverStarted = new(StringComparer.Ordinal)
    {
        ["SharedFixtureSmokeTest"] =
            "asserts that ConnectionString refuses to answer before InitializeAsync - the one fixture property "
            + "that can be checked without a container, so it runs where no runtime exists",
    };

    /// <summary>
    /// 1.9-ARCH-001 - no test class constructs the SQL Server fixture directly or takes it as a class
    /// fixture, outside <see cref="PrivateEngines"/>. Plant (Task 12): a new direct construction is
    /// caught.
    /// </summary>
    [Fact]
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
            .Where(path => !ConstructedNeverStarted.ContainsKey(Path.GetFileNameWithoutExtension(path)) || StartsTheFixture.IsMatch(Source(path)))
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
    [Fact]
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
    [Fact]
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

    private static string Source(string path) =>
        TestSources().Single(file => string.Equals(file.Path, path, StringComparison.Ordinal)).Source;

    private static Regex StatedBudget(int budget) =>
        new($@"(?i)at most\s+\**{budget}\**\s+(SQL Server\s+)?engines", RegexOptions.None, TimeSpan.FromSeconds(1));

    [GeneratedRegex(@"\bclass\s+(SqlServerContainerFixture|SqlServerEngine)\b", RegexOptions.None, 5000)]
    private static partial Regex EngineDeclaration { get; }

    [GeneratedRegex(
        @"new\s+(global::)?([\w.]+\.)?(SqlServerContainerFixture|SqlServerEngine)\s*\(|(SqlServerContainerFixture|SqlServerEngine)\s+\w+\s*=\s*new\s*\(|I(Class|Collection)Fixture\s*<\s*(global::)?([\w.]+\.)?(SqlServerContainerFixture|SqlServerEngine)\s*>",
        RegexOptions.None,
        5000)]
    private static partial Regex DirectEngine { get; }

    [GeneratedRegex(@"\.InitializeAsync\s*\(", RegexOptions.None, 5000)]
    private static partial Regex StartsTheFixture { get; }

    [GeneratedRegex(@"\b(SqlServerContainerFixture|SqlServerEngine|MigratedDatabaseFixture)\b", RegexOptions.None, 5000)]
    private static partial Regex UsesEngine { get; }

    [GeneratedRegex(@"\[\s*assembly\s*:\s*AssemblyFixture\s*\(\s*typeof\s*\(\s*([\w.]+\.)?(SqlServerContainerFixture|SqlServerEngine)\s*\)\s*\)\s*\]", RegexOptions.None, 5000)]
    private static partial Regex AssemblyFixtureOfEngine { get; }

    [GeneratedRegex(@"""MSSQL_MEMORY_LIMIT_MB""\s*,\s*""(?<megabytes>\d+)""", RegexOptions.None, 5000)]
    private static partial Regex MemoryLimit { get; }

    [GeneratedRegex(@"/\*.*?\*/|//[^\r\n]*", RegexOptions.Singleline, 5000)]
    private static partial Regex CommentPattern { get; }
}
