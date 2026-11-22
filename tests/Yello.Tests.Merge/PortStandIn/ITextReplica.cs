// Declared in the production namespace on purpose, so moving the file into Yello.Merge changes nothing that uses it.
#pragma warning disable IDE0130
namespace Yello.Merge;
#pragma warning restore IDE0130

/// <summary>
/// One participant's copy of one document's text. Not thread-safe: a replica belongs to one editing session or one
/// server-side rebuild at a time.
/// </summary>
/// <remarks>
/// A stand-in declared in the test project; see <see cref="ITextMergeStrategy"/>'s remarks.
/// </remarks>
public interface ITextReplica
{
    ReplicaId Id { get; }

    /// <summary>
    /// The current plain text.
    /// </summary>
    string Text { get; }

    /// <summary>
    /// Which changes this replica has, compact enough to send on reconnection.
    /// </summary>
    ReplicaVersion Version { get; }

    /// <summary>
    /// Inserts <paramref name="text"/> at a UTF-16 index and returns the change to send to the other replicas.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the text or splits a surrogate pair.</exception>
    TextChange Insert(int index, string text);

    /// <summary>
    /// Deletes a UTF-16 range and returns the change to send to the other replicas.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The range is outside the text or splits a surrogate pair.</exception>
    TextChange Delete(int index, int length);

    /// <summary>
    /// Applies another replica's change. Applying a change already held changes nothing, and a change that arrives before
    /// a change it depends on is held until that change arrives. A refused change leaves the replica as it was.
    /// </summary>
    ChangeAdmission Apply(TextChange change);

    /// <summary>
    /// The changes this replica holds that a replica at <paramref name="version"/> lacks: the reconnection exchange.
    /// </summary>
    IReadOnlyList<TextChange> ChangesMissingFrom(ReplicaVersion version);

    /// <summary>
    /// The whole replica as bytes, for <see cref="ITextMergeStrategy.LoadReplica"/>.
    /// </summary>
    ReadOnlyMemory<byte> EncodeState();

    /// <summary>
    /// Takes back a local change the server refused, as though it had never been made. Its identity is never issued
    /// again.
    /// </summary>
    void Withdraw(ChangeId change);
}
