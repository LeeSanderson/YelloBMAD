using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Yello.Tests.Shared;

/// <summary>
/// A <c>Yello.Host</c> process that stays up for a whole test class, with a cursor into its output so
/// each case reads only the lines its own requests produced.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a long-lived Host.</b> <see cref="HostProcess.RunAsync(string, Func{HttpClient, Task})"/>
/// starts a process per call, which is right for the slice cases and unaffordable for the isolation
/// suite's dozens of cases on two surfaces. This starts the same executable with the same environment
/// (<see cref="HostProcess.CreateStartInfo"/>) once per class.
/// </para>
/// <para>
/// <b>The log cursor is what makes AC10 a per-case check.</b> A case takes a
/// <see cref="HostLogCursor"/> (<see cref="LogCursorAsync"/>) before its requests and reads <see cref="LogSinceAsync"/> after them.
/// The read waits until the Host has written its request-finished line for every request sent since
/// the cursor, so a slice cannot be read before the lines it should contain have arrived - and an
/// empty slice is impossible for a case that sent anything.
/// </para>
/// <para>
/// <b>Request-finished lines are switched on for these Hosts only</b>, through configuration
/// (<c>Microsoft.AspNetCore.Hosting.Diagnostics</c> at <c>Information</c>). The product's own level is
/// unchanged. The line carries the method, the URL, the status and the Host's own elapsed time, and no
/// header - so it is both the per-request anchor and the server-side duration Q5 asks for.
/// </para>
/// </remarks>
public sealed partial class RunningHost : IAsyncDisposable
{
    private static readonly TimeSpan StartupWindow = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan LogWindow = TimeSpan.FromSeconds(20);

    private readonly Process _process;

    private Task _pump = Task.CompletedTask;

    private readonly List<string> _lines = [];

    private readonly Lock _gate = new();

    private readonly CountingHandler _handler;

    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private RunningHost(Process process, CountingHandler handler)
    {
        _process = process;
        _handler = handler;
    }

    /// <summary>
    /// The configuration every running Host gets on top of <see cref="HostProcess.CreateStartInfo"/>'s.
    /// </summary>
    public static IReadOnlyDictionary<string, string> RequestLogging { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Logging__LogLevel__Microsoft.AspNetCore.Hosting.Diagnostics"] = "Information",
        };

    /// <summary>
    /// The Host's address.
    /// </summary>
    public Uri Address { get; private set; } = new("https://127.0.0.1/");

    /// <summary>
    /// A client bound to the Host. It holds no cookies; every cookie a case sends is one it attached.
    /// </summary>
    public HttpClient Client { get; private set; } = new();

    /// <summary>
    /// Everything the Host has written so far, both streams.
    /// </summary>
    public string Log
    {
        get
        {
            lock (_gate)
            {
                return string.Join(Environment.NewLine, _lines);
            }
        }
    }

    /// <summary>
    /// Starts the Host and waits until it is listening.
    /// </summary>
    /// <param name="connectionString">The database the Host connects to.</param>
    /// <returns>The running Host.</returns>
    public static Task<RunningHost> StartAsync(string connectionString) =>
        StartAsync(connectionString, new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>
    /// Starts the Host with extra configuration and waits until it is listening.
    /// </summary>
    /// <param name="connectionString">The database the Host connects to.</param>
    /// <param name="settings">Extra configuration by environment-variable name, applied last.</param>
    /// <returns>The running Host.</returns>
    public static Task<RunningHost> StartAsync(string connectionString, IReadOnlyDictionary<string, string> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return StartCoreAsync(connectionString, settings);
    }

    private static async Task<RunningHost> StartCoreAsync(string connectionString, IReadOnlyDictionary<string, string> settings)
    {
        var combined = new Dictionary<string, string>(RequestLogging, StringComparer.Ordinal);

        foreach (var (key, value) in settings)
        {
            combined[key] = value;
        }

        var startInfo = HostProcess.CreateStartInfo(connectionString, combined);
        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start '{startInfo.FileName}'.");

        var host = new RunningHost(process, new CountingHandler(HostProcess.CreateClientHandler()));
        var listening = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task PumpAsync(StreamReader reader)
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                host.Append(line);

                var marker = line.IndexOf(HostProcess.ListeningMarker, StringComparison.Ordinal);

                if (marker >= 0)
                {
                    listening.TrySetResult(line[(marker + HostProcess.ListeningMarker.Length)..].Trim());
                }
            }
        }

