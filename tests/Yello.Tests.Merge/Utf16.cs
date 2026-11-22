using System.Buffers;
using System.Text;

namespace Yello.Tests.Merge;

/// <summary>
/// Converts between scalar positions, which workloads choose, and the UTF-16 indexes the port takes.
/// </summary>
internal static class Utf16
{
    public static int ScalarCount(string text)
    {
        var count = 0;

        foreach (var _ in text.EnumerateRunes())
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// The UTF-16 index of the scalar at <paramref name="scalarIndex"/>, or the text's length at its end.
    /// </summary>
    public static int IndexOf(string text, int scalarIndex)
    {
        var index = 0;
        var seen = 0;

        foreach (var rune in text.EnumerateRunes())
        {
            if (seen == scalarIndex)
            {
                return index;
            }

            index += rune.Utf16SequenceLength;
            seen++;
        }

        return index;
    }

    /// <summary>
    /// The UTF-16 length of <paramref name="scalars"/> scalars starting at UTF-16 <paramref name="index"/>.
    /// </summary>
    public static int LengthOf(string text, int index, int scalars)
    {
        var length = 0;

        for (var taken = 0; taken < scalars && index + length < text.Length; taken++)
        {
            length += char.IsHighSurrogate(text[index + length]) ? 2 : 1;
        }

        return length;
    }

    /// <summary>
    /// Whether every surrogate in the text is half of a well-formed pair.
    /// </summary>
    public static bool IsWellFormed(string text)
    {
        var remaining = text.AsSpan();

        while (!remaining.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(remaining, out _, out var consumed) != OperationStatus.Done)
            {
                return false;
            }

            remaining = remaining[consumed..];
        }

        return true;
    }
}
