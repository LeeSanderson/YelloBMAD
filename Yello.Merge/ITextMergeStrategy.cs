namespace Yello.Merge;

/// <summary>
/// The one merge port. It creates and loads replicas, and decodes the bytes replicas exchange. It holds no document state,
/// so one instance serves every document on any thread.
/// </summary>
/// <remarks>
/// No member names an algorithm, so a different merge can sit behind it without a caller changing. Indexes are UTF-16 code
/// units, as .NET strings and the browser's <c>selectionStart</c> count them; an index that would split a surrogate pair
/// is refused.
/// </remarks>
public interface ITextMergeStrategy
{
    /// <summary>
    /// Creates an empty replica. The caller owns the identity and must never reuse it for a fresh replica: a replica that
    /// resumes is loaded from its encoded state, never recreated.
    /// </summary>
    ITextReplica CreateReplica(ReplicaId id);

    /// <summary>
    /// Loads a replica from <see cref="ITextReplica.EncodeState"/>'s output, under the identity it is to continue as.
    /// </summary>
    Decoded<ITextReplica> LoadReplica(ReplicaId id, ReadOnlySpan<byte> state);

    /// <summary>
    /// Reads a change from <see cref="TextChange.Encoded"/>.
    /// </summary>
    Decoded<TextChange> DecodeChange(ReadOnlySpan<byte> encoded);

    /// <summary>
    /// Reads a "has" summary from <see cref="ReplicaVersion.Encoded"/>.
    /// </summary>
    Decoded<ReplicaVersion> DecodeVersion(ReadOnlySpan<byte> encoded);
}
