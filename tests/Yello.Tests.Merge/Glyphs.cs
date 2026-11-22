using System.Text;

namespace Yello.Tests.Merge;

/// <summary>
/// Hands out characters no other insert in the session has used, so a final text can be read back character by
/// character: which inserts survived, which deletes held, and whether anything appears twice. The order the merge chose is
/// never read.
/// </summary>
/// <remarks>
/// Narrow glyphs come from the CJK and Hangul blocks (one UTF-16 unit each); wide glyphs from CJK Extension B (a surrogate
/// pair each), so the same workloads exercise text that a code-unit index could split.
/// </remarks>
internal sealed class Glyphs
{
    private const int CjkStart = 0x4E00;
    private const int CjkCount = 0x9FFF - CjkStart + 1;
    private const int HangulStart = 0xAC00;
    private const int HangulCount = 0xD7A3 - HangulStart + 1;
    private const int WideStart = 0x20000;
    private const int WideCount = 0x2A6DF - WideStart + 1;

    private int _narrow;
    private int _wide;

    public string Take(int count, bool wide = false)
    {
        var text = new StringBuilder(count * 2);

        for (var i = 0; i < count; i++)
        {
            text.Append(wide ? NextWide().ToString() : NextNarrow().ToString());
        }

        return text.ToString();
    }

    private Rune NextNarrow()
    {
        var n = _narrow++;

        if (n < CjkCount)
        {
            return new Rune(CjkStart + n);
        }

        n -= CjkCount;

        return n < HangulCount
            ? new Rune(HangulStart + n)
            : throw new InvalidOperationException("The session used every narrow glyph; size the workload down.");
    }

    private Rune NextWide()
    {
        var n = _wide++;

        return n < WideCount
            ? new Rune(WideStart + n)
            : throw new InvalidOperationException("The session used every wide glyph; size the workload down.");
    }
}
