// Declared in the production namespace on purpose, so moving the file into Yello.Merge changes nothing that uses it.
#pragma warning disable IDE0130
namespace Yello.Merge;
#pragma warning restore IDE0130

/// <summary>
/// The one merge port. It creates and loads replicas, and decodes the bytes replicas exchange. It holds no document state,
/// so one instance serves every document on any thread.
/// </summary>
/// <remarks>
/// <para>
/// <b>A stand-in, not the port.</b> This file and its siblings under <c>PortStandIn/</c> declare the port's intended
/// shape inside the test project, in the production namespace, so the conformance suite compiles before any port exists in
/// <c>Yello.Merge</c>. The dev phase moves these declarations into <c>Yello.Merge</c> unchanged and deletes this folder:
/// a type declared in both places is CS0436, which the build treats as an error, so the two cannot coexist.
/// </para>
/// <para>
/// No member names an algorithm. Indexes are UTF-16 code units, as .NET strings and the browser's <c>selectionStart</c>
/// count them; an index that would split a surrogate pair is refused.
/// </para>
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
