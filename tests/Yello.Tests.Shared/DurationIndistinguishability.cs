using System.Diagnostics;
using System.Globalization;

namespace Yello.Tests.Shared;

/// <summary>
/// The B3 method: deciding whether two code paths take indistinguishable time.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written once, here, because two decisions need it and three stories will.</b>
/// <c>test-design-architecture.md:113</c> assigns blocker B3 to "stories 1.3 and 1.6", and it
/// blocks P0 test I-7. Story 1.3 needs it for AD-23 - a duplicate registration must be
/// indistinguishable from a new one "in status, body, shape <i>and duration</i>". Story 1.6 needs
/// it for AD-3, where a refusal must not reveal whether the thing refused exists. Story 1.9
/// reuses it on both surfaces. <c>Yello.Tests.Shared</c> is the one project all three suites
/// reference.
/// </para>
/// <para>
/// <b>The four things the story requires this to state, stated:</b>
/// </para>
/// <list type="bullet">
///   <item><description>
///     <b>Sample size:</b> <see cref="MinimumSamples"/> per arm, interleaved. A single-sample
///     assertion is one draw from two distributions and detects nothing at all.
///   </description></item>
///   <item><description>
///     <b>Statistic:</b> the median of each arm. Not the mean - one garbage collection or one
///     container hiccup moves a mean by more than the effect being measured, and there is no
///     reason to let a single outlier decide a release gate.
///   </description></item>
///   <item><description>
///     <b>Tolerance:</b> <see cref="DefaultTolerance"/>, as a fraction of the slower median.
///   </description></item>
///   <item><description>
///     <b>Measurement point:</b> the caller's, and it must be server-side. Measuring in a browser
///     or across a network measures the network. Each sample times exactly the operation under
///     test and nothing around it.
///   </description></item>
/// </list>
/// <para>
/// <b>WHAT THIS DETECTS, AND WHAT IT DOES NOT.</b> Stating the second half is the point. The
/// effect it is built for is enormous: a skipped password hash removes roughly 270 ms from a
/// roughly 275 ms operation, which is a ~99% shift in the median. A 20% tolerance catches that
/// with room to spare while tolerating the ordinary noise of a developer machine. It is
/// <b>not</b> a side-channel analysis and cannot find a subtle timing leak - a few microseconds
/// of difference from an early string comparison would pass here comfortably. Anyone reaching for
/// this to prove a cryptographic timing property should not use it.
/// </para>
/// <para>
/// <b>Why this is more tractable here than it looks.</b> <c>test-design-architecture.md:312</c>
/// notes that <c>MAXDOP = 1</c> plus a single replica make variance unusually low in this
/// product - but only if the method is written down once rather than improvised twice, which is
/// what this class is for.
/// </para>
/// <para>
/// <b>It must be validated by planting.</b> An absence assertion not proved against a planted
/// signal is not a test (<c>TESTING-CONVENTIONS.md:93</c>). The caller's obligation is to run the
/// same comparison with the hash deliberately skipped and confirm it fails by name; story 1.3's
/// result is in its Dev Agent Record.
/// </para>
/// </remarks>
public static class DurationIndistinguishability
{
    /// <summary>
    /// Samples per arm. Enough that one outlier cannot move a median.
    /// </summary>
    public static int MinimumSamples => 21;

    /// <summary>
    /// How far the two medians may differ, as a fraction of the slower one.
    /// </summary>
    /// <remarks>
    /// Generous on purpose. The effect this exists to catch is a whole missing hash; the noise it
    /// has to tolerate is a developer laptop under a container runtime, where a 20% spread
    /// between medians is ordinary and means nothing. Tightening it would trade a real detection
    /// for flakes.
    /// </remarks>
    public static double DefaultTolerance => 0.20;

    /// <summary>
    /// Times two operations, interleaved, and reports whether their durations are
    /// indistinguishable.
    /// </summary>
    /// <param name="first">One path - by convention, the one expected to be slower if either is.</param>
    /// <param name="second">The other path.</param>
    /// <returns>The verdict, carrying both medians so a failure message can quote them.</returns>
    public static Task<DurationVerdict> CompareAsync(Func<Task> first, Func<Task> second) =>
        CompareAsync(first, second, MinimumSamples, DefaultTolerance);

