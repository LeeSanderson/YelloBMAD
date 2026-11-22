using System.Diagnostics;
using Xunit;

namespace Yello.Tests.Merge.Conformance;

/// <summary>
/// M-4's unit half: the merge's own share of NFR-4's two seconds, on the native runtime (open question 6 (a)). One remote
/// change applies within a frame, because the browser applies it on its UI thread; a whole five-minute reconnection
/// exchange applies within half of NFR-4's budget, leaving the rest to transport.
/// </summary>
/// <remarks>
/// Native figures, not the browser's: the WebAssembly runtime is measured where the editor is (R15). Each budget is a
/// nearest-rank p95 of at least 100 samples after a warm-up.
/// </remarks>
[Trait("Suite", "Merge")]
[Trait("Priority", "P1")]
[Trait("Requirement", "NFR-4")]
[Trait("Requirement", "NFR-3")]
[Trait("Requirement", "AR-18")]
[Trait("Requirement", "AD-12")]
[Collection(nameof(MergeTimingCollection))]
public sealed class MergeBudgetTests
{
    private const int Samples = 100;

    private static readonly TimeSpan OneFrame = TimeSpan.FromMilliseconds(16);

    private static readonly TimeSpan HalfOfNfr4 = TimeSpan.FromSeconds(1);

    [Fact(Skip = RedPhase.NoCandidateBudget)]
    public void One_remote_change_to_a_working_size_description_applies_within_a_frame()
    {
        var strategy = Candidate.Strategy;
        var network = new Network(strategy, 2);
        var glyphs = new Glyphs();
        var random = new Random(0x4_0016);

        for (var typed = 0; typed < Workload.WorkingDocumentLength; typed += 50)
        {
            network.Insert(0, network[0].Text.Length, glyphs.Take(50));
        }

        network.DeliverAll(Delivery.AsSent);

        var durations = new List<TimeSpan>();

        for (var sample = -10; sample < Samples; sample++)
        {
            var change = network[0].Insert(random.Next(network[0].Text.Length + 1), glyphs.Take(1));
            var started = Stopwatch.GetTimestamp();

            Network.Admit(network[1], change);

            if (sample >= 0)
            {
                durations.Add(Stopwatch.GetElapsedTime(started));
            }
        }

        Assert.Equal(network[0].Text, network[1].Text);
        AssertWithin(OneFrame, durations, "applying one remote change to a 20,000-character description");
    }

    [Fact(Skip = RedPhase.NoCandidateBudget)]
    public void A_five_minute_reconnection_exchange_applies_within_half_of_the_convergence_budget()
    {
        var strategy = Candidate.Strategy;
        var network = MergeCases.FiveMinutesApart(strategy, 3);
        var offline = network[3].EncodeState().ToArray();
        var connected = network[0].EncodeState().ToArray();
        var durations = new List<TimeSpan>();

        for (var sample = -3; sample < Samples; sample++)
        {
            var returning = Port.Admitted(strategy.LoadReplica(network[3].Id, offline));
            var peer = Port.Admitted(strategy.LoadReplica(network[0].Id, connected));

            var started = Stopwatch.GetTimestamp();

            Network.Exchange(returning, peer);

            if (sample >= 0)
            {
                durations.Add(Stopwatch.GetElapsedTime(started));
            }

            Assert.Equal(peer.Text, returning.Text);
        }

        AssertWithin(HalfOfNfr4, durations, "the reconnection exchange after five minutes offline");
    }

    private static void AssertWithin(TimeSpan budget, List<TimeSpan> durations, string what)
    {
        var ordered = durations.Order().ToList();
        var p95 = ordered[int.CreateChecked(Math.Ceiling(0.95 * ordered.Count)) - 1];

        Assert.True(
            p95 <= budget,
            $"The p95 of {what} is {p95.TotalMilliseconds:0.###} ms over {ordered.Count} samples, above the {budget.TotalMilliseconds} ms budget (native runtime). Median {ordered[ordered.Count / 2].TotalMilliseconds:0.###} ms.");
    }
}

/// <summary>
/// The merge's timing cases run alone, so no other class's work moves their samples.
/// </summary>
[CollectionDefinition(nameof(MergeTimingCollection), DisableParallelization = true)]
public sealed class MergeTimingCollection;
