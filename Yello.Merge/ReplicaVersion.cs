using System.Diagnostics.CodeAnalysis;

namespace Yello.Merge;

/// <summary>
/// Which changes a replica has, as opaque bytes. Two replicas that hold the same changes produce equal versions, so the
/// encoding must be canonical.
/// </summary>
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
