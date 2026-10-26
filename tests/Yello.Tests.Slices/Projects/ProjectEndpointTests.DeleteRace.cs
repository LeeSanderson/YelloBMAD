using System.Net;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Projects;
using Yello.Domain.Tasks;
using Yello.Tests.Shared;
using DeleteWire = Yello.Tests.Slices.Tasks.DeleteWire;
using LabelWire = Yello.Tests.Slices.Tasks.LabelWire;
using TaskEditWire = Yello.Tests.Slices.Tasks.TaskEditWire;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Projects;

/// <summary>
/// Story 2.5 AC10 over the wire, for the Project: a Project deletion is a new writer on rows a Task creation is about to
/// reference, so it must serialise against the creation on the Project's Task range - or a creation that has already
/// read its Project inserts after the deletion commits and meets <c>FK_Task_Project_SpaceId_ProjectId</c> (547) as a
/// <c>500</c>. RACE-P1 and RACE-P2 of story Task 7; AR-34, FR-17, FR-19. Written red by the story 2.5 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>RACE-P1 (a) is the riskiest assumption of the story</b> (Q5): that taking the Project's Task range in the deletion
/// is what serialises it against a creation. It holds the request between two statements of the other writer (the
/// TESTING-CONVENTIONS rule story 2.4 added): the test <i>is</i> the creation - it has counted under the range and read
/// its Project - then sends the deletion and waits until it is blocked <b>or has answered</b>
/// (<see cref="LabelWire.WaitUntilBlockedOrAnsweredAsync"/>), then inserts. A deletion that skipped the range has
/// committed by then, and the test's insert meets 547 - reported as exactly that.
/// </para>
/// <para>
/// <b>The other direction runs today.</b> RACE-P1 (b) and RACE-P2 (b) hold the Project deletion in SQL and make the
/// request a creation or a Status edit - routes that already existed - and ran from the ATDD run on; the two cases whose
/// request is the deletion were activated by the dev phase. RACE-P1 (a) catches a deletion that skips the range (3/3).
/// </para>
/// </remarks>
public sealed partial class ProjectEndpointTests
{
    /// <summary>
    /// RACE-P1 (a) (AC10, Q5) - the test plays a creation that has counted the Project's Tasks under the range and read
    /// the Project; a deletion of the Project queues on the range; the test inserts its Task and commits; the deletion
    /// answers <c>204</c> and takes the new Task with it. Without the deletion's range lock it commits first and the
    /// creation's insert meets 547 - which the product would answer as a <c>500</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-17")]
    [Trait("Assumption", "PRD-12-5")]
    public async Task A_deletion_waits_for_a_creation_that_has_read_its_Project_and_takes_the_new_Task_with_it()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var projectId = Guid.Empty;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projectId = IdOf(await CreateAsync(owner, "Torn down while filled"));

            var statuses = await TaskWire.StatusesAsync(database, owner.SpaceId);

