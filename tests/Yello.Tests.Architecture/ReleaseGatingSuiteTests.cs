using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// AC5 - a single verified cross-Space disclosure fails the build, so the suites that gate on SM-1
/// and SM-2 ignore no exit code at all. Story 1.9 Task 11 (the local half; "on every push" is owed to
/// story 1.10's CI).
/// </summary>
/// <remarks>
/// <c>TestingConventionTests.Only_suites_with_no_tests_may_ignore_the_zero_test_exit_code</c> already
/// holds exit code 8 to the suite's contents. This holds the stronger property AC5 needs: no
/// <c>--ignore-exit-code</c> of any value, because <c>--ignore-exit-code 2</c> would make a failing
/// isolation case report success.
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-35")]
[Trait("Requirement", "NFR-1")]
public sealed class ReleaseGatingSuiteTests
{
    private static readonly string[] StrictSuites = ["Yello.Tests.Isolation", "Yello.Tests.Revocation"];

    /// <summary>
    /// 1.9-ARCH-004 - the Isolation and Revocation suites carry no <c>--ignore-exit-code</c>, read from
    /// evaluated MSBuild state rather than the project file's text.
    /// </summary>
    /// <remarks>
    /// <b>Active - green today, a standing gate.</b> Story 1.9's red-phase scaffolds are the suites'
    /// first cases, so the zero-test switch came off in the same change.
    /// </remarks>
    [Fact]
    public void The_isolation_and_revocation_suites_ignore_no_exit_code()
    {
        var projects = RepositoryLayout.AllProjectFiles
            .Where(project => StrictSuites.Contains(RepositoryLayout.ProjectName(project), StringComparer.Ordinal))
            .ToList();

        // Vacuity guard: both suites are found.
        Assert.Equal(StrictSuites.Order(StringComparer.Ordinal), projects.Select(RepositoryLayout.ProjectName).Order(StringComparer.Ordinal));

        var lenient = projects
            .Where(project => MsBuildEvaluation.Property(project, "TestingPlatformCommandLineArguments")
                .Contains("--ignore-exit-code", StringComparison.OrdinalIgnoreCase))
            .Select(RepositoryLayout.RelativePath)
            .ToList();

        Assert.True(
            lenient.Count == 0,
            "A release-gating suite ignores an exit code, so a failing isolation or revocation case could report success (AC5): "
            + string.Join(", ", lenient));
    }
}
