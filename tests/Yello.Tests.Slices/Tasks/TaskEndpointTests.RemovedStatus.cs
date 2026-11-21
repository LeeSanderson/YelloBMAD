using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Boards;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.TaskEditWire;
using static Yello.Tests.Slices.Tasks.TaskWire;
using RemovalWire = Yello.Tests.Slices.Projects.StatusRemovalWire;
using StatusWire = Yello.Tests.Slices.Projects.ProjectStatusSliceWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// The Task writers against a Status leaving its Project's set: an edit, a move or a creation that queues behind the
/// removal reads the set only after the Project's lock, so it never writes the Status the removal took away; and a removed
/// Status is refused afterwards exactly as a random id is.
/// </summary>
/// <remarks>
/// <para>
/// <b>The decisive cases hold the removal itself.</b> The test plays the removal of an unoccupied Space default in its own
/// open transaction (<see cref="RemovalWire.HoldRemovalAsync"/>: the Project's lock, then the <c>Remove</c> row), sends the
/// Task write, waits until it is blocked on <c>LCK_M_%</c>, then commits. A writer that validated its Status before the lock
/// read the set without the removal (RCSI reads the last committed version), so it writes the removed Status once the lock is
/// free: <c>200</c>, and the Remove-aware S-2 statement counts the stranded Task. A writer that validates after the lock reads
/// the committed removal and answers <c>400 invalid-task-status</c>.
/// </para>
/// <para>
/// Pre-implementation every case here fails at the first read of the set: the product cannot read a <c>Remove</c> row.
/// </para>
/// </remarks>
public sealed partial class TaskEndpointTests
{
    /// <summary>
    /// With the removal of the third default held under the Project's lock, an edit of a Task's Status to it queues; once
    /// the removal commits the edit is refused <c>400 invalid-task-status</c>, byte-identical to a random id's refusal; the
    /// Task keeps its Status and key; and no Task holds a Status outside its Project's set.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-26")]
    public async Task An_edit_to_a_Status_queued_behind_its_removal_is_refused_400_invalid_task_status()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var taskId = Guid.Empty;
        TaskRow? before = null;
        TaskRow? after = null;
        WireResponse? queued = null;
        WireResponse? random = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var (projectId, task) = await ProjectWithTaskAsync(owner, "Edited into a leaving Status");
            var third = (await StatusWire.DefaultsAsync(database, owner.SpaceId))[2].Id;

            taskId = task;
            before = await RowAsync(database, owner.SpaceId, taskId);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await RemovalWire.HoldRemovalAsync(holder, held, owner.SpaceId, projectId, third, cancellationToken);

            var editing = EditTaskAsync(owner, owner.SpaceId, taskId, Body(new { statusId = third }));

