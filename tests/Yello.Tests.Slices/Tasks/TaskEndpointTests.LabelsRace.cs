using System.Net;
using Xunit;
using Yello.Contracts;
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
    [Fact]
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
        Assert.Equal(ProblemTypes.LabelInUse, Problem(answer!)?.Type);
        Assert.Equal([labelId], (await LabelRowsAsync(database, owner.SpaceId)).Select(row => row.Id));
        Assert.Equal([labelId], await AppliedToAsync(database, owner.SpaceId, taskId));
    }

    /// <summary>
    /// AC10 (the deletion wins) - the test deletes the Label in its own open transaction; an edit applying it queues;
    /// the test commits; the edit answers <c>400 invalid-task-labels</c>, never <c>500</c>, and no <c>TaskLabel</c> row
    /// exists.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
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
        Assert.Equal(ProblemTypes.InvalidTaskLabels, Problem(answer!)?.Type);
        Assert.Empty(await ApplicationsAsync(database, owner.SpaceId));
        Assert.Empty(await LabelRowsAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// AC11, Q2 (a duplicate is detected by the unique index, never a pre-check alone) - the test defines a Label of a
    /// name in its own open transaction; a <c>POST</c> of the same name, differently cased, queues on the index key; the
    /// test commits; the request answers <c>409 label-name-taken</c>, never <c>500</c>, and one Label exists.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
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
        Assert.Equal(ProblemTypes.LabelNameTaken, Problem(answer!)?.Type);
        Assert.Equal([heldLabel], (await LabelRowsAsync(database, owner.SpaceId)).Select(row => row.Id));
    }

    /// <summary>
    /// AC10 (the application read its Labels first) - an edit applying a Label has read it and is held before it writes
    /// (the test holds an uncommitted application of another Label to the same Task, which the edit's read of the
    /// Task's current set waits on); a <c>DELETE</c> of the Label then queues on the edit's lock on it; the test rolls
    /// back; the edit answers <c>200</c> and the deletion <c>409 label-in-use</c>, never a <c>500</c>. Added by the dev
    /// phase: RACE-002 cannot see the apply's <c>UPDLOCK, HOLDLOCK</c> - a read-committed read already waits on the
    /// deleted, uncommitted row - so this is the interleaving the hint exists for. Without it the deletion commits
    /// under the held edit, whose insert then meets the composite foreign key: a <c>500</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-22")]
    [Trait("Requirement", "AR-34")]
    public async Task A_deletion_racing_an_application_that_has_read_its_Labels_waits_and_answers_409()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var taskId = Guid.Empty;
        var labelId = Guid.Empty;
        WireResponse? applied = null;
        WireResponse? deleted = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var (projectId, createdTask) = await ProjectWithTaskAsync(owner, "Being labelled");

            taskId = createdTask;

            // Defined first, so its id sorts before the contested Label's (sequential ids): the held application then
            // lies outside the deletion's in-use range, and only the edit's Label lock can make the deletion wait.
            var heldLabel = await SeedAsync(owner, owner.SpaceId, "Held aside");

            labelId = await SeedAsync(owner, owner.SpaceId, "Contested");

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, projectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await InsertApplicationAsync(holder, held, owner.SpaceId, taskId, heldLabel, cancellationToken);

            var applying = EditTaskAsync(owner, owner.SpaceId, taskId, ApplyBody(labelId));

            await WaitUntilBlockedOnALockAsync(observer, 1, applying, cancellationToken);

            var deleting = DeleteAsync(owner, owner.SpaceId, labelId);

            await WaitUntilBlockedOrAnsweredAsync(observer, 2, deleting, cancellationToken);
            await held.RollbackAsync(cancellationToken);

            applied = await applying;
            deleted = await deleting;
        });

        Assert.NotNull(owner);
        Assert.True(applied?.StatusCode == HttpStatusCode.OK, $"{applied?.StatusCode} {applied?.Body}");
        Assert.True(deleted?.StatusCode == HttpStatusCode.Conflict, $"{deleted?.StatusCode} {deleted?.Body}");
        Assert.Equal(ProblemTypes.LabelInUse, Problem(deleted!)?.Type);
        Assert.Contains(labelId, (await LabelRowsAsync(database, owner.SpaceId)).Select(row => row.Id));
        Assert.Equal([labelId], await AppliedToAsync(database, owner.SpaceId, taskId));
    }

    /// <summary>
    /// Q1 (a), the whole set (added at code review) - the test is a first Labels edit of the Task: it takes the Task's
    /// row and its <c>TaskLabel</c> range as the product does, and applies one Label in its own open transaction; a
    /// second edit setting a different Label queues; the test commits; the second edit answers <c>200</c> and the Task
    /// carries exactly the second edit's set. Without the edit's Task lock the second edit reads the Task's applications
    /// before the test's is committed, queues only to insert, and stores both Labels - a set neither edit sent (and two
    /// removals of one row meet as a <c>500</c>).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-22")]
    [Trait("Requirement", "AR-34")]
    public async Task A_Labels_edit_racing_another_of_the_same_Task_stores_exactly_its_own_set()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var taskId = Guid.Empty;
        var second = Guid.Empty;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var (projectId, createdTask) = await ProjectWithTaskAsync(owner, "Being relabelled");

            taskId = createdTask;

            var first = await SeedAsync(owner, owner.SpaceId, "First");

            second = await SeedAsync(owner, owner.SpaceId, "Second");

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, projectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await HoldTaskLabelsAsync(holder, held, owner.SpaceId, taskId, cancellationToken);

            var racing = EditTaskAsync(owner, owner.SpaceId, taskId, ApplyBody(second));

            await WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await InsertApplicationAsync(holder, held, owner.SpaceId, taskId, first, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.True(answer?.StatusCode == HttpStatusCode.OK, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal([second], await AppliedToAsync(database, owner.SpaceId, taskId));
    }
}
