using System.Text.Json;
using Xunit;
using Yello.Contracts.Spaces;

namespace Yello.Tests.Isolation;

/// <summary>
/// AC4 / AD-2 / I-8 - two requests for different Spaces served consecutively on one physical
/// connection: the second discloses nothing belonging to the first, on either surface.
/// </summary>
/// <remarks>
/// <para>
/// <b>Red-phase scaffold.</b> On activation it runs on the story's shared engine with its own
/// database: SqlClient keys pools by connection string, so <c>Max Pool Size=1</c> on one database is
/// its own pool without its own engine (Task 5). <c>Slices/PooledConnectionIsolationTests</c>
/// (in-process, <c>@@SPID</c> identity) stays as the lower-level guard.
/// </para>
/// <para>
/// <b>The anchor is the count of one.</b> A count of zero would mean the side query cannot see the
/// Host's sessions, and the case would pass having observed nothing. The plant for Task 12 is
/// removing <c>Max Pool Size=1</c>: this anchor must then fail, proving the case observes reuse.
/// </para>
/// </remarks>
[Trait("Suite", "Isolation")]
[Trait("Priority", "P0")]
[Trait("Requirement", "NFR-1")]
[Trait("Requirement", "AD-2")]
[Collection(nameof(PooledConnectionCollection))]
public sealed class PooledConnectionCaseTests
{
    private const int Alternations = 5;

    /// <summary>
    /// 1.9-POOL-001 / ISO-POOLED - A and B alternately read their own statuses through a Host whose
    /// pool holds one connection; every answer holds exactly its own Space's status ids, and the Host
    /// held exactly one physical session throughout.
    /// </summary>
    [Fact(Skip = "red: AC4 - the pinned-pool world does not exist yet (story 1.9 Tasks 2, 3 and 5)")]
    public async Task Consecutive_requests_for_two_Spaces_on_one_connection_each_see_only_their_own()
    {
        await using var world = await IsolationWorld.StartAsync(new IsolationWorldOptions(MaxPoolSize: 1));

        foreach (var surface in IsolationWorld.Surfaces)
        {
            var aOwn = Ids((await world.SendAsync(world.A, surface, HttpMethod.Get, SpaceRoutes.StatusesOf(world.A.SpaceId))).Body);
            var bOwn = Ids((await world.SendAsync(world.B, surface, HttpMethod.Get, SpaceRoutes.StatusesOf(world.B.SpaceId))).Body);

            Assert.Equal(3, aOwn.Count);
            Assert.Equal(3, bOwn.Count);
            Assert.Empty(aOwn.Intersect(bOwn));

            for (var round = 0; round < Alternations; round++)
            {
                var a = await world.SendAsync(world.A, surface, HttpMethod.Get, SpaceRoutes.StatusesOf(world.A.SpaceId));
                var b = await world.SendAsync(world.B, surface, HttpMethod.Get, SpaceRoutes.StatusesOf(world.B.SpaceId));

                Assert.Equal(aOwn.Order(), Ids(a.Body).Order());
                Assert.Equal(bOwn.Order(), Ids(b.Body).Order());
                Assert.DoesNotContain(world.A.SpaceName, b.Body, StringComparison.Ordinal);
                Assert.DoesNotContain(world.B.SpaceName, a.Body, StringComparison.Ordinal);
            }
        }

        Assert.Equal(1, await world.HostSessionCountAsync());
    }

    private static List<Guid> Ids(string body)
    {
        using var document = JsonDocument.Parse(body);

        return [.. document.RootElement.EnumerateArray().Select(row => row.GetProperty("id").GetGuid())];
    }
}

/// <summary>
/// The pooled-connection case runs alone: no other test may draw on the Host or the engine while it
/// proves that one physical connection served both Accounts.
/// </summary>
/// <remarks>
/// AC12: "the pooled-connection case keeps its own pinned pool and runs without parallelism". A
/// collection with parallelisation disabled runs after the parallel ones, by itself. It names no
/// fixture, so it starts nothing.
/// </remarks>
[CollectionDefinition(nameof(PooledConnectionCollection), DisableParallelization = true)]
public sealed class PooledConnectionCollection;
