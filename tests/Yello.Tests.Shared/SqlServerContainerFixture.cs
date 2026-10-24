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
/// <b>The topology is settled by story 1.9: one engine per test process.</b> This class is one
/// SQL Server; <see cref="SqlServerEngine"/> is the xunit v3 assembly fixture that owns the one
/// instance a test process starts, and every test class takes <i>its own database</i> on it
/// rather than an engine of its own. Before this, a full <c>dotnet test Yello.slnx</c> started
/// thirteen engines concurrently (ten class fixtures and three direct constructions; measured on
/// 2026-10-24 at a 7.9 GB Docker peak with 0.6 GB of host memory left, and the Rancher backend
/// stopped answering mid-run). <c>ContainerTopologyGateTests</c> holds the shape: no test class
/// starts this fixture outside a named list, and each suite declares exactly one assembly fixture.
/// </para>
/// <para>
/// <b>The pooled-connection cases need no engine of their own.</b> SqlClient keys a pool by its
/// exact connection string, so <c>Max Pool Size=1</c> on a class's own database is its own pinned
/// pool on the shared engine; those cases run in a collection with parallelisation disabled.
/// </para>
/// <para>
/// <b>Each engine is capped at <c>MSSQL_MEMORY_LIMIT_MB</c> 2048</b>, the floor SQL Server on
/// Linux is documented to need, so the engines of a full run cannot take the machine's memory.
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
/// derived types there is no finalizer for <see cref="DisposeAsync"/> to suppress.
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
    /// stated open question for Lee rather than an oversight, revisited when story 2.6 gives a
    /// reason to freeze the engine; the engine-behaviour tests in the slice suite give none yet - but it now
    /// floats in one place. Note that
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
    /// 1 once <see cref="InitializeAsync"/> has been entered. Reset by a failed start so a retry
    /// can report the cause instead of the guard.
    /// </summary>
    private int _starting;

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
                "The container is not running. Await InitializeAsync before reading this - " +
                "either by constructing the fixture directly in an `await using` and calling it, " +
                "which is what every consumer in this repository does today, or through xunit's " +
                "IClassFixture / ICollectionFixture. (If InitializeAsync threw, that exception is " +
                "the one to fix; this property stays closed rather than handing out a string for " +
                "an engine that never became ready.)");

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
        // Atomic, because a read-then-write guard lets two concurrent callers both past it and
        // one container is then started and abandoned. A FAILED start resets this, so a retry is
        // allowed and reports the real cause rather than "already run" - which was the previous
        // behaviour and hid the very failure a developer was retrying to understand.
        if (Interlocked.Exchange(ref _starting, 1) == 1)
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
            var diagnostics = await DiagnosticsAsync().ConfigureAwait(false);
            await AbandonAsync().ConfigureAwait(false);

            throw new InvalidOperationException(
                $"The SQL Server container failed to start (image {Image}).{diagnostics}",
                exception);
        }

        // Outside the catch: gathering diagnostics is itself awaitable work, and doing it inside
        // a catch block whose exception must survive is how the original failure gets lost.
        if (timedOut)
        {
            var diagnostics = await DiagnosticsAsync().ConfigureAwait(false);
            await AbandonAsync().ConfigureAwait(false);

            throw new InvalidOperationException(
                $"SQL Server did not become ready within {StartupTimeout.TotalSeconds:F0}s " +
                $"(image {Image}). Set YELLO_CONTAINER_STARTUP_TIMEOUT_SECONDS to raise the " +
                $"deadline if a cold image pull is the cause.{diagnostics}");
        }
    }

    /// <summary>
    /// Tears down a container that was created but never became ready, and reopens the door to a
    /// retry.
    /// </summary>
    /// <remarks>
    /// Without this, a failed start left the container running and undisposed - a stray ~2 GB SQL
    /// Server for the rest of the run, which nobody is looking for because the visible symptom is
    /// a test failure rather than a leak. The new tests happened to mask it by constructing the
    /// fixture in an <c>await using</c>; an <c>IClassFixture</c> consumer would not. Diagnostics
    /// are always gathered before this runs, so tearing down costs no information.
    /// </remarks>
    private async ValueTask AbandonAsync()
    {
        if (_container is not null)
        {
            try
            {
                await _container.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // Best effort. The start failure is the one worth reporting, and a teardown that
                // itself fails must not replace it - the same rule DiagnosticsAsync follows.
            }

            _container = null;
        }

        _ready = false;
        Interlocked.Exchange(ref _starting, 0);
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
    /// configured", without I/O. A socket that exists while the daemon behind it is dead passes
    /// here; <see cref="ProbeContainerRuntimeAsync(TimeSpan)"/> is the check that asks the daemon,
    /// and <see cref="SqlServerEngine"/> runs it before it starts anything. The split is deliberate
    /// and story 1.9's (Q1's sub-question): an <i>unconfigured</i> runtime skips, a configured but
    /// <i>dead</i> one fails, with the probe's reason as the message. Skipping SM-1 silently on a
    /// dead runtime would be the vacuous gate this repository refuses.
    /// </para>
    /// </remarks>
    public static bool IsContainerRuntimeAvailable()
    {
        // OUTSIDE the try, on purpose. Reading the image touches assembly metadata and throws
        // InvalidOperationException when the build did not stamp it - and with that read inside
        // a catch that swallows InvalidOperationException, a build-stamp defect reported itself
        // as "no container runtime is reachable". Demonstrated with a runtime confirmed up:
        // renaming the metadata key flipped this method to false, so every container-backed case
        // skipped and the whole container half of AC4's evidence disappeared while nothing went
        // red. A missing shared value is not a local Docker problem and must not be reported as
        // one.
        var image = Image;

        try
        {
            _ = new MsSqlBuilder().WithImage(image).Build();
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

    /// <summary>
    /// Asks the configured container runtime's daemon to answer, within <paramref name="deadline"/>.
    /// </summary>
    /// <param name="deadline">How long the daemon has to answer.</param>
    /// <returns><c>null</c> when the daemon answered; otherwise why it did not.</returns>
    /// <remarks>
    /// The Docker client used here is the one Testcontainers already carries; no package is added.
    /// It is the answer to <c>TESTING-CONVENTIONS.md</c>'s "the skip guard does no I/O": on this
    /// machine Rancher's Hyper-V socket times out while the configuration still reads as present.
    /// </remarks>
    public static Task<string?> ProbeContainerRuntimeAsync(TimeSpan deadline)
    {
        Uri endpoint;
        Docker.DotNet.DockerClientConfiguration configuration;

        try
        {
            var authentication = DotNet.Testcontainers.Configurations.TestcontainersSettings.OS.DockerEndpointAuthConfig;
            endpoint = authentication.Endpoint;
            configuration = authentication.GetDockerClientConfiguration(Guid.Empty);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return Task.FromResult<string?>($"No container runtime is configured: {exception.Message}");
        }

        return PingAsync(endpoint, configuration, deadline);
    }

    /// <summary>
    /// Asks the daemon at <paramref name="endpoint"/> to answer, within <paramref name="deadline"/>.
    /// </summary>
    /// <param name="endpoint">The daemon's endpoint, for example <c>npipe://./pipe/docker_engine</c>.</param>
    /// <param name="deadline">How long the daemon has to answer.</param>
    /// <returns><c>null</c> when the daemon answered; otherwise why it did not, naming the endpoint.</returns>
    public static Task<string?> ProbeContainerRuntimeAsync(Uri endpoint, TimeSpan deadline)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        return PingAsync(endpoint, new Docker.DotNet.DockerClientConfiguration(endpoint), deadline);
    }

    private static async Task<string?> PingAsync(
        Uri endpoint,
        Docker.DotNet.DockerClientConfiguration configuration,
        TimeSpan deadline)
    {
        using var cancellation = new CancellationTokenSource(deadline);

        try
        {
            using (configuration)
            using (var client = configuration.CreateClient())
            {
                await client.System.PingAsync(cancellation.Token).WaitAsync(cancellation.Token).ConfigureAwait(false);
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"The container runtime at {endpoint} did not answer a ping within {deadline.TotalSeconds:F0}s. On Rancher Desktop: `rdctl shutdown`, `wsl --terminate rancher-desktop`, then relaunch.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return $"The container runtime at {endpoint} did not answer a ping: {exception.GetType().Name}: {exception.Message}";
        }
    }

    private static MsSqlContainer BuildContainer() =>
        new MsSqlBuilder()
            .WithImage(Image)
            .WithEnvironment("MSSQL_MEMORY_LIMIT_MB", "2048")
            .Build();

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
