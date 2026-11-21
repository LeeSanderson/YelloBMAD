using System.Net;
using Xunit;
using Yello.Tests.Shared;
using DeleteWire = Yello.Tests.Slices.Tasks.DeleteWire;
using MoveWire = Yello.Tests.Slices.Tasks.MoveWire;
using RemovalWire = Yello.Tests.Slices.Projects.StatusRemovalWire;
using StatusRow = Yello.Tests.Slices.Tasks.StatusRow;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;
using Wire = Yello.Tests.Slices.Projects.ProjectStatusSliceWire;

namespace Yello.Tests.Slices.Projects;

/// <summary>
/// The removal against everything that could strand a Task: the S-2 statement itself, a Task writer that moved a Task into
/// the Status while the removal waited, two removals naming each other as destination, and a failure forced between the
/// migration and the commit.
/// </summary>
/// <remarks>
/// <para>
/// <b>S-2 must know <c>Remove</c> first.</b> Every other case here ends on
/// <see cref="ProjectStatusSliceWire.AssertNoTaskOutsideItsProjectsSetAsync"/>; if its statement ignored a removed default, a
/// removal that strands Tasks would still count zero. The first case proves the statement counts such a Task and nothing
/// else, by writing the <c>Remove</c> row itself (see <see cref="StatusRemovalWire"/> for the constraints it satisfies).
/// </para>
/// <para>
/// <b>Occupancy is decided under the lock.</b> The test plays an edit into the Status in its own open transaction
/// (<see cref="MoveWire.HoldMoveAsync"/>: the Project's lock, the Task row, its <c>UPDATE</c>), sends the removal, waits until
/// it is blocked, then commits. A removal that counted the Status's Tasks before the lock read zero (RCSI) and removes it
/// with nowhere to put the edited Task.
/// </para>
/// </remarks>
public sealed partial class ProjectEndpointTests
{
    private static readonly string[] RolledBackTitles = ["First", "Second", "Third"];

    /// <summary>
    /// A standing gate on the invariant's own statement: a Task whose Status its own Project's delta removes is counted
    /// outside its set; a sibling Project's Task in the same default is not; and a <c>Remove</c> of a Status no Task holds adds
    /// nothing. Anchored on zero before either row is written.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-26")]
    public async Task The_S2_statement_counts_a_Task_whose_Status_its_own_Project_removed_and_nothing_else()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var projectId = Guid.Empty;
        var siblingId = Guid.Empty;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            projectId = IdOf(await CreateAsync(owner, "Removes its first default"));
            siblingId = IdOf(await CreateAsync(owner, "Keeps every default"));

