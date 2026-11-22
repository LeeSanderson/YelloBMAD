using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Domain.Bounds;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.ProjectMoveSliceWire;
using static Yello.Tests.Slices.Tasks.TaskEditWire;
using static Yello.Tests.Slices.Tasks.TaskWire;
using RemovalWire = Yello.Tests.Slices.Projects.StatusRemovalWire;
using StatusWire = Yello.Tests.Slices.Projects.ProjectStatusSliceWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// A move between Projects against every writer it meets: the first write that changes a Task's Project takes two Projects'
/// locks, so it is raced from both sides on each lock it shares - a Status change and a Project deletion holding the
/// source's or the destination's, a Status removal holding the destination's, another move the other way, and creations and
/// moves filling the destination's last place.
/// </summary>
/// <remarks>
/// <para>
/// <b>The interleaving is the test's, never the clock's.</b> The test holds the competing write in its own open transaction
/// in the product's statement shape (<see cref="ProjectMoveSliceWire.HoldProjectMoveAsync"/>, <see cref="DeleteWire"/>,
/// <see cref="RemovalWire.HoldRemovalAsync"/>), sends the request, waits until it is blocked on any <c>LCK_M_%</c>, then
/// commits.
/// </para>
/// <para>
/// <b>Some cases play the move in SQL rather than send it</b>: a Status edit, a move within a Project, a deletion and a
/// creation queued behind it are the writers under test, so the move is held in the product's shape and committed by the
/// test.
/// </para>
/// </remarks>
public sealed partial class TaskEndpointTests
{
    /// <summary>
    /// With a Status change of the Task held on the source's lock, a move with no destination Status queues; once the change
    /// commits the move decides on the Status it made - one the destination removed - and answers
    /// <c>409 status-destination-required</c>, the Task still in its source.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "AR-24")]
    public async Task A_move_queued_behind_a_Status_change_decides_on_the_Status_that_change_committed()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        TwoProjects? projects = null;
        IReadOnlyList<StatusRow> defaults = [];
        WireResponse? queued = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["Changing"], []);
            defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);
            _ = await RemovalWire.RemovedAsync(owner, owner.SpaceId, projects.DestinationId, defaults[2].Id);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await MoveWire.HoldMoveAsync(holder, held, projects.SourceId, projects.InSource[0], defaults[2].Id, EmptyColumnKey, cancellationToken);

            var moving = MoveToProjectAsync(owner, owner.SpaceId, projects.InSource[0], ProjectMoveWire.Body(projects.DestinationId));

            await WaitUntilBlockedOnALockAsync(observer, 1, moving, cancellationToken);
            await held.CommitAsync(cancellationToken);

            queued = await moving;
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        RemovalWire.AssertRefused(queued, HttpStatusCode.Conflict, ProblemTypes.StatusDestinationRequired);

        var row = await RowAsync(database, owner.SpaceId, projects.InSource[0]);

        Assert.Equal(projects.SourceId, row?.ProjectId);
        Assert.Equal(defaults[2].Id, row?.StatusId);
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// With a cross-Project move of the Task held, a Status edit or a move within the Project queues on the source's lock;
    /// once the move commits it re-reads the Task under that lock, finds it in another Project, answers the boundary
    /// <c>404</c> and writes nothing. Both writers re-check the Project under its lock.
    /// </summary>
    /// <param name="writer">The writer queued behind the move.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("edit")]
    [InlineData("move")]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-41")]
    public async Task A_Status_edit_or_a_move_queued_behind_a_cross_Project_move_answers_404_and_writes_nothing(string writer)
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        TwoProjects? projects = null;
        IReadOnlyList<StatusRow> defaults = [];
        var playedKey = string.Empty;
        WireResponse? queued = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["Moving away"], ["Resident"]);
            defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            playedKey = await HoldProjectMoveAsync(holder, held, new PlayedMove(owner.SpaceId, projects.SourceId, projects.DestinationId, projects.InSource[0], defaults[0].Id), cancellationToken);

            var racing = writer == "edit"
                ? EditTaskAsync(owner, owner.SpaceId, projects.InSource[0], Body(new { statusId = defaults[1].Id }))
                : MoveWire.MoveTaskAsync(owner, owner.SpaceId, projects.InSource[0], defaults[1].Id, afterTaskId: null);

            await WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            queued = await racing;
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        RemovalWire.AssertRefused(queued, HttpStatusCode.NotFound, ProblemTypes.NotAvailable);

        var row = await RowAsync(database, owner.SpaceId, projects.InSource[0]);

        Assert.Equal(projects.DestinationId, row?.ProjectId);
        Assert.Equal(defaults[0].Id, row?.StatusId);
        Assert.Equal(playedKey, row?.PositionKey);
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A Task deletion that read the Task's Project before a cross-Project move committed queues on the old Project's lock;
    /// once the move commits it re-checks the Project under its row lock, answers the boundary <c>404</c>, and the Task
    /// survives in its new Project.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-41")]
    public async Task A_deletion_queued_behind_a_cross_Project_move_answers_404_and_the_Task_survives()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        TwoProjects? projects = null;
        WireResponse? queued = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["Moving away"], []);

            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            _ = await HoldProjectMoveAsync(holder, held, new PlayedMove(owner.SpaceId, projects.SourceId, projects.DestinationId, projects.InSource[0], defaults[0].Id), cancellationToken);

            var deleting = DeleteWire.DeleteTaskAsync(owner, owner.SpaceId, projects.InSource[0]);

            await WaitUntilBlockedOnALockAsync(observer, 1, deleting, cancellationToken);
            await held.CommitAsync(cancellationToken);

            queued = await deleting;
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        RemovalWire.AssertRefused(queued, HttpStatusCode.NotFound, ProblemTypes.NotAvailable);
        Assert.Equal(projects.DestinationId, (await RowAsync(database, owner.SpaceId, projects.InSource[0]))?.ProjectId);
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// With the destination's deletion held, a move into it queues; once the deletion commits the move re-reads the
    /// destination under its lock, finds it gone and answers <c>400 invalid-task-project</c>; the Task is where it was.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-41")]
    public async Task A_move_queued_behind_its_destinations_deletion_is_refused_400_invalid_task_project()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        TwoProjects? projects = null;
        TaskRow? before = null;
        WireResponse? queued = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["Moving"], ["Deleted with its Project"]);
            before = await RowAsync(database, owner.SpaceId, projects.InSource[0]);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteWire.DeleteProjectRowAsync(holder, held, projects.DestinationId, cancellationToken);

            var moving = MoveToProjectAsync(owner, owner.SpaceId, projects.InSource[0], ProjectMoveWire.Body(projects.DestinationId));

            await WaitUntilBlockedOnALockAsync(observer, 1, moving, cancellationToken);
            await held.CommitAsync(cancellationToken);

            queued = await moving;
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        RemovalWire.AssertRefused(queued, HttpStatusCode.BadRequest, ProblemTypes.InvalidTaskProject);
        Assert.Equal(before, await RowAsync(database, owner.SpaceId, projects.InSource[0]));
    }

    /// <summary>
    /// Two moves the opposite way - one Task from A to B, another from B to A - both queued behind whichever Project's lock
    /// the product takes first: both answer <c>200</c> once it is released, and neither is chosen as a deadlock victim.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "AR-24")]
    public async Task Two_opposite_moves_queued_on_one_lock_both_land_and_neither_deadlocks()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        TwoProjects? projects = null;
        WireResponse[] answers = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");
            projects = await TwoProjectsAsync(owner, ["A to B"], ["B to A"]);

            foreach (var actor in new[] { owner, member })
            {
                Assert.Equal(HttpStatusCode.OK, (await BoardAsync(actor, owner.SpaceId, projects.SourceId)).StatusCode);
            }

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteWire.HoldProjectRangeAsync(holder, held, InLockOrder(owner.SpaceId, projects.SourceId, projects.DestinationId)[0], cancellationToken);

            var racing = Task.WhenAll(
                MoveToProjectAsync(owner, owner.SpaceId, projects.InSource[0], ProjectMoveWire.Body(projects.DestinationId)),
                MoveToProjectAsync(member, owner.SpaceId, projects.InDestination[0], ProjectMoveWire.Body(projects.SourceId)));

            await WaitUntilBlockedOnALockAsync(observer, 2, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answers = await racing;
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        AssertMovedInto(answers[0], projects.DestinationId);
        AssertMovedInto(answers[1], projects.SourceId);
        Assert.Equal(projects.DestinationId, (await RowAsync(database, owner.SpaceId, projects.InSource[0]))?.ProjectId);
        Assert.Equal(projects.SourceId, (await RowAsync(database, owner.SpaceId, projects.InDestination[0]))?.ProjectId);
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// With the destination at 4,999 Tasks and its lock held, a move and a second write into it - another move, or a
    /// creation - both queue; once released exactly one is admitted and the other is <c>409 bound-reached</c>, and the
    /// destination holds 5,000.
    /// </summary>
    /// <param name="second">The second write into the destination.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("move")]
    [InlineData("creation")]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "AD-25")]
    [Trait("Requirement", "AR-30")]
    public async Task At_4999_a_move_and_a_second_write_into_the_destination_admit_exactly_one(string second)
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        TwoProjects? projects = null;
        WireResponse[] answers = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");
            projects = await TwoProjectsAsync(owner, ["First in", "Second in"], []);

            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            await SeedTasksAsync(database, owner.SpaceId, projects.DestinationId, defaults[0].Id, BoundRegistry.For(Bound.TasksPerProject).Limit - 1);

            foreach (var actor in new[] { owner, member })
            {
                Assert.Equal(HttpStatusCode.OK, (await BoardAsync(actor, owner.SpaceId, projects.SourceId)).StatusCode);
            }

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteWire.HoldProjectRangeAsync(holder, held, projects.DestinationId, cancellationToken);

            var racing = Task.WhenAll(
                MoveToProjectAsync(owner, owner.SpaceId, projects.InSource[0], ProjectMoveWire.Body(projects.DestinationId)),
                second == "move"
                    ? MoveToProjectAsync(member, owner.SpaceId, projects.InSource[1], ProjectMoveWire.Body(projects.DestinationId))
                    : CreateTaskAsync(member, owner.SpaceId, projects.DestinationId, "Created at the bound"));

            await WaitUntilBlockedOnALockAsync(observer, 2, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answers = await racing;
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        Assert.Single(answers, answer => answer.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created);

        var refused = Assert.Single(answers, answer => answer.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(ProblemTypes.BoundReached, Problem(refused)?.Type);
        Assert.Equal(BoundRegistry.For(Bound.TasksPerProject).Limit, await TaskCountAsync(database, owner.SpaceId, projects.DestinationId));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// With the destination's removal of the Task's Status held on the destination's lock, a move with no destination Status
    /// queues; once the removal commits the move reads the destination's set without it and answers
    /// <c>409 status-destination-required</c>; the Task is where it was.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-26")]
    public async Task A_move_queued_behind_the_destinations_removal_of_its_Status_needs_a_destination_Status()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        TwoProjects? projects = null;
        TaskRow? before = null;
        WireResponse? queued = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["Moving"], []);

            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            before = await RowAsync(database, owner.SpaceId, projects.InSource[0]);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await RemovalWire.HoldRemovalAsync(holder, held, owner.SpaceId, projects.DestinationId, defaults[0].Id, cancellationToken);

            var moving = MoveToProjectAsync(owner, owner.SpaceId, projects.InSource[0], ProjectMoveWire.Body(projects.DestinationId));

            await WaitUntilBlockedOnALockAsync(observer, 1, moving, cancellationToken);
            await held.CommitAsync(cancellationToken);

            queued = await moving;
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        RemovalWire.AssertRefused(queued, HttpStatusCode.Conflict, ProblemTypes.StatusDestinationRequired);
        Assert.Equal(before, await RowAsync(database, owner.SpaceId, projects.InSource[0]));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A creation in the destination queued behind a cross-Project move counts the moved Task and lands after it: at 4,999 it
    /// is refused <c>409 bound-reached</c>; otherwise it lands after the moved Task's key. The creation's
    /// bound count already waits on the destination's lock the move holds.
    /// </summary>
    /// <param name="full">Whether the destination holds 4,999 Tasks before the move.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "AD-15")]
    [Trait("Requirement", "AD-25")]
    public async Task A_creation_queued_behind_a_cross_Project_move_counts_the_moved_Task_and_lands_after_it(bool full)
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        TwoProjects? projects = null;
        var playedKey = string.Empty;
        WireResponse? created = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["Moving in"], full ? [] : ["Resident"]);

            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            if (full)
            {
                await SeedTasksAsync(database, owner.SpaceId, projects.DestinationId, defaults[0].Id, BoundRegistry.For(Bound.TasksPerProject).Limit - 1);
            }

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            playedKey = await HoldProjectMoveAsync(holder, held, new PlayedMove(owner.SpaceId, projects.SourceId, projects.DestinationId, projects.InSource[0], defaults[0].Id), cancellationToken);

            var creating = CreateTaskAsync(owner, owner.SpaceId, projects.DestinationId, "Created behind the move");

            await WaitUntilBlockedOnALockAsync(observer, 1, creating, cancellationToken);
            await held.CommitAsync(cancellationToken);

            created = await creating;
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        Assert.NotNull(created);

        if (full)
        {
            Assert.True(created.StatusCode == HttpStatusCode.Conflict, $"{created.StatusCode} {created.Body}");
            Assert.Equal(ProblemTypes.BoundReached, Problem(created)?.Type);
            Assert.Equal(BoundRegistry.For(Bound.TasksPerProject).Limit, await TaskCountAsync(database, owner.SpaceId, projects.DestinationId));

            return;
        }

        var createdId = IdOf(created);

        MoveWire.AssertStrictlyBetween(playedKey, await MoveWire.KeyOfAsync(database, owner.SpaceId, createdId), upper: null);
        Assert.Equal(
            [projects.InDestination[0], projects.InSource[0], createdId],
            await MoveWire.ColumnAsync(database, owner.SpaceId, projects.DestinationId, (await RowAsync(database, owner.SpaceId, createdId))!.StatusId));
    }
}
