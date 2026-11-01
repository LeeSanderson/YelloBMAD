using System.Net;
using System.Net.Http.Headers;
using Yello.Application.Outbox;

namespace Yello.Infrastructure.Email;

/// <summary>
/// Sends an email through the Azure Communication Services Email REST API, over <see cref="HttpClient"/> with HMAC
/// signing - no SDK package (story 4.1, open question 4 (a), Lee). AC11.
/// </summary>
/// <remarks>
/// <para>
/// <b>The call</b> is <c>POST {endpoint}/emails:send?api-version=2023-03-31</c>, answered <c>202 Accepted</c> with an
/// <c>Operation-Location</c> (https://learn.microsoft.com/rest/api/communication/dataplane/email/send). The version is
/// pinned in <see cref="ApiVersion"/>; <c>2023-03-31</c> is the GA version. Accepted is the end of this adapter's
/// interest: delivery status is not polled.
/// </para>
/// <para>
/// <b>The key</b> comes from the connection string <c>endpoint=https://...;accesskey=...</c>, which reaches the container
/// as the Key Vault secret <c>yello-acs-connection</c> (<c>ConnectionStrings__acs</c>). It is never logged, and neither
/// is anything the email holds.
/// </para>
/// <para>
/// <b>One long-lived client</b> with a bounded pooled-connection lifetime, so DNS changes are seen. A singleton.
/// </para>
/// </remarks>
internal sealed class AcsEmailSender : IEmailSender, IDisposable
{
    /// <summary>
    /// The ACS Email REST API version, pinned (GA).
    /// </summary>
    internal const string ApiVersion = "2023-03-31";

    private readonly HttpClient _client;

    private readonly AcsConnection _connection;

    private readonly string _senderAddress;

    private readonly TimeProvider _time;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="connection">The parsed connection string.</param>
    /// <param name="senderAddress">The configured sender: the managed domain's <c>DoNotReply@...</c>.</param>
    /// <param name="time">The clock the signature is dated by.</param>
    internal AcsEmailSender(AcsConnection connection, string senderAddress, TimeProvider time)
    {
        _connection = connection;
        _senderAddress = senderAddress;
        _time = time;
        _client = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
    }

    /// <inheritdoc />
    public Task SendAsync(OutboxEmail email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);

        return PostAsync(email, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();

    private async Task PostAsync(OutboxEmail email, CancellationToken cancellationToken)
    {
        var uri = new UriBuilder(_connection.Endpoint) { Path = "emails:send", Query = "api-version=" + ApiVersion }.Uri;
        var body = AcsEmailMessage.Serialise(_senderAddress, email);
        var signed = AcsRequestSigner.Sign("POST", uri, body, _connection.AccessKey, _time.GetUtcNow());

        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        using var content = new ByteArrayContent(body);

        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Content = content;
        request.Headers.Add("x-ms-date", signed.Date);
        request.Headers.Add("x-ms-content-sha256", signed.ContentHash);
        request.Headers.TryAddWithoutValidation("Authorization", signed.Authorization);

        try
        {
            using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.Accepted)
            {
                throw new EmailNotSentException($"Azure Communication Services answered {response.StatusCode:D}.");
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new EmailNotSentException("Azure Communication Services could not be reached.", exception);
        }
    }
}
