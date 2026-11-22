using Xunit;
using Yello.Application.Spaces;
using Yello.Application.Tasks.MoveStatusTasksToProject;
using Yello.Domain.Bounds;
using Yello.Domain.Statuses;
using Yello.Domain.Tasks;

namespace Yello.Tests.Slices.Tasks.MoveStatusTasksToProject;

/// <summary>
/// The bulk move slice: plain reads refuse a Project the caller cannot see before any lock; both Projects are locked in one
/// call; every decision after that rests on reads taken under the locks, in the order source, its set, destination, its set,
/// the selection, the bound and the one write; and each refusal leaves the store unasked to move.
/// </summary>
/// <remarks>
/// No database: <c>TaskEndpointTests.StatusBulkMove</c> and <c>.StatusBulkMoveRace</c> hold the locks, the keys and the bodies.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "FR-41")]
[Trait("Requirement", "AR-24")]
[Trait("Requirement", "AD-17")]
public sealed class MoveStatusTasksToProjectHandlerTests
{
    private static readonly int Limit = BoundRegistry.For(Bound.TasksPerProject).Limit;

    /// <summary>
    /// A source the store cannot see is not found, and no lock is taken.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_source_that_is_not_visible_is_not_found_before_any_lock()
    {
        var world = new World();
        var store = world.Store(projectExists: [false]);

        var result = await world.HandleAsync(store, statusId: null);

        Assert.Equal(MoveStatusTasksToProjectOutcome.NotFound, result.Outcome);
        Assert.DoesNotContain(store.Calls, call => call.StartsWith("lock", StringComparison.Ordinal));
        Assert.Empty(store.Moves);
    }

    /// <summary>
    /// A destination that is not visible, or is the source itself, is refused before any lock.
    /// </summary>
    /// <param name="own">Whether the destination named is the source.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "FR-19")]
    public async Task A_destination_that_is_not_another_visible_Project_is_refused_before_any_lock(bool own)
    {
        var world = new World();
        var store = world.Store(projectExists: [true, false]);
        var handler = world.Handler(store);

        var result = await handler.HandleAsync(world.Source, world.Defaults[0].Id, own ? world.Source : world.Destination, statusId: null, TestContext.Current.CancellationToken);

        Assert.Equal(MoveStatusTasksToProjectOutcome.ProjectInvalid, result.Outcome);
        Assert.DoesNotContain(store.Calls, call => call.StartsWith("lock", StringComparison.Ordinal));
        Assert.Empty(store.Moves);
    }

    /// <summary>
    /// Both Projects are locked in one call; the source, its set, the destination, its set, the selection and the
    /// destination's Tasks-per-Project count are all read after it, in that order, before the one write. The bound counted is
    /// the destination's, never the source's.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-25")]
    public async Task Both_Projects_are_locked_in_one_call_and_every_decision_is_read_after_it()
    {
        var world = new World();
        var store = world.Store();

        var result = await world.HandleAsync(store, statusId: null);

        Assert.Equal(MoveStatusTasksToProjectOutcome.Moved, result.Outcome);
        Assert.Equal(world.Selected, result.MovedCount);
        Assert.Equal(
            [
                "project",
                "project",
                $"lock {world.Source:D} {world.Destination:D}",
                "project",
                $"set {world.Source:D}",
                "project",
                $"set {world.Destination:D}",
                $"select {world.Source:D} {world.Defaults[0].Id:D}",
                $"count {world.Destination:D}",
                $"move {world.Source:D} {world.Defaults[0].Id:D} {world.Destination:D} {world.Defaults[0].Id:D}",
            ],
            store.Calls);
    }

    /// <summary>
    /// A source deleted while the move waited for its lock is not found, and nothing is moved.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_source_deleted_while_the_move_waited_is_not_found()
    {
        var world = new World();
        var store = world.Store(projectExists: [true, true, false]);

        var result = await world.HandleAsync(store, statusId: null);

        Assert.Equal(MoveStatusTasksToProjectOutcome.NotFound, result.Outcome);
        Assert.Empty(store.Moves);
    }

    /// <summary>
    /// A Status the source's set does not hold under the locks is not found, and nothing is counted or moved.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_Status_the_source_does_not_hold_is_not_found()
    {
        var world = new World();
        var store = world.Store();

        var result = await world.Handler(store).HandleAsync(world.Source, Guid.NewGuid(), world.Destination, statusId: null, TestContext.Current.CancellationToken);

        Assert.Equal(MoveStatusTasksToProjectOutcome.NotFound, result.Outcome);
        Assert.DoesNotContain(store.Calls, call => call.StartsWith("select", StringComparison.Ordinal) || call.StartsWith("count", StringComparison.Ordinal));
        Assert.Empty(store.Moves);
    }