    /// <summary>
    /// Times two operations, interleaved, and reports whether their durations are
    /// indistinguishable.
    /// </summary>
    /// <param name="first">One path - by convention, the one expected to be slower if either is.</param>
    /// <param name="second">The other path.</param>
    /// <param name="samples">
    /// Samples per arm. Raised to <see cref="MinimumSamples"/> if a caller asks for fewer - a
    /// comparison of three samples is not a cheaper version of this method, it is a different and
    /// useless one.
    /// </param>
    /// <param name="tolerance">The permitted gap, as a fraction of the slower median.</param>
    /// <returns>The verdict, carrying both medians so a failure message can quote them.</returns>
    /// <remarks>
    /// <b>Interleaved, not one arm then the other.</b> Measured while choosing this story's
    /// password work factor: whichever arm ran first read about 2.5x too fast, because the CPU is
    /// on turbo before sustained load pulls the clock down. Running one arm to completion and
    /// then the other therefore compares clock states as much as code paths, and would report a
    /// real difference where there is none - or hide one.
    /// </remarks>
    public static async Task<DurationVerdict> CompareAsync(
        Func<Task> first,
        Func<Task> second,
        int samples,
        double tolerance)
    {
        var count = Math.Max(samples, MinimumSamples);
        var allowed = tolerance;

        var firstSamples = new List<double>(count);
        var secondSamples = new List<double>(count);

        // A warm-up of each, outside the samples: the first call through any path pays JIT and
        // first-connection costs that have nothing to do with the comparison.
        await first().ConfigureAwait(false);
        await second().ConfigureAwait(false);

        for (var round = 0; round < count; round++)
        {
            firstSamples.Add(await TimeAsync(first).ConfigureAwait(false));
            secondSamples.Add(await TimeAsync(second).ConfigureAwait(false));
        }

        var firstMedian = Median(firstSamples);
        var secondMedian = Median(secondSamples);

        var slower = Math.Max(firstMedian, secondMedian);
        var difference = Math.Abs(firstMedian - secondMedian);

        // Guard against a degenerate zero: if both paths are instant there is nothing to
        // distinguish, and dividing by zero would report NaN as a pass.
        var relative = slower <= double.Epsilon ? 0 : difference / slower;

        return new DurationVerdict(firstMedian, secondMedian, relative, allowed);
    }

    private static async Task<double> TimeAsync(Func<Task> operation)
    {
        var stopwatch = Stopwatch.StartNew();
        await operation().ConfigureAwait(false);
        stopwatch.Stop();

        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private static double Median(List<double> samples)
    {
        samples.Sort();

        var middle = samples.Count / 2;

        return samples.Count % 2 == 1
            ? samples[middle]
            : (samples[middle - 1] + samples[middle]) / 2;
    }
}

/// <summary>
/// What <see cref="DurationIndistinguishability"/> concluded.
/// </summary>
/// <param name="FirstMedianMilliseconds">The first path's median.</param>
/// <param name="SecondMedianMilliseconds">The second path's median.</param>
/// <param name="RelativeDifference">The gap, as a fraction of the slower median.</param>
/// <param name="Tolerance">The fraction that was allowed.</param>
public sealed record DurationVerdict(
    double FirstMedianMilliseconds,
    double SecondMedianMilliseconds,
    double RelativeDifference,
    double Tolerance)
{
    /// <summary>
    /// Whether the two paths are indistinguishable at the stated tolerance.
    /// </summary>
    public bool AreIndistinguishable => RelativeDifference <= Tolerance;

    /// <summary>
    /// A failure message quoting both medians, so a failing run says what it actually saw rather
    /// than only that it was unhappy.
    /// </summary>
    public string Describe() => string.Create(
        CultureInfo.InvariantCulture,
        $"median {FirstMedianMilliseconds:F1}ms vs {SecondMedianMilliseconds:F1}ms - a relative " +
        $"difference of {RelativeDifference:P1}, against a tolerance of {Tolerance:P0}");
}