            await WaitUntilBlockedOnALockAsync(observer, 1, editing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            queued = await editing;
            after = await RowAsync(database, owner.SpaceId, taskId);
            random = await EditTaskAsync(owner, owner.SpaceId, taskId, Body(new { statusId = Guid.NewGuid() }));
        });

        Assert.NotNull(owner);
        AssertRefusedAsARandomStatus(queued, random);
        Assert.Equal(before, after);
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// With the removal of the third default held under the Project's lock, a move of a Task to it queues; once the removal
    /// commits the move is refused <c>400 invalid-task-status</c>, byte-identical to a random id's refusal; the Task keeps
    /// its Status and key; and no Task holds a Status outside its Project's set.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-26")]
    public async Task A_move_to_a_Status_queued_behind_its_removal_is_refused_400_invalid_task_status()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var taskId = Guid.Empty;
        TaskRow? before = null;
        TaskRow? after = null;
        WireResponse? queued = null;
        WireResponse? random = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var (projectId, task) = await ProjectWithTaskAsync(owner, "Moved into a leaving Status");
            var third = (await StatusWire.DefaultsAsync(database, owner.SpaceId))[2].Id;

            taskId = task;
            before = await RowAsync(database, owner.SpaceId, taskId);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await RemovalWire.HoldRemovalAsync(holder, held, owner.SpaceId, projectId, third, cancellationToken);

            var moving = MoveWire.MoveTaskAsync(owner, owner.SpaceId, taskId, third, afterTaskId: null);

            await WaitUntilBlockedOnALockAsync(observer, 1, moving, cancellationToken);
            await held.CommitAsync(cancellationToken);

            queued = await moving;
            after = await RowAsync(database, owner.SpaceId, taskId);
            random = await MoveWire.MoveTaskAsync(owner, owner.SpaceId, taskId, Guid.NewGuid(), afterTaskId: null);
        });

        Assert.NotNull(owner);
        AssertRefusedAsARandomStatus(queued, random);
        Assert.Equal(before, after);
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// With the removal of the Project's first Status held under its lock, a Task creation queues (the Tasks-per-Project
    /// bound count takes the same lock before the handler reads the set); once the removal commits the Task is created in
    /// the new first Status, the second default.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    public async Task A_creation_queued_behind_the_removal_of_the_first_Status_lands_in_the_new_first_Status()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var createdId = Guid.Empty;
        IReadOnlyList<StatusRow> defaults = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var projectId = await CreateProjectAsync(owner, "Loses its first Status");

            defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            // Present in the Space before the race, so the creation meets the Project's lock and nowhere earlier.
            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, projectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await RemovalWire.HoldRemovalAsync(holder, held, owner.SpaceId, projectId, defaults[0].Id, cancellationToken);

            var creating = CreateTaskAsync(owner, owner.SpaceId, projectId, "Created after the removal");

            await WaitUntilBlockedOnALockAsync(observer, 1, creating, cancellationToken);
            await held.CommitAsync(cancellationToken);

            createdId = IdOf(await creating);
        });

        Assert.NotNull(owner);
        Assert.Equal(defaults[1].Id, (await RowAsync(database, owner.SpaceId, createdId))?.StatusId);
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Once a Space default is removed from a Project through the product, an edit or a move of one of its Tasks to it is
    /// refused <c>400 invalid-task-status</c> byte-identical to a random id's, nothing changes, and the Board's column page for
    /// it answers <c>404</c> byte-identical to a random Status's column page.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-26")]
    public async Task A_removed_Status_is_refused_for_an_edit_and_a_move_and_its_column_page_is_404()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        TaskRow? before = null;
        TaskRow? after = null;
        WireResponse? edit = null;
        WireResponse? editRandom = null;
        WireResponse? move = null;
        WireResponse? moveRandom = null;
        WireResponse? page = null;
        WireResponse? randomPage = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var (projectId, task) = await ProjectWithTaskAsync(owner, "Stays where it is");
            var third = (await StatusWire.DefaultsAsync(database, owner.SpaceId))[2].Id;

            taskId = task;
            _ = await RemovalWire.RemovedAsync(owner, owner.SpaceId, projectId, third);
            before = await RowAsync(database, owner.SpaceId, taskId);
            edit = await EditTaskAsync(owner, owner.SpaceId, taskId, Body(new { statusId = third }));
            editRandom = await EditTaskAsync(owner, owner.SpaceId, taskId, Body(new { statusId = Guid.NewGuid() }));
            move = await MoveWire.MoveTaskAsync(owner, owner.SpaceId, taskId, third, afterTaskId: null);
            moveRandom = await MoveWire.MoveTaskAsync(owner, owner.SpaceId, taskId, Guid.NewGuid(), afterTaskId: null);
            after = await RowAsync(database, owner.SpaceId, taskId);
            page = await owner.Session.GetAsync(BoardRoutes.ColumnOf(owner.SpaceId, projectId, third));
            randomPage = await owner.Session.GetAsync(BoardRoutes.ColumnOf(owner.SpaceId, projectId, Guid.NewGuid()));
        });

        Assert.NotNull(owner);
        AssertRefusedAsARandomStatus(edit, editRandom);
        AssertRefusedAsARandomStatus(move, moveRandom);
        Assert.Equal(before, after);
        Assert.NotNull(page);
        Assert.NotNull(randomPage);
        Assert.True(page.StatusCode == HttpStatusCode.NotFound, $"{page.StatusCode} {page.Body}");
        Assert.Equal(randomPage.StatusCode, page.StatusCode);
        Assert.Equal(randomPage.Body, page.Body);
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    private static void AssertRefusedAsARandomStatus(WireResponse? answer, WireResponse? random)
    {
        Assert.NotNull(answer);
        Assert.NotNull(random);
        Assert.True(answer.StatusCode == HttpStatusCode.BadRequest, $"{answer.StatusCode} {answer.Body}");
        Assert.Equal(ProblemTypes.InvalidTaskStatus, Problem(answer)?.Type);
        Assert.Equal(random.StatusCode, answer.StatusCode);
        Assert.Equal(random.ContentType, answer.ContentType);
        Assert.Equal(random.Body, answer.Body);
    }
}
