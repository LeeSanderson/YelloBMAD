using Xunit;

namespace Yello.Tests.Merge.Conformance;

/// <summary>
/// Edits to different parts of a description both survive, and edits to the same part converge on one text, whatever
/// order each replica receives them in.
/// </summary>
[Trait("Suite", "Merge")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-31")]
[Trait("Requirement", "AR-18")]
[Trait("Requirement", "AD-12")]
public sealed class RegionConformanceTests
{
    [Fact]
    public void One_editor_typing_and_deleting_alone_is_seen_identically_by_every_replica() =>
        MergeCases.One_editor_typing_and_deleting_alone_is_seen_identically_by_every_replica(Candidate.Strategy);

    [Fact]
    public void Two_editors_inserting_in_different_paragraphs_both_keep_their_changes() =>
        MergeCases.Two_editors_inserting_in_different_paragraphs_both_keep_their_changes(Candidate.Strategy);

    [Fact]
    public void An_insert_and_a_delete_that_do_not_overlap_both_take_effect() =>
        MergeCases.An_insert_and_a_delete_that_do_not_overlap_both_take_effect(Candidate.Strategy);

    [Fact]
    public void Two_deletes_that_do_not_overlap_both_take_effect() =>
        MergeCases.Two_deletes_that_do_not_overlap_both_take_effect(Candidate.Strategy);

    [Fact]
    public void An_insert_at_the_start_and_an_insert_at_the_end_both_keep_their_changes() =>
        MergeCases.An_insert_at_the_start_and_an_insert_at_the_end_both_keep_their_changes(Candidate.Strategy);

    [Fact]
    public void Two_inserts_at_the_same_place_end_in_the_same_text_everywhere_and_both_survive() =>
        MergeCases.Two_inserts_at_the_same_place_end_in_the_same_text_everywhere_and_both_survive(Candidate.Strategy);

    [Fact]
    public void Overlapping_deletes_end_in_the_same_text_everywhere_with_nothing_deleted_coming_back() =>
        MergeCases.Overlapping_deletes_end_in_the_same_text_everywhere_with_nothing_deleted_coming_back(Candidate.Strategy);

    [Fact]
    public void An_insert_inside_a_range_another_editor_deletes_ends_in_the_same_text_everywhere_and_survives() =>
        MergeCases.An_insert_inside_a_range_another_editor_deletes_ends_in_the_same_text_everywhere_and_survives(Candidate.Strategy);

    [Fact]
    public void A_replace_against_an_insert_in_the_same_range_ends_in_the_same_text_everywhere() =>
        MergeCases.A_replace_against_an_insert_in_the_same_range_ends_in_the_same_text_everywhere(Candidate.Strategy);
}
