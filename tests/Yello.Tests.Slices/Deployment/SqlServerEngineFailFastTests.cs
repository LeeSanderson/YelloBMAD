using System.Reflection;
using Xunit;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Deployment;

/// <summary>
/// The shared engine's dead-runtime path, proved rather than read: with the daemon probe reporting
/// dead, <c>SqlServerEngine</c> fails with the probe's reason before constructing a container, and
/// later callers get the cached failure without probing again. Story 1.10 AC16; Task 9 (a);
/// <c>deferred-work.md:401</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as a red-phase scaffold by the ATDD run on 2026-10-25.</b> The probe is static today
/// (<c>SqlServerEngine.cs:184-194</c>), so a test cannot make it report dead without killing the
/// runtime every other class in this process is using.
/// </para>
/// <para>
/// <b>The seam is reached by name</b>, because it does not exist yet: a constructor taking the probe
/// (<c>Func&lt;CancellationToken, Task&lt;string?&gt;&gt;</c>) and the container factory
/// (<c>Func&lt;SqlServerContainerFixture&gt;</c>), used only by tests. On activation, replace the
/// reflection with the typed call and record the shape the seam took.
/// </para>
/// <para>
/// <b>No container is started</b>: the factory counts its calls and throws if called, so a regression
/// fails here rather than starting an engine on a memory-bound machine.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-35")]
public sealed class SqlServerEngineFailFastTests
{
    private const string DeadReason = "probe-reported-dead-" + "for-this-test";

    /// <summary>
    /// 1.10-UNIT-007 - a dead probe fails <c>CreateDatabaseAsync</c> with its reason, constructs no
    /// container, and the second call rethrows the cached failure without probing again.
    /// </summary>
    [Fact(Skip = "red: AC16 - SqlServerEngine has no probe seam, so its dead-runtime path cannot be driven (Task 9 (a))")]
    public async Task A_dead_runtime_fails_fast_with_the_probes_reason_and_the_failure_is_cached()
    {
        var probes = 0;
        var containers = 0;

        Func<CancellationToken, Task<string?>> probe = _ =>
        {
            Interlocked.Increment(ref probes);
            return Task.FromResult<string?>(DeadReason);
        };

        Func<SqlServerContainerFixture> factory = () =>
        {
            Interlocked.Increment(ref containers);
            throw new InvalidOperationException("The engine constructed a container after the probe reported the runtime dead.");
        };

        var constructor = typeof(SqlServerEngine).GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            [typeof(Func<CancellationToken, Task<string?>>), typeof(Func<SqlServerContainerFixture>)]);

        Assert.True(constructor is not null, "SqlServerEngine has no (probe, container factory) constructor (story 1.10 Task 9 (a)).");

        await using var engine = Assert.IsType<SqlServerEngine>(constructor.Invoke([probe, factory]));

        var first = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.CreateDatabaseAsync(TestContext.Current.CancellationToken));
        var second = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.CreateDatabaseAsync(TestContext.Current.CancellationToken));

        Assert.Contains(DeadReason, Describe(first), StringComparison.Ordinal);
        Assert.Contains(DeadReason, Describe(second), StringComparison.Ordinal);
        Assert.Equal(1, Volatile.Read(ref probes));
        Assert.Equal(0, Volatile.Read(ref containers));
    }

    private static string Describe(Exception exception) =>
        exception.InnerException is null ? exception.Message : exception.Message + " | " + Describe(exception.InnerException);
}
