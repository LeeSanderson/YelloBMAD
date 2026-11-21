using Xunit;
using Yello.Domain.Statuses;

namespace Yello.Tests.Slices.Statuses;

/// <summary>
/// What a change of the Space's defaults does to a Project's set through the one derivation, with no Project row written: a
/// new default reaches every Project at its Space slot, a renamed default shows its new name wherever no Project renamed it,
/// a reordered default moves wherever no Project placed it, and a name that would stand twice in a Project's after-set is the
/// one <c>Check</c> already refuses.
/// </summary>
/// <remarks>
/// These hold before any Space-level write exists, and must keep holding after: the Space-level plan computes every
/// Project's after-set with <see cref="EffectiveStatusSet.Of(IEnumerable{StatusDefinition}, ProjectStatusDelta)"/> and judges
/// it with <see cref="EffectiveStatusSet.Check"/>, never with a second derivation. A Space write that also wrote a
/// <c>Rename</c> or <c>Place</c> per Project would not turn these red; the wire cases' delta counts catch that.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AD-16")]
[Trait("Requirement", "FR-27")]
public sealed class EffectiveStatusSetSpaceDefaultTests
{
    private static readonly Guid SpaceId = Guid.NewGuid();

    private static readonly Guid ProjectId = Guid.NewGuid();

    private readonly StatusDefinition _first = Default(0, "Queued");

    private readonly StatusDefinition _second = Default(1, "Doing");

    private readonly StatusDefinition _third = Default(2, "Finished");

    private int _sequence;

    private StatusDefinition[] Defaults => [_first, _second, _third];

    /// <summary>
    /// A fourth default at the end of the Space order appears in a plain Project at the end, in a Project that removed the
    /// second default after the other two, and in a Project that added "Parked" before "Parked".
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-24")]
    public void A_new_default_reaches_every_Project_at_its_Space_slot_before_unplaced_added_Statuses()
    {
        var review = Default(3, "Review");
        var parked = Owned("Parked");
        StatusDefinition[] after = [_first, _second, _third, review];

        var plain = EffectiveStatusSet.Of(after, ProjectStatusDelta.Empty);
        var removed = EffectiveStatusSet.Of(after, Delta([], Operation(_second.Id, StatusDeltaKind.Remove)));
        var added = EffectiveStatusSet.Of(after, Delta([parked], Operation(parked.Id, StatusDeltaKind.Add)));

        Assert.Equal([_first.Id, _second.Id, _third.Id, review.Id], plain.Select(status => status.Id));
        Assert.Equal([_first.Id, _third.Id, review.Id], removed.Select(status => status.Id));
        Assert.Equal([_first.Id, _second.Id, _third.Id, review.Id, parked.Id], added.Select(status => status.Id));
        Assert.Equal(StatusSource.SpaceDefault, added[3].Source);
    }

    /// <summary>
    /// The second default renamed at Space level shows its new name, with no default name, in a plain Project, in one that
    /// placed it first and in one that renamed the first default.
    /// </summary>
    [Fact]
    public void A_renamed_default_shows_its_new_name_in_plain_placed_and_other_renamed_Projects()
    {
        _second.Name = "Underway";

        var plain = EffectiveStatusSet.Of(Defaults, ProjectStatusDelta.Empty);
        var placed = EffectiveStatusSet.Of(Defaults, Delta([], Operation(_second.Id, StatusDeltaKind.Place)));
        var other = EffectiveStatusSet.Of(Defaults, Delta([], Operation(_first.Id, StatusDeltaKind.Rename, name: "Intake")));

        Assert.Equal(new EffectiveStatus(_second.Id, "Underway", StatusSource.SpaceDefault, null), plain[1]);
        Assert.Equal(new EffectiveStatus(_second.Id, "Underway", StatusSource.SpaceDefault, null), placed[0]);
        Assert.Equal(["Intake", "Underway", "Finished"], other.Select(status => status.Name));
    }

    /// <summary>
    /// A Project that kept its own name for a default renamed at Space level shows its own name, and the new Space name as
    /// the default's - "renamed here (was {new name})".
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR16")]
    public void A_kept_own_name_states_the_new_Space_name_as_the_default_name()
    {
        _first.Name = "Waiting";

        var kept = EffectiveStatusSet.Of(Defaults, Delta([], Operation(_first.Id, StatusDeltaKind.Rename, name: "Intake")));

        Assert.Equal(new EffectiveStatus(_first.Id, "Intake", StatusSource.SpaceDefault, "Waiting"), kept[0]);
    }

    /// <summary>
    /// A Project's own name equal, ordinal, to the default's new Space name shows as a plain default with no default name, so
    /// to the Admin it has not renamed it; a name differing only in case still states the default's name.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR16")]
    public void An_own_name_equal_to_the_Space_name_shows_no_default_name_and_one_differing_in_case_does()
    {
        _first.Name = "Waiting";

        var same = EffectiveStatusSet.Of(Defaults, Delta([], Operation(_first.Id, StatusDeltaKind.Rename, name: "Waiting")));
        var caseOnly = EffectiveStatusSet.Of(Defaults, Delta([], Operation(_first.Id, StatusDeltaKind.Rename, name: "WAITING")));

        Assert.Null(same[0].DefaultName);
        Assert.Equal("Waiting", caseOnly[0].DefaultName);
    }

    /// <summary>
    /// The third default placed first in Space order moves first in a plain Project, while a Project that placed it after the
    /// first default keeps its own placement.
    /// </summary>
    [Fact]
    public void A_reordered_default_moves_in_a_plain_Project_and_keeps_its_place_in_a_placed_one()
    {
        _third.Position = 0;
        _first.Position = 1;
        _second.Position = 2;

        var plain = EffectiveStatusSet.Of(Defaults, ProjectStatusDelta.Empty);
        var placed = EffectiveStatusSet.Of(Defaults, Delta([], Operation(_third.Id, StatusDeltaKind.Place, afterStatusId: _first.Id)));

        Assert.Equal([_third.Id, _first.Id, _second.Id], plain.Select(status => status.Id));
        Assert.Equal([_first.Id, _third.Id, _second.Id], placed.Select(status => status.Id));
    }

    /// <summary>
    /// A new default named "parked" in a Project that added "Parked", and a default renamed "Review" in a Project that renamed
    /// another default "Review", each leave an after-set <c>Check</c> refuses as <c>NameTaken</c> for the default the Space
    /// write names; the same write in a plain Project is admitted.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-25")]
    public void A_Space_name_a_Projects_set_already_holds_is_one_Check_refuses()
    {
        var parked = Owned("Parked");
        var lowerParked = Default(3, "parked");
        var withAdded = EffectiveStatusSet.Of([.. Defaults, lowerParked], Delta([parked], Operation(parked.Id, StatusDeltaKind.Add)));
        var plainWithAdded = EffectiveStatusSet.Of([.. Defaults, lowerParked], ProjectStatusDelta.Empty);

        _third.Name = "Review";

        var withRenamed = EffectiveStatusSet.Of(Defaults, Delta([], Operation(_second.Id, StatusDeltaKind.Rename, name: "Review")));

        Assert.Equal(StatusDeltaVerdict.NameTaken, EffectiveStatusSet.Check(withAdded, lowerParked.Id));
        Assert.Equal(StatusDeltaVerdict.Admitted, EffectiveStatusSet.Check(plainWithAdded, lowerParked.Id));
        Assert.Equal(StatusDeltaVerdict.NameTaken, EffectiveStatusSet.Check(withRenamed, _third.Id));
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
