namespace Yello.Merge.Sequence;

/// <summary>
/// A replica as bytes: format byte, the identity it belonged to, its last sequence number and clock, the sequence numbers
/// it withdrew, then every change it holds. Loading replays the changes, which rebuilds the same text and "has".
/// </summary>
/// <remarks>
/// The counters are restored only when the replica is loaded under the identity it was saved from. A replica rebuilt under
/// a new identity has issued nothing under that identity yet.
/// </remarks>
internal static class ReplicaState
{
    internal static byte[] Encode(SequenceReplica replica)
    {
        var writer = new ByteWriter()
            .Byte(Format.Current)
            .Guid(replica.Id.Value)
            .Varint(replica.LastSequence)
            .Varint(replica.Clock)
            .Varint(replica.Withdrawn.Count);

        foreach (var sequence in replica.Withdrawn.Order())
        {
            writer.Varint(sequence);
        }

        var held = replica.Held.ToList();

        writer.Varint(held.Count);

        foreach (var change in held)
        {
            writer.LengthPrefixed(change.Encoded.Span);
        }

        return writer.ToArray();
    }

    internal static Decoded<ITextReplica> Load(ReplicaId id, ReadOnlySpan<byte> state)
    {
        var format = Format.Check(state);

        if (format != ChangeAdmission.Admitted)
        {
            return new Decoded<ITextReplica>(format, null);
        }

        var reader = new ByteReader(state[1..]);
        var owner = new ReplicaId(reader.Guid());
        var lastSequence = reader.Varint();
        var clock = reader.Varint();
        var withdrawn = new List<long>();

        for (var count = reader.Count(); withdrawn.Count < count && !reader.Failed;)
        {
            withdrawn.Add(reader.Positive());
        }

        var changes = new List<TextChange>();

        for (var count = reader.Count(); changes.Count < count && !reader.Failed;)
        {
            var encoded = reader.LengthPrefixed();
            var (admission, body) = ChangeCodec.Decode(encoded);

            if (admission != ChangeAdmission.Admitted)
            {
                return new Decoded<ITextReplica>(reader.Failed ? ChangeAdmission.Malformed : admission, null);
            }

            changes.Add(new TextChange(body!.Id, encoded));
        }

        if (!reader.IsComplete)
        {
            return new Decoded<ITextReplica>(ChangeAdmission.Malformed, null);
        }

        var replica = new SequenceReplica(id);

        if (owner == id)
        {
            replica.Resume(lastSequence, clock, withdrawn);
        }
        else
        {
            replica.Resume(0, clock, []);
        }

        return Replay(replica, changes);
    }

    private static Decoded<ITextReplica> Replay(SequenceReplica replica, List<TextChange> changes)
    {

        foreach (var change in changes)
        {
            var admission = replica.Apply(change);

            if (admission != ChangeAdmission.Admitted)
            {
                return new Decoded<ITextReplica>(admission, null);
            }
        }

        return new Decoded<ITextReplica>(ChangeAdmission.Admitted, replica);
    }
}
