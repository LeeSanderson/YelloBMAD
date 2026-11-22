using Xunit;

namespace Yello.Tests.Merge.Properties;

/// <summary>
/// The large interleaving budget: 5,000 sessions, on seeds the PR run never reaches. Explicit, so a plain <c>dotnet test</c> never
/// starts it; the Nightly workflow runs it with <c>--explicit on</c>.
/// </summary>
[Trait("Suite", "Merge")]
[Trait("Priority", "P0")]
[Trait("Requirement", "NFR-4")]
[Trait("Requirement", "AR-18")]
[Trait("Requirement", "AD-12")]
[Trait("Tier", "Nightly")]
public sealed class InterleavingNightlyTests
{
    private const int NightlyBudget = 5_000;

    [Fact(Explicit = true)]
    public void Five_thousand_random_sessions_converge_with_nothing_lost_or_resurrected() =>
        InterleavingHarness.AssertHolds(
            Candidate.Strategy,
            Enumerable.Range(InterleavingPropertyTests.PrFirstSeed + InterleavingPropertyTests.PrBudget, NightlyBudget));
}
