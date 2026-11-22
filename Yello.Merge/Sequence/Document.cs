using System.Text;

namespace Yello.Merge.Sequence;

/// <summary>
/// The ordered characters of one replica, deleted ones included, with a lookup by identity.
/// </summary>
/// <remarks>
/// The order is a deterministic function of the set of inserts applied, never of the order they arrived in: an insert goes
/// immediately after its anchor, past any concurrent insert at the same anchor whose identity is greater (and so past that
/// insert's own followers, whose identities are greater still). Every replica that applied the same inserts holds the
/// same sequence.
/// </remarks>
internal sealed class Document
{
    private readonly Element _start = new(default, default);
    private readonly Dictionary<ElementId, Element> _elements = [];
    private string? _text = string.Empty;

    internal IReadOnlyDictionary<ElementId, Element> Elements => _elements;

    internal string Text => _text ??= Render();

    internal void Integrate(InsertBody insert)
    {
        var at = insert.After is { } after ? _elements[after] : _start;
        var first = insert.ElementAt(0);

        while (at.Next is { } next && next.Id > first)
        {
            at = next;
        }

        for (var offset = 0; offset < insert.Scalars.Count; offset++)
        {
            var element = new Element(insert.ElementAt(offset), insert.Scalars[offset]);

            Link(at, element);
            _elements.Add(element.Id, element);
            at = element;
        }

        _text = null;
    }

    internal void Integrate(DeleteBody delete)
    {
        foreach (var id in delete.Elements())
        {
            _elements[id].Deletes++;
        }

        _text = null;
    }

    internal void Remove(InsertBody insert)
    {
        for (var offset = 0; offset < insert.Scalars.Count; offset++)
        {
            var element = _elements[insert.ElementAt(offset)];

            element.Previous!.Next = element.Next;

            if (element.Next is { } next)
            {
                next.Previous = element.Previous;
            }

            _elements.Remove(element.Id);
        }

        _text = null;
    }

    internal void Restore(DeleteBody delete)
    {
        foreach (var id in delete.Elements())
        {
            _elements[id].Deletes--;
        }

        _text = null;
    }

    /// <summary>
    /// The visible character that ends at UTF-16 <paramref name="index"/>, or <c>null</c> at the start of the text.
    /// </summary>
    internal ElementId? VisibleBefore(int index)
    {
        Element? before = null;
        var offset = 0;

        for (var element = _start.Next; element is not null && offset < index; element = element.Next)
        {
            if (element.IsVisible)
            {
                before = element;
                offset += element.VisibleLength;
            }
        }

        return before?.Id;
    }

    /// <summary>
    /// The visible characters covering UTF-16 <paramref name="index"/> to <paramref name="index"/> + <paramref name="length"/>,
    /// as runs of consecutive identities.
    /// </summary>
    internal List<ElementRun> VisibleRuns(int index, int length)
    {
        var runs = new List<ElementRun>();
        var offset = 0;
        var end = index + length;

        for (var element = _start.Next; element is not null && offset < end; element = element.Next)
        {
            if (!element.IsVisible)
            {
                continue;
            }

            if (offset >= index)
            {
                Append(runs, element.Id);
            }

            offset += element.VisibleLength;
        }

        return runs;
    }

    private static void Append(List<ElementRun> runs, ElementId id)
    {
        if (runs.Count > 0 && runs[^1] is var last && last.Replica == id.Replica && last.Clock + last.Count == id.Clock)
        {
            runs[^1] = last with { Count = last.Count + 1 };
        }
        else
        {
            runs.Add(new ElementRun(id.Replica, id.Clock, 1));
        }
    }

    private static void Link(Element after, Element element)
    {
        element.Previous = after;
        element.Next = after.Next;

        if (after.Next is { } next)
        {
            next.Previous = element;
        }

        after.Next = element;
    }

    private string Render()
    {
        var text = new StringBuilder();
        Span<char> units = stackalloc char[2];

        for (var element = _start.Next; element is not null; element = element.Next)
        {
            if (element.IsVisible)
            {
                text.Append(units[..element.Scalar.EncodeToUtf16(units)]);
            }
        }

        return text.ToString();
    }
}
