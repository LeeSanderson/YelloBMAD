using Xunit;

namespace Yello.Tests.Merge.Conformance;

/// <summary>
/// The port's index unit (open question 9 (a)): UTF-16 code units, with anything outside the text or inside a surrogate
/// pair refused, and text beyond the basic plane converging with no broken pair at any replica.
/// </summary>
[Trait("Suite", "Merge")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-31")]
[Trait("Requirement", "AR-18")]
[Trait("Requirement", "AD-12")]
public sealed class TextIndexConformanceTests
{
    [Fact(Skip = RedPhase.NoCandidate)]
    public void An_insert_outside_the_text_is_refused_and_changes_nothing() =>
        MergeCases.An_insert_outside_the_text_is_refused_and_changes_nothing(Candidate.Strategy);

    [Fact(Skip = RedPhase.NoCandidate)]
    public void A_delete_reaching_outside_the_text_is_refused_and_changes_nothing() =>
        MergeCases.A_delete_reaching_outside_the_text_is_refused_and_changes_nothing(Candidate.Strategy);

    [Fact(Skip = RedPhase.NoCandidate)]
    public void An_edit_that_would_split_a_surrogate_pair_is_refused_and_changes_nothing() =>
        MergeCases.An_edit_that_would_split_a_surrogate_pair_is_refused_and_changes_nothing(Candidate.Strategy);

    [Fact(Skip = RedPhase.NoCandidate)]
    public void An_edit_on_whole_surrogate_pairs_is_accepted() =>
        MergeCases.An_edit_on_whole_surrogate_pairs_is_accepted(Candidate.Strategy);

    [Fact(Skip = RedPhase.NoCandidate)]
    public void Text_with_characters_outside_the_basic_plane_converges_with_no_broken_pair_anywhere() =>
        MergeCases.Text_with_characters_outside_the_basic_plane_converges_with_no_broken_pair_anywhere(Candidate.Strategy);
}
