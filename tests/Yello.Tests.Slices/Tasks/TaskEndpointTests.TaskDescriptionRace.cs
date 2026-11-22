using Xunit;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.TaskDescriptionSliceWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Appends to one description serialise on the Task row, and each rebuilds the log only after taking it, so two editors'
/// concurrent writes both survive in the log and in the projection.
/// </summary>
/// <remarks>
/// <para>
/// <b>The overlap is controlled, never hoped for.</b> The test holds the Task row in the product's shape
/// (<see cref="TaskDescriptionSliceWire.HoldTaskAsync"/>: <c>UPDLOCK, HOLDLOCK, INDEX(1)</c>), sends the append, waits until
/// it is blocked on a lock (<see cref="TaskWire.WaitUntilBlockedOnALockAsync"/>), and only then lets it go.
/// </para>
/// <para>
/// <b>The decisive case is the read race</b>: holding the row cannot show an append that read the log before the lock, so
/// the test itself appends, in the holding transaction, what a competing editor would. An append that rebuilt before the
/// lock collides on <c>(SpaceId, TaskId, Sequence)</c> or projects without the competing text.
/// </para>
/// </remarks>
public sealed partial class TaskEndpointTests
{
    /// <summary>
    /// The read race: with the Task row held, an editor's append is sent and waits; the holder appends another editor's
    /// change as row 1 and re-projects, then commits. The waiting append is admitted as row 2, and the projection holds both
    /// texts and equals what the log rebuilds to.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-19")]
    [Trait("Requirement", "AD-13")]
    [Trait("Requirement", "FR-31")]
    public async Task An_append_rebuilds_the_log_only_under_the_Task_lock()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        WireResponse? answer = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            taskId = await TaskAsync(owner);

            // Present in the Space before the race, so a first-request presence insert cannot serialise anything.
            Assert.Equal(System.Net.HttpStatusCode.OK, (await ReadAsync(owner, owner.SpaceId, taskId)).StatusCode);

            var held = NewEditor();
            var heldChange = held.Insert(0, "alpha");
            var waiting = NewEditor();
            var (connection, transaction) = await HoldTaskAsync(database, owner.SpaceId, taskId);

            await using (connection)
            await using (transaction)
            {
                await using var observer = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
                var racing = SendChangesAsync(owner, owner.SpaceId, taskId, BatchOf(waiting, waiting.Insert(0, "beta")));

                await WaitUntilBlockedOnALockAsync(observer, 1, racing, TestContext.Current.CancellationToken);
                await InsertChangeAsync(connection, transaction, owner.SpaceId, taskId, heldChange, (owner.AccountId, "Held"), "alpha");
                await transaction.CommitAsync(TestContext.Current.CancellationToken);

                answer = await racing;
            }
        });

        Assert.NotNull(owner);
        AssertAnswered(answer, refused: 0);

        var rows = await RowsAsync(database, owner.SpaceId, taskId);
        var projection = await DescriptionAsync(database, owner.SpaceId, taskId);

        Assert.Equal([1L, 2L], rows.Select(row => row.Sequence));
        Assert.Equal(RebuiltText(rows), projection);
        Assert.Contains("alpha", projection, StringComparison.Ordinal);
        Assert.Contains("beta", projection, StringComparison.Ordinal);
    }

    /// <summary>
    /// Two editors' appends queued on one held Task row are both admitted when it is released: two rows, distinct sequences,
    /// and a projection holding both texts that equals what the log rebuilds to.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-19")]
    [Trait("Requirement", "AD-13")]
    [Trait("Requirement", "FR-31")]
    public async Task Two_appends_queued_on_one_Task_are_both_kept()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        var answers = Array.Empty<WireResponse>();

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            taskId = await TaskAsync(owner);
            Assert.Equal(System.Net.HttpStatusCode.OK, (await ReadAsync(owner, owner.SpaceId, taskId)).StatusCode);

            var first = NewEditor();
            var second = NewEditor();
            var (connection, transaction) = await HoldTaskAsync(database, owner.SpaceId, taskId);

            await using (connection)
            await using (transaction)
            {
                await using var observer = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
                var racing = Task.WhenAll(
                    SendChangesAsync(owner, owner.SpaceId, taskId, BatchOf(first, first.Insert(0, "left"))),
                    SendChangesAsync(owner, owner.SpaceId, taskId, BatchOf(second, second.Insert(0, "right"))));

                await WaitUntilBlockedOnALockAsync(observer, 2, racing, TestContext.Current.CancellationToken);
                await transaction.CommitAsync(TestContext.Current.CancellationToken);

                answers = await racing;
            }
        });

        Assert.NotNull(owner);
        foreach (var answer in answers)
        {
            AssertAnswered(answer, refused: 0);
        }

        var rows = await RowsAsync(database, owner.SpaceId, taskId);
        var projection = await DescriptionAsync(database, owner.SpaceId, taskId);

        Assert.Equal([1L, 2L], rows.Select(row => row.Sequence));
        Assert.Equal(RebuiltText(rows), projection);
        Assert.Contains("left", projection, StringComparison.Ordinal);
        Assert.Contains("right", projection, StringComparison.Ordinal);
    }
}