    /// <summary>
    /// A destination deleted while the move waited for its lock is refused as one that never existed.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_destination_deleted_while_the_move_waited_is_refused()
    {
        var world = new World();
        var store = world.Store(projectExists: [true, true, true, false]);

        var result = await world.HandleAsync(store, statusId: null);

        Assert.Equal(MoveStatusTasksToProjectOutcome.ProjectInvalid, result.Outcome);
        Assert.Empty(store.Moves);
    }

    /// <summary>
    /// With no Status named, a Status the destination holds is kept; one it lacks needs a destination Status, and nothing is
    /// counted against the bound or moved.
    /// </summary>
    /// <param name="held">Whether the destination's set holds the source Status.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "FR-26")]
    public async Task With_no_Status_named_the_source_Status_is_kept_or_a_destination_Status_is_required(bool held)
    {
        var world = new World { DestinationRemovesTheFirst = !held };
        var store = world.Store();

        var result = await world.HandleAsync(store, statusId: null);

        if (held)
        {
            Assert.Equal(MoveStatusTasksToProjectOutcome.Moved, result.Outcome);
            Assert.Equal(world.Defaults[0].Id, result.StatusId);
            Assert.Equal((world.Source, world.Defaults[0].Id, world.Destination, world.Defaults[0].Id), Assert.Single(store.Moves));

            return;
        }

        Assert.Equal(MoveStatusTasksToProjectOutcome.StatusDestinationRequired, result.Outcome);
        Assert.DoesNotContain(store.Calls, call => call.StartsWith("count", StringComparison.Ordinal));
        Assert.Empty(store.Moves);
    }

    /// <summary>
    /// A Status named is applied when the destination holds it, even when the source Status would have been kept, and
    /// refused when it does not.
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
        var named = held ? world.Defaults[2].Id : Guid.NewGuid();
        var store = world.Store();

        var result = await world.HandleAsync(store, named);

        if (held)
        {
            Assert.Equal(MoveStatusTasksToProjectOutcome.Moved, result.Outcome);
            Assert.Equal(named, result.StatusId);
            Assert.Equal((world.Source, world.Defaults[0].Id, world.Destination, named), Assert.Single(store.Moves));

            return;
        }

