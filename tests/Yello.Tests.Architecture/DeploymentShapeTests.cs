using System.Text.RegularExpressions;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// The deployment's invariants, gated at build from the files that define it: the IaC under
/// <c>infra/</c>, the workflows under <c>.github/workflows/</c>, the static host's configuration and
/// the AppHost. Story 1.10 AC1, AC4, AC5, AC6, AC10, AC11, AC13, AC15; Tasks 3, 6, 7, 9, 10, 11; Q16-Q18.
/// </summary>
/// <remarks>
/// <para>
/// <b>Each case reads the file that defines the invariant</b> - <c>infra/**/*.bicep</c>,
/// <c>.github/workflows/*.yml</c>, <c>staticwebapp.config.json</c>, the AppHost - and each was planted
/// once (story 1.10 Debug Log).
/// </para>
/// <para>
/// <b>Text gates with anchors, by decision</b> (Q18 (a)): a new YAML or Bicep parser package would be a
/// pin edit for a test. Each case first finds the construct it is about (the database resource, the
/// metric alert, the job that migrates) and only then asserts, so a renamed or missing file fails
/// rather than passing. If text proves too brittle, record that in the Debug Log and add
/// <c>YamlDotNet</c> as a labelled non-AR-1 test pin (story Task 10).
/// </para>
/// <para>
/// <b>What none of this proves</b>: that Azure honours any of it. Ingress forwarding, the Key Vault
/// references resolving, the job's execution, the alert firing and auto-pause are operator
/// observations after the first deploy (P1-P6), listed in <c>docs/operations.md</c> §11.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-38")]
public sealed partial class DeploymentShapeTests
{
    /// <summary>
    /// 1.10-ARCH-007 - exactly one deployment environment. The workflows name the GitHub environment
    /// <c>azure</c> and no other; nothing under <c>infra/</c> or <c>.github/</c> names a second target.
    /// </summary>
    /// <remarks>
    /// <c>Development</c>, as the ASP.NET environment name of the Local environment, is allowed and is
    /// not a deployment target. Plant (story Task 11): a second job with <c>environment: staging</c>.
    /// </remarks>
    [Fact]
    public void There_is_exactly_one_deployment_environment()
    {
        var workflows = WorkflowText();
        var infra = BicepText();

        var environments = WorkflowEnvironment.Matches(workflows)
            .Select(match => match.Groups["env"].Value.Trim('\'', '"'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.Equal(["azure"], environments);

        var named = SecondEnvironmentWord.Matches(workflows + "\n" + infra)
            .Select(match => match.Value)
            .Concat(QuotedEnvironmentLiteral.Matches(infra).Select(match => match.Value))
            .ToList();

        Assert.True(
            named.Count == 0,
            "A second deployment environment would consume the free grants that make production free (AC4): "
            + string.Join(", ", named));
    }

    /// <summary>
    /// 1.10-ARCH-008 - the Container App runs at most one replica (AD-14), in single revision mode.
    /// </summary>
    /// <remarks>Plant: <c>maxReplicas: 2</c>.</remarks>
    [Fact]
    [Trait("Requirement", "AD-14")]
    public void The_container_app_runs_at_most_one_replica_in_single_revision_mode()
    {
        var bicep = BicepText();

        Assert.Contains("Microsoft.App/containerApps@", bicep, StringComparison.Ordinal);

        var maxima = MaxReplicas.Matches(bicep).Select(match => match.Groups["n"].Value).ToList();

        Assert.NotEmpty(maxima);
        Assert.All(maxima, value => Assert.Equal("1", value));
        Assert.Matches(SingleRevisionMode, bicep);
    }

    /// <summary>
    /// 1.10-ARCH-009 - the database is on the free offer and pauses rather than bills when the free
    /// limit is reached, and the IaC sets no database-level collation (AD-15 sets it per column).
    /// </summary>
    /// <remarks>Plant: <c>freeLimitExhaustionBehavior: 'BillOverUsage'</c>.</remarks>
    [Fact]
    [Trait("Requirement", "AR-39")]
    public void The_database_pauses_at_the_free_limit_rather_than_billing_overage()
    {
        var bicep = BicepText();

        Assert.Contains("Microsoft.Sql/servers/databases@", bicep, StringComparison.Ordinal);
        Assert.Matches(UseFreeLimit, bicep);

        var behaviours = ExhaustionBehaviour.Matches(bicep).Select(match => match.Groups["b"].Value).ToList();

        Assert.NotEmpty(behaviours);
        Assert.All(behaviours, behaviour => Assert.Equal("AutoPause", behaviour));
        Assert.DoesNotMatch(Collation, bicep);
    }

    /// <summary>
    /// 1.10-ARCH-010 - a metric alert fires when the free amount remaining reaches 10% of the monthly
    /// 100,000 vCore-second allowance.
    /// </summary>
    /// <remarks>
    /// The metric's exact name and unit are to be confirmed against the Azure SQL free-offer page when
    /// the Bicep is written (story Task 6). If the unit is not vCore-seconds, correct the threshold
    /// literal here to the same 10% and record the correction in the Debug Log.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-39")]
    public void A_metric_alert_fires_at_ten_percent_of_the_free_allowance()
    {
        var bicep = BicepText();

        Assert.Contains("Microsoft.Insights/metricAlerts@", bicep, StringComparison.Ordinal);
        Assert.Matches(FreeAmountRemainingMetric, bicep);
        Assert.Matches(TenPercentThreshold, bicep);
        Assert.Contains("LessThanOrEqual", bicep, StringComparison.Ordinal);
    }

    /// <summary>
    /// 1.10-ARCH-011 - the ingress the Host sits behind: https only, port 8080, both probes, and
    /// forwarded headers switched on by configuration.
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "AR-33")]
    [Trait("Requirement", "AD-7")]
    public void The_ingress_is_https_only_on_8080_with_both_probes_and_forwarding_on()
    {
        var bicep = BicepText();

        Assert.Matches(AllowInsecureFalse, bicep);
        Assert.Matches(TargetPort8080, bicep);
        Assert.Contains("/health/live", bicep, StringComparison.Ordinal);
        Assert.Contains("/health/ready", bicep, StringComparison.Ordinal);
        Assert.Contains("'Liveness'", bicep, StringComparison.Ordinal);
        Assert.Contains("'Readiness'", bicep, StringComparison.Ordinal);
        Assert.Contains("ForwardedHeaders__Enabled", bicep, StringComparison.Ordinal);
    }

