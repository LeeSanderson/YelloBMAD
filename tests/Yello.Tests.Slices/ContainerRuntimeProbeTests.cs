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
/// <b>Red-phase scaffolds.</b> The probe does not exist yet, so it is reached <b>by name</b>:
/// <c>ProbeContainerRuntimeAsync(Uri endpoint, TimeSpan deadline)</c> and
/// <c>ProbeContainerRuntimeAsync(TimeSpan deadline)</c> (the configured endpoint), each returning
/// <c>Task&lt;string?&gt;</c> - <c>null</c> when the daemon answered, otherwise the reason. The names
/// and shape are proposals; a different choice in dev-story is corrected here and recorded in the
/// Debug Log. On activation, replace the reflection with typed calls.
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
    [Fact(Skip = "red: AC12 - the daemon probe does not exist yet; IsContainerRuntimeAvailable() does no I/O (story 1.9 Task 2)")]
    public async Task A_dead_runtime_endpoint_is_reported_unavailable_within_the_deadline()
    {
        var dead = new Uri("tcp://127.0.0.1:1");
        var started = TimeProvider.System.GetTimestamp();

        var reason = await ProbeAsync([typeof(Uri), typeof(TimeSpan)], dead, Deadline);

        Assert.NotNull(reason);
        Assert.Contains("127.0.0.1:1", reason, StringComparison.Ordinal);
        Assert.True(TimeProvider.System.GetElapsedTime(started) < Deadline + TimeSpan.FromSeconds(2), "The probe outlived its deadline.");
    }

    /// <summary>
    /// 1.9-PROBE-002 - anchor: the configured runtime, running, is reported available. Without this,
    /// a probe that always says "dead" would pass PROBE-001.
    /// </summary>
    [Fact(Skip = "red: AC12 - the daemon probe does not exist yet (story 1.9 Task 2)")]
    public async Task The_configured_running_runtime_is_reported_available()
    {
        Assert.True(SqlServerContainerFixture.IsContainerRuntimeAvailable(), "No container runtime is configured on this machine.");

        Assert.Null(await ProbeAsync([typeof(TimeSpan)], Deadline));
    }

    private static async Task<string?> ProbeAsync(Type[] parameters, params object[] arguments)
    {
        var probe = typeof(SqlServerContainerFixture).GetMethod(
            "ProbeContainerRuntimeAsync",
            BindingFlags.Public | BindingFlags.Static,
            parameters)
            ?? throw new InvalidOperationException(
                $"SqlServerContainerFixture.ProbeContainerRuntimeAsync({string.Join(", ", parameters.Select(p => p.Name))}) does not exist (story 1.9 Task 2).");

        var task = probe.Invoke(null, arguments) as Task<string?>
            ?? throw new InvalidOperationException("ProbeContainerRuntimeAsync does not return Task<string?> (story 1.9 Task 2).");

        return await task;
    }
}
