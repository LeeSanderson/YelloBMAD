using System.Net;
using Xunit;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.LabelWire;
using static Yello.Tests.Slices.Tasks.TaskEditWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Story 2.4 AC10 (and AC11's duplicate name) over the wire: a Label deletion and an application of the same Label,
/// and two definitions of one name, cannot race into a dangling reference or a <c>500</c>. FR-22, AR-34. Written red by
/// the story 2.4 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>The interleaving is the test's, and the test does the other write</b> (TESTING-CONVENTIONS "A race is
/// controlled, never hoped for" and "When the race is about a read, the test does the other write"): the test opens a
/// connection under the Space's key, writes what the competing request would have written inside its own open
/// transaction, sends the one request, waits until it is blocked on any <c>LCK_M_%</c>
/// (<see cref="TaskWire.WaitUntilBlockedOnALockAsync"/>), and commits. The request must then read the committed state:
/// a read taken without the product's <c>UPDLOCK, HOLDLOCK</c> - or under read-committed snapshot - sees the state
/// before the test's write and acts on it, and the composite foreign key or the unique index answers instead, which is
/// a <c>500</c>.
/// </para>
/// <para>
/// <b>What each plant looks like</b> (story Task 12): drop the delete's probe hint and RACE-001 deletes past the
/// uncommitted application, so the <c>NoAction</c> key fires 547 - the backstop must still answer <c>409</c>, never
/// <c>500</c>; drop the apply's hint and RACE-002 inserts against a Label deleted under it - a 547 answered as a
/// <c>500</c>; answer the duplicate name by a pre-check alone and RACE-003 meets 2601 as a <c>500</c>. Every racing
/// Account reads the Board first, so it does not write its <c>SpacePresence</c> row inside the race (the 2.2 finding).
/// </para>
/// </remarks>
public sealed partial class TaskEndpointTests
{
    /// <summary>
    /// AC10 (the application wins) - the test applies the Label in its own open transaction; a <c>DELETE</c> of that
    /// Label queues on it; the test commits; the deletion answers <c>409 label-in-use</c>, and both the Label and the
    /// application are present.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC10 - no Label delete or TaskLabel table yet (story 2.4 Tasks 3, 5)")]
    [Trait("Requirement", "FR-22")]
    [Trait("Requirement", "AR-34")]
    public async Task A_deletion_racing_an_application_that_commits_first_answers_409_label_in_use()
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

            var (projectId, createdTask) = await ProjectWithTaskAsync(owner, "Being labelled");

            taskId = createdTask;
            labelId = await SeedAsync(owner, owner.SpaceId, "Contested");

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, projectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await InsertApplicationAsync(holder, held, owner.SpaceId, taskId, labelId, cancellationToken);

            var racing = DeleteAsync(owner, owner.SpaceId, labelId);

            await WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.True(answer?.StatusCode == HttpStatusCode.Conflict, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal(LabelScaffold.LabelInUse, Problem(answer!)?.Type);
        Assert.Equal([labelId], (await LabelRowsAsync(database, owner.SpaceId)).Select(row => row.Id));
        Assert.Equal([labelId], await AppliedToAsync(database, owner.SpaceId, taskId));
    }

    /// <summary>
    /// AC10 (the deletion wins) - the test deletes the Label in its own open transaction; an edit applying it queues;
    /// the test commits; the edit answers <c>400 invalid-task-labels</c>, never <c>500</c>, and no <c>TaskLabel</c> row
    /// exists.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC10 - no labelIds member or Label table yet (story 2.4 Tasks 3, 5)")]
    [Trait("Requirement", "FR-22")]
    [Trait("Requirement", "AR-34")]
    public async Task An_application_racing_a_deletion_that_commits_first_answers_400_invalid_task_labels()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var (projectId, taskId) = await ProjectWithTaskAsync(owner, "Being labelled");
            var labelId = await SeedAsync(owner, owner.SpaceId, "Vanishing");

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, projectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteLabelRowAsync(holder, held, labelId, cancellationToken);

            var racing = EditTaskAsync(owner, owner.SpaceId, taskId, ApplyBody(labelId));

            await WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.True(answer?.StatusCode == HttpStatusCode.BadRequest, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal(LabelScaffold.InvalidTaskLabels, Problem(answer!)?.Type);
        Assert.Empty(await ApplicationsAsync(database, owner.SpaceId));
        Assert.Empty(await LabelRowsAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// AC11, Q2 (a duplicate is detected by the unique index, never a pre-check alone) - the test defines a Label of a
    /// name in its own open transaction; a <c>POST</c> of the same name, differently cased, queues on the index key; the
    /// test commits; the request answers <c>409 label-name-taken</c>, never <c>500</c>, and one Label exists.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC11 - no Label route or unique index yet (story 2.4 Tasks 3, 5; Q2)")]
    [Trait("Requirement", "AR-34")]
    [Trait("Assumption", "FR-22-Q2")]
    public async Task Two_definitions_of_one_name_answer_one_201_and_one_409_never_500()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var heldLabel = Guid.NewGuid();
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            // Presence first: the Label list is a Space-scoped read like any other.
            Assert.Equal(HttpStatusCode.OK, (await ListAsync(owner, owner.SpaceId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await InsertLabelAsync(holder, held, heldLabel, owner.SpaceId, "Release", cancellationToken);

            var racing = DefineAsync(owner, owner.SpaceId, "release");

            await WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.True(answer?.StatusCode == HttpStatusCode.Conflict, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal(LabelScaffold.LabelNameTaken, Problem(answer!)?.Type);
        Assert.Equal([heldLabel], (await LabelRowsAsync(database, owner.SpaceId)).Select(row => row.Id));
    }
}
