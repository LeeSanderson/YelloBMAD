using System.Text.RegularExpressions;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Gate A - AR-35's Consistency Conventions, as they apply to the test suites themselves.
/// </summary>
/// <remarks>
/// Story 1.1 owns AR-35, and the conventions it established were recorded in
/// <c>tests/TESTING-CONVENTIONS.md</c> and enforced by nothing. Each assertion here replaces a
/// comment or a document that stated an invariant a later story could silently break.
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-35")]
public sealed partial class TestingConventionTests
{
    private const string IgnoreZeroTestExitCode = "--ignore-exit-code 8";

    /// <summary>
    /// The image reference this gate hunts for, assembled at runtime.
    /// </summary>
    /// <remarks>
    /// Written in two pieces so that the gate does not match its own source. The alternative -
    /// excluding this file from the scan - would carve out the one file most likely to acquire
    /// a copy of the value while nobody was looking. Better that the gate reads everything,
    /// including itself.
    /// </remarks>
    private static readonly string SqlServerImageLiteral = string.Concat("mcr.microsoft", ".com/mssql/server");

    /// <summary>
    /// The switch that makes an empty suite pass, and the tests that make it a lie.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AC5 needs the four genuinely-empty suites to report zero tests rather than fail, and
    /// Microsoft.Testing.Platform is strict by default: exit code 8 means "the test session ran
    /// zero tests". So the switch is correct today and becomes dangerous the moment a suite
    /// gains its first test - at which point a <c>[Trait]</c> typo or broken discovery yields
    /// zero tests, <c>dotnet test</c> returns 0, and the suite reports success having asserted
    /// nothing.
    /// </para>
    /// <para>
    /// The only protection was a comment in each project file reading "REMOVE THIS THE MOMENT
    /// THIS SUITE GAINS ITS FIRST TEST" - which names this exact risk and then does not gate
    /// it. Story 1.9 writes the isolation cases that SM-1 gates release on, into a suite
    /// carrying that switch. The project's own bar applies: "a rule that relies on discipline
    /// is not a rule here".
    /// </para>
    /// </remarks>
    [Fact]
    public void Only_suites_with_no_tests_may_ignore_the_zero_test_exit_code()
    {
        var problems = new List<string>();

        foreach (var project in RepositoryLayout.AllProjectFiles.Where(IsTestProject))
        {
            var path = RepositoryLayout.RelativePath(project);
            var ignoresEmptyRun = IgnoresZeroTestExitCode(project);
            var hasTests = ContainsTestMethods(project);

            if (ignoresEmptyRun && hasTests)
            {
                problems.Add(
                    $"{path} carries `{IgnoreZeroTestExitCode}` AND contains tests. Remove the " +
                    "switch: with it in place, a filter typo or broken discovery makes this " +
                    "suite report success having run nothing.");
            }
            else if (!ignoresEmptyRun && !hasTests)
            {
                problems.Add(
                    $"{path} contains no tests and does NOT carry `{IgnoreZeroTestExitCode}`, so " +
                    "`dotnet test` over the solution will return exit code 8. Add the switch " +
                    "while the suite is genuinely empty, or add the tests.");
            }
            else
            {
                // Empty and permitted to be, or populated and strict. Both are correct.
            }
        }

        Assert.True(problems.Count == 0,
            "A suite's zero-test policy does not match whether it actually has tests." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            string.Join(Environment.NewLine, problems.Select(p => $"  - {p}")) +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "Note the precise failure mode this governs: the BUILD succeeds either way, and it " +
            "is `dotnet test` that returns 8.");
    }

    /// <summary>
    /// Values that more than one project has to agree on are stated once, in
    /// <c>Directory.Build.props</c>, and reached through assembly metadata.
    /// </summary>
    /// <remarks>
    /// Both of these used to be independent literals in two files each, with nothing comparing
    /// them: the SQL Server image in <c>Yello.AppHost</c> and in the shared fixture, and the
    /// Aspire database resource name in <c>Yello.AppHost</c> and <c>Yello.Host</c>. A
    /// divergence in the first has the suites and local orchestration silently running
    /// different engine builds; a divergence in the second has the Host start normally with
    /// AC4's only evidence gone and the exit code unchanged.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-1")]
    public void Values_shared_between_projects_are_stated_once_in_the_build()
    {
        var problems = new List<string>();

        var props = RepositoryLayout.LoadXml(RepositoryLayout.DirectoryBuildProps);

        foreach (var (property, key) in new[]
                 {
                     ("YelloSqlServerImage", "Yello.SqlServerImage"),
                     ("YelloDatabaseResourceName", "Yello.DatabaseResourceName"),
                 })
        {
            if (!props.Descendants().Any(e => e.Name.LocalName.Equals(property, StringComparison.Ordinal)))
            {
                problems.Add($"Directory.Build.props declares no <{property}>.");
            }

            if (!RepositoryLayout.ItemIncludes(RepositoryLayout.DirectoryBuildProps, "AssemblyMetadata")
                    .Contains(key, StringComparer.Ordinal))
            {
                problems.Add(
                    $"Directory.Build.props emits no AssemblyMetadata '{key}', so nothing can " +
                    "read the value at runtime.");
            }
        }

        problems.AddRange(HardCodedSharedValues());

        Assert.True(problems.Count == 0,
            "A value more than one project depends on is not stated in exactly one place." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            string.Join(Environment.NewLine, problems.Select(p => $"  - {p}")) +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "Directory.Build.props states them and stamps them into every assembly; consumers " +
            "read their own assembly's copy. A literal in source is a second source of truth.");
    }

