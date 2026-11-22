namespace Yello.Merge.Sequence;

/// <summary>
/// The merge behind <see cref="ITextMergeStrategy"/>: a plain-text sequence merge in which every character keeps a unique
/// identity. Stateless, so one instance serves every document on any thread.
/// </summary>
internal sealed class SequenceMergeStrategy : ITextMergeStrategy
{
    public ITextReplica CreateReplica(ReplicaId id) => new SequenceReplica(id);

    public Decoded<ITextReplica> LoadReplica(ReplicaId id, ReadOnlySpan<byte> state) => ReplicaState.Load(id, state);

    public Decoded<TextChange> DecodeChange(ReadOnlySpan<byte> encoded)
    {
        var (admission, body) = ChangeCodec.Decode(encoded);

        return admission == ChangeAdmission.Admitted
            ? new Decoded<TextChange>(admission, new TextChange(body!.Id, encoded))
            : new Decoded<TextChange>(admission, null);
    }

    public Decoded<ReplicaVersion> DecodeVersion(ReadOnlySpan<byte> encoded)
    {
        var (admission, _) = HeldChanges.Read(encoded);

        return admission == ChangeAdmission.Admitted
            ? new Decoded<ReplicaVersion>(admission, new ReplicaVersion(encoded))
            : new Decoded<ReplicaVersion>(admission, null);
    }
}