            _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(AsTaskActor(owner), owner.SpaceId, projectId, "Already there"));

            var last = (await TaskWire.TaskRowsAsync(database, owner.SpaceId)).Select(row => row.PositionKey).Max(StringComparer.Ordinal);

            Assert.Equal(HttpStatusCode.OK, (await owner.Session.GetAsync(ProjectRoutes.ListIn(owner.SpaceId))).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            // The creation, up to its insert: the bound's count under the range, then its Project read.
            await DeleteWire.HoldProjectRangeAsync(holder, held, projectId, cancellationToken);
            await DeleteWire.ReadProjectAsync(holder, held, projectId, cancellationToken);

            var racing = DeleteWire.DeleteProjectAsync(AsTaskActor(owner), owner.SpaceId, projectId);

            await LabelWire.WaitUntilBlockedOrAnsweredAsync(observer, 1, racing, cancellationToken);

            var placement = new Tasks.TaskPlacement(Guid.NewGuid(), owner.SpaceId, projectId, statuses[0].Id, PositionKey.After(last));
            var metForeignKey = false;

            try
            {
                await DeleteWire.InsertTaskAsync(holder, held, placement, cancellationToken);
            }
            catch (SqlException exception) when (exception.Number == 547)
            {
                metForeignKey = true;
            }

            Assert.False(
                metForeignKey,
                "The deletion did not wait on the Project's Task range: it committed under a creation that had read its Project, "
                + "whose insert then met the foreign key (547) - a 500 in the product.");

            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.True(answer?.StatusCode == HttpStatusCode.NoContent, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Empty(await ProjectRowsAsync(database, owner.SpaceId));
        Assert.Equal(0, await TaskWire.TaskCountAsync(database, owner.SpaceId, projectId));
    }

    /// <summary>
    /// RACE-P1 (b) (AC10) - the test holds a Project deletion, uncommitted (story Task 3's order: the Project row, the
    /// Project's Task range, the delete); a Task creation in that Project queues on the range; the test commits; the
    /// creation answers <c>404 not-available</c> - the Project is gone - never a <c>500</c> from 547, and no Task exists.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-19")]
    public async Task A_creation_queued_behind_a_Project_deletion_answers_404_never_500()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var projectId = IdOf(await CreateAsync(owner, "Gone before the Task lands"));

            Assert.Equal(HttpStatusCode.OK, (await owner.Session.GetAsync(ProjectRoutes.ListIn(owner.SpaceId))).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteWire.DeleteProjectRowAsync(holder, held, projectId, cancellationToken);

            var racing = TaskWire.CreateTaskAsync(AsTaskActor(owner), owner.SpaceId, projectId, "Too late");

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.True(answer?.StatusCode == HttpStatusCode.NotFound, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal(ProblemTypes.NotAvailable, Problem(answer!)?.Type);
        Assert.Empty(await TaskWire.TaskRowsAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// RACE-P2 (a) (AC10) - a Status edit of a Task in the Project holds the Project's Task range and the Task row; a
    /// deletion of the Project queues; the edit writes and commits; the deletion answers <c>204</c> and no Task of the
    /// Project remains. Never a 1205.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-17")]
    public async Task A_Project_deletion_queued_behind_a_Status_edit_answers_204_once_the_edit_commits()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var projectId = Guid.Empty;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projectId = IdOf(await CreateAsync(owner, "Edited while torn down"));

            var statuses = await TaskWire.StatusesAsync(database, owner.SpaceId);
            var taskId = TaskWire.IdOf(await TaskWire.CreateTaskAsync(AsTaskActor(owner), owner.SpaceId, projectId, "Moving"));

            Assert.Equal(HttpStatusCode.OK, (await owner.Session.GetAsync(ProjectRoutes.ListIn(owner.SpaceId))).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteWire.HoldProjectRangeAsync(holder, held, projectId, cancellationToken);
            await DeleteWire.HoldTaskRowAsync(holder, held, taskId, cancellationToken);

            var racing = DeleteWire.DeleteProjectAsync(AsTaskActor(owner), owner.SpaceId, projectId);

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await DeleteWire.MoveTaskRowAsync(holder, held, taskId, statuses[1].Id, PositionKey.First, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.True(answer?.StatusCode == HttpStatusCode.NoContent, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal(0, await TaskWire.TaskCountAsync(database, owner.SpaceId, projectId));
        Assert.Empty(await ProjectRowsAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// RACE-P2 (b) (AC10) - the test holds a Project deletion, uncommitted; a Status edit of one of its Tasks queues; the
    /// test commits; the edit answers <c>404 not-available</c> - never a <c>200</c> on a Task that is gone, never a
    /// <c>500</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-20")]
    public async Task A_Status_edit_queued_behind_its_Projects_deletion_answers_404()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var projectId = IdOf(await CreateAsync(owner, "Torn down while edited"));
            var statuses = await TaskWire.StatusesAsync(database, owner.SpaceId);
            var taskId = TaskWire.IdOf(await TaskWire.CreateTaskAsync(AsTaskActor(owner), owner.SpaceId, projectId, "Going with it"));

            Assert.Equal(HttpStatusCode.OK, (await owner.Session.GetAsync(ProjectRoutes.ListIn(owner.SpaceId))).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteWire.DeleteProjectRowAsync(holder, held, projectId, cancellationToken);

            var racing = TaskEditWire.EditTaskAsync(AsTaskActor(owner), owner.SpaceId, taskId, TaskEditWire.Body(new { statusId = statuses[1].Id }));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.True(answer?.StatusCode == HttpStatusCode.NotFound, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal(ProblemTypes.NotAvailable, Problem(answer!)?.Type);
        Assert.Empty(await TaskWire.TaskRowsAsync(database, owner.SpaceId));
    }
}
