using Xunit;

namespace Yello.Tests.Merge.Conformance;

/// <summary>
/// FR-33 at the merge: a change counts once however many times it arrives - twice, after an exchange that already carried
/// it, or resent after a reconnect - because it keeps its identity through encoding and resending.
/// </summary>
[Trait("Suite", "Merge")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-33")]
[Trait("Requirement", "AR-18")]
[Trait("Requirement", "AD-12")]
public sealed class ExactlyOnceConformanceTests
{
    [Fact(Skip = RedPhase.NoCandidate)]
    public void A_change_delivered_twice_appears_once() =>
        MergeCases.A_change_delivered_twice_appears_once(Candidate.Strategy);

    [Fact(Skip = RedPhase.NoCandidate)]
    public void A_change_arriving_after_a_reconnection_exchange_that_carried_it_appears_once() =>
        MergeCases.A_change_arriving_after_a_reconnection_exchange_that_carried_it_appears_once(Candidate.Strategy);

    [Fact(Skip = RedPhase.NoCandidate)]
    public void Pending_changes_resent_after_a_reconnect_appear_once() =>
        MergeCases.Pending_changes_resent_after_a_reconnect_appear_once(Candidate.Strategy);

    [Fact(Skip = RedPhase.NoCandidate)]
    public void A_change_keeps_its_identity_across_encoding_decoding_and_resending() =>
        MergeCases.A_change_keeps_its_identity_across_encoding_decoding_and_resending(Candidate.Strategy);
}