    /// <summary>
    /// The <c>Assumption</c> trait has to carry the identifier the selective run filters on.
    /// </summary>
    /// <remarks>
    /// The trait vocabulary was established in story 1.1 so that later stories would copy it
    /// rather than invent one, and the documented example diverged from the test design's own
    /// filter: the design uses <c>[Trait("Assumption", "PRD-12-2")]</c> and documents
    /// <c>dotnet test --filter "Assumption~PRD-12"</c> to find "every test resting on an
    /// unconfirmed assumption". A trait valued <c>A-3</c> never matches that filter, and the
    /// tests resting on the thirteen unconfirmed PRD assumptions would be unfindable by the one
    /// command written to find them. Cheap now, expensive once stories start copying it.
    /// </remarks>
    [Fact]
    public void Every_Assumption_trait_carries_a_source_identifier_the_selective_run_can_find()
    {
        var offenders = new List<string>();

        foreach (var project in RepositoryLayout.AllProjectFiles.Where(RepositoryLayout.IsUnderTestsDirectory))
        {
            foreach (var file in RepositoryLayout.SourceFilesOf(project))
            {
                offenders.AddRange(
                    from match in AssumptionTraitPattern.Matches(File.ReadAllText(file.FullName)).Cast<Match>()
                    let value = match.Groups[1].Value
                    where !AssumptionIdentifierPattern.IsMatch(value)
                    select $"{RepositoryLayout.RelativePath(file)}: Assumption trait valued '{value}'.");
            }
        }

        Assert.True(offenders.Count == 0,
            "An Assumption trait does not name its source document, so the selective run that " +
            "exists to find these tests - `dotnet test --filter \"Assumption~PRD-12\"` - will " +
            $"not match it:{Environment.NewLine}" +
            string.Join(Environment.NewLine, offenders.Select(o => $"  - {o}")) +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "Use the source identifier, e.g. \"PRD-12-2\" for PRD section 12 assumption 2.");
    }

    private static IEnumerable<string> HardCodedSharedValues()
    {
        var sourceFiles = RepositoryLayout.AllProjectFiles
            .SelectMany(RepositoryLayout.SourceFilesOf)
            .Distinct();

        foreach (var file in sourceFiles)
        {
            var text = File.ReadAllText(file.FullName);
            var path = RepositoryLayout.RelativePath(file);

            if (text.Contains(SqlServerImageLiteral, StringComparison.OrdinalIgnoreCase))
            {
                yield return $"{path} states the SQL Server image literally.";
            }

            foreach (var match in ResourceNameLiteralPattern.Matches(text).Cast<Match>())
            {
                yield return
                    $"{path} passes a literal to {match.Groups[1].Value}(), which duplicates the " +
                    "Aspire resource name.";
            }
        }
    }

    private static bool IsTestProject(FileInfo project) =>
        RepositoryLayout.LoadXml(project)
            .Descendants()
            .Any(e => e.Name.LocalName.Equals("IsTestProject", StringComparison.Ordinal)
                && e.Value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase));

    private static bool IgnoresZeroTestExitCode(FileInfo project) =>
        RepositoryLayout.LoadXml(project)
            .Descendants()
            .Where(e => e.Name.LocalName.Equals("TestingPlatformCommandLineArguments", StringComparison.Ordinal))
            .Any(e => e.Value.Contains(IgnoreZeroTestExitCode, StringComparison.Ordinal));

    private static bool ContainsTestMethods(FileInfo project) =>
        RepositoryLayout.SourceFilesOf(project)
            .Any(f => TestAttributePattern.IsMatch(File.ReadAllText(f.FullName)));

    // An opening bracket immediately before the attribute name, so the words appearing in prose
    // or in a failure message do not register as a test.
    [GeneratedRegex(@"\[\s*(Xunit\.)?(Fact|Theory)\s*[\](]", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex TestAttributePattern { get; }

    [GeneratedRegex(@"Trait\s*\(\s*""Assumption""\s*,\s*""([^""]*)""", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex AssumptionTraitPattern { get; }

    // A source document and a location within it - PRD-12-2, AR-40a, NFR-5.
    [GeneratedRegex(@"^(PRD|AR|AD|FR|NFR|UX-DR)-\w+", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex AssumptionIdentifierPattern { get; }

    [GeneratedRegex(@"\b(AddDatabase|GetConnectionString)\s*\(\s*""", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex ResourceNameLiteralPattern { get; }
}
