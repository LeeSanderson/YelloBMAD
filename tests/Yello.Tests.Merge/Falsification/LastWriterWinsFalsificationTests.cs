using Xunit;
using Xunit.Sdk;
using Yello.Merge;
using Yello.Tests.Merge.Conformance;
using Yello.Tests.Merge.Properties;

namespace Yello.Tests.Merge.Falsification;

/// <summary>
/// Whole-field last-writer-wins fails the conformance suite. The architecture says it cannot pass; this asserts it,
/// so the suite cannot quietly become one that any merge passes, or one written to whichever candidate was chosen.
/// </summary>
/// <remarks>
/// The same case bodies the conformance facts run, against <see cref="WholeFieldLastWriterWins"/>. Each must fail with an
/// assertion, never an exception from the double, and the double must pass a lone editor's case: otherwise "it fails"
/// could only mean "the double is broken".
/// </remarks>
[Trait("Suite", "Merge")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-18")]
[Trait("Requirement", "AD-12")]
[Trait("Requirement", "FR-31")]
[Trait("Requirement", "FR-33")]
public sealed class LastWriterWinsFalsificationTests
{
    private static readonly Dictionary<string, Action<ITextMergeStrategy>> Cases = new(StringComparer.Ordinal)
    {
        [nameof(MergeCases.Two_editors_inserting_in_different_paragraphs_both_keep_their_changes)] =
            MergeCases.Two_editors_inserting_in_different_paragraphs_both_keep_their_changes,
        [nameof(MergeCases.An_insert_and_a_delete_that_do_not_overlap_both_take_effect)] =
            MergeCases.An_insert_and_a_delete_that_do_not_overlap_both_take_effect,
        [nameof(MergeCases.Two_deletes_that_do_not_overlap_both_take_effect)] =
            MergeCases.Two_deletes_that_do_not_overlap_both_take_effect,
        [nameof(MergeCases.An_insert_at_the_start_and_an_insert_at_the_end_both_keep_their_changes)] =
            MergeCases.An_insert_at_the_start_and_an_insert_at_the_end_both_keep_their_changes,
        [nameof(MergeCases.An_editor_offline_for_five_minutes_finds_its_own_changes_present_exactly_once)] =
            MergeCases.An_editor_offline_for_five_minutes_finds_its_own_changes_present_exactly_once,
        [nameof(MergeCases.An_editor_offline_for_five_minutes_finds_the_others_changes_present)] =
            MergeCases.An_editor_offline_for_five_minutes_finds_the_others_changes_present,
    };

    [Theory]
    [InlineData(nameof(MergeCases.Two_editors_inserting_in_different_paragraphs_both_keep_their_changes))]
    [InlineData(nameof(MergeCases.An_insert_and_a_delete_that_do_not_overlap_both_take_effect))]
    [InlineData(nameof(MergeCases.Two_deletes_that_do_not_overlap_both_take_effect))]
    [InlineData(nameof(MergeCases.An_insert_at_the_start_and_an_insert_at_the_end_both_keep_their_changes))]
    [InlineData(nameof(MergeCases.An_editor_offline_for_five_minutes_finds_its_own_changes_present_exactly_once))]
    [InlineData(nameof(MergeCases.An_editor_offline_for_five_minutes_finds_the_others_changes_present))]
    public void Whole_field_last_writer_wins_fails_the_case_on_its_merge_semantics(string conformanceCase)
    {
        var failure = Record.Exception(() => Cases[conformanceCase](new WholeFieldLastWriterWins()));

        Assert.True(failure is not null, $"Whole-field last-writer-wins passed '{conformanceCase}', so that case cannot tell a merge from an overwrite.");
        Assert.True(
            failure is IAssertionException,
            $"'{conformanceCase}' failed on an exception from the double, not on an assertion, so it proves nothing about merge semantics:{Environment.NewLine}{failure}");
    }

    [Fact]
    public void Whole_field_last_writer_wins_passes_a_lone_editors_case_so_its_failures_are_not_its_own_bugs() =>
        MergeCases.One_editor_typing_and_deleting_alone_is_seen_identically_by_every_replica(new WholeFieldLastWriterWins());

    [Fact]
    public void Whole_field_last_writer_wins_passes_the_cases_it_has_no_merge_to_get_wrong()
    {
        var lww = new WholeFieldLastWriterWins();

        MergeCases.A_change_delivered_twice_appears_once(lww);
        MergeCases.A_change_decoded_from_its_encoding_is_the_same_change(lww);
        MergeCases.A_replica_loaded_from_its_encoded_state_has_the_same_text_and_the_same_changes(lww);
        MergeCases.A_damaged_state_or_version_is_refused(lww);
        MergeCases.An_edit_that_would_split_a_surrogate_pair_is_refused_and_changes_nothing(lww);

        foreach (var damage in Enum.GetValues<Damage>())
        {
            MergeCases.A_damaged_change_is_refused_and_leaves_the_replica_as_it_was(lww, damage);
        }
    }

    [Fact]
    public void The_interleaving_harness_catches_whole_field_last_writer_wins_on_its_merge_semantics()
    {
        var failure = InterleavingHarness.FirstFailure(new WholeFieldLastWriterWins(), Enumerable.Range(InterleavingPropertyTests.PrFirstSeed, 20));

        Assert.True(failure is not null, "Twenty random sessions all passed against whole-field last-writer-wins: the harness explores nothing that matters.");
        Assert.True(
            failure.Value.Outcome.Kind == OutcomeKind.Violated,
            $"Seed {failure.Value.Seed} failed on an exception, not a violated property:{Environment.NewLine}{failure.Value.Outcome.Detail}");
    }
}
