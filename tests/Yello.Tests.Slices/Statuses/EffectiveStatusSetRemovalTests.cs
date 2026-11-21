using Xunit;
using Yello.Domain.Statuses;

namespace Yello.Tests.Slices.Statuses;

/// <summary>
/// The one derivation once a delta can take a Status away: a <c>Remove</c> drops a default from the Project's set and from
/// no other Project's, a removal of the last Status is the empty set <c>Check</c> refuses, the removed Status is not in the
/// set a destination is checked against, and a default whose <c>Remove</c> is gone is back at its Space slot under its Space
/// name.
/// </summary>
/// <remarks>
/// <para>
/// <b>The kind is parsed from its name</b> because <c>StatusDeltaKind.Remove</c> does not exist yet; the parse throws until it
/// does, which is the red reason. The change that adds the member swaps <see cref="Remove"/> for it.
/// </para>
/// <para>
/// The re-anchoring planner and the post-removal delta are the dev phase's types (story Task 1); their unit tests are written
/// with them. The wire case <c>Removing_a_Status_that_others_are_placed_after_leaves_the_rest_in_their_order</c> holds the
/// same rule end to end.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AD-16")]
[Trait("Requirement", "FR-26")]
public sealed class EffectiveStatusSetRemovalTests
{
    private static readonly Guid SpaceId = Guid.NewGuid();

    private static readonly Guid ProjectId = Guid.NewGuid();

    private readonly StatusDefinition _first = Default(0, "Queued");

    private readonly StatusDefinition _second = Default(1, "Doing");

    private readonly StatusDefinition _third = Default(2, "Finished");

    private int _sequence;

    private static StatusDeltaKind Remove => Enum.Parse<StatusDeltaKind>("Remove");

    private StatusDefinition[] Defaults => [_first, _second, _third];

    /// <summary>
    /// A <c>Remove</c> of the second default drops it and keeps the other two in Space order.
    /// </summary>
    [Fact(Skip = "red: AC5 - StatusDeltaKind has no Remove and Apply ignores it (Q1)")]
    public void A_remove_drops_the_default_and_keeps_the_rest_in_Space_order()
    {
        var set = EffectiveStatusSet.Of(Defaults, Delta([], Operation(_second.Id, Remove)));

        Assert.Equal([_first.Id, _third.Id], set.Select(status => status.Id));
    }

    /// <summary>
    /// A removal is the Project's own: a Project that removed the first default lacks it, while a Project with no delta over
    /// the same defaults still holds it.
    /// </summary>
    [Fact(Skip = "red: AC5 - StatusDeltaKind has no Remove (Q1)")]
    public void A_remove_in_one_Project_leaves_another_Projects_set_whole()
    {
        var removedHere = EffectiveStatusSet.Of(Defaults, Delta([], Operation(_first.Id, Remove)));
        var elsewhere = EffectiveStatusSet.Of(Defaults, ProjectStatusDelta.Empty);

        Assert.False(EffectiveStatusSet.Contains(removedHere, _first.Id));
        Assert.True(EffectiveStatusSet.Contains(elsewhere, _first.Id));
    }

    /// <summary>
    /// The destination of a removal is checked against the set after it: the removed Status is not in that set, and the
    /// others are.
    /// </summary>
    [Fact(Skip = "red: AC2 - StatusDeltaKind has no Remove (Q5)")]
    public void The_removed_Status_is_not_in_the_set_a_destination_is_checked_against()
    {
        var after = EffectiveStatusSet.Of(Defaults, Delta([], Operation(_third.Id, Remove)));

        Assert.False(EffectiveStatusSet.Contains(after, _third.Id));
        Assert.True(EffectiveStatusSet.Contains(after, _first.Id));
        Assert.True(EffectiveStatusSet.Contains(after, _second.Id));
    }

    /// <summary>
    /// Removing every default leaves the empty set, which <c>Check</c> refuses as <c>SetEmpty</c>; one left is admitted.
    /// </summary>
    [Fact(Skip = "red: AC6 - StatusDeltaKind has no Remove, so SetEmpty is unreachable")]
    public void Removing_the_last_Status_leaves_the_empty_set_Check_refuses()
    {
        var oneLeft = EffectiveStatusSet.Of(Defaults, Delta([], Operation(_second.Id, Remove), Operation(_third.Id, Remove)));
        var none = EffectiveStatusSet.Of(Defaults, Delta([], Operation(_second.Id, Remove), Operation(_third.Id, Remove), Operation(_first.Id, Remove)));

        Assert.Equal(StatusDeltaVerdict.Admitted, EffectiveStatusSet.Check(oneLeft, namedStatusId: null));
        Assert.Equal(StatusDeltaVerdict.SetEmpty, EffectiveStatusSet.Check(none, namedStatusId: null));
    }

    /// <summary>
    /// A <c>Remove</c> of a Status the set does not hold changes nothing.
    /// </summary>
    [Fact(Skip = "red: AC5 - StatusDeltaKind has no Remove")]
    public void A_remove_of_a_Status_the_set_does_not_hold_changes_nothing()
    {
        var set = EffectiveStatusSet.Of(Defaults, Delta([], Operation(Guid.NewGuid(), Remove)));

        Assert.Equal([_first.Id, _second.Id, _third.Id], set.Select(status => status.Id));
    }

    /// <summary>
    /// A removed default whose <c>Remove</c> is deleted - and whose rename and place the removal already deleted - is back at
    /// its Space slot under its Space name, a Space default with no default name.
    /// </summary>
    [Fact(Skip = "red: AC7 - StatusDeltaKind has no Remove; Q3 (a) deletes the removed Status's rename and place")]
    public void A_default_whose_remove_is_gone_is_back_at_its_Space_slot_under_its_Space_name()
    {
        var removed = EffectiveStatusSet.Of(Defaults, Delta([], Operation(_second.Id, Remove)));
        var restored = EffectiveStatusSet.Of(Defaults, ProjectStatusDelta.Empty);

        Assert.DoesNotContain(removed, status => status.Id == _second.Id);
        Assert.Equal(new EffectiveStatus(_second.Id, "Doing", StatusSource.SpaceDefault, null), restored[1]);
    }

    /// <summary>
    /// A place anchored on a removed Status is ignored and the placed Status keeps its Space slot: the deterministic
    /// fallback for an anchor that leaves without being re-anchored (a Space-level removal).
    /// </summary>
    [Fact(Skip = "red: AC10 - StatusDeltaKind has no Remove; Q3 (a) keeps the ignored-anchor rule as the fallback")]
    public void A_place_anchored_on_a_removed_Status_falls_back_to_its_Space_slot()
    {
        var set = EffectiveStatusSet.Of(
            Defaults,
            Delta([], Operation(_third.Id, Remove), Operation(_first.Id, StatusDeltaKind.Place, afterStatusId: _third.Id)));

        Assert.Equal([_first.Id, _second.Id], set.Select(status => status.Id));
    }

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
