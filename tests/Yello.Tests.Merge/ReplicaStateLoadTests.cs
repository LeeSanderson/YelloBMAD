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
/// identities (origin and sequence), then the held changes; every varint here is one byte. Each case anchors the offset it
/// edits, so a layout change fails on the anchor rather than passing on the wrong byte.
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

    [Fact]
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

    [Fact]
    public void A_state_that_withdrew_a_change_it_still_holds_is_refused()
    {
        var strategy = Candidate.Strategy;
        var replica = strategy.CreateReplica(Owner);

        replica.Insert(0, "a");

        var valid = replica.EncodeState().ToArray();

        Assert.Equal(0, valid[WithdrawnCountOffset]);

        byte[] forged = [.. valid[..WithdrawnCountOffset], 1, .. Owner.Value.ToByteArray(), 1, .. valid[(WithdrawnCountOffset + 1)..]];

        Assert.Equal(ChangeAdmission.Malformed, strategy.LoadReplica(Owner, forged).Admission);
    }

    [Fact]
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
