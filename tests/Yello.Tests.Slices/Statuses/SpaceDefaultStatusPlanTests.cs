using Xunit;
using Yello.Domain.Statuses;

namespace Yello.Tests.Slices.Statuses;

/// <summary>
/// The Space-level plan, with no database: what an add, a rename and a reorder of the defaults write, which Projects a rename
/// conflicts with and whose <c>Rename</c> operations it deletes, which Projects a name clash names, and the guard that keeps
/// the default set from ever being empty.
/// </summary>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-24")]
[Trait("Requirement", "FR-27")]
[Trait("Requirement", "AD-16")]
public sealed class SpaceDefaultStatusPlanTests
{
    private static readonly Guid SpaceId = Guid.NewGuid();

    private readonly StatusDefinition _first = Default(0, "Queued");

    private readonly StatusDefinition _second = Default(1, "Doing");

    private readonly StatusDefinition _third = Default(2, "Finished");

    private int _sequence;

    private StatusDefinition[] Defaults => [_first, _second, _third];

    /// <summary>
    /// An add with no anchor goes last and moves no existing default; with an anchor it goes right after it and the defaults
    /// behind it shift by one. Every Project - plain, one that removed another default, one that added a Status - is judged
    /// and none is named, and no operation is deleted.
    /// </summary>
    [Fact]
    public void An_add_goes_last_or_after_its_anchor_and_writes_nothing_per_Project()
    {
        var removing = Guid.NewGuid();
        var adding = Guid.NewGuid();
        var parked = Owned(adding, "Parked");
        var deltas = new Dictionary<Guid, ProjectStatusDelta>
        {
            [removing] = Delta([], Operation(removing, _second.Id, StatusDeltaKind.Remove)),
            [adding] = Delta([parked], Operation(adding, parked.Id, StatusDeltaKind.Add)),
        };

        var last = SpaceDefaultStatusPlan.Add(Defaults, deltas, Default(0, "Review"), afterStatusId: null);
        var placed = SpaceDefaultStatusPlan.Add(Defaults, deltas, Default(0, "Triage"), _first.Id);

        Assert.Equal(SpaceDefaultStatusVerdict.Admitted, last.Verdict);
        Assert.Equal(3, last.Added?.Position);
        Assert.Empty(last.Positions);
        Assert.Empty(last.RenameOperationsDeletedIn);
        Assert.Equal(SpaceDefaultStatusVerdict.Admitted, placed.Verdict);
        Assert.Equal(1, placed.Added?.Position);
        Assert.Equal(new Dictionary<Guid, int> { [_second.Id] = 2, [_third.Id] = 3 }, placed.Positions);
    }

    /// <summary>
    /// An add of a name another default holds, in any case, is refused naming no Project; an anchor that is not a default is
    /// refused as a position.
    /// </summary>
    [Fact]
    public void An_add_of_a_default_name_or_after_a_non_default_is_refused()
    {
        Assert.Equal(SpaceDefaultStatusVerdict.NameTaken, SpaceDefaultStatusPlan.Add(Defaults, NoDeltas, Default(0, "DOING"), afterStatusId: null).Verdict);
        Assert.Equal(SpaceDefaultStatusVerdict.PositionInvalid, SpaceDefaultStatusPlan.Add(Defaults, NoDeltas, Default(0, "Review"), Guid.NewGuid()).Verdict);
    }

    /// <summary>
    /// A Project that added "Parked" makes a Space add of "parked" a clash naming that Project, and only that Project.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-25")]
    public void An_add_of_a_name_a_Project_added_names_that_Project()
    {
        var parking = Guid.NewGuid();
        var parked = Owned(parking, "Parked");
        var deltas = new Dictionary<Guid, ProjectStatusDelta>
        {
            [parking] = Delta([parked], Operation(parking, parked.Id, StatusDeltaKind.Add)),
            [Guid.NewGuid()] = ProjectStatusDelta.Empty,
        };

        var change = SpaceDefaultStatusPlan.Add(Defaults, deltas, Default(0, "parked"), afterStatusId: null);

        Assert.Equal(SpaceDefaultStatusVerdict.ProjectNameTaken, change.Verdict);
        Assert.Equal([parking], change.ClashingProjects);
    }

    /// <summary>
    /// A rename with no Project's own name writes only the name: no position, no deleted operation, no conflict - whether a
    /// Project placed the Status or renamed another one.
    /// </summary>
    [Fact]
    public void A_rename_no_Project_renamed_writes_only_the_name()
    {
        var placed = Guid.NewGuid();
        var other = Guid.NewGuid();
        var deltas = new Dictionary<Guid, ProjectStatusDelta>
        {
            [placed] = Delta([], Operation(placed, _second.Id, StatusDeltaKind.Place)),
            [other] = Delta([], Operation(other, _first.Id, StatusDeltaKind.Rename, "Intake")),
        };

        var change = SpaceDefaultStatusPlan.Rename(Defaults, deltas, _second.Id, "Underway", replaceProjectNames: null);

        Assert.Equal(SpaceDefaultStatusVerdict.Admitted, change.Verdict);
        Assert.Equal((_second.Id, "Underway"), (change.RenamedStatusId, change.Name));
        Assert.Empty(change.Positions);
        Assert.Empty(change.RenameOperationsDeletedIn);
        Assert.Empty(change.Conflicts);
    }

