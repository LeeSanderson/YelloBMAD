using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;
using DeleteWire = Yello.Tests.Slices.Tasks.DeleteWire;

namespace Yello.Tests.Slices.Projects;

/// <summary>
/// Story 2.5 Task 6 over the wire, for the Project: a deletion replays its original <c>204</c> under its key, the key is
/// refused on another Project's path, and a keyless resend is the in-Space <c>404</c>. Nothing new in the pipeline. Each
/// case on both surfaces (AD-4). AC9; AR-25, AD-18, NFR-5, P-10. Written red by the story 2.5 ATDD run.
/// </summary>
public sealed partial class ProjectEndpointTests
{
    /// <summary>
    /// AC9 - a Project deletion replayed with the same key returns the original bodiless <c>204</c>, not the <c>404</c> a
    /// fresh deletion of a gone Project meets, and deletes nothing more; a resend without a key is that <c>404</c> and
    /// records nothing.
    /// </summary>
    /// <param name="surface">The surface every request is sent on.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "NFR-5")]
    public async Task A_Project_deletion_replayed_with_the_same_key_returns_the_original_204_not_404(IsolationSurface surface)
    {
        var database = Available();
        Actor? owner = null;
        var sibling = Guid.Empty;
        WireResponse? original = null;
        WireResponse? replay = null;
        WireResponse? fresh = null;
        var key = Guid.NewGuid().ToString("D");

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var token = await IssueTokenAsync(owner, owner.SpaceId);
            var deleter = AsTaskActor(owner);
            var doomed = IdOf(await CreateAsync(owner, "Deleted once"));

            sibling = IdOf(await CreateAsync(owner, "Stays"));

            original = await DeleteWire.DeleteProjectOnAsync(surface, deleter, token, owner.SpaceId, doomed, key);
            replay = await DeleteWire.DeleteProjectOnAsync(surface, deleter, token, owner.SpaceId, doomed, key);

            // Anchor: without the key, the same deletion is the in-Space not-found.
            fresh = await DeleteWire.DeleteProjectOnAsync(surface, deleter, token, owner.SpaceId, doomed);
        });

        Assert.NotNull(owner);
        Assert.True(original?.StatusCode == HttpStatusCode.NoContent, $"{original?.StatusCode} {original?.Body}");
        Assert.Equal(HttpStatusCode.NoContent, replay?.StatusCode);
        Assert.Equal(string.Empty, replay!.Body);
        Assert.Equal(HttpStatusCode.NotFound, fresh?.StatusCode);
        Assert.Equal(ProblemTypes.NotAvailable, Problem(fresh!)?.Type);
        Assert.Equal([sibling], (await ProjectRowsAsync(database, owner.SpaceId)).Select(row => row.Id));
        Assert.Empty(await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId));
    }

    /// <summary>
    /// AC9, AR-25 - the key of one Project's deletion sent on another Project's path is refused
    /// <c>422 idempotency-key-reused</c>, and that other Project is not deleted.
    /// </summary>
    /// <param name="surface">The surface both requests are sent on.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "AR-25")]
    public async Task The_same_key_on_another_Projects_path_is_refused_422_and_deletes_nothing(IsolationSurface surface)
    {
        var database = Available();
        Actor? owner = null;
        var other = Guid.Empty;
        WireResponse? reused = null;
        var key = Guid.NewGuid().ToString("D");

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var token = await IssueTokenAsync(owner, owner.SpaceId);
            var deleter = AsTaskActor(owner);
            var first = IdOf(await CreateAsync(owner, "First"));

            other = IdOf(await CreateAsync(owner, "Other"));

            Assert.Equal(HttpStatusCode.NoContent, (await DeleteWire.DeleteProjectOnAsync(surface, deleter, token, owner.SpaceId, first, key)).StatusCode);
            reused = await DeleteWire.DeleteProjectOnAsync(surface, deleter, token, owner.SpaceId, other, key);
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused?.StatusCode);
        Assert.Equal(ProblemTypes.IdempotencyKeyReused, Problem(reused!)?.Type);
        Assert.Equal([other], (await ProjectRowsAsync(database, owner.SpaceId)).Select(row => row.Id));
    }
}
