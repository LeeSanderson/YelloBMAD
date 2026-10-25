using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Yello.Contracts;
using Yello.Infrastructure.Persistence;

namespace Yello.Host.Deployment;

/// <summary>
/// A database that is paused, resuming or unreachable is answered with one uniform <c>503</c>, never an
/// unhandled <c>500</c>, and admits nothing. Story 1.10 AC14 (and AC12); open question 11 (a);
/// <c>deferred-work.md:336</c>. Only availability is mapped: any other database error stays a
/// <c>500</c> (Lee's decision at code review, 2026-10-25; see <see cref="DatabaseUnavailability"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>One mapping for the whole pipeline</b> (AR-3), rather than a catch in each endpoint: the Session
/// revocation read, the API Token lookup, Space resolution and every slice reach the database through
/// the same pipeline, and a failure in any of them is the same fact about the database. The answer is
/// <c>503 application/problem+json</c> with <see cref="ProblemTypes.ServiceUnavailable"/> and a
/// <c>Retry-After</c>, byte-identical whichever route was asked.
/// </para>
/// <para>
/// <b>Not a sign-out.</b> Treating a failed revocation read as "no Session" would sign a person out of
/// a working Session because the database was resuming. A <c>503</c> tells the client to try again.
/// </para>
/// <para>
/// <b>The key ring is checked before a cookie is read.</b> The Session and anti-forgery cookies are
/// decrypted with keys kept in the database (<see cref="PersistedKeyRing"/>), and the framework's cookie
/// reader turns any decryption failure into "no cookie" - which would answer a paused database with
/// <c>401</c> and send the client to sign in. So until this process has loaded the ring once, a request
/// that carries one of Yello's cookies loads it here first, where a failure surfaces as a database
/// failure (see <see cref="KeyRingAvailability"/>). A request carrying only somebody else's cookies
/// has nothing to decrypt and does not load the ring.
/// </para>
/// <para>
/// <b>No retry on failure</b> (Finding F7): EF Core's retrying strategy refuses the explicit
/// transaction Space resolution holds. A resuming Azure SQL database is waited for by the connection
/// string's own <c>Connect Timeout</c> instead, which the infrastructure sets.
/// </para>
/// </remarks>
internal static class DatabaseAvailability
{
    /// <summary>
    /// What a client is told to wait, in seconds, before trying again. A paused serverless database
    /// resumes in about a minute.
    /// </summary>
    internal const int RetryAfterSeconds = 30;

    private static readonly string LoggerCategory = typeof(DatabaseAvailability).FullName!;

    /// <summary>
    /// The middleware: run the rest of the pipeline, answering a database failure with the uniform
    /// <c>503</c>.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <returns>A task that completes when the response is written.</returns>
    internal static Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        return InvokeCoreAsync(context, next);
    }

    /// <summary>
    /// The prefix every cookie the Host issues carries: the Session (<c>yello_session</c>, chunked or
    /// not) and the anti-forgery cookie (<c>yello_antiforgery</c>).
    /// </summary>
    internal const string CookiePrefix = "yello_";

    /// <summary>
    /// Whether an exception, or one it wraps, is the database being unavailable.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns><c>true</c> for an availability failure anywhere in the chain.</returns>
    internal static bool IsDatabaseFailure(Exception exception) => DatabaseUnavailability.IsUnavailable(exception);

    private static bool CarriesYelloCookie(HttpRequest request) =>
        request.Cookies.Keys.Any(name => name.StartsWith(CookiePrefix, StringComparison.Ordinal));

    private static async Task InvokeCoreAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            if (CarriesYelloCookie(context.Request))
            {
                context.RequestServices.GetRequiredService<KeyRingAvailability>().EnsureLoaded();
            }

            await next(context).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception) && !context.Response.HasStarted)
        {
            DeploymentLog.DatabaseUnavailable(
                context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory),
                exception);

            await RefuseAsync(context).ConfigureAwait(false);
        }
    }

    private static Task RefuseAsync(HttpContext context)
    {
        // Nothing set before the failure survives into the refusal - a Session or anti-forgery cookie
        // included - so the 503 is the same whichever route was asked. CORS writes its headers when the
        // response starts, so clearing here does not remove them.
        context.Response.Clear();
        context.Response.Headers.RetryAfter = RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                type: ProblemTypes.ServiceUnavailable,
                title: "The service is briefly unavailable.")
            .ExecuteAsync(context);
    }
}

/// <summary>
/// Whether this process has loaded the Data Protection key ring from the database yet.
/// </summary>
/// <remarks>
/// <para>
/// <b>Loaded once, then left to the framework.</b> Protecting forces the framework to load the ring -
/// or, for an empty one, to create a key and store it. A database that cannot be read makes it throw,
/// and the flag stays unset so the next request tries again. Once loaded, the framework keeps the ring
/// in memory and keeps the old one if a later refresh fails, so no fresh key is ever minted in memory
/// in place of the stored ones.
/// </para>
/// <para>
/// <b>A singleton holding one flag, and not a cache</b> of anything a request reads.
/// </para>
/// </remarks>
/// <param name="protection">The Host's Data Protection, whose key ring is in the database.</param>
internal sealed class KeyRingAvailability(IDataProtectionProvider protection)
{
    private static readonly byte[] Probe = [0x59];

    private readonly IDataProtector _protector = protection.CreateProtector("Yello.Host.KeyRingAvailability");

    private int _loaded;

    /// <summary>
    /// Loads the key ring if this process has not yet; throws if the database cannot be read.
    /// </summary>
    internal void EnsureLoaded()
    {
        if (Volatile.Read(ref _loaded) == 1)
        {
            return;
        }

        _ = _protector.Protect(Probe);

        Volatile.Write(ref _loaded, 1);
    }
}
