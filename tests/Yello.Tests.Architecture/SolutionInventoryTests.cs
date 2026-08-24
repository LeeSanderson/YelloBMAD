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
            $"The solution's project inventory does not match the Structural Seed." +
            $"{Environment.NewLine}  missing:    " +
            $"{(missing.Count == 0 ? "(none)" : string.Join(", ", missing))}" +
            $"{Environment.NewLine}  unexpected: " +
            $"{(unexpected.Count == 0 ? "(none)" : string.Join(", ", unexpected))}" +
            $"{Environment.NewLine}{Environment.NewLine}" +
            $"The expected set is the eight production projects, the five test projects, and " +
            $"exactly one declared variance ({AllowedReferenceEdges.DeclaredVariance}, which " +
            $"hosts the shared SQL Server fixture). Adding a project means adding a row to " +
            $"AllowedReferenceEdges.Table and justifying it against AC1.");
    }

    [Fact]
    public void Every_project_in_the_solution_file_exists_on_disk_and_vice_versa()
    {
        var solutionText = File.ReadAllText(RepositoryLayout.SolutionFile.FullName);

        var missingFromSolution = RepositoryLayout.AllProjectFiles
            .Where(p => !solutionText.Contains(p.Name, StringComparison.OrdinalIgnoreCase))
            .Select(RepositoryLayout.RelativePath)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(missingFromSolution.Count == 0,
            $"These project files exist on disk but are not in Yello.sln, so `dotnet build` " +
            $"and `dotnet test` over the solution would silently skip them - including any gate " +
            $"they contain:{Environment.NewLine}" +
            string.Join(Environment.NewLine, missingFromSolution.Select(p => $"  - {p}")));
    }

    /// <summary>
    /// The five test projects sit under <c>tests/</c>; the eight production projects sit at
    /// the repository root. There is no <c>src/</c> - that is the Structural Seed's layout,
    /// reproduced literally.
    /// </summary>
    [Fact]
    public void Production_projects_sit_at_the_repository_root_and_test_projects_under_tests()
    {
        var misplaced = new List<string>();

        foreach (var project in RepositoryLayout.AllProjectFiles)
        {
            var name = RepositoryLayout.ProjectName(project);
            var underTests = RepositoryLayout.IsUnderTestsDirectory(project);
            var shouldBeUnderTests = name.StartsWith("Yello.Tests.", StringComparison.Ordinal);

            if (underTests != shouldBeUnderTests)
            {
                misplaced.Add(
                    $"{RepositoryLayout.RelativePath(project)}: expected it " +
                    $"{(shouldBeUnderTests ? "under tests/" : "at the repository root")}.");
            }
        }

        Assert.True(misplaced.Count == 0,
            $"The source tree does not match the Structural Seed (production projects at the " +
            $"root, test projects under tests/, no src/):{Environment.NewLine}" +
            string.Join(Environment.NewLine, misplaced.Select(m => $"  - {m}")));
    }
}