            _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, "Stranded by the row"));
            _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, siblingId, "Not stranded"));
        });

        Assert.NotNull(owner);
        Assert.Equal(0, await Wire.TasksOutsideTheirProjectsSetAsync(database, owner.SpaceId));

        var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

        await using (var connection = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken))
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            await RemovalWire.InsertRemoveOperationAsync(connection, transaction, owner.SpaceId, siblingId, defaults[2].Id, cancellationToken);
            await RemovalWire.InsertRemoveOperationAsync(connection, transaction, owner.SpaceId, projectId, defaults[0].Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        Assert.Equal(1, await Wire.TasksOutsideTheirProjectsSetAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// With an edit of a Task into the third default held under the Project's lock, a removal of the third default with no
    /// destination queues; once the edit commits the removal is refused <c>409 status-destination-required</c>, the Status is
    /// still in the set holding the Task, and nothing is written.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1, AC8 - the remove route does not exist and occupancy is not yet read under the Project's lock (Q1, Q2, Q4)")]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-26")]
    public async Task A_removal_without_a_destination_queued_behind_an_edit_into_the_Status_is_refused_409()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var projectId = Guid.Empty;
        var taskId = Guid.Empty;
        var third = Guid.Empty;
        WireResponse? queued = null;
        IReadOnlyList<ProjectStatusEntry> set = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            projectId = IdOf(await CreateAsync(owner, "Occupied while waiting"));
            taskId = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, "Edited in"));
            third = (await Wire.DefaultsAsync(database, owner.SpaceId))[2].Id;

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await MoveWire.HoldMoveAsync(holder, held, projectId, taskId, third, "V0", cancellationToken);

            var removing = RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, third, destinationStatusId: null);

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, removing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            queued = await removing;
            set = await Wire.EffectiveAsync(actor, owner.SpaceId, projectId);
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(queued, HttpStatusCode.Conflict, ProjectStatusWire.StatusDestinationRequired);
        Assert.Contains(set, entry => entry.Id == third && entry.TaskCount == 1);
        Assert.Equal(third, (await TaskWire.TaskRowsAsync(database, owner.SpaceId)).Single(row => row.Id == taskId).StatusId);
        Assert.Equal(0, await Wire.DeltaCountAsync(database, owner.SpaceId, projectId));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// With an edit of a Task into the third default held under the Project's lock, a removal of the third default to the
    /// second queues; once the edit commits the removal answers <c>200</c> and migrates the edited Task too: it holds the
    /// second default, and the third is no longer in the set.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1, AC8 - the remove route does not exist and occupancy is not yet read under the Project's lock (Q1, Q2)")]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-26")]
    public async Task A_removal_with_a_destination_queued_behind_an_edit_into_the_Status_migrates_the_edited_Task()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var taskId = Guid.Empty;
        IReadOnlyList<StatusRow> defaults = [];
        WireResponse? queued = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var projectId = IdOf(await CreateAsync(owner, "Migrated while waiting"));

            taskId = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, "Edited in"));
            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await MoveWire.HoldMoveAsync(holder, held, projectId, taskId, defaults[2].Id, "V0", cancellationToken);

            var removing = RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, defaults[2].Id, defaults[1].Id);

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, removing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            queued = await removing;
        });

        Assert.NotNull(owner);
        Assert.True(queued?.StatusCode == HttpStatusCode.OK, $"{queued?.StatusCode} {queued?.Body}");
        Assert.DoesNotContain(ProjectStatusWire.EntriesOf(queued!.Body), entry => entry.Id == defaults[2].Id);
        Assert.Equal(defaults[1].Id, (await TaskWire.TaskRowsAsync(database, owner.SpaceId)).Single(row => row.Id == taskId).StatusId);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Two removals, each naming the other's Status as its destination, queue on the Project's lock; released, exactly one
    /// answers <c>200</c> and the other <c>400 invalid-status-destination</c> (its destination is gone); the set holds two
    /// Statuses, every Task is in the survivor of the pair or the third default, and no Task is outside the set.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2, AC8 - the remove route does not exist (Q1, Q2, Q4, Q5)")]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-26")]
    public async Task Two_removals_naming_each_other_as_destination_queue_and_the_second_is_refused_400()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var projectId = Guid.Empty;
        WireResponse[] answers = [];
        IReadOnlyList<StatusRow> defaults = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            projectId = IdOf(await CreateAsync(owner, "Crossed removals"));
            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
            _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, "In the first"));

            var second = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, "In the second"));

            Assert.Equal(HttpStatusCode.OK, (await MoveWire.MoveTaskAsync(actor, owner.SpaceId, second, defaults[1].Id, afterTaskId: null)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteWire.HoldProjectRangeAsync(holder, held, projectId, cancellationToken);

            var racing = Task.WhenAll(
                RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, defaults[0].Id, defaults[1].Id),
                RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, defaults[1].Id, defaults[0].Id));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answers = await racing;
        });

        Assert.NotNull(owner);
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.BadRequest], answers.Select(answer => answer.StatusCode).Order());
        RemovalWire.AssertRefused(answers.Single(answer => answer.StatusCode == HttpStatusCode.BadRequest), HttpStatusCode.BadRequest, ProjectStatusWire.InvalidStatusDestination);

        var survivors = ProjectStatusWire.EntriesOf(answers.Single(answer => answer.StatusCode == HttpStatusCode.OK).Body).Select(entry => entry.Id).ToList();

        Assert.Equal(2, survivors.Count);
        Assert.All(await TaskWire.TaskRowsAsync(database, owner.SpaceId), row => Assert.Contains(row.StatusId, survivors));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// S-1's removal case: a trigger that throws on the Project's delta write - after the Task migration, before the commit -
    /// makes the removal answer <c>5xx</c>; every Task still holds the removed Status at its old key, the set and the delta
    /// are as they were, and no Task is outside the set. Anchored on the same removal answering <c>200</c> once the trigger
    /// is dropped, with every Task migrated.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 - the remove route does not exist; the migration and the delta write must share one transaction (S-1)")]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "AR-24")]
    [Trait("Requirement", "FR-26")]
    public async Task A_failure_forced_after_the_migration_rolls_the_whole_removal_back()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        IReadOnlyList<StatusRow> defaults = [];
        IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)> before = [];
        IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)> afterFailure = [];
        IReadOnlyList<DeltaRow> deltaBefore = [];
        IReadOnlyList<DeltaRow> deltaAfterFailure = [];
        IReadOnlyList<ProjectStatusEntry> setAfterFailure = [];
        WireResponse? forced = null;
        WireResponse? anchor = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            projectId = IdOf(await CreateAsync(owner, "Rolled back"));
            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

            foreach (var title in RolledBackTitles)
            {
                _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, title));
            }

            before = await MoveWire.PlacementsAsync(database, owner.SpaceId, projectId);
            deltaBefore = await RemovalWire.DeltaRowsAsync(database, owner.SpaceId, projectId);

            var trigger = await RemovalWire.PlantFailingDeltaTriggerAsync(database, projectId);

            try
            {
                forced = await RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, defaults[0].Id, defaults[1].Id);
            }
            finally
            {
                await RemovalWire.DropTriggerAsync(database, trigger);
            }

            afterFailure = await MoveWire.PlacementsAsync(database, owner.SpaceId, projectId);
            deltaAfterFailure = await RemovalWire.DeltaRowsAsync(database, owner.SpaceId, projectId);
            setAfterFailure = await Wire.EffectiveAsync(actor, owner.SpaceId, projectId);
            anchor = await RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, defaults[0].Id, defaults[1].Id);
        });

        Assert.NotNull(owner);
        Assert.NotNull(forced);
        Assert.True(forced.StatusCode is >= HttpStatusCode.InternalServerError, $"{forced.StatusCode} {forced.Body}");
        Assert.Equal(3, before.Count);
        Assert.All(before, row => Assert.Equal(defaults[0].Id, row.StatusId));
        Assert.Equal(before, afterFailure);
        Assert.Equal(deltaBefore, deltaAfterFailure);
        Assert.Equal(defaults.Select(row => row.Id), setAfterFailure.Select(entry => entry.Id));
        Assert.True(anchor?.StatusCode == HttpStatusCode.OK, $"{anchor?.StatusCode} {anchor?.Body}");
        Assert.All(await MoveWire.PlacementsAsync(database, owner.SpaceId, projectId), row => Assert.Equal(defaults[1].Id, row.StatusId));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }
}
