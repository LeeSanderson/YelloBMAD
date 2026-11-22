using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Domain.Bounds;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.ProjectMoveSliceWire;
using static Yello.Tests.Slices.Tasks.StatusBulkMoveSliceWire;
using static Yello.Tests.Slices.Tasks.TaskEditWire;
using static Yello.Tests.Slices.Tasks.TaskWire;
using RemovalWire = Yello.Tests.Slices.Projects.StatusRemovalWire;
using StatusWire = Yello.Tests.Slices.Projects.ProjectStatusSliceWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// A bulk move against every writer it meets on the two Projects' locks and on the rows it rewrites: another bulk move the
/// other way, a creation and a single move holding the source, the deletion, Status edit and single move queued behind it,
/// the destination's or the source's deletion, the source Status's removal, the destination's removal of that Status, a
/// creation competing for the destination's last places, and a Labels edit holding one of the rows.
/// </summary>
/// <remarks>
/// <para>
/// <b>The interleaving is the test's, never the clock's.</b> The test holds the competing write in its own open transaction
/// in the product's shape (<see cref="StatusBulkMoveSliceWire"/>, <see cref="ProjectMoveSliceWire"/>, <see cref="DeleteWire"/>,
/// <see cref="RemovalWire.HoldRemovalAsync"/>), sends the request, waits until it is blocked on any <c>LCK_M_%</c>, then
/// commits.
/// </para>
/// <para>
/// <b>The selection is decided under the locks</b>, so the cases that hold a write on the source assert what the move then
/// selects: a Task created into the Status before the move's locks are granted is moved, and a Task moved out of it is not.
/// </para>
/// </remarks>
public sealed partial class TaskEndpointTests
{
    /// <summary>
    /// Two bulk moves the opposite way - one Status from A to B, the same Status from B to A - both queued behind whichever
    /// Project's lock the product takes first: both answer <c>200</c> once it is released, neither is chosen as a deadlock
    /// victim, and the outcome is the serial one: the second moves everything the first left in its source.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "AR-24")]
    public async Task Two_opposite_bulk_moves_queued_on_one_lock_both_land_and_neither_deadlocks()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        TwoProjects? projects = null;
        IReadOnlyList<StatusRow> defaults = [];
        WireResponse[] answers = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");
            projects = await TwoProjectsAsync(owner, ["A to B", "A to B too"], ["B to A", "B to A too"]);
            defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            foreach (var actor in new[] { owner, member })
            {
                Assert.Equal(HttpStatusCode.OK, (await BoardAsync(actor, owner.SpaceId, projects.SourceId)).StatusCode);
            }

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteWire.HoldProjectRangeAsync(holder, held, InLockOrder(owner.SpaceId, projects.SourceId, projects.DestinationId)[0], cancellationToken);

