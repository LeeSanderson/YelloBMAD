using Xunit;

namespace Yello.Tests.Merge.Conformance;

/// <summary>
/// AD-11 at the merge: damaged bytes are refused with a value the caller branches on (open question 8 (a)), and refusing
/// leaves the replica untouched; a local change the server refused can be withdrawn without breaking convergence.
/// </summary>
[Trait("Suite", "Merge")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-33")]
[Trait("Requirement", "AR-18")]
[Trait("Requirement", "AR-20")]
[Trait("Requirement", "AD-11")]
[Trait("Requirement", "AD-12")]
public sealed class AdmissionConformanceTests
{
    [Theory]
    [InlineData(Damage.Empty)]
    [InlineData(Damage.FormatByteOnly)]
    [InlineData(Damage.Truncated)]
    [InlineData(Damage.TrailingBytes)]
    [InlineData(Damage.UnknownFormat)]
    public void A_damaged_change_is_refused_and_leaves_the_replica_as_it_was(Damage damage) =>
        MergeCases.A_damaged_change_is_refused_and_leaves_the_replica_as_it_was(Candidate.Strategy, damage);

    [Fact]
    public void A_damaged_state_or_version_is_refused() =>
        MergeCases.A_damaged_state_or_version_is_refused(Candidate.Strategy);

    [Fact]
    public void A_local_change_withdrawn_after_the_server_refused_it_leaves_the_replica_converged_with_one_that_never_saw_it() =>
        MergeCases.A_local_change_withdrawn_after_the_server_refused_it_leaves_the_replica_converged_with_one_that_never_saw_it(Candidate.Strategy);

    [Fact]
    public void A_replica_that_withdrew_a_change_keeps_editing_and_still_converges() =>
        MergeCases.A_replica_that_withdrew_a_change_keeps_editing_and_still_converges(Candidate.Strategy);
}
