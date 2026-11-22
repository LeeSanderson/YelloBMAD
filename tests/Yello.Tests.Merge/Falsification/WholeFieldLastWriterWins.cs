using System.Buffers.Binary;
using System.Text;
using Yello.Merge;

namespace Yello.Tests.Merge.Falsification;

/// <summary>
/// The merge the architecture rules out, built as a working implementation of the port so the conformance suite can be
/// shown to reject it: every change carries the whole text and a timestamp, and the latest timestamp wins.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its timestamps are Lamport clocks, ties broken by replica</b> - the strongest form of last-writer-wins, since a wall
/// clock can only order changes worse. Everything else is done properly: idempotent apply, the "has" exchange, encoded
/// state, refusal of damaged bytes, withdrawal and index checks. So when it fails a case, it fails on merge semantics, and
/// the falsification tests check exactly that.
/// </para>
/// <para>Lives only in this test project; the gate counting implementations of the port scans production assemblies.</para>
/// </remarks>
internal sealed class WholeFieldLastWriterWins : ITextMergeStrategy
{
    private const byte Format = 1;

    public ITextReplica CreateReplica(ReplicaId id) => new Replica(id);

    public Decoded<ITextReplica> LoadReplica(ReplicaId id, ReadOnlySpan<byte> state)
    {
        var admission = CheckFormat(state);

        if (admission != ChangeAdmission.Admitted)
        {
            return new(admission, null);
        }

        var loaded = new Replica(id);
        var reader = state[1..];

        if (!TryReadLong(ref reader, out var nextSequence) || !TryReadInt(ref reader, out var count) || count < 0)
        {
            return new(ChangeAdmission.Malformed, null);
        }

        for (var i = 0; i < count; i++)
        {
            if (!TryReadInt(ref reader, out var length) || length < 0 || length > reader.Length
                || TryDecode(reader[..length], out var change) != ChangeAdmission.Admitted)
            {
                return new(ChangeAdmission.Malformed, null);
            }

            loaded.Restore(change!);
            reader = reader[length..];
        }

        if (!reader.IsEmpty)
        {
            return new(ChangeAdmission.Malformed, null);
        }

        loaded.ResumeAt(nextSequence);

        return new(ChangeAdmission.Admitted, loaded);
    }

    public Decoded<TextChange> DecodeChange(ReadOnlySpan<byte> encoded)
    {
        var admission = TryDecode(encoded, out var decoded);

        return new(admission, decoded?.Change);
    }

    public Decoded<ReplicaVersion> DecodeVersion(ReadOnlySpan<byte> encoded)
    {
        var admission = CheckFormat(encoded);

        if (admission != ChangeAdmission.Admitted)
        {
            return new(admission, null);
        }

        return TryReadIds(encoded, out _)
            ? new(ChangeAdmission.Admitted, new ReplicaVersion(encoded))
            : new(ChangeAdmission.Malformed, null);
    }

    private static ChangeAdmission CheckFormat(ReadOnlySpan<byte> encoded) => encoded switch
    {
        [] => ChangeAdmission.Malformed,
        [> Format, ..] => ChangeAdmission.UnsupportedVersion,
        [Format, ..] => ChangeAdmission.Admitted,
        _ => ChangeAdmission.Malformed,
    };

    private static ChangeAdmission TryDecode(ReadOnlySpan<byte> encoded, out Stamped? decoded)
    {
        decoded = null;

        var admission = CheckFormat(encoded);

        if (admission != ChangeAdmission.Admitted)
        {
            return admission;
        }

        var reader = encoded[1..];

        if (!TryReadHeader(ref reader, out var id, out var clock, out var length) || length != reader.Length)
        {
            return ChangeAdmission.Malformed;
        }

        string text;

        try
        {
            text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(reader);
        }
        catch (DecoderFallbackException)
        {
            return ChangeAdmission.Malformed;
        }

        decoded = new Stamped(new TextChange(id, encoded), clock, text);

        return ChangeAdmission.Admitted;
    }

    private static bool TryReadHeader(ref ReadOnlySpan<byte> reader, out ChangeId id, out long clock, out int length)
    {
        clock = 0;
        length = 0;

        return TryReadId(ref reader, out id) && TryReadLong(ref reader, out clock) && TryReadInt(ref reader, out length);
    }

    private static bool TryReadIds(ReadOnlySpan<byte> encoded, out List<ChangeId> ids)
    {
        ids = [];

        var reader = encoded[1..];

        if (!TryReadInt(ref reader, out var count) || count < 0)
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            if (!TryReadId(ref reader, out var id))
            {
                return false;
            }

            ids.Add(id);
        }

