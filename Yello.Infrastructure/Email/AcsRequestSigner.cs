using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Yello.Infrastructure.Email;

/// <summary>
/// Signs a request to Azure Communication Services with its access key: the HMAC-SHA256 scheme. Story 4.1, AC11,
/// open question 4 (a) - no SDK package.
/// </summary>
/// <remarks>
/// <para>
/// Verified against Microsoft's "Sign an HTTP request using HMAC" tutorial
/// (https://learn.microsoft.com/azure/communication-services/tutorials/hmac-header-tutorial, read 2026-11-01):
/// </para>
/// <list type="bullet">
/// <item><c>x-ms-date</c>: the UTC instant in RFC 1123 (<c>"r"</c>);</item>
/// <item><c>x-ms-content-sha256</c>: base64 of the SHA-256 of the body;</item>
/// <item>the string to sign: <c>"{VERB}\n{path and query}\n{x-ms-date};{host};{x-ms-content-sha256}"</c>, where the
/// host is the request URI's authority;</item>
/// <item>the signature: base64 of HMAC-SHA256 over the string to sign's UTF-8 bytes, keyed by the base64-decoded access
/// key;</item>
/// <item><c>Authorization: HMAC-SHA256 SignedHeaders=x-ms-date;host;x-ms-content-sha256&amp;Signature={signature}</c>.</item>
/// </list>
/// </remarks>
internal static class AcsRequestSigner
{
    /// <summary>
    /// The headers a signed request carries.
    /// </summary>
    /// <param name="Date">The <c>x-ms-date</c> value.</param>
    /// <param name="ContentHash">The <c>x-ms-content-sha256</c> value.</param>
    /// <param name="Authorization">The whole <c>Authorization</c> value, scheme included.</param>
    internal sealed record SignedHeaders(string Date, string ContentHash, string Authorization);

    /// <summary>
    /// Signs one request.
    /// </summary>
    /// <param name="method">The HTTP method, such as <c>POST</c>.</param>
    /// <param name="requestUri">The absolute request URI.</param>
    /// <param name="body">The exact body bytes sent.</param>
    /// <param name="accessKey">The resource's access key, base64, as the connection string carries it.</param>
    /// <param name="now">The signing instant.</param>
    /// <returns>The headers to send.</returns>
    internal static SignedHeaders Sign(string method, Uri requestUri, byte[] body, string accessKey, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(requestUri);

        var date = now.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture);
        var contentHash = Convert.ToBase64String(SHA256.HashData(body));
        var stringToSign = $"{method}\n{requestUri.PathAndQuery}\n{date};{requestUri.Authority};{contentHash}";
        var signature = Convert.ToBase64String(
            HMACSHA256.HashData(Convert.FromBase64String(accessKey), Encoding.UTF8.GetBytes(stringToSign)));

        return new SignedHeaders(
            date,
            contentHash,
            $"HMAC-SHA256 SignedHeaders=x-ms-date;host;x-ms-content-sha256&Signature={signature}");
    }
}