        Assert.Equal(MoveStatusTasksToProjectOutcome.StatusDestinationInvalid, result.Outcome);
        Assert.DoesNotContain(store.Calls, call => call.StartsWith("select", StringComparison.Ordinal));
        Assert.Empty(store.Moves);
    }

    /// <summary>
    /// An empty Status moves nothing and needs no destination Status, but a named one the destination lacks is still refused.
    /// </summary>
    /// <param name="named">Whether a Status outside the destination's set is named.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_empty_Status_moves_nothing_after_its_destination_Status_is_checked(bool named)
    {
        var world = new World { Selected = 0, DestinationRemovesTheFirst = true };
        var store = world.Store();

        var result = await world.HandleAsync(store, named ? Guid.NewGuid() : null);

        Assert.Equal(named ? MoveStatusTasksToProjectOutcome.StatusDestinationInvalid : MoveStatusTasksToProjectOutcome.Moved, result.Outcome);
        Assert.Equal(0, result.MovedCount);
        Assert.DoesNotContain(store.Calls, call => call.StartsWith("count", StringComparison.Ordinal));
        Assert.Empty(store.Moves);
    }

    /// <summary>
    /// The destination is counted against its bound with every Task the move adds: landing exactly on the limit is admitted,
    /// one over it is refused with the Tasks-per-Project bound.
    /// </summary>
    /// <param name="count">The destination's Tasks.</param>
    /// <param name="admitted">Whether the move of two is admitted.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(4_998, true)]
    [InlineData(4_999, false)]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "AD-25")]
    public async Task The_destination_is_counted_against_its_bound_with_every_Task_moved(int count, bool admitted)
    {
        Assert.Equal(5_000, Limit);

        var world = new World { Count = count, Selected = 2 };
        var store = world.Store();

        var result = await world.HandleAsync(store, statusId: null);

        if (admitted)
        {
            Assert.Equal(MoveStatusTasksToProjectOutcome.Moved, result.Outcome);
            Assert.Single(store.Moves);

            return;
        }

        Assert.Equal(MoveStatusTasksToProjectOutcome.BoundReached, result.Outcome);
        Assert.Equal(Bound.TasksPerProject, result.Bound?.Bound);
        Assert.Empty(store.Moves);
    }

    /// <summary>
    /// A write that does not cover exactly the Tasks selected fails rather than answering a count that was not written.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_write_that_differs_from_the_selection_fails()
    {
        var world = new World { Written = 2, Selected = 3 };
        var store = world.Store();

        await Assert.ThrowsAsync<InvalidOperationException>(() => world.HandleAsync(store, statusId: null));
    }

    /// <summary>
    /// The Space's defaults and the two Projects a case moves a Status between.
    /// </summary>
    private sealed class World
    {
        private readonly Guid _spaceId = Guid.NewGuid();

        public World()
        {
            Defaults = [Status(_spaceId, 0), Status(_spaceId, 1), Status(_spaceId, 2)];
        }

        public Guid Source { get; } = Guid.NewGuid();

        public Guid Destination { get; } = Guid.NewGuid();

        public IReadOnlyList<StatusDefinition> Defaults { get; }

        public int Count { get; init; }

        public int Selected { get; init; } = 3;

        public int? Written { get; init; }

        public bool DestinationRemovesTheFirst { get; init; }

        public ScriptedStore Store(bool[]? projectExists = null) => new(projectExists ?? [true], Selected, Written ?? Selected);

        public MoveStatusTasksToProjectHandler Handler(ScriptedStore store) =>
            new(store, new FixedStatuses(store.Calls, Defaults, Destination, DestinationRemovesTheFirst ? Removal() : null), new FixedCount(store.Calls, Count));

        public Task<MoveStatusTasksToProjectResult> HandleAsync(ScriptedStore store, Guid? statusId) =>
            Handler(store).HandleAsync(Source, Defaults[0].Id, Destination, statusId, TestContext.Current.CancellationToken);

        private StatusDeltaOperation Removal() => new()
        {
            Id = Guid.NewGuid(),
            SpaceId = _spaceId,
            ProjectId = Destination,
            StatusId = Defaults[0].Id,
            Kind = StatusDeltaKind.Remove,
            Sequence = 1,
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
    }

    private sealed class FixedStatuses(List<string> calls, IReadOnlyList<StatusDefinition> statuses, Guid destination, StatusDeltaOperation? destinationRemoval) : IStatusDefinitionReader
    {
        public Task<IReadOnlyList<StatusDefinition>> ListAsync(CancellationToken cancellationToken) => Task.FromResult(statuses);

        public Task<ProjectStatusDelta> DeltaOfAsync(Guid projectId, CancellationToken cancellationToken)
        {
            calls.Add($"set {projectId:D}");

            return Task.FromResult(projectId == destination && destinationRemoval is not null
                ? new ProjectStatusDelta([], [destinationRemoval])
                : ProjectStatusDelta.Empty);
        }
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
    /// Answers the Project reads in turn - the last answer repeats - and the selection and the write as scripted.
    /// </summary>
    private sealed class ScriptedStore(bool[] projectExists, int selected, int written) : ITaskStore
    {
        private int _projectReads;

        public List<(Guid Source, Guid SourceStatus, Guid Destination, Guid DestinationStatus)> Moves { get; } = [];

        public List<string> Calls { get; } = [];

        public Task<TaskItem?> CreateAsync(Guid spaceId, Guid projectId, Guid statusId, string title, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The bulk move creates no Task.");

        public Task<TaskItem?> FindAsync(Guid taskId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The bulk move reads no single Task.");

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

            return Task.FromResult(projectExists[Math.Min(_projectReads++, projectExists.Length - 1)]);
        }

        public Task<TaskItem?> MoveToProjectAsync(Guid taskId, Guid sourceProjectId, Guid destinationProjectId, Guid statusId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The bulk move is not the single move.");

        public Task<int> CountInStatusAsync(Guid projectId, Guid statusId, CancellationToken cancellationToken)
        {
            Calls.Add($"select {projectId:D} {statusId:D}");

            return Task.FromResult(selected);
        }

        public Task<int> MoveStatusToProjectAsync(Guid sourceProjectId, Guid sourceStatusId, Guid destinationProjectId, Guid destinationStatusId, CancellationToken cancellationToken)
        {
            Calls.Add($"move {sourceProjectId:D} {sourceStatusId:D} {destinationProjectId:D} {destinationStatusId:D}");
            Moves.Add((sourceProjectId, sourceStatusId, destinationProjectId, destinationStatusId));

            return Task.FromResult(written);
        }

        public Task<TaskItem?> EditAsync(Guid taskId, TaskEdit edit, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The bulk move edits nothing else.");

        public Task<bool> ReplaceLabelsAsync(Guid taskId, IReadOnlyList<Guid> labelIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The bulk move applies no Label.");

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LabelIdsOfAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The bulk move answers no Task.");

        public Task<TaskDeletion> DeleteAsync(Guid taskId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The bulk move deletes nothing.");

        public Task<TaskMove> MoveAsync(Guid taskId, Guid statusId, Guid? afterTaskId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The move between Projects is not the move within one.");
    }
}