    /// <summary>
    /// A Project's own name that differs from both the old and the new Space name, even only in case, is a conflict: with no
    /// decision the rename is refused naming it; with <c>true</c> its operation is deleted; with <c>false</c> it is kept. A
    /// Project whose own name equals the old or the new name exactly is no conflict, and its operation is deleted either way.
    /// A Project that removed the Status is never a conflict.
    /// </summary>
    /// <param name="decision">The decision sent, or <c>null</c> for none.</param>
    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "UX-DR16")]
    public void Own_names_that_differ_conflict_and_the_decision_decides_their_operations(bool? decision)
    {
        var differs = Guid.NewGuid();
        var caseOnly = Guid.NewGuid();
        var sameAsOld = Guid.NewGuid();
        var sameAsNew = Guid.NewGuid();
        var removed = Guid.NewGuid();
        var deltas = new Dictionary<Guid, ProjectStatusDelta>
        {
            [differs] = Delta([], Operation(differs, _first.Id, StatusDeltaKind.Rename, "Intake")),
            [caseOnly] = Delta([], Operation(caseOnly, _first.Id, StatusDeltaKind.Rename, "QUEUED")),
            [sameAsOld] = Delta([], Operation(sameAsOld, _first.Id, StatusDeltaKind.Rename, "Queued")),
            [sameAsNew] = Delta([], Operation(sameAsNew, _first.Id, StatusDeltaKind.Rename, "Waiting")),
            [removed] = Delta([], Operation(removed, _first.Id, StatusDeltaKind.Remove)),
        };

        var change = SpaceDefaultStatusPlan.Rename(Defaults, deltas, _first.Id, "Waiting", decision);

        Assert.Equal(new[] { caseOnly, differs }.Order(), change.Conflicts.Select(conflict => conflict.ProjectId).Order());

        switch (decision)
        {
            case null:
                Assert.Equal(SpaceDefaultStatusVerdict.RenameConflict, change.Verdict);
                Assert.Equal(_first.Id, change.RenamedStatusId);
                Assert.Empty(change.RenameOperationsDeletedIn);
                break;

            case true:
                Assert.Equal(SpaceDefaultStatusVerdict.Admitted, change.Verdict);
                Assert.Equal(new[] { caseOnly, differs, sameAsNew, sameAsOld }.Order(), change.RenameOperationsDeletedIn.Order());
                break;

            default:
                Assert.Equal(SpaceDefaultStatusVerdict.Admitted, change.Verdict);
                Assert.Equal(new[] { sameAsNew, sameAsOld }.Order(), change.RenameOperationsDeletedIn.Order());
                break;
        }
    }

    /// <summary>
    /// A Project renamed the second default "Review" and the third "Mine". A Space rename of the third to "Review" clashes
    /// when the Project's own name is replaced and not when it is kept; a Space rename of the first to "Review", which that
    /// Project never renamed, clashes either way. Another default's name is refused before any Project is looked at, and the
    /// default's own name in another case is admitted.
    /// </summary>
    /// <param name="replace">The decision.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "FR-25")]
    public void A_rename_clashes_with_a_Projects_other_name_depending_on_the_decision(bool replace)
    {
        var project = Guid.NewGuid();
        var deltas = new Dictionary<Guid, ProjectStatusDelta>
        {
            [project] = Delta(
                [],
                Operation(project, _second.Id, StatusDeltaKind.Rename, "Review"),
                Operation(project, _third.Id, StatusDeltaKind.Rename, "Mine")),
        };

        var throughConflict = SpaceDefaultStatusPlan.Rename(Defaults, deltas, _third.Id, "Review", replace);
        var plain = SpaceDefaultStatusPlan.Rename(Defaults, deltas, _first.Id, "review", replace);

        Assert.Equal(replace ? SpaceDefaultStatusVerdict.ProjectNameTaken : SpaceDefaultStatusVerdict.Admitted, throughConflict.Verdict);
        Assert.Equal(replace ? new[] { project } : Array.Empty<Guid>(), throughConflict.ClashingProjects);
        Assert.Equal(SpaceDefaultStatusVerdict.ProjectNameTaken, plain.Verdict);
        Assert.Equal([project], plain.ClashingProjects);
        Assert.Equal(SpaceDefaultStatusVerdict.NameTaken, SpaceDefaultStatusPlan.Rename(Defaults, deltas, _first.Id, "FINISHED", replace).Verdict);
        Assert.Equal(SpaceDefaultStatusVerdict.Admitted, SpaceDefaultStatusPlan.Rename(Defaults, NoDeltas, _first.Id, "QUEUED", replace).Verdict);
    }

    /// <summary>
    /// A rename to the name the default already has changes nothing; a Status that is not a default is not found.
    /// </summary>
    [Fact]
    public void A_rename_to_the_same_name_is_unchanged_and_a_non_default_is_not_found()
    {
        Assert.Equal(SpaceDefaultStatusVerdict.Unchanged, SpaceDefaultStatusPlan.Rename(Defaults, NoDeltas, _first.Id, "Queued", replaceProjectNames: null).Verdict);
        Assert.Equal(SpaceDefaultStatusVerdict.NotFound, SpaceDefaultStatusPlan.Rename(Defaults, NoDeltas, Guid.NewGuid(), "Queued", replaceProjectNames: null).Verdict);
    }

    /// <summary>
    /// Placing the third default first rewrites every default's position to 0..2 in the new order, and nothing else: a plain
    /// Project follows it through the derivation while a Project that placed the third after the first keeps its own place.
    /// </summary>
    [Fact]
    public void A_reorder_rewrites_positions_and_only_unplaced_Projects_follow()
    {
        var placed = Guid.NewGuid();
        var placedDelta = Delta([], Operation(placed, _third.Id, StatusDeltaKind.Place, afterStatusId: _first.Id));

        var change = SpaceDefaultStatusPlan.Place(Defaults, _third.Id, afterStatusId: null);

        Assert.Equal(SpaceDefaultStatusVerdict.Admitted, change.Verdict);
        Assert.Equal(new Dictionary<Guid, int> { [_third.Id] = 0, [_first.Id] = 1, [_second.Id] = 2 }, change.Positions);

        foreach (var status in Defaults)
        {
            status.Position = change.Positions[status.Id];
        }

        Assert.Equal([_third.Id, _first.Id, _second.Id], EffectiveStatusSet.Of(Defaults, ProjectStatusDelta.Empty).Select(status => status.Id));
        Assert.Equal([_first.Id, _third.Id, _second.Id], EffectiveStatusSet.Of(Defaults, placedDelta).Select(status => status.Id));
    }

    /// <summary>
    /// A place anchored on itself or on a Status that is not a default is refused as a position; a Status in the path that is
    /// not a default is not found.
    /// </summary>
    [Fact]
    public void A_reorder_anchor_that_is_not_another_default_is_refused()
    {
        Assert.Equal(SpaceDefaultStatusVerdict.PositionInvalid, SpaceDefaultStatusPlan.Place(Defaults, _second.Id, _second.Id).Verdict);
        Assert.Equal(SpaceDefaultStatusVerdict.PositionInvalid, SpaceDefaultStatusPlan.Place(Defaults, _second.Id, Guid.NewGuid()).Verdict);
        Assert.Equal(SpaceDefaultStatusVerdict.NotFound, SpaceDefaultStatusPlan.Place(Defaults, Guid.NewGuid(), afterStatusId: null).Verdict);
    }

    /// <summary>
    /// The default set is never left empty; one default is enough. Anchor: the guard admits the three seeded defaults.
    /// </summary>
    [Fact]
    public void An_empty_default_set_is_refused()
    {
        Assert.Equal(SpaceDefaultStatusVerdict.SetEmpty, SpaceDefaultStatusPlan.CheckDefaults([]));
        Assert.Equal(SpaceDefaultStatusVerdict.Admitted, SpaceDefaultStatusPlan.CheckDefaults([_first]));
        Assert.Equal(SpaceDefaultStatusVerdict.Admitted, SpaceDefaultStatusPlan.CheckDefaults(Defaults));
    }

    private static Dictionary<Guid, ProjectStatusDelta> NoDeltas => [];

    private static StatusDefinition Owned(Guid projectId, string name) => new()
    {
        Id = Guid.NewGuid(),
        SpaceId = SpaceId,
        OwnerProjectId = projectId,
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

    private static ProjectStatusDelta Delta(IReadOnlyList<StatusDefinition> owned, params StatusDeltaOperation[] operations) => new(owned, operations);

    private StatusDeltaOperation Operation(Guid projectId, Guid statusId, StatusDeltaKind kind, string? name = null, Guid? afterStatusId = null) => new()
    {
        Id = Guid.NewGuid(),
        SpaceId = SpaceId,
        ProjectId = projectId,
        StatusId = statusId,
        Kind = kind,
        Name = name,
        AfterStatusId = afterStatusId,
        Sequence = ++_sequence,
        CreatedAt = DateTimeOffset.UnixEpoch,
    };
}
