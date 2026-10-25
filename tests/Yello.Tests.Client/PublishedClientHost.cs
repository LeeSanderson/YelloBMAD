using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Yello.Tests.Client;

/// <summary>
/// The published client served the way Azure Static Web Apps serves it: files as they are, and
/// <c>staticwebapp.config.json</c>'s <c>navigationFallback</c> for everything else. Story 1.10 AC13;
/// Task 7.
/// </summary>
/// <remarks>
/// <para>
/// <b>The published output, not the build output.</b> <c>dotnet publish Yello.Client -c Release</c>
/// into a temporary folder. The published <c>appsettings.Production.json</c> (committed empty, so the
/// boot configuration lists it) is then overwritten with <c>HostBaseAddress</c>, and its precompressed
/// siblings are removed, exactly as the deploy workflow does. The dev server answers every route with
/// <c>index.html</c> by itself, so serving through it would prove nothing about the file.
/// </para>
/// <para>
/// <b>Exactly one rule.</b> A request for a file that exists gets the file. Otherwise a path that
/// matches the fallback's <c>exclude</c> list is a <c>404</c>, and any other path gets the rewrite
/// target with <c>200</c> - the rule Static Web Apps applies, read from the published file, so the
/// fallback under test is the file's and not this server's.
/// </para>
/// <para>
/// <b>No package.</b> <see cref="HttpListener"/> is in the base class library. It cannot bind port 0,
/// so a free port is taken from the operating system first; if another process takes it in between,
/// the listener's own start fails and says so.
/// </para>
/// </remarks>
internal sealed class PublishedClientHost : IAsyncDisposable
{
    private const int StatusOk = 200;

    private const int StatusNotFound = 404;

    /// <summary>
    /// The API host written into the published <c>appsettings.Production.json</c>. Nothing listens there.
    /// </summary>
    public const string ConfiguredApiHost = "api.yello.test";

