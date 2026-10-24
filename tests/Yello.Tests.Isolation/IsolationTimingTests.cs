using Xunit;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// AC3 / AD-3 / I-6 - a Space-boundary 404 is indistinguishable by duration as well as by body, on
/// each surface. Through the one duration method (<see cref="DurationIndistinguishability"/>: 21
/// samples per arm, interleaved, one warm-up, median, 20% of the slower median) - never a second one.
/// </summary>
/// <remarks>
/// <para>
/// <b>The measurement point is the Host's own</b> (Q5's sub-question): each sample is the elapsed time
/// on the Host's request-finished line for that request, read through
/// <see cref="RunningHost.ServerDurationOfAsync"/> and fed to
/// <see cref="DurationIndistinguishability.CompareMeasuredAsync"/>. The round trip would add the
/// client's and the loopback's noise to both arms.
/// </para>
/// <para>
/// <b>The in-Space not-found arm AC3 names is the pending row <c>ISO-TIMING-IN-SPACE</c></b> (story
/// 2.2): no route looks a resource up by id inside a Space yet (Q5 (a); <c>deferred-work.md:282</c>).
/// </para>
/// <para>
/// <b>The oracle is permanent.</b> A comparison that can never fail is not a test, so each surface also
/// compares the boundary 404 with an arm that genuinely does less work - the unmatched-path fallback,
/// which resolves nothing and records nothing - and that comparison must report a difference.
/// </para>
/// <para>
/// <b>Alone, in a collection with parallelisation disabled</b>, so another class's load cannot move one
/// arm's samples and not the other's.
/// </para>
/// </remarks>
/// <param name="fixture">The class's world.</param>
[Trait("Suite", "Isolation")]
[Trait("Priority", "P0")]
[Trait("Requirement", "NFR-1")]
[Trait("Requirement", "AD-3")]
[Collection(nameof(TimingCollection))]
public sealed class IsolationTimingTests(IsolationWorldFixture fixture) : IClassFixture<IsolationWorldFixture>
{
    /// <summary>
    /// The timing runner's rows.
    /// </summary>
    public static IEnumerable<TheoryDataRow<string, IsolationSurface>> Rows => IsolationCases.TimingRows;

    /// <summary>
    /// 1.9-TIME-001 and TIME-002, as rows: each timing row on each surface.
    /// </summary>
    /// <param name="caseId">The row.</param>
    /// <param name="surface">The surface.</param>
    /// <returns>A task.</returns>
    [Theory]
    [MemberData(nameof(Rows))]
    public Task Each_timing_case_is_indistinguishable_on_each_surface(string caseId, IsolationSurface surface) =>
        CaseRun.RunAsync(fixture.World, IsolationCases.Named(caseId), surface);

    /// <summary>
    /// 1.9-TIME-003 - the planted oracle, per surface: the method tells the boundary 404 from the
    /// unmatched-path fallback, which does no resolution and writes no <c>AccessRefusal</c> row.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    public async Task The_duration_method_tells_the_boundary_refusal_from_an_arm_that_records_nothing(IsolationSurface surface)
    {
        var world = fixture.World;
        var boundary = SpaceRoutes.ActiveOf(world.B.SpaceId);
        var nothing = boundary + "/nothing";

        var verdict = await DurationIndistinguishability.CompareMeasuredAsync(
            () => world.Host.ServerDurationOfAsync(() => world.A.SendAsync(surface, HttpMethod.Get, boundary)),
            () => world.Host.ServerDurationOfAsync(() => world.A.SendAsync(surface, HttpMethod.Get, nothing)));

        TimingRecord.Add($"oracle (boundary vs unmatched) / {surface}: {verdict.Describe()}");
        Assert.False(verdict.AreIndistinguishable, $"{surface}: the oracle was not caught - {verdict.Describe()}");
    }
}

/// <summary>
/// The timing cases run alone.
/// </summary>
[CollectionDefinition(nameof(TimingCollection), DisableParallelization = true)]
public sealed class TimingCollection;
