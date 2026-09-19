using Microsoft.AspNetCore.Cors.Infrastructure;

namespace Yello.Host.Sessions;

/// <summary>
/// The cross-origin half of AC5: exactly the configured client origin, with credentials, and
/// never a wildcard or a reflected <c>Origin</c>. AD-7 / AR-14.
/// </summary>
/// <remarks>
/// <para>
/// <b>The allowed origin comes from configuration, not from source</b> (AR-37). That is also what
/// makes "exactly the configured client origin" a testable claim rather than a tautology: a test
/// can start the Host with one origin configured and watch a different one be refused.
/// </para>
/// <para>
/// <b>Never <c>AllowAnyOrigin</c>, and never <c>SetIsOriginAllowed(_ =&gt; true)</c>.</b> The
/// second is the trap rather than the first: <c>AllowAnyOrigin</c> combined with
/// <c>AllowCredentials</c> throws at runtime, and the error message pushes an implementer straight
/// onto the reflected-<c>Origin</c> predicate - which AD-7 bans by name, and which is strictly
/// worse, because it returns the caller's own origin in
/// <c>Access-Control-Allow-Origin</c> alongside <c>Access-Control-Allow-Credentials: true</c> and
/// so allows every origin while naming one.
/// </para>
/// <para>
/// <b>This policy is also .NET 10's CSRF trust list, which is the second reason a wildcard cannot
/// be used here.</b> The automatic anti-request-forgery middleware
/// <c>WebApplication.CreateBuilder</c> registers reads the CORS policy to decide which
/// cross-site <c>Origin</c> values to trust, and deliberately does <b>not</b> honour
/// <c>AllowAnyOrigin</c> as a trust signal - because treating it as trusted would make the
/// middleware a no-op. So a wildcard would not merely breach AC5; it would silently disarm a
/// defence that is on by default.
/// </para>
/// <para>
/// <b>With nothing configured, nothing is allowed.</b> The policy is built with no origins rather
/// than defaulting to something permissive, so a missing setting fails closed - a browser is
/// refused and the Host says so at startup. The alternative, defaulting to the development client
/// address, would put a working cross-origin hole in every deployment that forgot the setting.
/// </para>
/// </remarks>
public static class CrossOriginPolicy
{
    /// <summary>
    /// The configuration key naming the one origin the browser client is served from.
    /// </summary>
    /// <remarks>
    /// Injected by <c>Yello.AppHost</c> from the client project's own endpoint under
    /// <c>aspire run</c>, so the development value cannot drift from the address the client is
    /// actually served on. Story 1.10 owns what production sets it to.
    /// </remarks>
    public static string OriginConfigurationKey => "ClientOrigin";

    /// <summary>
    /// The policy's name.
    /// </summary>
    public static string PolicyName => "yello-client-origin";

    /// <summary>
    /// Builds the policy for a configured origin, or an empty one when none is configured.
    /// </summary>
    /// <param name="options">The CORS options.</param>
    /// <param name="configuredOrigin">The value of <see cref="OriginConfigurationKey"/>.</param>
    public static void Configure(CorsOptions options, string? configuredOrigin)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(PolicyName, policy =>
        {
            if (string.IsNullOrWhiteSpace(configuredOrigin))
            {
                // Fails closed. WithOrigins() with no arguments is an empty allow-list, which is
                // what "no configured origin" should mean.
                policy.WithOrigins();
                return;
            }

            policy
                .WithOrigins(configuredOrigin.Trim().TrimEnd('/'))
                .AllowCredentials()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    }

    /// <summary>
    /// Says why a configured origin could never match a browser's <c>Origin</c> header, or
    /// <c>null</c> when it is usable.
    /// </summary>
    /// <param name="configuredOrigin">The value of <see cref="OriginConfigurationKey"/>.</param>
    /// <returns>A sentence naming the problem, or <c>null</c>.</returns>
    /// <remarks>
    /// <para>
    /// <b>This exists because failing closed silently is indistinguishable from working.</b> The
    /// startup warning only fired when the setting was BLANK, and ASP.NET Core matches
    /// <c>Origins</c> by ordinal equality without validating what it was given - so a value that is
    /// present but not a bare origin refuses every browser request with nothing logged anywhere.
    /// The likely mistakes are all of that shape: a path on the end
    /// (<c>https://localhost:7045/app</c>), a missing scheme (<c>localhost:7045</c>), a trailing
    /// space. Each produces the morning lost to a CORS error in a console that
    /// <c>NoClientOriginConfigured</c> was written to prevent, through the one door it did not
    /// cover. Found at code review on 2026-09-19.
    /// </para>
    /// <para>
    /// A description rather than a correction: it reports and does not rewrite, because guessing
    /// what an operator meant by a malformed origin is how a policy quietly allows something nobody
    /// configured. <see cref="Configure"/> trims whitespace and a trailing slash, which are
    /// unambiguous; anything else is named here and left alone.
    /// </para>
    /// </remarks>
    public static string? Describe(string? configuredOrigin)
    {
        if (string.IsNullOrWhiteSpace(configuredOrigin))
        {
            return null;
        }

        var candidate = configuredOrigin.Trim().TrimEnd('/');

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var origin))
        {
            return $"'{configuredOrigin}' is not an absolute URL. "
                + "It must be a bare origin, such as 'https://localhost:7045'.";
        }

        if (origin.Scheme is not ("http" or "https"))
        {
            return $"'{configuredOrigin}' uses the scheme '{origin.Scheme}'. "
                + "A browser origin is http or https.";
        }

        // PathAndQuery is "/" for a bare origin, which Configure's TrimEnd already removes.
        return origin.PathAndQuery is "/" && string.IsNullOrEmpty(origin.Fragment)
            ? null
            : $"'{configuredOrigin}' carries a path, query or fragment. An Origin header is only "
                + $"scheme, host and port - use '{origin.Scheme}://{origin.Authority}'.";
    }
}
