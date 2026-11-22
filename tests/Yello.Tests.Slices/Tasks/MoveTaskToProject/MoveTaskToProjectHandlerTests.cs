using Xunit;
using Yello.Application.Spaces;
using Yello.Application.Tasks.MoveTaskToProject;
using Yello.Domain.Bounds;
using Yello.Domain.Statuses;
using Yello.Domain.Tasks;

namespace Yello.Tests.Slices.Tasks.MoveTaskToProject;

/// <summary>
/// The move-into-another-Project slice: plain reads refuse what the caller cannot see before any lock; both Projects are
/// locked in one call; every decision after that is made on reads taken under the locks, in the order the Status, the bound
/// and then the write; and each refusal leaves the store unasked to move.
/// </summary>
/// <remarks>
/// No database: <c>TaskEndpointTests.ProjectMove</c> and <c>.ProjectMoveRace</c> hold the locks, the keys and the bodies.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "FR-41")]
[Trait("Requirement", "AR-24")]
[Trait("Requirement", "AD-17")]
public sealed class MoveTaskToProjectHandlerTests
{
    private static readonly int Limit = BoundRegistry.For(Bound.TasksPerProject).Limit;

    /// <summary>
    /// A Task the store cannot see is not found, and no lock is taken.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_Task_that_is_not_visible_is_not_found_before_any_lock()
    {
        var world = new World();
        var store = World.Store(visible: null);

        var result = await world.Handler(store).HandleAsync(Guid.NewGuid(), world.Destination, statusId: null, TestContext.Current.CancellationToken);

        Assert.Equal(MoveTaskToProjectOutcome.NotFound, result.Outcome);
        Assert.DoesNotContain(store.Calls, call => call.StartsWith("lock", StringComparison.Ordinal));
        Assert.Empty(store.Moves);
    }

    /// <summary>
    /// A destination that is not visible, or is the Task's own Project, is refused before any lock.
    /// </summary>
    /// <param name="own">Whether the destination named is the Task's own Project.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "FR-19")]
    public async Task A_destination_that_is_not_another_visible_Project_is_refused_before_any_lock(bool own)
    {
        var world = new World();
        var task = world.Task();
        var store = World.Store(task, destinationExists: [false]);

        var result = await world.Handler(store).HandleAsync(task.Id, own ? task.ProjectId : world.Destination, statusId: null, TestContext.Current.CancellationToken);

        Assert.Equal(MoveTaskToProjectOutcome.ProjectInvalid, result.Outcome);
        Assert.DoesNotContain(store.Calls, call => call.StartsWith("lock", StringComparison.Ordinal));
        Assert.Empty(store.Moves);
    }

    /// <summary>
    /// Both Projects are locked in one call, and the Task, the destination, its set and its count are all read again after
    /// it, in that order, before the one write.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-25")]
    public async Task Both_Projects_are_locked_in_one_call_and_every_decision_is_read_after_it()
    {
        var world = new World();
        var task = world.Task();
        var store = World.Store(task);

        var result = await world.Handler(store).HandleAsync(task.Id, world.Destination, statusId: null, TestContext.Current.CancellationToken);

        Assert.Equal(MoveTaskToProjectOutcome.Moved, result.Outcome);
        Assert.Equal(
            [
                "find",
                "project",
                $"lock {task.ProjectId:D} {world.Destination:D}",
                "find",
                "project",
                "read the set",
                $"count {world.Destination:D}",
                $"move {task.ProjectId:D} {world.Destination:D} {task.StatusId:D}",
            ],
            store.Calls);
    }

    /// <summary>
    /// A Task that is gone, or in another Project, when it is read again under the locks is not found and not moved.
    /// </summary>
    /// <param name="gone">Whether the Task is gone rather than moved elsewhere.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_Task_that_left_its_Project_while_the_move_waited_is_not_found(bool gone)
    {
        var world = new World();
        var task = world.Task();
        var moved = new TaskItem
        {
            Id = task.Id,
            SpaceId = task.SpaceId,
            ProjectId = Guid.NewGuid(),
            StatusId = task.StatusId,
            Title = task.Title,
            PositionKey = task.PositionKey,
            CreatedAt = task.CreatedAt,
        };
        var store = new ScriptedStore(task, gone ? null : moved, [true, true]);

        var result = await world.Handler(store).HandleAsync(task.Id, world.Destination, statusId: null, TestContext.Current.CancellationToken);

        Assert.Equal(MoveTaskToProjectOutcome.NotFound, result.Outcome);
        Assert.Empty(store.Moves);
    }

