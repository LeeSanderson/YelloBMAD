using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Domain;
using Yello.Domain.Bounds;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;
using static Yello.Tests.Slices.Tasks.DeleteWire;
using static Yello.Tests.Slices.Tasks.MoveWire;
using static Yello.Tests.Slices.Tasks.TaskEditWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Story 2.6 over the wire: moving a Task to a chosen Status and position. FR-29, FR-35's position clause, AC3, AC5, AC6,
/// AC8, AC9, AC10's refusal half; AR-21 with AD-15, AD-3, AD-20, AR-10, AR-11, AR-34, NFR-7. Written red by the story 2.6
/// ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Decisions these cases rest on</b> (story Open questions, recommended options): Q1 (a) - <c>POST
/// /spaces/{spaceId}/tasks/{taskId}/move</c>, Session-only; Q2 (a) - the body is <c>{ statusId, afterTaskId }</c>
/// (<c>null</c> = top), never a key, and a <c>positionKey</c> member is refused <c>400</c>; Q3 (a) - a neighbour that is not
/// one is <c>409 task-position-unavailable</c>, byte-identical for a random and a foreign id, unrecorded; Q10 - an exhausted
/// gap is the same refusal; Q14 - the Status <c>PATCH</c> still appends; Q17 - <c>ManageTasks</c>; Q18 - a move to where the
/// Task already is answers <c>200</c> and writes nothing. If one is decided otherwise, correct the expectation and record
/// it in the Debug Log.
/// </para>
/// <para>
/// <b>Every case whose request is a move is skipped</b> <c>red: AC&lt;n&gt;</c>: the route is unmapped today and answers the
/// unmatched <c>404</c>. The three API-narrowness cases whose requests are 2.2's and 2.3's routes (a <c>PATCH</c> carrying
/// <c>positionKey</c>, and the Board and Task reads over a Token) pass today and run as standing gates of FR-35.
/// </para>
/// <para>
/// <b>AC3's sibling check compares every other Task's (id, Status, key) byte for byte</b> before and after, in both
/// columns: a move writes the moved Task's key alone, never a renumber (AD-15). Every <c>4xx</c> other than the
/// <c>403</c> asserts no refusal row (AD-20).
/// </para>
/// </remarks>
public sealed partial class TaskEndpointTests
{
    /// <summary>
    /// MOV-01 (AC9, AC3, AC6, FR-29) - an Owner, Admin or Member moves a Task from the first column to between two Tasks
    /// of the second: <c>200</c> with the Task as stored, its <c>statusId</c> the destination and its <c>positionKey</c>
    /// strictly between its new neighbours'; the Board lists it there; every other Task's Status and key are byte-identical
    /// before and after, in both columns; both columns' true totals moved by one; and a second Account's read of the Board
    /// (a reload by someone else) shows the same order. Nothing is recorded as a refusal.
    /// </summary>
    /// <param name="role">The mover's Role in the Owner's Space.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("Owner")]
    [InlineData("Admin")]
    [InlineData("Member")]
    [Trait("Requirement", "FR-29")]
    [Trait("Requirement", "AR-21")]
    [Trait("Requirement", "AD-15")]
    public async Task An_Owner_Admin_or_Member_moves_a_Task_between_two_Tasks_of_another_column_and_only_it_is_written(string role)
    {
        var database = Available();
        Actor? owner = null;
        Actor? mover = null;
        MoveBoard? board = null;
        IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)> before = [];
        IReadOnlyList<(Guid StatusId, int Total, IReadOnlyList<Guid> TaskIds)> columnsBefore = [];
        IReadOnlyList<(Guid StatusId, int Total, IReadOnlyList<Guid> TaskIds)> columnsAfter = [];
        IReadOnlyList<(Guid StatusId, int Total, IReadOnlyList<Guid> TaskIds)> reloaded = [];
        WireResponse? moved = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            mover = owner;

            if (role != "Owner")
            {
                mover = await RegisterAsync(client, database);
                await AddMemberAsync(database, owner.SpaceId, mover.AccountId, role);
            }

