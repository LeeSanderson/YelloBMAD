using Xunit;
using Yello.Merge;

namespace Yello.Tests.Merge;

/// <summary>
/// Saved state whose counters contradict the changes it holds is refused on load, and state in the format before the
/// current one is refused as unsupported: no state is persisted before this check exists, so the format can still change.
/// </summary>
/// <remarks>
/// <para>
/// The forged states edit the current candidate's byte layout, so these cases belong to that candidate, as
/// <see cref="HostileChangeTests"/> do: a replacement rewrites them, while the conformance suite stays as it is. The port
/// alone cannot produce a contradicting state, because applying an own change raises the saved counter with it.
/// </para>
/// <para>
/// Layout read here: format byte, the saving identity (16 bytes), the last sequence, the clock, the withdrawn count and
/// sequences, then the held changes; every varint here is one byte. Each case anchors the offset it edits, so a layout
/// change fails on the anchor rather than passing on the wrong byte. A format change that keys the withdrawn set by change
/// identity rewrites the second forgery and its anchor.
/// </para>
/// </remarks>
[Trait("Suite", "Merge")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-18")]
[Trait("Requirement", "AD-12")]
[Trait("Requirement", "AD-14")]
public sealed class ReplicaStateLoadTests
{
    private const int LastSequenceOffset = 17;
    private const int WithdrawnCountOffset = 19;

    private static readonly ReplicaId Owner = Network.IdOf(0);

    [Fact(Skip = "red: AC10, Q8 - load restores a saved last sequence below a held own change instead of refusing it")]
    public void A_state_whose_last_sequence_is_below_one_of_its_own_held_changes_is_refused()
    {
        var strategy = Candidate.Strategy;
        var replica = strategy.CreateReplica(Owner);

        replica.Insert(0, "a");
        replica.Insert(1, "b");

        var forged = replica.EncodeState().ToArray();

        Assert.Equal(2, forged[LastSequenceOffset]);

        forged[LastSequenceOffset] = 1;

        Assert.Equal(ChangeAdmission.Malformed, strategy.LoadReplica(Owner, forged).Admission);
    }

    [Fact(Skip = "red: AC10, Q8 - load accepts a withdrawn sequence that is also held")]
    public void A_state_that_withdrew_a_change_it_still_holds_is_refused()
    {
        var strategy = Candidate.Strategy;
        var replica = strategy.CreateReplica(Owner);

        replica.Insert(0, "a");

        var valid = replica.EncodeState().ToArray();

        Assert.Equal(0, valid[WithdrawnCountOffset]);

        byte[] forged = [.. valid[..WithdrawnCountOffset], 1, 1, .. valid[(WithdrawnCountOffset + 1)..]];

        Assert.Equal(ChangeAdmission.Malformed, strategy.LoadReplica(Owner, forged).Admission);
    }

    [Fact(Skip = "red: AC10, Q8 - the state format is not bumped, so the format before the current one reads as malformed")]
    public void State_in_the_format_before_the_current_one_is_refused_as_unsupported()
    {
        var strategy = Candidate.Strategy;
        var replica = strategy.CreateReplica(Owner);

        replica.Insert(0, "a");

        var previous = replica.EncodeState().ToArray();

        previous[0] = byte.CreateTruncating(previous[0] - 1);

        Assert.Equal(ChangeAdmission.UnsupportedVersion, strategy.LoadReplica(Owner, previous).Admission);
    }
}
