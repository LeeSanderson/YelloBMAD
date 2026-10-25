using System.Data.Common;
using Yello.Application.Spaces;

namespace Yello.Host.Spaces;

/// <summary>
/// Gives the refusal-record purge its chance on a request that has already reached the database, off
/// the request path. AD-10 / AR-33; AD-20; story 1.10 AC14.
/// </summary>
/// <remarks>
/// <para>
/// <b>Request traffic is the only trigger.</b> There is no timer, no <c>BackgroundService</c> and no
/// <c>IHostedService</c>: a purge that runs only once a request has arrived cannot wake an idle
/// database. <see cref="IRefusalRetention"/> decides whether a purge is due - at most once a day per
/// process - and returns a completed task when it is not, which is every request but one a day.
/// </para>
/// <para>
/// <b>Only after an authenticated request</b> (story 1.10, question 13). Authenticating a Session reads
/// its Account's revocation instant, and authenticating an API Token looks the Token up, so such a
/// request has already woken the database. An anonymous request - a scanner asking for an
/// anti-forgery token, an unknown <c>/api</c> path, a <c>401</c> - must not start a delete. (Such a
/// request may still read the Data Protection key ring once per process, which lives in the database;
/// that is one read, never a write and never repeated.) With scale-to-zero every cold start is a new
/// process, so a trigger on every request would purge on the first request after each one.
/// </para>
/// <para>
/// <b>Not awaited by the request.</b> The one request a day that claims the purge does not wait for
/// the delete; it logs the claim, a failure is logged, and the request is unaffected.
/// </para>
/// </remarks>
internal static class RefusalRetentionTrigger
{
    private static readonly string LoggerCategory = typeof(RefusalRetentionTrigger).FullName!;

    /// <summary>
    /// The middleware: start a purge if one is due and the request is authenticated, then carry on
    /// with the request. Placed after authentication.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <returns>The rest of the pipeline.</returns>
    internal static Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (context.User.Identity?.IsAuthenticated != true)
        {
            return next(context);
        }

        var services = context.RequestServices;
        var purge = services.GetRequiredService<IRefusalRetention>().PurgeIfDueAsync(CancellationToken.None);

        if (!purge.IsCompleted || purge.IsFaulted)
        {
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory);
            DeploymentLog.RefusalPurgeClaimed(logger);
            _ = ObserveAsync(purge, logger);
        }

        return next(context);
    }

    private static async Task ObserveAsync(Task purge, ILogger logger)
    {
        try
        {
            await purge.ConfigureAwait(false);
        }
        catch (DbException exception)
        {
            // Off the request path, nothing above this frame would observe the failure.
            SpaceLog.RefusalPurgeFailed(logger, exception);
        }
        catch (InvalidOperationException exception)
        {
            // A Host started with no database configured: the same, and no louder.
            SpaceLog.RefusalPurgeFailed(logger, exception);
        }
    }
}
