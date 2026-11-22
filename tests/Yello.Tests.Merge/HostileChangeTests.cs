using Xunit;
using Yello.Merge;

namespace Yello.Tests.Merge;

/// <summary>
/// Well-formed but hostile changes, as a client that reused its identity or forged a frame would send them: each is
/// refused as a value, and no replica is left corrupted or diverged.
/// </summary>
/// <remarks>
/// The forged frames edit the current candidate's byte layout, so these cases belong to that candidate: a replacement
/// rewrites them, while the conformance suite stays as it is.
/// </remarks>
[Trait("Suite", "Merge")]
[Trait("Priority", "P1")]
[Trait("Requirement", "AR-18")]
[Trait("Requirement", "AD-11")]
[Trait("Requirement", "AD-12")]
public sealed class HostileChangeTests
{
    // Offsets in an insert that follows a character: format, kind, origin (16), sequence, anchor tag, anchor origin (16),
    // anchor clock, then the insert's own clock. Every varint here is one byte.
    private const int SequenceOffset = 18;
    private const int AnchorClockOffset = 36;
    private const int ClockOffset = 37;

    private static readonly ReplicaId Author = Network.IdOf(0);
    private static readonly ReplicaId Other = Network.IdOf(1);
    private static readonly ReplicaId Observer = Network.IdOf(2);

    [Fact]
    public void A_change_reusing_a_held_identity_with_different_text_is_refused()
    {
        var strategy = Candidate.Strategy;
        var original = strategy.CreateReplica(Author).Insert(0, "a");
        var reused = strategy.CreateReplica(Author).Insert(0, "x");
        var observer = strategy.CreateReplica(Observer);

        Assert.Equal(original.Id, reused.Id);
        Assert.Equal(ChangeAdmission.Admitted, observer.Apply(original));
        Assert.Equal(ChangeAdmission.Malformed, observer.Apply(reused));
        Assert.Equal("a", observer.Text);
    }

    [Fact]
    public void A_change_whose_characters_collide_with_a_waiting_change_is_refused_and_the_waiting_change_still_applies()
    {
        var strategy = Candidate.Strategy;
        var first = strategy.CreateReplica(Author);
        var anchor = first.Insert(0, "a");
        var waiting = first.Insert(1, "bc");

        // The same identity started again from nothing, so its third change reuses a character of the second above.
        var restarted = strategy.CreateReplica(Author);

        restarted.Insert(0, "x");
        restarted.Insert(0, "y");

        var colliding = restarted.Insert(0, "z");
        var observer = strategy.CreateReplica(Observer);

        Assert.Equal(ChangeAdmission.Admitted, observer.Apply(waiting));
        Assert.Equal(ChangeAdmission.Malformed, observer.Apply(colliding));
        Assert.Equal(ChangeAdmission.Admitted, observer.Apply(anchor));
        Assert.Equal("abc", observer.Text);
    }

    [Fact]
    public void An_insert_not_later_than_the_character_it_follows_is_refused()
    {
        var (strategy, valid) = InsertAfterAnotherReplicasCharacter();
        var forged = valid.ToArray();

        forged[ClockOffset] = forged[AnchorClockOffset];

        Assert.Equal(ChangeAdmission.Malformed, strategy.DecodeChange(forged).Admission);
    }

    [Fact]
    public void A_number_padded_with_an_extra_byte_is_refused()
    {
        var (strategy, valid) = InsertAfterAnotherReplicasCharacter();
        byte[] padded = [.. valid[..SequenceOffset], byte.CreateTruncating(valid[SequenceOffset] | 0x80), 0x00, .. valid[(SequenceOffset + 1)..]];

        Assert.Equal(ChangeAdmission.Admitted, strategy.DecodeChange(valid).Admission);
        Assert.Equal(ChangeAdmission.Malformed, strategy.DecodeChange(padded).Admission);
    }

    [Fact]
    public void A_received_clock_at_the_limit_stops_local_inserts_rather_than_wrapping()
    {
        var (strategy, valid) = InsertAfterAnotherReplicasCharacter();
        byte[] forged = [.. valid[..ClockOffset], .. Varint(long.MaxValue - 1), .. valid[(ClockOffset + 1)..]];
        var receiver = strategy.CreateReplica(Observer);

        Assert.Equal(ChangeAdmission.Admitted, receiver.Apply(Port.Admitted(strategy.DecodeChange(FirstChange(strategy)))));
        Assert.Equal(ChangeAdmission.Admitted, receiver.Apply(Port.Admitted(strategy.DecodeChange(forged))));
        Assert.Throws<InvalidOperationException>(() => receiver.Insert(0, "ab"));
    }

    private static byte[] FirstChange(ITextMergeStrategy strategy) =>
        strategy.CreateReplica(Other).Insert(0, "q").Encoded.ToArray();

    /// <summary>
    /// One character typed by <see cref="Author"/> after a character typed by <see cref="Other"/>, so its anchor is not
    /// one of its own characters.
    /// </summary>
    private static (ITextMergeStrategy Strategy, byte[] Encoded) InsertAfterAnotherReplicasCharacter()
    {
        var strategy = Candidate.Strategy;
        var author = strategy.CreateReplica(Author);

        Assert.Equal(ChangeAdmission.Admitted, author.Apply(Port.Admitted(strategy.DecodeChange(FirstChange(strategy)))));

        var encoded = author.Insert(1, "z").Encoded.ToArray();

        Assert.Equal(1, encoded[AnchorClockOffset]);
        Assert.Equal(2, encoded[ClockOffset]);

        return (strategy, encoded);
    }

    private static byte[] Varint(long value)
    {
        var bytes = new List<byte>();
        var remaining = ulong.CreateChecked(value);

        while (remaining >= 0x80)
        {
            bytes.Add(byte.CreateTruncating(remaining | 0x80));
            remaining >>= 7;
        }

        bytes.Add(byte.CreateTruncating(remaining));

        return [.. bytes];
    }
}
