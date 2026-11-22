using Xunit;
using Yello.Merge;

namespace Yello.Tests.Merge.Conformance;

internal static partial class MergeCases
{
    public static void A_change_decoded_from_its_encoding_is_the_same_change(ITextMergeStrategy strategy)
    {
        var network = new Network(strategy, 1);

        network.Seed("text 😀 here");

        foreach (var change in new[] { network.Insert(0, 5, "more "), network.Delete(0, 10, 2) })
        {
            var decoded = Port.Admitted(strategy.DecodeChange(change.Encoded.Span));
            Assert.Equal(change, decoded);
        }
    }

    public static void A_replica_loaded_from_its_encoded_state_has_the_same_text_and_the_same_changes(ITextMergeStrategy strategy)
    {
        var network = WithHistory(strategy);
        var original = network[0];

        var loaded = Port.Admitted(strategy.LoadReplica(original.Id, original.EncodeState().Span));

        Assert.Equal(original.Text, loaded.Text);
        Assert.Equal(original.Version, loaded.Version);
        var version = Port.Admitted(strategy.DecodeVersion(original.Version.Encoded.Span));
        Assert.Equal(original.Version, version);
    }

    public static void A_replica_loaded_from_encoded_state_converges_with_the_others_on_later_changes(ITextMergeStrategy strategy)
    {
        var network = WithHistory(strategy);
        var rebuilt = new ReplicaId(Guid.Parse("b0b0b0b0-0000-4000-8000-000000000001"));

        var loaded = Port.Admitted(strategy.LoadReplica(rebuilt, network[0].EncodeState().Span));

        var later = new[] { network.Insert(0, 0, "A"), network.Insert(1, 3, "B"), network.Delete(2, 1, 2) };
        var own = loaded.Insert(loaded.Text.Length, "C");

        network.DeliverAll(Delivery.Reversed);

        foreach (var change in later)
        {
            Network.Admit(loaded, change);
        }

        for (var replica = 0; replica < network.Size; replica++)
        {
            Network.Admit(network[replica], own);
        }

        network.AssertConverged();
        Assert.Equal(network[0].Text, loaded.Text);
    }

    public static void A_replica_resumed_from_its_encoded_state_never_reissues_a_change_identity(ITextMergeStrategy strategy)
    {
        var network = WithHistory(strategy);
        var before = network.Sent.Select(sent => sent.Change.Id).ToHashSet();

        // The server rebuilds its replica under its own identity after a restart.
        var resumed = Port.Admitted(strategy.LoadReplica(network[0].Id, network[0].EncodeState().Span));

        var next = resumed.Insert(0, "resumed ");

        Assert.DoesNotContain(next.Id, before);

        Network.Admit(network[1], next);

        Assert.Equal(resumed.Text, network[1].Text);
    }

    /// <summary>
    /// Three replicas with a history of local and remote inserts and deletes, all delivered.
    /// </summary>
    private static Network WithHistory(ITextMergeStrategy strategy)
    {
        var network = new Network(strategy, 3);

        network.Seed("The quick brown fox");
        network.Insert(1, 4, "very ");
        network.Delete(2, 10, 6);
        network.Insert(0, 0, "🦊 ");
        network.DeliverEachInItsOwnOrder(seed: 7);
        network.Replace(1, 0, 3, "A");
        network.DeliverAll(Delivery.AsSent);
        network.AssertConverged();

        return network;
    }
}
