using Xunit;
using Yello.Domain.Statuses;

namespace Yello.Tests.Slices.Statuses;

/// <summary>
/// The one derivation under a Space-level removal: with the default taken out of the defaults, every operation naming it
/// taken out of each delta, and each Project's dependent places re-anchored with <see cref="EffectiveStatusSet.Reanchored"/>,
/// every Project's set is its set before with that one Status taken out - order and names unchanged. A Project that had
/// already removed it is identical before and after; one left holding only it would be empty, which <c>Check</c> refuses.
/// </summary>
/// <remarks>
/// <para>
/// <b>Green before the removal exists</b>: these pin the premise the Space-level plan rests on - no second derivation and no
/// third ordering algorithm. The plan's own unit tests, which need its types, are the develop phase's.
/// </para>
/// <para>
/// <see cref="AfterSpaceRemoval"/> is the after-set the story states, built from the existing members only.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AD-16")]
[Trait("Requirement", "FR-27")]
public sealed class EffectiveStatusSetSpaceRemovalTests
{
    private static readonly Guid SpaceId = Guid.NewGuid();

    private static readonly Guid ProjectId = Guid.NewGuid();

    private readonly StatusDefinition _first = Default(0, "Queued");

    private readonly StatusDefinition _second = Default(1, "Doing");

    private readonly StatusDefinition _third = Default(2, "Finished");

    private int _sequence;

    private StatusDefinition[] Defaults => [_first, _second, _third];

    /// <summary>
    /// A Project with no delta: its set after is the remaining defaults in Space order.
    /// </summary>
    [Fact]
    public void A_plain_Projects_set_loses_only_the_removed_default()
    {
        AssertOnlyTheRemovedStatusLeaves(ProjectStatusDelta.Empty, _second.Id);
    }

    /// <summary>
    /// The removed default anchors places on both sides: it is itself placed first, and an added Status is placed after it
    /// (order: second, added, first, third). After the Space removal of the second, the added Status stays first and the rest
    /// keep their order.
    /// </summary>
    [Fact]
    public void Places_anchored_on_the_removed_default_on_both_sides_keep_the_rest_of_the_order()
    {
        var parked = Owned("Parked");
        var delta = Delta(
            [parked],
            Operation(parked.Id, StatusDeltaKind.Add),
            Operation(_second.Id, StatusDeltaKind.Place, afterStatusId: null),
            Operation(parked.Id, StatusDeltaKind.Place, afterStatusId: _second.Id));

        Assert.Equal([_second.Id, parked.Id, _first.Id, _third.Id], EffectiveStatusSet.Of(Defaults, delta).Select(status => status.Id));

        var after = AssertOnlyTheRemovedStatusLeaves(delta, _second.Id);

        Assert.Equal([parked.Id, _first.Id, _third.Id], after.Select(status => status.Id));
    }

    /// <summary>
    /// A default placed after the removed one, and the removed one placed after another default, both re-anchor: the order
    /// third, first-after-it, and so on, survives without the removed Status.
    /// </summary>
    [Fact]
    public void A_default_placed_after_the_removed_one_is_re_anchored_to_its_predecessor()
    {
        var delta = Delta(
            [],
            Operation(_third.Id, StatusDeltaKind.Place, afterStatusId: null),
            Operation(_first.Id, StatusDeltaKind.Place, afterStatusId: _second.Id));

        Assert.Equal([_third.Id, _second.Id, _first.Id], EffectiveStatusSet.Of(Defaults, delta).Select(status => status.Id));

        var after = AssertOnlyTheRemovedStatusLeaves(delta, _second.Id);

        Assert.Equal([_third.Id, _first.Id], after.Select(status => status.Id));
    }

    /// <summary>
    /// A Project that renamed the removed default is affected by identity, not name: it loses the Status, and its other
    /// names are unchanged.
    /// </summary>
    [Fact]
    public void A_Project_that_renamed_the_default_loses_it_and_keeps_its_other_names()
    {
        var delta = Delta(
            [],
            Operation(_first.Id, StatusDeltaKind.Rename, name: "Intake"),
            Operation(_third.Id, StatusDeltaKind.Rename, name: "Shipped"));

        var after = AssertOnlyTheRemovedStatusLeaves(delta, _first.Id);

        Assert.Equal(["Doing", "Shipped"], after.Select(status => status.Name));
    }

    /// <summary>
    /// A Project that had already removed the default is identical before and after: same Statuses, same order, same names,
    /// although its <c>Remove</c> of it goes with the definition.
    /// </summary>
    [Fact]
    public void A_Project_that_already_removed_the_default_is_identical_before_and_after()
    {
        var parked = Owned("Parked");
        var delta = Delta(
            [parked],
            Operation(_first.Id, StatusDeltaKind.Remove),
            Operation(parked.Id, StatusDeltaKind.Add),
            Operation(parked.Id, StatusDeltaKind.Place, afterStatusId: null),
            Operation(_third.Id, StatusDeltaKind.Rename, name: "Shipped"));
        var before = EffectiveStatusSet.Of(Defaults, delta);

        Assert.False(EffectiveStatusSet.Contains(before, _first.Id));
        Assert.Equal(before, AfterSpaceRemoval(delta, _first.Id));
    }

