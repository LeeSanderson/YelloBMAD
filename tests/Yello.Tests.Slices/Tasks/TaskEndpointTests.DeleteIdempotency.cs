using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.DeleteWire;
using static Yello.Tests.Slices.Tasks.TaskEditWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Story 2.5 Task 6 over the wire: a Task deletion is a <c>{spaceId}</c> state-changing request, so the pipeline's
/// <c>Idempotency-Key</c> behaviour applies unchanged - nothing new in the pipeline (<c>IdempotentResponses.IsStateChanging</c>
/// already includes DELETE, and a bodiless <c>204</c> is recorded and replayed). Each case on both surfaces (AD-4).
/// AC9; AR-25, AD-18, NFR-5, P-10. Written red by the story 2.5 ATDD run.
/// </summary>
public sealed partial class TaskEndpointTests
{
    /// <summary>
    /// AC9 - a Task deletion replayed with the same key returns the original bodiless <c>204</c> - not the <c>404</c> a
    /// fresh deletion of a gone Task meets - and deletes nothing more; a resend without a key is that <c>404</c>, and
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
    public async Task A_Task_deletion_replayed_with_the_same_key_returns_the_original_204_not_404(IsolationSurface surface)
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
            var (projectId, taskId) = await ProjectWithTaskAsync(owner, "Deleted once");

            sibling = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Stays"));

            original = await DeleteTaskOnAsync(surface, owner, token, owner.SpaceId, taskId, key);
            replay = await DeleteTaskOnAsync(surface, owner, token, owner.SpaceId, taskId, key);

            // Anchor: without the key, the same deletion is the in-Space not-found.
            fresh = await DeleteTaskOnAsync(surface, owner, token, owner.SpaceId, taskId);
        });

        Assert.NotNull(owner);
        Assert.True(original?.StatusCode == HttpStatusCode.NoContent, $"{original?.StatusCode} {original?.Body}");
        Assert.Equal(HttpStatusCode.NoContent, replay?.StatusCode);
        Assert.Equal(string.Empty, replay!.Body);
        Assert.Equal(HttpStatusCode.NotFound, fresh?.StatusCode);
        Assert.Equal(ProblemTypes.NotAvailable, Problem(fresh!)?.Type);
        Assert.Equal([sibling], (await TaskRowsAsync(database, owner.SpaceId)).Select(row => row.Id));
        Assert.Empty(await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId));
    }

    /// <summary>
    /// AC9, AR-25 - the key of one Task's deletion sent on another Task's path is refused
    /// <c>422 idempotency-key-reused</c>, and that other Task is not deleted.
    /// </summary>
    /// <param name="surface">The surface both requests are sent on.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "AR-25")]
    public async Task The_same_key_on_another_Tasks_path_is_refused_422_and_deletes_nothing(IsolationSurface surface)
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
            var (projectId, first) = await ProjectWithTaskAsync(owner, "First");

            other = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Other"));

            Assert.Equal(HttpStatusCode.NoContent, (await DeleteTaskOnAsync(surface, owner, token, owner.SpaceId, first, key)).StatusCode);
            reused = await DeleteTaskOnAsync(surface, owner, token, owner.SpaceId, other, key);
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused?.StatusCode);
        Assert.Equal(ProblemTypes.IdempotencyKeyReused, Problem(reused!)?.Type);
        Assert.Equal([other], (await TaskRowsAsync(database, owner.SpaceId)).Select(row => row.Id));
    }
}
