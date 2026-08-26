using System.Xml.Linq;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Gate A - the project-file gate. Asserts the ring rule as it is <i>declared</i>, by
/// reading every project file in the repository.
/// </summary>
/// <remarks>
/// These assertions sit outside the A-1..A-15 numbering. The test design scoped that series
/// to bytecode and schema assertions; this class reads project files as text, which is a
/// different mechanism answering a different question. Keeping them in a separate class
/// keeps the A-series counts legible as later stories add A-4 onward.
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-2")]
public sealed class ProjectFileGateTests
{
    /// <summary>
    /// Properties that decide what a project compiles against, and must therefore be stated
    /// in exactly one place.
    /// </summary>
    private static readonly string[] FrameworkProperties =
        ["TargetFramework", "TargetFrameworks", "RuntimeFrameworkVersion"];

    /// <summary>
    /// Reference item types that must never appear in a file every project imports.
    /// </summary>
    private static readonly string[] ReferenceItemTypes = ["ProjectReference", "PackageReference"];

    /// <summary>
    /// The two file shapes that claim to describe the solution's project inventory.
    /// </summary>
    private static readonly string[] SolutionFilePatterns = ["*.sln", "*.slnf"];

    /// <summary>
    /// Asserts every project's declared edges against its row in the table - exactly, in
    /// both directions. An unauthorised edge is a ring violation; a missing edge means the
    /// solution is not wired as the architecture says it is, and a gate that ignored that
    /// would pass an entirely unwired repository.
    /// </summary>
    [Fact]
    public void Every_project_declares_exactly_the_references_the_dependency_rule_allows()
    {
        var violations = new List<string>();

        foreach (var project in RepositoryLayout.AllProjectFiles)
        {
            violations.AddRange(EdgeViolations(project));
        }

        Assert.True(violations.Count == 0, BuildFailureMessage(
            "The declared project references do not match the dependency rule (AD-21 / AR-2).",
            violations));
    }

    private static IEnumerable<string> EdgeViolations(FileInfo project)
    {
        var name = RepositoryLayout.ProjectName(project);
        var path = RepositoryLayout.RelativePath(project);

        if (!AllowedReferenceEdges.Table.TryGetValue(name, out var permitted))
        {
            yield return
                $"{path}: project '{name}' has no row in the allowed-edge table, so it has " +
                "no agreed ring position. Add a row to AllowedReferenceEdges.Table, or " +
                "remove the project.";
            yield break;
        }

        var declared = RepositoryLayout.DeclaredProjectReferences(project);

        // Not Except: it de-duplicates, so a doubly-declared reference would compare equal to
        // a singly-declared one and a duplicate would be invisible. Counting makes both the
        // unauthorised edge and the repeated edge visible.
        foreach (var unauthorised in declared.Where(d => !permitted.Contains(d, StringComparer.Ordinal))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(n => n, StringComparer.Ordinal))
        {
            yield return
                $"{path}: '{name}' -> '{unauthorised}' is NOT PERMITTED. '{name}' may " +
                $"reference {(permitted.Length == 0 ? "NOTHING" : string.Join(", ", permitted))}.";
        }

        foreach (var missing in permitted.Where(p => !declared.Contains(p, StringComparer.Ordinal))
                     .OrderBy(n => n, StringComparer.Ordinal))
        {
            yield return
                $"{path}: '{name}' -> '{missing}' is MISSING. The table requires this edge, " +
                "so either restore the ProjectReference or change the table.";
        }

        foreach (var duplicate in declared.GroupBy(d => d, StringComparer.Ordinal)
                     .Where(g => g.Count() > 1)
                     .Select(g => g.Key)
                     .OrderBy(n => n, StringComparer.Ordinal))
        {
            yield return
                $"{path}: '{name}' -> '{duplicate}' is declared more than once. The edge set is " +
                "asserted exactly, and a repeated edge means the file says something different " +
                "from what it appears to say.";
        }
    }

    [Fact]
    public void Yello_Domain_declares_no_project_reference_at_all()
    {
        var domain = FindProject("Yello.Domain");
        var references = RepositoryLayout.DeclaredProjectReferences(domain);

        Assert.True(references.Count == 0,
            "Yello.Domain is the innermost ring and must reference NOTHING, but " +
            $"{RepositoryLayout.RelativePath(domain)} declares: {string.Join(", ", references)}.");
    }

    [Fact]
    public void Yello_Application_references_neither_Infrastructure_nor_Host()
    {
        AssertDoesNotReference("Yello.Application", "Yello.Infrastructure", "Yello.Host");
    }

    [Fact]
    public void Yello_Infrastructure_does_not_reference_Host()
    {
        AssertDoesNotReference("Yello.Infrastructure", "Yello.Host");
    }