    /// <summary>
    /// A Project that removed the Space-wide destination is blocked: it still holds the removed default, and its set after
    /// the removal does not hold the destination - so it needs a destination of its own, from that set.
    /// </summary>
    [Fact]
    public void A_Project_that_removed_the_destination_holds_the_default_but_not_the_destination_after()
    {
        var parked = Owned("Parked");
        var delta = Delta([parked], Operation(_second.Id, StatusDeltaKind.Remove), Operation(parked.Id, StatusDeltaKind.Add));

        Assert.True(EffectiveStatusSet.Contains(EffectiveStatusSet.Of(Defaults, delta), _first.Id));

        var after = AssertOnlyTheRemovedStatusLeaves(delta, _first.Id);

        Assert.False(EffectiveStatusSet.Contains(after, _second.Id));
        Assert.Equal([_third.Id, parked.Id], after.Select(status => status.Id));
    }

    /// <summary>
    /// A Project holding only the removed default - it removed the other two and added none - would be left with no Status,
    /// which <c>Check</c> refuses as <c>SetEmpty</c>; so would removing the Space's last default from a plain Project.
    /// </summary>
    [Fact]
    public void A_Project_left_with_nothing_is_the_empty_set_Check_refuses()
    {
        var onlyFirst = Delta([], Operation(_second.Id, StatusDeltaKind.Remove), Operation(_third.Id, StatusDeltaKind.Remove));

        Assert.Equal([_first.Id], EffectiveStatusSet.Of(Defaults, onlyFirst).Select(status => status.Id));
        Assert.Equal(StatusDeltaVerdict.SetEmpty, EffectiveStatusSet.Check(AfterSpaceRemoval(onlyFirst, _first.Id), namedStatusId: null));
        Assert.Equal(StatusDeltaVerdict.SetEmpty, EffectiveStatusSet.Check(EffectiveStatusSet.Of([], ProjectStatusDelta.Empty), namedStatusId: null));
    }

    /// <summary>
    /// Asserts the invariant: the set after the Space removal is the set before with <paramref name="removedId"/> taken out,
    /// entry for entry (id, name, source, default name). Answers the set after.
    /// </summary>
    private IReadOnlyList<EffectiveStatus> AssertOnlyTheRemovedStatusLeaves(ProjectStatusDelta delta, Guid removedId)
    {
        var before = EffectiveStatusSet.Of(Defaults, delta);
        var after = AfterSpaceRemoval(delta, removedId);

        Assert.True(EffectiveStatusSet.Contains(before, removedId), "The case's Project does not hold the Status it removes.");
        Assert.Equal(before.Where(status => status.Id != removedId), after);

        return after;
    }

    /// <summary>
    /// The set after a Space-level removal of <paramref name="removedId"/>: the defaults without it, the delta without every
    /// operation naming it, and the places it anchored re-anchored by <see cref="EffectiveStatusSet.Reanchored"/> against the
    /// set before, sequenced after the delta's last operation.
    /// </summary>
    private IReadOnlyList<EffectiveStatus> AfterSpaceRemoval(ProjectStatusDelta delta, Guid removedId)
    {
        var before = EffectiveStatusSet.Of(Defaults, delta);
        var placed = delta.Operations
            .Where(operation => operation.Kind == StatusDeltaKind.Place && operation.StatusId != removedId)
            .Select(operation => operation.StatusId)
            .ToHashSet();
        var anchoredOnIt = delta.Operations.Any(operation => operation.Kind == StatusDeltaKind.Place && (operation.AfterStatusId == removedId || operation.StatusId == removedId));
        var kept = delta.Without(removedId);

        if (anchoredOnIt && EffectiveStatusSet.Contains(before, removedId))
        {
            _sequence = Math.Max(_sequence, delta.NextSequence - 1);
            kept = kept.With(null, [.. EffectiveStatusSet.Reanchored(before, placed, removedId).Select(placement => Operation(placement.StatusId, StatusDeltaKind.Place, afterStatusId: placement.AfterStatusId))]);
        }

        return EffectiveStatusSet.Of(Defaults.Where(definition => definition.Id != removedId), kept);
    }

    private static StatusDefinition Owned(string name) => new()
    {
        Id = Guid.NewGuid(),
        SpaceId = SpaceId,
        OwnerProjectId = ProjectId,
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

    private StatusDeltaOperation Operation(Guid statusId, StatusDeltaKind kind, string? name = null, Guid? afterStatusId = null) => new()
    {
        Id = Guid.NewGuid(),
        SpaceId = SpaceId,
        ProjectId = ProjectId,
        StatusId = statusId,
        Kind = kind,
        Name = name,
        AfterStatusId = afterStatusId,
        Sequence = ++_sequence,
        CreatedAt = DateTimeOffset.UnixEpoch,
    };
}
