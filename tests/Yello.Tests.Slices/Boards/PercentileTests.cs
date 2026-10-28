using Xunit;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Boards;

/// <summary>
/// The nearest-rank percentile the Nightly and Weekly budget cases read their p95 with (<see cref="Percentile"/>), proved
/// against known answers before any figure is trusted - as <c>CieDe2000</c> was before the palette (story 2.4). Story 2.9
/// Task 3. Written by the story 2.9 ATDD run with the helper; green from the start, a standing gate.
/// </summary>
/// <remarks>
/// No fixture and no database: a container-backed class fails entirely when the runtime is down, and this needs no rows.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "NFR-5")]
public sealed class PercentileTests
{
    private static readonly double[] OneToHundred = [.. Enumerable.Range(1, 100).Select(value => value + 0.0)];

    /// <summary>
    /// With 100 samples 1..100, the p95 is the 95th smallest, the p50 the 50th and the p100 the largest.
    /// </summary>
    /// <param name="percentile">The percentile asked for.</param>
    /// <param name="expected">The sample at its nearest rank.</param>
    [Theory]
    [InlineData(95, 95)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(1, 1)]
    [InlineData(0.5, 1)]
    public void The_nearest_rank_of_one_to_a_hundred_is_the_sample_at_that_rank(double percentile, double expected) =>
        Assert.Equal(expected, Percentile.NearestRank(OneToHundred, percentile));

    /// <summary>
    /// The rank rounds up and the answer is always a measured sample, never an interpolation: of five samples the p95 is
    /// the fifth (ceil(4.75)), and of twenty the p95 is the nineteenth.
    /// </summary>
    [Fact]
    public void The_rank_rounds_up_and_the_answer_is_a_measured_sample()
    {
        Assert.Equal(50, Percentile.NearestRank([10, 20, 30, 40, 50], 95));
        Assert.Equal(19, Percentile.NearestRank([.. Enumerable.Range(1, 20).Select(value => value + 0.0)], 95));
        Assert.Equal(40, Percentile.NearestRank([10, 20, 30, 40, 50], 80));
    }

    /// <summary>
    /// The samples' order does not matter, and the caller's collection is not reordered.
    /// </summary>
    [Fact]
    public void The_samples_order_does_not_matter_and_is_not_changed()
    {
        double[] shuffled = [300, 12, 45, 7, 120];

        Assert.Equal(300, Percentile.NearestRank(shuffled, 95));
        Assert.Equal([300, 12, 45, 7, 120], shuffled);
    }

    /// <summary>
    /// No samples, and a percentile outside (0, 100], are refused rather than answered.
    /// </summary>
    [Fact]
    public void No_samples_or_a_percentile_outside_its_range_is_refused()
    {
        Assert.Throws<ArgumentException>(() => Percentile.NearestRank([], 95));
        Assert.Throws<ArgumentOutOfRangeException>(() => Percentile.NearestRank([1], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Percentile.NearestRank([1], 100.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => Percentile.NearestRank([1], double.NaN));
    }
}
