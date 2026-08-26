using Xunit;

namespace Yello.Tests.Architecture;


/// <summary>
/// Gate A - AC1's "the solution exists, in the exact shape". A missing or renamed project
/// fails the build.
/// </summary>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-1")]
public sealed class SolutionInventoryTests
{
    [Fact]
    public void The_solution_contains_exactly_the_thirteen_named_projects_and_the_one_declared_variance()
    {
        var actual = RepositoryLayout.AllProjectFiles
            .Select(RepositoryLayout.ProjectName)
            .ToHashSet(StringComparer.Ordinal);

        var expected = AllowedReferenceEdges.ExpectedProjects.ToHashSet(StringComparer.Ordinal);

        var missing = expected.Except(actual, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var unexpected = actual.Except(expected, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.True(missing.Count == 0 && unexpected.Count == 0,
            "The solution's project inventory does not match the Structural Seed." +
            $"{Environment.NewLine}  missing:    " +
            $"{(missing.Count == 0 ? "(none)" : string.Join(", ", missing))}" +
            $"{Environment.NewLine}  unexpected: " +
            $"{(unexpected.Count == 0 ? "(none)" : string.Join(", ", unexpected))}" +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "The expected set is the eight production projects, the five test projects, and " +
            $"exactly one declared variance ({AllowedReferenceEdges.DeclaredVariance}, which " +
            "hosts the shared SQL Server fixture). Adding a project means adding a row to " +
            "AllowedReferenceEdges.Table and justifying it against AC1.");
    }

    /// <summary>
    /// Two project files with the same name would make every gate that resolves a project by
    /// name ambiguous, and the inventory set above would silently collapse them into one.
    /// </summary>
    [Fact]
    public void No_two_project_files_share_a_name()
    {
        var duplicates = RepositoryLayout.AllProjectFiles
            .GroupBy(RepositoryLayout.ProjectName, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(RepositoryLayout.RelativePath))}")
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToList();

        Assert.True(duplicates.Count == 0,
            "Two project files share a name. The inventory set is keyed by name, so it reports " +
            "them as one, and every gate that looks a project up by name resolves to whichever " +
            $"was enumerated first:{Environment.NewLine}" +
            string.Join(Environment.NewLine, duplicates.Select(d => $"  - {d}")));
    }

    /// <summary>
    /// Solution membership, both directions, from the parsed <c>.slnx</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This was a substring match on the solution's raw text, and the bypass was demonstrated
    /// during review rather than argued: wrapping the <c>Yello.Tests.Isolation</c> entry in an
    /// XML comment made <c>dotnet sln list</c> return thirteen projects while
    /// <c>solutionText.Contains(name)</c> still matched, so the assertion passed while a
    /// release-gating suite had been dropped from the build. Parsing the file is the fix:
    /// comments do not survive it.
    /// </para>
    /// <para>
    /// The reverse direction was named in the old test - "and vice versa" - and never
    /// implemented. An entry pointing at a project that no longer exists breaks the build for
    /// a different and more obvious reason, but the gate should say which one it is.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_solution_file_and_the_disk_agree_about_which_projects_exist()
    {
        var declared = RepositoryLayout.SolutionProjectPaths().ToHashSet(StringComparer.OrdinalIgnoreCase);

        var onDisk = RepositoryLayout.AllProjectFiles
            .Select(p => Path.GetFullPath(p.FullName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var notInSolution = onDisk.Except(declared, StringComparer.OrdinalIgnoreCase)
            .Select(ToRelative).OrderBy(p => p, StringComparer.Ordinal).ToList();

        var notOnDisk = declared.Except(onDisk, StringComparer.OrdinalIgnoreCase)
            .Select(ToRelative).OrderBy(p => p, StringComparer.Ordinal).ToList();

        Assert.True(notInSolution.Count == 0 && notOnDisk.Count == 0,
            $"{RepositoryLayout.SolutionFile.Name} and the source tree disagree." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            $"  on disk but not in the solution:{Environment.NewLine}" +
            Format(notInSolution) +
            $"{Environment.NewLine}  in the solution but not on disk:{Environment.NewLine}" +
            Format(notOnDisk) +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "A project missing from the solution is silently skipped by `dotnet build` and " +
            "`dotnet test` over the solution - including any gate it contains.");
    }

    /// <summary>
    /// The five test projects sit under <c>tests/</c>; the eight production projects sit at
    /// the repository root. There is no <c>src/</c> - that is the Structural Seed's layout,
    /// reproduced literally.
    /// </summary>
    /// <remarks>
    /// The depth check is the half that was missing. The only condition used to be
    /// <c>underTests != shouldBeUnderTests</c>, so moving all eight production projects into a
    /// <c>src/</c> directory left both sides false and recorded no violation - while this
    /// method's own summary claimed to enforce "there is no src/".
    /// </remarks>
    [Fact]
    public void Production_projects_sit_at_the_repository_root_and_test_projects_under_tests()
    {
        var misplaced = new List<string>();

        foreach (var project in RepositoryLayout.AllProjectFiles)
        {
            misplaced.AddRange(Misplacement(project));
        }

        Assert.True(misplaced.Count == 0,
            "The source tree does not match the Structural Seed (production projects at the " +
            $"root, test projects under tests/, no src/):{Environment.NewLine}" +
            string.Join(Environment.NewLine, misplaced.Select(m => $"  - {m}")));
    }

    /// <summary>
    /// The bytecode gates and the project-file gates must be looking at the same eight
    /// assemblies.
    /// </summary>
    /// <remarks>
    /// <see cref="ProductionAssemblies"/> and
    /// <see cref="AllowedReferenceEdges.ProductionProjects"/> were two hand-maintained
    /// eight-element lists that nothing reconciled. Adding a ninth production project forces
    /// the <c>ProductionProjects</c> and <c>Table</c> edits - the inventory and ring gates see
    /// to that - and forces nothing at all for <c>ProductionAssemblies.All</c>. Gate B's ring
    /// rules and all four of Gate C's bans would then silently never examine the new
    /// assembly: the Role-API ban would stop covering new production code, in the story that
    /// exists to prevent exactly that.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-4")]
    public void The_loaded_production_assemblies_are_exactly_the_production_projects()
    {
        var loaded = ProductionAssemblies.All
            .Select(a => a.GetName().Name ?? "(unnamed)")
            .ToHashSet(StringComparer.Ordinal);

        var expected = AllowedReferenceEdges.ProductionProjects.ToHashSet(StringComparer.Ordinal);

        var notLoaded = expected.Except(loaded, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var notExpected = loaded.Except(expected, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.True(notLoaded.Count == 0 && notExpected.Count == 0,
            "ProductionAssemblies.All and AllowedReferenceEdges.ProductionProjects disagree, so " +
            "the bytecode gates and the project-file gates are asserting over different " +
            $"solutions.{Environment.NewLine}" +
            $"  in the ring table but not loaded: {Format(notLoaded, inline: true)}" +
            $"{Environment.NewLine}" +
            $"  loaded but not in the ring table: {Format(notExpected, inline: true)}" +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "A production project the bytecode gates do not load is one Gate B's ring rules " +
            "and all four of Gate C's bans never examine.");
    }

    private static IEnumerable<string> Misplacement(FileInfo project)
    {
        var name = RepositoryLayout.ProjectName(project);
        var path = RepositoryLayout.RelativePath(project);
        var underTests = RepositoryLayout.IsUnderTestsDirectory(project);
        var shouldBeUnderTests = name.StartsWith("Yello.Tests.", StringComparison.Ordinal);

        if (underTests != shouldBeUnderTests)
        {
            yield return
                $"{path}: expected it {(shouldBeUnderTests ? "under tests/" : "at the repository root")}.";
            yield break;
        }

        // Yello.Domain/Yello.Domain.csproj is depth 1; tests/Yello.Tests.Merge/....csproj is
        // depth 2. Anything deeper means a directory the Structural Seed does not have -
        // src/Yello.Domain/... being the case the summary above claims to forbid.
        var expectedDepth = shouldBeUnderTests ? 2 : 1;
        var actualDepth = RepositoryLayout.DepthBelowRoot(project);

        if (actualDepth != expectedDepth)
        {
            yield return
                $"{path}: sits {actualDepth} directories below the repository root, expected " +
                $"{expectedDepth}. The Structural Seed puts production projects directly at the " +
                "root and test projects directly under tests/. There is no src/.";
        }
    }

    private static string ToRelative(string fullPath) =>
        Path.GetRelativePath(RepositoryLayout.Root.FullName, fullPath).Replace('\\', '/');

    private static string Format(IReadOnlyCollection<string> items, bool inline = false)
    {
        if (items.Count == 0)
        {
            return inline ? "(none)" : "    (none)";
        }

        return inline
            ? string.Join(", ", items)
            : string.Join(Environment.NewLine, items.Select(i => $"    - {i}"));
    }
}
