using Xunit;
using Yello.Application.Spaces;
using Yello.Application.Statuses;
using Yello.Application.Statuses.AddSpaceStatus;
using Yello.Application.Statuses.PlaceSpaceStatus;
using Yello.Application.Statuses.RenameSpaceStatus;
using Yello.Domain;
using Yello.Domain.Memberships;
using Yello.Domain.Statuses;

namespace Yello.Tests.Slices.Statuses;

/// <summary>
/// The three Space-level slices over the writer, with a fake store: the lock is taken before anything is read, a refusal
/// never reaches the store, a conflict report carries each Project's name and Task count, a clash names its Projects, and an
/// admitted change is recorded once and answered with the defaults as read afterwards.
/// </summary>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "FR-27")]
[Trait("Requirement", "UX-DR16")]
public sealed class SpaceDefaultStatusHandlerTests
{
    private static readonly Guid SpaceId = Guid.NewGuid();

    private readonly World _world = new();

    /// <summary>
    /// A rename two Projects renamed themselves, sent with no decision, is refused with both named, by name and Task count,
    /// in name order, and nothing is written.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_conflicting_rename_reports_each_Project_and_writes_nothing()
    {
        var intake = _world.Project("Intake project", tasksInFirst: 2);
        var backlog = _world.Project("Backlog project", tasksInFirst: 0);

        _world.Rename(intake, _world.First.Id, "Intake");
        _world.Rename(backlog, _world.First.Id, "Backlog");

        var result = await new RenameSpaceStatusHandler(_world.Writer).HandleAsync(_world.First.Id, "Waiting", replaceProjectNames: null, TestContext.Current.CancellationToken);

        Assert.Equal(SpaceStatusWriteOutcome.RenameConflict, result.Outcome);
        Assert.Null(result.Defaults);
        Assert.Equal(
            [new SpaceStatusConflictReport(backlog, "Backlog project", "Backlog", 0), new SpaceStatusConflictReport(intake, "Intake project", "Intake", 2)],
            result.Conflicts);
        Assert.Empty(_world.Written);
        Assert.True(_world.LockedBeforeReading);
    }

    /// <summary>
    /// An add of a name a Project added is refused naming that Project; nothing is written.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-25")]
    public async Task A_clashing_add_names_the_Project_and_writes_nothing()
    {
        var parking = _world.Project("Parks its work", tasksInFirst: 0);

        _world.AddOwned(parking, "Parked");

        var result = await new AddSpaceStatusHandler(_world.Writer, _world, TimeProvider.System)
            .HandleAsync(new ActiveSpaceContext(Guid.NewGuid(), SpaceId, Role.Owner), "parked", afterStatusId: null, TestContext.Current.CancellationToken);

        Assert.Equal(SpaceStatusWriteOutcome.NameTaken, result.Outcome);
        Assert.Equal([new SpaceStatusProject(parking, "Parks its work")], result.Projects);
        Assert.Empty(_world.Written);
    }

    /// <summary>
    /// An admitted place is recorded once and answered with the defaults as read after it; a rename to the default's own
    /// name writes nothing and answers the defaults.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_admitted_change_is_recorded_once_and_an_unchanged_rename_writes_nothing()
    {
        var unchanged = await new RenameSpaceStatusHandler(_world.Writer).HandleAsync(_world.First.Id, _world.First.Name, replaceProjectNames: null, TestContext.Current.CancellationToken);

        Assert.Equal(SpaceStatusWriteOutcome.Written, unchanged.Outcome);
        Assert.Empty(_world.Written);

        var placed = await new PlaceSpaceStatusHandler(_world.Writer).HandleAsync(_world.Defaults[2].Id, afterStatusId: null, TestContext.Current.CancellationToken);

        Assert.Equal(SpaceStatusWriteOutcome.Written, placed.Outcome);
        Assert.Single(_world.Written);
        Assert.Equal(_world.Defaults[2].Id, placed.Defaults?[0].Id);
    }

    /// <summary>
    /// A place of a Status that is not a default is not found, and an anchor that is not another default is a position
    /// refusal; neither writes.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_place_outside_the_defaults_is_refused_and_writes_nothing()
    {
        var handler = new PlaceSpaceStatusHandler(_world.Writer);

        Assert.Equal(SpaceStatusWriteOutcome.NotFound, (await handler.HandleAsync(Guid.NewGuid(), afterStatusId: null, TestContext.Current.CancellationToken)).Outcome);
        Assert.Equal(SpaceStatusWriteOutcome.PositionInvalid, (await handler.HandleAsync(_world.First.Id, _world.First.Id, TestContext.Current.CancellationToken)).Outcome);
        Assert.Empty(_world.Written);
    }

