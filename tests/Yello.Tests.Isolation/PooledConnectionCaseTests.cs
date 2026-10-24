using Xunit;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// AC4 / AD-2 / I-8 - two requests for different Spaces served consecutively on one physical
/// connection: the second discloses nothing belonging to the first, on either surface.
/// </summary>
/// <remarks>
/// <para>
/// <b>On the shared engine, with a database and a pinned pool of its own.</b> SqlClient keys pools by
/// connection string, so the Host's <c>Max Pool Size=1</c> on this world's database is its own pool
/// without its own engine. <c>Slices/PooledConnectionIsolationTests</c> (in-process, <c>@@SPID</c>
/// identity) stays as the lower-level guard.
/// </para>
/// <para>
/// <b>The anchor is the count of one.</b> A count of zero would mean the side query cannot see the
/// Host's sessions, and the case would pass having observed nothing. The plant for Task 12 is removing
/// the pin: the count must then exceed one.
/// </para>
/// </remarks>
/// <param name="fixture">The pinned-pool world.</param>
[Trait("Suite", "Isolation")]
[Trait("Priority", "P0")]
[Trait("Requirement", "NFR-1")]
[Trait("Requirement", "AD-2")]
[Collection(nameof(PooledConnectionCollection))]
public sealed class PooledConnectionCaseTests(PinnedPoolWorldFixture fixture) : IClassFixture<PinnedPoolWorldFixture>
{
    /// <summary>
    /// The pooled runner's rows.
    /// </summary>
    public static IEnumerable<TheoryDataRow<string, IsolationSurface>> Rows => IsolationCases.PooledRows;

    /// <summary>
    /// 1.9-POOL-001 / ISO-POOLED, as a row on each surface.
    /// </summary>
    /// <param name="caseId">The row.</param>
    /// <param name="surface">The surface.</param>
    /// <returns>A task.</returns>
    [Theory]
    [MemberData(nameof(Rows))]
    public Task Consecutive_requests_for_two_Spaces_on_one_connection_each_see_only_their_own(string caseId, IsolationSurface surface) =>
        CaseRun.RunAsync(fixture.World, IsolationCases.Named(caseId), surface);
}

/// <summary>
/// The pooled-connection case runs alone: no other test may draw on the Host or the engine while it
/// proves that one physical connection served both Accounts.
/// </summary>
/// <remarks>
/// AC12: "the pooled-connection case keeps its own pinned pool and runs without parallelism". A
/// collection with parallelisation disabled runs after the parallel ones, by itself.
/// </remarks>
[CollectionDefinition(nameof(PooledConnectionCollection), DisableParallelization = true)]
public sealed class PooledConnectionCollection;