    /// <summary>
    /// AC1's ".NET 10.0.11" has to be a stated fact rather than an implication of whichever
    /// SDK happens to be installed, which is what RuntimeFrameworkVersion makes it - and the
    /// target framework has to be stated at all, which it previously was not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The previous assertion was <c>Assert.Contains("&lt;RuntimeFrameworkVersion&gt;10.0.11...")</c>
    /// over the file's raw text, which passes on a string sitting inside an XML comment or
    /// inside a <c>Condition</c>-guarded <c>PropertyGroup</c> that never evaluates, and passes
    /// while a later <c>PropertyGroup</c> in the same file overrides it. It also read only the
    /// root file, so a per-project override in any of the fourteen <c>.csproj</c> files was
    /// undetected - and because <c>Directory.Build.props</c> is imported <i>first</i>, a
    /// project-level <c>&lt;TargetFramework&gt;net9.0&lt;/TargetFramework&gt;</c> silently
    /// wins.
    /// </para>
    /// <para>
    /// So: parsed as XML (comments vanish), unconditional only, declared exactly once, and
    /// nowhere else in the repository. Together with
    /// <see cref="Exactly_one_of_each_MSBuild_import_file_governs_the_solution"/> that makes
    /// "the root file says net10.0" equivalent to "every project builds net10.0", which is
    /// what the assertion was claiming all along.
    /// </para>
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-1")]
    public void The_target_framework_and_runtime_are_pinned_in_exactly_one_unconditional_place()
    {
        var problems = new List<string>();

        AssertPinnedOnce(problems, "TargetFramework", "net10.0");
        AssertPinnedOnce(problems, "RuntimeFrameworkVersion", "10.0.11");

        foreach (var file in RepositoryLayout.AllProjectFiles.Concat(OtherImportFiles()))
        {
            problems.AddRange(FrameworkRedeclarations(file));
        }

        Assert.True(problems.Count == 0,
            "The framework pin is not a single unconditional fact about the whole solution." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            string.Join(Environment.NewLine, problems.Select(p => $"  - {p}")) +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "Directory.Build.props is imported BEFORE every project file, so a project-level " +
            "restatement wins silently. AC1's \".NET 10.0.11\" has to be one stated fact.");
    }

    private static void AssertPinnedOnce(List<string> problems, string property, string expected)
    {
        var declarations = UnconditionalProperties(RepositoryLayout.DirectoryBuildProps, property).ToList();

        if (declarations.Count == 0)
        {
            problems.Add(
                $"Directory.Build.props declares no unconditional <{property}>. (A value inside " +
                "an XML comment or a Condition-guarded PropertyGroup does not count - it does " +
                "not reach the build.)");
        }
        else if (declarations.Count > 1)
        {
            problems.Add(
                $"Directory.Build.props declares <{property}> {declarations.Count} times " +
                $"({string.Join(", ", declarations)}). MSBuild takes the last, so the file says " +
                "one thing and the build does another.");
        }
        else if (!declarations[0].Equals(expected, StringComparison.Ordinal))
        {
            problems.Add($"Directory.Build.props sets <{property}> to '{declarations[0]}', expected '{expected}'.");
        }
        else
        {
            // Declared exactly once, unconditionally, at the expected value.
        }
    }

    private static IEnumerable<string> FrameworkRedeclarations(FileInfo file) =>
        from property in FrameworkProperties
        where RepositoryLayout.LoadXml(file).Descendants()
            .Any(e => e.Name.LocalName.Equals(property, StringComparison.Ordinal))
        select $"{RepositoryLayout.RelativePath(file)} restates <{property}>, which " +
               "Directory.Build.props owns for the whole solution.";

    /// <summary>
    /// MSBuild imports the <i>nearest</i> <c>Directory.Build.props</c> and stops, so a second
    /// one is not an addition to the root file but a replacement of it.
    /// </summary>
    /// <remarks>
    /// This is what makes every other assertion that reads the root file mean what it says. A
    /// new <c>tests/Directory.Build.props</c> shadows the root entirely and can set any
    /// framework property it likes, while a gate reading the root file still finds the literal
    /// it was looking for and reports green.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-1")]
    public void Exactly_one_of_each_MSBuild_import_file_governs_the_solution()
    {
        var problems = RepositoryLayout.MsBuildImportFiles
            .GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1 || RepositoryLayout.DepthBelowRoot(g.First()) != 0)
            .Select(g =>
                $"'{g.Key}' is at: {string.Join(", ", g.Select(RepositoryLayout.RelativePath))}. " +
                "Exactly one, at the repository root, is the only arrangement in which it " +
                "governs the whole solution.")
            .ToList();

