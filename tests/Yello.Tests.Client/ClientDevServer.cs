using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace Yello.Tests.Client;

/// <summary>
/// Serves <c>Yello.Client</c> over HTTP for the lifetime of a browser suite run, and hands back
/// the address the operating system allocated.
/// </summary>
/// <remarks>
/// <para>
/// <b>A real process serving the real build output.</b> The same shape as
/// <c>Yello.Tests.Slices</c>'s <c>HostProcess</c>, and for the same reason story 1.1 established
/// it: <c>Program.cs</c> genuinely runs. That is not incidental here - it is the entire subject.
/// The defect this suite exists to catch was in the client's composition root, threw before the
/// first render, and was invisible to 136 tests that all read files. A harness that assembled the
/// application itself would reproduce that blindness exactly.
/// </para>
/// <para>
/// <b>Why <c>dotnet run</c> rather than an executable path.</b> <c>HostProcess</c> can launch
/// <c>Yello.Host.exe</c> directly because an ASP.NET Core project produces one. Yello.Client is a
/// WebAssembly application: its output is static assets plus managed assemblies, and the thing
/// that serves them is <c>Microsoft.AspNetCore.Components.WebAssembly.DevServer</c>, reached
/// through the SDK's run target. So the launch goes through <c>dotnet run</c>, with
/// <c>--no-build</c> so this never competes with the build that produced it.
/// </para>
/// <para>
/// <b><c>--no-launch-profile</c> is load-bearing.</b> The <c>http</c> profile in
/// <c>Yello.Client/Properties/launchSettings.json</c> sets <c>launchBrowser: true</c> and a fixed
/// <c>applicationUrl</c> of port 5131. Honouring it would open a real browser window on whoever
/// ran the suite and would collide with a developer already serving the client on that port.
/// </para>
/// <para>
/// <b>The configuration this suite was built in.</b> <c>dotnet run</c> defaults to Debug, so
/// without <c>--configuration</c> a Release build (CI's) serves a Debug output that does not
/// exist, while the build-output check below looks at the Release one and passes.
/// </para>
/// <para>
/// <b>Port 0, and the address read back from the process.</b> Asking the operating system for a
/// free port and reading Kestrel's own line is the only form with no race in it: choosing a port
/// by probing it first leaves a window between the probe and the bind, which is the classic way a
/// suite becomes intermittently red on a busy machine.
/// </para>
/// </remarks>
internal sealed class ClientDevServer : IAsyncDisposable
{
    /// <summary>
    /// Kestrel's line, which is how the allocated port is learned.
    /// </summary>
    private const string ListeningMarker = "Now listening on:";

    /// <summary>
    /// How long to wait for the dev server to report an address.
    /// </summary>
    /// <remarks>
    /// Generous because the first run after a build does real work - the SDK resolves static web
    /// assets before Kestrel binds - and because a hang in CI is worse than a slow pass. Never
    /// finishing is a different failure from taking a while, and only one of them is acceptable.
    /// </remarks>
    private static readonly TimeSpan StartupWindow = TimeSpan.FromSeconds(120);

    private readonly Process _process;
    private readonly Task _pump;
    private readonly ConcurrentQueue<string> _output;

    private ClientDevServer(Process process, Task pump, ConcurrentQueue<string> output, Uri baseAddress)
    {
        _process = process;
        _pump = pump;
        _output = output;
        BaseAddress = baseAddress;
    }

    /// <summary>
    /// The address the dev server is listening on, allocated by the operating system.
    /// </summary>
    public Uri BaseAddress { get; }

    /// <summary>
    /// Everything the dev server has written so far, for a failure message that can explain
    /// itself.
    /// </summary>
    public string Output => string.Join(Environment.NewLine, _output);

    /// <summary>
    /// Starts the dev server and waits until it reports an address.
    /// </summary>
    /// <returns>A running server. Dispose it to stop the process.</returns>
    public static async Task<ClientDevServer> StartAsync()
    {
        var root = RepositoryRoot();
        var project = ClientProject(root);

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = root.FullName,
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(project.FullName);
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add(BuildConfiguration());
        startInfo.ArgumentList.Add("--no-launch-profile");
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add("http://127.0.0.1:0");

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Could not start `dotnet run` for Yello.Client. The .NET SDK must be on PATH; " +
                "global.json pins 10.0.303 (rollForward latestPatch).");

