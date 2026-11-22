namespace Yello.Tests.Merge;

/// <summary>
/// Random local edits made of fresh <see cref="Glyphs"/>, so the <see cref="Ledger"/> can account for every character.
/// </summary>
/// <remarks>
/// Sizes are open question 7 (a), recorded here as the assumption they are: a working document of up to 20,000
/// characters; five minutes offline is about 1,500 local edits per editor (five a second); ten simultaneous editors each
/// make at least 200 concurrent edits. No product limit on a description's length exists, so none is invented.
/// </remarks>
internal static class Workload
{
    public const int WorkingDocumentLength = 20_000;

    public const int FiveMinutesOfEdits = 1_500;

    public const int ConcurrentEditsPerEditor = 200;

    /// <summary>
    /// One edit at a random scalar position: an insert of one to three fresh glyphs (one insert in four uses surrogate
    /// pairs), or, three times in ten, a delete of one to three scalars.
    /// </summary>
    public static void RandomEdit(Network network, int replica, Random random, Glyphs glyphs)
    {
        var text = network[replica].Text;
        var scalars = Utf16.ScalarCount(text);

        if (scalars > 0 && random.Next(10) < 3)
        {
            var start = random.Next(scalars);
            var count = Math.Min(1 + random.Next(3), scalars - start);
            var index = Utf16.IndexOf(text, start);

            network.Delete(replica, index, Utf16.LengthOf(text, index, count));

            return;
        }

        var at = Utf16.IndexOf(text, random.Next(scalars + 1));

        network.Insert(replica, at, glyphs.Take(1 + random.Next(3), wide: random.Next(4) == 0));
    }
}
