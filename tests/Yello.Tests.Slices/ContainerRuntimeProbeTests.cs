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
        // An unconfigured runtime skips, as every container-backed case does (Q1); a configured one must answer.
        Assert.SkipUnless(SqlServerContainerFixture.IsContainerRuntimeAvailable(), "No container runtime is configured on this machine.");

        Assert.Null(await SqlServerContainerFixture.ProbeContainerRuntimeAsync(Deadline));
    }
}
