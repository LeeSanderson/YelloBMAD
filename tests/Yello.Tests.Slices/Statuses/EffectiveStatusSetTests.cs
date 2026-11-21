using Xunit;
using Yello.Domain.Statuses;

namespace Yello.Tests.Slices.Statuses;

/// <summary>
/// The one derivation of a Project's effective Status set: the Space's defaults in Space order, then the Project's delta
/// applied in the delta's order - add, rename, place after, place first, an anchor the set does not hold, a stable tie, two
/// Projects over one set of defaults - and the check every delta write makes: never empty, and a name no other Status of
/// the set holds, compared case-insensitively.
/// </summary>
/// <remarks>
/// No database: the wire cases in <c>ProjectEndpointTests.Statuses</c> hold the same rules end to end.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AD-16")]
[Trait("Requirement", "FR-25")]
public sealed class EffectiveStatusSetTests
{
    private static readonly Guid SpaceId = Guid.NewGuid();

    private static readonly Guid ProjectId = Guid.NewGuid();

    private readonly StatusDefinition _first = Default(0, "Queued");

    private readonly StatusDefinition _second = Default(1, "Doing");

    private readonly StatusDefinition _third = Default(2, "Finished");

    private int _sequence;

    /// <summary>
    /// An empty delta is the Space's defaults in Space order, each a Space default with no default name.
    /// </summary>
    [Fact]
    public void An_empty_delta_is_the_Spaces_defaults_in_Space_order()
    {
        var set = EffectiveStatusSet.Of([_third, _first, _second], ProjectStatusDelta.Empty);

        Assert.Equal([_first.Id, _second.Id, _third.Id], set.Select(status => status.Id));
        Assert.All(set, status => Assert.Equal(StatusSource.SpaceDefault, status.Source));
        Assert.All(set, status => Assert.Null(status.DefaultName));
        Assert.Equal(set, EffectiveStatusSet.Of([_third, _first, _second]));
    }

    /// <summary>
    /// Two defaults sharing a position keep the reader's order, which is the database's <c>uniqueidentifier</c> order.
    /// </summary>
    [Fact]
    public void A_tie_on_position_keeps_the_readers_order()
    {
        var tiedFirst = Default(1, "Tied first");
        var tiedSecond = Default(1, "Tied second");

        Assert.Equal([_first.Id, tiedFirst.Id, tiedSecond.Id], EffectiveStatusSet.Of([tiedFirst, _first, tiedSecond]).Select(status => status.Id));
        Assert.Equal([_first.Id, tiedSecond.Id, tiedFirst.Id], EffectiveStatusSet.Of([tiedSecond, _first, tiedFirst]).Select(status => status.Id));
    }

    /// <summary>
    /// An added Status goes last, marked as added here under the name it was added with.
    /// </summary>
    [Fact]
    public void An_add_appends_the_owned_Status_as_added_here()
    {
        var parked = Owned("Parked");
        var set = EffectiveStatusSet.Of(Defaults, Delta([parked], Operation(parked.Id, StatusDeltaKind.Add)));

        Assert.Equal([_first.Id, _second.Id, _third.Id, parked.Id], set.Select(status => status.Id));
        Assert.Equal(new EffectiveStatus(parked.Id, "Parked", StatusSource.AddedHere, null), set[^1]);
    }

    /// <summary>
    /// An add whose definition the Project does not own changes nothing: a delta reaches only its own Project's Statuses.
    /// </summary>
    [Fact]
    public void An_add_of_a_definition_the_Project_does_not_own_changes_nothing()
    {
        var set = EffectiveStatusSet.Of(Defaults, Delta([], Operation(Guid.NewGuid(), StatusDeltaKind.Add)));

        Assert.Equal([_first.Id, _second.Id, _third.Id], set.Select(status => status.Id));
    }

    /// <summary>
    /// A renamed default keeps its id and source, takes the new name and states its own; a renamed added Status has no
    /// default name; renaming a default back to its own name clears it.
    /// </summary>
    [Fact]
    public void A_rename_keeps_the_id_and_names_the_default_it_renamed()
    {
        var parked = Owned("Parked");
        var set = EffectiveStatusSet.Of(
            Defaults,
            Delta(
                [parked],
                Operation(_second.Id, StatusDeltaKind.Rename, name: "Underway"),
                Operation(parked.Id, StatusDeltaKind.Add),
                Operation(parked.Id, StatusDeltaKind.Rename, name: "On ice"),
                Operation(_third.Id, StatusDeltaKind.Rename, name: "Finished")));

        Assert.Equal(new EffectiveStatus(_second.Id, "Underway", StatusSource.SpaceDefault, "Doing"), set[1]);
        Assert.Equal(new EffectiveStatus(_third.Id, "Finished", StatusSource.SpaceDefault, null), set[2]);
        Assert.Equal(new EffectiveStatus(parked.Id, "On ice", StatusSource.AddedHere, null), set[3]);
    }

    /// <summary>
    /// A place moves one Status first or right after another, and later places apply to the order earlier ones left.
    /// </summary>
    [Fact]
    public void A_place_moves_one_Status_first_or_after_another_in_the_deltas_order()
    {
        var parked = Owned("Parked");
        var set = EffectiveStatusSet.Of(
            Defaults,
            Delta(
                [parked],
                Operation(parked.Id, StatusDeltaKind.Add),
                Operation(parked.Id, StatusDeltaKind.Place, afterStatusId: null),
                Operation(_third.Id, StatusDeltaKind.Place, afterStatusId: parked.Id)));

        Assert.Equal([parked.Id, _third.Id, _first.Id, _second.Id], set.Select(status => status.Id));
    }

