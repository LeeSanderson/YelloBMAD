using Xunit;
using Yello.Merge;

namespace Yello.Tests.Merge.Conformance;

internal static partial class MergeCases
{
    public static void An_insert_outside_the_text_is_refused_and_changes_nothing(ITextMergeStrategy strategy)
    {
        var replica = Seeded(strategy, "abc");

        AssertRefused(replica, r => r.Insert(4, "x"));
        AssertRefused(replica, r => r.Insert(-1, "x"));
    }

    public static void A_delete_reaching_outside_the_text_is_refused_and_changes_nothing(ITextMergeStrategy strategy)
    {
        var replica = Seeded(strategy, "abc");

        AssertRefused(replica, r => r.Delete(2, 2));
        AssertRefused(replica, r => r.Delete(-1, 1));
        AssertRefused(replica, r => r.Delete(0, -1));
        AssertRefused(replica, r => r.Delete(4, 0));
    }

    public static void An_edit_that_would_split_a_surrogate_pair_is_refused_and_changes_nothing(ITextMergeStrategy strategy)
    {
        // "a😀b": the emoji is the pair at UTF-16 indexes 1 and 2.
        var replica = Seeded(strategy, "a\U0001F600b");

        AssertRefused(replica, r => r.Insert(2, "x"));
        AssertRefused(replica, r => r.Delete(2, 1));
        AssertRefused(replica, r => r.Delete(1, 1));
        AssertRefused(replica, r => r.Delete(0, 2));
    }

    public static void An_edit_on_whole_surrogate_pairs_is_accepted(ITextMergeStrategy strategy)
    {
        var replica = Seeded(strategy, "a\U0001F600b");

        replica.Insert(3, "\U0001F389");
        replica.Delete(1, 2);

        Assert.Equal("a\U0001F389b", replica.Text);
    }

    public static void Text_with_characters_outside_the_basic_plane_converges_with_no_broken_pair_anywhere(ITextMergeStrategy strategy)
    {
        var network = new Network(strategy, 4);

        network.Seed("a\U0001D11Eb\U0001F600c");
        network.Insert(0, 1, "\U0001F600");
        network.Insert(1, 1, "\U0001F389");
        network.Delete(2, 1, 2);
        network.Replace(3, 4, 2, "\U0001F44D");
        network.DeliverEachInItsOwnOrder(seed: 9);

        network.AssertConverged();
        Assert.All(network.Texts, text => Assert.True(Utf16.IsWellFormed(text), $"A replica holds an unpaired surrogate: \"{text}\"."));
    }

    private static ITextReplica Seeded(ITextMergeStrategy strategy, string text)
    {
        var replica = strategy.CreateReplica(Network.IdOf(0));

        replica.Insert(0, text);

        return replica;
    }

    private static void AssertRefused(ITextReplica replica, Func<ITextReplica, TextChange> edit)
    {
        var text = replica.Text;
        var version = replica.Version;

        Assert.ThrowsAny<ArgumentException>(() => edit(replica));
        Assert.Equal(text, replica.Text);
        Assert.Equal(version, replica.Version);
    }
}
