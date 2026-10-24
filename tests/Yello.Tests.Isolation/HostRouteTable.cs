using System.Diagnostics;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Yello.Host;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// The real Host's route table, read from the Host's own endpoint data source (story 1.9 Q4 (a)).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing is hand-listed and nothing is added to the product.</b> The Host's entry point runs
/// in-process - <c>Program.cs</c> exactly as it ships, Production environment, an http loopback port -
/// and the host it builds is captured from the <c>Microsoft.Extensions.Hosting</c> diagnostic events,
/// the same hook <c>dotnet ef</c> uses to reach a minimal-API application's services. Once the
/// application has started, every endpoint <c>Program.cs</c> mapped is in its
/// <see cref="EndpointDataSource"/>; the routes are read and the application is stopped.
/// </para>
/// <para>
/// <b>Why not the <c>Yello.Contracts</c> constants (Q4 (b)).</b> A route mapped without a constant
/// would be invisible to a reflection scan, and that is the route the gap gate most needs to see.
/// </para>
/// </remarks>
internal static class HostRouteTable
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    private static readonly SemaphoreSlim OneAtATime = new(1, 1);

    /// <summary>
    /// Every endpoint's methods and raw route pattern.
    /// </summary>
    /// <returns>The route table.</returns>
    public static async Task<IReadOnlyList<(string Method, string Pattern)>> ReadAsync()
    {
        await OneAtATime.WaitAsync();

        try
        {
            return await ReadCoreAsync();
        }
        finally
        {
            OneAtATime.Release();
        }
    }

    private static async Task<IReadOnlyList<(string Method, string Pattern)>> ReadCoreAsync()
    {
        var built = new TaskCompletionSource<IHost>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var observer = new HostingObserver(built);
        using var subscription = DiagnosticListener.AllListeners.Subscribe(observer);

        var entryPoint = typeof(AssemblyMarker).Assembly.EntryPoint
            ?? throw new InvalidOperationException("Yello.Host has no entry point.");

        var resourceKey = HostProcess.ConnectionStringKey.Replace("__", ":", StringComparison.Ordinal);
        string[] arguments =
        [
            "--environment", "Production",
            "--urls", "http://127.0.0.1:0",
            $"--{resourceKey}", "Server=127.0.0.1,1;Database=unused;User ID=sa;Password=Not$Used1;TrustServerCertificate=true",
        ];

        var run = Task.Factory.StartNew(
            () => entryPoint.Invoke(null, [arguments]),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        // Bounded: an entry point that neither builds a host nor returns must fail the gate, not hang it.
        var host = await Task.WhenAny(built.Task, run).WaitAsync(Window) == built.Task
            ? await built.Task
            : throw new InvalidOperationException("Yello.Host's entry point returned before it built a host.", run.Exception);

        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        List<(string Method, string Pattern)> routes;

        try
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            using (lifetime.ApplicationStarted.Register(() => started.TrySetResult()))
            {
                if (await Task.WhenAny(started.Task, run).WaitAsync(Window) != started.Task)
                {
                    throw new InvalidOperationException("Yello.Host's entry point returned before the application started.", run.Exception);
                }
            }

            routes = host.Services.GetRequiredService<EndpointDataSource>().Endpoints
                .OfType<RouteEndpoint>()
                .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["*"])
                    .Select(method => (Method: method, Pattern: endpoint.RoutePattern.RawText ?? string.Empty)))
                .ToList();
        }
        finally
        {
            // Stopped whatever happened above, so a failed read never leaves a Host running in the
            // test process.
            lifetime.StopApplication();
        }

        await run.WaitAsync(Window);

        return routes;
    }

    /// <summary>
    /// Captures the host <c>Program.cs</c> builds, from the hosting diagnostic events.
    /// </summary>
    private sealed class HostingObserver(TaskCompletionSource<IHost> built)
        : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly List<IDisposable> _subscriptions = [];

        public void OnNext(DiagnosticListener value)
        {
            if (string.Equals(value.Name, "Microsoft.Extensions.Hosting", StringComparison.Ordinal))
            {
                _subscriptions.Add(value.Subscribe(this));
            }
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (string.Equals(value.Key, "HostBuilt", StringComparison.Ordinal) && value.Value is IHost host)
            {
                built.TrySetResult(host);
            }
        }

        public void OnCompleted()
        {
            // Nothing to do: the listeners outlive this read.
        }

        public void OnError(Exception error) => built.TrySetException(error);

        public void Dispose()
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }
        }
    }
}
