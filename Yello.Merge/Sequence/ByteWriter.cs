using System.Buffers;

namespace Yello.Merge.Sequence;

/// <summary>
/// Writes the merge's encodings: single bytes, unsigned LEB128 varints, GUIDs and length-prefixed byte strings.
/// </summary>
internal sealed class ByteWriter
{
    internal const int GuidLength = 16;
    internal const int VarintPayloadBits = 7;
    internal const byte VarintContinues = 0x80;

    private readonly ArrayBufferWriter<byte> _buffer = new();

    internal ByteWriter Byte(byte value)
    {
        _buffer.GetSpan(1)[0] = value;
        _buffer.Advance(1);

        return this;
    }

    internal ByteWriter Varint(long value)
    {
        var remaining = ulong.CreateChecked(value);

        while (remaining >= VarintContinues)
        {
            Byte(byte.CreateTruncating(remaining | VarintContinues));
            remaining >>= VarintPayloadBits;
        }

        return Byte(byte.CreateTruncating(remaining));
    }

    internal ByteWriter Guid(Guid value)
    {
        value.TryWriteBytes(_buffer.GetSpan(GuidLength));
        _buffer.Advance(GuidLength);

        return this;
    }

    internal ByteWriter LengthPrefixed(ReadOnlySpan<byte> value)
    {
        Varint(value.Length);
        _buffer.Write(value);

        return this;
    }

    internal byte[] ToArray() => _buffer.WrittenSpan.ToArray();
}
