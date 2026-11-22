namespace Yello.Merge;

/// <summary>
/// A change's identity: the replica that made it and its place in that replica's own sequence. It survives encoding,
/// decoding and resending, which is what makes a change applied twice count once.
/// </summary>
public readonly record struct ChangeId(ReplicaId Origin, long Sequence);
