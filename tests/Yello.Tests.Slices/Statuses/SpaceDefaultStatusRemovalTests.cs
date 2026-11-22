using Xunit;
using Yello.Domain;
using Yello.Domain.Statuses;

namespace Yello.Tests.Slices.Statuses;

/// <summary>
/// The Space-level removal plan with no database: every Project judged by its own set after the removal, a blocked Project
/// asked for a destination of its own and never placed, a Project left empty refused, and every after-set the set before
/// with the one Status taken out.
/// </summary>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-27")]
[Trait("Requirement", "FR-24")]
[Trait("Requirement", "AD-16")]
public sealed class SpaceDefaultStatusRemovalTests : IIdentifierGenerator
{
    private static readonly Guid SpaceId = Guid.NewGuid();

    private readonly StatusDefinition _first = Default(0, "Queued");

    private readonly StatusDefinition _second = Default(1, "Doing");

    private readonly StatusDefinition _third = Default(2, "Finished");

    private readonly Dictionary<Guid, ProjectStatusDelta> _deltas = [];

    private readonly Dictionary<Guid, int> _occupancy = [];

    private readonly List<Guid> _projects = [];

    private int _sequence;

    private StatusDefinition[] Defaults => [_first, _second, _third];

    /// <inheritdoc />
    public Guid Next() => Guid.NewGuid();

    /// <summary>
    /// Plain Projects take the Space-wide destination; nothing is reported and each occupied one is migrated there.
    /// </summary>
    [Fact]
    public void Plain_Projects_take_the_Space_wide_destination()
    {
        var a = Project(tasks: 2);
        var b = Project(tasks: 1);
        _ = Project(tasks: 0);

        var plan = Plan(_first.Id, _second.Id);

        Assert.Equal(SpaceDefaultStatusVerdict.Admitted, plan.Verdict);
        Assert.Equal([new SpaceStatusMigration(a, _second.Id), new SpaceStatusMigration(b, _second.Id)], plan.Migrations);
        Assert.Empty(plan.Places);
    }

    /// <summary>
    /// A Project that removed the destination is blocked and refused by name, with its count and its set after; given an
    /// exception it passes and its Tasks go there.
    /// </summary>
    [Fact]
    public void A_Project_that_removed_the_destination_is_blocked_until_it_has_an_exception()
    {
        var plain = Project(tasks: 1);
        var blocked = Project(tasks: 3, Operation(_second.Id, StatusDeltaKind.Remove));

        var refused = Plan(_first.Id, _second.Id);

        Assert.Equal(SpaceDefaultStatusVerdict.ProjectDestinationRequired, refused.Verdict);
        Assert.Equal(blocked, Assert.Single(refused.Report).ProjectId);
        Assert.Equal(3, refused.Report[0].TaskCount);
        Assert.Equal([_third.Id], refused.Report[0].After.Select(status => status.Id));
        Assert.Empty(refused.Migrations);

        var admitted = Plan(_first.Id, _second.Id, new SpaceStatusDestination(blocked, _third.Id));

        Assert.Equal(SpaceDefaultStatusVerdict.Admitted, admitted.Verdict);
        Assert.Equal([new SpaceStatusMigration(plain, _second.Id), new SpaceStatusMigration(blocked, _third.Id)], admitted.Migrations);
    }

    /// <summary>
    /// An exception may be a Status the blocked Project added.
    /// </summary>
    [Fact]
    public void An_exception_that_is_a_Status_the_Project_added_passes()
    {
        var parked = Owned("Parked");
        var blocked = Project(tasks: 1, [parked], Operation(_second.Id, StatusDeltaKind.Remove), Operation(parked.Id, StatusDeltaKind.Add));

        var plan = Plan(_first.Id, _second.Id, new SpaceStatusDestination(blocked, parked.Id));

        Assert.Equal(SpaceDefaultStatusVerdict.Admitted, plan.Verdict);
        Assert.Equal([new SpaceStatusMigration(blocked, parked.Id)], plan.Migrations);
    }

    /// <summary>
    /// An exception outside the blocked Project's set after the removal - the destination it removed, the Status itself, an
    /// unknown id - is invalid, naming that Project; a Project named twice is invalid naming none.
    /// </summary>
    [Fact]
    public void An_exception_outside_the_set_is_invalid_naming_the_Project()
    {
        var blocked = Project(tasks: 1, Operation(_second.Id, StatusDeltaKind.Remove));

        foreach (var destination in new[] { _second.Id, _first.Id, Guid.NewGuid() })
        {
            var plan = Plan(_first.Id, _second.Id, new SpaceStatusDestination(blocked, destination));

            Assert.Equal(SpaceDefaultStatusVerdict.DestinationInvalid, plan.Verdict);
            Assert.Equal([blocked], plan.Projects);
        }

        var twice = Plan(_first.Id, _second.Id, new SpaceStatusDestination(blocked, _third.Id), new SpaceStatusDestination(blocked, _third.Id));

        Assert.Equal(SpaceDefaultStatusVerdict.DestinationInvalid, twice.Verdict);
        Assert.Empty(twice.Projects);
    }