    /// <summary>
    /// The delta applies in <see cref="StatusDeltaOperation.Sequence"/> order, whatever order it was read in.
    /// </summary>
    [Fact]
    public void Operations_apply_in_sequence_order_not_read_order()
    {
        var placeFirst = Operation(_third.Id, StatusDeltaKind.Place, afterStatusId: null);
        var placeAfter = Operation(_first.Id, StatusDeltaKind.Place, afterStatusId: null);

        var set = EffectiveStatusSet.Of(Defaults, new ProjectStatusDelta([], [placeAfter, placeFirst]));

        Assert.Equal([_first.Id, _third.Id, _second.Id], set.Select(status => status.Id));
    }

    /// <summary>
    /// A place whose anchor the set does not hold is ignored, and the Status keeps its slot.
    /// </summary>
    [Fact]
    public void A_place_whose_anchor_is_not_in_the_set_leaves_the_Status_where_it_was()
    {
        var set = EffectiveStatusSet.Of(Defaults, Delta([], Operation(_first.Id, StatusDeltaKind.Place, afterStatusId: Guid.NewGuid())));

        Assert.Equal([_first.Id, _second.Id, _third.Id], set.Select(status => status.Id));
    }

    /// <summary>
    /// Two Projects' deltas over one set of defaults give two sets, and neither reaches the other's or the defaults.
    /// </summary>
    [Fact]
    public void Two_Projects_deltas_over_one_set_of_defaults_give_two_sets()
    {
        var parked = Owned("Parked");
        var defaults = Defaults;

        var one = EffectiveStatusSet.Of(defaults, Delta([parked], Operation(parked.Id, StatusDeltaKind.Add)));
        var other = EffectiveStatusSet.Of(defaults, Delta([], Operation(_second.Id, StatusDeltaKind.Rename, name: "Underway")));

        Assert.True(EffectiveStatusSet.Contains(one, parked.Id));
        Assert.False(EffectiveStatusSet.Contains(other, parked.Id));
        Assert.Equal("Doing", one[1].Name);
        Assert.Equal("Underway", other[1].Name);
        Assert.Equal("Doing", _second.Name);
    }

    /// <summary>
    /// A write that would leave the set empty is refused. The product has no removal yet, so this is the guard's only
    /// reachable proof: an empty set of defaults with an empty delta.
    /// </summary>
    [Fact]
    public void A_write_that_would_leave_the_set_empty_is_refused()
    {
        Assert.Equal(StatusDeltaVerdict.SetEmpty, EffectiveStatusSet.Check(EffectiveStatusSet.Of([], ProjectStatusDelta.Empty), namedStatusId: null));
        Assert.Equal(StatusDeltaVerdict.Admitted, EffectiveStatusSet.Check(EffectiveStatusSet.Of(Defaults), namedStatusId: null));
    }

    /// <summary>
    /// The name a write sets must be no other Status's in the set, compared case-insensitively; the Status's own name, in
    /// any case, is admitted.
    /// </summary>
    [Fact]
    public void A_name_another_Status_holds_in_any_case_is_taken_and_its_own_is_admitted()
    {
        var taken = EffectiveStatusSet.Of(Defaults, Delta([], Operation(_third.Id, StatusDeltaKind.Rename, name: "QUEUED")));
        var own = EffectiveStatusSet.Of(Defaults, Delta([], Operation(_first.Id, StatusDeltaKind.Rename, name: "QUEUED")));

        Assert.Equal(StatusDeltaVerdict.NameTaken, EffectiveStatusSet.Check(taken, _third.Id));
        Assert.Equal(StatusDeltaVerdict.Admitted, EffectiveStatusSet.Check(own, _first.Id));
    }

    /// <summary>
    /// A write replaces the operation of the same Status and kind and goes last; the next sequence follows the highest.
    /// </summary>
    [Fact]
    public void A_write_replaces_the_operation_of_the_same_Status_and_kind()
    {
        var delta = Delta([], Operation(_first.Id, StatusDeltaKind.Rename, name: "Intake"), Operation(_second.Id, StatusDeltaKind.Rename, name: "Underway"));
        var rewritten = delta.With(null, [Operation(_first.Id, StatusDeltaKind.Rename, name: "Inbox")]);

        Assert.Equal(3, delta.NextSequence);
        Assert.Equal(["Underway", "Inbox"], rewritten.Operations.Select(operation => operation.Name));
        Assert.Equal("Inbox", EffectiveStatusSet.Of(Defaults, rewritten)[0].Name);
        Assert.Equal(1, ProjectStatusDelta.Empty.NextSequence);
    }

    /// <summary>
    /// A name is trimmed and is 1 to 100 characters.
    /// </summary>
    /// <param name="submitted">What was sent.</param>
    /// <param name="admitted">Whether it is a name.</param>
    [Theory]
    [InlineData("  Parked  ", true)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void A_name_is_trimmed_and_one_to_a_hundred_characters(string? submitted, bool admitted)
    {
        Assert.Equal(admitted, StatusName.TryNormalise(submitted, out var name));
        Assert.Equal(admitted ? "Parked" : string.Empty, name);
        Assert.True(StatusName.TryNormalise(new string('n', 100), out _));
        Assert.False(StatusName.TryNormalise(new string('n', 101), out _));
    }

    private StatusDefinition[] Defaults => [_first, _second, _third];

    private static StatusDefinition Default(int position, string name) => new()
    {
        Id = Guid.NewGuid(),
        SpaceId = SpaceId,
        Name = name,
        Position = position,
        CreatedAt = DateTimeOffset.UnixEpoch,
    };

    private static StatusDefinition Owned(string name) => new()
    {
        Id = Guid.NewGuid(),
        SpaceId = SpaceId,
        OwnerProjectId = ProjectId,
        Name = name,
        Position = 0,
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
