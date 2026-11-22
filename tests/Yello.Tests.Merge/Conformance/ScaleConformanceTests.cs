using Xunit;

namespace Yello.Tests.Merge.Conformance;

/// <summary>
/// NFR-4 and FR-33 at the merge's own scale: ten simultaneous editors (M-5), convergence with no extra round (M-4's unit
/// half), and five minutes offline reconciled through the "has" exchange alone (M-6). Workload sizes are open question 7
/// (a); see <see cref="Workload"/>.
/// </summary>
[Trait("Suite", "Merge")]
[Trait("Priority", "P0")]
[Trait("Requirement", "NFR-4")]
[Trait("Requirement", "AR-18")]
[Trait("Requirement", "AD-12")]
public sealed class ScaleConformanceTests
{
    [Fact]
    public void Ten_editors_changing_one_description_at_once_end_with_identical_text_and_every_surviving_character_once() =>
        MergeCases.Ten_editors_changing_one_description_at_once_end_with_identical_text_and_every_surviving_character_once(Candidate.Strategy);

    [Fact]
    public void Once_every_change_has_arrived_the_texts_are_identical_with_no_further_message() =>
        MergeCases.Once_every_change_has_arrived_the_texts_are_identical_with_no_further_message(Candidate.Strategy);

    [Fact]
    [Trait("Requirement", "FR-33")]
    public void An_editor_offline_for_five_minutes_finds_its_own_changes_present_exactly_once() =>
        MergeCases.An_editor_offline_for_five_minutes_finds_its_own_changes_present_exactly_once(Candidate.Strategy);

    [Fact]
    [Trait("Requirement", "FR-33")]
    public void An_editor_offline_for_five_minutes_finds_the_others_changes_present() =>
        MergeCases.An_editor_offline_for_five_minutes_finds_the_others_changes_present(Candidate.Strategy);

    [Fact]
    [Trait("Requirement", "FR-33")]
    public void Two_editors_offline_at_once_cut_off_from_each_other_both_reconcile() =>
        MergeCases.Two_editors_offline_at_once_cut_off_from_each_other_both_reconcile(Candidate.Strategy);
}
