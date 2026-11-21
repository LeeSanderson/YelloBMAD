using Xunit;
using Yello.Application.Spaces;
using Yello.Application.Statuses;
using Yello.Application.Statuses.AddProjectStatus;
using Yello.Application.Statuses.PlaceProjectStatus;
using Yello.Application.Statuses.RemoveProjectStatus;
using Yello.Application.Statuses.RenameProjectStatus;
using Yello.Application.Statuses.RestoreProjectStatus;
using Yello.Domain;
using Yello.Domain.Boards;
using Yello.Domain.Memberships;
using Yello.Domain.Projects;
using Yello.Domain.Statuses;
using Yello.Domain.Tasks;

namespace Yello.Tests.Slices.Statuses;

/// <summary>
/// The three delta writes take the Project's lock before they read the set they check, write nothing when they refuse,
/// stamp the context's Space and the route's Project on what they write, and answer with the set read after the write.
/// </summary>
/// <remarks>
/// No database: the wire cases in <c>ProjectEndpointTests.Statuses</c> and <c>.StatusRace</c> hold the lock, the indexes
/// and the rollback.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-25")]
[Trait("Requirement", "AD-16")]
[Trait("Requirement", "AR-3")]
public sealed class ProjectStatusWriteHandlerTests
{
    private readonly World _world = new();

    /// <summary>
    /// An add takes the lock first, then reads, then writes one owned definition and one add operation stamped with the
    /// context's Space and the route's Project, and answers the set with the new Status last.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_add_locks_then_reads_then_writes_an_owned_definition_and_its_operation()
    {
        var result = await new AddProjectStatusHandler(_world.Writer).HandleAsync(_world.Context, _world.ProjectId, "Parked", afterStatusId: null, TestContext.Current.CancellationToken);

        Assert.Equal(ProjectStatusWriteOutcome.Written, result.Outcome);
        Assert.Equal(["lock", "defaults", "delta", "write", "defaults", "delta"], _world.Calls.Take(6));

        var (added, operations) = Assert.Single(_world.Writes);

        Assert.NotNull(added);
        Assert.Equal((_world.Context.SpaceId, _world.ProjectId, "Parked"), (added.SpaceId, added.OwnerProjectId, added.Name));

        var operation = Assert.Single(operations);

        Assert.Equal((StatusDeltaKind.Add, added.Id, _world.ProjectId, _world.Context.SpaceId, 1), (operation.Kind, operation.StatusId, operation.ProjectId, operation.SpaceId, operation.Sequence));
        Assert.Equal(added.Id, result.Set!.Statuses[^1].Id);
    }

    /// <summary>
    /// An add at a position records the add and then the place, in sequence, in one write.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_add_at_a_position_writes_the_add_then_the_place()
    {
        var first = _world.Defaults[0].Id;

        var result = await new AddProjectStatusHandler(_world.Writer).HandleAsync(_world.Context, _world.ProjectId, "Triage", first, TestContext.Current.CancellationToken);

        var (_, operations) = Assert.Single(_world.Writes);

        Assert.Equal([StatusDeltaKind.Add, StatusDeltaKind.Place], operations.Select(operation => operation.Kind));
        Assert.Equal([null, first], operations.Select(operation => operation.AfterStatusId));
        Assert.Equal([1, 2], operations.Select(operation => operation.Sequence));
        Assert.Equal("Triage", result.Set!.Statuses[1].Name);
    }

    /// <summary>
    /// A Project the lock does not find is not found, and nothing is read or written.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_Project_the_lock_does_not_find_is_not_found_and_nothing_is_read_or_written()
    {
        _world.ProjectExists = false;

        var result = await new RenameProjectStatusHandler(_world.Writer).HandleAsync(_world.Context, _world.ProjectId, _world.Defaults[0].Id, "Intake", TestContext.Current.CancellationToken);

        Assert.Equal(ProjectStatusWriteOutcome.NotFound, result.Outcome);
        Assert.Equal(["lock"], _world.Calls);
    }

