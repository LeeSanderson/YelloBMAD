namespace Yello.Merge.Sequence;

/// <summary>
/// A replica as bytes: the state format byte, the identity it belonged to, its last sequence number and clock, the
/// identities of the changes it withdrew, then every change it holds. Loading replays the changes, which rebuilds the same
/// text and "has".
/// </summary>
/// <remarks>
/// <para>
/// The counters are restored only when the replica is loaded under the identity it was saved from. A replica rebuilt under
/// a new identity has issued nothing under that identity yet. The withdrawn set is restored under either identity, because
/// the server may still redeliver a change the old identity took back.
/// </para>
/// <para>
/// State is persisted, so it is checked before it is trusted: counters that contradict the changes held are refused.
/// </para>
/// </remarks>
internal static class ReplicaState
{
    internal static byte[] Encode(SequenceReplica replica)
    {
        var writer = new ByteWriter()
            .Byte(Format.State)
            .Guid(replica.Id.Value)
            .Varint(replica.LastSequence)
            .Varint(replica.Clock)
            .Varint(replica.Withdrawn.Count);

        foreach (var change in replica.Withdrawn.OrderBy(change => change.Origin.Value).ThenBy(change => change.Sequence))
        {
            writer.Guid(change.Origin.Value).Varint(change.Sequence);
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
        var format = Format.CheckState(state);

        if (format != ChangeAdmission.Admitted)
        {
            return new Decoded<ITextReplica>(format, null);
        }

        var reader = new ByteReader(state[1..]);
        var owner = new ReplicaId(reader.Guid());
        var lastSequence = reader.Varint();
        var clock = reader.Varint();
        var withdrawn = ReadWithdrawn(ref reader);
        var (admission, changes) = ReadChanges(ref reader);

        if (admission != ChangeAdmission.Admitted)
        {
            return new Decoded<ITextReplica>(admission, null);
        }

        if (!reader.IsComplete || Contradicts(owner, lastSequence, withdrawn, changes))
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
            replica.Resume(0, clock, withdrawn);
        }

        return Replay(replica, changes);
    }

    private static HashSet<ChangeId> ReadWithdrawn(ref ByteReader reader)
    {
        var withdrawn = new HashSet<ChangeId>();

        for (var count = reader.Count(); withdrawn.Count < count && !reader.Failed;)
        {
            // A repeated identity is not canonical state; failing the reader refuses the whole state.
            if (!withdrawn.Add(new ChangeId(new ReplicaId(reader.Guid()), reader.Positive())))
            {
                reader.MarkFailed();
            }
        }

        return withdrawn;
    }

    private static (ChangeAdmission Admission, List<TextChange> Changes) ReadChanges(ref ByteReader reader)
    {
        var changes = new List<TextChange>();

        for (var count = reader.Count(); changes.Count < count && !reader.Failed;)
        {
            var encoded = reader.LengthPrefixed();
            var (admission, body) = ChangeCodec.Decode(encoded);

            if (admission != ChangeAdmission.Admitted)
            {
                return (reader.Failed ? ChangeAdmission.Malformed : admission, changes);
            }

            changes.Add(new TextChange(body!.Id, encoded));
        }

        return (ChangeAdmission.Admitted, changes);
    }

    /// <summary>
    /// Whether the saved counters disagree with what is held: an own change, held or withdrawn, numbered above the last
    /// sequence issued (resuming would issue that number again), or a change both withdrawn and held.
    /// </summary>
    private static bool Contradicts(ReplicaId owner, long lastSequence, HashSet<ChangeId> withdrawn, List<TextChange> changes) =>
        withdrawn.Any(change => change.Origin == owner && change.Sequence > lastSequence)
        || changes.Exists(change => withdrawn.Contains(change.Id) || (change.Id.Origin == owner && change.Id.Sequence > lastSequence));

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
