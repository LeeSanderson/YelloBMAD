using System.Diagnostics.CodeAnalysis;

// Declared in the production namespace on purpose, so moving the file into Yello.Merge changes nothing that uses it.
#pragma warning disable IDE0130
namespace Yello.Merge;
#pragma warning restore IDE0130

/// <summary>
/// Which changes a replica has, as opaque bytes. Two replicas that hold the same changes produce equal versions, so the
/// encoding must be canonical.
/// </summary>
/// <remarks>
/// A stand-in declared in the test project; see <see cref="ITextMergeStrategy"/>'s remarks.
/// </remarks>
public sealed class ReplicaVersion : IEquatable<ReplicaVersion>
{
    private readonly byte[] _encoded;

    public ReplicaVersion(ReadOnlySpan<byte> encoded) => _encoded = encoded.ToArray();

    public ReadOnlyMemory<byte> Encoded => _encoded;

    public bool Equals([NotNullWhen(true)] ReplicaVersion? other) => other is not null && _encoded.AsSpan().SequenceEqual(other._encoded);

    public override bool Equals([NotNullWhen(true)] object? obj) => Equals(obj as ReplicaVersion);

    public override int GetHashCode()
    {
        var hash = default(HashCode);

        hash.AddBytes(_encoded);

        return hash.ToHashCode();
    }
}
