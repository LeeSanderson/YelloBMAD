using Xunit;

namespace Yello.Tests.Merge.Conformance;

/// <summary>
/// The port's persistence shape (AD-13, AD-14): a change survives its encoding, and a replica survives its encoded state -
/// same text, same "has", still converging, and never reissuing an identity when it resumes.
/// </summary>
[Trait("Suite", "Merge")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-33")]
[Trait("Requirement", "AR-18")]
[Trait("Requirement", "AD-12")]
[Trait("Requirement", "AD-14")]
public sealed class RoundTripConformanceTests
{
    [Fact]
    public void A_change_decoded_from_its_encoding_is_the_same_change() =>
        MergeCases.A_change_decoded_from_its_encoding_is_the_same_change(Candidate.Strategy);

    [Fact]
    public void A_replica_loaded_from_its_encoded_state_has_the_same_text_and_the_same_changes() =>
        MergeCases.A_replica_loaded_from_its_encoded_state_has_the_same_text_and_the_same_changes(Candidate.Strategy);

    [Fact]
    public void A_replica_loaded_from_encoded_state_converges_with_the_others_on_later_changes() =>
        MergeCases.A_replica_loaded_from_encoded_state_converges_with_the_others_on_later_changes(Candidate.Strategy);

    [Fact]
    public void A_replica_resumed_from_its_encoded_state_never_reissues_a_change_identity() =>
        MergeCases.A_replica_resumed_from_its_encoded_state_never_reissues_a_change_identity(Candidate.Strategy);
}