        Assert.True(problems.Count == 0,
            "An MSBuild import file is duplicated or is not at the repository root." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            string.Join(Environment.NewLine, problems.Select(p => $"  - {p}")) +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "MSBuild imports the NEAREST Directory.Build.props and stops. A nested one does not " +
            "add to the root file - it replaces it, and every gate that reads the root file " +
            "then describes a file the build no longer uses.");
    }

    /// <summary>
    /// The ring rule is asserted from <c>.csproj</c> files, so a reference that arrives any
    /// other way is a hole in it.
    /// </summary>
    /// <remarks>
    /// A <c>&lt;ProjectReference&gt;</c> placed in <c>Directory.Build.props</c> gives
    /// <i>every</i> project that edge - Domain included - and the ring gate, which reads
    /// project files, never sees it. AC2's "when a project reference is added" does not
    /// distinguish where it was written.
    /// </remarks>
    [Fact]
    public void No_MSBuild_import_file_declares_a_project_or_package_reference()
    {
        var problems = new List<string>();

        // EVERY import file, the root Directory.Build.props included. Excluding it - as an
        // earlier draft of this gate did, by reusing the exclusion the framework-property check
        // needs - carved out the single most likely home for the violation. The planted
        // reference went undetected until that was fixed.
        foreach (var file in RepositoryLayout.MsBuildImportFiles)
        {
            problems.AddRange(
                from item in ReferenceItemTypes
                let includes = RepositoryLayout.ItemIncludes(file, item).ToList()
                where includes.Count > 0
                select $"{RepositoryLayout.RelativePath(file)} declares <{item}> for: " +
                       $"{string.Join(", ", includes)}.");
        }

        Assert.True(problems.Count == 0,
            "An MSBuild import file declares a reference, which applies it to EVERY project in " +
            "the solution - Yello.Domain included - while the ring gate, which reads .csproj " +
            $"files, sees nothing:{Environment.NewLine}" +
            string.Join(Environment.NewLine, problems.Select(p => $"  - {p}")) +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "References belong in the project that needs them. (GlobalPackageReference in " +
            "Directory.Packages.props is the one sanctioned solution-wide form, and " +
            "PackageVersionPinTests asserts that set exactly.)");
    }

    /// <summary>
    /// A ring boundary is crossed by moving code across it, not only by referencing across it.
    /// </summary>
    /// <remarks>
    /// <c>&lt;Compile Include="..\Yello.Domain\Invariants.cs" /&gt;</c> puts domain source
    /// inside another assembly with no <c>ProjectReference</c> for Gate A to read and no
    /// cross-assembly dependency for Gate B to find. Both gates report green over a solution
    /// whose rings have been merged.
    /// </remarks>
    [Fact]
    public void No_project_compiles_source_from_outside_its_own_directory()
    {
        var problems = new List<string>();

        foreach (var project in RepositoryLayout.AllProjectFiles)
        {
            problems.AddRange(EscapingCompileItems(project));
        }

        Assert.True(problems.Count == 0,
            "A project compiles source from outside its own directory, which moves code across " +
            "a ring boundary with no reference for either gate to see:" +
            $"{Environment.NewLine}" +
            string.Join(Environment.NewLine, problems.Select(p => $"  - {p}")));
    }

    private static IEnumerable<string> EscapingCompileItems(FileInfo project)
    {
        if (project.Directory is null)
        {
            yield break;
        }

        var projectDirectory = Path.GetFullPath(project.Directory.FullName);

        foreach (var include in RepositoryLayout.ItemIncludes(project, "Compile"))
        {
            // Wildcards are resolved by MSBuild against the project directory, so a glob that
            // does not itself climb out cannot reach past it.
            var resolved = Path.GetFullPath(Path.Combine(projectDirectory, include.Replace('\\', '/')));

            if (!resolved.StartsWith(projectDirectory, StringComparison.OrdinalIgnoreCase))
            {
                yield return
                    $"{RepositoryLayout.RelativePath(project)} compiles '{include}', which " +
                    "resolves outside its own directory.";
            }
        }
    }

    /// <summary>
    /// One solution file, in XML format. Story 1.1 originally required the classic format on
    /// the grounds that <c>bmad-testarch-framework</c>'s preflight globs for
    /// <c>package.json</c>, <c>*.csproj</c>, <c>*.sln</c>, <c>playwright.config.*</c> and has
    /// no <c>.slnx</c> branch. Re-read, that rationale does not hold: the backend indicator is
    /// the alternation <c>*.csproj</c>/<c>*.sln</c>, and the skill's Validate Prerequisites
    /// step lists <c>*.csproj</c> without <c>*.sln</c> at all. Fourteen <c>.csproj</c> files
    /// satisfy backend detection on their own, so the solution's extension never reaches the
    /// decision. The same alternation appears in the atdd, automate and ci preflights.
    /// <para>
    /// What the gate still protects is singularity: two solution files - a stale
    /// <c>Yello.sln</c> left beside the <c>.slnx</c> - would let <c>dotnet build</c> and an
    /// IDE disagree about the project inventory, which is the fact every other gate here
    /// reads. <c>dotnet sln migrate</c> leaves the original in place, so this regresses by
    /// accident rather than by decision.
    /// </para>
    /// <para>
    /// The search is recursive and covers <c>.slnf</c> as well. A solution filter is a partial
    /// view of the solution, and CI invoked against one could exclude this very suite - the
    /// gate would not fail, it would not run.
    /// </para>
    /// </summary>
    [Fact]
    public void The_slnx_is_the_only_solution_file_in_the_repository()
    {
        // RepositoryLayout.Root is DEFINED as the directory containing Yello.slnx, so asserting
        // its existence here would be a tautology that reads like a check. What is worth
        // asserting is that nothing else claims to describe the project inventory.
        var strays = SolutionFilePatterns
            .SelectMany(pattern => RepositoryLayout.Root.EnumerateFiles(pattern, SearchOption.AllDirectories))
            .Where(f => f.Extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                || f.Extension.Equals(".slnf", StringComparison.OrdinalIgnoreCase))
            .Select(RepositoryLayout.RelativePath)
            .Where(p => !p.Contains("/bin/", StringComparison.Ordinal)
                && !p.Contains("/obj/", StringComparison.Ordinal))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.True(strays.Count == 0,
            "Found another file describing the project inventory beside " +
            $"{RepositoryLayout.SolutionFile.Name}:{Environment.NewLine}" +
            string.Join(Environment.NewLine, strays.Select(s => $"  - {s}")) +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "A stale .sln lets `dotnet build` and an IDE disagree about which projects exist, " +
            "and a .slnf can exclude the architecture suite from a CI run entirely - which " +
            "does not fail this gate, it stops it running.");
    }

    private static IEnumerable<string> UnconditionalProperties(FileInfo file, string property) =>
        RepositoryLayout.LoadXml(file)
            .Descendants()
            .Where(e => e.Name.LocalName.Equals(property, StringComparison.Ordinal))
            .Where(IsUnconditional)
            .Select(e => e.Value.Trim());

    /// <summary>
    /// True when neither the property nor any ancestor carries a <c>Condition</c>.
    /// </summary>
    private static bool IsUnconditional(XElement element)
    {
        for (var current = element; current is not null; current = current.Parent)
        {
            if (current.Attribute("Condition") is not null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The import files other than the root <c>Directory.Build.props</c>, which is the one
    /// file that is supposed to state the framework pin.
    /// </summary>
    private static IEnumerable<FileInfo> OtherImportFiles() =>
        RepositoryLayout.MsBuildImportFiles.Where(f =>
            !f.FullName.Equals(RepositoryLayout.DirectoryBuildProps.FullName, StringComparison.OrdinalIgnoreCase));

    private static void AssertDoesNotReference(string projectName, params string[] forbidden)
    {
        var project = FindProject(projectName);
        var references = RepositoryLayout.DeclaredProjectReferences(project);
        var offending = forbidden.Where(f => references.Contains(f, StringComparer.Ordinal)).ToList();

        Assert.True(offending.Count == 0,
            $"{RepositoryLayout.RelativePath(project)}: '{projectName}' -> " +
            $"'{string.Join("', '", offending)}' violates the dependency rule. " +
            $"'{projectName}' may reference " +
            $"{string.Join(", ", AllowedReferenceEdges.Table[projectName])}.");
    }

    /// <summary>
    /// Locates a project by name, failing with the reason rather than with a LINQ exception.
    /// </summary>
    /// <remarks>
    /// <c>Single()</c> and <c>FirstOrDefault()</c> were both used here. The first throws
    /// "Sequence contains more than one matching element" when two project files share a name,
    /// which names neither the project nor the files; the second silently picks one of them.
    /// Both are failures a reader has to reverse-engineer.
    /// </remarks>
    private static FileInfo FindProject(string projectName)
    {
        var matches = RepositoryLayout.AllProjectFiles
            .Where(p => RepositoryLayout.ProjectName(p).Equals(projectName, StringComparison.Ordinal))
            .ToList();

        Assert.True(matches.Count == 1,
            matches.Count == 0
                ? $"No project file named '{projectName}.csproj' exists in the repository. " +
                  "The inventory gate in SolutionInventoryTests is the one that explains why."
                : $"{matches.Count} project files are named '{projectName}.csproj': " +
                  $"{string.Join(", ", matches.Select(RepositoryLayout.RelativePath))}. Every " +
                  "gate that looks a project up by name is ambiguous until that is resolved.");

        return matches[0];
    }

    private static string BuildFailureMessage(string headline, IEnumerable<string> violations) =>
        $"{headline}{Environment.NewLine}{Environment.NewLine}" +
        string.Join(Environment.NewLine, violations.Select(v => $"  - {v}"));
}