        return reader.IsEmpty;
    }

    private static bool TryReadId(ref ReadOnlySpan<byte> reader, out ChangeId id)
    {
        id = default;

        if (reader.Length < 24)
        {
            return false;
        }

        id = new ChangeId(new ReplicaId(new Guid(reader[..16])), BinaryPrimitives.ReadInt64LittleEndian(reader[16..]));
        reader = reader[24..];

        return true;
    }

    private static bool TryReadLong(ref ReadOnlySpan<byte> reader, out long value)
    {
        value = 0;

        if (reader.Length < 8)
        {
            return false;
        }

        value = BinaryPrimitives.ReadInt64LittleEndian(reader);
        reader = reader[8..];

        return true;
    }

    private static bool TryReadInt(ref ReadOnlySpan<byte> reader, out int value)
    {
        value = 0;

        if (reader.Length < 4)
        {
            return false;
        }

        value = BinaryPrimitives.ReadInt32LittleEndian(reader);
        reader = reader[4..];

        return true;
    }

    private sealed record Stamped(TextChange Change, long Clock, string Text);

    private sealed class Replica(ReplicaId id) : ITextReplica
    {
        private readonly Dictionary<ChangeId, Stamped> _log = [];
        private long _clock;
        private long _nextSequence = 1;
        private Stamped? _winner;

        public ReplicaId Id { get; } = id;

        public string Text => _winner?.Text ?? string.Empty;

        public ReplicaVersion Version
        {
            get
            {
                var ids = _log.Keys.Order(Comparer<ChangeId>.Create(Compare)).ToList();
                var bytes = new byte[1 + 4 + (24 * ids.Count)];

                bytes[0] = Format;
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(1), ids.Count);

                for (var i = 0; i < ids.Count; i++)
                {
                    var at = 5 + (24 * i);

                    ids[i].Origin.Value.TryWriteBytes(bytes.AsSpan(at, 16));
                    BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(at + 16), ids[i].Sequence);
                }

                return new ReplicaVersion(bytes);
            }
        }

        public TextChange Insert(int index, string text)
        {
            var current = Text;

            CheckBoundary(current, index);

            return Local(current.Insert(index, text));
        }

        public TextChange Delete(int index, int length)
        {
            var current = Text;

            ArgumentOutOfRangeException.ThrowIfNegative(length);
            CheckBoundary(current, index);
            CheckBoundary(current, index + length);

            return Local(current.Remove(index, length));
        }

        public ChangeAdmission Apply(TextChange change)
        {
            var admission = TryDecode(change.Encoded.Span, out var decoded);

            if (admission != ChangeAdmission.Admitted || _log.ContainsKey(decoded!.Change.Id))
            {
                return admission;
            }

            _clock = Math.Max(_clock, decoded.Clock) + 1;
            _log[decoded.Change.Id] = decoded;
            Consider(decoded);

            return ChangeAdmission.Admitted;
        }

        public IReadOnlyList<TextChange> ChangesMissingFrom(ReplicaVersion version)
        {
            if (!TryReadIds(version.Encoded.Span, out var held))
            {
                throw new ArgumentException("The version is not one this strategy encoded.", nameof(version));
            }

            var has = held.ToHashSet();

            return [.. _log.Values.Where(stamped => !has.Contains(stamped.Change.Id)).Select(stamped => stamped.Change)];
        }

        public ReadOnlyMemory<byte> EncodeState()
        {
            using var stream = new MemoryStream();
            Span<byte> number = stackalloc byte[8];

            stream.WriteByte(Format);
            BinaryPrimitives.WriteInt64LittleEndian(number, _nextSequence);
            stream.Write(number);
            BinaryPrimitives.WriteInt32LittleEndian(number, _log.Count);
            stream.Write(number[..4]);

            foreach (var stamped in _log.Values)
            {
                BinaryPrimitives.WriteInt32LittleEndian(number, stamped.Change.Encoded.Length);
                stream.Write(number[..4]);
                stream.Write(stamped.Change.Encoded.Span);
            }

            return stream.ToArray();
        }

        public void Withdraw(ChangeId change)
        {
            if (change.Origin != Id || !_log.Remove(change))
            {
                throw new InvalidOperationException("Only a local change this replica holds can be withdrawn.");
            }

            _winner = null;

            foreach (var stamped in _log.Values)
            {
                Consider(stamped);
            }
        }

        public void Restore(Stamped stamped)
        {
            _log[stamped.Change.Id] = stamped;
            _clock = Math.Max(_clock, stamped.Clock);
            Consider(stamped);
        }

        public void ResumeAt(long nextSequence) => _nextSequence = nextSequence;

        private static byte[] Encode(ChangeId id, long clock, string text)
        {
            var utf8 = Encoding.UTF8.GetBytes(text);
            var bytes = new byte[1 + 16 + 8 + 8 + 4 + utf8.Length];

            bytes[0] = Format;
            id.Origin.Value.TryWriteBytes(bytes.AsSpan(1, 16));
            BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(17), id.Sequence);
            BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(25), clock);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(33), utf8.Length);
            utf8.CopyTo(bytes.AsSpan(37));

            return bytes;
        }

        private static int Compare(ChangeId a, ChangeId b)
        {
            var origin = a.Origin.Value.CompareTo(b.Origin.Value);

            return origin != 0 ? origin : a.Sequence.CompareTo(b.Sequence);
        }

        private static void CheckBoundary(string text, int index)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(index, text.Length);

            if (index > 0 && index < text.Length && char.IsLowSurrogate(text[index]) && char.IsHighSurrogate(text[index - 1]))
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The index splits a surrogate pair.");
            }
        }

        private void Consider(Stamped stamped)
        {
            if (_winner is null
                || stamped.Clock > _winner.Clock
                || (stamped.Clock == _winner.Clock && stamped.Change.Id.Origin.Value.CompareTo(_winner.Change.Id.Origin.Value) > 0))
            {
                _winner = stamped;
            }
        }

        private TextChange Local(string text)
        {
            var changeId = new ChangeId(Id, _nextSequence++);

            _clock++;

            var change = new TextChange(changeId, Encode(changeId, _clock, text));

            var stamped = new Stamped(change, _clock, text);

            _log[changeId] = stamped;
            Consider(stamped);

            return change;
        }
    }
}
