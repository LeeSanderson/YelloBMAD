using Microsoft.AspNetCore.HttpOverrides;

namespace Yello.Host.Deployment;

/// <summary>
/// Whether the Host trusts the hop the platform's ingress appends to <c>X-Forwarded-For</c> and
/// <c>X-Forwarded-Proto</c>, and how. Story 1.10 AC11; open question 10 (a); Finding F3.
/// </summary>
/// <remarks>
/// <para>
/// <b>Off unless configured.</b> Only the infrastructure as code sets
/// <c>ForwardedHeaders__Enabled=true</c>. A local Host has no proxy in front of it, and trusting a
/// forwarded header there would let any client choose its own address and scheme.
/// </para>
/// <para>
/// <b>Exactly one hop, the right-most.</b> Container Apps ingress terminates TLS and appends the
/// client's address and the original scheme. It has no fixed address, and the container is reachable
/// only through it, so the known-proxy lists are cleared and <c>ForwardLimit = 1</c> takes the last
/// entry - the one the platform wrote. A value a client sent lands to its left and is ignored, so a
/// client that rotates it cannot escape the sign-in rate limit's window.
/// </para>
/// <para>
/// <b>Not only about the rate limit.</b> TLS ends at ingress, so the request reaches Kestrel as
/// <c>http</c>. With <c>CookieSecurePolicy.Always</c> the anti-forgery system refuses to issue a token
/// over a request it believes is not secure, so without the forwarded scheme the deployed product
/// cannot sign anyone in.
/// </para>
/// </remarks>
internal static class ForwardedHeadersPolicy
{
    /// <summary>
    /// Where the switch is configured - <c>ForwardedHeaders__Enabled</c> as an environment variable.
    /// </summary>
    internal const string EnabledConfigurationKey = "ForwardedHeaders:Enabled";

    /// <summary>
    /// Reads the configured switch.
    /// </summary>
    /// <param name="configured">The configured value, or <c>null</c>/blank when none is set.</param>
    /// <returns>Whether forwarding is on, and the problem with the configured value when there is one.</returns>
    internal static ForwardedHeadersSetting Read(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return new ForwardedHeadersSetting(Enabled: false, Problem: null);
        }

        return bool.TryParse(configured.Trim(), out var enabled)
            ? new ForwardedHeadersSetting(enabled, Problem: null)
            : new ForwardedHeadersSetting(
                Enabled: false,
                $"'{configured}' is not true or false. Forwarded headers stay off.");
    }

    /// <summary>
    /// Trusts the single right-most hop for the client address and the scheme.
    /// </summary>
    /// <param name="options">The middleware's options.</param>
    internal static void Configure(ForwardedHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;

        // The defaults trust loopback only. The platform's proxy has no fixed address, so nothing would
        // be forwarded at all while these lists held anything.
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    }
}

/// <summary>
/// The resolved forwarded-headers switch, and what was wrong with the configured value if anything.
/// </summary>
/// <param name="Enabled">Whether forwarded headers are read.</param>
/// <param name="Problem">The problem with the configured value, or <c>null</c>.</param>
internal sealed record ForwardedHeadersSetting(bool Enabled, string? Problem);
