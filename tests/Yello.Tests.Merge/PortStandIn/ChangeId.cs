// Declared in the production namespace on purpose, so moving the file into Yello.Merge changes nothing that uses it.
#pragma warning disable IDE0130
namespace Yello.Merge;
#pragma warning restore IDE0130

/// <summary>
/// A change's identity: the replica that made it and its place in that replica's own sequence. It survives encoding,
/// decoding and resending, which is what makes a change applied twice count once.
/// </summary>
/// <remarks>
/// A stand-in declared in the test project; see <see cref="ITextMergeStrategy"/>'s remarks.
/// </remarks>
public readonly record struct ChangeId(ReplicaId Origin, long Sequence);
