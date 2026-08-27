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
    /// <remarks>
    /// <c>GlobalPackageReference</c> belongs here, and its omission was a live bypass:
    /// declared in <c>Directory.Build.props</c> it reaches every project in the solution, and
    /// it was read by neither this gate nor
    /// <c>PackageVersionPinTests.The_only_solution_wide_package_is_the_coding_standard</c>,
    /// which scoped itself to <c>Directory.Packages.props</c>. Demonstrated during review:
    /// <c>Microsoft.NET.Test.Sdk</c> placed that way applies its build assets to every project
    /// (<c>IsTestProject</c> and <c>GenerateProgramFile</c> both evaluate true in a project
    /// declaring neither), and the in-memory provider placed that way restores. The story's own
    /// plant used <c>Directory.Packages.props</c> - the one file that <i>was</i> read - which is
    /// why the route survived a pass that believed it had closed it.
    /// <para>
    /// <c>Directory.Packages.props</c> is exempted for <c>GlobalPackageReference</c> only: it
    /// is that item's sanctioned home, and the set declared there is asserted exactly.
    /// </para>
    /// </remarks>
    private static readonly string[] ReferenceItemTypes =
        ["ProjectReference", "PackageReference", "GlobalPackageReference"];

    /// <summary>
    /// Item types whose <c>Include</c> a gate in this repository reads literally, and which
    /// must therefore not hide behind an MSBuild property.
    /// </summary>
    private static readonly string[] GatedItemTypes =
        ["ProjectReference", "PackageReference", "GlobalPackageReference", "Compile"];

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

        // The root Directory.Build.props is excluded from the sweep above because it is the
        // pin's legitimate home - but that exclusion also covered its CONDITIONAL
        // declarations, which was a live bypass: AssertPinnedOnce counts only unconditional
        // ones, so a Condition-guarded <TargetFramework>net9.0</TargetFramework> in this file
        // was invisible to both halves. Verified during review with
        // `dotnet msbuild -getProperty:TargetFramework`, which returned net9.0 while the gate
        // reported one unconditional net10.0.
        problems.AddRange(ConditionalFrameworkDeclarations(RepositoryLayout.DirectoryBuildProps));

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
                "an XML comment does not count, because it does not reach the build. A " +
                "Condition-guarded value is not counted here either - but it is NOT harmless, " +
                "because a condition that evaluates true does reach the build and wins, so it " +
                "is reported separately by this same assertion.)");
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
    /// Framework properties declared inside a <c>Condition</c> in the one file permitted to
    /// state them. A condition that evaluates true reaches the build and overrides the
    /// unconditional value, so "declared exactly once, unconditionally" is only a pin if
    /// nothing conditional sits alongside it.
    /// </summary>
    private static IEnumerable<string> ConditionalFrameworkDeclarations(FileInfo file) =>
        from property in FrameworkProperties
        from element in RepositoryLayout.LoadXml(file).Descendants()
            .Where(e => e.Name.LocalName.Equals(property, StringComparison.Ordinal))
        where !RepositoryLayout.IsUnconditional(element)
        select $"{RepositoryLayout.RelativePath(file)} declares <{property}> inside a " +
               $"Condition, set to '{element.Value.Trim()}'. If that condition evaluates true " +
               "the build takes this value, not the unconditional one - so the file states two " +
               "framework pins and the gate would only see the other.";

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
            // Directory.Packages.props is GlobalPackageReference's sanctioned home; every
            // other import file, and every other item type, is a violation.
            var isPackagesProps = file.FullName.Equals(
                RepositoryLayout.DirectoryPackagesProps.FullName, StringComparison.OrdinalIgnoreCase);

            problems.AddRange(
                from item in ReferenceItemTypes
                where !(isPackagesProps && item.Equals("GlobalPackageReference", StringComparison.Ordinal))
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
            "References belong in the project that needs them. GlobalPackageReference is " +
            "included here and is checked in EVERY import file except Directory.Packages.props, " +
            "which is its one sanctioned home and whose set PackageVersionPinTests asserts " +
            "exactly. Scoping that check to the sanctioned file alone is what previously left " +
            "Directory.Build.props and Directory.Build.targets open.");
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

        // The trailing separator is load-bearing. Path.GetFullPath returns no trailing
        // separator, so a bare prefix comparison treats a SIBLING directory whose name extends
        // this one as being inside it: '..\Yello.Domain.Extras\Thing.cs' StartsWith
        // '...\Yello.Domain' is true. Verified during review. Yello.Application.Slices and
        // Yello.Host.Endpoints are the same shape, and each would move source across a ring
        // with no reference for Gate A and no cross-assembly dependency for Gate B.
        var projectDirectory = Path.GetFullPath(project.Directory.FullName)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

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
    /// Every ban in this suite compares the literal <c>Include</c> text, so an item hidden
    /// behind an MSBuild property is a ban that silently does not apply.
    /// </summary>
    /// <remarks>
    /// Demonstrated during review: <c>&lt;Orm&gt;Microsoft.EntityFrameworkCore&lt;/Orm&gt;</c>
    /// with <c>&lt;PackageReference Include="$(Orm)" /&gt;</c> restores EF Core while every
    /// gate here sees the string <c>$(Orm)</c>. That defeats the per-ring package ban, the
    /// VSTest ban and the row-level-security provider ban at once, and puts EF Core into
    /// <c>Yello.Application</c> declared-but-unused - the exact asymmetry Gate B cannot see and
    /// the reason the package ban exists. For <c>Compile</c> it is worse than a miss:
    /// <c>Path.Combine</c> treats <c>$(Shared)</c> as an ordinary directory name, so the path
    /// resolves <i>inside</i> the project and the escape check affirmatively passes.
    /// <para>
    /// The ring <b>edge</b> gate is unaffected either way - an unexpanded <c>$(X)</c> presents
    /// as an unauthorised edge, so it errs strict. This assertion covers the ones that err
    /// permissive.
    /// </para>
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-1")]
    public void No_gated_item_hides_behind_an_MSBuild_property()
    {
        var problems = new List<string>();

        foreach (var file in RepositoryLayout.AllProjectFiles.Concat(RepositoryLayout.MsBuildImportFiles))
        {
            problems.AddRange(RepositoryLayout.UnresolvableIncludes(file, GatedItemTypes)
                .Select(i => $"{RepositoryLayout.RelativePath(file)} declares {i}"));
        }

        Assert.True(problems.Count == 0, BuildFailureMessage(
            "An item this suite gates is declared through an MSBuild property, so the gate " +
            "compares the unexpanded text and the ban does not apply to what actually restores " +
            "or compiles.",
            problems));
    }

    /// <summary>
    /// Two further routes across a ring boundary that no gate here reads: a raw assembly
    /// reference, and an explicit <c>&lt;Import&gt;</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>&lt;Reference Include="Yello.Domain" HintPath="..\Yello.Domain\bin\...\Yello.Domain.dll" /&gt;</c>
    /// crosses a ring with no <c>ProjectReference</c> for Gate A to read, and if the types are
    /// never touched, no <c>AssemblyRef</c> for Gate B either - the same declared-but-unused
    /// asymmetry this story closed for project references and left open here. Framework and SDK
    /// assemblies arrive implicitly on this stack, so a raw <c>&lt;Reference&gt;</c> has no
    /// legitimate use in this repository and is banned outright rather than validated.
    /// </para>
    /// <para>
    /// <c>&lt;Import&gt;</c> is subject to neither
    /// <see cref="Exactly_one_of_each_MSBuild_import_file_governs_the_solution"/> nor
    /// <see cref="No_MSBuild_import_file_declares_a_project_or_package_reference"/>, both of
    /// which reason about the files MSBuild imports by directory position. It can carry a
    /// reference, a <c>GlobalPackageReference</c> or a framework property in from a file
    /// nothing reads, which would make every "declared fact" this suite asserts less than the
    /// facts the build uses.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_project_declares_a_raw_assembly_reference_or_an_explicit_import()
    {
        var problems = new List<string>();

        foreach (var file in RepositoryLayout.AllProjectFiles.Concat(RepositoryLayout.MsBuildImportFiles))
        {
            var path = RepositoryLayout.RelativePath(file);

            problems.AddRange(RepositoryLayout.ItemIncludes(file, "Reference")
                .Select(r => $"{path} declares a raw <Reference> to '{r}'. Use a " +
                             "ProjectReference, which the ring gate reads, or a PackageReference."));

            problems.AddRange(RepositoryLayout.DeclaredImports(file)
                .Select(i => $"{path} explicitly imports '{i}', which no gate in this suite " +
                             "reads. Anything that file declares is invisible to Gate A."));
        }

        Assert.True(problems.Count == 0, BuildFailureMessage(
            "A build file reaches outside what this suite can read.",
            problems));
    }

    /// <summary>
    /// A <c>ProjectReference</c> has to point at a project in this repository, because the
    /// edge table is keyed by project <i>name</i> and a name is not a location.
    /// </summary>
    /// <remarks>
    /// <c>RepositoryLayout.DeclaredProjectReferences</c> reduces an <c>Include</c> to its file
    /// name, which is right for comparing an edge against the table and wrong for establishing
    /// that the edge points anywhere in particular:
    /// <c>..\..\vendored\Yello.Domain\Yello.Domain.csproj</c>, or a path under any of the
    /// excluded non-source trees, satisfies the permitted <c>Yello.Domain</c> edge while
    /// compiling against something nothing in this suite has examined.
    /// </remarks>
    [Fact]
    public void Every_declared_project_reference_resolves_to_a_project_in_this_repository()
    {
        var known = RepositoryLayout.AllProjectFiles
            .Select(p => p.FullName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var problems = new List<string>();

        foreach (var project in RepositoryLayout.AllProjectFiles)
        {
            problems.AddRange(
                from reference in RepositoryLayout.ResolvedProjectReferences(project)
                where !known.Contains(reference.ResolvedPath)
                select $"{RepositoryLayout.RelativePath(project)} references '{reference.Include}', " +
                       $"which resolves to '{reference.ResolvedPath}' - not a project in this " +
                       "repository's source tree.");
        }

        Assert.True(problems.Count == 0, BuildFailureMessage(
            "A ProjectReference points outside the source tree, so its name satisfies the edge " +
            "table while the code it compiles against is unexamined.",
            problems));
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
        // Not a tautology, though it looks like one. Root is defined as the directory holding
        // Yello.slnx ONLY on the upward-walk path; the YELLO_REPOSITORY_ROOT escape hatch sets
        // it from an environment variable with no such guarantee, and on that path this check
        // is the only thing standing between "no solution file" and a gate reporting green
        // over a directory that is not this repository.
        Assert.True(RepositoryLayout.SolutionFile.Exists,
            $"{RepositoryLayout.SolutionFile.Name} does not exist at " +
            $"'{RepositoryLayout.Root.FullName}'. Gate A reads the solution to learn which " +
            "projects exist, so without it the inventory assertions have nothing to compare " +
            "against. If the root came from YELLO_REPOSITORY_ROOT, it is pointing at the wrong " +
            "directory.");

        // Routed through EnumerateSourceFiles so ExcludedDirectories applies. Doing its own
        // walk here bypassed that list - whose own documentation warns that a file arriving
        // under .claude with a skill update "would fail a build nobody had changed" - and the
        // ad-hoc /bin/ and /obj/ filter it used instead covered two of the nine entries.
        // The Extension filter stays: on Windows a three-character extension in a search
        // pattern also matches longer ones, so "*.sln" matches Yello.slnx.
        var strays = SolutionFilePatterns
            .SelectMany(RepositoryLayout.EnumerateSourceFiles)
            .Where(f => f.Extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                || f.Extension.Equals(".slnf", StringComparison.OrdinalIgnoreCase))
            .Select(RepositoryLayout.RelativePath)
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
            .Where(RepositoryLayout.IsUnconditional)
            .Select(e => e.Value.Trim());

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