    /// <summary>
    /// 1.10-ARCH-012 - CI runs on every push and pull request to <c>main</c>; the architecture suite
    /// runs before the whole solution; and a CI agent requires both the container runtime and the
    /// migration bundle, so neither SM-1 nor the bundle test can pass by skipping.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-35")]
    [Trait("Requirement", "NFR-1")]
    public void Ci_runs_the_architecture_suite_first_then_everything_with_the_runtime_required()
    {
        var ci = File.ReadAllText(WorkflowFile("ci.yml").FullName);

        Assert.Matches(PushToMain, ci);
        Assert.Matches(PullRequestToMain, ci);

        var lines = ci.Split('\n');
        var architecture = Array.FindIndex(lines, line => line.Contains("dotnet test", StringComparison.Ordinal) && line.Contains("Yello.Tests.Architecture", StringComparison.Ordinal));
        var everything = Array.FindIndex(lines, line => line.Contains("dotnet test Yello.slnx", StringComparison.Ordinal));

        Assert.True(architecture >= 0, "No step runs the architecture suite on its own.");
        Assert.True(everything >= 0, "No step runs `dotnet test Yello.slnx`.");
        Assert.True(architecture < everything, "The architecture suite must run first (TESTING-CONVENTIONS.md, CI tiering).");

        Assert.Matches(RequireVariable("YELLO_REQUIRE_CONTAINER_RUNTIME"), ci);
        Assert.Matches(RequireVariable("YELLO_REQUIRE_MIGRATION_BUNDLE"), ci);
    }

