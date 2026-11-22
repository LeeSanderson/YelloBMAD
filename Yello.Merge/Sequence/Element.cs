using System.Text;

namespace Yello.Merge.Sequence;

/// <summary>
/// One Unicode scalar in the document's sequence, deleted or not. Deleted characters stay in place, because a concurrent
/// insert may still name one as the character it follows.
/// </summary>
internal sealed class Element(ElementId id, Rune scalar)
{
    internal ElementId Id { get; } = id;

    internal Rune Scalar { get; } = scalar;

    /// <summary>
    /// How many held changes delete this character. A count rather than a flag, so withdrawing one of two concurrent
    /// deletes leaves it deleted.
    /// </summary>
    internal int Deletes { get; set; }

    internal bool IsVisible => Deletes == 0;

    internal int VisibleLength => IsVisible ? Scalar.Utf16SequenceLength : 0;

    internal Element? Previous { get; set; }

    internal Element? Next { get; set; }
}