    /// <summary>
    /// Exceptions for a Project that can take the Space-wide destination, or for no Project of the Space, are ignored.
    /// </summary>
    [Fact]
    public void Exceptions_for_unblocked_or_unknown_Projects_are_ignored()
    {
        var plain = Project(tasks: 1);

        var plan = Plan(_first.Id, _second.Id, new SpaceStatusDestination(plain, _third.Id), new SpaceStatusDestination(Guid.NewGuid(), _third.Id));

        Assert.Equal(SpaceDefaultStatusVerdict.Admitted, plan.Verdict);
        Assert.Equal([new SpaceStatusMigration(plain, _second.Id)], plan.Migrations);
    }

    /// <summary>
    /// A Project that removed the Status is unaffected: not reported, not migrated, not re-anchored.
    /// </summary>
    [Fact]
    public void A_Project_that_removed_the_Status_is_unaffected()
    {
        _ = Project(tasks: 0, Operation(_first.Id, StatusDeltaKind.Remove), Operation(_third.Id, StatusDeltaKind.Place, afterStatusId: null));
        var plain = Project(tasks: 1);

        var refused = Plan(_first.Id, destination: null);

        Assert.Equal(SpaceDefaultStatusVerdict.DestinationRequired, refused.Verdict);
        Assert.Equal([plain], refused.Report.Select(entry => entry.ProjectId));
        Assert.Empty(Plan(_first.Id, _second.Id).Places);
    }

    /// <summary>
    /// A Project that renamed the Status is affected by identity: reported, with its other names unchanged.
    /// </summary>
    [Fact]
    public void A_Project_that_renamed_the_Status_is_affected()
    {
        var renamed = Project(tasks: 1, Operation(_first.Id, StatusDeltaKind.Rename, name: "Intake"), Operation(_third.Id, StatusDeltaKind.Rename, name: "Shipped"));

        var refused = Plan(_first.Id, destination: null);

        Assert.Equal(renamed, Assert.Single(refused.Report).ProjectId);
        Assert.Equal(["Doing", "Shipped"], refused.Report[0].After.Select(status => status.Name));
    }

    /// <summary>
    /// A Project holding only the Status is left empty and named, whatever its occupancy and destination; removing the
    /// Space's last default is refused naming none.
    /// </summary>
    [Fact]
    public void A_Project_left_empty_or_the_last_default_is_SetEmpty()
    {
        var onlyIt = Project(tasks: 0, Operation(_second.Id, StatusDeltaKind.Remove), Operation(_third.Id, StatusDeltaKind.Remove));

        foreach (var destination in new Guid?[] { _second.Id, Guid.NewGuid(), null })
        {
            var plan = Plan(_first.Id, destination);

            Assert.Equal(SpaceDefaultStatusVerdict.SetEmpty, plan.Verdict);
            Assert.Equal([onlyIt], plan.Projects);
        }

        var last = SpaceDefaultStatusRemoval.Plan(new SpaceStatusRemovalState([_first], _deltas, [], _occupancy), new SpaceStatusRemovalRequest(_first.Id, null, []), this, DateTimeOffset.UnixEpoch);

        Assert.Equal(SpaceDefaultStatusVerdict.SetEmpty, last.Verdict);
        Assert.Empty(last.Projects);
    }

    /// <summary>
    /// Nothing in the Status: no destination needed. A destination sent anyway is still validated, and a Status that is not
    /// a default is not found.
    /// </summary>
    [Fact]
    public void An_unoccupied_Status_needs_no_destination()
    {
        _ = Project(tasks: 0);

        Assert.Equal(SpaceDefaultStatusVerdict.Admitted, Plan(_first.Id, destination: null).Verdict);
        Assert.Equal(SpaceDefaultStatusVerdict.DestinationInvalid, Plan(_first.Id, _first.Id).Verdict);
        Assert.Equal(SpaceDefaultStatusVerdict.DestinationInvalid, Plan(_first.Id, Guid.NewGuid()).Verdict);
        Assert.Equal(SpaceDefaultStatusVerdict.NotFound, Plan(Guid.NewGuid(), destination: null).Verdict);
    }

