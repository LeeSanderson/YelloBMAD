using Xunit;
using Yello.Application.Statuses;
using Yello.Application.Statuses.RemoveSpaceStatus;
using Yello.Domain;
using Yello.Domain.Statuses;

namespace Yello.Tests.Slices.Statuses;

/// <summary>
/// The Space-level removal slice with fake stores: the locks are taken in their one order before the plan's reads, only the
/// Projects whose set holds the Status are Tasks-locked, the Tasks are counted after the last lock, a refusal never reaches
/// the store and names Projects by name, and an admitted removal is recorded once and answered with the defaults read after.
/// </summary>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-27")]
[Trait("Requirement", "AD-17")]
public sealed class RemoveSpaceStatusHandlerTests
{
    private static readonly Guid SpaceId = Guid.NewGuid();

    private readonly World _world = new();

    /// <summary>
    /// The Status-defaults lock, then the Project list, then the holders' Tasks locks, and only then the count; the Project
    /// that removed the Status is not locked.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_locks_come_in_one_order_and_the_count_after_the_last()
    {
        var plain = _world.Project("Plain", tasks: 1);
        var removedIt = _world.Project("Removed it", tasks: 0);

        _world.Removes(removedIt, _world.Defaults[0].Id);

        Guid[] kept = [_world.Defaults[1].Id, _world.Defaults[2].Id];
        var result = await HandleAsync(_world.Defaults[0].Id, _world.Defaults[1].Id);

        Assert.Equal(SpaceStatusWriteOutcome.Written, result.Outcome);
        Assert.Equal(["defaults", "projects", "tasks", "count", "remove"], _world.Calls);
        Assert.Equal([plain], _world.TasksLocked);
        Assert.Single(_world.Removed);
        Assert.Equal(kept, result.Defaults?.Select(status => status.Id));
    }

    /// <summary>
    /// Occupied with no destination: refused with the report, each Project by name and its set after by its own names, and
    /// nothing recorded.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_occupied_removal_with_no_destination_reports_by_name_and_writes_nothing()
    {
        var plain = _world.Project("Plain", tasks: 2);

        var result = await HandleAsync(_world.Defaults[0].Id, destination: null);

        Assert.Equal(SpaceStatusWriteOutcome.DestinationRequired, result.Outcome);

        var entry = Assert.Single(result.Removal);

        Assert.Equal((plain, "Plain", 2), (entry.ProjectId, entry.ProjectName, entry.TaskCount));
        Assert.Equal([_world.Defaults[1].Name, _world.Defaults[2].Name], entry.Destinations.Select(choice => choice.Name));
        Assert.Empty(_world.Removed);
    }

    /// <summary>
    /// A Project left empty is named by name; a Status that is not a default is not found; neither writes.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_Project_left_empty_is_named_and_an_unknown_Status_is_not_found()
    {
        var onlyIt = _world.Project("Only it", tasks: 0);

        _world.Removes(onlyIt, _world.Defaults[1].Id, _world.Defaults[2].Id);

        var empty = await HandleAsync(_world.Defaults[0].Id, _world.Defaults[1].Id);

        Assert.Equal(SpaceStatusWriteOutcome.SetEmpty, empty.Outcome);
        Assert.Equal([new SpaceStatusProject(onlyIt, "Only it")], empty.Projects);
        Assert.Equal(SpaceStatusWriteOutcome.NotFound, (await HandleAsync(Guid.NewGuid(), destination: null)).Outcome);
        Assert.Empty(_world.Removed);
    }

    private Task<SpaceStatusWriteResult> HandleAsync(Guid statusId, Guid? destination) =>
        new RemoveSpaceStatusHandler(_world, _world, _world, _world, TimeProvider.System)
            .HandleAsync(new SpaceStatusRemovalRequest(statusId, destination, []), TestContext.Current.CancellationToken);

    private sealed class World : ISpaceDefaultStatusStore, ISpaceDefaultStatusRemovalStore, IStatusDefinitionReader, IIdentifierGenerator
    {
        private readonly Dictionary<Guid, string> _projects = [];

        private readonly Dictionary<Guid, int> _tasks = [];

        private readonly Dictionary<Guid, ProjectStatusDelta> _deltas = [];

        private int _sequence;

        public List<StatusDefinition> Defaults { get; } =
        [
            Default(0, "Queued"),
            Default(1, "Doing"),
            Default(2, "Finished"),
        ];

        public List<string> Calls { get; } = [];

        public List<Guid> TasksLocked { get; } = [];

        public List<SpaceDefaultStatusRemovalPlan> Removed { get; } = [];

        public Guid Project(string name, int tasks)
        {
            var id = Guid.NewGuid();

            _projects[id] = name;
            _tasks[id] = tasks;

            return id;
        }

        public void Removes(Guid projectId, params Guid[] statusIds) =>
            _deltas[projectId] = new ProjectStatusDelta([], [.. statusIds.Select(statusId => Operation(projectId, statusId))]);

        public Guid Next() => Guid.NewGuid();

        public Task LockAsync(CancellationToken cancellationToken)
        {
            Calls.Add("defaults");

            return Task.CompletedTask;
        }

        public Task<IReadOnlyDictionary<Guid, string>> LockProjectsAsync(CancellationToken cancellationToken)
        {
            Calls.Add("projects");

            return Task.FromResult<IReadOnlyDictionary<Guid, string>>(_projects);
        }

        public Task LockProjectTasksAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken)
        {
            Calls.Add("tasks");
            TasksLocked.AddRange(projectIds);

            return Task.CompletedTask;
        }

        public Task<IReadOnlyDictionary<Guid, int>> CountTasksByProjectAsync(Guid statusId, CancellationToken cancellationToken)
        {
            Calls.Add("count");

            return Task.FromResult<IReadOnlyDictionary<Guid, int>>(statusId == Defaults[0].Id ? _tasks.Where(pair => pair.Value > 0).ToDictionary() : []);
        }

        public Task RemoveAsync(SpaceDefaultStatusRemovalPlan plan, CancellationToken cancellationToken)
        {
            Calls.Add("remove");
            Removed.Add(plan);
            Defaults.RemoveAll(status => status.Id == plan.StatusId);

            return Task.CompletedTask;
        }

        public Task<IReadOnlyDictionary<Guid, ProjectStatusDelta>> DeltasAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, ProjectStatusDelta>>(_deltas);

        public Task<IReadOnlyDictionary<Guid, string>> ProjectNamesAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(_projects.Where(pair => projectIds.Contains(pair.Key)).ToDictionary());

        public Task<bool> WriteAsync(SpaceDefaultStatusChange change, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<IReadOnlyList<StatusDefinition>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StatusDefinition>>([.. Defaults]);

        public Task<ProjectStatusDelta> DeltaOfAsync(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(_deltas.GetValueOrDefault(projectId, ProjectStatusDelta.Empty));

        private static StatusDefinition Default(int position, string name) =>
            new() { Id = Guid.NewGuid(), SpaceId = SpaceId, Name = name, Position = position, CreatedAt = DateTimeOffset.UnixEpoch };

        private StatusDeltaOperation Operation(Guid projectId, Guid statusId) => new()
        {
            Id = Guid.NewGuid(),
            SpaceId = SpaceId,
            ProjectId = projectId,
            StatusId = statusId,
            Kind = StatusDeltaKind.Remove,
            Sequence = ++_sequence,
            CreatedAt = DateTimeOffset.UnixEpoch,
        };
    }
}
