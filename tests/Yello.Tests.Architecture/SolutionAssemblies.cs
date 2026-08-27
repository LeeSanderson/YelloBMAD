namespace Yello.Tests.Architecture;

/// <summary>
/// Every compiled assembly in the solution, as files on disk for Mono.Cecil to read.
/// </summary>
/// <remarks>
/// <para>
/// AC3 bans the Role API "anywhere in the solution". <see cref="ProductionAssemblies"/> is
/// the wrong set for that: it excludes <c>tests/**</c>, so a banned role API written in a
/// suite or a fixture satisfies all four A-3 assertions. Test code is where a role-based
/// shortcut is most likely to be written first - a fixture that seeds "an admin" is the
/// natural place - and from there it becomes the pattern production code copies.
/// </para>
/// <para>
/// <b>The test assemblies are found by path convention, not by reference.</b> They cannot be
/// referenced: <c>Yello.Tests.Architecture</c>'s row in the ring table names the eight
/// production projects and nothing else, and a suite referencing its sibling suites would be
/// a worse defect than the one this closes. So each is located at the same output path
/// relative to its own project directory that this assembly occupies relative to its own -
/// and when one is not there, the gate <b>fails</b> rather than quietly scanning a smaller
/// set. A gate that narrows its own scope in silence is the exact failure mode this file
/// exists to fix.
/// </para>
/// </remarks>
internal static class SolutionAssemblies
{
    /// <summary>
    /// Assembly files that should exist but do not. Non-empty means the scan cannot see the
    /// whole solution, which the gate reports as a failure.
    /// </summary>
    public static IReadOnlyList<string> Unreadable => Discovery.Value.Missing;

    /// <summary>
    /// Every assembly file the Role-API scan reads: the eight production assemblies plus
    /// every test assembly.
    /// </summary>
    public static IReadOnlyList<string> AllFiles => Discovery.Value.Files;

    private static readonly Lazy<DiscoveryResult> Discovery = new(Discover, isThreadSafe: true);

    private sealed record DiscoveryResult(List<string> Files, List<string> Missing);

    private static DiscoveryResult Discover()
    {
        var files = ProductionAssemblies.All.Select(a => a.Location).ToList();
        var missing = new List<string>();

        foreach (var project in RepositoryLayout.AllProjectFiles.Where(RepositoryLayout.IsUnderTestsDirectory))
        {
            var candidate = ExpectedOutputPath(project);

            // NOT `continue`. Reporting nothing here is what let this gate pass over zero test
            // assemblies: `Unreadable` stayed empty, the precondition assertion was satisfied,
            // and Gate C's four bans quietly covered the eight production assemblies only.
            // Demonstrated during review by building with -p:ArtifactsPath - 44/44 green with
            // nothing under tests/ scanned. An unknown output layout is the gate being unable
            // to answer, which is a failure, not an absence of violations.
            if (candidate is null)
            {
                missing.Add(
                    $"{RepositoryLayout.ProjectName(project)} (its assembly could not be located: " +
                    "the build output layout is not the one this convention assumes, so the " +
                    "path cannot be derived from this assembly's own location)");
                continue;
            }

            if (File.Exists(candidate))
            {
                files.Add(candidate);
            }
            else
            {
                missing.Add($"{RepositoryLayout.ProjectName(project)} (expected at {candidate})");
            }
        }

        return new DiscoveryResult(files, missing);
    }

    /// <summary>
    /// Where a sibling test project's assembly sits, assuming it builds the way this one does.
    /// </summary>
    /// <remarks>
    /// The output tail is read from this assembly's own location rather than hard-coded, so a
    /// Release build, a different TFM or a changed <c>BaseOutputPath</c> resolves correctly
    /// without the convention being restated anywhere.
    /// </remarks>
    private static string? ExpectedOutputPath(FileInfo project)
    {
        var ownProjectDirectory = Path.Combine(
            RepositoryLayout.Root.FullName, "tests", "Yello.Tests.Architecture");

        var tail = Path.GetRelativePath(ownProjectDirectory, AppContext.BaseDirectory);

        // A tail that climbs out of the project directory means the output layout is not the
        // one this convention assumes (a custom ArtifactsPath, say). Report nothing rather
        // than guess - the caller turns a null into a named failure, which it must, because
        // the alternative is a scan whose scope silently shrank to the assemblies it could
        // find. Do not make this `continue` at the call site.
        if (tail.StartsWith("..", StringComparison.Ordinal) || project.Directory is null)
        {
            return null;
        }

        return Path.GetFullPath(Path.Combine(
            project.Directory.FullName, tail, $"{RepositoryLayout.ProjectName(project)}.dll"));
    }
}
