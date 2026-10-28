namespace Yello.Tests.Shared;

/// <summary>
/// The nearest-rank percentile of a set of measured durations - the one percentile the suites use for a p95 (NFR-5:
/// reads 300 ms, writes 500 ms, p95, server-side). Added by the story 2.9 ATDD run (Task 3: "Add a nearest-rank
/// percentile helper to <c>Yello.Tests.Shared</c> (none exists) with its own unit tests").
/// </summary>
/// <remarks>
/// <para>
/// <b>Nearest rank, not interpolation</b>: the value at rank <c>ceil(p / 100 x n)</c> of the sorted samples, so the p95
/// is always a duration that was actually measured, and with 100 samples it is the 95th smallest. Interpolating
/// between two samples would report a figure no request took.
/// </para>
/// <para>
/// <b>Not a second comparison method.</b> Whether two paths are indistinguishable is still
/// <see cref="DurationIndistinguishability"/>'s (median, 21 samples, 20%); this answers only "what is the p95 of these
/// samples", for a budget assertion.
/// </para>
/// </remarks>
public static class Percentile
{
    private const double Hundred = 100.0;

    /// <summary>
    /// The nearest-rank <paramref name="percentile"/> of <paramref name="samples"/>.
    /// </summary>
    /// <param name="samples">At least one measured value.</param>
    /// <param name="percentile">Greater than 0 and at most 100 - 95 for a p95.</param>
    /// <returns>The sample at rank <c>ceil(percentile / 100 x count)</c> in ascending order.</returns>
    /// <exception cref="ArgumentException">No samples were given.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="percentile"/> is not in (0, 100].</exception>
    public static double NearestRank(IReadOnlyCollection<double> samples, double percentile)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Count == 0)
        {
            throw new ArgumentException("A percentile of no samples is undefined.", nameof(samples));
        }

        if (percentile is <= 0 or > Hundred || double.IsNaN(percentile))
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), percentile, "A percentile is greater than 0 and at most 100.");
        }

        var sorted = samples.Order().ToArray();
        var rank = Convert.ToInt32(Math.Ceiling(percentile / Hundred * sorted.Length), System.Globalization.CultureInfo.InvariantCulture);

        return sorted[Math.Clamp(rank, 1, sorted.Length) - 1];
    }
}
