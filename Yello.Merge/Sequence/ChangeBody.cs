using System.Text;

namespace Yello.Merge.Sequence;

/// <summary>
/// A change as the replica applies it, read from or written to <see cref="TextChange.Encoded"/>.
/// </summary>
internal abstract class ChangeBody(ChangeId id)
{
    internal ChangeId Id { get; } = id;

    /// <summary>
    /// The first character this change needs before it can apply, or <c>null</c> when it can apply now.
    /// </summary>
    internal abstract ElementId? FirstMissing(IReadOnlyDictionary<ElementId, Element> elements);

    /// <summary>
    /// Whether applying this change needs <paramref name="element"/> to exist.
    /// </summary>
    internal abstract bool DependsOn(ElementId element);

    /// <summary>
    /// The characters this change brings into being; none for a delete.
    /// </summary>
    internal abstract IEnumerable<ElementId> Created();

    internal abstract void ApplyTo(Document document);

    internal abstract void TakeBackFrom(Document document);
}

/// <summary>
/// Characters typed after <see cref="After"/> (or at the start, when it is <c>null</c>). The i-th scalar's identity is
/// <c>(Id.Origin, Clock + i)</c>.
/// </summary>
internal sealed class InsertBody(ChangeId id, ElementId? after, long clock, IReadOnlyList<Rune> scalars) : ChangeBody(id)
{
    internal ElementId? After { get; } = after;

    internal long Clock { get; } = clock;

    internal IReadOnlyList<Rune> Scalars { get; } = scalars;

    internal ElementId ElementAt(int offset) => new(Id.Origin.Value, Clock + offset);

    internal IEnumerable<ElementId> Elements() => Enumerable.Range(0, Scalars.Count).Select(ElementAt);

    internal bool Overlaps(InsertBody other) =>
        other.Id.Origin == Id.Origin && other.Clock < Clock + Scalars.Count && Clock < other.Clock + other.Scalars.Count;

    internal override ElementId? FirstMissing(IReadOnlyDictionary<ElementId, Element> elements) =>
        After is { } anchor && !elements.ContainsKey(anchor) ? anchor : null;

    internal override bool DependsOn(ElementId element) => After == element;

    internal override IEnumerable<ElementId> Created() => Elements();

    internal override void ApplyTo(Document document) => document.Integrate(this);

    internal override void TakeBackFrom(Document document) => document.Remove(this);
}

/// <summary>
/// Characters removed, named by identity so a delete removes exactly what its author saw and never a character typed
/// concurrently into the same range.
/// </summary>
internal sealed class DeleteBody(ChangeId id, IReadOnlyList<ElementRun> targets) : ChangeBody(id)
{
    internal IReadOnlyList<ElementRun> Targets { get; } = targets;

    internal IEnumerable<ElementId> Elements() =>
        Targets.SelectMany(run => Enumerable.Range(0, run.Count).Select(offset => new ElementId(run.Replica, run.Clock + offset)));

    internal override ElementId? FirstMissing(IReadOnlyDictionary<ElementId, Element> elements)
    {
        foreach (var element in Elements())
        {
            if (!elements.ContainsKey(element))
            {
                return element;
            }
        }

        return null;
    }

    internal override bool DependsOn(ElementId element) =>
        Targets.Any(run => run.Replica == element.Replica && element.Clock >= run.Clock && element.Clock < run.Clock + run.Count);

    internal override IEnumerable<ElementId> Created() => [];

    internal override void ApplyTo(Document document) => document.Integrate(this);

    internal override void TakeBackFrom(Document document) => document.Restore(this);
}

/// <summary>
/// Consecutive characters typed by one replica: <c>Count</c> identities from <c>(Replica, Clock)</c>.
/// </summary>
internal readonly record struct ElementRun(Guid Replica, long Clock, int Count);