    /// <summary>
    /// 1.10-ARCH-013 - migrations run before the revision is promoted, and nothing deploys after a red
    /// run: every job that updates the Container App's image transitively needs the job that starts the
    /// migration job, and every job that deploys transitively needs the job that runs the tests.
    /// </summary>
    /// <remarks>
    /// Reads <c>needs:</c> across every workflow's jobs. If Q10's recommendation is not taken and the
    /// deploy is a separate <c>workflow_run</c> workflow, the second half of this gate must be corrected
    /// to read that trigger instead, and the correction recorded. Plant (story Task 13): drop
    /// <c>needs: migrate</c> from the promote job.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-36")]
    public void The_revision_is_promoted_only_after_the_migration_job_and_only_after_green_tests()
    {
        var jobs = Jobs(WorkflowText());

        var migrate = Assert.Single(jobs, job => job.Body.Contains("az containerapp job start", StringComparison.Ordinal));
        var test = Assert.Single(jobs, job => job.Body.Contains("dotnet test Yello.slnx", StringComparison.Ordinal));
        var promotes = jobs.Where(job => ContainerAppImageUpdate.IsMatch(job.Body)).ToList();
        var deploys = jobs.Where(job => job.Body.Contains("az ", StringComparison.Ordinal) || job.Body.Contains("static-web-apps-deploy", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(promotes);

        Assert.All(promotes, promote => Assert.Contains(migrate.Name, Needed(promote.Name, jobs)));
        Assert.All(deploys, deploy => Assert.Contains(test.Name, Needed(deploy.Name, jobs)));
    }

    /// <summary>
    /// 1.10-ARCH-014 - every third-party action is pinned to a full commit SHA.
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    public void Every_third_party_action_is_pinned_to_a_commit_sha()
    {
        var uses = ActionUse.Matches(WorkflowText())
            .Where(match => !match.Groups["action"].Value.StartsWith("./", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(uses);

        var unpinned = uses
            .Where(match => !CommitSha.IsMatch(match.Groups["ref"].Value))
            .Select(match => match.Value.Trim())
            .ToList();

        Assert.True(unpinned.Count == 0, "Pin every action to a full commit SHA, with the version in a trailing comment:\n" + string.Join('\n', unpinned));
    }

    /// <summary>
    /// 1.10-ARCH-015 - the static host serves <c>index.html</c> for every client route, never for the
    /// framework's own files, and no production client configuration is committed (the deploy writes
    /// it into the publish output from the IaC's output).
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-7")]
    public void The_static_host_falls_back_to_index_html_for_client_routes()
    {
        var wwwroot = Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client", "wwwroot");
        var config = new FileInfo(Path.Combine(wwwroot, "staticwebapp.config.json"));

        Assert.True(config.Exists, "Yello.Client/wwwroot/staticwebapp.config.json does not exist.");

        using var document = RepositoryLayout.LoadJson(config);
        var fallback = document.RootElement.GetProperty("navigationFallback");

        Assert.Equal("/index.html", fallback.GetProperty("rewrite").GetString());
        Assert.Contains(
            "/_framework/*",
            fallback.GetProperty("exclude").EnumerateArray().Select(item => item.GetString()),
            StringComparer.Ordinal);

        Assert.False(
            File.Exists(Path.Combine(wwwroot, "appsettings.Production.json")),
            "appsettings.Production.json is generated into the publish output at deploy, never committed (Task 7).");
    }

    /// <summary>
    /// 1.10-ARCH-016 - under <c>aspire run</c> the migration runs as its own resource and the Host
    /// waits for it to complete, so the Local environment is migrated the way Azure is (Q17 (a)).
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "AR-36")]
    public void The_apphost_runs_the_migration_before_the_host()
    {
        var appHost = File.ReadAllText(Path.Combine(RepositoryLayout.Root.FullName, "Yello.AppHost", "Program.cs"));

        // Anchor: the database the migration targets.
        Assert.Contains("AddDatabase(", appHost, StringComparison.Ordinal);
        Assert.Contains("WaitForCompletion(", appHost, StringComparison.Ordinal);
    }

    private static string BicepText()
    {
        var infra = new DirectoryInfo(Path.Combine(RepositoryLayout.Root.FullName, "infra"));

        Assert.True(infra.Exists, "infra/ does not exist (story 1.10 Task 6).");

        var files = infra.EnumerateFiles("*.bicep", SearchOption.AllDirectories).ToList();

        Assert.Contains(files, file => file.Name.Equals("main.bicep", StringComparison.Ordinal));

        return string.Join('\n', files.Select(file => File.ReadAllText(file.FullName)));
    }

    private static FileInfo WorkflowFile(string name)
    {
        var file = new FileInfo(Path.Combine(RepositoryLayout.Root.FullName, ".github", "workflows", name));

        Assert.True(file.Exists, $".github/workflows/{name} does not exist (story 1.10 Task 9).");

        return file;
    }

    private static string WorkflowText()
    {
        var directory = WorkflowFile("ci.yml").Directory!;

        return string.Join(
            '\n',
            directory.EnumerateFiles("*.yml").Concat(directory.EnumerateFiles("*.yaml"))
                .OrderBy(file => file.Name, StringComparer.Ordinal)
                .Select(file => File.ReadAllText(file.FullName)));
    }

    /// <summary>
    /// Every job in the workflows: its name, its <c>needs</c>, and its whole body as text. A job is a
    /// key indented by two spaces under a top-level <c>jobs:</c>.
    /// </summary>
    private static List<Job> Jobs(string workflows)
    {
        var jobs = new List<Job>();
        var inJobs = false;
        string? name = null;
        var body = new List<string>();

        void Close()
        {
            if (name is not null)
            {
                var text = string.Join('\n', body);
                var needs = NeedsOf(text);
                jobs.Add(new Job(name, needs, text));
            }

            name = null;
            body.Clear();
        }

        foreach (var raw in workflows.Split('\n'))
        {
            var line = raw.TrimEnd('\r');

            if (line.Length > 0 && !char.IsWhiteSpace(line[0]))
            {
                Close();
                inJobs = line.StartsWith("jobs:", StringComparison.Ordinal);
                continue;
            }

            if (!inJobs)
            {
                continue;
            }

            var job = JobKey.Match(line);

            if (job.Success)
            {
                Close();
                name = job.Groups["job"].Value;
                continue;
            }

            body.Add(line);
        }

        Close();

        return jobs;
    }

    private static List<string> NeedsOf(string jobBody)
    {
        var inline = NeedsInline.Match(jobBody);

        if (inline.Success)
        {
            return [.. inline.Groups["list"].Value.Trim('[', ']', ' ')
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(item => item.Trim('\'', '"'))];
        }

        var block = NeedsBlock.Match(jobBody);

        return block.Success
            ? [.. block.Groups["item"].Captures.Select(capture => capture.Value.Trim().Trim('\'', '"'))]
            : [];
    }

    /// <summary>
    /// Every job <paramref name="job"/> needs, directly or through another job.
    /// </summary>
    private static SortedSet<string> Needed(string job, List<Job> jobs)
    {
        var needed = new SortedSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>([job]);

        while (pending.TryPop(out var current))
        {
            foreach (var need in jobs.Where(candidate => candidate.Name.Equals(current, StringComparison.Ordinal)).SelectMany(candidate => candidate.Needs))
            {
                if (needed.Add(need))
                {
                    pending.Push(need);
                }
            }
        }

        return needed;
    }

    private static Regex RequireVariable(string name) =>
        new($@"{Regex.Escape(name)}:\s*['""]?1['""]?", RegexOptions.None, TimeSpan.FromSeconds(1));

    [GeneratedRegex(@"environment:\s*(?:\r?\n\s+name:\s*)?(?<env>['""]?[A-Za-z][A-Za-z0-9_-]*['""]?)", RegexOptions.None, 1000)]
    private static partial Regex WorkflowEnvironment { get; }

    [GeneratedRegex(@"\b(?:staging|preprod|pre-prod|uat)\b", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex SecondEnvironmentWord { get; }

    [GeneratedRegex(@"'(?:stage|staging|dev|uat|preprod)'", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex QuotedEnvironmentLiteral { get; }

    [GeneratedRegex(@"maxReplicas:\s*(?<n>\d+)", RegexOptions.None, 1000)]
    private static partial Regex MaxReplicas { get; }

    [GeneratedRegex(@"activeRevisionsMode:\s*'Single'", RegexOptions.None, 1000)]
    private static partial Regex SingleRevisionMode { get; }

    [GeneratedRegex(@"useFreeLimit:\s*true\b", RegexOptions.None, 1000)]
    private static partial Regex UseFreeLimit { get; }

    [GeneratedRegex(@"freeLimitExhaustionBehavior:\s*'(?<b>[A-Za-z]+)'", RegexOptions.None, 1000)]
    private static partial Regex ExhaustionBehaviour { get; }

    [GeneratedRegex(@"\bcollation\s*:", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex Collation { get; }

    [GeneratedRegex(@"free_?amount_?remaining", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex FreeAmountRemainingMetric { get; }

    [GeneratedRegex(@"threshold:\s*10000\b", RegexOptions.None, 1000)]
    private static partial Regex TenPercentThreshold { get; }

    [GeneratedRegex(@"allowInsecure:\s*false\b", RegexOptions.None, 1000)]
    private static partial Regex AllowInsecureFalse { get; }

    [GeneratedRegex(@"targetPort:\s*8080\b", RegexOptions.None, 1000)]
    private static partial Regex TargetPort8080 { get; }

    [GeneratedRegex(@"push:\s*\r?\n\s+branches:\s*\[?\s*['""]?main", RegexOptions.None, 1000)]
    private static partial Regex PushToMain { get; }

    [GeneratedRegex(@"pull_request:\s*\r?\n\s+branches:\s*\[?\s*['""]?main", RegexOptions.None, 1000)]
    private static partial Regex PullRequestToMain { get; }

    [GeneratedRegex(@"^  (?<job>[A-Za-z0-9_-]+):\s*$", RegexOptions.None, 1000)]
    private static partial Regex JobKey { get; }

    [GeneratedRegex(@"^\s{4}needs:\s*(?<list>\S.*)$", RegexOptions.Multiline, 1000)]
    private static partial Regex NeedsInline { get; }

    [GeneratedRegex(@"^\s{4}needs:\s*\n(?:\s{6}-\s*(?<item>[^\n]+)\n?)+", RegexOptions.Multiline, 1000)]
    private static partial Regex NeedsBlock { get; }

    [GeneratedRegex(@"az containerapp update\b", RegexOptions.None, 1000)]
    private static partial Regex ContainerAppImageUpdate { get; }

    [GeneratedRegex(@"uses:\s*(?<action>[^@\s]+)@(?<ref>[^\s#]+)", RegexOptions.None, 1000)]
    private static partial Regex ActionUse { get; }

    [GeneratedRegex(@"^[0-9a-f]{40}$", RegexOptions.None, 1000)]
    private static partial Regex CommitSha { get; }

    /// <summary>
    /// One workflow job: its key, the jobs it needs, and its body as text.
    /// </summary>
    private sealed record Job(string Name, IReadOnlyList<string> Needs, string Body);
}
