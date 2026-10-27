using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Domain.Tasks;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.DeleteWire;
using static Yello.Tests.Slices.Tasks.LabelWire;
using static Yello.Tests.Slices.Tasks.MoveWire;
using static Yello.Tests.Slices.Tasks.TaskEditWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Story 2.6 AC11 (and AC5's deterministic half) over the wire: a move is the fourth key placer and a new writer on rows
/// every Task writer already locks, so it meets creation, the Status and Labels edits, both deletions and another move on
/// one lock in one order, and never answers a <c>500</c>, a 1205, a duplicate key or a silently lost move. RACE-M1..M8 of
/// story Task 7; AR-21 with AD-15, AR-34. Written red by the story 2.6 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>The interleaving is the test's, never the clock's</b> (TESTING-CONVENTIONS "A race is controlled, never hoped for",
/// "When the race is about a read, the test does the other write", "A deletion races every writer on its rows, from both
/// sides"). The test plays the competing writer in its own open transaction in the product's statement shape
/// (<see cref="DeleteWire"/>, <see cref="LabelWire"/>, <see cref="MoveWire.HoldMoveAsync"/>), sends the request, waits until
/// it is blocked on any <c>LCK_M_%</c> (<see cref="TaskWire.WaitUntilBlockedOnALockAsync"/>), finishes the competing write,
/// and commits or rolls back. Every racing Account reads the Board first, so no <c>SpacePresence</c> insert serialises the
/// pair before the race (the 2.2 finding).
/// </para>
/// <para>
/// <b>Which side holds what.</b> In the "(a)" cases and M1-M3 the request is the move (skipped <c>red: AC11</c> - the route
/// is unmapped); in the "(b)" cases the test holds the move in SQL - range, then the Task row on its clustered key, then the
/// row alone updated (story Task 3's recommended order) - and the request is an existing writer: a Task deletion, a Project
/// deletion or a Labels edit. <b>The "(b)" cases run from the ATDD run on</b> as standing gates: they prove today that every
/// existing writer waits on the lock shape the move will take, so a move built in that shape cannot be overtaken by one.
/// </para>
/// <para>
/// <b>Plants for the dev phase</b> (three runs each, <c>n/3</c> in the Debug Log, a masked plant recorded with its reason and
/// never deleted): the move's range hint removed (M1, M3); the move's row <c>INDEX(1)</c> removed (M4 (a), M8 (a)); the
/// row's <c>UPDLOCK, HOLDLOCK</c> removed (M5 (a)); the neighbour read moved before the range (M2 - the read race, written
/// the 3/3 way 2.3 found); and 2.5's three masked deletion row hints (<c>deferred-work.md:543</c>, Q15) under M5 (b) and
/// M6 (b).
/// </para>
/// </remarks>
public sealed partial class TaskEndpointTests
{
    /// <summary>
    /// RACE-M1 (AC5, AC11) - the test holds the Project's Task range; two moves into the same gap from two Sessions (the
    /// Owner's and a Member's) both queue; the test commits; both answer <c>200</c> with distinct keys strictly between the
    /// gap's neighbours, and neither is lost: the column reads neighbour, the two moved Tasks, neighbour.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC11 - the move route (Q1 (a)) is not mapped yet; RACE-M1 needs two moves to queue on the range")]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "AR-21")]
    [Trait("Requirement", "AD-15")]
    public async Task Two_moves_into_one_gap_queued_on_the_range_both_land_with_distinct_keys()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        MoveBoard? board = null;
        var keys = new Dictionary<Guid, string>();
        WireResponse[] answers = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");

            board = await MoveBoardAsync(database, owner, todo: 2, doing: 2);
            keys = (await PlacementsAsync(database, owner.SpaceId, board.ProjectId)).ToDictionary(row => row.Id, row => row.PositionKey);

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, board.ProjectId)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(member, owner.SpaceId, board.ProjectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await HoldProjectRangeAsync(holder, held, board.ProjectId, cancellationToken);

            var racing = Task.WhenAll(
                MoveTaskAsync(owner, owner.SpaceId, board.Todo[0], board.Doing, board.InDoing[0]),
                MoveTaskAsync(member, owner.SpaceId, board.Todo[1], board.Doing, board.InDoing[0]));

            await WaitUntilBlockedOnALockAsync(observer, 2, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answers = await racing;
        });

        Assert.NotNull(owner);
        Assert.NotNull(board);
        Assert.All(answers, answer => Assert.True(answer.StatusCode == HttpStatusCode.OK, $"{answer.StatusCode} {answer.Body}"));

        var placed = answers.Select(answer => PlacementOf(answer).PositionKey).ToList();

        Assert.NotEqual(placed[0], placed[1]);
        Assert.All(placed, key => AssertStrictlyBetween(keys[board.InDoing[0]], key, keys[board.InDoing[1]]));

        var column = await ColumnAsync(database, owner.SpaceId, board.ProjectId, board.Doing);

        Assert.Equal(4, column.Count);
        Assert.Equal(board.InDoing[0], column[0]);
        Assert.Equal(board.InDoing[1], column[3]);
        Assert.Equal(new[] { board.Todo[0], board.Todo[1] }.Order(), column.Skip(1).Take(2).Order());
    }

    /// <summary>
    /// RACE-M2 (AC11, the read race - written the way 2.3's
    /// <c>A_Status_edit_reads_the_last_key_of_its_new_column_only_under_the_lock</c> caught 3/3) - the test holds the range;
    /// a move "after X" queues; the test then places a Task C immediately after X (between X and X's old successor Y) and
    /// commits. The move must land strictly between X and C - its <b>current</b> neighbours - so a move that read X's
    /// successor before the lock (and so computed a key between X and Y) lands after C and fails here, or collides with it
    /// (2601, a <c>500</c>). No duplicate key, no <c>500</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC11 - the move route (Q1 (a)) is not mapped yet")]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "AR-21")]
    [Trait("Requirement", "AD-15")]
    public async Task A_move_reads_its_neighbours_keys_only_under_the_lock()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        MoveBoard? board = null;
        var placed = Guid.NewGuid();
        var keyX = string.Empty;
        var keyC = string.Empty;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            board = await MoveBoardAsync(database, owner, todo: 1, doing: 2);

            var keys = (await PlacementsAsync(database, owner.SpaceId, board.ProjectId)).ToDictionary(row => row.Id, row => row.PositionKey);

            keyX = keys[board.InDoing[0]];
            keyC = KeyJustAfter(keyX, keys[board.InDoing[1]]);

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, board.ProjectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await HoldProjectRangeAsync(holder, held, board.ProjectId, cancellationToken);

            var racing = MoveTaskAsync(owner, owner.SpaceId, board.Todo[0], board.Doing, board.InDoing[0]);

            await WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);

            // The other write: a Task placed right after X, under the range the test holds.
            await InsertTaskAtAsync(holder, held, new TaskPlacement(placed, owner.SpaceId, board.ProjectId, board.Doing, keyC), "Placed by the test", cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.NotNull(board);
        Assert.True(answer?.StatusCode == HttpStatusCode.OK, $"{answer?.StatusCode} {answer?.Body}");
        AssertStrictlyBetween(keyX, PlacementOf(answer!).PositionKey, keyC);
        Assert.Equal(
            [board.InDoing[0], board.Todo[0], placed, board.InDoing[1]],
            await ColumnAsync(database, owner.SpaceId, board.ProjectId, board.Doing));
    }

    /// <summary>
    /// RACE-M3 (AC11) - the test holds the Project's Task range (a creation's bound count); a creation in the first column
    /// by a Member and a move of a second-column Task to the bottom of the first column by the Owner both queue; the test
    /// commits; both are admitted (<c>201</c>, <c>200</c>) with distinct keys in that column, in whichever order the range
    /// serves them.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC11 - the move route (Q1 (a)) is not mapped yet")]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "AR-30")]
    public async Task A_creation_and_a_move_into_one_column_both_succeed_with_distinct_keys()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        MoveBoard? board = null;
        WireResponse[] answers = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");

            board = await MoveBoardAsync(database, owner, todo: 1, doing: 1);

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, board.ProjectId)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(member, owner.SpaceId, board.ProjectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await HoldProjectRangeAsync(holder, held, board.ProjectId, cancellationToken);

            var racing = Task.WhenAll(
                CreateTaskAsync(member, owner.SpaceId, board.ProjectId, "Arriving"),
                MoveTaskAsync(owner, owner.SpaceId, board.InDoing[0], board.Todo0Status, board.Todo[0]));

            await WaitUntilBlockedOnALockAsync(observer, 2, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answers = await racing;
        });

        Assert.NotNull(owner);
        Assert.NotNull(board);
        Assert.True(answers[0].StatusCode == HttpStatusCode.Created, $"{answers[0].StatusCode} {answers[0].Body}");
        Assert.True(answers[1].StatusCode == HttpStatusCode.OK, $"{answers[1].StatusCode} {answers[1].Body}");

        var column = (await TaskRowsAsync(database, owner.SpaceId)).Where(row => row.ProjectId == board.ProjectId && row.StatusId == board.Todo0Status).ToList();

        Assert.Equal(3, column.Count);
        Assert.Equal(3, column.Select(row => row.PositionKey).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// RACE-M4 (a) (AC11) - the test holds a Status edit of the Task in the product's shape (the range, the Task row on its
    /// clustered key, then the row moved to the empty second column); a move of the same Task to the top of the third
    /// column queues; the test commits; the move answers <c>200</c> and the Task ends in the third column - the later writer
    /// wins by its own outcome. A move that took the row before the range deadlocks with the edit (a 1205 here or a
    /// <c>500</c> there).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC11 - the move route (Q1 (a)) is not mapped yet")]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-20")]
    public async Task A_move_queued_behind_a_Status_edit_of_the_same_Task_lands_after_it_commits()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        MoveBoard? board = null;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            board = await MoveBoardAsync(database, owner, todo: 2, doing: 0);

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, board.ProjectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await HoldMoveAsync(holder, held, board.ProjectId, board.Todo[0], board.Doing, PositionKey.First, cancellationToken);

            var racing = MoveTaskAsync(owner, owner.SpaceId, board.Todo[0], board.Done, afterTaskId: null);

            await WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.NotNull(board);
        Assert.True(answer?.StatusCode == HttpStatusCode.OK, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal(board.Done, (await RowAsync(database, owner.SpaceId, board.Todo[0]))?.StatusId);
    }

    /// <summary>
    /// RACE-M5 (a) (AC11) - the test holds a deletion of the Task, uncommitted (range, row, delete); a move of it queues; the
    /// test commits; the move answers <c>404 not-available</c> - never <c>200</c> on a Task that is gone (the 2.3 review
    /// finding, repeated for the move), never a <c>500</c> - and no row is written back.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC11 - the move route (Q1 (a)) is not mapped yet")]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-23")]
    public async Task A_move_queued_behind_the_deletion_of_its_Task_answers_404()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        MoveBoard? board = null;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            board = await MoveBoardAsync(database, owner, todo: 2, doing: 0);

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, board.ProjectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteTaskRowAsync(holder, held, board.ProjectId, board.Todo[0], cancellationToken);

            var racing = MoveTaskAsync(owner, owner.SpaceId, board.Todo[0], board.Doing, afterTaskId: null);

            await WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.NotNull(board);
        Assert.True(answer?.StatusCode == HttpStatusCode.NotFound, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal(ProblemTypes.NotAvailable, Problem(answer!)?.Type);
        Assert.Equal([board.Todo[1]], (await TaskRowsAsync(database, owner.SpaceId)).Where(row => row.ProjectId == board.ProjectId).Select(row => row.Id));
    }

    /// <summary>
    /// RACE-M5 (b) (AC11, Q15) - the test holds the move of a Task in the product's shape (range, row, update); a deletion
    /// of that Task queues on the range; the test commits; the deletion answers <c>204</c> and the Task is gone. <b>Green
    /// today</b> - the deletion takes the range first (2.5) - and the standing gate under which story Task 7 plants 2.5's
    /// three masked deletion row hints (<c>deferred-work.md:543</c>).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-23")]
    public async Task A_deletion_queued_behind_a_move_of_its_Task_answers_204_once_the_move_commits()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        MoveBoard? board = null;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            board = await MoveBoardAsync(database, owner, todo: 2, doing: 0);

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, board.ProjectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await HoldMoveAsync(holder, held, board.ProjectId, board.Todo[0], board.Doing, PositionKey.First, cancellationToken);

            var racing = DeleteTaskAsync(owner, owner.SpaceId, board.Todo[0]);

            await WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.NotNull(board);
        Assert.True(answer?.StatusCode == HttpStatusCode.NoContent, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal([board.Todo[1]], (await TaskRowsAsync(database, owner.SpaceId)).Where(row => row.ProjectId == board.ProjectId).Select(row => row.Id));
    }

    /// <summary>
    /// RACE-M6 (a) (AC11, Q3 (a)) - the test holds a deletion of the <b>neighbour</b> X, uncommitted; a move "after X"
    /// queues; the test commits; the move answers <c>409 task-position-unavailable</c>, and the moved Task is unchanged - never
    /// placed after a Task that is gone, never a <c>500</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC11 - the move route (Q1 (a)) and Q3's task-position-unavailable do not exist yet")]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-29")]
    public async Task A_move_queued_behind_the_deletion_of_its_neighbour_is_refused_409_and_writes_nothing()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        MoveBoard? board = null;
        TaskRow? before = null;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            board = await MoveBoardAsync(database, owner, todo: 1, doing: 2);
            before = await RowAsync(database, owner.SpaceId, board.Todo[0]);

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, board.ProjectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteTaskRowAsync(holder, held, board.ProjectId, board.InDoing[0], cancellationToken);

            var racing = MoveTaskAsync(owner, owner.SpaceId, board.Todo[0], board.Doing, board.InDoing[0]);

            await WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.NotNull(board);
        Assert.True(answer?.StatusCode == HttpStatusCode.Conflict, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal(TaskPositionUnavailable, Problem(answer!)?.Type);
        Assert.Equal(before, await RowAsync(database, owner.SpaceId, board.Todo[0]));
    }

    /// <summary>
    /// RACE-M6 (b) (AC11, Q15) - the test holds a move of A to just after X (range, row, update); a deletion of X queues; the
    /// test commits; the deletion answers <c>204</c>, and A stays where the move put it. <b>Green today</b>; a standing gate
    /// and the second home for the masked-hint plants (<c>deferred-work.md:543</c>).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-23")]
    public async Task A_deletion_of_the_neighbour_queued_behind_a_move_answers_204_and_the_move_stands()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        MoveBoard? board = null;
        var placedKey = string.Empty;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            board = await MoveBoardAsync(database, owner, todo: 1, doing: 2);

            var keys = (await PlacementsAsync(database, owner.SpaceId, board.ProjectId)).ToDictionary(row => row.Id, row => row.PositionKey);

            placedKey = KeyJustAfter(keys[board.InDoing[0]], keys[board.InDoing[1]]);

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, board.ProjectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await HoldMoveAsync(holder, held, board.ProjectId, board.Todo[0], board.Doing, placedKey, cancellationToken);

            var racing = DeleteTaskAsync(owner, owner.SpaceId, board.InDoing[0]);

            await WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.NotNull(board);
        Assert.True(answer?.StatusCode == HttpStatusCode.NoContent, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal([board.Todo[0], board.InDoing[1]], await ColumnAsync(database, owner.SpaceId, board.ProjectId, board.Doing));
        Assert.Equal(placedKey, await KeyOfAsync(database, owner.SpaceId, board.Todo[0]));
    }

    /// <summary>
    /// RACE-M7 (a) (AC11) - the test holds a deletion of the Task's Project (row, range, delete - its Tasks cascade); a move
    /// of one of its Tasks queues; the test commits; the move answers <c>404 not-available</c>, never a <c>500</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC11 - the move route (Q1 (a)) is not mapped yet")]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-17")]
    public async Task A_move_queued_behind_the_deletion_of_its_Project_answers_404()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        MoveBoard? board = null;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            board = await MoveBoardAsync(database, owner, todo: 2, doing: 0);

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, board.ProjectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteProjectRowAsync(holder, held, board.ProjectId, cancellationToken);

            var racing = MoveTaskAsync(owner, owner.SpaceId, board.Todo[1], board.Todo0Status, afterTaskId: null);

            await WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.NotNull(board);
        Assert.True(answer?.StatusCode == HttpStatusCode.NotFound, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal(ProblemTypes.NotAvailable, Problem(answer!)?.Type);
        Assert.DoesNotContain((await TaskRowsAsync(database, owner.SpaceId)), row => row.ProjectId == board.ProjectId);
    }

    /// <summary>
    /// RACE-M7 (b) (AC11) - the test holds a move of a Task in the product's shape; a deletion of its Project queues on the
    /// range; the test commits; the deletion answers <c>204</c> and the Project's Tasks are gone with it. <b>Green
    /// today</b>; a standing gate.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-17")]
    public async Task A_Project_deletion_queued_behind_a_move_answers_204_once_the_move_commits()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        MoveBoard? board = null;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            board = await MoveBoardAsync(database, owner, todo: 2, doing: 0);

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, board.ProjectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await HoldMoveAsync(holder, held, board.ProjectId, board.Todo[0], board.Doing, PositionKey.First, cancellationToken);

            var racing = DeleteProjectAsync(owner, owner.SpaceId, board.ProjectId);

            await WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.NotNull(board);
        Assert.True(answer?.StatusCode == HttpStatusCode.NoContent, $"{answer?.StatusCode} {answer?.Body}");
        Assert.DoesNotContain((await TaskRowsAsync(database, owner.SpaceId)), row => row.ProjectId == board.ProjectId);
    }

    /// <summary>
    /// RACE-M8 (a) (AC11) - the test holds a Labels edit of the Task (its row on the clustered key and its <c>TaskLabel</c>
    /// range) and applies a Label; a move of that Task takes the range, then queues on the row; the test commits; the move
    /// answers <c>200</c> and the application stays. Never a 1205 - the 2.4 deadlock was two lockers on two indexes of one
    /// row, which <c>INDEX(1)</c> on both prevents.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC11 - the move route (Q1 (a)) is not mapped yet")]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-22")]
    public async Task A_move_queued_behind_a_Labels_edit_of_the_same_Task_answers_200_and_keeps_the_application()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        MoveBoard? board = null;
        var labelId = Guid.Empty;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            board = await MoveBoardAsync(database, owner, todo: 2, doing: 0);
            labelId = await SeedAsync(owner, owner.SpaceId, "Applied in the race");

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, board.ProjectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await HoldTaskLabelsAsync(holder, held, owner.SpaceId, board.Todo[0], cancellationToken);

            var racing = MoveTaskAsync(owner, owner.SpaceId, board.Todo[0], board.Doing, afterTaskId: null);

            await WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await InsertApplicationAsync(holder, held, owner.SpaceId, board.Todo[0], labelId, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.NotNull(board);
        Assert.True(answer?.StatusCode == HttpStatusCode.OK, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal(board.Doing, (await RowAsync(database, owner.SpaceId, board.Todo[0]))?.StatusId);
        Assert.Equal([labelId], await AppliedToAsync(database, owner.SpaceId, board.Todo[0]));
    }

    /// <summary>
    /// RACE-M8 (b) (AC11) - the test holds a move of the Task in the product's shape; a Labels edit applying a Label to it
    /// queues on the row; the test commits; the edit answers <c>200</c>, the Label is applied and the Task is where the
    /// move put it. <b>Green today</b>; a standing gate.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-22")]
    public async Task A_Labels_edit_queued_behind_a_move_of_the_same_Task_answers_200_once_the_move_commits()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        MoveBoard? board = null;
        var labelId = Guid.Empty;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            board = await MoveBoardAsync(database, owner, todo: 2, doing: 0);
            labelId = await SeedAsync(owner, owner.SpaceId, "Applied after the move");

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, board.ProjectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await HoldMoveAsync(holder, held, board.ProjectId, board.Todo[0], board.Doing, PositionKey.First, cancellationToken);

            var racing = EditTaskAsync(owner, owner.SpaceId, board.Todo[0], ApplyBody(labelId));

            await WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.NotNull(board);
        Assert.True(answer?.StatusCode == HttpStatusCode.OK, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal([labelId], await AppliedToAsync(database, owner.SpaceId, board.Todo[0]));
        Assert.Equal(board.Doing, (await RowAsync(database, owner.SpaceId, board.Todo[0]))?.StatusId);
    }
}