    /// <summary>
    /// The Space's defaults, Projects, deltas and Task counts in memory, recording what the writer asks the store to write
    /// and applying positions so the answer reads them back.
    /// </summary>
    private sealed class World : ISpaceDefaultStatusStore, IStatusDefinitionReader, IIdentifierGenerator
    {
        private readonly Dictionary<Guid, string> _projects = [];

        private readonly Dictionary<Guid, int> _tasksInFirst = [];

        private readonly Dictionary<Guid, ProjectStatusDelta> _deltas = [];

        private bool _locked;

        public World()
        {
            Defaults = [Default(0, "Queued"), Default(1, "Doing"), Default(2, "Finished")];
            Writer = new SpaceDefaultStatusWriter(this, this);
        }

        public List<StatusDefinition> Defaults { get; }

        public StatusDefinition First => Defaults[0];

        public SpaceDefaultStatusWriter Writer { get; }

        public List<SpaceDefaultStatusChange> Written { get; } = [];

        public bool LockedBeforeReading { get; private set; } = true;

        public Guid Project(string name, int tasksInFirst)
        {
            var id = Guid.NewGuid();

            _projects[id] = name;
            _tasksInFirst[id] = tasksInFirst;

            return id;
        }

        public void Rename(Guid projectId, Guid statusId, string name) =>
            _deltas[projectId] = new ProjectStatusDelta([], [Operation(projectId, statusId, StatusDeltaKind.Rename, name)]);

        public void AddOwned(Guid projectId, string name)
        {
            var owned = new StatusDefinition { Id = Guid.NewGuid(), SpaceId = SpaceId, OwnerProjectId = projectId, Name = name, Position = 0, CreatedAt = DateTimeOffset.UnixEpoch };

            _deltas[projectId] = new ProjectStatusDelta([owned], [Operation(projectId, owned.Id, StatusDeltaKind.Add, name: null)]);
        }

        public Guid Next() => Guid.NewGuid();

        public Task LockAsync(CancellationToken cancellationToken)
        {
            _locked = true;

            return Task.CompletedTask;
        }

        public Task<IReadOnlyDictionary<Guid, ProjectStatusDelta>> DeltasAsync(CancellationToken cancellationToken)
        {
            LockedBeforeReading &= _locked;

            return Task.FromResult<IReadOnlyDictionary<Guid, ProjectStatusDelta>>(_deltas);
        }

        public Task<IReadOnlyDictionary<Guid, int>> CountTasksByProjectAsync(Guid statusId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, int>>(statusId == First.Id ? _tasksInFirst.Where(pair => pair.Value > 0).ToDictionary() : []);

        public Task<IReadOnlyDictionary<Guid, string>> ProjectNamesAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(_projects.Where(pair => projectIds.Contains(pair.Key)).ToDictionary());

        public Task<bool> WriteAsync(SpaceDefaultStatusChange change, CancellationToken cancellationToken)
        {
            Written.Add(change);

            foreach (var status in Defaults)
            {
                status.Position = change.Positions.GetValueOrDefault(status.Id, status.Position);
            }

            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<StatusDefinition>> ListAsync(CancellationToken cancellationToken)
        {
            LockedBeforeReading &= _locked;

            return Task.FromResult<IReadOnlyList<StatusDefinition>>([.. Defaults.OrderBy(status => status.Position)]);
        }

        public Task<ProjectStatusDelta> DeltaOfAsync(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(_deltas.GetValueOrDefault(projectId, ProjectStatusDelta.Empty));

        private static StatusDefinition Default(int position, string name) =>
            new() { Id = Guid.NewGuid(), SpaceId = SpaceId, Name = name, Position = position, CreatedAt = DateTimeOffset.UnixEpoch };

        private static StatusDeltaOperation Operation(Guid projectId, Guid statusId, StatusDeltaKind kind, string? name) => new()
        {
            Id = Guid.NewGuid(),
            SpaceId = SpaceId,
            ProjectId = projectId,
            StatusId = statusId,
            Kind = kind,
            Name = name,
            Sequence = 1,
            CreatedAt = DateTimeOffset.UnixEpoch,
        };
    }
}
