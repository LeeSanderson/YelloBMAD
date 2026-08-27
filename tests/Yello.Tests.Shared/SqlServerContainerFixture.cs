using System.Globalization;
using System.Reflection;
using Testcontainers.MsSql;
using Xunit;

namespace Yello.Tests.Shared;

/// <summary>
/// The shared SQL Server container every suite runs against, and the mechanism by which a
/// later story asserts a migrated schema.
/// </summary>
/// <remarks>
/// <para>
/// This closes a test-design entry criterion that has no owning story: "All suites: shared
/// Testcontainers SQL Server fixture running <c>mssql/server:2025-latest</c>".
/// </para>
/// <para>
/// It is a real SQL Server, never an in-memory provider. An in-memory provider cannot
/// exercise row-level security, which is what NFR-1 rests on - so
/// <c>Microsoft.EntityFrameworkCore.InMemory</c> is not merely unreferenced but has no
/// central version at all, and Gate A asserts both halves of that ban, along with SQLite for
/// the same reason.
/// </para>
/// <para>
/// <b>Story 1.1 writes no schema assertion</b>: there is no schema. Story 1.3 creates the
/// first three tables. What is seeded here is the mechanism story 2.6 needs for risk R7 -
/// AD-15's <c>Latin1_General_100_BIN2</c> collation is irreversible, because
/// <c>ALTER DATABASE ... COLLATE</c> is unsupported on Azure SQL, so it has to be asserted
/// against a migrated database rather than reviewed.
/// </para>
/// <para>
/// <b>Topology is NOT settled, and this class does not settle it.</b> The intent recorded
/// during story 1.1 was "one container amortised across collections", and as written that
/// cannot hold: this is a plain fixture with no <c>[CollectionDefinition]</c>, no
/// assembly-level fixture and no <c>WithReuse</c>, so xunit constructs one instance per
/// collection - and the suites run as separate Microsoft.Testing.Platform <i>processes</i>,
/// which puts cross-suite sharing out of reach without container reuse or an external
/// orchestrator. Each consumer therefore gets its own SQL Server today. That is a real cost
/// and a real decision, and it belongs with <b>story 1.9</b>, which owns the single-connection
/// pooled variant and is the first story that has to make the sharing model real. Until then,
/// read the sentence above as intent rather than as description. The pooled-connection
/// isolation case will need its own container regardless, with pool size pinned to 1 and
/// parallelism disabled: a pooled connection carrying a stale session context is the thing
/// that case exists to catch, and it cannot be observed on a shared pool.
/// </para>
/// <para>
/// <b>No <c>Task.Delay</c>.</b> Readiness is a wait strategy, never a sleep. The test
/// design calls this out as "cheaper to enforce as a convention from story 1.1 than to
/// unpick later", and it binds to every suite, not just this fixture.
/// </para>
/// </remarks>
/// <remarks>
/// Sealed deliberately. Fixtures are consumed through xunit's <c>IClassFixture</c> /
/// <c>ICollectionFixture</c>, never by inheritance, and sealing settles CA1816: with no
/// derived types there is no finalizer for <see cref="DisposeAsync"/> to suppress. Story
/// 1.9's single-connection variant will be its own fixture, not a subclass of this one.
/// </remarks>
public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    /// <summary>
    /// How long to wait for the engine before failing. A cold pull of a ~1.5 GB image on a
    /// slow link legitimately takes minutes; never finishing is a different thing entirely.
    /// </summary>
    /// <remarks>
    /// Without a deadline there was none anywhere: no <c>WithStartupTimeout</c>, and
    /// <see cref="InitializeAsync"/> accepts no <c>CancellationToken</c> (xunit's
    /// <c>IAsyncLifetime</c> does not offer one). A host below SQL Server's memory floor, or a
    /// container that starts and never opens 1433, left the wait strategy retrying forever -
    /// so the run <i>hung</i> rather than failing, which is the worst of the available
    /// outcomes in CI.
    /// </remarks>
    /// <remarks>
    /// Lazy rather than a field initialiser for the same reason as <see cref="Image"/>: both
    /// throw on bad input, and from a static initialiser that arrives as
    /// <c>TypeInitializationException</c> across every test in the consuming suite, naming
    /// neither the cause nor the remedy.
    /// </remarks>
    public static TimeSpan StartupTimeout => StartupTimeoutValue.Value;

    /// <summary>
    /// The SQL Server image, from the one place the solution states it.
    /// </summary>
    /// <remarks>
    /// The same assembly metadata <c>Yello.AppHost</c> reads, emitted into every assembly by
    /// <c>Directory.Build.props</c>. It used to be a literal here and a second literal in the
    /// AppHost with nothing comparing them, so the suites and local orchestration could
    /// silently run different engine builds - and AD-15's collation and NFR-1's row-level
    /// security are both engine behaviour.
    /// <para>
    /// The tag still floats by cumulative update rather than being pinned by digest. That is a
    /// stated open question for Lee rather than an oversight, revisited when stories 1.5 and
    /// 2.6 give a reason to freeze the engine - but it now floats in one place. Note that
    /// <c>Yello.AppHost</c> rejects a digest reference outright rather than mis-splitting it,
    /// so pinning one is a deliberate change to both files.
    /// </para>
    /// <para>
    /// Read through a <c>Lazy</c> so that a missing value surfaces as an
    /// <c>InvalidOperationException</c> from this property, at the point of use, rather than as
    /// a <c>TypeInitializationException</c> from the class's static constructor.
    /// </para>
    /// </remarks>
    public static string Image => ImageValue.Value;

    private static readonly Lazy<TimeSpan> StartupTimeoutValue = new(ReadTimeout, isThreadSafe: true);

    private static readonly Lazy<string> ImageValue =
        new(() => ReadMetadata("Yello.SqlServerImage"), isThreadSafe: true);

    private MsSqlContainer? _container;

    /// <summary>
    /// Set only once <c>StartAsync</c> has returned, so readiness is distinguishable from
    /// "a container object exists".
    /// </summary>
    private bool _ready;

    /// <summary>
    /// The ADO.NET connection string for the running container.
    /// </summary>
    /// <remarks>
    /// The precondition used to be stated only in a comment. Throwing says the same thing at
    /// the moment it matters, and names the remedy.
    /// <para>
    /// Gated on <see cref="_ready"/> rather than on the container being non-null. A container
    /// whose wait strategy timed out is a live object with a published port, so the null check
    /// handed out a plausible connection string for an engine that never became ready - and the
    /// failure then arrived as a login error from whichever test used it first, rather than as
    /// the precondition this property exists to state.
    /// </para>
    /// </remarks>
    public string ConnectionString =>
        _ready && _container is not null
            ? _container.GetConnectionString()
            : throw new InvalidOperationException(
                "The container is not running. Consume this fixture through xunit's " +
                "IClassFixture or ICollectionFixture so InitializeAsync completes before any " +
                "test reads this. (If InitializeAsync threw, that exception is the one to fix; " +
                "this property stays closed rather than handing out a string for an engine that " +
                "never became ready.)");

    /// <summary>
    /// Starts the container and waits for the engine to accept connections.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The wait strategy is the builder's own, deliberately.</b>
    /// <c>MsSqlBuilder.Init()</c> registers a <c>WaitUntil</c> that shells
    /// <c>sqlcmd -Q "SELECT 1;"</c> inside the container - an engine-readiness probe.
    /// <c>WithWaitStrategy(...)</c> <i>replaces</i> the strategy list rather than appending to
    /// it, so overriding it with a port check swapped that probe for
    /// <c>UntilPortIsAvailable(1433)</c>. SQL Server binds 1433 well before <c>master</c> and
    /// <c>tempdb</c> recovery and login initialisation complete, so every consuming suite
    /// would race the engine and fail intermittently with login errors - in a file whose own
    /// remarks advertise "Readiness is a wait strategy, never a sleep". Not overriding it is
    /// the fix; there is nothing to add.
    /// </para>
    /// </remarks>
    public async ValueTask InitializeAsync()
    {
        // A second call would abandon the first container undisposed, leaving a stray 2 GB SQL
        // Server for the rest of the run - and on a retry after a failed start, the stray is the
        // one nobody is looking for.
        if (_container is not null)
        {
            throw new InvalidOperationException(
                "InitializeAsync has already run on this fixture instance. xunit calls it once " +
                "per fixture; if you need a second container, construct a second fixture.");
        }

        _container = BuildContainer();

        using var deadline = new CancellationTokenSource(StartupTimeout);

        var timedOut = false;

        try
        {
            await _container.StartAsync(deadline.Token).ConfigureAwait(false);
            _ready = true;
        }
        catch (OperationCanceledException)
        {
            timedOut = true;
        }
        catch (Exception exception)
        {
            // Catches everything on purpose. The previous filter was
            // `when (exception is not InvalidOperationException)`, which excluded Testcontainers'
            // own "the Docker endpoint is not reachable" shape - so the single most likely
            // failure on a developer machine arrived with no container diagnostics attached,
            // which is the opposite of what the diagnostics exist for. Re-wrapping our own
            // exception is avoided by the flag above rather than by filtering on type.
            throw new InvalidOperationException(
                $"The SQL Server container failed to start (image {Image})." +
                await DiagnosticsAsync().ConfigureAwait(false),
                exception);
        }

        // Outside the catch: gathering diagnostics is itself awaitable work, and doing it inside
        // a catch block whose exception must survive is how the original failure gets lost.
        if (timedOut)
        {
            throw new InvalidOperationException(
                $"SQL Server did not become ready within {StartupTimeout.TotalSeconds:F0}s " +
                $"(image {Image}). Set YELLO_CONTAINER_STARTUP_TIMEOUT_SECONDS to raise the " +
                "deadline if a cold image pull is the cause." +
                await DiagnosticsAsync().ConfigureAwait(false));
        }
    }

    /// <summary>
    /// Disposes the container, which is also how test data is cleaned up.
    /// </summary>
    /// <remarks>
    /// Cleanup is by container disposal or transaction rollback - never by delete
    /// statements, which would themselves need an RLS session context to see the rows they
    /// are trying to remove.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
            _container = null;
            _ready = false;
        }
    }

    /// <summary>
    /// True when a container runtime is reachable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With no runtime present, <c>Build()</c> throws and every consuming suite becomes
    /// unrunnable rather than skippable. This machine runs Rancher Desktop with a backend that
    /// is routinely stopped, so that is a live local concern rather than a theoretical one - a
    /// suite can consult this and skip with a reason instead of failing with a stack trace.
    /// </para>
    /// <para>
    /// <b>What this does not detect, stated rather than implied.</b> It answers "is a runtime
    /// configured and reachable enough to build a container definition", which is a
    /// configuration question answered without I/O. A socket that exists while the daemon behind
    /// it is dead or unhealthy passes here, and the failure then surfaces from
    /// <see cref="InitializeAsync"/> - bounded by <see cref="StartupTimeout"/> and reported with
    /// container diagnostics, which is the reason that deadline is not optional. Distinguishing
    /// the two properly needs a daemon ping, and the only client available for one arrives
    /// transitively through Testcontainers; referencing it directly would add an unpinned
    /// package, which Gate A correctly forbids. So the honest split is: configuration problems
    /// skip, daemon problems fail with a diagnosis.
    /// </para>
    /// </remarks>
    public static bool IsContainerRuntimeAvailable()
    {
        try
        {
            _ = BuildContainer();
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            // Testcontainers reports an absent or misconfigured runtime from the builder, before
            // any I/O is attempted. Both types are observed: ArgumentException for a missing
            // endpoint, InvalidOperationException for one it cannot interpret. Catching only the
            // first turned the second into an error in every consuming suite rather than a skip.
            return false;
        }
    }

    private static MsSqlContainer BuildContainer() =>
        new MsSqlBuilder().WithImage(Image).Build();

    /// <summary>
    /// Whatever the engine managed to say before it gave up.
    /// </summary>
    /// <remarks>
    /// Without this, <see cref="DisposeAsync"/> tears the container down and <i>why</i> it
    /// failed to start is unrecoverable after the fact - which turns "SQL Server did not start"
    /// into a bisect rather than a fix. The most common causes (a host below the engine's
    /// memory floor, an unaccepted EULA, a bad password policy) all announce themselves here.
    /// </remarks>
    private async Task<string> DiagnosticsAsync()
    {
        if (_container is null)
        {
            return string.Empty;
        }

        try
        {
            var (stdout, stderr) = await _container.GetLogsAsync().ConfigureAwait(false);
            var logs = string.Concat(stdout, stderr).Trim();

            return logs.Length == 0
                ? $"{Environment.NewLine}The container produced no output at all."
                : $"{Environment.NewLine}Container output:{Environment.NewLine}{logs}";
        }
        catch (Exception exception)
        {
            // Catches everything on purpose. This runs while an original failure is on its way
            // out, and the Docker client's own exception types (DockerApiException,
            // DockerContainerNotFoundException) are not among the three that used to be listed -
            // so a throw here REPLACED the failure being diagnosed, losing the very thing this
            // method exists to preserve. Best-effort diagnostics must never be able to do that.
            return $"{Environment.NewLine}(Container logs were unavailable: {exception.Message})";
        }
    }

    /// <summary>
    /// The startup deadline, from the environment or the default.
    /// </summary>
    /// <remarks>
    /// A malformed value is rejected rather than ignored. Falling back silently meant the
    /// documented remedy - "set YELLO_CONTAINER_STARTUP_TIMEOUT_SECONDS to raise the deadline" -
    /// could be followed exactly and do nothing, after which the same failure recurs at the same
    /// default and the message says to do the thing that was already done.
    /// <c>int.TryParse</c> rejects <c>600s</c>, <c>10m</c> and <c>1e3</c>, so all three were
    /// silent no-ops. The upper bound is <c>CancellationTokenSource</c>'s: it accepts up to
    /// 4,294,967 seconds and throws <c>ArgumentOutOfRangeException</c> above that - verified -
    /// which would previously have escaped from a static initialiser with no diagnosis at all.
    /// </remarks>
    private static TimeSpan ReadTimeout()
    {
        const string variable = "YELLO_CONTAINER_STARTUP_TIMEOUT_SECONDS";
        const int maximumSeconds = 4_294_967;

        var configured = Environment.GetEnvironmentVariable(variable);

        if (string.IsNullOrWhiteSpace(configured))
        {
            return TimeSpan.FromMinutes(5);
        }

        if (!int.TryParse(configured, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            || seconds <= 0
            || seconds > maximumSeconds)
        {
            throw new InvalidOperationException(
                $"{variable} is set to '{configured}', which is not a whole number of seconds " +
                $"between 1 and {maximumSeconds}. It is read as a plain integer - '600s' and " +
                "'10m' are not accepted. Unset it to use the five-minute default rather than " +
                "leaving a value that does nothing.");
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private static string ReadMetadata(string key)
    {
        var value = typeof(SqlServerContainerFixture).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key.Equals(key, StringComparison.Ordinal))
            ?.Value;

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"Assembly metadata '{key}' is missing. Directory.Build.props emits it for every " +
                "project; if it is gone, the value it carried has no source at all.")
            : value;
    }
}
