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
    private const string IgnoreExitCodeFlag = "--ignore-exit-code";
    private const string ZeroTestExitCode = "8";

    /// <summary>
    /// The image reference this gate hunts for, assembled at runtime.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written in two pieces so that the gate does not match its own source. The alternative -
    /// excluding this file from the scan - would carve out the one file most likely to acquire
    /// a copy of the value while nobody was looking. Better that the gate reads everything,
    /// including itself.
    /// </para>
    /// <para>
    /// The scan deliberately covers comments and XML docs as well as code, so writing the
    /// registry-qualified reference in prose fails the build. That was considered as a defect
    /// and kept as a rule: stripping comments first means respecting string literals, and
    /// getting that wrong hides a real second source of truth rather than merely annoying
    /// someone. Name the MSBuild property in prose - the existing comments in the fixture and
    /// the AppHost do exactly that, which is why they pass.
    /// </para>
    /// </remarks>
    private static readonly string SqlServerImageLiteral = string.Concat("mcr.microsoft", ".com/mssql/server");

    /// <summary>
    /// The image AC4 names, assembled for the same reason as
    /// <see cref="SqlServerImageLiteral"/>.
    /// </summary>
    /// <remarks>
    /// AC4 requires a container on the registry-qualified <c>2025-latest</c> reference this
    /// field assembles, and after the shared value was centralised nothing asserted it: the
    /// gate below checked that the property and the assembly metadata <i>existed</i>, never
    /// what they said. (The reference is not written out here for the same reason it is
    /// assembled - this file is scanned too.) So the tag
    /// could be changed to any other image with all assertions green - the centralisation
    /// removed the two duplicate literals and, with them, the only thing capable of comparing
    /// them. The one remaining backstop was <c>Assert.StartsWith("17.", …)</c> in
    /// <c>Yello.Tests.Slices</c>, which is not release-gating and skips without a container
    /// runtime.
    /// </remarks>
    private static readonly string ExpectedSqlServerImage =
        string.Concat("mcr.microsoft", ".com/mssql/server", ":2025-latest");

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
            var declarations = props.Descendants()
                .Where(e => e.Name.LocalName.Equals(property, StringComparison.Ordinal))
                .ToList();

            // Unconditional, because a Condition-guarded declaration leaves this gate green
            // while nothing is stamped - and the consumers then fail at startup rather than the
            // build failing here. Verified: `-getItem:AssemblyMetadata` returns empty for a
            // guarded declaration.
            if (!declarations.Exists(RepositoryLayout.IsUnconditional))
            {
                problems.Add(declarations.Count == 0
                    ? $"Directory.Build.props declares no <{property}>."
                    : $"Directory.Build.props declares <{property}> only inside a Condition, so " +
                      "whether any consumer receives a value depends on a condition no gate here " +
                      "evaluates.");
            }

            var metadata = props.Descendants()
                .Where(e => e.Name.LocalName.Equals("AssemblyMetadata", StringComparison.Ordinal))
                .Where(e => string.Equals(e.Attribute("Include")?.Value.Trim(), key, StringComparison.Ordinal))
                .ToList();

            if (metadata.Count == 0)
            {
                problems.Add(
                    $"Directory.Build.props emits no AssemblyMetadata '{key}', so nothing can " +
                    "read the value at runtime.");
            }
            else if (!metadata.Exists(RepositoryLayout.IsUnconditional))
            {
                problems.Add(
                    $"AssemblyMetadata '{key}' is emitted only inside a Condition, so whether it " +
                    "reaches an assembly depends on a condition no gate here evaluates.");
            }
            else if (!metadata.Exists(e => string.Equals(
                         e.Attribute("Value")?.Value.Trim(), $"$({property})", StringComparison.Ordinal)))
            {
                // Include without Value is the failure that matters: the metadata exists, the
                // gate was satisfied, and the value stamped is empty.
                problems.Add(
                    $"AssemblyMetadata '{key}' does not carry Value=\"$({property})\", so what is " +
                    $"stamped into every assembly is not what <{property}> says.");
            }
            else
            {
                // Declared unconditionally, stamped unconditionally, and stamped from the
                // property rather than from a second copy of the value.
            }
        }

        // AC4 names the image, so the value is asserted and not merely its presence.
        var image = props.Descendants()
            .Where(e => e.Name.LocalName.Equals("YelloSqlServerImage", StringComparison.Ordinal))
            .Where(RepositoryLayout.IsUnconditional)
            .Select(e => e.Value.Trim())
            .LastOrDefault();

        if (image is not null && !image.Equals(ExpectedSqlServerImage, StringComparison.Ordinal))
        {
            problems.Add(
                $"<YelloSqlServerImage> is '{image}'. AC4 names " +
                $"'{ExpectedSqlServerImage}' - the engine AD-15's Latin1_General_100_BIN2 " +
                "collation and the row-level security NFR-1 rests on were verified against. " +
                "Changing the engine is an architecture edit, not a developer decision.");
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

        // The resource name's value, quoted, built at runtime rather than written: this file is
        // itself scanned, so a literal here would report itself. Same reason as
        // SqlServerImageLiteral.
        var resourceName = RepositoryLayout.LoadXml(RepositoryLayout.DirectoryBuildProps)
            .Descendants()
            .Where(e => e.Name.LocalName.Equals("YelloDatabaseResourceName", StringComparison.Ordinal))
            .Where(RepositoryLayout.IsUnconditional)
            .Select(e => e.Value.Trim())
            .LastOrDefault();

        var quotedResourceName = string.IsNullOrEmpty(resourceName)
            ? null
            : string.Concat("\"", resourceName, "\"");

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

            // The pattern above requires a quote immediately after the parenthesis, so an
            // interpolated argument, a const passed by name, and the name held in a local all
            // slipped past it - each reinstating the second source of truth this gate exists to
            // prevent. Matching the VALUE closes all three at once, wherever it is written.
            // (The value is not spelled out anywhere in this file, because this file is scanned.)
            if (quotedResourceName is not null
                && text.Contains(quotedResourceName, StringComparison.Ordinal))
            {
                yield return
                    $"{path} states the Aspire database resource name as a literal. Read it from " +
                    "assembly metadata instead - it is stated once in Directory.Build.props.";
            }
        }
    }

    /// <summary>
    /// Whether a project is one of the suites the zero-test policy governs.
    /// </summary>
    /// <remarks>
    /// The declared property is authoritative when present, and all five suites declare it. The
    /// fallback matters for the suite that does not: reading only the literal element meant a
    /// future suite letting the xunit props infer <c>IsTestProject</c> would drop out of this
    /// gate entirely, taking its <c>--ignore-exit-code</c> switch with it - which is the one
    /// outcome this gate exists to prevent. Under <c>tests/</c> with <c>OutputType=Exe</c> is
    /// what a Microsoft.Testing.Platform suite looks like regardless of who set the property.
    /// <c>Yello.Tests.Shared</c> is deliberately neither, being a fixture library rather than a
    /// suite, so it stays out on both tests.
    /// </remarks>
    private static bool IsTestProject(FileInfo project)
    {
        var document = RepositoryLayout.LoadXml(project);

        var declared = document.Descendants()
            .Any(e => e.Name.LocalName.Equals("IsTestProject", StringComparison.Ordinal)
                && e.Value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase));

        if (declared)
        {
            return true;
        }

        return RepositoryLayout.IsUnderTestsDirectory(project)
            && document.Descendants()
                .Any(e => e.Name.LocalName.Equals("OutputType", StringComparison.Ordinal)
                    && e.Value.Trim().Equals("Exe", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IgnoresZeroTestExitCode(FileInfo project) =>
        RepositoryLayout.LoadXml(project)
            .Descendants()
            .Where(e => e.Name.LocalName.Equals("TestingPlatformCommandLineArguments", StringComparison.Ordinal))
            .Any(e => IgnoredExitCodes(e.Value).Contains(ZeroTestExitCode, StringComparer.Ordinal));

    /// <summary>
    /// The exit codes an argument string tells Microsoft.Testing.Platform to ignore.
    /// </summary>
    /// <remarks>
    /// Substring-matching the whole <c>--ignore-exit-code 8</c> phrase was wrong in both
    /// directions. The multi-code form <c>--ignore-exit-code 2;8</c> does carry 8 and did not
    /// match, so a populated release-gating suite would go on swallowing zero-test runs with
    /// this gate reporting green; and <c>--ignore-exit-code 80</c> does not carry 8 but did
    /// match, so a suite would be told to remove a switch it never had.
    /// </remarks>
    private static IEnumerable<string> IgnoredExitCodes(string arguments)
    {
        var tokens = arguments.Split(
            [' ', '\t', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = 0; i < tokens.Length - 1; i++)
        {
            if (!tokens[i].Equals(IgnoreExitCodeFlag, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var code in tokens[i + 1].Split(
                         ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return code;
            }
        }
    }

    private static bool ContainsTestMethods(FileInfo project) =>
        RepositoryLayout.SourceFilesOf(project)
            .Any(f => TestAttributePattern.IsMatch(File.ReadAllText(f.FullName)));

    // An opening bracket immediately before the attribute name, so the words appearing in prose
    // or in a failure message do not register as a test.
    //
    // The trailing class accepts a COMMA, and the optional Attribute suffix is not decoration.
    // Without them this pattern missed [Theory, InlineData(1)], [Theory, MemberData(...)],
    // [Fact, Trait("a","b")] and the fully-suffixed [FactAttribute] - so a populated suite read
    // as empty and kept --ignore-exit-code 8. Story 1.9 writes SM-1's isolation cases as "the
    // same case on both surfaces", which is [Theory, InlineData] shaped, into a release-gating
    // suite. The story's own plant used a solo [Fact], which is why the hole survived a pass
    // that believed it had closed exactly this.
    [GeneratedRegex(@"\[\s*(Xunit\.)?(Fact|Theory)(Attribute)?\s*[\](,]", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex TestAttributePattern { get; }

    [GeneratedRegex(@"Trait\s*\(\s*""Assumption""\s*,\s*""([^""]*)""", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex AssumptionTraitPattern { get; }

    // A source document and a location within it - PRD-12-2, AR-40a, NFR-5.
    [GeneratedRegex(@"^(PRD|AR|AD|FR|NFR|UX-DR)-\w+", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex AssumptionIdentifierPattern { get; }

    [GeneratedRegex(@"\b(AddDatabase|GetConnectionString)\s*\(\s*""", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex ResourceNameLiteralPattern { get; }
}