    /// <summary>
    /// A destination deleted while the move waited for its lock is refused as a destination that never existed.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_destination_deleted_while_the_move_waited_is_refused()
    {
        var world = new World();
        var task = world.Task();
        var store = World.Store(task, destinationExists: [true, false]);

        var result = await world.Handler(store).HandleAsync(task.Id, world.Destination, statusId: null, TestContext.Current.CancellationToken);

        Assert.Equal(MoveTaskToProjectOutcome.ProjectInvalid, result.Outcome);
        Assert.Empty(store.Moves);
    }

    /// <summary>
    /// With no Status named, a Task whose Status the destination holds keeps it; one whose Status it lacks needs one, and
    /// nothing is counted or moved.
    /// </summary>
    /// <param name="held">Whether the destination's set holds the Task's Status.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "FR-26")]
    public async Task With_no_Status_named_the_Tasks_own_is_kept_or_a_destination_Status_is_required(bool held)
    {
        var world = new World();
        var task = world.Task(statusId: held ? world.Defaults[1].Id : Guid.NewGuid());
        var store = World.Store(task);

        var result = await world.Handler(store).HandleAsync(task.Id, world.Destination, statusId: null, TestContext.Current.CancellationToken);

        if (held)
        {
            Assert.Equal(MoveTaskToProjectOutcome.Moved, result.Outcome);
            Assert.Equal((task.Id, task.ProjectId, world.Destination, task.StatusId), Assert.Single(store.Moves));

            return;
        }

        Assert.Equal(MoveTaskToProjectOutcome.StatusDestinationRequired, result.Outcome);
        Assert.DoesNotContain(store.Calls, call => call.StartsWith("count", StringComparison.Ordinal));
        Assert.Empty(store.Moves);
    }

    /// <summary>
    /// A Status named is applied when the destination holds it, even when the Task's own would have been kept, and refused
    /// when it does not.
    /// </summary>
    /// <param name="held">Whether the destination's set holds the Status named.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "FR-20")]
    public async Task A_Status_named_is_applied_when_the_destination_holds_it_and_refused_otherwise(bool held)
    {
        var world = new World();
        var task = world.Task(statusId: world.Defaults[0].Id);
        var named = held ? world.Defaults[2].Id : Guid.NewGuid();
        var store = World.Store(task);

        var result = await world.Handler(store).HandleAsync(task.Id, world.Destination, named, TestContext.Current.CancellationToken);

        if (held)
        {
            Assert.Equal(MoveTaskToProjectOutcome.Moved, result.Outcome);
            Assert.Equal((task.Id, task.ProjectId, world.Destination, named), Assert.Single(store.Moves));

            return;
        }

        Assert.Equal(MoveTaskToProjectOutcome.StatusDestinationInvalid, result.Outcome);
        Assert.Empty(store.Moves);
    }

