using System.Buffers;
using System.Text;

namespace Yello.Merge.Sequence;

/// <summary>
/// Text split into Unicode scalars, the unit the merge stores, so a surrogate pair can never be split by an edit.
/// </summary>
internal static class Scalars
{
    /// <summary>
    /// The scalars of well-formed UTF-16, or <c>null</c> when it holds an unpaired surrogate.
    /// </summary>
    internal static List<Rune>? FromUtf16(ReadOnlySpan<char> text)
    {
        var scalars = new List<Rune>(text.Length);

        while (!text.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(text, out var scalar, out var consumed) != OperationStatus.Done)
            {
                return null;
            }

            scalars.Add(scalar);
            text = text[consumed..];
        }

        return scalars;
    }

    /// <summary>
    /// The scalars of strictly valid UTF-8, or <c>null</c> when any byte sequence is invalid.
    /// </summary>
    internal static List<Rune>? FromUtf8(ReadOnlySpan<byte> utf8)
    {
        var scalars = new List<Rune>(utf8.Length);

        while (!utf8.IsEmpty)
        {
            if (Rune.DecodeFromUtf8(utf8, out var scalar, out var consumed) != OperationStatus.Done)
            {
                return null;
            }

            scalars.Add(scalar);
            utf8 = utf8[consumed..];
        }

        return scalars;
    }

    internal static byte[] ToUtf8(IReadOnlyList<Rune> scalars)
    {
        var utf8 = new byte[scalars.Sum(scalar => scalar.Utf8SequenceLength)];
        var written = 0;

        foreach (var scalar in scalars)
        {
            written += scalar.EncodeToUtf8(utf8.AsSpan(written));
        }

        return utf8;
    }
}
