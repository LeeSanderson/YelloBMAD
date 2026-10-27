using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.MoveWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Story 2.6 AC13 over the wire: a move is a <c>{spaceId}</c> state-changing <c>POST</c>, so the pipeline's
/// <c>Idempotency-Key</c> behaviour applies unchanged. On the Session surface only - the Token surface is the
/// no-credential <c>401</c> (Q1 (a), MOV-12). AR-25, AD-18, NFR-5, P-10. Written red by the story 2.6 ATDD run.
/// </summary>
public sealed partial class TaskEndpointTests
{
    /// <summary>
    /// IDM-M01 (AC13, NFR-5) - a move replayed with the same key returns the original <c>200</c> and body byte for byte
    /// without re-applying it: the Task's key after the replay is the key the first answer named, so no second key was
    /// written, even though the Board changed between the two (another Task was moved into the same gap meanwhile).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC13 - the move route (Q1 (a)) is not mapped yet")]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "NFR-5")]
    public async Task A_move_replayed_with_the_same_key_returns_the_original_200_and_writes_no_second_key()
    {
        var database = Available();
        Actor? owner = null;
        var moved = Guid.Empty;
        WireResponse? original = null;
        WireResponse? replay = null;
        var key = Guid.NewGuid().ToString("D");

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var statuses = await StatusesAsync(database, owner.SpaceId);
            var projectId = await CreateProjectAsync(owner, "Moved once");
            var first = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "First"));
            var other = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Other"));

            moved = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Moved"));

            original = await MoveTaskAsync(owner, owner.SpaceId, moved, statuses[0].Id, afterTaskId: null, key);

            // The gap changes between the two: a replay that re-ran the move would compute another key.
            Assert.Equal(HttpStatusCode.OK, (await MoveTaskAsync(owner, owner.SpaceId, other, statuses[0].Id, afterTaskId: null)).StatusCode);

            replay = await MoveTaskAsync(owner, owner.SpaceId, moved, statuses[0].Id, afterTaskId: null, key);

            Assert.NotEqual(Guid.Empty, first);
        });

        Assert.NotNull(owner);
        Assert.True(original?.StatusCode == HttpStatusCode.OK, $"{original?.StatusCode} {original?.Body}");
        Assert.Equal(HttpStatusCode.OK, replay?.StatusCode);
        Assert.Equal(original!.Body, replay!.Body);
        Assert.Equal(PlacementOf(original).PositionKey, await KeyOfAsync(database, owner.SpaceId, moved));
    }

    /// <summary>
    /// IDM-M02 (AC13, AR-25) - the same key with a different body (another destination) is refused
    /// <c>422 idempotency-key-reused</c>, and the Task stays where the first move put it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC13 - the move route (Q1 (a)) is not mapped yet")]
    [Trait("Requirement", "AR-25")]
    public async Task The_same_key_with_a_different_move_is_refused_422_and_nothing_moves()
    {
        var database = Available();
        Actor? owner = null;
        var moved = Guid.Empty;
        WireResponse? original = null;
        WireResponse? reused = null;
        var key = Guid.NewGuid().ToString("D");

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var statuses = await StatusesAsync(database, owner.SpaceId);
            var (projectId, created) = await TaskEditWire.ProjectWithTaskAsync(owner, "Moved by key");

            moved = created;
            _ = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Bystander"));

            original = await MoveTaskAsync(owner, owner.SpaceId, moved, statuses[1].Id, afterTaskId: null, key);
            reused = await MoveTaskAsync(owner, owner.SpaceId, moved, statuses[2].Id, afterTaskId: null, key);
        });

        Assert.NotNull(owner);
        Assert.True(original?.StatusCode == HttpStatusCode.OK, $"{original?.StatusCode} {original?.Body}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused?.StatusCode);
        Assert.Equal(ProblemTypes.IdempotencyKeyReused, Problem(reused!)?.Type);
        Assert.Equal(PlacementOf(original!).StatusId, (await TaskEditWire.RowAsync(database, owner.SpaceId, moved))?.StatusId);
    }
}
