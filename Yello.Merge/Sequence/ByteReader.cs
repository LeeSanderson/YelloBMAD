namespace Yello.Merge.Sequence;

/// <summary>
/// Reads what <see cref="ByteWriter"/> wrote. A read past the end or an out-of-range value marks the reader failed and
/// returns a default; callers check <see cref="IsComplete"/> once at the end instead of after every read.
/// </summary>
internal ref struct ByteReader(ReadOnlySpan<byte> encoded)
{
    private const int MaxVarintBytes = 9;
    private const byte VarintPayload = 0x7F;

    private ReadOnlySpan<byte> _remaining = encoded;

    internal bool Failed { get; private set; }

    /// <summary>
    /// Whether every read succeeded and nothing is left over.
    /// </summary>
    internal readonly bool IsComplete => !Failed && _remaining.IsEmpty;

    internal byte Byte()
    {
        if (_remaining.IsEmpty)
        {
            MarkFailed();

            return 0;
        }

        var value = _remaining[0];

        _remaining = _remaining[1..];

        return value;
    }

    /// <summary>
    /// A non-negative varint no greater than <paramref name="max"/>.
    /// </summary>
    internal long Varint(long max = long.MaxValue)
    {
        ulong value = 0;

        for (var shift = 0; shift < MaxVarintBytes * ByteWriter.VarintPayloadBits; shift += ByteWriter.VarintPayloadBits)
        {
            var next = Byte();

            if (Failed)
            {
                return 0;
            }

            value |= ulong.CreateTruncating(next & VarintPayload) << shift;

            if ((next & ByteWriter.VarintContinues) == 0)
            {
                return Within(value, max);
            }
        }

        MarkFailed();

        return 0;
    }

    /// <summary>
    /// A count of items that each take at least one more byte, so it can never exceed what is left to read.
    /// </summary>
    internal int Count(int min = 0) => int.CreateTruncating(Positive(min, _remaining.Length));

    internal long Positive(long min = 1, long max = long.MaxValue)
    {
        var value = Varint(max);

        if (value < min)
        {
            MarkFailed();
        }

        return value;
    }

    internal Guid Guid() => Take(ByteWriter.GuidLength) is { Length: ByteWriter.GuidLength } raw ? new Guid(raw) : default;

    internal ReadOnlySpan<byte> LengthPrefixed() => Take(Count());

    internal void MarkFailed() => Failed = true;

    private ReadOnlySpan<byte> Take(int length)
    {
        if (Failed || length > _remaining.Length)
        {
            MarkFailed();

            return [];
        }

        var taken = _remaining[..length];

        _remaining = _remaining[length..];

        return taken;
    }

    private long Within(ulong value, long max)
    {
        if (value <= ulong.CreateChecked(max))
        {
            return long.CreateChecked(value);
        }

        MarkFailed();

        return 0;
    }
}
