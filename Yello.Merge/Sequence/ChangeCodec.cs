namespace Yello.Merge.Sequence;

/// <summary>
/// The byte form of a change: format byte, kind, origin, sequence, then the insert's anchor, clock and UTF-8 text, or the
/// delete's runs of character identities.
/// </summary>
internal static class ChangeCodec
{
    private const byte InsertKind = 1;
    private const byte DeleteKind = 2;
    private const byte AtStart = 0;
    private const byte AfterElement = 1;

    internal static TextChange Encode(ChangeBody body)
    {
        var writer = new ByteWriter()
            .Byte(Format.Current)
            .Byte(body is InsertBody ? InsertKind : DeleteKind)
            .Guid(body.Id.Origin.Value)
            .Varint(body.Id.Sequence);

        if (body is InsertBody insert)
        {
            WriteInsert(writer, insert);
        }
        else if (body is DeleteBody delete)
        {
            WriteDelete(writer, delete);
        }
        else
        {
            throw new ArgumentException("A change is an insert or a delete.", nameof(body));
        }

        return new TextChange(body.Id, writer.ToArray());
    }

    internal static (ChangeAdmission Admission, ChangeBody? Body) Decode(ReadOnlySpan<byte> encoded)
    {
        var format = Format.Check(encoded);

        if (format != ChangeAdmission.Admitted)
        {
            return (format, null);
        }

        var reader = new ByteReader(encoded[1..]);
        var kind = reader.Byte();
        var id = new ChangeId(new ReplicaId(reader.Guid()), reader.Positive());

        ChangeBody? body = kind switch
        {
            InsertKind => ReadInsert(ref reader, id),
            DeleteKind => ReadDelete(ref reader, id),
            _ => null,
        };

        return body is not null && reader.IsComplete ? (ChangeAdmission.Admitted, body) : (ChangeAdmission.Malformed, null);
    }

    private static void WriteInsert(ByteWriter writer, InsertBody insert)
    {
        if (insert.After is { } after)
        {
            writer.Byte(AfterElement).Guid(after.Replica).Varint(after.Clock);
        }
        else
        {
            writer.Byte(AtStart);
        }

        writer.Varint(insert.Clock).LengthPrefixed(Scalars.ToUtf8(insert.Scalars));
    }

    private static void WriteDelete(ByteWriter writer, DeleteBody delete)
    {
        writer.Varint(delete.Targets.Count);

        foreach (var run in delete.Targets)
        {
            writer.Guid(run.Replica).Varint(run.Clock).Varint(run.Count);
        }
    }

    private static InsertBody? ReadInsert(ref ByteReader reader, ChangeId id)
    {
        ElementId? after = null;
        var anchor = reader.Byte();

        if (anchor == AfterElement)
        {
            after = new ElementId(reader.Guid(), reader.Positive());
        }
        else if (anchor != AtStart)
        {
            reader.MarkFailed();
        }
        else
        {
            // At the start of the text: there is no anchor to read.
        }

        var clock = reader.Positive();
        var scalars = Scalars.FromUtf8(reader.LengthPrefixed());

        if (reader.Failed || scalars is not { Count: > 0 } || clock > long.MaxValue - scalars.Count)
        {
            return null;
        }

        var body = new InsertBody(id, after, clock, scalars);

        // An insert anchored on one of its own characters could never apply.
        return after is { } own && body.Creates(own) ? null : body;
    }

    private static DeleteBody? ReadDelete(ref ByteReader reader, ChangeId id)
    {
        var count = reader.Count(min: 1);
        var targets = new List<ElementRun>(count);

        for (var i = 0; i < count && !reader.Failed; i++)
        {
            var run = new ElementRun(reader.Guid(), reader.Positive(), int.CreateTruncating(reader.Positive(max: int.MaxValue)));

            if (run.Clock > long.MaxValue - run.Count)
            {
                return null;
            }

            targets.Add(run);
        }

        return reader.Failed ? null : new DeleteBody(id, targets);
    }
}
