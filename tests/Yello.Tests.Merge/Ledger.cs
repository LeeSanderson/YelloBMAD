using System.Text;

namespace Yello.Tests.Merge;

/// <summary>
/// Every glyph each replica inserted and every glyph any replica deleted, for workloads built from <see cref="Glyphs"/>.
/// A glyph is inserted once, so "present exactly once" and "absent" are each a single count.
/// </summary>
internal sealed class Ledger
{
    private readonly Dictionary<Rune, int> _insertedBy = [];
    private readonly HashSet<Rune> _deleted = [];

    public void RecordInsert(int replica, string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            _insertedBy[rune] = replica;
        }
    }

    public void RecordDelete(string removed)
    {
        foreach (var rune in removed.EnumerateRunes())
        {
            _deleted.Add(rune);
        }
    }

    /// <summary>
    /// What is wrong with <paramref name="text"/> against the inserts made by the replicas <paramref name="insertedBy"/>
    /// selects: a surviving insert missing or duplicated, or a deleted glyph back. Empty when nothing is.
    /// </summary>
    public IReadOnlyList<string> Problems(string text, Func<int, bool> insertedBy)
    {
        var counts = new Dictionary<Rune, int>();

        foreach (var rune in text.EnumerateRunes())
        {
            counts[rune] = counts.GetValueOrDefault(rune) + 1;
        }

        var problems = new List<string>();

        foreach (var (rune, replica) in _insertedBy.Where(entry => insertedBy(entry.Value)))
        {
            var count = counts.GetValueOrDefault(rune);

            var deleted = _deleted.Contains(rune);

            if (deleted && count != 0)
            {
                problems.Add($"U+{rune.Value:X} was deleted but is present {count} time(s) (inserted by replica {replica})");
            }

            if (!deleted && count != 1)
            {
                problems.Add($"U+{rune.Value:X} was inserted by replica {replica} and never deleted, but is present {count} time(s)");
            }
        }

        return problems;
    }

    public IReadOnlyList<string> Problems(string text) => Problems(text, _ => true);
}
