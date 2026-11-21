using Xunit;
using Yello.Application.Spaces;
using Yello.Application.Statuses;
using Yello.Application.Statuses.AddProjectStatus;
using Yello.Application.Statuses.PlaceProjectStatus;
using Yello.Application.Statuses.RenameProjectStatus;
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

        public Task<int> CountTasksAsync(Guid projectId, Guid statusId, CancellationToken cancellationToken) =>
            Task.FromResult(0);

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
