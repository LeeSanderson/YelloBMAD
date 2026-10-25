using System.Reflection;
using Xunit;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices;

/// <summary>
/// AC12 - a configured-but-dead container runtime is detected before a fixture tries to start a
/// container. <c>SqlServerContainerFixture.IsContainerRuntimeAvailable()</c> does no I/O, so a dead
/// daemon reads as available and the failure arrives five minutes later from a container start.
/// Story 1.9 Task 2; <c>TESTING-CONVENTIONS.md:463-479</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Activated by story 1.9 Task 2.</b> The probe is
/// <see cref="SqlServerContainerFixture.ProbeContainerRuntimeAsync(Uri, TimeSpan)"/> and its
/// configured-endpoint overload, each returning <c>null</c> when the daemon answered and the reason
/// otherwise - the shape the scaffold proposed.
/// </para>
/// <para>
/// <b>The decision the probe serves is recorded, not changed:</b> a dead runtime still makes
/// container-backed tests fail, not skip (Q1's sub-question). The probe gives the failure a precise
/// message.
/// </para>
/// <para>
/// <b>No fixture, deliberately.</b> A ping starts no container, so this class adds no engine. The
/// dead endpoint is passed in rather than set through <c>DOCKER_HOST</c>, because this process runs
/// container-backed classes in parallel.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "AR-35")]
public sealed class ContainerRuntimeProbeTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 1.9-PROBE-001 - an endpoint nothing listens on is reported unavailable within the deadline, with
    /// a reason that names the endpoint.
    /// </summary>
    [Fact]
    public async Task A_dead_runtime_endpoint_is_reported_unavailable_within_the_deadline()
    {
        var dead = new Uri("tcp://127.0.0.1:1");
        var started = TimeProvider.System.GetTimestamp();

        var reason = await SqlServerContainerFixture.ProbeContainerRuntimeAsync(dead, Deadline);

        Assert.NotNull(reason);
        Assert.Contains("127.0.0.1:1", reason, StringComparison.Ordinal);
        Assert.True(TimeProvider.System.GetElapsedTime(started) < Deadline + TimeSpan.FromSeconds(2), "The probe outlived its deadline.");
    }

    /// <summary>
    /// 1.9-PROBE-002 - anchor: the configured runtime, running, is reported available. Without this,
    /// a probe that always says "dead" would pass PROBE-001.
    /// </summary>
    [Fact]
    public async Task The_configured_running_runtime_is_reported_available()
    {
        // An unconfigured runtime skips, as every container-backed case does (Q1) - and fails on a CI
        // agent, where it is required (story 1.10 AC10); a configured one must answer.
        var unavailable = SqlServerEngine.UnavailableReason;
        Assert.SkipUnless(unavailable is null, unavailable ?? string.Empty);

        Assert.Null(await SqlServerContainerFixture.ProbeContainerRuntimeAsync(Deadline));
    }

    /// <summary>
    /// 1.10-UNIT-005 - on a CI agent (<c>YELLO_REQUIRE_CONTAINER_RUNTIME=1</c>) an unconfigured runtime is
    /// a failure, not a skip, so SM-1 cannot pass vacuously in CI. Story 1.10 AC10; Task 9, Q19 (a).
    /// </summary>
    /// <remarks>
    /// The decision is reached through <c>SqlServerEngine.UnavailableReasonFor(bool runtimeConfigured,
    /// bool runtimeRequired)</c>, so the case can state both values without setting a process-wide
    /// environment variable while container-backed classes run in parallel. It is internal to
    /// <c>Yello.Tests.Shared</c>, which grants this project nothing, so it is reached by reflection.
    /// </remarks>
    [Fact]
    [Trait("Priority", "P0")]
    [Trait("Requirement", "NFR-1")]
    public void When_ci_requires_the_runtime_an_unconfigured_one_fails_rather_than_skips()
    {
        var decide = UnavailableReasonFor();

        var failure = Assert.Throws<TargetInvocationException>(() => decide.Invoke(null, [false, true]));

        var inner = Assert.IsType<InvalidOperationException>(failure.InnerException);
        Assert.Contains("YELLO_REQUIRE_CONTAINER_RUNTIME", inner.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 1.10-UNIT-006 - anchor: without the requirement, an unconfigured runtime still skips with a
    /// reason (story 1.9 Q1, unchanged locally), and a configured one is available either way.
    /// </summary>
    [Fact]
    public void Without_the_requirement_an_unconfigured_runtime_still_skips()
    {
        var decide = UnavailableReasonFor();

        Assert.False(string.IsNullOrWhiteSpace(decide.Invoke(null, [false, false]) as string));
        Assert.Null(decide.Invoke(null, [true, false]));
        Assert.Null(decide.Invoke(null, [true, true]));
    }

    private static MethodInfo UnavailableReasonFor()
    {
        var method = typeof(SqlServerEngine).GetMethod(
            "UnavailableReasonFor",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            [typeof(bool), typeof(bool)]);

        Assert.True(method is not null, "SqlServerEngine.UnavailableReasonFor(bool runtimeConfigured, bool runtimeRequired) does not exist (story 1.10 Task 9).");

        return method;
    }
}
