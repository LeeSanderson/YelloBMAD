using Xunit;
using Yello.Merge;

namespace Yello.Tests.Merge.Conformance;

internal static partial class MergeCases
{
    public static void A_damaged_change_is_refused_and_leaves_the_replica_as_it_was(ITextMergeStrategy strategy, Damage damage)
    {
        var network = new Network(strategy, 2);

        network.Seed("kept");

        var valid = network.Insert(0, 4, " as is");
        var damaged = Damaged(valid.Encoded.Span, damage);
        var expected = damage == Damage.UnknownFormat ? ChangeAdmission.UnsupportedVersion : ChangeAdmission.Malformed;
        var receiver = network[1];
        var text = receiver.Text;
        var version = receiver.Version;

        var decoded = strategy.DecodeChange(damaged);

        Assert.Equal(expected, decoded.Admission);
        Assert.Null(decoded.Value);
        Assert.Equal(expected, receiver.Apply(new TextChange(valid.Id, damaged)));
        Assert.Equal(text, receiver.Text);
        Assert.Equal(version, receiver.Version);

        // Refusing the damaged copy did not poison the identity: the intact change is still admitted.
        Network.Admit(receiver, valid);
        Assert.Equal("kept as is", receiver.Text);
    }

    public static void A_damaged_state_or_version_is_refused(ITextMergeStrategy strategy)
    {
        var network = new Network(strategy, 1);

        network.Seed("state");

        var state = network[0].EncodeState().Span;
        var version = network[0].Version.Encoded.Span;

        var truncated = strategy.LoadReplica(network[0].Id, Damaged(state, Damage.Truncated));

        Assert.Equal(ChangeAdmission.Malformed, truncated.Admission);
        Assert.Null(truncated.Value);
        var unknown = strategy.LoadReplica(network[0].Id, Damaged(state, Damage.UnknownFormat));

        Assert.Equal(ChangeAdmission.UnsupportedVersion, unknown.Admission);
        Assert.Null(unknown.Value);
        var empty = strategy.DecodeVersion(Damaged(version, Damage.Empty));

        Assert.Equal(ChangeAdmission.Malformed, empty.Admission);
        Assert.Null(empty.Value);
        var future = strategy.DecodeVersion(Damaged(version, Damage.UnknownFormat));

        Assert.Equal(ChangeAdmission.UnsupportedVersion, future.Admission);
        Assert.Null(future.Value);
    }

    public static void A_local_change_withdrawn_after_the_server_refused_it_leaves_the_replica_converged_with_one_that_never_saw_it(ITextMergeStrategy strategy)
    {
        var network = new Network(strategy, 2);

        network.Seed("base");
        network.Isolate(0);

        // Made while the frame was in flight; the server refused it, so it was never relayed.
        var refused = network.Insert(0, 0, "REFUSED ");

        network.Insert(1, 4, " accepted");
        network[0].Withdraw(refused.Id);
        Network.Exchange(network[0], network[1]);

        Assert.Equal("base accepted", network[1].Text);
        Assert.Equal(network[1].Text, network[0].Text);
    }

    public static void A_replica_that_withdrew_a_change_keeps_editing_and_still_converges(ITextMergeStrategy strategy)
    {
        var network = new Network(strategy, 2);

        network.Seed("base");

        var refused = network[0].Insert(4, " refused");

        network[0].Withdraw(refused.Id);

        var next = network.Insert(0, 4, " next");

        network.Insert(1, 0, ">");
        network.DeliverAll(Delivery.AsSent);
        Network.Exchange(network[0], network[1]);

        Assert.NotEqual(refused.Id, next.Id);
        Assert.Equal(">base next", network[0].Text);
        network.AssertConverged();
    }

    private static byte[] Damaged(ReadOnlySpan<byte> valid, Damage damage) => damage switch
    {
        Damage.Empty => [],
        Damage.FormatByteOnly => [valid[0]],
        Damage.Truncated => valid[..^1].ToArray(),
        Damage.TrailingBytes => [.. valid, 0x00, 0x7F],
        Damage.UnknownFormat => [byte.CreateTruncating(valid[0] + 1), .. valid[1..]],
        _ => throw new ArgumentOutOfRangeException(nameof(damage), damage, "Unknown damage."),
    };
}
