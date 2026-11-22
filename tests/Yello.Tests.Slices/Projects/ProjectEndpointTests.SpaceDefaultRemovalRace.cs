using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;
using DeleteWire = Yello.Tests.Slices.Tasks.DeleteWire;
using MoveWire = Yello.Tests.Slices.Tasks.MoveWire;
using RemovalWire = Yello.Tests.Slices.Projects.StatusRemovalWire;
using Space = Yello.Tests.Slices.Projects.SpaceDefaultStatusWire;
using StatusRow = Yello.Tests.Slices.Tasks.StatusRow;
using TaskEditWire = Yello.Tests.Slices.Tasks.TaskEditWire;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;
using Wire = Yello.Tests.Slices.Projects.ProjectStatusSliceWire;

namespace Yello.Tests.Slices.Projects;

/// <summary>
/// A Space-level removal against every writer it can meet. A Task writer takes only its own Project's lock, so unless the
/// removal holds that lock for every Project whose set holds the Status, a Task can be put into the Status after the
/// occupancy count and the definition's delete then fails on <c>FK_Task_StatusDefinition</c>; a Project created after the
/// Project list was read is the same hole. And the whole request is one transaction.
/// </summary>
/// <remarks>
/// <para>
/// <b>The lock order the scaffolds assume</b>: <c>StatusDefaults</c> Exclusive, then <c>ProjectsPerSpace</c>, then
/// <c>ProjectTasks</c> of every Project whose set holds the Status, in sorted id order; every read the plan judges after the
/// last grant. A Project delta write takes <c>StatusDefaults</c> Shared, then its <c>ProjectTasks</c>.
/// </para>
/// <para>
/// <b>Interleaving is controlled, never hoped for</b>: the test holds a Project's Tasks lock
/// (<see cref="DeleteWire.HoldProjectRangeAsync"/>) or plays a Task edit under it (<see cref="MoveWire.HoldMoveAsync"/>),
/// sends the requests one at a time and waits until each is blocked on <c>LCK_M_%</c> before sending the next, so the order
/// they are granted in is the order they queued in. A removal that skips a lock answers before it is ever blocked, and
/// the wait fails.
/// </para>
/// </remarks>
public sealed partial class ProjectEndpointTests
{
    /// <summary>
    /// With an edit of a Task into the third default held under its Project's lock, a Space removal of the third default
    /// queues on that lock; once the edit commits, a removal with no destination is refused <c>409</c> whose report counts the
    /// edited Task, and one with a destination migrates it.
    /// </summary>
    /// <param name="withDestination">Whether the removal names the second default as destination.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC6 - the Space-level removal does not exist yet, so nothing takes the affected Projects' Tasks locks (Q5)")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-27")]
    public async Task A_Space_removal_queued_behind_a_Task_edit_into_the_default_counts_or_migrates_the_edited_Task(bool withDestination)
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var projectId = Guid.Empty;
        var taskId = Guid.Empty;
        IReadOnlyList<StatusRow> defaults = [];
        WireResponse? queued = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            projectId = IdOf(await CreateAsync(owner, "Edited in while waiting"));
            taskId = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, "Edited in"));
            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await MoveWire.HoldMoveAsync(holder, held, projectId, taskId, defaults[2].Id, "V0", cancellationToken);

            var removing = Space.RemoveAsync(actor, owner.SpaceId, defaults[2].Id, withDestination ? defaults[1].Id : null);

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, removing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            queued = await removing;
        });

        Assert.NotNull(owner);

        var task = (await TaskWire.TaskRowsAsync(database, owner.SpaceId)).Single(row => row.Id == taskId);

        if (withDestination)
        {
            Assert.True(queued?.StatusCode == HttpStatusCode.OK, $"{queued?.StatusCode} {queued?.Body}");
            Assert.Equal(defaults[1].Id, task.StatusId);
        }
        else
        {
            RemovalWire.AssertRefused(queued, HttpStatusCode.Conflict, ProblemTypes.StatusDestinationRequired);
            Assert.Equal([(projectId, 1)], SpaceStatusWire.RemovalOf(queued!.Body).Select(entry => (entry.ProjectId, entry.TaskCount)));
            Assert.Equal(defaults[2].Id, task.StatusId);
        }

        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// With a Project's Tasks lock held, a Space removal of the third default queues on it, and an edit of a Task into the
    /// third default queues behind the removal; released, the removal answers <c>200</c> and the edit, which reads the set
    /// only once it holds the lock, is refused <c>400 invalid-task-status</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC6 - the Space-level removal does not exist yet (Q5)")]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-27")]
    public async Task A_Task_edit_into_the_default_queued_behind_the_Space_removal_is_refused_400()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var taskId = Guid.Empty;
        IReadOnlyList<StatusRow> defaults = [];
        WireResponse? removal = null;
        WireResponse? edit = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var projectId = IdOf(await CreateAsync(owner, "Edited too late"));

            taskId = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, "Too late"));
            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteWire.HoldProjectRangeAsync(holder, held, projectId, cancellationToken);

            var removing = Space.RemoveAsync(actor, owner.SpaceId, defaults[2].Id, destinationStatusId: null);

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, removing, cancellationToken);

            var editing = TaskEditWire.EditTaskAsync(actor, owner.SpaceId, taskId, TaskEditWire.Body(new { statusId = defaults[2].Id }));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(removing, editing), cancellationToken);
            await held.CommitAsync(cancellationToken);

            removal = await removing;
            edit = await editing;
        });

        Assert.NotNull(owner);
        Assert.True(removal?.StatusCode == HttpStatusCode.OK, $"{removal?.StatusCode} {removal?.Body}");
        RemovalWire.AssertRefused(edit, HttpStatusCode.BadRequest, ProblemTypes.InvalidTaskStatus);
        Assert.Equal(defaults[0].Id, (await TaskWire.TaskRowsAsync(database, owner.SpaceId)).Single(row => row.Id == taskId).StatusId);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A Project that removed the second default adds it back while a Space removal of the third default holds the Status
    /// lock: the add-back queues on it (Shared behind Exclusive) and, released, applies to the state after the removal - the
    /// Project holds the first and second defaults and not the third.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC6 - the Space-level removal does not exist yet (Q5)")]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-27")]
    public async Task A_Project_add_back_queued_behind_the_Space_removal_applies_to_the_state_after_it()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        IReadOnlyList<StatusRow> defaults = [];
        WireResponse? removal = null;
        WireResponse? restore = null;
        IReadOnlyList<Guid> set = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var projectId = IdOf(await CreateAsync(owner, "Adds back meanwhile"));

            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
            _ = await RemovalWire.RemovedAsync(actor, owner.SpaceId, projectId, defaults[1].Id);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteWire.HoldProjectRangeAsync(holder, held, projectId, cancellationToken);

            var removing = Space.RemoveAsync(actor, owner.SpaceId, defaults[2].Id, destinationStatusId: null);

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, removing, cancellationToken);

            var restoring = RemovalWire.RestoreAsync(actor, owner.SpaceId, projectId, defaults[1].Id);

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(removing, restoring), cancellationToken);
            await held.CommitAsync(cancellationToken);

            removal = await removing;
            restore = await restoring;
            set = [.. (await Wire.EffectiveAsync(actor, owner.SpaceId, projectId)).Select(entry => entry.Id)];
        });

        Assert.NotNull(owner);
        Assert.True(removal?.StatusCode == HttpStatusCode.OK, $"{removal?.StatusCode} {removal?.Body}");
        Assert.True(restore?.StatusCode == HttpStatusCode.OK, $"{restore?.StatusCode} {restore?.Body}");
        Assert.Equal([defaults[0].Id, defaults[1].Id], set);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// The mockup's "no longer has the Status you chose": a Project's removal of the second default queues on its Tasks lock
    /// holding the Status lock Shared, and a Space removal of the first default to the second queues behind it. Released, the
    /// Project removal commits first and the Space removal, judging at commit, sees that Project blocked and is refused
    /// <c>409</c> naming it - never placing its Tasks anywhere.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC4, AC6 - the Space-level removal does not exist yet (Q2: the blocked refusal is a stand-in type)")]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "UX-DR16")]
    public async Task A_Project_that_removes_the_destination_first_makes_the_Space_removal_refuse_naming_it()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var projectId = Guid.Empty;
        IReadOnlyList<StatusRow> defaults = [];
        WireResponse? projectRemoval = null;
        WireResponse? spaceRemoval = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            projectId = IdOf(await CreateAsync(owner, "Changed its mind"));
            _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, "Held in the first"));
            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteWire.HoldProjectRangeAsync(holder, held, projectId, cancellationToken);

            var removingHere = RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, defaults[1].Id, destinationStatusId: null);

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, removingHere, cancellationToken);

            var removingEverywhere = Space.RemoveAsync(actor, owner.SpaceId, defaults[0].Id, defaults[1].Id);

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(removingHere, removingEverywhere), cancellationToken);
            await held.CommitAsync(cancellationToken);

            projectRemoval = await removingHere;
            spaceRemoval = await removingEverywhere;
        });

        Assert.NotNull(owner);
        Assert.True(projectRemoval?.StatusCode == HttpStatusCode.OK, $"{projectRemoval?.StatusCode} {projectRemoval?.Body}");
        RemovalWire.AssertRefused(spaceRemoval, HttpStatusCode.Conflict, SpaceStatusWire.StatusProjectDestinationRequired);
        Assert.Equal([(projectId, 1)], SpaceStatusWire.RemovalOf(spaceRemoval!.Body).Select(entry => (entry.ProjectId, entry.TaskCount)));
        Assert.Equal(3, (await Wire.DefaultsAsync(database, owner.SpaceId)).Count);
        Assert.All(await TaskWire.TaskRowsAsync(database, owner.SpaceId), row => Assert.Equal(defaults[0].Id, row.StatusId));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A Space removal of the third default queued on a held Project's Tasks lock already holds the Space's Project list: a
    /// Project creation queues behind it and, released, answers <c>201</c> after the removal, with a set that does not hold
    /// the third default.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC6 - the Space-level removal does not exist yet, so nothing takes ProjectsPerSpace (Q5)")]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-27")]
    public async Task A_Project_created_while_the_Space_removal_holds_the_Project_list_waits_and_holds_a_set_without_the_default()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        IReadOnlyList<StatusRow> defaults = [];
        WireResponse? removal = null;
        WireResponse? creation = null;
        IReadOnlyList<Guid> set = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var projectId = IdOf(await CreateAsync(owner, "Holds the third"));

            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteWire.HoldProjectRangeAsync(holder, held, projectId, cancellationToken);

            var removing = Space.RemoveAsync(actor, owner.SpaceId, defaults[2].Id, destinationStatusId: null);

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, removing, cancellationToken);

            var creating = CreateAsync(owner, "Created meanwhile");

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(removing, creating), cancellationToken);
            await held.CommitAsync(cancellationToken);

            removal = await removing;
            creation = await creating;
            set = [.. (await Wire.EffectiveAsync(actor, owner.SpaceId, IdOf(creation))).Select(entry => entry.Id)];
        });

        Assert.NotNull(owner);
        Assert.True(removal?.StatusCode == HttpStatusCode.OK, $"{removal?.StatusCode} {removal?.Body}");
        Assert.True(creation?.StatusCode == HttpStatusCode.Created, $"{creation?.StatusCode} {creation?.Body}");
        Assert.Equal([defaults[0].Id, defaults[1].Id], set);
    }

    /// <summary>
    /// The atomicity case for the Space-level removal: three Projects - one taking the Space-wide destination, one blocked and given
    /// an exception, one that had already removed the Status - and a trigger that throws at the default's delete, the last
    /// statement. The request answers <c>5xx</c>; every Task in every Project still holds the Status at its old key; every
    /// operation, every definition and every Project's set is as before. Anchored on the same request answering <c>200</c>
    /// once the trigger is dropped.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC10 - the Space-level removal does not exist yet (Q13: the trigger fires at the definition delete)")]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "AR-24")]
    [Trait("Requirement", "FR-27")]
    public async Task A_failure_forced_at_the_default_delete_rolls_back_every_Project()
    {
        var database = Available();
        Actor? owner = null;
        SpaceRemovalSeed? seed = null;
        SpaceSnapshot? before = null;
        SpaceSnapshot? afterFailure = null;
        var setsBefore = new Dictionary<Guid, IReadOnlyList<string>>();
        var setsAfterFailure = new Dictionary<Guid, IReadOnlyList<string>>();
        WireResponse? forced = null;
        WireResponse? anchor = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            seed = await SeedRemovalAsync(owner, database);

            var projects = new[] { seed.Plain, seed.Blocked, seed.Renamed, seed.RemovedIt };

            foreach (var projectId in projects)
            {
                setsBefore[projectId] = [.. (await Wire.EffectiveAsync(actor, owner.SpaceId, projectId)).Select(Shown)];
            }

            before = await SnapshotAsync(database, owner.SpaceId);

            var trigger = await Space.PlantFailingDefinitionDeleteTriggerAsync(database, seed.Defaults[0].Id);

            try
            {
                forced = await Space.RemoveAsync(actor, owner.SpaceId, seed.Defaults[0].Id, seed.Defaults[1].Id, [(seed.Blocked, seed.Parked)]);
            }
            finally
            {
                await Space.DropTriggersAsync(database, [trigger]);
            }

            afterFailure = await SnapshotAsync(database, owner.SpaceId);

            foreach (var projectId in projects)
            {
                setsAfterFailure[projectId] = [.. (await Wire.EffectiveAsync(actor, owner.SpaceId, projectId)).Select(Shown)];
            }

            await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
            anchor = await Space.RemoveAsync(actor, owner.SpaceId, seed.Defaults[0].Id, seed.Defaults[1].Id, [(seed.Blocked, seed.Parked)]);
        });

        Assert.NotNull(owner);
        Assert.NotNull(seed);
        Assert.NotNull(forced);
        Assert.True(forced.StatusCode is >= HttpStatusCode.InternalServerError, $"{forced.StatusCode} {forced.Body}");
        Assert.Equal(before, afterFailure);
        Assert.All(setsBefore, pair => Assert.Equal(pair.Value, setsAfterFailure[pair.Key]));
        Assert.True(anchor?.StatusCode == HttpStatusCode.OK, $"{anchor?.StatusCode} {anchor?.Body}");
        Assert.DoesNotContain(await Wire.DefinitionsAsync(database, owner.SpaceId), row => row.Id == seed.Defaults[0].Id);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }
}
