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
            var name = RepositoryLayout.ProjectName(project);
            var path = RepositoryLayout.RelativePath(project);

            if (!AllowedReferenceEdges.Table.TryGetValue(name, out var permitted))
            {
                violations.Add(
                    $"{path}: project '{name}' has no row in the allowed-edge table, so it has " +
                    $"no agreed ring position. Add a row to AllowedReferenceEdges.Table, or " +
                    $"remove the project.");
                continue;
            }

            var declared = RepositoryLayout.DeclaredProjectReferences(project);

            foreach (var unauthorised in declared.Except(permitted, StringComparer.Ordinal)
                         .OrderBy(n => n, StringComparer.Ordinal))
            {
                violations.Add(
                    $"{path}: '{name}' -> '{unauthorised}' is NOT PERMITTED. '{name}' may " +
                    $"reference {(permitted.Length == 0 ? "NOTHING" : string.Join(", ", permitted))}.");
            }

            foreach (var missing in permitted.Except(declared, StringComparer.Ordinal)
                         .OrderBy(n => n, StringComparer.Ordinal))
            {
                violations.Add(
                    $"{path}: '{name}' -> '{missing}' is MISSING. The table requires this edge, " +
                    $"so either restore the ProjectReference or change the table.");
            }
        }

        Assert.True(violations.Count == 0, BuildFailureMessage(
            "The declared project references do not match the dependency rule (AD-21 / AR-2).",
            violations));
    }

    [Fact]
    public void Yello_Domain_declares_no_project_reference_at_all()
    {
        var domain = FindProject("Yello.Domain");
        var references = RepositoryLayout.DeclaredProjectReferences(domain);

        Assert.True(references.Count == 0,
            $"Yello.Domain is the innermost ring and must reference NOTHING, but " +
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
    /// SDK happens to be installed, which is what RuntimeFrameworkVersion makes it.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-1")]
    public void The_runtime_framework_version_is_pinned_to_the_AR1_patch()
    {
        var props = File.ReadAllText(RepositoryLayout.DirectoryBuildProps.FullName);

        Assert.Contains("<RuntimeFrameworkVersion>10.0.11</RuntimeFrameworkVersion>", props,
            StringComparison.Ordinal);
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
    /// </summary>
    [Fact]
    public void The_solution_file_is_slnx_format_and_is_the_only_one()
    {
        Assert.True(RepositoryLayout.SolutionFile.Exists,
            $"Expected an XML-format solution at " +
            $"{RepositoryLayout.RelativePath(RepositoryLayout.SolutionFile)}.");

        // The pattern has to be filtered, not trusted: on Windows a three-character extension
        // in a search pattern also matches longer ones, so "*.sln" alone would match the .slnx.
        var classic = RepositoryLayout.Root
            .EnumerateFiles("*.sln")
            .Where(f => f.Extension.Equals(".sln", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(classic.Count == 0,
            $"Found a classic-format solution file ({string.Join(", ", classic.Select(f => f.Name))}) " +
            $"beside {RepositoryLayout.SolutionFile.Name}. Two solution files can disagree about " +
            $"the project inventory, which is the fact Gate A reads - delete the .sln.");
    }

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

    private static FileInfo FindProject(string projectName)
    {
        var match = RepositoryLayout.AllProjectFiles
            .FirstOrDefault(p => RepositoryLayout.ProjectName(p).Equals(projectName, StringComparison.Ordinal));

        Assert.NotNull(match);
        return match;
    }

    private static string BuildFailureMessage(string headline, IEnumerable<string> violations) =>
        $"{headline}{Environment.NewLine}{Environment.NewLine}" +
        string.Join(Environment.NewLine, violations.Select(v => $"  - {v}"));
}
