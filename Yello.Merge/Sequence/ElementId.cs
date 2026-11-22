namespace Yello.Merge.Sequence;

/// <summary>
/// One character's identity: the replica that typed it and the Lamport clock it was typed at. A replica's clock rises with
/// every character it creates, so the pair is unique.
/// </summary>
/// <remarks>
/// The order is the tie-break between concurrent inserts at one place: the greater identity goes first. Because a replica
/// only ever inserts after characters it has seen, everything inserted after a character has a greater clock than it,
/// which is what lets an insert skip past concurrent ones without reading the whole document.
/// </remarks>
internal readonly record struct ElementId(Guid Replica, long Clock) : IComparable<ElementId>
{
    public int CompareTo(ElementId other)
    {
        var byClock = Clock.CompareTo(other.Clock);

        return byClock != 0 ? byClock : Replica.CompareTo(other.Replica);
    }

    public static bool operator <(ElementId left, ElementId right) => left.CompareTo(right) < 0;

    public static bool operator >(ElementId left, ElementId right) => left.CompareTo(right) > 0;

    public static bool operator <=(ElementId left, ElementId right) => left.CompareTo(right) <= 0;

    public static bool operator >=(ElementId left, ElementId right) => left.CompareTo(right) >= 0;
}
