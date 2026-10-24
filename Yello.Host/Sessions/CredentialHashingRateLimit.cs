using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Yello.Contracts;

namespace Yello.Host.Sessions;

/// <summary>
/// The rate limit on the two unauthenticated endpoints that spend a deliberate ~273 ms of CPU on any
/// well-formed body - registration and sign-in. AD-18; the ledger's high-severity entry (Lee,
/// 2026-09-10).
/// </summary>
/// <remarks>
/// <para>
/// <b>Two limits, and both are needed.</b> A concurrency limit shared by both endpoints, about one
/// permit per processor with a short queue, so no amount of traffic from anywhere can hold more
/// PBKDF2 work in flight than the machine can do. And a fixed window per client address, so one
/// client cannot take the whole of that budget for itself.
/// </para>
/// <para>
/// <b>The partition is never the submitted email address</b> (AD-23). A limiter keyed on it would
/// answer a known address differently from an unknown one, which is exactly the oracle the uniform
/// <c>204</c> and the uniform <c>401</c> exist to close. The window is keyed on the connection's
/// address and nothing in the body is read.
/// </para>
/// <para>
/// <b>The refusal is uniform too:</b> <c>429</c>, <c>application/problem+json</c>, the
/// <see cref="ProblemTypes.RateLimited"/> type and a <c>Retry-After</c> header, with the same body
/// whatever was submitted. It is not an <c>AccessRefusal</c> - no Space boundary was reached.
/// </para>
/// <para>
/// <b>Attached by endpoint, never inside a slice</b> (AR-3): the two routes carry the named policy,
/// and the handlers know nothing about it. In the shared framework since .NET 7, so no package.
/// </para>
/// </remarks>
internal static class CredentialHashingRateLimit
{
    /// <summary>
    /// The policy the two password-hashing endpoints require.
    /// </summary>
    internal const string PolicyName = "credential-hashing";

    /// <summary>
    /// Where the per-client permits per window are configured - <c>RateLimiting__PermitsPerWindow</c>
    /// as an environment variable.
    /// </summary>
    internal const string PermitsPerWindowConfigurationKey = "RateLimiting:PermitsPerWindow";

    /// <summary>
    /// Ten attempts a minute from one client address, by default: generous for a person mistyping a
    /// password, and a hard ceiling on what one address can make the Host hash.
    /// </summary>
    internal const int DefaultPermitsPerWindow = 10;

    /// <summary>
    /// The window the per-client permits are counted over.
    /// </summary>
    internal static TimeSpan Window { get; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How many requests may wait for each concurrency permit before the rest are refused.
    /// </summary>
    private const int QueuedPerPermit = 2;

    /// <summary>
    /// Reads the configured permits per window.
    /// </summary>
    /// <param name="configured">The configured value, or <c>null</c>/blank when none is set.</param>
    /// <returns>The permits to use, and the problem with the configured value when there is one.</returns>
    internal static PermitsSetting ReadPermitsSetting(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return new PermitsSetting(DefaultPermitsPerWindow, Problem: null);
        }

        return int.TryParse(configured, NumberStyles.None, CultureInfo.InvariantCulture, out var permits) && permits > 0
            ? new PermitsSetting(permits, Problem: null)
            : new PermitsSetting(
                DefaultPermitsPerWindow,
                $"'{configured}' is not a positive whole number. Falling back to the default.");
    }

    /// <summary>
    /// Configures both limits and the uniform refusal.
    /// </summary>
    /// <param name="options">The rate limiter's options.</param>
    /// <param name="permitsPerWindow">The per-client permits per window.</param>
    internal static void Configure(RateLimiterOptions options, int permitsPerWindow)
    {
        ArgumentNullException.ThrowIfNull(options);

        var processors = Environment.ProcessorCount;

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // The shared concurrency budget, applied only to requests for the two policy endpoints.
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            IsCredentialHashing(context)
                ? RateLimitPartition.GetConcurrencyLimiter(PolicyName, _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = processors,
                    QueueLimit = processors * QueuedPerPermit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                })
                : RateLimitPartition.GetNoLimiter(string.Empty));

        options.AddPolicy(PolicyName, context =>
            RateLimitPartition.GetFixedWindowLimiter(PartitionOf(context), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitsPerWindow,
                Window = Window,
                QueueLimit = 0,
            }));

        options.OnRejected = RefuseAsync;
    }

    /// <summary>
    /// The partition a request is counted in: the client's address, and never anything it sent.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <returns>The partition key.</returns>
    internal static string PartitionOf(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

    private static bool IsCredentialHashing(HttpContext context) =>
        string.Equals(
            context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName,
            PolicyName,
            StringComparison.Ordinal);

    private static async ValueTask RefuseAsync(OnRejectedContext rejected, CancellationToken cancellationToken)
    {
        var retryAfter = rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var after)
            ? after
            : TimeSpan.FromSeconds(1);

        rejected.HttpContext.Response.Headers.RetryAfter =
            Math.Max(1L, Convert.ToInt64(Math.Ceiling(retryAfter.TotalSeconds))).ToString(CultureInfo.InvariantCulture);

        await Results.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                type: ProblemTypes.RateLimited,
                title: "Too many requests.")
            .ExecuteAsync(rejected.HttpContext)
            .ConfigureAwait(false);
    }
}

/// <summary>
/// The resolved permits per window, and what was wrong with the configured value if anything.
/// </summary>
/// <param name="PermitsPerWindow">The permits to use.</param>
/// <param name="Problem">The problem with the configured value, or <c>null</c>.</param>
internal sealed record PermitsSetting(int PermitsPerWindow, string? Problem);
