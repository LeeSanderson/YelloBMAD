namespace Yello.Merge.Sequence;

/// <summary>
/// A set of change identities as, per origin, ascending ranges of sequence numbers: the content of a
/// <see cref="ReplicaVersion"/>.
/// </summary>
/// <remarks>
/// Ranges rather than one counter per origin, because a withdrawn change leaves a gap its origin never fills: a later
/// change from that origin must still count as held without the withdrawn one.
/// </remarks>
internal sealed class HeldChanges
{
    private readonly SortedDictionary<Guid, List<SequenceRange>> _origins;

    private HeldChanges(SortedDictionary<Guid, List<SequenceRange>> origins) => _origins = origins;

    internal static HeldChanges Of(IEnumerable<ChangeId> changes)
    {
        var origins = new SortedDictionary<Guid, List<SequenceRange>>();

        foreach (var origin in changes.GroupBy(change => change.Origin.Value))
        {
            var ranges = new List<SequenceRange>();

            foreach (var sequence in origin.Select(change => change.Sequence).Order())
            {
                if (ranges.Count > 0 && ranges[^1].Last + 1 == sequence)
                {
                    ranges[^1] = ranges[^1] with { Last = sequence };
                }
                else
                {
                    ranges.Add(new SequenceRange(sequence, sequence));
                }
            }

            origins.Add(origin.Key, ranges);
        }

        return new HeldChanges(origins);
    }

    internal bool Contains(ChangeId change)
    {
        if (!_origins.TryGetValue(change.Origin.Value, out var ranges))
        {
            return false;
        }

        var low = 0;
        var high = ranges.Count - 1;

        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var range = ranges[middle];

            if (change.Sequence < range.First)
            {
                high = middle - 1;
            }
            else if (change.Sequence > range.Last)
            {
                low = middle + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Canonical bytes: origins ascending, and each range as its gap from the previous range's end and its length, so two
    /// replicas holding the same changes write the same bytes.
    /// </summary>
    internal ReplicaVersion ToVersion()
    {
        var writer = new ByteWriter().Byte(Format.Current).Varint(_origins.Count);

        foreach (var (origin, ranges) in _origins)
        {
            writer.Guid(origin).Varint(ranges.Count);

            var end = 0L;

            foreach (var range in ranges)
            {
                writer.Varint(range.First - end).Varint(range.Last - range.First);
                end = range.Last;
            }
        }

        return new ReplicaVersion(writer.ToArray());
    }

    internal static (ChangeAdmission Admission, HeldChanges? Held) Read(ReadOnlySpan<byte> encoded)
    {
        var format = Format.Check(encoded);

        if (format != ChangeAdmission.Admitted)
        {
            return (format, null);
        }

        var reader = new ByteReader(encoded[1..]);
        var origins = new SortedDictionary<Guid, List<SequenceRange>>();
        var count = reader.Count();
        Guid? previous = null;

        for (var i = 0; i < count && !reader.Failed; i++)
        {
            var origin = reader.Guid();

            if (previous is { } before && origin.CompareTo(before) <= 0)
            {
                return (ChangeAdmission.Malformed, null);
            }

            origins.Add(origin, ReadRanges(ref reader));
            previous = origin;
        }

        return reader.IsComplete ? (ChangeAdmission.Admitted, new HeldChanges(origins)) : (ChangeAdmission.Malformed, null);
    }

    private static List<SequenceRange> ReadRanges(ref ByteReader reader)
    {
        var count = reader.Count(min: 1);
        var ranges = new List<SequenceRange>(count);
        var end = 0L;

        for (var i = 0; i < count && !reader.Failed; i++)
        {
            // A gap of one would join this range to the previous one, which the canonical form never writes.
            var first = end + reader.Positive(min: i == 0 ? 1 : 2, max: long.MaxValue - end);
            var last = first + reader.Varint(max: long.MaxValue - first);

            ranges.Add(new SequenceRange(first, last));
            end = last;
        }

        return ranges;
    }

    private readonly record struct SequenceRange(long First, long Last);
}
