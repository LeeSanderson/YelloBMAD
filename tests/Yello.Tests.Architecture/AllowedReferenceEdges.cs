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
    /// The test suites. Five are named in the Structural Seed and the sixth was added by the
    /// framework run; four of the six are release-gating.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Of the Structural Seed's five, <c>Yello.Tests.Slices</c> is the one that is not a gate.
    /// The readiness report says "the four test suites" in one place and "all five test
    /// projects" in another - both are correct, and this is the reconciliation.
    /// </para>
    /// <para>
    /// <b><c>Yello.Tests.Client</c> is the sixth, and it is an addition rather than a
    /// variance.</b> <c>Yello.Tests.Shared</c> is the variance: infrastructure holding no
    /// cases. This one holds cases, so it is a suite, and AC5's "later stories add cases to
    /// existing suites rather than creating suites" applies to it. It was added anyway because
    /// the spine names a test level it never binds - <c>test-design-architecture.md:174</c>
    /// lists five levels and gives E2E as "browser, binding per B5" - and
    /// <c>test-design-architecture.md:315</c> asks for exactly this: "the browser-test binding
    /// decision, and a sixth test project". Two AD-29 invariants exist only in the rendered
    /// client and are unreachable from all five of the others.
    /// </para>
    /// <para>
    /// The conversation <c>TESTING-CONVENTIONS.md</c> requires before a suite exists - "that is
    /// a conversation about the architecture, not a project template" - was held with Lee on
    /// 2026-09-07 and recorded in <c>framework-setup-progress.md</c>. The alternative
    /// considered was folding the cases into <c>Yello.Tests.Slices</c>; they were kept apart
    /// because this suite's entry criterion is a live dev-server process and a real browser.
    /// </para>
    /// </remarks>
    public static readonly string[] TestProjects =
    [
        "Yello.Tests.Isolation",
        "Yello.Tests.Revocation",
        "Yello.Tests.Merge",
        "Yello.Tests.Architecture",
        "Yello.Tests.Slices",
        "Yello.Tests.Client",
    ];

    /// <summary>
    /// The one declared variance from the Structural Seed: a project holding the shared
    /// Testcontainers SQL Server fixture, which is an entry criterion for every suite and has
    /// no owning story. It is infrastructure, not a suite, so it does not breach AC5's "later
    /// stories add cases to existing suites rather than creating suites".
    /// </summary>
    /// <remarks>
    /// Still the only <i>variance</i>. <c>Yello.Tests.Client</c> also arrived after story 1.1,
    /// but it holds cases and is therefore a suite, listed in <see cref="TestProjects"/> with
    /// the reasoning for it. The distinction is worth keeping: a variance is something the
    /// Structural Seed did not anticipate and that no story owns, and there is exactly one.
    /// </remarks>
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
            // Infrastructure was added by story 1.9, for the schema only: each test class gets a
            // database of its own and runs the migrations into it through YelloDbContext, because
            // AR-36 forbids the Host applying them at startup. Seeding stays through the product
            // over HTTP, and InternalsVisibleTo still names only the slice suite, so these suites
            // reach Infrastructure's public migration surface and nothing internal.
            ["Yello.Tests.Isolation"] = ["Yello.Host", "Yello.Contracts", "Yello.Infrastructure", DeclaredVariance],
            ["Yello.Tests.Revocation"] = ["Yello.Host", "Yello.Contracts", "Yello.Infrastructure", DeclaredVariance],
            ["Yello.Tests.Merge"] = ["Yello.Merge", "Yello.Contracts"],
            // Contracts was added by story 1.3, and the edit is the visible moment the rule
            // changed - which is what this table is for. The slice tests assert the registration
            // endpoint's wire behaviour: they POST a Yello.Contracts request DTO and read back an
            // RFC 9457 problem body whose stable `type` is a Yello.Contracts constant. Reaching
            // those through Host's transitive output was the alternative and is exactly what
            // deferred-work.md:10 warns against - "declared edges are not the effective
            // dependency closure" - so the edge is declared rather than relied on.
            ["Yello.Tests.Slices"] =
            [
                "Yello.Application",
                "Yello.Domain",
                "Yello.Infrastructure",
                "Yello.Host",
                "Yello.Contracts",
                DeclaredVariance,
            ],

            // The browser level, added by the framework run that resolved blocker B5.
            //
            // Contracts and NOTHING ELSE, and the absence is the deliberate half. The suite
            // drives Yello.Client as a black box over HTTP, which is what a browser level is
            // for: a compile-time edge to the thing under test would let a case assert against
            // a type instead of against what the browser actually received - and the defect
            // that prompted this suite was invisible to 136 tests that all did exactly that.
            //
            // Contracts is here so the boot check computes its expected `<html lang>` from
            // CultureSelection against SupportedCultures - the one list both surfaces read -
            // rather than hard-coding "en". Every locale resolves to "en" today, so a literal
            // would pass and keep passing for the wrong reason.
            //
            // Story 1.9 (AC11) added Shared and Infrastructure for RealHostCollection - the real
            // client against a real Host on a real database. Shared carries the Host harness and the
            // one SQL Server engine; Infrastructure is for the schema only (the migrations, through
            // YelloDbContext, as the isolation and revocation suites run them). Neither is the thing
            // under test, so the black-box reason above still holds, and Yello.Client stays absent.
            ["Yello.Tests.Client"] = ["Yello.Contracts", DeclaredVariance, "Yello.Infrastructure"],

            // Fixtures and the wire harness. Depends on Testcontainers and xunit, and on one
            // Yello project: Contracts, added by story 1.9 when HostProcess and WireSession moved
            // here so the isolation and revocation suites share the slice suite's harness (Q2 (a)).
            // WireSession posts Contracts DTOs and reads Contracts routes. Contracts references
            // nothing, so this edge reaches no ring: a fixture that knew about a ring would let a
            // suite reach a ring its own row forbids, and that is still why nothing else is here.
            [DeclaredVariance] = ["Yello.Contracts"],
        };

    /// <summary>
    /// Every project the solution is expected to contain: the eight production projects, the
    /// six test suites, and the one declared variance.
    /// </summary>
    public static IEnumerable<string> ExpectedProjects =>
        ProductionProjects.Concat(TestProjects).Append(DeclaredVariance);

    /// <summary>
    /// Package prefixes a given project may not reference, by project.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ring rule above governs <c>ProjectReference</c> only, which leaves a hole the same
    /// shape as the one Gate A exists to close for project references.
    /// <c>&lt;PackageReference Include="Microsoft.EntityFrameworkCore" /&gt;</c> in
    /// <c>Yello.Application</c> breaks AD-21 exactly as an Infrastructure project reference
    /// would, and neither gate sees it: the ring table has no package column, and Gate B is a
    /// <i>type</i>-dependency rule that fires only once a type is actually touched. Declared
    /// but unused is invisible to bytecode - which is the whole reason Gate A exists.
    /// </para>
    /// <para>
    /// These are prefixes rather than exact ids, and bans rather than an allow-list. An
    /// allow-list would force a table edit for every ordinary package a later story adds,
    /// which buys nothing: the architectural fact is not "which packages" but "which
    /// <i>kinds</i> of package may cross this ring boundary". A ban states that directly and
    /// only changes when the architecture does.
    /// </para>
    /// <para>
    /// <c>Yello.Client</c> is deliberately absent. It legitimately references
    /// <c>Microsoft.AspNetCore.Components.WebAssembly</c> - Blazor WASM is ASP.NET Core
    /// surface, and the client is where it belongs.
    /// </para>
    /// </remarks>
    // Declared before the table that reads it: static field initialisers run in textual
    // order, so the other way round hands the table an array that is still null (MA0195).
    private static readonly string[] PersistenceAndTransport =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore.",
        "Microsoft.Data.SqlClient",
        "Microsoft.Data.Sqlite",
        "Aspire.",
        "Testcontainers",
        "Dapper",
        "System.Data.SqlClient",
    ];

    /// <summary>
    /// Package prefixes a given project may not reference, by project. See
    /// <see cref="PersistenceAndTransport"/> for the shared list.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> ForbiddenPackagePrefixes =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            // The innermost ring holds entities, invariants and ports. It reaches nothing at
            // all - so the ban is every prefix that could put a framework inside an invariant.
            ["Yello.Domain"] = [.. PersistenceAndTransport],
            ["Yello.Application"] = [.. PersistenceAndTransport],

            // Contracts and Merge are compiled into the WebAssembly client as well as the
            // server. Either one gaining EF Core or ASP.NET Core would drag a server-side ring
            // across the wire boundary and into the browser payload.
            ["Yello.Contracts"] = [.. PersistenceAndTransport],
            ["Yello.Merge"] = [.. PersistenceAndTransport],
        };
}