        host._pump = Task.WhenAll(
            Task.Run(() => PumpAsync(process.StandardOutput)),
            Task.Run(() => PumpAsync(process.StandardError)));

        try
        {
            using var deadline = new CancellationTokenSource(StartupWindow);

            // A Host that exits during startup fails now, not at the end of the window.
            var exited = process.WaitForExitAsync(deadline.Token);

            if (await Task.WhenAny(listening.Task, exited).WaitAsync(deadline.Token).ConfigureAwait(false) != listening.Task)
            {
                // The exit wait ends cancelled when the window closes; that is the timeout, not an exit.
                deadline.Token.ThrowIfCancellationRequested();

                await host._pump.ConfigureAwait(false);

                throw new InvalidOperationException(
                    $"The Host exited with code {process.ExitCode} before it reported an address. Its output:{Environment.NewLine}{host.Log}");
            }

            host.Address = new Uri(await listening.Task.ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            var log = host.Log;

            await host.DisposeAsync().ConfigureAwait(false);

            throw new InvalidOperationException(
                $"The Host did not report an address within {StartupWindow.TotalSeconds:F0}s. Its output:{Environment.NewLine}{log}");
        }
        catch (Exception exception) when (exception is InvalidOperationException or UriFormatException)
        {
            await host.DisposeAsync().ConfigureAwait(false);

            throw;
        }

        host.Client.Dispose();
        host.Client = new HttpClient(host._handler, disposeHandler: false) { BaseAddress = host.Address };

        return host;
    }

    /// <summary>
    /// A position in the Host's output and in the requests sent to it. Everything after it belongs to
    /// the case that took it.
    /// </summary>
    /// <returns>The cursor.</returns>
    /// <remarks>
    /// Waits first until every request sent so far has its request-finished line, so a line that
    /// belongs to an earlier request cannot arrive late and be counted in the next case's slice.
    /// </remarks>
    public async Task<HostLogCursor> LogCursorAsync()
    {
        await LinesSinceAsync(new HostLogCursor(0, 0)).ConfigureAwait(false);

        lock (_gate)
        {
            return new HostLogCursor(_lines.Count, _handler.Sent);
        }
    }

