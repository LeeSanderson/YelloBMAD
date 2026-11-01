namespace Yello.Infrastructure.Email;

/// <summary>
/// An Azure Communication Services connection string, parsed: <c>endpoint=https://{resource}.communication.azure.com/;accesskey={base64}</c>.
/// Story 4.1, AC11.
/// </summary>
/// <param name="Endpoint">The resource endpoint: scheme and authority only.</param>
/// <param name="AccessKey">The access key, base64.</param>
internal sealed record AcsConnection(Uri Endpoint, string AccessKey)
{
    /// <summary>
    /// Parses a connection string, or answers why it cannot be used - never quoting the key.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="connection">The parsed connection, when usable.</param>
    /// <returns><c>null</c> when usable, otherwise the problem.</returns>
    internal static string? TryParse(string connectionString, out AcsConnection? connection)
    {
        connection = null;
        string? endpoint = null;
        string? accessKey = null;

        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var equals = part.IndexOf('=', StringComparison.Ordinal);

            if (equals <= 0)
            {
                continue;
            }

            var key = part[..equals];
            var value = part[(equals + 1)..];

            if (key.Equals("endpoint", StringComparison.OrdinalIgnoreCase))
            {
                endpoint = value;
            }
            else if (key.Equals("accesskey", StringComparison.OrdinalIgnoreCase))
            {
                accessKey = value;
            }
            else
            {
                // Any other part is ignored.
            }
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return "it names no https endpoint";
        }

        if (string.IsNullOrEmpty(accessKey) || !IsBase64(accessKey))
        {
            return "it carries no base64 access key";
        }

        connection = new AcsConnection(new Uri(uri.GetLeftPart(UriPartial.Authority)), accessKey);

        return null;
    }

    private static bool IsBase64(string value)
    {
        var buffer = new byte[value.Length];

        return Convert.TryFromBase64String(value, buffer, out _);
    }
}