    private static readonly TimeSpan PublishWindow = TimeSpan.FromMinutes(5);

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".js"] = "text/javascript",
        [".mjs"] = "text/javascript",
        [".css"] = "text/css",
        [".json"] = "application/json",
        [".png"] = "image/png",
        [".ico"] = "image/x-icon",
        [".svg"] = "image/svg+xml",
        [".txt"] = "text/plain",
    };

    private readonly HttpListener _listener;

    private readonly DirectoryInfo _root;

    private readonly DirectoryInfo _publish;

    private readonly List<Regex> _excluded;

    private readonly Dictionary<string, string> _mimeTypes;

    private readonly string _rewrite;

    private readonly Task _serving;

    private PublishedClientHost(DirectoryInfo publish, int port)
    {
        _publish = publish;
        _root = new DirectoryInfo(Path.Combine(publish.FullName, "wwwroot"));

        using (var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root.FullName, "staticwebapp.config.json"))))
        {
            var fallback = config.RootElement.GetProperty("navigationFallback");
            _rewrite = fallback.GetProperty("rewrite").GetString()!;
            _excluded = [.. fallback.GetProperty("exclude").EnumerateArray().Select(item => GlobToRegex(item.GetString()!))];
            _mimeTypes = config.RootElement.TryGetProperty("mimeTypes", out var mime)
                ? mime.EnumerateObject().ToDictionary(pair => pair.Name, pair => pair.Value.GetString()!, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        BaseAddress = new Uri($"http://localhost:{port}/");
        _listener = new HttpListener();
        _listener.Prefixes.Add(BaseAddress.ToString());
        _listener.Start();
        _serving = Task.Run(ServeAsync);
    }

    /// <summary>
    /// Where the static host listens.
    /// </summary>
    public Uri BaseAddress { get; }

    /// <summary>
    /// Publishes the client and starts serving it.
    /// </summary>
    /// <param name="cancellationToken">Cancels the publish.</param>
    /// <returns>The running host.</returns>
    public static async Task<PublishedClientHost> StartAsync(CancellationToken cancellationToken)
    {
        var publish = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "yello-client-publish-" + Guid.NewGuid().ToString("N")));

        try
        {
            await PublishAsync(publish, cancellationToken);

            // What the deploy workflow does with the infrastructure's output: overwrite the published
            // (empty) production settings with the API's address, and drop the precompressed copies.
            var settings = new FileInfo(Path.Combine(publish.FullName, "wwwroot", "appsettings.Production.json"));

            if (!settings.Exists)
            {
                throw new InvalidOperationException("The publish output has no appsettings.Production.json, so the boot configuration cannot list it.");
            }

            await File.WriteAllTextAsync(settings.FullName, $$"""{ "HostBaseAddress": "https://{{ConfiguredApiHost}}/" }""", cancellationToken);
            File.Delete(settings.FullName + ".br");
            File.Delete(settings.FullName + ".gz");

            return new PublishedClientHost(publish, FreePort());
        }
        catch
        {
            publish.Delete(recursive: true);
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _listener.Stop();
        _listener.Close();

        try
        {
            await _serving;
        }
        catch (HttpListenerException)
        {
            // The listener was stopped under a pending GetContextAsync, which is how serving ends.
        }
        catch (ObjectDisposedException)
        {
            // The same, on platforms that report it this way.
        }
        finally
        {
            _publish.Delete(recursive: true);
        }
    }

    private static async Task PublishAsync(DirectoryInfo output, CancellationToken cancellationToken)
    {
        var project = Path.Combine(RepositoryRoot().FullName, "Yello.Client", "Yello.Client.csproj");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in new[] { "publish", project, "-c", "Release", "-o", output.FullName, "--nologo" })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start `dotnet publish`.");

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(PublishWindow);

        try
        {
            await process.WaitForExitAsync(deadline.Token);
        }
        catch (OperationCanceledException)
        {
            // A publish past its window is killed rather than orphaned, so its files can be deleted.
            process.Kill(entireProcessTree: true);
            throw;
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"`dotnet publish Yello.Client` exited {process.ExitCode}:\n{await stdout}\n{await stderr}");
        }
    }

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();

        return probe.LocalEndpoint is IPEndPoint endpoint ? endpoint.Port : throw new InvalidOperationException("The probe listener has no port.");
    }

    private static DirectoryInfo RepositoryRoot()
    {
        for (var candidate = new DirectoryInfo(AppContext.BaseDirectory); candidate is not null; candidate = candidate.Parent)
        {
            if (candidate.EnumerateFiles("Yello.slnx").Any())
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root (Yello.slnx).");
    }

    /// <summary>
    /// A Static Web Apps route glob as a regular expression: <c>*</c> is any run of characters and
    /// <c>{a,b}</c> is either alternative. A glob with no leading <c>/</c> matches at the end of a path.
    /// </summary>
    private static Regex GlobToRegex(string glob)
    {
        var pattern = Regex.Escape(glob)
            .Replace(@"\*", ".*", StringComparison.Ordinal)
            .Replace(@"\{", "(", StringComparison.Ordinal)
            .Replace("}", ")", StringComparison.Ordinal)
            .Replace(",", "|", StringComparison.Ordinal);

        return new Regex("^" + (glob.StartsWith('/') ? string.Empty : ".*") + pattern + "$", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
    }

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            var context = await _listener.GetContextAsync();

            try
            {
                await RespondAsync(context);
            }
            catch (HttpListenerException)
            {
                // The browser went away mid-response.
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                // One bad request is answered 500 and the host keeps serving, so a later request does
                // not hang until the browser's timeout.
                context.Response.StatusCode = 500;
            }
            finally
            {
                context.Response.Close();
            }
        }
    }

    private async Task RespondAsync(HttpListenerContext context)
    {
        var path = Uri.UnescapeDataString(context.Request.Url!.AbsolutePath);
        var file = path.EndsWith('/') ? null : Resolve(path);

        if (file is null)
        {
            if (!path.Equals("/", StringComparison.Ordinal) && _excluded.Exists(rule => rule.IsMatch(path)))
            {
                context.Response.StatusCode = StatusNotFound;

                return;
            }

            file = Resolve(_rewrite) ?? throw new InvalidOperationException($"The rewrite target {_rewrite} is not in the published output.");
        }

        context.Response.StatusCode = StatusOk;
        context.Response.ContentType = _mimeTypes.TryGetValue(file.Extension, out var configured)
            ? configured
            : ContentTypes.GetValueOrDefault(file.Extension, "application/octet-stream");

        await using var stream = file.OpenRead();
        context.Response.ContentLength64 = stream.Length;
        await stream.CopyToAsync(context.Response.OutputStream);
    }

    private FileInfo? Resolve(string path)
    {
        var full = Path.GetFullPath(Path.Combine(_root.FullName, path.TrimStart('/')));

        return full.StartsWith(Path.TrimEndingDirectorySeparator(_root.FullName) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(full)
            ? new FileInfo(full)
            : null;
    }
}
