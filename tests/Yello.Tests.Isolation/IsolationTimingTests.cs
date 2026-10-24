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
/// <b>Red-phase scaffolds.</b> The in-Space not-found arm AC3 names does not exist until story 2.2
/// (no route looks a resource up by id inside a Space). It is the pending row
/// <c>ISO-TIMING-IN-SPACE</c>, not a scaffold here (Q5 (a); <c>deferred-work.md:282</c>).
/// </para>
/// <para>
/// <b>The measurement point is Q5's sub-question.</b> These scaffolds time the round trip, which is
/// what <see cref="DurationIndistinguishability.CompareAsync(Func{Task}, Func{Task})"/> measures
/// today. The story recommends the Host's own figure (Kestrel's request-finished line, read through
/// <see cref="IIsolationWorld.LastServerDurationMillisecondsAsync"/>). If dev-story takes that, it
/// feeds the server figure into the same method - an overload taking the sample, not a second
/// method - and records that the measurement point moved.
/// </para>
/// <para>
/// <b>The oracle is permanent.</b> A comparison that can never fail is not a test, so each surface
/// also compares the boundary 404 against an arm that genuinely does less work - the unmatched-path
/// fallback, which resolves nothing and records nothing - and that comparison must report a
/// difference. Record the measured gaps per surface in the Debug Log.
/// </para>
/// </remarks>
[Trait("Suite", "Isolation")]
[Trait("Priority", "P0")]
[Trait("Requirement", "NFR-1")]
[Trait("Requirement", "AD-3")]
public sealed class IsolationTimingTests
{
    /// <summary>
    /// 1.9-TIME-001 / ISO-TIMING-BOUNDARY - on each surface, A asking for B's Space and A asking for a
    /// Space that does not exist take the same time and return the same body.
    /// </summary>
    [Fact(Skip = "red: AC3 - the two-surface harness does not exist yet (story 1.9 Tasks 3 and 6)")]
    public async Task A_foreign_Space_and_an_absent_one_take_the_same_time_on_both_surfaces()
    {
        await using var world = await IsolationWorld.StartAsync();

        foreach (var surface in IsolationWorld.Surfaces)
        {
            var foreign = await world.SendAsync(world.A, surface, HttpMethod.Get, SpaceRoutes.ActiveOf(world.B.SpaceId));
            var absent = await world.SendAsync(world.A, surface, HttpMethod.Get, SpaceRoutes.ActiveOf(Guid.NewGuid()));

            Assert.Equal(404, foreign.Status);
            Assert.Equal(absent.Body, foreign.Body);

            var verdict = await DurationIndistinguishability.CompareAsync(
                () => world.SendAsync(world.A, surface, HttpMethod.Get, SpaceRoutes.ActiveOf(world.B.SpaceId)),
                () => world.SendAsync(world.A, surface, HttpMethod.Get, SpaceRoutes.ActiveOf(Guid.NewGuid())));

            Assert.True(verdict.AreIndistinguishable, $"{surface}: {verdict.Describe()}");
        }
    }

    /// <summary>
    /// 1.9-TIME-002 / ISO-TIMING-TOKEN-ELSEWHERE / I-12 - a Token bound to A's own Space, used at a
    /// second Space A owns, takes the same time to refuse as a non-member Session at that Space. Lifted
    /// from <c>ActiveSpaceEndpointTests.Tokens.cs:275-307</c>; the Slices case stays as the mechanism's
    /// proof.
    /// </summary>
    [Fact(Skip = "red: AC3 - the two-surface harness does not exist yet (story 1.9 Tasks 3 and 6)")]
    [Trait("Requirement", "FR-36")]
    public async Task A_Token_bound_elsewhere_and_a_non_member_take_the_same_time_to_refuse()
    {
        await using var world = await IsolationWorld.StartAsync();
        var secondSpace = await world.AddSecondOwnedSpaceAsync(world.A);
        var route = SpaceRoutes.ActiveOf(secondSpace);

        var verdict = await DurationIndistinguishability.CompareAsync(
            () => world.SendAsync(world.A, ScaffoldSurface.ApiToken, HttpMethod.Get, route),
            () => world.SendAsync(world.B, ScaffoldSurface.Session, HttpMethod.Get, route));

        Assert.True(verdict.AreIndistinguishable, verdict.Describe());
    }

    /// <summary>
    /// 1.9-TIME-003 - the planted oracle, per surface: the method tells the boundary 404 from the
    /// unmatched-path fallback, which does no resolution and writes no <c>AccessRefusal</c> row. If this
    /// reports "indistinguishable", the wire's noise swamps the band and TIME-001 means nothing on that
    /// surface - fall back to the in-process measurement for it and record the deviation.
    /// </summary>
    [Fact(Skip = "red: AC3 - the two-surface harness does not exist yet (story 1.9 Tasks 3 and 6)")]
    public async Task The_duration_method_tells_the_boundary_refusal_from_an_arm_that_records_nothing_on_both_surfaces()
    {
        await using var world = await IsolationWorld.StartAsync();

        foreach (var surface in IsolationWorld.Surfaces)
        {
            var verdict = await DurationIndistinguishability.CompareAsync(
                () => world.SendAsync(world.A, surface, HttpMethod.Get, SpaceRoutes.ActiveOf(world.B.SpaceId)),
                () => world.SendAsync(world.A, surface, HttpMethod.Get, SpaceRoutes.ActiveOf(world.B.SpaceId) + "/nothing"));

            Assert.False(verdict.AreIndistinguishable, $"{surface}: the oracle was not caught - {verdict.Describe()}");
        }
    }
}
