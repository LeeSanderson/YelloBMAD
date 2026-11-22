using System.Diagnostics;
using Xunit;
using Yello.Merge;

namespace Yello.Tests.Merge.Conformance;

/// <summary>
/// The server's rebuild of one description, measured where it runs: a working-size description loaded from its latest
/// snapshot, then every change after it decoded and applied, as each append does under the Task's lock. The figure is the
/// cost collecting deleted characters would reduce, so it is the number that decision is made against.
/// </summary>
/// <remarks>
/// Native figures. The snapshot holds 15,000 characters and 5,000 single-character changes follow it, so the rebuilt text is
/// 20,000 characters with at least 5,000 rows since the snapshot. The budget is a nearest-rank p95 of the samples after a
/// warm-up; fewer samples than <see cref="MergeBudgetTests"/>, because one sample is a whole rebuild.
/// </remarks>
[Trait("Suite", "Merge")]
[Trait("Priority", "P1")]
[Trait("Requirement", "AR-19")]
[Trait("Requirement", "NFR-4")]
[Trait("Requirement", "AD-13")]
[Collection(nameof(MergeTimingCollection))]
public sealed class DescriptionRebuildBudgetTests
{
    private const int Samples = 20;

    private const int ChangesSinceSnapshot = 5_000;

    private static readonly TimeSpan RebuildBudget = TimeSpan.FromMilliseconds(200);

    private static readonly ReplicaId Server = new(Guid.Parse("7a5c0000-0000-4000-8000-000000000002"));

    [Fact]
    public void A_working_size_description_rebuilds_from_its_snapshot_and_5000_later_changes_within_the_budget()
    {
        var strategy = Candidate.Strategy;
        var editor = strategy.CreateReplica(Network.IdOf(0));
        var glyphs = new Glyphs();
        var random = new Random(0x7_0002);

        while (editor.Text.Length < Workload.WorkingDocumentLength - ChangesSinceSnapshot)
        {
            editor.Insert(editor.Text.Length, glyphs.Take(50));
        }

        var snapshot = editor.EncodeState().ToArray();
        var later = new List<byte[]>(ChangesSinceSnapshot);

        for (var change = 0; change < ChangesSinceSnapshot; change++)
        {
            var at = Utf16.IndexOf(editor.Text, random.Next(Utf16.ScalarCount(editor.Text) + 1));

            later.Add(editor.Insert(at, glyphs.Take(1)).Encoded.ToArray());
        }

        var durations = new List<TimeSpan>();

        for (var sample = -3; sample < Samples; sample++)
        {
            var started = Stopwatch.GetTimestamp();
            var rebuilt = Port.Admitted(strategy.LoadReplica(Server, snapshot));

            foreach (var bytes in later)
            {
                Network.Admit(rebuilt, Port.Admitted(strategy.DecodeChange(bytes)));
            }

            var elapsed = Stopwatch.GetElapsedTime(started);

            Assert.Equal(editor.Text, rebuilt.Text);

            if (sample >= 0)
            {
                durations.Add(elapsed);
            }
        }

        var ordered = durations.Order().ToList();
        var p95 = ordered[int.CreateChecked(Math.Ceiling(0.95 * ordered.Count)) - 1];

        Assert.True(
            p95 <= RebuildBudget,
            $"The p95 of rebuilding a {editor.Text.Length}-character description from its snapshot and {ChangesSinceSnapshot} later changes is {p95.TotalMilliseconds:0.###} ms over {ordered.Count} samples, above the {RebuildBudget.TotalMilliseconds} ms budget (native runtime). Median {ordered[ordered.Count / 2].TotalMilliseconds:0.###} ms.");
    }
}
