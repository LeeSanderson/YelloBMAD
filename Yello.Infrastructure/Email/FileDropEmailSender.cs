using System.Globalization;
using Yello.Application.Outbox;

namespace Yello.Infrastructure.Email;

/// <summary>
/// Writes each email to a folder instead of sending it, so invitations can be exercised by hand and read by the wire
/// suites. Story 4.1, AC11 and the ATDD phase's Q13 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>Chosen whenever <c>Email:DropFolder</c> is set and <c>ConnectionStrings:acs</c> is not, in any environment</b>
/// (Q13 (a)): every wire suite starts the Host as a separate Production process, so no test can swap in a fake, and this
/// sender pointed at a folder the test owns is the recording sender. Development points it at <c>artifacts/mail-drop</c>,
/// which git ignores. <b>A deployed environment never sets it</b>: an Architecture gate fails if <c>infra/</c>, any
/// <c>appsettings.Production*.json</c> or the AppHost names <c>DropFolder</c>.
/// </para>
/// <para>
/// <b>One whole file per message</b>, written under a temporary name and then moved, so a reader never sees half a
/// message. The content is exactly the ACS request body (<see cref="AcsEmailMessage"/>).
/// </para>
/// </remarks>
/// <param name="folder">The folder to drop into; created when missing.</param>
/// <param name="senderAddress">The configured sender.</param>
/// <param name="time">Names each file by the instant it was dropped, so a reader can take them in order.</param>
internal sealed class FileDropEmailSender(string folder, string senderAddress, TimeProvider time) : IEmailSender
{
    /// <inheritdoc />
    public Task SendAsync(OutboxEmail email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);

        return DropAsync(email, cancellationToken);
    }

    private async Task DropAsync(OutboxEmail email, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(folder);

            var name = string.Create(CultureInfo.InvariantCulture, $"{time.GetUtcNow():yyyyMMddHHmmssfffffff}-{Guid.NewGuid():N}");
            var temporary = Path.Combine(folder, name + ".tmp");

            await File.WriteAllBytesAsync(temporary, AcsEmailMessage.Serialise(senderAddress, email), cancellationToken).ConfigureAwait(false);

            File.Move(temporary, Path.Combine(folder, name + ".json"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new EmailNotSentException("The email could not be written to the drop folder.", exception);
        }
    }
}