    /// <summary>
    /// At its limit the destination refuses the move with the Tasks-per-Project bound; one below it admits the move.
    /// </summary>
    /// <param name="count">The destination's Tasks.</param>
    /// <param name="admitted">Whether the move is admitted.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(4_999, true)]
    [InlineData(5_000, false)]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "AD-25")]
    public async Task The_destination_is_counted_against_its_bound(int count, bool admitted)
    {
        Assert.Equal(5_000, Limit);

        var world = new World { Count = count };
        var task = world.Task();
        var store = World.Store(task);

        var result = await world.Handler(store).HandleAsync(task.Id, world.Destination, statusId: null, TestContext.Current.CancellationToken);

        if (admitted)
        {
            Assert.Equal(MoveTaskToProjectOutcome.Moved, result.Outcome);
            Assert.Single(store.Moves);

            return;
        }

        Assert.Equal(MoveTaskToProjectOutcome.BoundReached, result.Outcome);
        Assert.Equal(Bound.TasksPerProject, result.Bound?.Bound);
        Assert.Empty(store.Moves);
    }

    /// <summary>
    /// The Task and the Space's defaults a case moves between two Projects.
    /// </summary>
    private sealed class World
    {
        private readonly Guid _spaceId = Guid.NewGuid();

        public World()
        {
            Defaults = [Status(_spaceId, 0), Status(_spaceId, 1), Status(_spaceId, 2)];
        }

        public Guid Destination { get; } = Guid.NewGuid();

        public IReadOnlyList<StatusDefinition> Defaults { get; }

        public int Count { get; init; }

        public TaskItem Task(Guid? statusId = null) => new()
        {
            Id = Guid.NewGuid(),
            SpaceId = _spaceId,
            ProjectId = Guid.NewGuid(),
            StatusId = statusId ?? Defaults[0].Id,
            Title = "Draft the brief",
            PositionKey = PositionKey.First,
            CreatedAt = DateTimeOffset.UnixEpoch,
        };

        public static ScriptedStore Store(TaskItem? visible, bool[]? destinationExists = null) =>
            new(visible, visible, destinationExists ?? [true, true]);

        public MoveTaskToProjectHandler Handler(ScriptedStore store) =>
            new(store, new FixedStatuses(store.Calls, Defaults), new FixedCount(store.Calls, Count));

        private static StatusDefinition Status(Guid spaceId, int position) => new()
        {
            Id = Guid.NewGuid(),
            SpaceId = spaceId,
            Name = $"Status {position}",
            Position = position,
            CreatedAt = DateTimeOffset.UnixEpoch,
        };
    }

    private sealed class FixedStatuses(List<string> calls, IReadOnlyList<StatusDefinition> statuses) : IStatusDefinitionReader
    {
        public Task<IReadOnlyList<StatusDefinition>> ListAsync(CancellationToken cancellationToken)
        {
            calls.Add("read the set");

            return Task.FromResult(statuses);
        }

        public Task<ProjectStatusDelta> DeltaOfAsync(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(ProjectStatusDelta.Empty);
    }

    private sealed class FixedCount(List<string> calls, int count) : IBoundCounter
    {
        public Task<int> CountAsync(Bound bound, Guid? scopeId, CancellationToken cancellationToken)
        {
            Assert.Equal(Bound.TasksPerProject, bound);
            calls.Add($"count {scopeId:D}");

            return Task.FromResult(count);
        }
    }

    /// <summary>
    /// Answers the first read of the Task and of the destination as they were before the locks, and every later one as they
    /// are under them.
    /// </summary>
    private sealed class ScriptedStore(TaskItem? visible, TaskItem? underTheLocks, bool[] destinationExists) : ITaskStore
    {
        private int _finds;
        private int _projectReads;

        public List<(Guid TaskId, Guid Source, Guid Destination, Guid StatusId)> Moves { get; } = [];

        public List<string> Calls { get; } = [];

        public Task<TaskItem?> CreateAsync(Guid spaceId, Guid projectId, Guid statusId, string title, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The move creates no Task.");

        public Task<TaskItem?> FindAsync(Guid taskId, CancellationToken cancellationToken)
        {
            Calls.Add("find");

            var found = _finds++ == 0 ? visible : underTheLocks;

            return Task.FromResult(found?.Id == taskId ? found : null);
        }

        public Task LockProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("A move between Projects takes both locks in one call.");

        public Task LockProjectsAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken)
        {
            Calls.Add("lock " + string.Join(' ', projectIds.Select(id => id.ToString("D"))));

            return Task.CompletedTask;
        }

        public Task<bool> ProjectExistsAsync(Guid projectId, CancellationToken cancellationToken)
        {
            Calls.Add("project");

            return Task.FromResult(destinationExists[Math.Min(_projectReads++, destinationExists.Length - 1)]);
        }

        public Task<TaskItem?> MoveToProjectAsync(Guid taskId, Guid sourceProjectId, Guid destinationProjectId, Guid statusId, CancellationToken cancellationToken)
        {
            Calls.Add($"move {sourceProjectId:D} {destinationProjectId:D} {statusId:D}");
            Moves.Add((taskId, sourceProjectId, destinationProjectId, statusId));

            return Task.FromResult<TaskItem?>(new TaskItem
            {
                Id = taskId,
                SpaceId = visible!.SpaceId,
                ProjectId = destinationProjectId,
                StatusId = statusId,
                Title = visible.Title,
                PositionKey = PositionKey.First,
                CreatedAt = visible.CreatedAt,
            });
        }

        public Task<TaskItem?> EditAsync(Guid taskId, TaskEdit edit, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The move edits nothing else.");

        public Task<bool> ReplaceLabelsAsync(Guid taskId, IReadOnlyList<Guid> labelIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The move applies no Label.");

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LabelIdsOfAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(new Dictionary<Guid, IReadOnlyList<Guid>>());

        public Task<TaskDeletion> DeleteAsync(Guid taskId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The move deletes nothing.");

        public Task<TaskMove> MoveAsync(Guid taskId, Guid statusId, Guid? afterTaskId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The move between Projects is not the move within one.");
    }
}