    /// <summary>
    /// The Host's output since <paramref name="cursor"/>, once it has written its request-finished line
    /// for every request sent since then.
    /// </summary>
    /// <param name="cursor">Where the case's slice starts.</param>
    /// <returns>The slice.</returns>
    /// <exception cref="InvalidOperationException">The lines did not arrive within the window.</exception>
    public async Task<string> LogSinceAsync(HostLogCursor cursor)
    {
        var lines = await LinesSinceAsync(cursor).ConfigureAwait(false);

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// The Host's output since <paramref name="cursor"/>, once it satisfies <paramref name="arrived"/> -
    /// for requests this process did not send (a browser's), whose lines it cannot count.
    /// </summary>
    /// <param name="cursor">Where the slice starts.</param>
    /// <param name="arrived">The condition the slice must meet: the line that anchors the case.</param>
    /// <returns>The slice.</returns>
    /// <exception cref="InvalidOperationException">The condition was not met within the window.</exception>
    public Task<string> WaitForLogAsync(HostLogCursor cursor, Func<string, bool> arrived)
    {
        ArgumentNullException.ThrowIfNull(arrived);

        return WaitForLogCoreAsync(cursor, arrived);
    }

    private async Task<string> WaitForLogCoreAsync(HostLogCursor cursor, Func<string, bool> arrived)
    {
        using var deadline = new CancellationTokenSource(LogWindow);

        while (true)
        {
            Task changed;
            string slice;

            lock (_gate)
            {
                slice = string.Join(Environment.NewLine, _lines.Skip(cursor.Line));
                changed = _changed.Task;
            }

            if (arrived(slice))
            {
                return slice;
            }

            try
            {
                await changed.WaitAsync(deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException(
                    $"The Host's log did not show the expected line within {LogWindow.TotalSeconds:F0}s. The slice:{Environment.NewLine}{slice}");
            }
        }
    }

    /// <summary>
    /// The Host's own elapsed time for each request finished since <paramref name="cursor"/>, in the
    /// order they finished, read from the request-finished lines (Q5's server-side measurement point).
    /// </summary>
    /// <param name="cursor">Where to start.</param>
    /// <returns>Milliseconds per request.</returns>
    public async Task<IReadOnlyList<double>> ServerDurationsSinceAsync(HostLogCursor cursor)
    {
        var lines = await LinesSinceAsync(cursor).ConfigureAwait(false);

        return [.. lines
            .Select(line => RequestFinished.Match(line))
            .Where(match => match.Success)
            .Select(match => double.Parse(match.Groups["ms"].Value, CultureInfo.InvariantCulture))];
    }

    /// <summary>
    /// Sends one request through <paramref name="request"/> and returns the Host's own elapsed time for
    /// it. The sample <see cref="DurationIndistinguishability.CompareMeasuredAsync"/> takes.
    /// </summary>
    /// <param name="request">Sends exactly one request to this Host.</param>
    /// <returns>Milliseconds, as the Host measured them.</returns>
    public Task<double> ServerDurationOfAsync(Func<Task> request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ServerDurationOfCoreAsync(request);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        _handler.Dispose();

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // It exited between the check and the kill: what the kill was for.
        }

        await _pump.ConfigureAwait(false);
        _process.Dispose();
    }

    private async Task<double> ServerDurationOfCoreAsync(Func<Task> request)
    {
        var cursor = await LogCursorAsync().ConfigureAwait(false);

        await request().ConfigureAwait(false);

        var durations = await ServerDurationsSinceAsync(cursor).ConfigureAwait(false);

        return durations.Count == 1
            ? durations[0]
            : throw new InvalidOperationException($"Expected one request-finished line for one request; found {durations.Count}.");
    }

    private async Task<List<string>> LinesSinceAsync(HostLogCursor cursor)
    {
        using var deadline = new CancellationTokenSource(LogWindow);

        while (true)
        {
            Task changed;
            List<string> slice;
            long expected;

            lock (_gate)
            {
                slice = _lines.GetRange(cursor.Line, _lines.Count - cursor.Line);
                expected = _handler.Sent - cursor.Requests;
                changed = _changed.Task;
            }

            if (slice.Count(line => RequestFinished.IsMatch(line)) >= expected)
            {
                return slice;
            }

            try
            {
                await changed.WaitAsync(deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException(
                    $"The Host wrote {slice.Count(line => RequestFinished.IsMatch(line))} request-finished line(s) for "
                    + $"{expected} request(s) within {LogWindow.TotalSeconds:F0}s. The slice so far:{Environment.NewLine}"
                    + string.Join(Environment.NewLine, slice));
            }
        }
    }

    private void Append(string line)
    {
        TaskCompletionSource changed;

        lock (_gate)
        {
            _lines.Add(line);
            changed = _changed;
            _changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        changed.TrySetResult();
    }

    [GeneratedRegex(@"Request finished .* (?<ms>\d+(\.\d+)?)ms\s*$", RegexOptions.None, 1000)]
    private static partial Regex RequestFinished { get; }

    /// <summary>
    /// Counts what is sent, so a log read knows how many request-finished lines to wait for.
    /// </summary>
    private sealed class CountingHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        private long _sent;

        public long Sent => Interlocked.Read(ref _sent);

        // Counted once answered: a request that never reached the Host writes no line, and counting it
        // would make every later read wait out its window for a line that cannot come.
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            Interlocked.Increment(ref _sent);

            return response;
        }
    }
}

/// <summary>
/// A position in a <see cref="RunningHost"/>'s output: the line count and the requests sent so far.
/// </summary>
/// <param name="Line">Lines written before the cursor.</param>
/// <param name="Requests">Requests sent before the cursor.</param>
public readonly record struct HostLogCursor(int Line, long Requests);
