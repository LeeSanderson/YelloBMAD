using Xunit;

namespace Yello.Tests.Merge.Conformance;

/// <summary>
/// What loading persisted state must keep before any state is persisted: a change the replica withdrew stays withdrawn
/// when the state is reloaded under a new identity, as an editor reopened in a new panel is, and a version read back from
/// its bytes asks for exactly what the version itself does.
/// </summary>
[Trait("Suite", "Merge")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-18")]
[Trait("Requirement", "AD-12")]
[Trait("Requirement", "AD-14")]
[Trait("Requirement", "AR-19")]
public sealed class LoadConformanceTests
{
    [Fact]
    public void A_change_withdrawn_before_a_reload_under_a_new_identity_is_never_resurrected() =>
        MergeCases.A_change_withdrawn_before_a_reload_under_a_new_identity_is_never_resurrected(Candidate.Strategy);

    [Fact]
    public void A_version_decoded_from_its_bytes_asks_for_exactly_what_the_version_itself_asks_for() =>
        MergeCases.A_version_decoded_from_its_bytes_asks_for_exactly_what_the_version_itself_asks_for(Candidate.Strategy);
}