    /// <summary>
    /// A rename to a name another Status holds, in any case, and a Status outside the set, are refused and nothing is
    /// written; the name the Status already has, in another case, is admitted.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_rename_is_refused_for_a_taken_name_or_a_Status_outside_the_set_and_writes_nothing()
    {
        var handler = new RenameProjectStatusHandler(_world.Writer);
        var cancellationToken = TestContext.Current.CancellationToken;

        var taken = await handler.HandleAsync(_world.Context, _world.ProjectId, _world.Defaults[2].Id, _world.Defaults[0].Name.ToUpperInvariant(), cancellationToken);
        var outside = await handler.HandleAsync(_world.Context, _world.ProjectId, Guid.NewGuid(), "Anything", cancellationToken);

        Assert.Equal((ProjectStatusWriteOutcome.NameTaken, ProjectStatusWriteOutcome.NotFound), (taken.Outcome, outside.Outcome));
        Assert.Empty(_world.Writes);

        var own = await handler.HandleAsync(_world.Context, _world.ProjectId, _world.Defaults[0].Id, _world.Defaults[0].Name.ToUpperInvariant(), cancellationToken);

        Assert.Equal(ProjectStatusWriteOutcome.Written, own.Outcome);
    }

    /// <summary>
    /// A place after itself or after a Status outside the set is an invalid position; nothing is written.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_place_after_itself_or_outside_the_set_is_an_invalid_position()
    {
        var handler = new PlaceProjectStatusHandler(_world.Writer);
        var status = _world.Defaults[1].Id;
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal(ProjectStatusWriteOutcome.PositionInvalid, (await handler.HandleAsync(_world.Context, _world.ProjectId, status, status, cancellationToken)).Outcome);
        Assert.Equal(ProjectStatusWriteOutcome.PositionInvalid, (await handler.HandleAsync(_world.Context, _world.ProjectId, status, Guid.NewGuid(), cancellationToken)).Outcome);
        Assert.Empty(_world.Writes);
    }

    /// <summary>
    /// Moving a Status that was already placed lands every Status exactly where the editor's one-step moves put it, and
    /// leaves the others where they were: a replaced place must not replay the other places against an order that no
    /// longer exists. Up, up, down over A B C D first (which used to answer A C B D for C B A D), then a fixed run of moves
    /// up and down, each checked against the order the move asked for.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Moving_an_already_placed_Status_lands_every_Status_where_the_moves_asked()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var added = await new AddProjectStatusHandler(_world.Writer).HandleAsync(_world.Context, _world.ProjectId, "Parked", afterStatusId: null, cancellationToken);
        var expected = added.Set!.Statuses.Select(status => status.Id).ToList();
        var place = new PlaceProjectStatusHandler(_world.Writer);
        (int From, int To)[] opening = [(1, 0), (2, 1), (0, 1)];
        var moves = opening.Concat(Enumerable.Range(0, 40).Select(step => OneStep((step * 7 + 3) % 4, up: step % 3 == 0)));

