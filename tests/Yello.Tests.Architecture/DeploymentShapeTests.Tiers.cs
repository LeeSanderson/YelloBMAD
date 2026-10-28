using System.Text.RegularExpressions;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.9's CI stages (AC15, Task 11; <c>deferred-work.md:419</c>; <c>test-design-qa.md:402-435</c>): a Nightly and a
/// Weekly workflow, each running its own tier on a schedule against a local Testcontainers engine and never Azure (R13),
/// and a PR run that excludes both. Plus the tier trait itself: every <c>Tier</c> value is <c>Nightly</c> or
/// <c>Weekly</c>. Written by the story 2.9 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tier selection is story Q13 (a)</b>: a fifth trait name, <c>Tier</c>, valued <c>Nightly</c> or <c>Weekly</c>; the PR
/// run excludes both with <c>--filter-not-trait</c>; separate <c>nightly.yml</c> and <c>weekly.yml</c>. If Q13 is decided
/// otherwise, rewrite these cases to the decision and record it - never delete them. <c>TESTING-CONVENTIONS.md</c>'s
/// "Four names, and only these four" is amended in the same change (story Task 11).
/// </para>
/// <para>
/// <b>Gate that goes red by design</b>: <see cref="The_revision_is_promoted_only_after_the_migration_job_and_only_after_green_tests"/>
/// takes <c>Assert.Single</c> over the jobs that run <c>dotnet test Yello.slnx</c>; a scheduled workflow that runs the same
/// command makes it two. Either run the tier per project (<c>dotnet test --project …</c>) or narrow that gate to
/// <c>ci.yml</c> in the same change - and record which.
/// </para>
/// </remarks>
public sealed partial class DeploymentShapeTests
{
    private static readonly string[] Tiers = ["Nightly", "Weekly"];

    /// <summary>
    /// 2.9-ARCH-901 (AC15) - <c>nightly.yml</c> and <c>weekly.yml</c> each run on a <c>schedule</c> and on
    /// <c>workflow_dispatch</c>, select their own tier, run one test module at a time, and carry no Azure login, no secret
    /// and no deployment environment - a local engine only (R13).
    /// </summary>
    /// <param name="tier">The tier.</param>
    /// <param name="file">Its workflow file.</param>
    [Theory(Skip = "red: AC15 - no Nightly or Weekly workflow until story Task 11")]
    [InlineData("Nightly", "nightly.yml")]
    [InlineData("Weekly", "weekly.yml")]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "AR-35")]
    public void Each_scheduled_tier_runs_its_own_tests_against_a_local_engine_only(string tier, string file)
    {
        var workflow = File.ReadAllText(WorkflowFile(file).FullName);

        Assert.Matches(ScheduleTrigger, workflow);
        Assert.Contains("workflow_dispatch", workflow, StringComparison.Ordinal);
        Assert.Contains($"Tier={tier}", workflow, StringComparison.Ordinal);
        Assert.Contains("--max-parallel-test-modules 1", workflow, StringComparison.Ordinal);
        Assert.Matches(RequireVariable("YELLO_REQUIRE_CONTAINER_RUNTIME"), workflow);
        Assert.DoesNotContain("azure/login", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secrets.", workflow, StringComparison.Ordinal);
        Assert.DoesNotMatch(WorkflowEnvironment, workflow);
        Assert.DoesNotContain("az ", workflow, StringComparison.Ordinal);
    }

    /// <summary>
    /// 2.9-ARCH-902 (AC15) - the PR stage's <c>dotnet test Yello.slnx</c> excludes both tiers, so it still finishes under
    /// 15 minutes while the scale suites exist.
    /// </summary>
    [Fact(Skip = "red: AC15 - ci.yml excludes no tier until story Task 11")]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "AR-35")]
    public void The_PR_run_excludes_the_Nightly_and_Weekly_tiers()
    {
        var ci = File.ReadAllText(WorkflowFile("ci.yml").FullName);
        var everything = ci.Split('\n').Single(line => line.Contains("dotnet test Yello.slnx", StringComparison.Ordinal));

        Assert.All(Tiers, tier => Assert.Contains($"Tier={tier}", everything, StringComparison.Ordinal));
        Assert.Contains("--filter-not-trait", everything, StringComparison.Ordinal);
    }

    /// <summary>
    /// 2.9-ARCH-903 (AC15, Q13 (a)) - every <c>Tier</c> trait in the test sources is <c>Nightly</c> or <c>Weekly</c>, so the
    /// two filters select every tiered test and a misspelt tier cannot slip into the PR run. Green today (story 2.9's
    /// scaffolds are the only tiered tests); a standing gate.
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "AR-35")]
    public void Every_Tier_trait_is_Nightly_or_Weekly()
    {
        var values = RepositoryLayout.EnumerateSourceFiles("*.cs")
            .Where(file => RepositoryLayout.RelativePath(file).StartsWith("tests/", StringComparison.Ordinal))
            .SelectMany(file => TierTrait.Matches(File.ReadAllText(file.FullName)).Select(match => (File: RepositoryLayout.RelativePath(file), Value: match.Groups["tier"].Value)))
            .ToList();

        // Anchor: the scale suites carry the trait, so "every value is allowed" is about something.
        Assert.Contains(values, value => value.Value == "Nightly");
        Assert.Contains(values, value => value.Value == "Weekly");

        var problems = values.Where(value => !Tiers.Contains(value.Value, StringComparer.Ordinal)).Select(value => $"{value.File}: Tier '{value.Value}'").ToList();

        Assert.True(problems.Count == 0, "A Tier trait the Nightly and Weekly filters do not select:\n" + string.Join('\n', problems));
    }

    /// <summary>
    /// The detector behind <see cref="Every_Tier_trait_is_Nightly_or_Weekly"/>, fired on planted text.
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    public void The_Tier_trait_scan_reads_a_planted_value()
    {
        // Assembled, so this file's own text holds no Tier trait for the live scan to read.
        Assert.Equal("Nighlty", TierTrait.Match("[Trait(\"Ti" + "er\", \"Nighlty\")]").Groups["tier"].Value);
        Assert.Equal("Weekly", TierTrait.Match("[Trait( \"Ti" + "er\" ,\"Weekly\" )]").Groups["tier"].Value);
        Assert.DoesNotMatch(TierTrait, """[Trait("Suite", "Slices")]""");
    }

    [GeneratedRegex(@"^\s*schedule:\s*$[\s\S]*?cron:", RegexOptions.Multiline, 1000)]
    private static partial Regex ScheduleTrigger { get; }

    [GeneratedRegex("""Trait\s*\(\s*"Tier"\s*,\s*"(?<tier>[^"]*)"\s*\)""", RegexOptions.None, 1000)]
    private static partial Regex TierTrait { get; }
}