    /// <summary>
    /// A blocked Project with no Task in the Status has nothing to place: it is never reported and needs no exception.
    /// </summary>
    [Fact]
    public void A_blocked_Project_with_no_Tasks_is_not_reported()
    {
        _ = Project(tasks: 0, Operation(_second.Id, StatusDeltaKind.Remove));
        var plain = Project(tasks: 1);

        var plan = Plan(_first.Id, _second.Id);

        Assert.Equal(SpaceDefaultStatusVerdict.Admitted, plan.Verdict);
        Assert.Equal([new SpaceStatusMigration(plain, _second.Id)], plan.Migrations);
    }

    /// <summary>
    /// The removed Status anchors places on both sides (order: second, added, first, third). The plan re-anchors them, and
    /// the set the planned rows derive is the set before without the Status: added, first, third.
    /// </summary>
    [Fact]
    public void Places_anchored_on_the_Status_keep_the_rest_of_the_order()
    {
        var parked = Owned("Parked");
        var anchored = Project(
            tasks: 0,
            [parked],
            Operation(parked.Id, StatusDeltaKind.Add),
            Operation(_second.Id, StatusDeltaKind.Place, afterStatusId: null),
            Operation(parked.Id, StatusDeltaKind.Place, afterStatusId: _second.Id));
        var delta = _deltas[anchored];
        var before = EffectiveStatusSet.Of(Defaults, delta);

        var plan = Plan(_second.Id, destination: null);

        Assert.Equal(SpaceDefaultStatusVerdict.Admitted, plan.Verdict);

        var places = Assert.Single(plan.Places);

        Assert.Equal(anchored, places.ProjectId);
        Assert.All(places.Places, place => Assert.True(place.Sequence >= delta.NextSequence));
        Assert.DoesNotContain(places.Places, place => place.StatusId == _second.Id || place.AfterStatusId == _second.Id);

        var after = delta.Without(_second.Id) with
        {
            Operations = [.. delta.Without(_second.Id).Operations.Where(operation => operation.Kind != StatusDeltaKind.Place), .. places.Places],
        };

        Assert.Equal(before.Where(status => status.Id != _second.Id), EffectiveStatusSet.Of(Defaults.Where(status => status.Id != _second.Id), after));
        Assert.Equal([parked.Id, _first.Id, _third.Id], EffectiveStatusSet.Of(Defaults.Where(status => status.Id != _second.Id), after).Select(status => status.Id));
    }

    private SpaceDefaultStatusRemovalPlan Plan(Guid statusId, Guid? destination, params SpaceStatusDestination[] exceptions) =>
        SpaceDefaultStatusRemoval.Plan(
            new SpaceStatusRemovalState(Defaults, _deltas, _projects, _occupancy),
            new SpaceStatusRemovalRequest(statusId, destination, exceptions),
            this,
            DateTimeOffset.UnixEpoch);

    private Guid Project(int tasks, params StatusDeltaOperation[] operations) => Project(tasks, [], operations);

    private Guid Project(int tasks, IReadOnlyList<StatusDefinition> owned, params StatusDeltaOperation[] operations)
    {
        var id = Guid.NewGuid();

        _projects.Add(id);

        if (tasks > 0)
        {
            _occupancy[id] = tasks;
        }

        if (operations.Length > 0)
        {
            _deltas[id] = new ProjectStatusDelta(owned, operations);
        }

        return id;
    }

    private static StatusDefinition Owned(string name) => new()
    {
        Id = Guid.NewGuid(),
        SpaceId = SpaceId,
        OwnerProjectId = Guid.NewGuid(),
        Name = name,
        Position = 0,
        CreatedAt = DateTimeOffset.UnixEpoch,
    };

    private static StatusDefinition Default(int position, string name) => new()
    {
        Id = Guid.NewGuid(),
        SpaceId = SpaceId,
        Name = name,
        Position = position,
        CreatedAt = DateTimeOffset.UnixEpoch,
    };

    private StatusDeltaOperation Operation(Guid statusId, StatusDeltaKind kind, string? name = null, Guid? afterStatusId = null) => new()
    {
        Id = Guid.NewGuid(),
        SpaceId = SpaceId,
        ProjectId = Guid.Empty,
        StatusId = statusId,
        Kind = kind,
        Name = name,
        AfterStatusId = afterStatusId,
        Sequence = ++_sequence,
        CreatedAt = DateTimeOffset.UnixEpoch,
    };
}
