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
    /// The image AC4 names, assembled rather than written.
    /// </summary>
    /// <remarks>
    /// AC4 requires a container on the registry-qualified <c>2025-latest</c> reference this
    /// field assembles, and after the shared value was centralised nothing asserted it: the
    /// gate below checked that the property and the assembly metadata <i>existed</i>, never
    /// what they said. It is assembled in pieces because the consumer scan below reads string
    /// literals in this solution's source, and a complete literal here would be one. So the tag
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

            var unconditional = declarations.Where(RepositoryLayout.IsUnconditional).ToList();

            if (unconditional.Count == 0)
            {
                problems.Add(declarations.Count == 0
                    ? $"Directory.Build.props declares no <{property}>."
                    : $"Directory.Build.props declares <{property}> only inside a Condition, so " +
                      "whether any consumer receives a value depends on a condition no gate here " +
                      "evaluates.");
            }

            // A conditional declaration ALONGSIDE the unconditional one is the case that
            // mattered and was missed: filtering conditionals out let a Condition-guarded
            // override win at build time while this gate read the unconditional value and
            // passed. Demonstrated with `-getProperty:`. The correct rule already existed in
            // ProjectFileGateTests.ConditionalFrameworkDeclarations and was not carried across -
            // which is the criticism the previous pass levelled at the one before it.
            problems.AddRange(declarations
                .Where(e => !RepositoryLayout.IsUnconditional(e))
                .Select(e => $"Directory.Build.props ALSO declares <{property}> inside a " +
                             $"Condition, set to '{e.Value.Trim()}'. If that condition fires the " +
                             "build takes this value, not the unconditional one."));

            // MSBuild stamps the value verbatim; this gate used to compare a trimmed copy, so a
            // value written across lines passed here and reached the consumers unusable.
            problems.AddRange(unconditional
                .Where(e => e.Value != e.Value.Trim())
                .Select(_ => $"<{property}> carries leading or trailing whitespace. MSBuild " +
                             "stamps it verbatim, so the consumers receive an unusable value " +
                             "while this assertion compares a trimmed copy."));

            var metadata = props.Descendants()
                .Where(e => e.Name.LocalName.Equals("AssemblyMetadata", StringComparison.Ordinal))
                .Where(e => string.Equals(e.Attribute("Include")?.Value.Trim(), key, StringComparison.Ordinal))
                .ToList();

            // EXACTLY one, not "any". Both readers use FirstOrDefault, so a duplicate item
            // placed BEFORE the correct one wins - demonstrated: the fixture returned a
            // 2019-latest image while all 47 assertions stayed green, which is precisely the
            // silent engine divergence centralising the value was meant to end. SQL Server 2019
            // has neither SESSION_CONTEXT parity for NFR-1 nor the collation AD-15 needs.
            if (metadata.Count != 1)
            {
                problems.Add(metadata.Count == 0
                    ? $"Directory.Build.props emits no AssemblyMetadata '{key}', so nothing can " +
                      "read the value at runtime."
                    : $"Directory.Build.props emits AssemblyMetadata '{key}' {metadata.Count} " +
                      "times. Both readers take the FIRST, so a duplicate silently decides the " +
                      "value; there must be exactly one.");
            }
            else if (!RepositoryLayout.IsUnconditional(metadata[0]))
            {
                problems.Add(
                    $"AssemblyMetadata '{key}' is emitted only inside a Condition, so whether it " +
                    "reaches an assembly depends on a condition no gate here evaluates.");
            }
            else if (!string.Equals(
                         metadata[0].Attribute("Value")?.Value.Trim(), $"$({property})", StringComparison.Ordinal))
            {
                // Include without Value is the failure that matters: the metadata exists, the
                // gate was satisfied, and the value stamped is empty.
                problems.Add(
                    $"AssemblyMetadata '{key}' does not carry Value=\"$({property})\", so what is " +
                    $"stamped into every assembly is not what <{property}> says.");
            }
            else
            {
                // Declared unconditionally, stamped exactly once, and stamped from the property
                // rather than from a second copy of the value.
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

    /// <summary>
    /// Shared values restated as literals in the source of the projects that consume them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Scoped to the consumers, and to string-literal positions.</b> Both narrowings close
    /// demonstrated false positives without giving up what the scan is for. A duplicate only
    /// <i>matters</i> in a project that reads the value - that is what "two sources of truth"
    /// means, and it is exactly the original defect: the image was stated once in the AppHost and
    /// again in the fixture, and the resource name once in the AppHost and again in the Host.
    /// A `.cs` file elsewhere that merely contains the characters is not a second source of
    /// anything, and failing the build for it was reported as a real defect.
    /// </para>
    /// <para>
    /// Matching complete string literals rather than raw text is the other half: the resource
    /// name is <c>yello</c>, the product's own name, so <c>internal const string Greeting =
    /// "yello";</c> - or a comment, a trait, a display string, a seed value - failed the build
    /// with a message about a duplication that did not exist.
    /// </para>
    /// <para>
    /// The <c>AddDatabase</c>/<c>GetConnectionString</c> regex that used to sit here has been
    /// removed rather than narrowed. It reported every literal-argument call, so a plainly
    /// legitimate <c>GetConnectionString("read-replica")</c> failed the build and story 1.10 or
    /// any story adding a cache, a blob or a read replica would have hit it. Matching the value
    /// closes the interpolation, <c>const</c> and local routes it was added for, so it was
    /// redundant as well as wrong.
    /// </para>
    /// </remarks>
    private static IEnumerable<string> HardCodedSharedValues()
    {
        // Read at runtime rather than written: this file is itself scanned, so a literal here
        // would report itself, and the consumer scan below reads literals in this solution.
        var image = SharedValue("YelloSqlServerImage");
        var resourceName = SharedValue("YelloDatabaseResourceName");

        foreach (var (project, value, label) in new[]
                 {
                     ("Yello.AppHost", image, "the SQL Server image"),
                     ("Yello.Tests.Shared", image, "the SQL Server image"),
                     ("Yello.AppHost", resourceName, "the Aspire database resource name"),
                     ("Yello.Host", resourceName, "the Aspire database resource name"),
                 })
        {
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            var projectFile = RepositoryLayout.AllProjectFiles
                .FirstOrDefault(p => RepositoryLayout.ProjectName(p).Equals(project, StringComparison.Ordinal));

            if (projectFile is null)
            {
                yield return
                    $"'{project}' consumes {label} but no such project exists, so this scan is " +
                    "reading nothing. Update the consumer list.";
                continue;
            }

            foreach (var file in RepositoryLayout.SourceFilesOf(projectFile))
            {
                var literals = StringLiteralPattern
                    .Matches(File.ReadAllText(file.FullName))
                    .Select(m => m.Groups[1].Value);

                if (literals.Contains(value, StringComparer.Ordinal))
                {
                    yield return
                        $"{RepositoryLayout.RelativePath(file)} states {label} as a string " +
                        "literal. Read it from assembly metadata instead - it is stated once in " +
                        "Directory.Build.props, and this project is one of the consumers that " +
                        "would diverge.";
                }
            }
        }
    }

    /// <summary>
    /// A shared value as <c>Directory.Build.props</c> unconditionally states it.
    /// </summary>
    private static string SharedValue(string property) =>
        RepositoryLayout.LoadXml(RepositoryLayout.DirectoryBuildProps)
            .Descendants()
            .Where(e => e.Name.LocalName.Equals(property, StringComparison.Ordinal))
            .Where(RepositoryLayout.IsUnconditional)
            .Select(e => e.Value.Trim())
            .LastOrDefault() ?? string.Empty;

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
    private static bool IsTestProject(FileInfo project) =>
        MsBuildEvaluation.Property(project, "IsTestProject").Equals("true", StringComparison.OrdinalIgnoreCase)
        || (RepositoryLayout.IsUnderTestsDirectory(project)
            && MsBuildEvaluation.Property(project, "OutputType").Equals("Exe", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether the suite tells Microsoft.Testing.Platform to tolerate a zero-test run.
    /// </summary>
    /// <remarks>
    /// Read from evaluated state, not from the project file's text. Reading the text was
    /// defeated in one line and demonstrated on the populated <c>Yello.Tests.Slices</c>:
    /// <c>&lt;PlantSwitch&gt;--ignore-exit-code 8&lt;/PlantSwitch&gt;</c> plus
    /// <c>$(PlantSwitch)</c> in the arguments made MSBuild apply the switch while this gate
    /// reported green. The four suites already compose this property with
    /// <c>$(TestingPlatformCommandLineArguments)</c>, so property expansion is the file's native
    /// idiom rather than an exotic route.
    /// </remarks>
    private static bool IgnoresZeroTestExitCode(FileInfo project) =>
        IgnoredExitCodes(MsBuildEvaluation.Property(project, "TestingPlatformCommandLineArguments"))
            .Contains(ZeroTestExitCode, StringComparer.Ordinal);

    /// <summary>
    /// The exit codes an argument string tells Microsoft.Testing.Platform to ignore.
    /// </summary>
    /// <remarks>
    /// Substring-matching the whole <c>--ignore-exit-code 8</c> phrase was wrong in both
    /// directions. The multi-code form <c>--ignore-exit-code 2;8</c> does carry 8 and did not
    /// match, so a populated release-gating suite would go on swallowing zero-test runs with
    /// this gate reporting green; and <c>--ignore-exit-code 80</c> does not carry 8 but did
    /// match, so a suite would be told to remove a switch it never had.
    /// <para>
    /// Both spellings of the flag are accepted, because Microsoft.Testing.Platform accepts
    /// both: space-separated and <c>=</c>-joined. The <c>=</c> form was demonstrated to work
    /// while the space-only tokeniser never saw it - "Zero tests ran … passed", exit 0.
    /// </para>
    /// </remarks>
    private static IEnumerable<string> IgnoredExitCodes(string arguments)
    {
        var tokens = arguments.Split(
            [' ', '\t', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = 0; i < tokens.Length; i++)
        {
            var joined = tokens[i].StartsWith(IgnoreExitCodeFlag + "=", StringComparison.Ordinal)
                ? tokens[i][(IgnoreExitCodeFlag.Length + 1)..]
                : null;

            var separate = tokens[i].Equals(IgnoreExitCodeFlag, StringComparison.Ordinal) && i < tokens.Length - 1
                ? tokens[i + 1]
                : null;

            foreach (var code in (joined ?? separate ?? string.Empty).Split(
                         ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return code;
            }
        }
    }

    /// <summary>
    /// Whether a suite actually contains test methods.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Comments are stripped before matching, and the attribute must be followed by a member
    /// declaration. Matching raw text produced a state <b>no edit to the project could
    /// satisfy</b>: a non-test helper whose doc comment mentioned "the <c>[Fact]</c> methods
    /// story 1.9 will add" made this gate demand the zero-test switch be removed, and removing
    /// it made <c>dotnet test</c> return exit code 8. Both gates could not hold at once, in the
    /// first file story 1.9 would touch. An over-broad gate that fails a build nobody has broken
    /// is not a lesser defect than a permissive one - it is how a suite comes to be dismissed.
    /// </para>
    /// <para>
    /// The pattern also no longer requires the attribute to come first in its list, which
    /// <c>[Trait("a","b"), Fact]</c> did not, nor to be unqualified, which
    /// <c>[global::Xunit.Fact]</c> was not.
    /// </para>
    /// </remarks>
    private static bool ContainsTestMethods(FileInfo project) =>
        RepositoryLayout.SourceFilesOf(project)
            .Any(f => TestAttributePattern.IsMatch(WithoutComments(File.ReadAllText(f.FullName))));

    /// <summary>
    /// Source with comments blanked out, so prose about a construct is not read as the construct.
    /// </summary>
    /// <remarks>
    /// Line and block comments are replaced rather than removed, so this cannot join two tokens
    /// that were separated only by a comment. String literals containing <c>//</c> are affected,
    /// which is acceptable here: this is used only for the test-attribute scan, where a
    /// <c>[Fact]</c> inside a string after a <c>//</c> is not a case worth protecting. It is
    /// deliberately NOT used for the shared-value scans, where a false negative would hide a
    /// real second source of truth.
    /// </remarks>
    private static string WithoutComments(string source) =>
        CommentPattern.Replace(source, match => new string(' ', match.Length));

    // An opening bracket immediately before the attribute name, so the words appearing in prose
    // or in a failure message do not register as a test.
    //
    // An attribute list containing Fact or Theory in ANY position, optionally qualified and
    // optionally suffixed, followed by further attribute lists and then a member declaration.
    // Every part of that earns its place against a demonstrated miss: the attribute need not be
    // first ([Trait("a","b"), Fact]) nor unqualified ([global::Xunit.Fact]) nor unsuffixed
    // ([FactAttribute]); and requiring a declaration to follow is what stops prose about a
    // [Fact] registering as one, which had created an unsatisfiable gate pair.
    [GeneratedRegex(
        @"\[[^\]]*\b(global::)?(Xunit\.)?(Fact|Theory)(Attribute)?\b[^\]]*\]\s*(\[[^\]]*\]\s*)*(public|private|protected|internal|static|async|partial)\b",
        RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex TestAttributePattern { get; }

    // Block comments and line comments, including XML docs.
    [GeneratedRegex(@"/\*.*?\*/|//[^\r\n]*", RegexOptions.Singleline, matchTimeoutMilliseconds: 5000)]
    private static partial Regex CommentPattern { get; }

    // A complete C# string literal, so a shared value is matched where it is actually STATED
    // rather than wherever its characters happen to appear. The resource name is `yello` - the
    // product's own name, five characters - so raw substring matching failed the build on any
    // file that merely mentioned it.
    [GeneratedRegex(@"""([^""\\\r\n]*)""", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex StringLiteralPattern { get; }

    [GeneratedRegex(@"Trait\s*\(\s*""Assumption""\s*,\s*""([^""]*)""", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex AssumptionTraitPattern { get; }

    // A source document and a location within it - PRD-12-2, AR-40a, NFR-5.
    [GeneratedRegex(@"^(PRD|AR|AD|FR|NFR|UX-DR)-\w+", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex AssumptionIdentifierPattern { get; }
}
