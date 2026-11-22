using System.Diagnostics.CodeAnalysis;

// Declared in the production namespace on purpose, so moving the file into Yello.Merge changes nothing that uses it.
#pragma warning disable IDE0130
namespace Yello.Merge;
#pragma warning restore IDE0130

/// <summary>
/// One change, as the opaque bytes that travel and are stored, with its identity read out.
/// </summary>
/// <remarks>
/// <para>
/// Every encoding the port produces starts with one format byte. A byte above the highest format a build knows is
/// <see cref="ChangeAdmission.UnsupportedVersion"/>; that is the one thing about the bytes a caller may rely on.
/// </para>
/// <para>A stand-in declared in the test project; see <see cref="ITextMergeStrategy"/>'s remarks.</para>
/// </remarks>
public sealed class TextChange : IEquatable<TextChange>
{
    private readonly byte[] _encoded;

    public TextChange(ChangeId id, ReadOnlySpan<byte> encoded)
    {
        Id = id;
        _encoded = encoded.ToArray();
    }

    public ChangeId Id { get; }

    public ReadOnlyMemory<byte> Encoded => _encoded;

    public bool Equals([NotNullWhen(true)] TextChange? other) =>
        other is not null && Id == other.Id && _encoded.AsSpan().SequenceEqual(other._encoded);

    public override bool Equals([NotNullWhen(true)] object? obj) => Equals(obj as TextChange);

    public override int GetHashCode() => Id.GetHashCode();
}
