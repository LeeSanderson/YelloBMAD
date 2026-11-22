using Xunit;
using Yello.Merge;

namespace Yello.Tests.Merge.Conformance;

internal static partial class MergeCases
{
    public static void A_change_withdrawn_before_a_reload_under_a_new_identity_is_never_resurrected(ITextMergeStrategy strategy)
    {
        var original = strategy.CreateReplica(Network.IdOf(0));
        var kept = original.Insert(0, "base");
        var refused = original.Insert(4, " refused");

        original.Withdraw(refused.Id);

        // A panel reopened on the same browser state takes a fresh identity; the server may still redeliver the old change.
        var reopened = Port.Admitted(strategy.LoadReplica(Network.IdOf(1), original.EncodeState().Span));
        var redelivered = reopened.Apply(refused);
        var peer = strategy.CreateReplica(Network.IdOf(2));

        Network.Admit(peer, kept);
        Network.Exchange(reopened, peer);

        Assert.NotEqual(ChangeAdmission.UnsupportedVersion, redelivered);
        Assert.Equal("base", reopened.Text);
        Assert.Equal(peer.Text, reopened.Text);
        Assert.DoesNotContain(refused.Id, reopened.ChangesMissingFrom(strategy.CreateReplica(Network.IdOf(3)).Version).Select(change => change.Id));
    }

    public static void A_version_decoded_from_its_bytes_asks_for_exactly_what_the_version_itself_asks_for(ITextMergeStrategy strategy)
    {
        var network = WithHistory(strategy);
        var behind = strategy.CreateReplica(Network.IdOf(4));

        Network.Admit(behind, network.Sent[0].Change);

        var decoded = Port.Admitted(strategy.DecodeVersion(behind.Version.Encoded.Span));

        Assert.Equal(behind.Version, decoded);
        Assert.Equal(
            network[0].ChangesMissingFrom(behind.Version).Select(change => change.Id),
            network[0].ChangesMissingFrom(decoded).Select(change => change.Id));
    }
}
