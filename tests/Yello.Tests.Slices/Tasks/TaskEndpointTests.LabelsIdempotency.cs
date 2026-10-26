using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.LabelWire;
using static Yello.Tests.Slices.Tasks.TaskEditWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Story 2.4 Task 6 over the wire: the Label routes and the Labels edit are <c>{spaceId}</c> state-changing routes, so
/// the pipeline's <c>Idempotency-Key</c> behaviour applies unchanged - nothing new in the pipeline. Each case on both
/// surfaces (AD-4). AC11; AR-25, AD-18, P-10, C-7. Written red by the story 2.4 ATDD run.
/// </summary>
public sealed partial class TaskEndpointTests
{
    /// <summary>
    /// AC11 - a definition replayed with the same key returns the original <c>201</c> byte for byte and does not define a
    /// second Label.
    /// </summary>
    /// <param name="surface">The surface both requests are sent on.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "AD-4")]
    public async Task A_definition_replayed_with_the_same_key_returns_the_original_201_and_defines_one_Label(IsolationSurface surface)
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? original = null;
        WireResponse? replay = null;
        var key = Guid.NewGuid().ToString("D");

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var token = await IssueTokenAsync(owner, owner.SpaceId);

            original = await DefineOnAsync(surface, owner, token, owner.SpaceId, "Once", Rust, key);
            replay = await DefineOnAsync(surface, owner, token, owner.SpaceId, "Once", Rust, key);
        });

        Assert.NotNull(owner);
        Assert.True(original?.StatusCode == HttpStatusCode.Created, $"{original?.StatusCode} {original?.Body}");
        Assert.Equal(original!.StatusCode, replay?.StatusCode);
        Assert.Equal(original.Body, replay!.Body);
        Assert.Equal(original.Header("Location"), replay.Header("Location"));
        Assert.Single(await LabelRowsAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// AC11, AR-25 - the same key with a different definition is refused <c>422 idempotency-key-reused</c> and defines
    /// nothing more.
    /// </summary>
    /// <param name="surface">The surface both requests are sent on.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "AR-25")]
    public async Task The_same_key_with_a_different_definition_is_refused_422_and_defines_nothing(IsolationSurface surface)
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? reused = null;
        var key = Guid.NewGuid().ToString("D");

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var token = await IssueTokenAsync(owner, owner.SpaceId);

            Assert.Equal(HttpStatusCode.Created, (await DefineOnAsync(surface, owner, token, owner.SpaceId, "First", Rust, key)).StatusCode);
            reused = await DefineOnAsync(surface, owner, token, owner.SpaceId, "Second", Rust, key);
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused?.StatusCode);
        Assert.Equal(ProblemTypes.IdempotencyKeyReused, Problem(reused!)?.Type);
        Assert.Equal(["First"], (await LabelRowsAsync(database, owner.SpaceId)).Select(row => row.Name));
    }

    /// <summary>
    /// AC11, Task 6 - a deletion replayed with the same key returns the original bodiless <c>204</c>
    /// (<c>IdempotentResponses</c> replays a status), not the <c>404</c> a fresh deletion of a gone Label would meet.
    /// </summary>
    /// <param name="surface">The surface both requests are sent on.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "AD-18")]
    public async Task A_deletion_replayed_with_the_same_key_returns_the_original_204_not_404(IsolationSurface surface)
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? original = null;
        WireResponse? replay = null;
        WireResponse? fresh = null;
        var key = Guid.NewGuid().ToString("D");

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var token = await IssueTokenAsync(owner, owner.SpaceId);
            var labelId = await SeedAsync(owner, owner.SpaceId, "Short-lived");

            original = await DeleteOnAsync(surface, owner, token, owner.SpaceId, labelId, key);
            replay = await DeleteOnAsync(surface, owner, token, owner.SpaceId, labelId, key);

            // Anchor: without the key, the same deletion is the in-Space not-found.
            fresh = await DeleteOnAsync(surface, owner, token, owner.SpaceId, labelId);
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.NoContent, original?.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, replay?.StatusCode);
        Assert.Equal(string.Empty, replay!.Body);
        Assert.Equal(HttpStatusCode.NotFound, fresh?.StatusCode);
        Assert.Empty(await LabelRowsAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// AC11 - a Labels edit replayed with the same key returns the original <c>200</c> byte for byte and is not
    /// re-applied: replayed after a different Labels edit by another key, it still answers the first body and the Task
    /// keeps the later set.
    /// </summary>
    /// <param name="surface">The surface every request is sent on.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "AD-18")]
    public async Task A_Labels_edit_replayed_with_the_same_key_returns_the_original_200_and_is_not_reapplied(IsolationSurface surface)
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        var later = Guid.Empty;
        WireResponse? original = null;
        WireResponse? replay = null;
        var key = Guid.NewGuid().ToString("D");

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var token = await IssueTokenAsync(owner, owner.SpaceId);

            (_, taskId) = await ProjectWithTaskAsync(owner, "Replayed");
            var first = await SeedAsync(owner, owner.SpaceId, "First");
            later = await SeedAsync(owner, owner.SpaceId, "Later");

            original = await EditTaskOnAsync(surface, owner, token, taskId, ApplyBody(first), key);
            Assert.Equal(HttpStatusCode.OK, (await EditTaskOnAsync(surface, owner, token, taskId, ApplyBody(later), Guid.NewGuid().ToString("D"))).StatusCode);
            replay = await EditTaskOnAsync(surface, owner, token, taskId, ApplyBody(first), key);
        });

        Assert.NotNull(owner);
        Assert.True(original?.StatusCode == HttpStatusCode.OK, $"{original?.StatusCode} {original?.Body}");
        Assert.Equal(original!.StatusCode, replay?.StatusCode);
        Assert.Equal(original.Body, replay!.Body);
        Assert.Equal([later], await AppliedToAsync(database, owner.SpaceId, taskId));
    }
}
