namespace Yello.Tests.Shared;

/// <summary>
/// <c>Yello.Host</c> processes shared by the tests of one class: one per distinct connection string and settings,
/// started on first use and killed when the pool is disposed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why.</b> Booting a Host per test dominated the slice suite's run time. The process is still the real
/// executable started through <see cref="HostProcess.CreateStartInfo"/>, so <c>Program.cs</c> still runs; it just runs
/// once per class rather than once per test.
/// </para>
/// <para>
/// <b>What a shared Host cannot give a test</b>, which therefore stays on
/// <see cref="HostProcess.RunAsync(string, IReadOnlyDictionary{string, string}, Func{HttpClient, Task})"/>: the
/// Host's log (this returns none), settings of its own (a <see cref="MailDrop"/>, a rate limit, a session lifetime),
/// and fresh in-process state - a process-start sweep, a first-request purge, or anything else the Host does once.
/// </para>
/// <para>
/// <b>Each run gets a client of its own</b> with no cookie container, exactly as a per-test Host's client was. A
/// class's tests run one at a time, so no two of them talk to one Host at once.
/// </para>
/// </remarks>
public sealed class HostPool : IAsyncDisposable
{
    /// <summary>
    /// A class shares a Host per fixed settings set; more than this means per-test settings are being pooled, which
    /// would keep one idle process alive per test until the class ends.
    /// </summary>
    private const int MaximumHosts = 2;

    private static readonly IReadOnlyDictionary<string, string> NoExtraSettings =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly Dictionary<string, RunningHost> _hosts = new(StringComparer.Ordinal);

    private bool _disposed;

    /// <summary>
    /// Runs <paramref name="work"/> against this pool's Host for <paramref name="connectionString"/>, starting it if
    /// none is running.
    /// </summary>
    /// <param name="connectionString">The database the Host connects to.</param>
    /// <param name="work">What to do. Receives a fresh client bound to the Host.</param>
    /// <returns>A task.</returns>
    public Task RunAsync(string connectionString, Func<HttpClient, Task> work) =>
        RunAsync(connectionString, NoExtraSettings, work);

    /// <summary>
    /// Runs <paramref name="work"/> against this pool's Host for <paramref name="connectionString"/> and
    /// <paramref name="settings"/>, starting it if none is running.
    /// </summary>
    /// <param name="connectionString">The database the Host connects to.</param>
    /// <param name="settings">Extra configuration by environment-variable name; part of the Host's identity.</param>
    /// <param name="work">What to do. Receives a fresh client bound to the Host.</param>
    /// <returns>A task.</returns>
    public Task RunAsync(string connectionString, IReadOnlyDictionary<string, string> settings, Func<HttpClient, Task> work)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(work);

        return RunCoreAsync(connectionString, settings, work);
    }

    /// <summary>
    /// Kills every pooled Host connected to <paramref name="connectionString"/> and waits for it to exit. A later
    /// <see cref="RunAsync(string, Func{HttpClient, Task})"/> starts a fresh one.
    /// </summary>
    /// <remarks>
    /// A pooled Host is an outbox dispatcher on the class's database: any authenticated request it serves, or a sweep a
    /// previous case kicked, can claim and send a row the current case seeded, into the default drop rather than the
    /// case's <see cref="MailDrop"/>. A case that seeds outbox rows for a Host of its own to send stops the pool first.
    /// </remarks>
    /// <param name="connectionString">The database whose Hosts are stopped.</param>
    /// <returns>A task.</returns>
    public Task StopAsync(string connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        return StopCoreAsync(connectionString);
    }

    private async Task StopCoreAsync(string connectionString)
    {
        await _gate.WaitAsync().ConfigureAwait(false);

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            foreach (var key in _hosts.Keys.Where(key => IsFor(key, connectionString)).ToList())
            {
                var host = _hosts[key];

                _hosts.Remove(key);
                await host.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _gate.WaitAsync().ConfigureAwait(false);

        try
        {
            _disposed = true;

            // Every host is killed even when one disposal throws, so no process outlives the class.
            var failures = new List<Exception>();

            foreach (var host in _hosts.Values)
            {
                try
                {
                    await host.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    failures.Add(exception);
                }
            }

            _hosts.Clear();

            if (failures.Count > 0)
            {
                throw new AggregateException("A shared Host could not be stopped.", failures);
            }
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private static string KeyOf(string connectionString, IReadOnlyDictionary<string, string> settings) =>
        string.Join(
            '\n',
            settings.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value).Prepend(connectionString));

    private static bool IsFor(string key, string connectionString) =>
        key.Length >= connectionString.Length
        && key.StartsWith(connectionString, StringComparison.Ordinal)
        && (key.Length == connectionString.Length || key[connectionString.Length] == '\n');

    private async Task RunCoreAsync(string connectionString, IReadOnlyDictionary<string, string> settings, Func<HttpClient, Task> work)
    {
        var host = await HostForAsync(connectionString, settings).ConfigureAwait(false);

        using var handler = HostProcess.CreateClientHandler();
        using var client = new HttpClient(handler) { BaseAddress = host.Address };

        await work(client).ConfigureAwait(false);
    }

    private async Task<RunningHost> HostForAsync(string connectionString, IReadOnlyDictionary<string, string> settings)
    {
        var key = KeyOf(connectionString, settings);

        await _gate.WaitAsync().ConfigureAwait(false);

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_hosts.TryGetValue(key, out var running))
            {
                if (!running.HasExited)
                {
                    return running;
                }

                // A Host that died is replaced rather than handed to every later test in the class.
                _hosts.Remove(key);
                await running.DisposeAsync().ConfigureAwait(false);
            }

            if (_hosts.Count >= MaximumHosts)
            {
                throw new InvalidOperationException(
                    $"This pool already holds {_hosts.Count} Hosts. A pool is for a class's fixed settings; a case with " +
                    "settings of its own (a MailDrop, a rate limit) uses HostProcess.RunAsync.");
            }

            var started = await RunningHost.StartAsync(connectionString, settings).ConfigureAwait(false);

            _hosts[key] = started;

            return started;
        }
        finally
        {
            _gate.Release();
        }
    }
}
