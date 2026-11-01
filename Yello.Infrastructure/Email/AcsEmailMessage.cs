using System.Text.Json;
using Yello.Application.Outbox;

namespace Yello.Infrastructure.Email;

/// <summary>
/// The Azure Communication Services Email "send" request body, as both senders write it: the ACS adapter posts it, and
/// the file drop writes exactly the same JSON, so what a test reads is what ACS would have been sent. Story 4.1, AC11.
/// </summary>
/// <remarks>
/// The shape is <c>EmailMessage</c> of the ACS Email REST API, version <c>2023-03-31</c>
/// (https://learn.microsoft.com/rest/api/communication/dataplane/email/send): <c>senderAddress</c>,
/// <c>recipients.to[].address</c>, <c>content.subject</c> and <c>content.plainText</c>.
/// </remarks>
internal static class AcsEmailMessage
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The JSON body for one email.
    /// </summary>
    /// <param name="senderAddress">The configured sender.</param>
    /// <param name="email">The email.</param>
    /// <returns>The UTF-8 JSON.</returns>
    internal static byte[] Serialise(string senderAddress, OutboxEmail email) =>
        JsonSerializer.SerializeToUtf8Bytes(
            new Body(senderAddress, new Recipients([new Recipient(email.Recipient)]), new Content(email.Subject, email.Body)),
            Options);

    private sealed record Body(string SenderAddress, Recipients Recipients, Content Content);

    private sealed record Recipients(IReadOnlyList<Recipient> To);

    private sealed record Recipient(string Address);

    private sealed record Content(string Subject, string PlainText);
}