        foreach (var (from, to) in moves)
        {
            var moved = expected[from];

            expected.RemoveAt(from);
            expected.Insert(to, moved);

            var result = await place.HandleAsync(_world.Context, _world.ProjectId, moved, to == 0 ? null : expected[to - 1], cancellationToken);

            Assert.Equal(ProjectStatusWriteOutcome.Written, result.Outcome);
            Assert.Equal(expected, result.Set!.Statuses.Select(status => status.Id));
        }
    }

    /// <summary>
    /// A name index that refuses what the check admitted answers as a taken name, never a failure.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_index_refusal_answers_as_a_taken_name()
    {
        _world.IndexRefuses = true;

        var result = await new AddProjectStatusHandler(_world.Writer).HandleAsync(_world.Context, _world.ProjectId, "Parked", afterStatusId: null, TestContext.Current.CancellationToken);

        Assert.Equal(ProjectStatusWriteOutcome.NameTaken, result.Outcome);
        Assert.Null(result.Set);
    }

    /// <summary>
    /// A removal decides under the lock, in order: a Status outside the set is not found; a destination that is the removed
    /// Status or outside the set is invalid whatever the occupancy; an occupied Status with no destination needs one, counted
    /// after the lock. Nothing is written by any of them.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-26")]
    public async Task A_removal_refuses_in_order_under_the_lock_and_writes_nothing()
    {
        var handler = new RemoveProjectStatusHandler(_world.Writer);
        var first = _world.Defaults[0].Id;
        var cancellationToken = TestContext.Current.CancellationToken;

        _world.Occupants[first] = 2;

        var outside = await handler.HandleAsync(_world.Context, _world.ProjectId, Guid.NewGuid(), destinationStatusId: null, cancellationToken);
        var itself = await handler.HandleAsync(_world.Context, _world.ProjectId, first, first, cancellationToken);
        var random = await handler.HandleAsync(_world.Context, _world.ProjectId, first, Guid.NewGuid(), cancellationToken);

        _world.Calls.Clear();

        var unmapped = await handler.HandleAsync(_world.Context, _world.ProjectId, first, destinationStatusId: null, cancellationToken);

        Assert.Equal(
            [ProjectStatusWriteOutcome.NotFound, ProjectStatusWriteOutcome.DestinationInvalid, ProjectStatusWriteOutcome.DestinationInvalid, ProjectStatusWriteOutcome.DestinationRequired],
            new[] { outside, itself, random, unmapped }.Select(result => result.Outcome));
        Assert.Equal(["lock", "defaults", "delta", "count"], _world.Calls);
        Assert.Empty(_world.Writes);
        Assert.Empty(_world.Migrations);
    }

    /// <summary>
    /// An occupied default removed with a destination migrates first, then deletes the default's own operations, then
    /// records its <c>Remove</c>; the answer lacks it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-26")]
    [Trait("Requirement", "AD-17")]
    public async Task An_occupied_default_removed_with_a_destination_migrates_then_records_its_removal()
    {
        var (first, second) = (_world.Defaults[0].Id, _world.Defaults[1].Id);

        _world.Occupants[first] = 3;

        var result = await new RemoveProjectStatusHandler(_world.Writer).HandleAsync(_world.Context, _world.ProjectId, first, second, TestContext.Current.CancellationToken);

        Assert.Equal(ProjectStatusWriteOutcome.Written, result.Outcome);
        Assert.Equal(["lock", "defaults", "delta", "migrate", "delete all", "write"], _world.Calls.Take(6));
        Assert.Equal((first, second), Assert.Single(_world.Migrations));
        Assert.Equal((first, StatusDeltaKind.Remove), _world.Operations.Select(operation => (operation.StatusId, operation.Kind)).Single());
        Assert.Equal([second, _world.Defaults[2].Id], result.Set!.Statuses.Select(status => status.Id));
    }

    /// <summary>
    /// A Status the Project added is removed by deleting its operations, writing no <c>Remove</c>, and then its definition,
    /// once nothing refers to it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-25")]
    public async Task An_added_Status_is_removed_by_deleting_its_definition_last()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var added = (await new AddProjectStatusHandler(_world.Writer).HandleAsync(_world.Context, _world.ProjectId, "Parked", _world.Defaults[0].Id, cancellationToken)).Set!.Statuses[1].Id;

        _world.Calls.Clear();

        var result = await new RemoveProjectStatusHandler(_world.Writer).HandleAsync(_world.Context, _world.ProjectId, added, destinationStatusId: null, cancellationToken);

        Assert.Equal(ProjectStatusWriteOutcome.Written, result.Outcome);
        Assert.Equal(["lock", "defaults", "delta", "count", "delete all", "write", "delete definition"], _world.Calls.Take(7));
        Assert.Empty(_world.Operations);
        Assert.Equal(_world.Defaults.Select(status => status.Id), result.Set!.Statuses.Select(status => status.Id));
    }

    /// <summary>
    /// Removing a Status others are placed after leaves the rest in their order, over a fixed run of places and removals.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-16")]
    public async Task Removing_a_Status_others_are_placed_after_keeps_the_rest_in_their_order()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var add = new AddProjectStatusHandler(_world.Writer);
        var place = new PlaceProjectStatusHandler(_world.Writer);
        var remove = new RemoveProjectStatusHandler(_world.Writer);

        _ = await add.HandleAsync(_world.Context, _world.ProjectId, "Parked", afterStatusId: null, cancellationToken);
        var set = (await add.HandleAsync(_world.Context, _world.ProjectId, "Waiting", afterStatusId: null, cancellationToken)).Set!.Statuses.Select(status => status.Id).ToList();

        // Third after first, Parked after third, Waiting first: Waiting, first, third, Parked, second.
        _ = await place.HandleAsync(_world.Context, _world.ProjectId, set[2], set[0], cancellationToken);
        _ = await place.HandleAsync(_world.Context, _world.ProjectId, set[3], set[2], cancellationToken);
        var expected = (await place.HandleAsync(_world.Context, _world.ProjectId, set[4], afterStatusId: null, cancellationToken)).Set!.Statuses.Select(status => status.Id).ToList();

        foreach (var removed in new[] { set[2], set[4], set[0] })
        {
            expected.Remove(removed);

            var result = await remove.HandleAsync(_world.Context, _world.ProjectId, removed, destinationStatusId: null, cancellationToken);

            Assert.Equal(ProjectStatusWriteOutcome.Written, result.Outcome);
            Assert.Equal(expected, result.Set!.Statuses.Select(status => status.Id));
            Assert.DoesNotContain(_world.Operations, operation => operation.AfterStatusId == removed);
            Assert.Equal(_world.Operations.Count, _world.Operations.Select(operation => operation.Sequence).Distinct().Count());
        }
    }

    /// <summary>
    /// The last Status is never removed: with two defaults removed, the third is refused as an empty set, even with a
    /// destination named.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-25")]
    public async Task The_last_Status_is_refused_as_an_empty_set()
    {
        var handler = new RemoveProjectStatusHandler(_world.Writer);
        var cancellationToken = TestContext.Current.CancellationToken;

        _ = await handler.HandleAsync(_world.Context, _world.ProjectId, _world.Defaults[1].Id, destinationStatusId: null, cancellationToken);
        _ = await handler.HandleAsync(_world.Context, _world.ProjectId, _world.Defaults[2].Id, destinationStatusId: null, cancellationToken);

        var writes = _world.Writes.Count;

        Assert.Equal(ProjectStatusWriteOutcome.SetEmpty, (await handler.HandleAsync(_world.Context, _world.ProjectId, _world.Defaults[0].Id, destinationStatusId: null, cancellationToken)).Outcome);
        Assert.Equal(ProjectStatusWriteOutcome.SetEmpty, (await handler.HandleAsync(_world.Context, _world.ProjectId, _world.Defaults[0].Id, Guid.NewGuid(), cancellationToken)).Outcome);
        Assert.Equal(writes, _world.Writes.Count);
    }

    /// <summary>
    /// A removed default comes back by deleting its <c>Remove</c>, at its Space slot under its Space name; a default not
    /// removed is not found; a removed default whose name the Project has since given to an added Status is a taken name.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-25")]
    public async Task A_removed_default_is_restored_unless_its_name_is_taken_and_only_a_removed_default_is()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var remove = new RemoveProjectStatusHandler(_world.Writer);
        var restore = new RestoreProjectStatusHandler(_world.Writer);
        var (first, second, third) = (_world.Defaults[0], _world.Defaults[1], _world.Defaults[2]);

        _ = await new RenameProjectStatusHandler(_world.Writer).HandleAsync(_world.Context, _world.ProjectId, second.Id, "Underway", cancellationToken);
        _ = await remove.HandleAsync(_world.Context, _world.ProjectId, second.Id, destinationStatusId: null, cancellationToken);

        Assert.Equal(ProjectStatusWriteOutcome.NotFound, (await restore.HandleAsync(_world.Context, _world.ProjectId, first.Id, cancellationToken)).Outcome);

        var restored = await restore.HandleAsync(_world.Context, _world.ProjectId, second.Id, cancellationToken);

        Assert.Equal(ProjectStatusWriteOutcome.Written, restored.Outcome);
        Assert.Equal(new EffectiveStatus(second.Id, second.Name, StatusSource.SpaceDefault, null), restored.Set!.Statuses[1]);
        Assert.DoesNotContain(_world.Operations, operation => operation.StatusId == second.Id);

        _ = await remove.HandleAsync(_world.Context, _world.ProjectId, third.Id, destinationStatusId: null, cancellationToken);
        _ = await new AddProjectStatusHandler(_world.Writer).HandleAsync(_world.Context, _world.ProjectId, third.Name, afterStatusId: null, cancellationToken);

        Assert.Equal(ProjectStatusWriteOutcome.NameTaken, (await restore.HandleAsync(_world.Context, _world.ProjectId, third.Id, cancellationToken)).Outcome);
        Assert.Contains(_world.Operations, operation => operation.StatusId == third.Id && operation.Kind == StatusDeltaKind.Remove);
    }

    // A one-place move over four rows, as the editor's buttons make it; an end row can only go inwards.
    private static (int From, int To) OneStep(int from, bool up)
    {
        if (from == 0)
        {
            return (from, 1);
        }

        if (from == 3)
        {
            return (from, 2);
        }

        return (from, up ? from - 1 : from + 1);
    }

    private sealed class World : IProjectStatusStore, IStatusDefinitionReader, IBoardReader, IIdentifierGenerator
    {
        private readonly List<StatusDefinition> _owned = [];

        private readonly List<StatusDeltaOperation> _operations = [];

        public World()
        {
            Context = new ActiveSpaceContext(Guid.NewGuid(), Guid.NewGuid(), Role.Owner);
            Defaults = [Default(0, "Queued"), Default(1, "Doing"), Default(2, "Finished")];
            Writer = new ProjectStatusDeltaWriter(this, this, this, this, TimeProvider.System);
        }

        public ActiveSpaceContext Context { get; }

        public Guid ProjectId { get; } = Guid.NewGuid();

        public List<StatusDefinition> Defaults { get; }

        public ProjectStatusDeltaWriter Writer { get; }

        public List<string> Calls { get; } = [];

        public List<(StatusDefinition? Added, IReadOnlyList<StatusDeltaOperation> Operations)> Writes { get; } = [];

        public bool ProjectExists { get; set; } = true;

        public bool IndexRefuses { get; set; }

        public Dictionary<Guid, int> Occupants { get; } = [];

        public List<(Guid From, Guid To)> Migrations { get; } = [];

        public IReadOnlyList<StatusDeltaOperation> Operations => _operations;

        public Guid Next() => Guid.NewGuid();

        public Task<bool> LockAsync(Guid projectId, CancellationToken cancellationToken)
        {
            Calls.Add("lock");

            return Task.FromResult(ProjectExists && projectId == ProjectId);
        }

        public Task<bool> WriteAsync(StatusDefinition? added, IReadOnlyList<StatusDeltaOperation> operations, CancellationToken cancellationToken)
        {
            Calls.Add("write");

            if (IndexRefuses)
            {
                return Task.FromResult(false);
            }

            Writes.Add((added, operations));

            if (added is not null)
            {
                _owned.Add(added);
            }

            _operations.RemoveAll(existing => operations.Any(written => written.StatusId == existing.StatusId && written.Kind == existing.Kind));
            _operations.AddRange(operations);

            return Task.FromResult(true);
        }

        public Task MigrateTasksAsync(Guid projectId, Guid fromStatusId, Guid toStatusId, CancellationToken cancellationToken)
        {
            Calls.Add("migrate");
            Migrations.Add((fromStatusId, toStatusId));

            foreach (var key in Occupants.Keys.Where(key => key == fromStatusId).ToList())
            {
                Occupants[toStatusId] = Occupants.GetValueOrDefault(toStatusId) + Occupants[key];
                Occupants.Remove(key);
            }

            return Task.CompletedTask;
        }

        public Task DeleteOperationsAsync(Guid projectId, Guid statusId, StatusDeltaKind? kind, CancellationToken cancellationToken)
        {
            Calls.Add(kind is { } only ? $"delete {only}" : "delete all");
            _operations.RemoveAll(operation => operation.StatusId == statusId && (kind is null || operation.Kind == kind));

            return Task.CompletedTask;
        }

        public Task DeleteOwnedDefinitionAsync(Guid projectId, Guid statusId, CancellationToken cancellationToken)
        {
            Calls.Add("delete definition");

            if (_operations.Exists(operation => operation.StatusId == statusId || operation.AfterStatusId == statusId) || Occupants.ContainsKey(statusId))
            {
                throw new InvalidOperationException("The definition is still referenced: a NO ACTION key would refuse its delete.");
            }

            _owned.RemoveAll(definition => definition.Id == statusId);

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StatusDefinition>> ListAsync(CancellationToken cancellationToken)
        {
            Calls.Add("defaults");

            return Task.FromResult<IReadOnlyList<StatusDefinition>>([.. Defaults]);
        }

        public Task<ProjectStatusDelta> DeltaOfAsync(Guid projectId, CancellationToken cancellationToken)
        {
            Calls.Add("delta");

            return Task.FromResult(new ProjectStatusDelta([.. _owned], [.. _operations.OrderBy(operation => operation.Sequence)]));
        }

        public Task<Project?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<Project?>(null);

        public Task<IReadOnlyDictionary<Guid, int>> CountTasksByStatusAsync(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());

        public Task<int> CountTasksAsync(Guid projectId, Guid statusId, CancellationToken cancellationToken)
        {
            Calls.Add("count");

            return Task.FromResult(Occupants.GetValueOrDefault(statusId));
        }

        public Task<IReadOnlyList<TaskItem>> PageAsync(Guid projectId, Guid statusId, string? after, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TaskItem>>([]);

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LabelIdsOfAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(new Dictionary<Guid, IReadOnlyList<Guid>>());

        private StatusDefinition Default(int position, string name) => new()
        {
            Id = Guid.NewGuid(),
            SpaceId = Context.SpaceId,
            Name = name,
            Position = position,
            CreatedAt = DateTimeOffset.UnixEpoch,
        };
    }
}
