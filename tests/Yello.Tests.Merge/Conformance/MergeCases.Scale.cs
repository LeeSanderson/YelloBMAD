using Xunit;
using Yello.Merge;

namespace Yello.Tests.Merge.Conformance;

internal static partial class MergeCases
{
    private const int TenEditors = 10;

    /// <summary>
    /// The partitioned replicas in the disconnection workloads. Replicas 0 and 1 stay connected throughout.
    /// </summary>
    private const int Offline = 3;
    private const int AlsoOffline = 2;

    public static void Ten_editors_changing_one_description_at_once_end_with_identical_text_and_every_surviving_character_once(ITextMergeStrategy strategy)
    {
        var network = new Network(strategy, TenEditors);
        var glyphs = new Glyphs();
        var random = new Random(0x10_0010);

        network.Seed(glyphs.Take(200));

        // Every edit is made before any is delivered, so each is concurrent with every other editor's.
        for (var round = 0; round < Workload.ConcurrentEditsPerEditor; round++)
        {
            for (var editor = 0; editor < TenEditors; editor++)
            {
                Workload.RandomEdit(network, editor, random, glyphs);
            }
        }

        network.DeliverEachInItsOwnOrder(seed: 0x10);

        network.AssertConverged();
        network.AssertNothingLostOrResurrected(_ => true, "Ten concurrent editors");
    }

    public static void Once_every_change_has_arrived_the_texts_are_identical_with_no_further_message(ITextMergeStrategy strategy)
    {
        var network = new Network(strategy, 3);
        var glyphs = new Glyphs();
        var random = new Random(0x4_0004);

        network.Seed(glyphs.Take(50));

        for (var round = 0; round < 20; round++)
        {
            for (var editor = 0; editor < network.Size; editor++)
            {
                Workload.RandomEdit(network, editor, random, glyphs);
            }
        }

        // Each change delivered exactly once, and nothing else: no acknowledgement, no exchange, no second round.
        network.DeliverEachInItsOwnOrder(seed: 0x4);

        network.AssertConverged();

        for (var a = 0; a < network.Size; a++)
        {
            for (var b = 0; b < network.Size; b++)
            {
                Assert.Empty(network[a].ChangesMissingFrom(network[b].Version));
            }
        }
    }

    public static void An_editor_offline_for_five_minutes_finds_its_own_changes_present_exactly_once(ITextMergeStrategy strategy)
    {
        var network = FiveMinutesApart(strategy, Offline);

        network.Reconnect(Offline);
        network.DeliverAll(Delivery.AsSent);

        network.AssertConverged();
        network.AssertNothingLostOrResurrected(replica => replica == Offline, "The offline editor's own changes after reconnection");
    }

    public static void An_editor_offline_for_five_minutes_finds_the_others_changes_present(ITextMergeStrategy strategy)
    {
        var network = FiveMinutesApart(strategy, Offline);

        network.Reconnect(Offline);
        network.DeliverAll(Delivery.AsSent);

        network.AssertConverged();
        network.AssertNothingLostOrResurrected(replica => replica != Offline, "The others' changes after the offline editor reconnected");
    }

    public static void Two_editors_offline_at_once_cut_off_from_each_other_both_reconcile(ITextMergeStrategy strategy)
    {
        var network = FiveMinutesApart(strategy, AlsoOffline, Offline);

        network.Reconnect(AlsoOffline);
        network.Reconnect(Offline);
        network.DeliverAll(Delivery.AsSent);

        network.AssertConverged();
        network.AssertNothingLostOrResurrected(_ => true, "Two editors partitioned at once, after both reconnected");
    }

    /// <summary>
    /// Four replicas on a 2,000-glyph description; <paramref name="partitioned"/> are cut off, each from every other
    /// replica, while all four make five minutes of edits. The connected ones exchange their changes after every round.
    /// Returned before any reconnection.
    /// </summary>
    internal static Network FiveMinutesApart(ITextMergeStrategy strategy, params int[] partitioned)
    {
        var network = new Network(strategy, 4);
        var glyphs = new Glyphs();
        var random = new Random(0x6_0006);

        network.Seed(glyphs.Take(2_000));

        foreach (var replica in partitioned)
        {
            network.Isolate(replica);
        }

        for (var round = 0; round < Workload.FiveMinutesOfEdits; round++)
        {
            for (var editor = 0; editor < network.Size; editor++)
            {
                Workload.RandomEdit(network, editor, random, glyphs);
            }

            network.DeliverAll(Delivery.AsSent);
        }

        return network;
    }
}
