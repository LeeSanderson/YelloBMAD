using Xunit;

namespace Yello.Tests.Merge.Properties;

/// <summary>
/// M-9: orderings nobody would enumerate by hand, generated from seeds. The PR run explores a fixed budget; recorded seeds
/// replay one session each, and a seed that ever fails is added to <see cref="RecordedSeeds"/> for good.
/// </summary>
[Trait("Suite", "Merge")]
[Trait("Priority", "P0")]
[Trait("Requirement", "NFR-4")]
[Trait("Requirement", "FR-31")]
[Trait("Requirement", "FR-33")]
[Trait("Requirement", "AR-18")]
[Trait("Requirement", "AD-12")]
public sealed class InterleavingPropertyTests
{
    /// <summary>
    /// Open question 10 (a): 200 sessions on every PR run; <see cref="InterleavingNightlyTests"/> runs 5,000.
    /// </summary>
    internal const int PrBudget = 200;

    internal const int PrFirstSeed = 1;

    public static TheoryData<int> RecordedSeeds { get; } = [1, 2, 3, 42, 2026];

    [Fact(Skip = RedPhase.NoCandidateProperty)]
    public void Random_sessions_converge_with_nothing_lost_or_resurrected() =>
        InterleavingHarness.AssertHolds(Candidate.Strategy, Enumerable.Range(PrFirstSeed, PrBudget));

    [Theory(Skip = RedPhase.NoCandidateProperty)]
    [MemberData(nameof(RecordedSeeds))]
    public void A_recorded_seed_still_holds(int seed) =>
        InterleavingHarness.AssertHolds(Candidate.Strategy, [seed]);
}
