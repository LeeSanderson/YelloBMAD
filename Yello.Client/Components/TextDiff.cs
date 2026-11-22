using Yello.Merge;

namespace Yello.Client.Components;

/// <summary>
/// Turns one edit of a text field into merge changes, and maps a caret through a change made under it.
/// </summary>
/// <remarks>
/// <b>The changed span is what lies between the common prefix and the common suffix</b> of the text before and after, so a
/// keystroke, a paste over a selection and an undo each become at most one delete and one insert. Neither boundary may fall
/// between the halves of a surrogate pair: the merge refuses an index that splits one, and a character is never half
/// changed.
/// </remarks>
internal static class TextDiff
{
    /// <summary>
    /// Applies the edit from <paramref name="before"/> to <paramref name="after"/> to the replica and answers its changes.
    /// </summary>
    /// <param name="replica">The local replica, holding <paramref name="before"/>.</param>
    /// <param name="before">The text the field held.</param>
    /// <param name="after">The text it holds now.</param>
    /// <returns>A delete, an insert, both in that order, or none.</returns>
    internal static List<TextChange> Record(ITextReplica replica, string before, string after)
    {
        var (prefix, suffix) = Span(before, after);
        var changes = new List<TextChange>(2);
        var removed = before.Length - prefix - suffix;
        var inserted = after.Length - prefix - suffix;

        if (removed > 0)
        {
            changes.Add(replica.Delete(prefix, removed));
        }

        if (inserted > 0)
        {
            changes.Add(replica.Insert(prefix, after.Substring(prefix, inserted)));
        }

        return changes;
    }

    /// <summary>
    /// Where a position in <paramref name="before"/> lands in <paramref name="after"/>: unchanged before the changed span,
    /// shifted by its growth after it, and at its end inside it.
    /// </summary>
    /// <param name="before">The text before the change.</param>
    /// <param name="after">The text after it.</param>
    /// <param name="position">A UTF-16 position in <paramref name="before"/>.</param>
    /// <returns>The position in <paramref name="after"/>.</returns>
    internal static int Map(string before, string after, int position)
    {
        var (prefix, suffix) = Span(before, after);

        if (position <= prefix)
        {
            return Math.Min(position, after.Length);
        }

        return position >= before.Length - suffix
            ? Math.Clamp(position + after.Length - before.Length, 0, after.Length)
            : after.Length - suffix;
    }

    /// <summary>
    /// The lengths of the common prefix and the common suffix, neither overlapping the other nor splitting a surrogate pair.
    /// </summary>
    private static (int Prefix, int Suffix) Span(string before, string after)
    {
        var prefix = CommonPrefix(before, after);

        return (prefix, CommonSuffix(before, after, prefix));
    }

    private static int CommonPrefix(string before, string after)
    {
        var shorter = Math.Min(before.Length, after.Length);
        var prefix = 0;

        while (prefix < shorter && before[prefix] == after[prefix])
        {
            prefix++;
        }

        return prefix > 0 && (SplitsAPair(before, prefix) || SplitsAPair(after, prefix)) ? prefix - 1 : prefix;
    }

    private static int CommonSuffix(string before, string after, int prefix)
    {
        var room = Math.Min(before.Length, after.Length) - prefix;
        var suffix = 0;

        while (suffix < room && before[before.Length - 1 - suffix] == after[after.Length - 1 - suffix])
        {
            suffix++;
        }

        return suffix > 0 && (SplitsAPair(before, before.Length - suffix) || SplitsAPair(after, after.Length - suffix)) ? suffix - 1 : suffix;
    }

    private static bool SplitsAPair(string text, int index) =>
        index > 0 && index < text.Length && char.IsLowSurrogate(text[index]);
}