            var reader = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, reader.AccountId, "Viewer");

            board = await MoveBoardAsync(database, owner, todo: 3, doing: 2);
            before = await PlacementsAsync(database, owner.SpaceId, board.ProjectId);
            columnsBefore = Columns(await BoardAsync(owner, owner.SpaceId, board.ProjectId));

            moved = await MoveTaskAsync(mover, owner.SpaceId, board.Todo[1], board.Doing, afterTaskId: board.InDoing[0]);

            columnsAfter = Columns(await BoardAsync(owner, owner.SpaceId, board.ProjectId));
            reloaded = Columns(await BoardAsync(reader, owner.SpaceId, board.ProjectId));
        });

        Assert.NotNull(owner);
        Assert.NotNull(mover);
        Assert.NotNull(board);
        Assert.True(moved?.StatusCode == HttpStatusCode.OK, $"{moved?.StatusCode} {moved?.Body}");

        var (statusId, key) = PlacementOf(moved!);
        var keys = before.ToDictionary(row => row.Id, row => row.PositionKey);

        Assert.Equal(board.Doing, statusId);
        AssertStrictlyBetween(keys[board.InDoing[0]], key, keys[board.InDoing[1]]);

        // The Board: the Task is where it was asked to go, and nowhere else.
        Assert.Equal([board.Todo[0], board.Todo[2]], columnsAfter.Single(column => column.StatusId == board.Todo0Status).TaskIds);
        Assert.Equal([board.InDoing[0], board.Todo[1], board.InDoing[1]], columnsAfter.Single(column => column.StatusId == board.Doing).TaskIds);
        Assert.Equal(columnsBefore.Single(column => column.StatusId == board.Todo0Status).Total - 1, columnsAfter.Single(column => column.StatusId == board.Todo0Status).Total);
        Assert.Equal(columnsBefore.Single(column => column.StatusId == board.Doing).Total + 1, columnsAfter.Single(column => column.StatusId == board.Doing).Total);

        // AC3, AD-15: every sibling, in both columns, keeps its Status and its key byte for byte.
        var after = await PlacementsAsync(database, owner.SpaceId, board.ProjectId);

        Assert.Equal(before.Where(row => row.Id != board.Todo[1]), after.Where(row => row.Id != board.Todo[1]));

        // AC6: someone else's read of the Board is the same order.
        Assert.Equal(columnsAfter.Select(column => column.TaskIds), reloaded.Select(column => column.TaskIds));
        Assert.Empty(await RefusalKindsAsync(database, mover.AccountId, owner.SpaceId));
    }

    /// <summary>
    /// MOV-02 (AC9, AC3, FR-29 "reorder within one") - within one column of four, the last Task moved to the top, the last
    /// moved to between the first and second, and the first moved to the bottom each answer <c>200</c>, read back in exactly
    /// the asked order, and leave every other key untouched.
    /// </summary>
    /// <param name="placement">Where in the column the Task is moved: <c>top</c>, <c>between</c> or <c>bottom</c>.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("top")]
    [InlineData("between")]
    [InlineData("bottom")]
    [Trait("Requirement", "FR-29")]
    [Trait("Requirement", "AR-21")]
    [Trait("Requirement", "AD-15")]
    public async Task A_Task_reordered_within_its_column_reads_back_exactly_where_it_was_put(string placement)
    {
        var database = Available();
        Actor? owner = null;
        MoveBoard? board = null;
        IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)> before = [];
        WireResponse? moved = null;
        Guid[] expected = [];
        var movedId = Guid.Empty;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            board = await MoveBoardAsync(database, owner, todo: 4, doing: 0);
            before = await PlacementsAsync(database, owner.SpaceId, board.ProjectId);

            var (a, b, c, d) = (board.Todo[0], board.Todo[1], board.Todo[2], board.Todo[3]);

            Guid? neighbour = null;

            if (placement == "top")
            {
                (movedId, expected) = (d, [d, a, b, c]);
            }
            else if (placement == "between")
            {
                (movedId, neighbour, expected) = (d, a, [a, d, b, c]);
            }
            else
            {
                (movedId, neighbour, expected) = (a, d, [b, c, d, a]);
            }

            moved = await MoveTaskAsync(owner, owner.SpaceId, movedId, board.Todo0Status, neighbour);
        });

        Assert.NotNull(owner);
        Assert.NotNull(board);
        Assert.True(moved?.StatusCode == HttpStatusCode.OK, $"{moved?.StatusCode} {moved?.Body}");
        Assert.Equal(expected, await ColumnAsync(database, owner.SpaceId, board.ProjectId, board.Todo0Status));

        var afterMove = await PlacementsAsync(database, owner.SpaceId, board.ProjectId);

        Assert.Equal(before.Where(row => row.Id != movedId), afterMove.Where(row => row.Id != movedId));
    }

    /// <summary>
    /// MOV-03 (AC5, Q2 (a)) - two Members of one Space move two Tasks after the same neighbour, one after the other: both
    /// <c>200</c>, both kept (neither silently discarded), with distinct keys strictly between the neighbour and its old
    /// successor; the later one lands nearer the neighbour; and both Accounts read one order. RACE-M1 is the deterministic,
    /// concurrent version.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-29")]
    [Trait("Requirement", "AR-21")]
    public async Task Two_Members_moving_after_the_same_neighbour_both_land_and_read_one_order()
    {
        var database = Available();
        Actor? owner = null;
        MoveBoard? board = null;
        WireResponse? first = null;
        WireResponse? second = null;
        IReadOnlyList<Guid> byOwner = [];
        IReadOnlyList<Guid> byMember = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");

            board = await MoveBoardAsync(database, owner, todo: 2, doing: 2);

            first = await MoveTaskAsync(owner, owner.SpaceId, board.Todo[0], board.Doing, board.InDoing[0]);
            second = await MoveTaskAsync(member, owner.SpaceId, board.Todo[1], board.Doing, board.InDoing[0]);

            byOwner = Columns(await BoardAsync(owner, owner.SpaceId, board.ProjectId)).Single(column => column.StatusId == board.Doing).TaskIds;
            byMember = Columns(await BoardAsync(member, owner.SpaceId, board.ProjectId)).Single(column => column.StatusId == board.Doing).TaskIds;
        });

        Assert.NotNull(owner);
        Assert.NotNull(board);
        Assert.True(first?.StatusCode == HttpStatusCode.OK, $"{first?.StatusCode} {first?.Body}");
        Assert.True(second?.StatusCode == HttpStatusCode.OK, $"{second?.StatusCode} {second?.Body}");
        Assert.NotEqual(PlacementOf(first!).PositionKey, PlacementOf(second!).PositionKey);
        Assert.Equal([board.InDoing[0], board.Todo[1], board.Todo[0], board.InDoing[1]], byOwner);
        Assert.Equal(byOwner, byMember);
    }

    /// <summary>
    /// MOV-04 (AC9, AC2's API half, V-1, NFR-7, Q17) - a Viewer's move is refused <c>403 insufficient-role</c> naming
    /// <c>manage-tasks</c>, one <c>InsufficientRole</c> row is written, and nothing changes. Anchor: a Member's identical
    /// move answers <c>200</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-29")]
    [Trait("Requirement", "AR-11")]
    [Trait("Requirement", "NFR-7")]
    public async Task A_Viewer_is_refused_403_manage_tasks_and_one_refusal_is_recorded_and_nothing_moves()
    {
        var database = Available();
        Actor? owner = null;
        Actor? viewer = null;
        MoveBoard? board = null;
        IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)> before = [];
        IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)> afterRefusal = [];
        WireResponse? refused = null;
        WireResponse? anchor = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            viewer = await RegisterAsync(client, database);
            var member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, viewer.AccountId, "Viewer");
            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");

            board = await MoveBoardAsync(database, owner, todo: 2, doing: 0);
            before = await PlacementsAsync(database, owner.SpaceId, board.ProjectId);

            refused = await MoveTaskAsync(viewer, owner.SpaceId, board.Todo[1], board.Todo0Status, afterTaskId: null);
            afterRefusal = await PlacementsAsync(database, owner.SpaceId, board.ProjectId);
            anchor = await MoveTaskAsync(member, owner.SpaceId, board.Todo[1], board.Todo0Status, afterTaskId: null);
        });

        Assert.NotNull(owner);
        Assert.NotNull(viewer);
        Assert.True(refused?.StatusCode == HttpStatusCode.Forbidden, $"{refused?.StatusCode} {refused?.Body}");
        Assert.Equal(ProblemTypes.InsufficientRole, Problem(refused!)?.Type);
        Assert.Equal(StableCode.Of(Capability.ManageTasks), Problem(refused!)?.Capability);
        Assert.Equal([InsufficientRoleKind], await RefusalKindsAsync(database, viewer.AccountId, owner.SpaceId));
        Assert.Equal(before, afterRefusal);
        Assert.Equal(HttpStatusCode.OK, anchor?.StatusCode);
    }

    /// <summary>
    /// MOV-05 (AC9, AD-3, AD-20) - a random Task id, a deleted one and another Space's real Task id, each moved in one's
    /// own Space, answer the byte-identical floored <c>404 not-available</c> of a Space that does not exist, and record
    /// nothing; the other Space's Task is not moved.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-20")]
    [Trait("Requirement", "AR-10")]
    public async Task An_absent_deleted_or_foreign_Task_answers_the_identical_404_and_records_nothing_when_moved()
    {
        var database = Available();
        Actor? owner = null;
        Actor? stranger = null;
        var answers = new List<WireResponse>();
        WireResponse? anchor = null;
        IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)> theirsBefore = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            stranger = await RegisterAsync(client, database);

            var statuses = await StatusesAsync(database, owner.SpaceId);
            var (_, deleted) = await ProjectWithTaskAsync(owner, "Deleted, then moved");
            var (theirProject, foreign) = await ProjectWithTaskAsync(stranger, "Someone else's");

            theirsBefore = await PlacementsAsync(database, stranger.SpaceId, theirProject);

            Assert.Equal(HttpStatusCode.NoContent, (await DeleteTaskAsync(owner, owner.SpaceId, deleted)).StatusCode);

            answers.Add(await MoveTaskAsync(owner, owner.SpaceId, Guid.NewGuid(), statuses[1].Id, afterTaskId: null));
            answers.Add(await MoveTaskAsync(owner, owner.SpaceId, deleted, statuses[1].Id, afterTaskId: null));
            answers.Add(await MoveTaskAsync(owner, owner.SpaceId, foreign, statuses[1].Id, afterTaskId: null));

            // Anchor: a Space that does not exist - the boundary's own 404.
            anchor = await MoveTaskAsync(owner, Guid.NewGuid(), Guid.NewGuid(), statuses[1].Id, afterTaskId: null);

            Assert.Equal(theirsBefore, await PlacementsAsync(database, stranger.SpaceId, theirProject));
        });

        Assert.NotNull(owner);
        Assert.NotNull(stranger);
        Assert.NotNull(anchor);
        Assert.Equal(HttpStatusCode.NotFound, anchor.StatusCode);

        Assert.All(answers, answer =>
        {
            Assert.Equal(anchor.StatusCode, answer.StatusCode);
            Assert.Equal(anchor.ContentType, answer.ContentType);
            Assert.Equal(anchor.Body, answer.Body);
            Assert.Equal(anchor.HeaderNames, answer.HeaderNames);
            Assert.Equal(ProblemTypes.NotAvailable, Problem(answer)?.Type);
        });

        Assert.Empty(await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId));
    }

    /// <summary>
    /// MOV-06 (AC9, I-13, 2.3's type) - a destination Status outside the Task's Project's effective set - a random id, and
    /// another Space's real Status - answers <c>400 invalid-task-status</c>, the two byte-identical, with no refusal row,
    /// and nothing moves.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "AR-24")]
    public async Task A_move_to_a_Status_outside_the_effective_set_is_refused_400_identically_for_a_random_and_a_foreign_id()
    {
        var database = Available();
        Actor? owner = null;
        MoveBoard? board = null;
        IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)> before = [];
        WireResponse? random = null;
        WireResponse? foreign = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var stranger = await RegisterAsync(client, database);
            var theirStatus = (await StatusesAsync(database, stranger.SpaceId))[1].Id;

            board = await MoveBoardAsync(database, owner, todo: 2, doing: 0);
            before = await PlacementsAsync(database, owner.SpaceId, board.ProjectId);

            random = await MoveTaskAsync(owner, owner.SpaceId, board.Todo[0], Guid.NewGuid(), afterTaskId: null);
            foreign = await MoveTaskAsync(owner, owner.SpaceId, board.Todo[0], theirStatus, afterTaskId: null);
        });

        Assert.NotNull(owner);
        Assert.NotNull(board);
        Assert.True(random?.StatusCode == HttpStatusCode.BadRequest, $"{random?.StatusCode} {random?.Body}");
        Assert.Equal(ProblemTypes.InvalidTaskStatus, Problem(random!)?.Type);
        Assert.Equal(random!.StatusCode, foreign?.StatusCode);
        Assert.Equal(random.Body, foreign!.Body);
        Assert.Empty(await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId));
        Assert.Equal(before, await PlacementsAsync(database, owner.SpaceId, board.ProjectId));
    }

    /// <summary>
    /// MOV-07 (AC9, Q3 (a), I-13) - a neighbour that is not a Task in the destination column of the moved Task's Project
    /// answers <c>409 task-position-unavailable</c>, never a <c>500</c>, records nothing and moves nothing: a random id,
    /// another Space's real Task, a Task in another Project of the same Space, a Task in another column, a deleted Task,
    /// and the moved Task itself. The random and the foreign id answer the same bytes.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-29")]
    [Trait("Requirement", "AR-34")]
    public async Task A_neighbour_that_is_not_one_is_refused_409_and_nothing_moves()
    {
        var database = Available();
        Actor? owner = null;
        MoveBoard? board = null;
        IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)> before = [];
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var stranger = await RegisterAsync(client, database);
            var (_, foreign) = await ProjectWithTaskAsync(stranger, "Someone else's neighbour");
            var (otherProject, inOtherProject) = await ProjectWithTaskAsync(owner, "In another Project");

            board = await MoveBoardAsync(database, owner, todo: 3, doing: 1);

            var deleted = IdOf(await CreateTaskAsync(owner, owner.SpaceId, board.ProjectId, "Deleted neighbour"));

            Assert.Equal(HttpStatusCode.NoContent, (await DeleteTaskAsync(owner, owner.SpaceId, deleted)).StatusCode);

            before = await PlacementsAsync(database, owner.SpaceId, board.ProjectId);

            var moved = board.Todo[0];

            // Destination: the second column. Every neighbour below is not a Task of that column of this Project.
            answers["random"] = await MoveTaskAsync(owner, owner.SpaceId, moved, board.Doing, Guid.NewGuid());
            answers["foreign"] = await MoveTaskAsync(owner, owner.SpaceId, moved, board.Doing, foreign);
            answers["other Project"] = await MoveTaskAsync(owner, owner.SpaceId, moved, board.Doing, inOtherProject);
            answers["other column"] = await MoveTaskAsync(owner, owner.SpaceId, moved, board.Doing, board.Todo[1]);
            answers["deleted"] = await MoveTaskAsync(owner, owner.SpaceId, moved, board.Doing, deleted);
            answers["itself"] = await MoveTaskAsync(owner, owner.SpaceId, moved, board.Todo0Status, moved);

            Assert.NotEqual(Guid.Empty, otherProject);
        });

        Assert.NotNull(owner);
        Assert.NotNull(board);

        Assert.All(answers, pair =>
        {
            Assert.True(pair.Value.StatusCode == HttpStatusCode.Conflict, $"{pair.Key}: {pair.Value.StatusCode} {pair.Value.Body}");
            Assert.Equal(TaskPositionUnavailable, Problem(pair.Value)?.Type);
        });

        Assert.Equal(answers["random"].Body, answers["foreign"].Body);
        Assert.Equal(answers["random"].HeaderNames, answers["foreign"].HeaderNames);
        Assert.Empty(await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId));
        Assert.Equal(before, await PlacementsAsync(database, owner.SpaceId, board.ProjectId));
    }

    /// <summary>
    /// MOV-08 (AC9, Q2 (a)) - an unreadable body, a body with no <c>statusId</c>, a body with no <c>afterTaskId</c> (the
    /// position has no default), a <c>statusId</c> that is not an id, and a body carrying a <c>positionKey</c> member each
    /// answer a documented <c>400</c> problem, never a <c>500</c>, and nothing moves: a client can never believe it wrote a
    /// key.
    /// </summary>
    /// <param name="shape">Which malformed body is sent.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("unreadable")]
    [InlineData("no statusId")]
    [InlineData("no afterTaskId")]
    [InlineData("statusId not an id")]
    [InlineData("positionKey")]
    [Trait("Requirement", "FR-35")]
    [Trait("Requirement", "AR-21")]
    public async Task A_malformed_move_or_one_naming_a_position_key_is_a_400_and_nothing_moves(string shape)
    {
        var database = Available();
        Actor? owner = null;
        MoveBoard? board = null;
        IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)> before = [];
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            board = await MoveBoardAsync(database, owner, todo: 2, doing: 0);
            before = await PlacementsAsync(database, owner.SpaceId, board.ProjectId);

            var status = board.Todo0Status.ToString("D");
            var body = shape switch
            {
                "unreadable" => "{ \"statusId\": ",
                "no statusId" => "{ \"afterTaskId\": null }",
                "no afterTaskId" => $$"""{ "statusId": "{{status}}" }""",
                "statusId not an id" => "{ \"statusId\": \"not-an-id\", \"afterTaskId\": null }",
                _ => $$"""{ "statusId": "{{status}}", "afterTaskId": null, "positionKey": "0" }""",
            };

            answer = await MoveTaskAsync(owner, owner.SpaceId, board.Todo[1], RawBody(body));
        });

        Assert.NotNull(owner);
        Assert.NotNull(board);
        Assert.True(answer?.StatusCode == HttpStatusCode.BadRequest, $"{shape}: {answer?.StatusCode} {answer?.Body}");
        Assert.StartsWith("urn:yello:problem:", Problem(answer!)?.Type ?? string.Empty, StringComparison.Ordinal);

        if (shape == "no afterTaskId")
        {
            Assert.Equal(ProblemTypes.InvalidTaskEdit, Problem(answer!)?.Type);
        }

        Assert.Equal(before, await PlacementsAsync(database, owner.SpaceId, board.ProjectId));
        Assert.Empty(await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId));
    }

    /// <summary>
    /// MOV-09 (AC9, Q18) - a move to where the Task already is (after its own predecessor, in its own column) answers
    /// <c>200</c> with the Task unchanged, and writes nothing: its key is byte-identical.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-29")]
    public async Task A_move_to_where_the_Task_already_is_answers_200_and_writes_nothing()
    {
        var database = Available();
        Actor? owner = null;
        MoveBoard? board = null;
        IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)> before = [];
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            board = await MoveBoardAsync(database, owner, todo: 3, doing: 0);
            before = await PlacementsAsync(database, owner.SpaceId, board.ProjectId);

            answer = await MoveTaskAsync(owner, owner.SpaceId, board.Todo[1], board.Todo0Status, board.Todo[0]);
        });

        Assert.NotNull(owner);
        Assert.NotNull(board);
        Assert.True(answer?.StatusCode == HttpStatusCode.OK, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal(before.Single(row => row.Id == board.Todo[1]).PositionKey, PlacementOf(answer!).PositionKey);
        Assert.Equal(before, await PlacementsAsync(database, owner.SpaceId, board.ProjectId));
    }

    /// <summary>
    /// MOV-10 (AC9, story Task 3 "a move consults no bound", 2.3 P3) - in a Project holding 5,000 Tasks (the
    /// Tasks-per-Project limit), a move is admitted (<c>200</c>): a move creates nothing, so the bound never refuses it.
    /// Seeds 4,999 by one statement (2.2's <see cref="TaskWire.SeedTasksAsync"/>); no new large seed.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-30")]
    [Trait("Requirement", "AD-25")]
    [Trait("Priority", "P1")]
    public async Task A_move_in_a_full_Project_is_admitted_because_a_move_consults_no_bound()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        WireResponse? refusedCreation = null;
        WireResponse? moved = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projectId = await CreateProjectAsync(owner, "Full, still moving");

            var statuses = await StatusesAsync(database, owner.SpaceId);
            var limit = BoundRegistry.For(Bound.TasksPerProject).Limit;

            await SeedTasksAsync(database, owner.SpaceId, projectId, statuses[0].Id, limit - 1);

            var last = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "The 5,000th"));

            // Anchor: the Project really is full.
            refusedCreation = await CreateTaskAsync(owner, owner.SpaceId, projectId, "Refused while full");
            moved = await MoveTaskAsync(owner, owner.SpaceId, last, statuses[1].Id, afterTaskId: null);
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.Conflict, refusedCreation?.StatusCode);
        Assert.True(moved?.StatusCode == HttpStatusCode.OK, $"{moved?.StatusCode} {moved?.Body}");
        Assert.Equal(BoundRegistry.For(Bound.TasksPerProject).Limit, await TaskCountAsync(database, owner.SpaceId, projectId));
    }

    /// <summary>
    /// MOV-11 (AC10, Q10) - a gap with no room left: two neighbours of 128 characters (<c>PositionKey.MaxLength</c>) that
    /// differ only in their last character, so any key between them would be 129 or longer. A move into it answers the
    /// documented refusal (<c>409 task-position-unavailable</c>) - never a <c>500</c>, never a sibling renumber - and nothing
    /// changes.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-21")]
    [Trait("Requirement", "AD-15")]
    [Trait("Priority", "P1")]
    public async Task A_move_into_a_gap_with_no_room_left_is_refused_and_nothing_is_renumbered()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)> before = [];
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projectId = await CreateProjectAsync(owner, "No room");

            var statuses = await StatusesAsync(database, owner.SpaceId);
            var prefix = new string('V', 127);
            var lower = await SeedTaskAtAsync(database, owner.SpaceId, projectId, statuses[1].Id, prefix + "0");

            _ = await SeedTaskAtAsync(database, owner.SpaceId, projectId, statuses[1].Id, prefix + "1");

            var moved = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Wants to squeeze in"));

            before = await PlacementsAsync(database, owner.SpaceId, projectId);
            answer = await MoveTaskAsync(owner, owner.SpaceId, moved, statuses[1].Id, lower);
        });

        Assert.NotNull(owner);
        Assert.True(answer?.StatusCode == HttpStatusCode.Conflict, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal(TaskPositionUnavailable, Problem(answer!)?.Type);
        Assert.Equal(before, await PlacementsAsync(database, owner.SpaceId, projectId));
    }

    /// <summary>
    /// MOV-12 (AC8, FR-35, C-5, Q1 (a)) - a Token cannot reach the move route: an Owner's and a Member's Token each get the
    /// no-credential <c>401</c> byte-identical to the same request with no credential at all, and nothing moves. A Session
    /// on the same move answers <c>200</c> (the anchor that the route exists).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-35")]
    [Trait("Requirement", "AD-4")]
    public async Task A_Token_on_the_move_route_is_answered_as_no_credential_and_nothing_moves()
    {
        var database = Available();
        Actor? owner = null;
        MoveBoard? board = null;
        IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)> before = [];
        IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)> afterTokens = [];
        var byToken = new List<WireResponse>();
        WireResponse? anonymous = null;
        WireResponse? bySession = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");

            var ownerToken = await IssueTokenAsync(owner, owner.SpaceId);
            var memberToken = await IssueTokenAsync(member, owner.SpaceId);

            board = await MoveBoardAsync(database, owner, todo: 2, doing: 0);
            before = await PlacementsAsync(database, owner.SpaceId, board.ProjectId);

            byToken.Add(await MoveTaskWithTokenAsync(owner, ownerToken, owner.SpaceId, board.Todo[1], MoveBody(board.Todo0Status, null)));
            byToken.Add(await MoveTaskWithTokenAsync(member, memberToken, owner.SpaceId, board.Todo[1], MoveBody(board.Todo0Status, null)));
            anonymous = await MoveTaskWithoutCredentialAsync(client, owner.SpaceId, board.Todo[1], MoveBody(board.Todo0Status, null));
            afterTokens = await PlacementsAsync(database, owner.SpaceId, board.ProjectId);
            bySession = await MoveTaskAsync(owner, owner.SpaceId, board.Todo[1], board.Todo0Status, afterTaskId: null);
        });

        Assert.NotNull(owner);
        Assert.NotNull(anonymous);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        Assert.All(byToken, answer =>
        {
            Assert.Equal(anonymous.StatusCode, answer.StatusCode);
            Assert.Equal(anonymous.ContentType, answer.ContentType);
            Assert.Equal(anonymous.Body, answer.Body);
            Assert.Equal(anonymous.HeaderNames, answer.HeaderNames);
        });

        Assert.Equal(before, afterTokens);
        Assert.Equal(HttpStatusCode.OK, bySession?.StatusCode);
    }

    /// <summary>
    /// MOV-13 (AC8, FR-35, C-5) - over a Token, a Task <c>PATCH</c> naming only <c>positionKey</c> is refused
    /// <c>400 invalid-task-edit</c> (the member is not one the edit reads, so the body names nothing), and the Task's key is
    /// unchanged. <b>Green today</b> - 2.3's edit body ignores members it does not read - and a standing gate that the edit
    /// never learns to write a key.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-35")]
    [Trait("Requirement", "AR-21")]
    public async Task A_Token_PATCH_naming_only_a_position_key_is_refused_and_writes_no_key()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        var keyBefore = string.Empty;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var token = await IssueTokenAsync(owner, owner.SpaceId);

            (_, taskId) = await ProjectWithTaskAsync(owner, "Keyed by the server");
            keyBefore = await KeyOfAsync(database, owner.SpaceId, taskId);

            answer = await EditTaskWithTokenAsync(owner, token, owner.SpaceId, taskId, RawBody("""{ "positionKey": "0" }"""));
        });

        Assert.NotNull(owner);
        Assert.True(answer?.StatusCode == HttpStatusCode.BadRequest, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal(ProblemTypes.InvalidTaskEdit, Problem(answer!)?.Type);
        Assert.Equal(keyBefore, await KeyOfAsync(database, owner.SpaceId, taskId));
    }

    /// <summary>
    /// MOV-14 (AC8, FR-35, Q14) - over a Token, a Status <c>PATCH</c> that also carries a <c>positionKey</c> moves the Task
    /// to the <b>end</b> of its new column exactly as 2.3 built it, and the key sent is ignored: the stored key is the one
    /// after the column's last, never the one the caller chose. <b>Green today</b>; a standing gate.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-35")]
    [Trait("Requirement", "AR-21")]
    public async Task A_Token_Status_PATCH_carrying_a_position_key_appends_and_ignores_the_key()
    {
        var database = Available();
        Actor? owner = null;
        MoveBoard? board = null;
        WireResponse? answer = null;
        var chosen = "0";

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var token = await IssueTokenAsync(owner, owner.SpaceId);

            board = await MoveBoardAsync(database, owner, todo: 1, doing: 2);

            var status = board.Doing.ToString("D");

            answer = await EditTaskWithTokenAsync(
                owner,
                token,
                owner.SpaceId,
                board.Todo[0],
                RawBody($$"""{ "statusId": "{{status}}", "positionKey": "{{chosen}}" }"""));
        });

        Assert.NotNull(owner);
        Assert.NotNull(board);
        Assert.True(answer?.StatusCode == HttpStatusCode.OK, $"{answer?.StatusCode} {answer?.Body}");

        var (statusId, key) = PlacementOf(answer!);

        Assert.Equal(board.Doing, statusId);
        Assert.NotEqual(chosen, key);
        Assert.Equal([board.InDoing[0], board.InDoing[1], board.Todo[0]], await ColumnAsync(database, owner.SpaceId, board.ProjectId, board.Doing));
    }

    /// <summary>
    /// MOV-15 (AC8, FR-35 "readable", C-5) - a Task's position is readable on both surfaces: the Board and the Task read
    /// over a Token each serve <c>positionKey</c>, equal to the stored key, so a consumer can reproduce what a User sees.
    /// <b>Green today</b> (2.2, 2.3); a standing gate.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-35")]
    [Trait("Requirement", "AD-4")]
    public async Task A_Tasks_position_key_is_readable_over_a_Token_on_the_Board_and_the_Task()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        WireResponse? board = null;
        WireResponse? read = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var token = await IssueTokenAsync(owner, owner.SpaceId);
            var (projectId, created) = await ProjectWithTaskAsync(owner, "Readable position");

            taskId = created;
            board = await owner.Session.GetWithTokenAsync(Yello.Contracts.Boards.BoardRoutes.BoardOf(owner.SpaceId, projectId), token);
            read = await ViewTaskWithTokenAsync(owner, token, owner.SpaceId, taskId);
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.OK, board?.StatusCode);
        Assert.Equal(HttpStatusCode.OK, read?.StatusCode);

        var stored = await KeyOfAsync(database, owner.SpaceId, taskId);

        Assert.Equal(stored, PlacementOf(read!).PositionKey);

        using var document = JsonDocument.Parse(board!.Body);
        var served = document.RootElement.GetProperty("columns").EnumerateArray()
            .SelectMany(column => column.GetProperty("tasks").EnumerateArray())
            .Single(task => task.GetProperty("id").GetGuid() == taskId)
            .GetProperty("positionKey").GetString();

        Assert.Equal(stored, served);
    }

    /// <summary>
    /// A Project of <paramref name="owner"/>'s with the given numbers of Tasks in its first and its second column, all created through the product (the second column's by a creation and then
    /// 2.3's Status <c>PATCH</c>, which appends), in creation order.
    /// </summary>
    private static async Task<MoveBoard> MoveBoardAsync(RegistrationDatabase database, Actor owner, int todo, int doing)
    {
        var projectId = await CreateProjectAsync(owner, "Board " + Guid.NewGuid().ToString("N")[..8]);
        var statuses = await StatusesAsync(database, owner.SpaceId);
        var todoIds = new Guid[todo];
        var doingIds = new Guid[doing];

        for (var index = 0; index < todo; index++)
        {
            todoIds[index] = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, $"Todo {index + 1}"));
        }

        for (var index = 0; index < doing; index++)
        {
            doingIds[index] = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, $"Doing {index + 1}"));

            var placed = await EditTaskAsync(owner, owner.SpaceId, doingIds[index], Body(new { statusId = statuses[1].Id }));

            Assert.True(placed.StatusCode == HttpStatusCode.OK, $"{placed.StatusCode} {placed.Body}");
        }

        return new MoveBoard(projectId, statuses[0].Id, statuses[1].Id, statuses[2].Id, todoIds, doingIds);
    }

    /// <summary>
    /// A Project seeded for a move case: its three Statuses and the Tasks in its first two columns, in column order.
    /// </summary>
    private sealed record MoveBoard(Guid ProjectId, Guid Todo0Status, Guid Doing, Guid Done, Guid[] Todo, Guid[] InDoing);
}
