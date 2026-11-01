using System.Text.Json;

namespace Yello.Tests.Shared;

/// <summary>
/// A folder a Host started by these suites drops every email it sends into, and the reader for it: the recording
/// sender story 4.1 AC11 asks for, in the only shape a Host started as a separate process allows. Written by the story
/// 4.1 ATDD run (2026-11-01).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a folder and not an in-process fake.</b> The story's Task 6 says "tests replace the sender with a recording
/// fake", but every wire suite starts <c>Yello.Host</c> as a real process (<see cref="HostProcess"/>; there is no
/// <c>WebApplicationFactory</c> in this solution), so no test can register a service in it, and 1.9 Q4 forbids test-only
/// code in production assemblies. The Host's own file-drop sender (AC11's Development sender) is therefore the
/// recording sender, pointed at a folder the test owns.
/// </para>
/// <para>
/// <b>Assumed for the dev phase, recorded in the ATDD checklist as Q13 (a):</b>
/// <list type="bullet">
/// <item>The sender is chosen by configuration: <c>ConnectionStrings:acs</c> present - the ACS adapter; otherwise
/// <c>Email:DropFolder</c> present - the file-drop sender, in <b>any</b> environment (Development defaults it to a
/// folder git ignores); otherwise startup fails loudly. Every Host these suites start runs as <c>Production</c>
/// (<see cref="HostProcess.CreateStartInfo"/>), so the drop folder cannot be Development-only.</item>
/// <item>The sender writes one <c>*.json</c> file per message, whole (written to a temporary name, then moved), holding
/// exactly the body it would <c>POST</c> to ACS Email (<c>senderAddress</c>, <c>recipients.to[].address</c>,
/// <c>content.subject</c>, <c>content.plainText</c>, optional <c>content.html</c>).</item>
/// <item><see cref="HostProcess.CreateStartInfo"/> gains a default <c>Email__DropFolder</c>, or every Host-starting test
/// in the solution fails at startup the moment AC11's "fails loudly" lands. A case that reads mail passes
/// <see cref="Settings"/>, which is applied last and wins.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class MailDrop : IDisposable
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(30);

    private readonly DirectoryInfo _folder;

    /// <summary>
    /// Creates an empty folder of the case's own.
    /// </summary>
    public MailDrop()
    {
        _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "yello-mail-drop", Guid.NewGuid().ToString("N")));
    }

    /// <summary>
    /// The sender address the Host is configured with (AC11's non-secret <c>Email__Sender</c>).
    /// </summary>
    public static string Sender => "DoNotReply@yello.test";

    /// <summary>
    /// The folder.
    /// </summary>
    public string Folder => _folder.FullName;

    /// <summary>
    /// The configuration that points a Host at this folder, by environment-variable name.
    /// </summary>
    public IReadOnlyDictionary<string, string> Settings => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Email__DropFolder"] = _folder.FullName,
        ["Email__Sender"] = Sender,
    };

    /// <summary>
    /// <paramref name="settings"/> with this folder's settings added (and winning).
    /// </summary>
    /// <param name="settings">A case's other settings.</param>
    /// <returns>The merged settings.</returns>
    public Dictionary<string, string> With(IReadOnlyDictionary<string, string> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var merged = new Dictionary<string, string>(settings, StringComparer.Ordinal);

        foreach (var (key, value) in Settings)
        {
            merged[key] = value;
        }

        return merged;
    }

    /// <summary>
    /// Every email dropped so far.
    /// </summary>
    /// <returns>The emails, in file-name order.</returns>
    public async Task<IReadOnlyList<DroppedEmail>> ReadAllAsync()
    {
        var emails = new List<DroppedEmail>();

        foreach (var file in _folder.EnumerateFiles("*.json").OrderBy(file => file.Name, StringComparer.Ordinal))
        {
            await using var stream = file.OpenRead();
            using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

            emails.Add(DroppedEmail.Read(document.RootElement));
        }

        return emails;
    }

    /// <summary>
    /// Waits until at least <paramref name="count"/> emails have been dropped, signalled by the file system and bounded
    /// by a deadline - never a sleep (<c>TESTING-CONVENTIONS.md</c>).
    /// </summary>
    /// <param name="count">How many.</param>
    /// <returns>Every email dropped by then.</returns>
    /// <exception cref="InvalidOperationException">Fewer arrived within the window.</exception>
    public Task<IReadOnlyList<DroppedEmail>> WaitForAsync(int count) =>
        WaitForAsync(emails => emails.Count >= count, $"{count} email(s)");

    /// <summary>
    /// Waits until an email to <paramref name="recipient"/> has been dropped.
    /// </summary>
    /// <param name="recipient">The address, as submitted.</param>
    /// <returns>That email.</returns>
    public async Task<DroppedEmail> WaitForEmailToAsync(string recipient)
    {
        var emails = await WaitForAsync(
            dropped => dropped.Any(email => string.Equals(email.To, recipient, StringComparison.Ordinal)),
            $"an email to {recipient}").ConfigureAwait(false);

        return emails.Single(email => string.Equals(email.To, recipient, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            _folder.Delete(recursive: true);
        }
        catch (IOException)
        {
            // A Host still holding a file when the case ends leaves the folder for the OS to clean.
        }
        catch (UnauthorizedAccessException)
        {
            // As above.
        }
    }

    private async Task<IReadOnlyList<DroppedEmail>> WaitForAsync(Func<IReadOnlyList<DroppedEmail>, bool> arrived, string what)
    {
        using var deadline = new CancellationTokenSource(Window);
        using var watcher = new FileSystemWatcher(_folder.FullName, "*.json");

        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void Signal(object _, FileSystemEventArgs __) => changed.TrySetResult();

        watcher.Created += Signal;
        watcher.Renamed += (_, _) => changed.TrySetResult();
        watcher.EnableRaisingEvents = true;

        while (true)
        {
            var waiting = changed.Task;
            var emails = await ReadAllAsync().ConfigureAwait(false);

            if (arrived(emails))
            {
                return emails;
            }

            try
            {
                await waiting.WaitAsync(deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException(
                    $"The Host did not drop {what} within {Window.TotalSeconds:F0}s; {emails.Count} email(s) were dropped.");
            }

            changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}

/// <summary>
/// One dropped email, as the ACS Email request body shapes it.
/// </summary>
/// <param name="From">The sender address.</param>
/// <param name="To">The one recipient's address.</param>
/// <param name="Subject">The subject.</param>
/// <param name="PlainText">The plain-text body.</param>
/// <param name="Html">The HTML body, or empty.</param>
public sealed record DroppedEmail(string From, string To, string Subject, string PlainText, string Html)
{
    /// <summary>
    /// Every part of the email a reader sees, as one string - what a disclosure assertion searches.
    /// </summary>
    public string Everything => string.Join('\n', From, To, Subject, PlainText, Html);

    /// <summary>
    /// Reads one dropped file's root element.
    /// </summary>
    /// <param name="root">The element.</param>
    /// <returns>The email.</returns>
    public static DroppedEmail Read(JsonElement root)
    {
        var content = root.GetProperty("content");
        var recipients = root.GetProperty("recipients").GetProperty("to");

        return new DroppedEmail(
            root.GetProperty("senderAddress").GetString() ?? string.Empty,
            recipients.GetArrayLength() == 1
                ? recipients[0].GetProperty("address").GetString() ?? string.Empty
                : throw new InvalidOperationException($"An invitation email names {recipients.GetArrayLength()} recipients, not one."),
            content.GetProperty("subject").GetString() ?? string.Empty,
            content.GetProperty("plainText").GetString() ?? string.Empty,
            content.TryGetProperty("html", out var html) ? html.GetString() ?? string.Empty : string.Empty);
    }
}
