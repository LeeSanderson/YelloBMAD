using Xunit;
using Yello.Merge;

namespace Yello.Tests.Merge.Conformance;

/// <summary>
/// The conformance case bodies. Each takes the strategy under test and nothing else, so the conformance facts run them
/// against the candidate and the falsification runs the same bodies against whole-field last-writer-wins.
/// </summary>
/// <remarks>
/// <b>Never assert an ordering the requirement does not fix.</b> Two inserts at one index must end in the same order at
/// every replica, but which comes first is the algorithm's choice, so the cases read survival and agreement only. Where a
/// requirement does fix the text (edits to different regions), the case states it exactly.
/// </remarks>
internal static partial class MergeCases
{
    /// <summary>
    /// The concurrency shape of every pairwise case: replica 0 edits, replica 1 edits concurrently, both receive each
    /// other's change, and two observers receive both changes in opposite orders.
    /// </summary>
    private const int EditorA = 0;
    private const int EditorB = 1;
    private const int ObserverAsSent = 2;
    private const int ObserverReversed = 3;

    public static void One_editor_typing_and_deleting_alone_is_seen_identically_by_every_replica(ITextMergeStrategy strategy)
    {
        var network = new Network(strategy, 3);
        var expected = string.Empty;

        void Typed(Action edit, string after)
        {
            edit();
            network.DeliverAll(Delivery.AsSent);
            expected = after;

            Assert.All(network.Texts, text => Assert.Equal(expected, text));
        }

        Typed(() => network.Insert(0, 0, "Fix the login"), "Fix the login");
        Typed(() => network.Insert(0, 13, " page"), "Fix the login page");
        Typed(() => network.Delete(0, 0, 4), "the login page");
        Typed(() => network.Insert(0, 0, "Restyle "), "Restyle the login page");
        Typed(() => network.Replace(0, 12, 5, "sign-in"), "Restyle the sign-in page");
    }

    public static void Two_editors_inserting_in_different_paragraphs_both_keep_their_changes(ITextMergeStrategy strategy) =>
        AssertDifferentRegions(
            strategy,
            "one\n\ntwo",
            network => network.Insert(EditorA, 3, " more"),
            network => network.Insert(EditorB, 8, " too"),
            "one more\n\ntwo too");

    public static void An_insert_and_a_delete_that_do_not_overlap_both_take_effect(ITextMergeStrategy strategy) =>
        AssertDifferentRegions(
            strategy,
            "one two three",
            network => network.Insert(EditorA, 0, "zero "),
            network => network.Delete(EditorB, 7, 6),
            "zero one two");

    public static void Two_deletes_that_do_not_overlap_both_take_effect(ITextMergeStrategy strategy) =>
        AssertDifferentRegions(
            strategy,
            "one two three",
            network => network.Delete(EditorA, 0, 4),
            network => network.Delete(EditorB, 7, 6),
            "two");

    public static void An_insert_at_the_start_and_an_insert_at_the_end_both_keep_their_changes(ITextMergeStrategy strategy) =>
        AssertDifferentRegions(
            strategy,
            "middle",
            network => network.Insert(EditorA, 0, "["),
            network => network.Insert(EditorB, 6, "]"),
            "[middle]");

    public static void Two_inserts_at_the_same_place_end_in_the_same_text_everywhere_and_both_survive(ITextMergeStrategy strategy)
    {
        var network = ConcurrentPair(
            strategy,
            "ab",
            n => n.Insert(EditorA, 1, "X"),
            n => n.Insert(EditorB, 1, "Y"));

        var text = network[EditorA].Text;

        Assert.True(text is "aXYb" or "aYXb", $"Both inserts must survive between 'a' and 'b'; the text is \"{text}\".");
    }

    public static void Overlapping_deletes_end_in_the_same_text_everywhere_with_nothing_deleted_coming_back(ITextMergeStrategy strategy)
    {
        var network = ConcurrentPair(
            strategy,
            "abcdef",
            n => n.Delete(EditorA, 1, 3),
            n => n.Delete(EditorB, 2, 3));

        Assert.Equal("af", network[EditorA].Text);
    }

    public static void An_insert_inside_a_range_another_editor_deletes_ends_in_the_same_text_everywhere_and_survives(ITextMergeStrategy strategy)
    {
        // The deleting editor never saw the insert, so its delete cannot have meant to remove it.
        var network = ConcurrentPair(
            strategy,
            "abcdef",
            n => n.Insert(EditorA, 3, "X"),
            n => n.Delete(EditorB, 1, 4));

        Assert.Equal("aXf", network[EditorA].Text);
    }

    public static void A_replace_against_an_insert_in_the_same_range_ends_in_the_same_text_everywhere(ITextMergeStrategy strategy)
    {
        var network = ConcurrentPair(
            strategy,
            "abcdef",
            n => n.Replace(EditorA, 2, 2, "Z"),
            n => n.Insert(EditorB, 3, "Y"));

        var text = network[EditorA].Text;

        Assert.True(
            text is "abZYef" or "abYZef",
            $"The replaced 'cd' must be gone and both the replacement and the concurrent insert present; the text is \"{text}\".");
    }

    private static void AssertDifferentRegions(
        ITextMergeStrategy strategy,
        string seed,
        Action<Network> editA,
        Action<Network> editB,
        string expected)
    {
        var network = ConcurrentPair(strategy, seed, editA, editB);

        Assert.Equal(expected, network[EditorA].Text);
    }

    /// <summary>
    /// Runs two concurrent edits on a shared seed text, delivers everything, and asserts every replica agrees, whatever
    /// order it received the two changes in.
    /// </summary>
    private static Network ConcurrentPair(ITextMergeStrategy strategy, string seed, Action<Network> editA, Action<Network> editB)
    {
        var network = new Network(strategy, 4);

        network.Seed(seed);
        editA(network);
        editB(network);

        network.Deliver(EditorA, Delivery.AsSent);
        network.Deliver(EditorB, Delivery.AsSent);
        network.Deliver(ObserverAsSent, Delivery.AsSent);
        network.Deliver(ObserverReversed, Delivery.Reversed);

        network.AssertConverged();

        return network;
    }
}