        var output = new ConcurrentQueue<string>();
        var listening = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);

        var pump = Task.WhenAll(
            PumpAsync(process.StandardOutput, output, listening),
            PumpAsync(process.StandardError, output, listening));

        try
        {
            // Waiting on the condition, never on the clock: Task.Delay as a synchronisation
            // mechanism is banned outright by this repository's conventions.
            using var deadline = new CancellationTokenSource(StartupWindow);

            var address = await listening.Task.WaitAsync(deadline.Token).ConfigureAwait(false);

            return new ClientDevServer(process, pump, output, address);
        }
        catch (OperationCanceledException exception)
        {
            await StopAsync(process, pump).ConfigureAwait(false);

            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Yello.Client's dev server did not report an address within {StartupWindow.TotalSeconds:F0}s. ") +
                "It is launched with --no-build, so the most likely cause is that the client has " +
                "not been built: run `dotnet build Yello.slnx` first. What it wrote:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, output),
                exception);
        }
    }

    /// <summary>
    /// Stops the dev server.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync(_process, _pump).ConfigureAwait(false);
        _process.Dispose();
    }

    /// <summary>
    /// Reads a stream line by line, recording everything and completing
    /// <paramref name="listening"/> at the first address reported.
    /// </summary>
    private static async Task PumpAsync(
        StreamReader reader,
        ConcurrentQueue<string> output,
        TaskCompletionSource<Uri> listening)
    {
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            output.Enqueue(line);

            var marker = line.IndexOf(ListeningMarker, StringComparison.Ordinal);

            if (marker < 0)
            {
                continue;
            }

            var address = line[(marker + ListeningMarker.Length)..].Trim();

            if (Uri.TryCreate(address, UriKind.Absolute, out var parsed))
            {
                listening.TrySetResult(parsed);
            }
        }
    }

    private static async Task StopAsync(Process process, Task pump)
    {
        if (!process.HasExited)
        {
            // entireProcessTree, because `dotnet run` is a launcher: the dev server that holds
            // the port is its child, and killing only the parent leaves the port bound.
            process.Kill(entireProcessTree: true);
        }

        await pump.ConfigureAwait(false);
    }

    /// <summary>
    /// <c>Yello.Client</c>'s project file, with the build output checked first so a missing
    /// build fails by name rather than as a timeout.
    /// </summary>
    private static FileInfo ClientProject(DirectoryInfo root)
    {
        var project = new FileInfo(Path.Combine(root.FullName, "Yello.Client", "Yello.Client.csproj"));

        if (!project.Exists)
        {
            throw new InvalidOperationException(
                $"Yello.Client's project file was not found at '{project.FullName}'. The " +
                "repository root was located by walking up for Yello.slnx, so this means the " +
                "source tree is not the one the Structural Seed describes.");
        }

        // The same convention HostProcess uses: the tail of this assembly's own path below its
        // project directory is where a sibling project's output sits too.
        var ownProjectDirectory = Path.Combine(root.FullName, "tests", "Yello.Tests.Client");
        var tail = Path.GetRelativePath(ownProjectDirectory, AppContext.BaseDirectory);

        if (tail.StartsWith("..", StringComparison.Ordinal))
        {
            // A custom ArtifactsPath, or a published test project. Not a failure on its own -
            // --no-build may still be satisfiable - so the launch proceeds and any real problem
            // arrives as the startup timeout, which names this case.
            return project;
        }

        var assembly = new FileInfo(
            Path.GetFullPath(Path.Combine(root.FullName, "Yello.Client", tail, "Yello.Client.dll")));

        return assembly.Exists
            ? project
            : throw new InvalidOperationException(
                $"Yello.Client has not been built: '{assembly.FullName}' does not exist. This " +
                "suite serves the real build output and launches it with --no-build, so run " +
                "`dotnet build Yello.slnx` before it.");
    }

    private static string BuildConfiguration() =>
        typeof(ClientDevServer).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration
        ?? throw new InvalidOperationException(
            "The test assembly carries no AssemblyConfigurationAttribute, so the configuration " +
            "to serve Yello.Client in is unknown.");

    private static DirectoryInfo RepositoryRoot()
    {
        var candidate = new DirectoryInfo(AppContext.BaseDirectory);

        while (candidate is not null)
        {
            if (candidate.EnumerateFiles("Yello.slnx").Any())
            {
                return candidate;
            }

            candidate = candidate.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate the repository root by walking up from " +
            $"'{AppContext.BaseDirectory}' looking for Yello.slnx.");
    }
}
