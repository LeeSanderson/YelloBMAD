using Xunit;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Accounts.RegisterAccount;

/// <summary>
/// The B3 method's own constants and arithmetic, without a database.
/// </summary>
/// <remarks>
/// <para>
/// <b>A separate class from <see cref="RegistrationDurationTests"/>, and the separation is the
/// point.</b> That class takes a <c>MigratedDatabaseFixture</c>, and xunit constructs a class
/// fixture before any test in the class runs - so when the container runtime is down, every test
/// in it fails, including ones that never touch a database. These assertions read two numbers and
/// do some arithmetic; they belong somewhere they can actually run on a machine with no container
/// runtime, because that is exactly the machine where somebody is tempted to widen a tolerance to
/// quiet a flake.
/// </para>
/// <para>
/// Found the hard way at code review on 2026-09-10: these were first written inside
/// <see cref="RegistrationDurationTests"/> with a remark claiming they ran without a container,
/// and they did not - the claim was false for the same structural reason the review was
/// correcting elsewhere.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-28")]
[Trait("Requirement", "AD-23")]
public sealed class DurationIndistinguishabilityTests
{
    /// <summary>
    /// The two constants the guarantee rests on, pinned at their values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Written at code review on 2026-09-10, because the claim that this was already protected
    /// was false.</b> <c>RegistrationDurationTests</c>'s plant says of itself: "if a later story
    /// widens the tolerance to quiet a flake, this fails." It would not have. The planted arm
    /// skips the hash entirely - roughly 275 ms against single-digit milliseconds, a relative
    /// difference of about 0.98 - so the tolerance could have been raised from 0.20 all the way to
    /// 0.90 with <i>both</i> of that class's tests still green, while a work factor quietly cut
    /// from 220,000 to about 30,000 sailed through the real assertion. Nothing anywhere referenced
    /// either constant outside the file that declares them.
    /// </para>
    /// <para>
    /// So the protection is written down rather than assumed. Changing either value now requires
    /// changing this test, which is the point at which somebody has to say why.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_methods_constants_are_the_ones_its_guarantee_rests_on()
    {
        Assert.Equal(0.20, DurationIndistinguishability.DefaultTolerance);

        Assert.Equal(21, DurationIndistinguishability.MinimumSamples);
    }

    /// <summary>
    /// The verdict's arithmetic, at and past the tolerance.
    /// </summary>
    /// <remarks>
    /// The plant in <c>RegistrationDurationTests</c> proves the method can see an enormous
    /// difference - five times the tolerance. It proves nothing about where the line actually
    /// falls, and a comparison that only ever sees effects far from its threshold is not evidence
    /// that the threshold works. These are synthetic verdicts rather than timed runs, so the
    /// boundary is asserted exactly and without a flake: a timed test near 20% would be measuring
    /// the machine.
    /// </remarks>
    [Theory]
    [InlineData(0.00, true)]
    [InlineData(0.19, true)]
    [InlineData(0.20, true)]
    [InlineData(0.21, false)]
    [InlineData(0.98, false)]
    public void The_verdict_draws_the_line_at_the_tolerance(double relative, bool expected)
    {
        var verdict = new DurationVerdict(
            FirstMedianMilliseconds: 275,
            SecondMedianMilliseconds: 275 * (1 - relative),
            RelativeDifference: relative,
            Tolerance: DurationIndistinguishability.DefaultTolerance);

        Assert.Equal(expected, verdict.AreIndistinguishable);
    }
}
