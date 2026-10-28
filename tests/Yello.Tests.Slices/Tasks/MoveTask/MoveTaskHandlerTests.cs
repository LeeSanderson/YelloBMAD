using Xunit;
using Yello.Application.Tasks.MoveTask;
using Yello.Domain.Statuses;
using Yello.Domain.Tasks;

namespace Yello.Tests.Slices.Tasks.MoveTask;

/// <summary>
/// Story 2.6 Task 3 - the move slice answers a Task it cannot see as not found, checks the destination Status against the
/// effective set before the store is asked, and answers whatever the store decided under its locks. AC9; FR-29, AD-17.
/// </summary>
/// <remarks>
/// No database: <c>TaskEndpointTests.Move</c> and <c>.MoveRace</c> hold the locks, the keys and the problem bodies.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "FR-29")]
[Trait("Requirement", "AR-3")]
public sealed class MoveTaskHandlerTests
{
    /// <summary>
    /// AC9 - a Task the store cannot see is not found, and nothing is moved.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_Task_that_is_not_visible_is_not_found_and_nothing_moves()
    {
        var spaceId = Guid.NewGuid();
        var doing = Status(spaceId, 1);
        var store = new RecordingStore(visible: null, TaskMove.NotFound);

        var result = await new MoveTaskHandler(store, new FixedStatuses(Status(spaceId, 0), doing))
            .HandleAsync(Guid.NewGuid(), doing.Id, afterTaskId: null, TestContext.Current.CancellationToken);

        Assert.Equal(MoveTaskOutcome.NotFound, result.Outcome);
        Assert.Empty(store.Moves);
    }

    /// <summary>
    /// AD-17 - a Status outside the effective set is refused before the store is asked to move anything.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    public async Task A_Status_outside_the_effective_set_is_refused_before_any_move()
    {
        var spaceId = Guid.NewGuid();
        var task = NewTask(spaceId);
        var store = new RecordingStore(task, TaskMove.Moved(task));

        var result = await new MoveTaskHandler(store, new FixedStatuses(Status(spaceId, 0), Status(spaceId, 1)))
            .HandleAsync(task.Id, Guid.NewGuid(), afterTaskId: null, TestContext.Current.CancellationToken);

        Assert.Equal(MoveTaskOutcome.StatusOutsideTheEffectiveSet, result.Outcome);
        Assert.Empty(store.Moves);
    }

    /// <summary>
    /// AC9 - each outcome the store reaches is the slice's answer, for the Task, Status and neighbour the request named.
    /// </summary>
    /// <param name="outcome">What the store decided.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(TaskMoveOutcome.Moved)]
    [InlineData(TaskMoveOutcome.NotFound)]
    [InlineData(TaskMoveOutcome.PositionUnavailable)]
    public async Task The_store_decides_where_the_Task_lands_and_the_slice_answers_it(TaskMoveOutcome outcome)
    {
        var spaceId = Guid.NewGuid();
        var task = NewTask(spaceId);
        var doing = Status(spaceId, 1);
        Guid? neighbour = Guid.NewGuid();
        var decided = outcome switch
        {
            TaskMoveOutcome.Moved => TaskMove.Moved(task),
            TaskMoveOutcome.NotFound => TaskMove.NotFound,
            _ => TaskMove.PositionUnavailable,
        };
        var store = new RecordingStore(task, decided);

        var result = await new MoveTaskHandler(store, new FixedStatuses(Status(spaceId, 0), doing))
            .HandleAsync(task.Id, doing.Id, neighbour, TestContext.Current.CancellationToken);

        Assert.Equal((task.Id, doing.Id, neighbour), Assert.Single(store.Moves));
        Assert.Equal(
            outcome switch
            {
                TaskMoveOutcome.Moved => MoveTaskOutcome.Moved,
                TaskMoveOutcome.NotFound => MoveTaskOutcome.NotFound,
                _ => MoveTaskOutcome.PositionUnavailable,
            },
            result.Outcome);
        Assert.Equal(outcome == TaskMoveOutcome.Moved ? task : null, result.Task);
    }

    private static TaskItem NewTask(Guid spaceId) => new()
    {
        Id = Guid.NewGuid(),
        SpaceId = spaceId,
        ProjectId = Guid.NewGuid(),
        StatusId = Guid.NewGuid(),
        Title = "Draft the brief",
        PositionKey = PositionKey.First,
        CreatedAt = DateTimeOffset.UnixEpoch,
    };

    private static StatusDefinition Status(Guid spaceId, int position) => new()
    {
        Id = Guid.NewGuid(),
        SpaceId = spaceId,
        Name = $"Status {position}",
        Position = position,
        CreatedAt = DateTimeOffset.UnixEpoch,
    };

    private sealed class FixedStatuses(params StatusDefinition[] statuses) : IStatusDefinitionReader
    {
        public Task<IReadOnlyList<StatusDefinition>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StatusDefinition>>(statuses);
    }

    private sealed class RecordingStore(TaskItem? visible, TaskMove decided) : ITaskStore
    {
        public List<(Guid TaskId, Guid StatusId, Guid? AfterTaskId)> Moves { get; } = [];

        public Task<TaskItem?> CreateAsync(Guid spaceId, Guid projectId, Guid statusId, string title, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The move slice creates no Task.");

        public Task<TaskItem?> FindAsync(Guid taskId, CancellationToken cancellationToken) =>
            Task.FromResult(visible?.Id == taskId ? visible : null);

        public Task<TaskItem?> EditAsync(Guid taskId, TaskEdit edit, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The move slice edits nothing else.");

        public Task<bool> ReplaceLabelsAsync(Guid taskId, IReadOnlyList<Guid> labelIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The move slice applies no Label.");

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LabelIdsOfAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(new Dictionary<Guid, IReadOnlyList<Guid>>());

        public Task<TaskDeletion> DeleteAsync(Guid taskId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The move slice deletes nothing.");

        public Task<TaskMove> MoveAsync(Guid taskId, Guid statusId, Guid? afterTaskId, CancellationToken cancellationToken)
        {
            Moves.Add((taskId, statusId, afterTaskId));

            return Task.FromResult(decided);
        }
    }
}
