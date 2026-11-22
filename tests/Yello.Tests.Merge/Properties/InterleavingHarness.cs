using System.Text;
using Xunit;
using Xunit.Sdk;
using Yello.Merge;

namespace Yello.Tests.Merge.Properties;

/// <summary>
/// A seeded property harness over random editing sessions: 2 to 10 replicas, random inserts, deletes and replaces,
/// random partial deliveries in random orders, duplicates, reloads of a replica from its own encoded state, and partitions
/// healed by the reconnection exchange over versions decoded from their bytes. Written by
/// hand because the repository has no property-testing package, and adding one needs Lee's approval.
/// </summary>
/// <remarks>
/// <para>
/// After every session, all replicas are reconnected and every inbox drained, and three properties must hold: every
/// replica holds the same text; every inserted character that no delete removed is present exactly once; no deleted
/// character is present. Every text must also be well-formed UTF-16.
/// </para>
/// <para>
/// A failing session is shrunk by removing steps one at a time while it still fails, and the message carries the seed
/// and that minimal trace. A seed is replayed alone by <see cref="InterleavingPropertyTests.A_recorded_seed_still_holds"/>.
/// </para>
/// </remarks>
internal static class InterleavingHarness
{
    private const int ShrinkBudget = 2_000;

    public static Session Generate(int seed)
    {
        var random = new Random(seed);
        var replicas = random.Next(2, 11);
        var steps = new List<SessionStep> { new InsertStep(0, 0, random.Next(5, 15), Wide: false) };
        var count = random.Next(10, 60);

        for (var i = 0; i < count; i++)
        {
            steps.Add(NextStep(random, replicas));
        }

        return new Session(seed, replicas, steps);
    }

    public static Outcome Run(ITextMergeStrategy strategy, Session session)
    {
        try
        {
            var network = new Network(strategy, session.Replicas);
            var glyphs = new Glyphs();

            foreach (var step in session.Steps)
            {
                step.Run(network, glyphs);
            }

            for (var replica = 0; replica < network.Size; replica++)
            {
                if (network.IsIsolated(replica))
                {
                    network.Reconnect(replica);
                }
            }

            network.DeliverEachInItsOwnOrder(session.Seed);

            network.AssertConverged();
            network.AssertNothingLostOrResurrected(_ => true, "After every change was delivered");
            Assert.All(network.Texts, text => Assert.True(Utf16.IsWellFormed(text), "A replica holds an unpaired surrogate."));

            return Outcome.Passed;
        }
        catch (Exception exception) when (exception is IAssertionException)
        {
            return new Outcome(OutcomeKind.Violated, exception.Message);
        }
#pragma warning disable CA1031 // Any exception the strategy throws is a failing session to shrink and report, not a crash.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return new Outcome(OutcomeKind.Threw, exception.ToString());
        }
    }

    /// <summary>
    /// Runs each seed and fails with the first failing seed's minimal trace.
    /// </summary>
    public static void AssertHolds(ITextMergeStrategy strategy, IEnumerable<int> seeds)
    {
        foreach (var seed in seeds)
        {
            var session = Generate(seed);
            var outcome = Run(strategy, session);

            if (outcome.Kind != OutcomeKind.Passed)
            {
                Assert.Fail(Describe(session, Shrink(strategy, session), outcome));
            }
        }
    }

    /// <summary>
    /// The first seed whose session fails, and how, or <c>null</c> when every session holds.
    /// </summary>
    public static (int Seed, Outcome Outcome)? FirstFailure(ITextMergeStrategy strategy, IEnumerable<int> seeds)
    {
        foreach (var seed in seeds)
        {
            var outcome = Run(strategy, Generate(seed));

            if (outcome.Kind != OutcomeKind.Passed)
            {
                return (seed, outcome);
            }
        }

        return null;
    }

    public static Session Shrink(ITextMergeStrategy strategy, Session failing)
    {
        var steps = failing.Steps.ToList();
        var runs = 0;
        var removed = true;

        while (removed && runs < ShrinkBudget)
        {
            removed = false;

            for (var i = steps.Count - 1; i >= 0 && runs < ShrinkBudget; i--)
            {
                var candidate = steps.Where((_, index) => index != i).ToList();

                runs++;

                if (Run(strategy, failing with { Steps = candidate }).Kind != OutcomeKind.Passed)
                {
                    steps = candidate;
                    removed = true;
                }
            }
        }

        return failing with { Steps = steps };
    }

    private static string Describe(Session original, Session minimal, Outcome outcome)
    {
        var text = new StringBuilder()
            .AppendLine($"Seed {original.Seed} ({original.Replicas} replicas) fails ({outcome.Kind}). Replay it with A_recorded_seed_still_holds.")
            .AppendLine($"Minimal trace, {minimal.Steps.Count} of {original.Steps.Count} steps:");

        for (var i = 0; i < minimal.Steps.Count; i++)
        {
            text.AppendLine($"  {i + 1}. {minimal.Steps[i]}");
        }

        return text.AppendLine("then every replica reconnects and every inbox is drained.").AppendLine(outcome.Detail).ToString();
    }

    private static SessionStep NextStep(Random random, int replicas)
    {
        var replica = random.Next(replicas);
        var roll = random.Next(100);

        return roll switch
        {
            < 30 => new InsertStep(replica, random.NextDouble(), random.Next(1, 4), Wide: random.Next(5) == 0),
            < 45 => new DeleteStep(replica, random.NextDouble(), random.Next(1, 4)),
            < 55 => new ReplaceStep(replica, random.NextDouble(), random.Next(1, 4), random.Next(1, 4)),
            < 80 => new DeliverStep(replica, random.Next(1, 6), random.Next()),
            < 87 => new DuplicateStep(replica, random.NextDouble()),
            < 92 => new IsolateStep(replica),
            < 97 => new ReconnectStep(replica),
            _ => new ReloadStep(replica),
        };
    }
}

internal sealed record Session(int Seed, int Replicas, IReadOnlyList<SessionStep> Steps);

internal enum OutcomeKind
{
    Passed,
    Violated,
    Threw,
}

internal sealed record Outcome(OutcomeKind Kind, string Detail)
{
    public static Outcome Passed { get; } = new(OutcomeKind.Passed, string.Empty);
}
