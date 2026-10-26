using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Domain.Tasks;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.DeleteWire;
using static Yello.Tests.Slices.Tasks.LabelWire;
using static Yello.Tests.Slices.Tasks.TaskEditWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Story 2.5 AC10 over the wire: a Task deletion is a new writer on rows every other Task writer already locks, so it
/// cannot race any of them into a <c>500</c>, a deadlock victim (1205) or a <c>TaskLabel</c> whose Task is gone.
/// RACE-T1..T5 of story Task 7; AR-34, AD-15, FR-22, FR-23. Written red by the story 2.5 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>The interleaving is the test's, never the clock's</b> (TESTING-CONVENTIONS "A race is controlled, never hoped
/// for", "When the race is about a read, the test does the other write", "A race about a lock held between two
/// statements holds the request between them"). The test plays the competing writer in its own open transaction with
/// <see cref="DeleteWire"/>'s holders - each the product's own statement shape - sends the one request, waits until it is
/// blocked on any <c>LCK_M_%</c> (<see cref="TaskWire.WaitUntilBlockedOnALockAsync"/>), finishes the competing write,
/// and commits or rolls back. Every racing Account reads the Board first, so no <c>SpacePresence</c> insert serialises
/// the pair before the race (the 2.2 finding).
/// </para>
/// <para>
/// <b>Which side holds what.</b> RACE-T1 and RACE-T3 (a) hold the Status or Labels edit and make the request the
/// deletion; RACE-T2, RACE-T3 (b) and RACE-T4 hold the deletion (story Task 3's recommended order: the Project's Task
/// range, then the Task row on its clustered key, then the delete) and make the request the other writer. RACE-T1 is the
/// lock-order case: an edit holds the range and then writes the row; a deletion that locked the row before asking for
/// the range would hold what the edit needs while waiting for what the edit has - a 1205, either here or in the request.
/// </para>
/// <para>
/// <b>Three of these run today.</b> RACE-T2, RACE-T3 (b) and RACE-T4 make the request an edit or a Label deletion - routes
/// that exist - against a deletion the test plays in SQL. Where one is green today it stays active as a standing gate
/// (checklist); the rest are skipped <c>red: AC10</c> until the Task DELETE route is mapped.
/// </para>
/// </remarks>
public sealed partial class TaskEndpointTests
{
    /// <summary>
    /// RACE-T1 (AC10, Q5) - a Status edit holds the Project's Task range; a deletion of the Task queues; the edit then
    /// writes the Task's new Status and key and commits; the deletion answers <c>204</c> and the Task is gone. A deletion
    /// that locked the row first and the range second deadlocks with the edit's write - 1205 as the test's exception or as
    /// the request's <c>500</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC10 - DELETE /spaces/{spaceId}/tasks/{taskId} is not mapped yet; it answers the unmatched 404 without waiting (story Task 5)")]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-23")]
    public async Task A_deletion_queued_behind_a_Status_edit_answers_204_once_the_edit_commits()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var doomed = Guid.Empty;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var projectId = await CreateProjectAsync(owner, "Edited while deleted");
            var statuses = await StatusesAsync(database, owner.SpaceId);

            doomed = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Moving, then going"));
            _ = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Bystander"));

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, projectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await HoldProjectRangeAsync(holder, held, projectId, cancellationToken);

            var racing = DeleteTaskAsync(owner, owner.SpaceId, doomed);

