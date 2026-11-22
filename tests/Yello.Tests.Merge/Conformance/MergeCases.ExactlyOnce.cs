using Xunit;
using Yello.Merge;

namespace Yello.Tests.Merge.Conformance;

internal static partial class MergeCases
{
    public static void A_change_delivered_twice_appears_once(ITextMergeStrategy strategy)
    {
        var network = new Network(strategy, 2);

        network.Seed("draft");

        var change = network.Insert(0, 5, " one");

        network.Deliver(1, Delivery.AsSent);

        var text = network[1].Text;
        var version = network[1].Version;

        Network.Admit(network[1], change);

        Assert.Equal("draft one", text);
        Assert.Equal(text, network[1].Text);
        Assert.Equal(version, network[1].Version);
    }

    public static void A_change_arriving_after_a_reconnection_exchange_that_carried_it_appears_once(ITextMergeStrategy strategy)
    {
        var network = new Network(strategy, 2);

        network.Seed("draft");

        // The frame was sent and delayed; the exchange on reconnection carried the same change first.
        var delayed = network.Insert(0, 0, "late ");

        Network.Exchange(network[0], network[1]);

        var text = network[1].Text;

        network.Deliver(1, Delivery.AsSent);
        Network.Admit(network[1], delayed);

        Assert.Equal("late draft", text);
        Assert.Equal(text, network[1].Text);
        network.AssertConverged();
    }

    public static void Pending_changes_resent_after_a_reconnect_appear_once(ITextMergeStrategy strategy)
    {
        var network = new Network(strategy, 2);

        network.Seed("list:");
        network.Isolate(0);

        TextChange[] pending =
        [
            network.Insert(0, 5, " a"),
            network.Insert(0, 7, " b"),
            network.Delete(0, 0, 1),
        ];

        network.Reconnect(0);

        var text = network[1].Text;

        // The client cannot know which of its pending frames the server took before the connection dropped, so it
        // sends them all again.
        foreach (var change in pending)
        {
            Network.Admit(network[1], change);
        }

        Assert.Equal("ist: a b", text);
        Assert.Equal(text, network[1].Text);
        network.AssertConverged();
    }

    public static void A_change_keeps_its_identity_across_encoding_decoding_and_resending(ITextMergeStrategy strategy)
    {
        var network = new Network(strategy, 2);

        network.Seed("x");
        network.Isolate(0);

        var made = new[] { network.Insert(0, 1, "y"), network.Insert(0, 2, "z") };
        var resent = network[0].ChangesMissingFrom(network[1].Version);

        Assert.True(
            made.Select(change => change.Id).ToHashSet().SetEquals(resent.Select(change => change.Id)),
            "The reconnection exchange must resend exactly the changes made offline, under the identities they were made with.");

        foreach (var change in made)
        {
            var decoded = Port.Admitted(strategy.DecodeChange(change.Encoded.Span));
            Assert.Equal(change.Id, decoded.Id);
            Assert.Equal(change, decoded);
        }

        Assert.Equal(2, made.Select(change => change.Id).Distinct().Count());
    }
}
