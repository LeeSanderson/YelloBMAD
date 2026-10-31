using System.Net;
using Xunit;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 3.2 AC18 over the wire: no Space-scoped answer may outlive sign-out in the browser's HTTP cache on a shared
/// machine - the exposure AC5's stated reason names, by another door. Open question 2 (a), assumed: every <c>/api</c>
/// response carries <c>Cache-Control: no-store</c>, set once in the pipeline. Written by the story 3.2 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>In this class rather than a new one</b>, as a partial file: a container-backed class is one more SQL Server engine
/// on a full run (<c>CLAUDE.md</c>, the test memory budget). One Host process, one registered Account.
/// </para>
/// <para>
/// <b>Seed constraints:</b> none written here. The Account and its Personal Space (with its Owner Membership and three
/// Statuses) are made by the product's own registration, so <c>UX_Account_NormalizedEmailAddress</c> holds (a random
/// address) and every foreign key is the product's. The boundary case names a random Space id that exists nowhere.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// AC18 / Q2 (a) - the switcher's <c>200</c>, a Space-scoped <c>200</c> (the active Space and its Statuses) and the
    /// boundary's <c>404</c> each carry <c>Cache-Control: no-store</c>.
    /// </summary>
    /// <remarks>
    /// Anchored on the statuses first, so a route that fell through to the unmatched endpoint is not read as an answer.
    /// Plant: remove the pipeline's <c>no-store</c> - this goes red on all four.
    /// </remarks>
    [Fact]
    [Trait("Priority", "P0")]
    [Trait("Requirement", "UX-DR31")]
    [Trait("Requirement", "AD-7")]
    [Trait("Requirement", "NFR-1")]
    public async Task Every_api_answer_is_no_store_on_the_switcher_a_Space_and_the_boundary()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var absent = Guid.NewGuid();

        WireResponse? directory = null;
        WireResponse? active = null;
        WireResponse? statuses = null;
        WireResponse? boundary = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, name);
            var personal = await PersonalSpaceOfAsync(database, name);

            directory = await session.GetAsync(SpaceRoutes.Directory);
            active = await session.GetAsync(SpaceRoutes.ActiveOf(personal));
            statuses = await session.GetAsync(SpaceRoutes.StatusesOf(personal));
            boundary = await session.GetAsync(SpaceRoutes.ActiveOf(absent));
        });

        Assert.NotNull(directory);
        Assert.NotNull(active);
        Assert.NotNull(statuses);
        Assert.NotNull(boundary);

        // Anchors: each route answered as itself.
        Assert.Equal(HttpStatusCode.OK, directory.StatusCode);
        Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        Assert.Equal(HttpStatusCode.OK, statuses.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, boundary.StatusCode);

        foreach (var (what, response) in new[] { ("switcher", directory), ("active Space", active), ("Statuses", statuses), ("boundary 404", boundary) })
        {
            var cacheControl = response.Header("Cache-Control") ?? string.Empty;

            Assert.True(
                cacheControl.Contains("no-store", StringComparison.OrdinalIgnoreCase),
                $"The {what} answer carries Cache-Control '{cacheControl}', not no-store (story 3.2 AC18).");
        }
    }
}
