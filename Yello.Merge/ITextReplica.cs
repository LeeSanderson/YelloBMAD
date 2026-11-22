namespace Yello.Merge;

/// <summary>
/// One participant's copy of one document's text. Not thread-safe: a replica belongs to one editing session or one
/// server-side rebuild at a time.
/// </summary>
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
    /// <exception cref="ArgumentException">The text is empty or holds an unpaired surrogate.</exception>
    /// <exception cref="InvalidOperationException">The document can take no more characters.</exception>
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
    /// <exception cref="ArgumentException">
    /// The version cannot be read. Bytes from another replica go through <see cref="ITextMergeStrategy.DecodeVersion"/>
    /// first, which refuses them as a value instead.
    /// </exception>
    IReadOnlyList<TextChange> ChangesMissingFrom(ReplicaVersion version);

    /// <summary>
    /// The whole replica as bytes, for <see cref="ITextMergeStrategy.LoadReplica"/>.
    /// </summary>
    ReadOnlyMemory<byte> EncodeState();

    /// <summary>
    /// Takes back a local change the server refused, as though it had never been made. Its identity is never issued
    /// again. Withdrawing a change this replica does not hold does nothing.
    /// </summary>
    /// <exception cref="ArgumentException">The change is another replica's.</exception>
    /// <exception cref="InvalidOperationException">
    /// A later change builds on this one's text; withdraw the later changes first, the latest first.
    /// </exception>
    void Withdraw(ChangeId change);
}
