using Yello.Domain.Tasks;
using Yello.Merge;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// The merge half of the description store: the server replica rebuilt from stored rows, and a batch admitted to it.
/// </summary>
/// <remarks>
/// <b>One identity per Task</b>, the Task's id, never <see cref="Guid.Empty"/>: the server replica never makes a local
/// change, so a fixed identity reloads its own snapshot with its counters.
/// </remarks>
internal static class DescriptionReplica
{
    /// <summary>
    /// The replica the latest snapshot and the change rows after it rebuild to.
    /// </summary>
    /// <param name="merge">The merge port.</param>
    /// <param name="taskId">The Task, whose id is the replica's identity.</param>
    /// <param name="snapshot">The latest snapshot's payload, or <c>null</c> when there is none.</param>
    /// <param name="changes">The change rows after it, in sequence order.</param>
    /// <returns>The replica.</returns>
    /// <exception cref="InvalidOperationException">A stored row no longer reads or applies: the log is corrupt.</exception>
    internal static ITextReplica Rebuild(ITextMergeStrategy merge, Guid taskId, byte[]? snapshot, IEnumerable<byte[]> changes)
    {
        var identity = new ReplicaId(taskId);
        var replica = snapshot is null ? merge.CreateReplica(identity) : Stored(merge.LoadReplica(identity, snapshot));

        foreach (var payload in changes)
        {
            if (replica.Apply(Stored(merge.DecodeChange(payload))) != ChangeAdmission.Admitted)
            {
                throw new InvalidOperationException("A stored description change no longer applies to the rebuilt description.");
            }
        }

        return replica;
    }

    /// <summary>
    /// Applies each change of the batch on its own: one that does not decode, or that the replica refuses, is refused by its
    /// batch position and changes nothing.
    /// </summary>
    /// <param name="merge">The merge port.</param>
    /// <param name="replica">The rebuilt server replica.</param>
    /// <param name="batch">The batch.</param>
    /// <returns>
    /// The refused positions, and the payloads to append: each change the replica did not hold before the batch, once, in
    /// batch order. A change already held - a resend, a replayed key, a repeat in the batch - is admitted and appends nothing.
    /// </returns>
    internal static (List<int> Refused, List<byte[]> Appended) Admit(ITextMergeStrategy merge, ITextReplica replica, DescriptionBatch batch)
    {
        var before = replica.Version;
        var refused = new List<int>();
        var admitted = new List<TextChange>();

        for (var position = 0; position < batch.Changes.Count; position++)
        {
            if (merge.DecodeChange(batch.Changes[position].Span) is { Admission: ChangeAdmission.Admitted, Value: { } change }
                && replica.Apply(change) == ChangeAdmission.Admitted)
            {
                admitted.Add(change);
            }
            else
            {
                refused.Add(position);
            }
        }

        var fresh = replica.ChangesMissingFrom(before).Select(change => change.Id).ToHashSet();

        return (refused, [.. admitted.Where(change => fresh.Remove(change.Id)).Select(change => change.Encoded.ToArray())]);
    }

    /// <summary>
    /// The client's version, or <c>null</c> when the merge cannot read it.
    /// </summary>
    internal static ReplicaVersion? VersionOf(ITextMergeStrategy merge, ReadOnlyMemory<byte> encoded) =>
        merge.DecodeVersion(encoded.Span) is { Admission: ChangeAdmission.Admitted, Value: { } version } ? version : null;

    private static T Stored<T>(Decoded<T> decoded)
        where T : class =>
        decoded is { Admission: ChangeAdmission.Admitted, Value: { } value }
            ? value
            : throw new InvalidOperationException($"A stored description row could not be read ({decoded.Admission}).");
}