            var racing = Task.WhenAll(
                BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId)),
                BulkMoveAsync(member, owner.SpaceId, projects.DestinationId, defaults[0].Id, ProjectMoveWire.Body(projects.SourceId)));

            await WaitUntilBlockedOnALockAsync(observer, 2, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answers = await racing;
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        Assert.All(answers, answer => Assert.True(answer.StatusCode == HttpStatusCode.OK, $"{answer.StatusCode} {answer.Body}"));
        Assert.Equal([2, 4], answers.Select(answer => StatusBulkMoveWire.MovedCountIn(answer.Body) ?? -1).Order());

        var last = answers.Single(answer => StatusBulkMoveWire.MovedCountIn(answer.Body) == 4);
        var landedIn = ProjectMoveWire.ProjectIdIn(last.Body);

        Assert.NotNull(landedIn);

        var column = await MoveWire.ColumnAsync(database, owner.SpaceId, landedIn.Value, defaults[0].Id);

        Assert.Equal(projects.InSource.Concat(projects.InDestination).Order(), column.Order());
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A creation into the source Status holds the source's lock and the bulk move queues; once the creation commits the move
    /// selects under its locks and moves the created Task too, counting it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-41")]
    [Trait("Requirement", "FR-26")]
    [Trait("Requirement", "AD-17")]
    public async Task A_Task_created_into_the_Status_before_the_move_took_its_locks_is_moved()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        TwoProjects? projects = null;
        IReadOnlyList<StatusRow> defaults = [];
        var created = Guid.Empty;
        WireResponse? moved = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["Moving", "Also moving"], []);
            defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            created = await HoldCreationAsync(holder, held, owner.SpaceId, projects.SourceId, defaults[0].Id, cancellationToken);

            var moving = BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId));

            await WaitUntilBlockedOnALockAsync(observer, 1, moving, cancellationToken);
            await held.CommitAsync(cancellationToken);

            moved = await moving;
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        AssertBulkMoved(moved, projects.DestinationId, defaults[0].Id, 3);
        Assert.Equal([.. projects.InSource, created], await MoveWire.ColumnAsync(database, owner.SpaceId, projects.DestinationId, defaults[0].Id));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A single move of one of the Status's Tasks into a third Project holds the source's lock and the bulk move queues; once
    /// it commits that Task stays in the third Project and the bulk move moves the rest.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-41")]
    [Trait("Requirement", "AD-17")]
    public async Task A_Task_moved_out_of_the_Status_before_the_move_took_its_locks_is_not_moved()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        TwoProjects? projects = null;
        IReadOnlyList<StatusRow> defaults = [];
        var third = Guid.Empty;
        WireResponse? moved = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["Leaves first", "Moving", "Also moving"], []);
            defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);
            third = await CreateProjectAsync(owner, "Third " + Guid.NewGuid().ToString("N")[..8]);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            _ = await HoldProjectMoveAsync(holder, held, new PlayedMove(owner.SpaceId, projects.SourceId, third, projects.InSource[0], defaults[0].Id), cancellationToken);

            var moving = BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId));

            await WaitUntilBlockedOnALockAsync(observer, 1, moving, cancellationToken);
            await held.CommitAsync(cancellationToken);

            moved = await moving;
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        AssertBulkMoved(moved, projects.DestinationId, defaults[0].Id, 2);
        Assert.Equal(third, (await RowAsync(database, owner.SpaceId, projects.InSource[0]))?.ProjectId);
        Assert.Equal(projects.InSource.Skip(1), await MoveWire.ColumnAsync(database, owner.SpaceId, projects.DestinationId, defaults[0].Id));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// With a bulk move held, a Task deletion, a Status edit or a single move of one of its Tasks queues on the source's lock;
    /// once the bulk move commits each re-reads the Task under that lock, finds it in another Project, answers the boundary
    /// <c>404</c> and writes nothing. The Task is where the bulk move put it.
    /// </summary>
    /// <param name="writer">The writer queued behind the bulk move.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("deletion")]
    [InlineData("status-edit")]
    [InlineData("single-move")]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-41")]
    public async Task A_writer_queued_behind_a_bulk_move_answers_404_and_writes_nothing(string writer)
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
            projects = await TwoProjectsAsync(owner, ["Moving away", "Also moving away"], ["Resident"]);
            defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            var third = await CreateProjectAsync(owner, "Third " + Guid.NewGuid().ToString("N")[..8]);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            _ = await HoldStatusBulkMoveAsync(holder, held, new PlayedBulkMove(owner.SpaceId, projects.SourceId, projects.DestinationId, defaults[0].Id, defaults[0].Id), cancellationToken);

            var racing = writer switch
            {
                "deletion" => DeleteWire.DeleteTaskAsync(owner, owner.SpaceId, projects.InSource[0]),
                "status-edit" => EditTaskAsync(owner, owner.SpaceId, projects.InSource[0], Body(new { statusId = defaults[1].Id })),
                _ => MoveToProjectAsync(owner, owner.SpaceId, projects.InSource[0], ProjectMoveWire.Body(third)),
            };

            await WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            queued = await racing;
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        RemovalWire.AssertRefused(queued, HttpStatusCode.NotFound, ProblemTypes.NotAvailable);
        Assert.Equal(
            [projects.InDestination[0], .. projects.InSource],
            await MoveWire.ColumnAsync(database, owner.SpaceId, projects.DestinationId, defaults[0].Id));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// With the destination's deletion held, a bulk move into it queues; once the deletion commits the move re-reads the
    /// destination under its lock, finds it gone and answers <c>400 invalid-task-project</c>; nothing moved.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-41")]
    public async Task A_bulk_move_queued_behind_its_destinations_deletion_is_refused_400_invalid_task_project()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        TwoProjects? projects = null;
        IReadOnlyList<TaskRow> before = [];
        WireResponse? queued = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["Moving", "Also moving"], ["Deleted with its Project"]);

            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            before = await SourceRowsAsync(database, owner.SpaceId, projects.SourceId);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteWire.DeleteProjectRowAsync(holder, held, projects.DestinationId, cancellationToken);

            var moving = BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId));

            await WaitUntilBlockedOnALockAsync(observer, 1, moving, cancellationToken);
            await held.CommitAsync(cancellationToken);

            queued = await moving;
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        RemovalWire.AssertRefused(queued, HttpStatusCode.BadRequest, ProblemTypes.InvalidTaskProject);
        Assert.Equal(before, await SourceRowsAsync(database, owner.SpaceId, projects.SourceId));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// With the source's deletion held, a bulk move out of it queues; once the deletion commits the move re-reads the source
    /// under its lock, finds it gone and answers the boundary <c>404</c>; the destination is unchanged.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "AD-3")]
    public async Task A_bulk_move_queued_behind_its_sources_deletion_answers_the_boundary_404()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        TwoProjects? projects = null;
        IReadOnlyList<TaskRow> destinationBefore = [];
        WireResponse? queued = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["Deleted with its Project"], ["Resident"]);

            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            destinationBefore = await SourceRowsAsync(database, owner.SpaceId, projects.DestinationId);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteWire.DeleteProjectRowAsync(holder, held, projects.SourceId, cancellationToken);

            var moving = BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId));

            await WaitUntilBlockedOnALockAsync(observer, 1, moving, cancellationToken);
            await held.CommitAsync(cancellationToken);

            queued = await moving;
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        RemovalWire.AssertRefused(queued, HttpStatusCode.NotFound, ProblemTypes.NotAvailable);
        Assert.Equal(destinationBefore, await SourceRowsAsync(database, owner.SpaceId, projects.DestinationId));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// With the source's removal of the moved Status held - its Tasks migrated to another Status of the source - a bulk move of
    /// that Status queues; once the removal commits the move reads the source's set without it and answers the boundary
    /// <c>404</c>; the Tasks stay in the source, in the Status the removal gave them.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-26")]
    public async Task A_bulk_move_queued_behind_the_removal_of_its_source_Status_answers_the_boundary_404()
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
            projects = await TwoProjectsAsync(owner, ["Migrated by the removal", "Also migrated"], []);
            defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await HoldOccupiedRemovalAsync(holder, held, owner.SpaceId, projects.SourceId, defaults[0].Id, defaults[1].Id, cancellationToken);

            var moving = BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId));

            await WaitUntilBlockedOnALockAsync(observer, 1, moving, cancellationToken);
            await held.CommitAsync(cancellationToken);

            queued = await moving;
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        RemovalWire.AssertRefused(queued, HttpStatusCode.NotFound, ProblemTypes.NotAvailable);
        Assert.Equal(projects.InSource, await MoveWire.ColumnAsync(database, owner.SpaceId, projects.SourceId, defaults[1].Id));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// With the destination's removal of the moved Status held on the destination's lock, a bulk move with no destination
    /// Status queues; once the removal commits the move reads the destination's set without it and answers
    /// <c>409 status-destination-required</c>; nothing moved.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-26")]
    public async Task A_bulk_move_queued_behind_the_destinations_removal_of_its_Status_needs_a_destination_Status()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        TwoProjects? projects = null;
        IReadOnlyList<TaskRow> before = [];
        WireResponse? queued = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["Moving", "Also moving"], []);

            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            before = await SourceRowsAsync(database, owner.SpaceId, projects.SourceId);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await RemovalWire.HoldRemovalAsync(holder, held, owner.SpaceId, projects.DestinationId, defaults[0].Id, cancellationToken);

            var moving = BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId));

            await WaitUntilBlockedOnALockAsync(observer, 1, moving, cancellationToken);
            await held.CommitAsync(cancellationToken);

            queued = await moving;
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        RemovalWire.AssertRefused(queued, HttpStatusCode.Conflict, ProblemTypes.StatusDestinationRequired);
        Assert.Equal(before, await SourceRowsAsync(database, owner.SpaceId, projects.SourceId));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// With the destination at 4,990 Tasks and its lock held, a bulk move of ten and a creation into it both queue; once
    /// released exactly one is admitted: either the move lands the destination on 5,000 and the creation is refused, or the
    /// creation lands and the move is refused <c>409 bound-reached</c> with nothing moved.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "AD-25")]
    [Trait("Requirement", "AR-30")]
    public async Task At_4990_a_bulk_move_of_ten_and_a_creation_into_the_destination_admit_exactly_one()
    {
        const int toMove = 10;

        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        var limit = BoundRegistry.For(Bound.TasksPerProject).Limit;
        Actor? owner = null;
        TwoProjects? projects = null;
        WireResponse? moved = null;
        WireResponse? created = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");
            projects = await TwoProjectsAsync(owner, [], []);

            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            await SeedTasksAsync(database, owner.SpaceId, projects.SourceId, defaults[0].Id, toMove);
            await SeedTasksAsync(database, owner.SpaceId, projects.DestinationId, defaults[0].Id, limit - toMove);

            foreach (var actor in new[] { owner, member })
            {
                Assert.Equal(HttpStatusCode.OK, (await BoardAsync(actor, owner.SpaceId, projects.SourceId)).StatusCode);
            }

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteWire.HoldProjectRangeAsync(holder, held, projects.DestinationId, cancellationToken);

            var moving = BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId));
            var creating = CreateTaskAsync(member, owner.SpaceId, projects.DestinationId, "Created at the bound");

            await WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(moving, creating), cancellationToken);
            await held.CommitAsync(cancellationToken);

            moved = await moving;
            created = await creating;
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        Assert.NotNull(moved);
        Assert.NotNull(created);

        var destination = await TaskCountAsync(database, owner.SpaceId, projects.DestinationId);
        var source = await TaskCountAsync(database, owner.SpaceId, projects.SourceId);

        if (moved.StatusCode == HttpStatusCode.OK)
        {
            Assert.Equal(toMove, StatusBulkMoveWire.MovedCountIn(moved.Body));
            Assert.True(created.StatusCode == HttpStatusCode.Conflict, $"{created.StatusCode} {created.Body}");
            Assert.Equal(ProblemTypes.BoundReached, Problem(created)?.Type);
            Assert.Equal((limit, 0), (destination, source));
        }
        else
        {
            Assert.True(created.StatusCode == HttpStatusCode.Created, $"{created.StatusCode} {created.Body}");
            RemovalWire.AssertRefused(moved, HttpStatusCode.Conflict, ProblemTypes.BoundReached);
            Assert.Equal((limit - toMove + 1, toMove), (destination, source));
        }

        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A Labels-only edit holds one of the Status's rows without the Project's lock: a bulk move and a second Labels edit of
    /// that Task both queue on the row; once it is released both answer <c>200</c>, neither is a deadlock victim, and the
    /// Task is in the destination carrying the Label.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-41")]
    public async Task A_bulk_move_and_a_Labels_edit_on_one_of_its_rows_both_land()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        TwoProjects? projects = null;
        IReadOnlyList<StatusRow> defaults = [];
        var label = Guid.Empty;
        WireResponse[] answers = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["Moving", "Labelled while moving", "Also moving"], []);
            defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);
            label = await LabelWire.SeedAsync(owner, owner.SpaceId, "Urgent " + Guid.NewGuid().ToString("N")[..6]);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await LabelWire.HoldTaskLabelsAsync(holder, held, owner.SpaceId, projects.InSource[1], cancellationToken);

            var racing = Task.WhenAll(
                BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId)),
                EditTaskAsync(owner, owner.SpaceId, projects.InSource[1], Body(new { labelIds = new[] { label } })));

            await WaitUntilBlockedOnALockAsync(observer, 2, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answers = await racing;
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        AssertBulkMoved(answers[0], projects.DestinationId, defaults[0].Id, 3);
        Assert.True(answers[1].StatusCode == HttpStatusCode.OK, $"{answers[1].StatusCode} {answers[1].Body}");
        Assert.Equal(projects.InSource, await MoveWire.ColumnAsync(database, owner.SpaceId, projects.DestinationId, defaults[0].Id));
        Assert.Equal([label], await LabelWire.AppliedToAsync(database, owner.SpaceId, projects.InSource[1]));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }
}
