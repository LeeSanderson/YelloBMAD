namespace Yello.Tests.Architecture;

/// <summary>
/// The dependency rule, as data. This table is the executable form of AD-21 / AR-2.
/// </summary>
/// <remarks>
/// <para>
/// AD-21 enumerates only <c>Domain</c>, <c>Application</c>, <c>Infrastructure</c> and
/// <c>Host</c>. The rows for <c>Contracts</c>, <c>Merge</c>, <c>Client</c> and
/// <c>AppHost</c> are <b>derived</b> from the spine's dependency graph and source tree, not
/// quoted from AD-21 - flagged here so nobody later mistakes a derivation for a citation.
/// </para>
/// <para>
/// Two derivation notes worth carrying forward to Epic 7: the spine's graph shows
/// <c>Merge --&gt; Contracts</c> as Merge's only outbound edge, yet describes
/// <c>Yello.Infrastructure</c> as holding the "merge adapter" with no
/// <c>Infrastructure --&gt; Merge</c> edge; and <c>Yello.AppHost</c> appears in the source
/// tree and the graph but in no ring-table row. Neither blocks story 1.1 - the adapter
/// arrives in Epic 7 - but these rows are inferred and should be revisited when Epic 7
/// wires the adapter.
/// </para>
/// <para>
/// <b>The gate asserts EXACT equality</b> - a project's declared edges must match its row
/// precisely. A subset check would have been the softer reading of the spine's "may
/// reference", but it does not gate: it passes an entirely unwired solution, because the
/// empty set is a subset of everything. That is not hypothetical - it happened while story
/// 1.1 was being built, when <c>dotnet add reference</c> failed silently and a subset-based
/// gate reported green over fourteen projects with no edges at all. Task 4's instruction is
/// to wire "exactly the allowed edges", so exact equality is also the literal reading.
/// </para>
/// <para>
/// The consequence is deliberate: a later story that needs a new edge edits this table, and
/// that edit is the visible moment the dependency rule changes.
/// </para>
/// </remarks>
internal static class AllowedReferenceEdges
{
    /// <summary>
    /// The eight production projects, in ring order.
    /// </summary>
    public static readonly string[] ProductionProjects =
    [
        "Yello.Domain",
        "Yello.Application",
        "Yello.Infrastructure",
        "Yello.Host",
        "Yello.Contracts",
        "Yello.Merge",
        "Yello.Client",
        "Yello.AppHost",
    ];

    /// <summary>
    /// The five test projects named in the Structural Seed. Four are release-gating;
    /// <c>Yello.Tests.Slices</c> is the fifth and is not a gate. The readiness report says
    /// "the four test suites" in one place and "all five test projects" in another - both
    /// are correct, and this is the reconciliation.
    /// </summary>
    public static readonly string[] TestProjects =
    [
        "Yello.Tests.Isolation",
        "Yello.Tests.Revocation",
        "Yello.Tests.Merge",
        "Yello.Tests.Architecture",
        "Yello.Tests.Slices",
    ];

    /// <summary>
    /// The one declared variance from the Structural Seed: a fourteenth project holding the
    /// shared Testcontainers SQL Server fixture, which is an entry criterion for every
    /// suite and has no owning story. It is infrastructure, not a suite, so it does not
    /// breach AC5's "later stories add cases to existing suites rather than creating
    /// suites".
    /// </summary>
    public const string DeclaredVariance = "Yello.Tests.Shared";

    /// <summary>
    /// Project name to the set of projects it is permitted to reference. A project absent
    /// from this table is itself a failure: an unknown project has no agreed ring position.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> Table =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            // The five rings. Domain references nothing at all - it is the innermost ring.
            ["Yello.Domain"] = [],
            ["Yello.Application"] = ["Yello.Domain"],
            ["Yello.Infrastructure"] = ["Yello.Application", "Yello.Domain"],
            ["Yello.Host"] =
            [
                "Yello.Domain",
                "Yello.Application",
                "Yello.Infrastructure",
                "Yello.Contracts",
                "Yello.Merge",
            ],

            // Shared client + server. Contracts references nothing so that both sides can
            // depend on it without either dragging a ring across the boundary.
            ["Yello.Contracts"] = [],
            ["Yello.Merge"] = ["Yello.Contracts"],
            ["Yello.Client"] = ["Yello.Contracts", "Yello.Merge"],

            // Orchestration, in no ring. Excluded from the ring assertions and asserted
            // against this row instead: it legitimately references Host and Client as
            // Aspire project resources, which the ring rule would otherwise read as a
            // violation.
            ["Yello.AppHost"] = ["Yello.Host", "Yello.Client"],

            // Test projects, likewise excluded from the ring assertions. The architecture
            // suite legitimately references all eight production projects - it has to load
            // their assemblies - so without its own row the gate would fail on itself.
            ["Yello.Tests.Architecture"] = [.. ProductionProjects],
            ["Yello.Tests.Isolation"] = ["Yello.Host", "Yello.Contracts", DeclaredVariance],
            ["Yello.Tests.Revocation"] = ["Yello.Host", "Yello.Contracts", DeclaredVariance],
            ["Yello.Tests.Merge"] = ["Yello.Merge", "Yello.Contracts"],
            ["Yello.Tests.Slices"] =
            [
                "Yello.Application",
                "Yello.Domain",
                "Yello.Infrastructure",
                "Yello.Host",
                DeclaredVariance,
            ],

            // Fixtures only. Depends on Testcontainers and xunit, and on no Yello project:
            // a fixture that knew about a ring would let a suite reach a ring its own row
            // forbids.
            [DeclaredVariance] = [],
        };

    /// <summary>
    /// Every project the solution is expected to contain: the thirteen named, plus the variance.
    /// </summary>
    public static IEnumerable<string> ExpectedProjects =>
        ProductionProjects.Concat(TestProjects).Append(DeclaredVariance);
}
