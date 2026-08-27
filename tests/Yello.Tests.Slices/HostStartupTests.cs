using System.Diagnostics;
using Xunit;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices;

/// <summary>
/// AC4, asserted against the real Host process: it opens a connection to the container in
/// Development, and it does not touch the database outside Development.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a process and not a method call.</b> <see cref="StartupConnectivityCheckTests"/> calls
/// <c>StartupConnectivityCheck.RunAsync</c> directly, which asserts the check's own logic but
/// never executes <c>Yello.Host/Program.cs</c>. The third review pass demonstrated the
/// consequence: deleting the entire <c>await StartupConnectivityCheck.RunAsync(...)</c> block
/// from <c>Program.cs</c> left the whole solution at 52/52 green. A comment in that file claimed
/// the new tests caught "deleting the call, inverting the environment guard inside it or dropping
/// this await", and only the middle one was true. Booting the real entry point is what makes all
/// three true, and it is why AC4's evidence should stop needing rework.
/// </para>
/// <para>
/// <b>What each case proves.</b> The Development case is AC4's own words - "a working connection
/// from Host to container" - observed from the Host's own log rather than from a transcript
/// pasted into a story file, which is what went stale behind two rewrites. The non-Development
/// case is the inverted-guard regression, and it deliberately needs <b>no container</b>: the
/// check returns at the environment test before it reads any configuration, so an inverted guard
/// shows up as a connectivity failure whether or not anything is listening. That case therefore
/// runs everywhere, including on a machine or CI leg with no container runtime.
/// </para>
/// <para>
/// AR-33 and AR-36 are unaffected and still visible here: exactly one connection, at startup,
/// before Kestrel binds, with no probe, no timer and no migration.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "AR-1")]
[Trait("Requirement", "AR-33")]
[Trait("Requirement", "AR-36")]
public sealed class HostStartupTests
{
    /// <summary>
    /// The log message <c>StartupLog.ConnectivityConfirmed</c> produces.
    /// </summary>
    private const string ConnectedMessage = "Connected to SQL Server";

    /// <summary>
    /// The log message <c>StartupLog.ConnectivityCheckSkipped</c> produces.
    /// </summary>
    private const string SkippedMessage = "Startup connectivity check skipped";

    /// <summary>
    /// How long to let the Host run before concluding it has said what it is going to say.
    /// </summary>
    private static readonly TimeSpan StartupWindow = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The Host's executable, named for whichever platform produced it.
    /// </summary>
    private static readonly string[] ExecutableNames = ["Yello.Host.exe", "Yello.Host"];

    /// <summary>
    /// Log fragments that mean the connectivity check has finished, one way or another.
    /// </summary>
    private static readonly string[] StartupFinishedMarkers =
    [
        ConnectedMessage,
        SkippedMessage,
        "connection string was injected",
        "connection string is not usable",
        "Could not open the injected",
        "Gave up waiting for SQL Server",
        "Now listening on",
    ];

    [Fact]
    public async Task The_Host_opens_a_connection_to_the_container_at_startup_in_Development()
    {
        Assert.SkipUnless(
            SqlServerContainerFixture.IsContainerRuntimeAvailable(),
            "No container runtime is reachable, so there is no engine for the Host to connect to.");

        await using var fixture = new SqlServerContainerFixture();
        await fixture.InitializeAsync();

        var output = await RunHostAsync("Development", fixture.ConnectionString);

        Assert.Contains(ConnectedMessage, output, StringComparison.Ordinal);
        Assert.DoesNotContain(SkippedMessage, output, StringComparison.Ordinal);
    }

    /// <summary>
    /// The inverted-guard regression. Needs no container, deliberately - see the class remarks.
    /// </summary>
    [Fact]
    public async Task The_Host_does_not_touch_the_database_outside_Development()
    {
        // A connection string that is well-formed and points at nothing. If the guard were
        // inverted the Host would try it and log a connectivity failure or a timeout; because the
        // guard holds, it never reads this value at all.
        const string unreachable = "Server=127.0.0.1,1;Database=yello;User ID=sa;Password=Not$Used1;TrustServerCertificate=true";

        var output = await RunHostAsync("Production", unreachable);

        Assert.Contains(SkippedMessage, output, StringComparison.Ordinal);
        Assert.DoesNotContain(ConnectedMessage, output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Starts <c>Yello.Host</c>, collects its console output until it has bound or the window
    /// closes, then stops it.
    /// </summary>
    private static async Task<string> RunHostAsync(string environment, string connectionString)
    {
        var executable = HostExecutable();

        var startInfo = new ProcessStartInfo(executable.FullName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = executable.DirectoryName,
        };

        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = environment;

        // The double underscore is configuration's separator for a nested key, so this is
        // ConnectionStrings:<resource>. The resource name is read from the Host's own assembly
        // metadata rather than written here, for the same reason the production code reads it:
        // a literal would be a second source of truth, and a gate asserts there are none.
        startInfo.Environment[$"ConnectionStrings__{HostMetadata.DatabaseResourceName}"] = connectionString;

        // Port 0 so a developer running this alongside `aspire run` does not collide.
        startInfo.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start '{executable.FullName}'.");

        // Read the Host's log as it is written and stop at the first line that shows the
        // connectivity check has finished, rather than waiting out the window: the Host runs
        // until it is stopped, so waiting for exit would cost the full deadline on every case.
        // No Task.Delay anywhere - the convention this story established bans sleeping as a
        // synchronisation mechanism, and reading the stream is the real signal.
        var log = new List<string>();

        try
        {
            using var window = new CancellationTokenSource(StartupWindow);

            var finished = false;

            while (!finished && await process.StandardOutput.ReadLineAsync(window.Token) is { } line)
            {
                log.Add(line);
                finished = StartupFinished(line);
            }
        }
        catch (OperationCanceledException)
        {
            log.Add(
                "(the Host produced no line showing the connectivity check had finished within " +
                $"{StartupWindow.TotalSeconds:F0}s)");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }

        return string.Join(Environment.NewLine, log);
    }

    /// <summary>
    /// Whether a log line shows the startup connectivity check has run, whatever it concluded.
    /// </summary>
    /// <remarks>
    /// Kestrel's "Now listening on" is emitted after the check, so it is the backstop that says
    /// "the check is behind us" even when the check itself logged something unanticipated - which
    /// is what keeps a missing-check regression from looking like a timeout.
    /// </remarks>
    private static bool StartupFinished(string line) =>
        Array.Exists(StartupFinishedMarkers, m => line.Contains(m, StringComparison.Ordinal));

    /// <summary>
    /// <c>Yello.Host</c>'s own build output, located by the same convention
    /// <see cref="SqlServerContainerFixture"/>'s consumers rely on: the tail of this assembly's
    /// path below its project directory.
    /// </summary>
    /// <remarks>
    /// Throws rather than skipping when it cannot be found. A test that cannot locate the thing
    /// it asserts is a test that cannot answer, and the second review pass's highest-severity
    /// finding was a gate that quietly narrowed its own scope instead of failing.
    /// </remarks>
    private static FileInfo HostExecutable()
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

        return candidates.FirstOrDefault(f => f.Exists)
            ?? throw new InvalidOperationException(
                $"Yello.Host's executable was not found under '{Path.Combine(root.FullName, "Yello.Host", tail)}'. " +
                "Run `dotnet build Yello.slnx` before this suite: AC4's evidence is the Host " +
                "actually starting, so there is nothing to assert without it.");
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