            await WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);

            // The edit's own write, after its range: the Task row moved to the (empty) second column's first key.
            await MoveTaskRowAsync(holder, held, doomed, statuses[1].Id, PositionKey.First, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.True(answer?.StatusCode == HttpStatusCode.NoContent, $"{answer?.StatusCode} {answer?.Body}");
        Assert.DoesNotContain(doomed, (await TaskRowsAsync(database, owner.SpaceId)).Select(row => row.Id));
    }

    /// <summary>
    /// RACE-T2 (AC10; 2.3's review patch first exercised for real) - the test holds a deletion of the Task, uncommitted;
    /// a Status edit of it queues; the test commits; the edit answers <c>404 not-available</c> - never <c>200</c> on a
    /// Task that is gone, never a <c>500</c> - and no Task row is written back.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-20")]
    public async Task A_Status_edit_queued_behind_a_deletion_answers_404_once_the_deletion_commits()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var projectId = await CreateProjectAsync(owner, "Deleted while edited");
            var statuses = await StatusesAsync(database, owner.SpaceId);
            var doomed = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Going"));

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, projectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteTaskRowAsync(holder, held, projectId, doomed, cancellationToken);

            var racing = EditTaskAsync(owner, owner.SpaceId, doomed, Body(new { statusId = statuses[1].Id }));

            await WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.True(answer?.StatusCode == HttpStatusCode.NotFound, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal(ProblemTypes.NotAvailable, Problem(answer!)?.Type);
        Assert.Empty(await TaskRowsAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// RACE-T3 (a) (AC10) - a Labels edit holds the Project's range, the Task row and its <c>TaskLabel</c> range, and
    /// applies a Label; a deletion of the Task queues; the edit commits; the deletion answers <c>204</c>, and no
    /// <c>TaskLabel</c> row is left for the gone Task (the cascade took the committed application with it). The Label
    /// itself stays.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC10 - DELETE /spaces/{spaceId}/tasks/{taskId} is not mapped yet (story Task 5)")]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-22")]
    public async Task A_deletion_queued_behind_a_Labels_edit_answers_204_and_leaves_no_application()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var doomed = Guid.Empty;
        var labelId = Guid.Empty;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var (projectId, taskId) = await ProjectWithTaskAsync(owner, "Labelled, then going");

            doomed = taskId;
            labelId = await SeedAsync(owner, owner.SpaceId, "Applied in the race");

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, projectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await HoldProjectRangeAsync(holder, held, projectId, cancellationToken);
            await HoldTaskLabelsAsync(holder, held, owner.SpaceId, doomed, cancellationToken);

            var racing = DeleteTaskAsync(owner, owner.SpaceId, doomed);

            await WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await InsertApplicationAsync(holder, held, owner.SpaceId, doomed, labelId, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.True(answer?.StatusCode == HttpStatusCode.NoContent, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Empty(await ApplicationsAsync(database, owner.SpaceId));
        Assert.Empty(await TaskRowsAsync(database, owner.SpaceId));
        Assert.Equal([labelId], (await LabelRowsAsync(database, owner.SpaceId)).Select(row => row.Id));
    }

    /// <summary>
    /// RACE-T3 (b) (AC10) - the test holds a deletion of the Task, uncommitted; a Labels edit applying a Label to it
    /// queues; the test commits; the edit answers <c>404 not-available</c> - never a <c>500</c> from the composite key,
    /// never a 1205 - and no <c>TaskLabel</c> row exists.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-22")]
    public async Task A_Labels_edit_queued_behind_a_deletion_answers_404_and_writes_no_application()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var (projectId, doomed) = await ProjectWithTaskAsync(owner, "Going while labelled");
            var labelId = await SeedAsync(owner, owner.SpaceId, "Never applied");

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, projectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteTaskRowAsync(holder, held, projectId, doomed, cancellationToken);

            var racing = EditTaskAsync(owner, owner.SpaceId, doomed, ApplyBody(labelId));

            await WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.True(answer?.StatusCode == HttpStatusCode.NotFound, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal(ProblemTypes.NotAvailable, Problem(answer!)?.Type);
        Assert.Empty(await ApplicationsAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// RACE-T4 (AC10, AC7) - the test holds a deletion of the Task carrying a Label's only application (the cascade has
    /// removed the application, uncommitted); a deletion of that Label queues on the removed row; committed, the Label's
    /// deletion answers <c>204</c>; rolled back, it answers <c>409 label-in-use</c> and the application is back. Never a
    /// <c>500</c>, never a 1205.
    /// </summary>
    /// <param name="commit">Whether the held Task deletion commits (otherwise it rolls back).</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-22")]
    public async Task A_Label_deletion_queued_behind_the_deletion_of_its_last_Task_follows_that_deletions_outcome(bool commit)
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var taskId = Guid.Empty;
        var labelId = Guid.Empty;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var (projectId, createdTask) = await ProjectWithTaskAsync(owner, "Last carrier");

            taskId = createdTask;
            labelId = await SeedAsync(owner, owner.SpaceId, "Carried once");

            Assert.Equal(HttpStatusCode.OK, (await EditTaskAsync(owner, owner.SpaceId, taskId, ApplyBody(labelId))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, projectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteTaskRowAsync(holder, held, projectId, taskId, cancellationToken);

            var racing = LabelWire.DeleteAsync(owner, owner.SpaceId, labelId);

            await WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);

            if (commit)
            {
                await held.CommitAsync(cancellationToken);
            }
            else
            {
                await held.RollbackAsync(cancellationToken);
            }

            answer = await racing;
        });

        Assert.NotNull(owner);

        if (commit)
        {
            Assert.True(answer?.StatusCode == HttpStatusCode.NoContent, $"{answer?.StatusCode} {answer?.Body}");
            Assert.Empty(await LabelRowsAsync(database, owner.SpaceId));
            Assert.Empty(await ApplicationsAsync(database, owner.SpaceId));
        }
        else
        {
            Assert.True(answer?.StatusCode == HttpStatusCode.Conflict, $"{answer?.StatusCode} {answer?.Body}");
            Assert.Equal(ProblemTypes.LabelInUse, Problem(answer!)?.Type);
            Assert.Equal([labelId], await AppliedToAsync(database, owner.SpaceId, taskId));
        }
    }

    /// <summary>
    /// RACE-T5 (AC10) - the test holds the Project's Task range (the creation's bound count); a creation in that Project
    /// by a Member and a deletion of another Task in it by the Owner both queue; the test commits; both are admitted
    /// (<c>201</c>, <c>204</c>) in whichever order the range serves them, and the Project holds exactly the created Task
    /// and the bystander.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC10 - DELETE /spaces/{spaceId}/tasks/{taskId} is not mapped yet (story Task 5)")]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "AR-30")]
    public async Task A_creation_and_a_deletion_in_one_Project_both_succeed()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var bystander = Guid.Empty;
        WireResponse[] answers = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");

            var projectId = await CreateProjectAsync(owner, "Busy");
            var doomed = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Going"));

            bystander = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Staying"));

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, projectId)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(member, owner.SpaceId, projectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await HoldProjectRangeAsync(holder, held, projectId, cancellationToken);

            var racing = Task.WhenAll(
                CreateTaskAsync(member, owner.SpaceId, projectId, "Arriving"),
                DeleteTaskAsync(owner, owner.SpaceId, doomed));

            await WaitUntilBlockedOnALockAsync(observer, 2, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answers = await racing;
        });

        Assert.NotNull(owner);
        Assert.True(answers[0].StatusCode == HttpStatusCode.Created, $"{answers[0].StatusCode} {answers[0].Body}");
        Assert.True(answers[1].StatusCode == HttpStatusCode.NoContent, $"{answers[1].StatusCode} {answers[1].Body}");
        Assert.Equal(
            new[] { bystander, IdOf(answers[0]) }.Order(),
            (await TaskRowsAsync(database, owner.SpaceId)).Select(row => row.Id).Order());
    }
}
