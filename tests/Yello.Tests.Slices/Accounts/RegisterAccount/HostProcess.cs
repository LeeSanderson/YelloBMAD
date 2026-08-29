using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

namespace Yello.Tests.Slices.Accounts.RegisterAccount;

/// <summary>
/// Boots <c>Yello.Host</c> as a real process, talks to it over HTTP, and hands back everything it
/// wrote to stdout.
/// </summary>
/// <remarks>
/// <para>
/// <b>A process, not a <c>WebApplicationFactory</c>.</b> There is no
/// <c>Microsoft.AspNetCore.Mvc.Testing</c> in this solution and no central pin for one, so
/// in-memory hosting is not available - and story 1.1 established the process-booting pattern for
/// exactly this reason (<c>HostStartupTests.RunHostAsync</c>). It also buys something the
/// in-memory factory does not: <c>Program.cs</c> genuinely runs, so deleting the endpoint
/// registration fails these tests rather than leaving them green against a pipeline assembled by
/// the test itself. Story 1.1 found that shape of hole the hard way - deleting an entire startup
/// call left 52 of 52 tests passing.
/// </para>
/// <para>
/// <b>The captured stdout is not a convenience.</b> It is the evidence for AC5's "the password
/// appears in none of them - every log" and for AD-23's reach into logging: a response that says
/// nothing while a log line says which path was taken is the same leak arriving by a slower
/// route.
/// </para>
/// </remarks>
internal static class HostProcess
{
    /// <summary>
    /// Kestrel's line, which is how a caller learns which port was allocated.
    /// </summary>
    private const string ListeningMarker = "Now listening on:";

    private static readonly TimeSpan StartupWindow = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The Host's executable, named for whichever platform produced it.
    /// </summary>
    private static readonly string[] ExecutableNames = ["Yello.Host.exe", "Yello.Host"];

    /// <summary>
    /// Starts the Host, runs <paramref name="work"/> against it, stops it, and returns its log.
    /// </summary>
    /// <param name="connectionString">The container's connection string, injected as configuration.</param>
    /// <param name="work">What to do while it is up. Receives a client bound to its address.</param>
    /// <returns>Everything the Host wrote to stdout.</returns>
    public static async Task<string> RunAsync(string connectionString, Func<HttpClient, Task> work)
    {
        var executable = Locate();

        var startInfo = new ProcessStartInfo(executable.FullName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = executable.DirectoryName,
        };

        // Production, so the startup connectivity check returns at its environment guard rather
        // than opening a connection these tests do not need. The endpoint reaches the database on
        // its own, when a request arrives.
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        startInfo.Environment[$"ConnectionStrings__{HostMetadata.DatabaseResourceName}"] = connectionString;
        startInfo.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start '{executable.FullName}'.");

        var lines = new ConcurrentQueue<string>();
        var listening = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var pump = Task.Run(async () =>
        {
            while (await process.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                lines.Enqueue(line);

                var marker = line.IndexOf(ListeningMarker, StringComparison.Ordinal);

                if (marker >= 0)
                {
                    listening.TrySetResult(line[(marker + ListeningMarker.Length)..].Trim());
                }
            }
        });

        try
        {
            // Waiting on the condition, never on the clock: Task.Delay as a synchronisation
            // mechanism is banned outright by this repository's conventions.
            using var deadline = new CancellationTokenSource(StartupWindow);

            var address = await listening.Task.WaitAsync(deadline.Token).ConfigureAwait(false);

            using var client = new HttpClient { BaseAddress = new Uri(address) };

            await work(client).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            lines.Enqueue(string.Create(
                CultureInfo.InvariantCulture,
                $"(the Host did not report an address within {StartupWindow.TotalSeconds:F0}s)"));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await pump.ConfigureAwait(false);
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// <c>Yello.Host</c>'s build output, by the same convention <c>HostStartupTests</c> uses: the
    /// tail of this assembly's path below its own project directory.
    /// </summary>
    private static FileInfo Locate()
    {
        var root = RepositoryRoot();
        var ownProjectDirectory = Path.Combine(root.FullName, "tests", "Yello.Tests.Slices");
        var tail = Path.GetRelativePath(ownProjectDirectory, AppContext.BaseDirectory);

        if (tail.StartsWith("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "This assembly's output layout is not the one the convention assumes, so " +
                "Yello.Host's executable cannot be located from it. Build the solution normally " +
                "(`dotnet build Yello.slnx`) rather than with a custom ArtifactsPath.");
        }

        var candidates = ExecutableNames
            .Select(name => new FileInfo(Path.GetFullPath(Path.Combine(root.FullName, "Yello.Host", tail, name))));

        return candidates.FirstOrDefault(file => file.Exists)
            ?? throw new InvalidOperationException(
                $"Yello.Host's executable was not found under '{Path.Combine(root.FullName, "Yello.Host", tail)}'. " +
                "Run `dotnet build Yello.slnx` before this suite.");
    }

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
