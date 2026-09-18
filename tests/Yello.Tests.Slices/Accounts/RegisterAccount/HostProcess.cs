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
    /// Kestrel's line, which is how a caller learns which port was allocated - and the boundary
    /// between the Host's startup output and everything a request caused.
    /// </summary>
    internal const string ListeningMarker = "Now listening on:";

    private static readonly TimeSpan StartupWindow = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The Host's executable, named for whichever platform produced it.
    /// </summary>
    private static readonly string[] ExecutableNames = ["Yello.Host.exe", "Yello.Host"];

    /// <summary>
    /// The empty configuration the two-argument overload passes.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> NoExtraSettings =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Starts the Host, runs <paramref name="work"/> against it, stops it, and returns its log.
    /// </summary>
    /// <param name="connectionString">The container's connection string, injected as configuration.</param>
    /// <param name="work">What to do while it is up. Receives a client bound to its address.</param>
    /// <returns>Everything the Host wrote to stdout.</returns>
    public static Task<string> RunAsync(string connectionString, Func<HttpClient, Task> work) =>
        RunAsync(connectionString, NoExtraSettings, work);

    /// <summary>
    /// Starts the Host with extra configuration, runs <paramref name="work"/> against it, stops it,
    /// and returns its log.
    /// </summary>
    /// <param name="connectionString">The container's connection string, injected as configuration.</param>
    /// <param name="settings">
    /// Extra configuration, by its environment-variable name - so <c>Session:Lifetime</c> is passed
    /// as <c>Session__Lifetime</c>, which is the spelling .NET's environment provider reads.
    /// </param>
    /// <param name="work">What to do while it is up. Receives a client bound to its address.</param>
    /// <returns>Everything the Host wrote to stdout.</returns>
    /// <remarks>
    /// <b>Added by story 1.4, and the reason is AC3.</b> A Session expiring eight hours from now
    /// cannot be watched expiring by any test this repository permits - <c>Task.Delay</c> as a
    /// synchronisation mechanism is banned outright, and the Host is a separate process so no
    /// in-process <c>TimeProvider</c> reaches it. Starting it with a NEGATIVE lifetime makes every
    /// ticket born already expired, which turns "an expired Session grants nothing" into an
    /// ordinary assertion against a real cookie and the framework's own expiry evaluation.
    /// </remarks>
    public static Task<string> RunAsync(
        string connectionString,
        IReadOnlyDictionary<string, string> settings,
        Func<HttpClient, Task> work)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Split from the body below because S4457 requires an argument check to run eagerly rather
        // than on the first await of a state machine.
        return RunCoreAsync(connectionString, settings, work);
    }

    private static async Task<string> RunCoreAsync(
        string connectionString,
        IReadOnlyDictionary<string, string> settings,
        Func<HttpClient, Task> work)
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
        // HTTPS, FROM STORY 1.4, AND IT IS NOT A PREFERENCE.
        //
        // AC5 requires the Session cookie to carry `Secure`, so the Host configures
        // `CookieSecurePolicy.Always` - and ASP.NET Core's antiforgery system REFUSES OUTRIGHT to
        // issue a token when that policy is set and the request is not an SSL request:
        // "The antiforgery system has the configuration value AntiforgeryOptions.Cookie.
        // SecurePolicy = Always, but the current request is not an SSL request." Found by running
        // these tests over http on 2026-09-18, where it arrived as a 500 with an empty body.
        //
        // The framework is right and the test was wrong. Weakening the policy to SameAsRequest to
        // make a suite pass would have removed `Secure` from a production cookie to accommodate a
        // test - which is AC5 traded for convenience. Serving these tests over https instead means
        // the transport under test is the one the product actually runs on, and it is what makes
        // the `Secure` attribute assertion beside it mean something rather than describing a header
        // no browser would have honoured.
        //
        // Needs the .NET development certificate, which `dotnet dev-certs https` creates. It is not
        // TRUSTED by this client - see the validation callback below - so no machine state beyond
        // the certificate's existence is required.
        startInfo.Environment["ASPNETCORE_URLS"] = "https://127.0.0.1:0";

        foreach (var (key, value) in settings)
        {
            startInfo.Environment[key] = value;
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start '{executable.FullName}'.");

        var lines = new ConcurrentQueue<string>();
        var listening = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        // BOTH streams, into one queue. Until code review on 2026-09-10 stderr was redirected and
        // then never read: RedirectStandardError = true creates the pipe, and nothing drained it.
        //
        // The consequence that matters is an assertion one, not a plumbing one. AC5's requirement
        // is that the password appears in NO log line - "every log" - and the likeliest way a
        // submitted value escapes into output is an unhandled exception's stack trace, which
        // ASP.NET Core writes to stderr. So the single test standing behind AC5 was reading the
        // one stream the leak would not be on, and would have reported green through it.
        //
        // The plumbing consequence is real too: an undrained pipe fills at around 4 KB and then
        // blocks the Host mid-write, which arrived here as the startup-timeout line below and read
        // like a container fault.
        async Task PumpAsync(StreamReader reader)
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                lines.Enqueue(line);

                var marker = line.IndexOf(ListeningMarker, StringComparison.Ordinal);

                if (marker >= 0)
                {
                    listening.TrySetResult(line[(marker + ListeningMarker.Length)..].Trim());
                }
            }
        }

        var pump = Task.WhenAll(
            Task.Run(() => PumpAsync(process.StandardOutput)),
            Task.Run(() => PumpAsync(process.StandardError)));

        try
        {
            // Waiting on the condition, never on the clock: Task.Delay as a synchronisation
            // mechanism is banned outright by this repository's conventions.
            using var deadline = new CancellationTokenSource(StartupWindow);

            // Awaited in its own step, and caught in its own clause. The startup wait and the work
            // used to share a try, so HttpClient's 100-second timeout inside `work` - an
            // OperationCanceledException - was recorded as "the Host did not report an address",
            // which is a diagnostic pointing at the wrong half of the test. A request that hangs
            // against a Host that started perfectly well now fails as itself.
            string? address = null;

            try
            {
                address = await listening.Task.WaitAsync(deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                lines.Enqueue(string.Create(
                    CultureInfo.InvariantCulture,
                    $"(the Host did not report an address within {StartupWindow.TotalSeconds:F0}s)"));
            }

            // Not an early return: the finally below is what drains the pump, and a return
            // statement here would compose the result string before those last lines arrived.
            if (address is not null)
            {
                // COOKIES ARE NOT HANDLED FOR THE CALLER, from story 1.4 onwards, and that is a
                // deliberate change rather than a default left alone.
                //
                // Two reasons, and the second is the one that matters. A container would attach
                // cookies invisibly, so an assertion that a request "was authenticated" could pass
                // without anyone having said which cookie carried it - and the Session cookie's
                // attributes ARE AC5's contract, so the test that reads them has to read the
                // header. And .NET's CookieContainer refuses to store a `Secure` cookie received
                // over http, which this Host is: with handling on, every Session cookie issued here
                // would be silently dropped and every round trip would fail for a reason no
                // assertion would name.
                using var handler = new HttpClientHandler
                {
                    UseCookies = false,

                    // The development certificate is self-signed and this process has no reason to
                    // trust the machine's root store - a suite that required `dotnet dev-certs
                    // https --trust` would fail on a cold CI agent for a reason unrelated to
                    // anything it asserts. Scoped to the loopback address this test started the
                    // Host on, so it cannot quietly accept a bad certificate from anywhere else.
                    ServerCertificateCustomValidationCallback = (request, _, _, _) =>
                        request.RequestUri?.IsLoopback == true,
                };

                using var client = new HttpClient(handler) { BaseAddress = new Uri(address) };

                await work(client).ConfigureAwait(false);
            }
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
