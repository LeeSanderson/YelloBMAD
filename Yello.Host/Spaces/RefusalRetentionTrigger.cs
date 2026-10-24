using System.Data.Common;
using Yello.Application.Spaces;

namespace Yello.Host.Spaces;

/// <summary>
/// Gives the refusal-record purge its chance on each inbound request, off the request path. AD-10 /
/// AR-33; AD-20.
/// </summary>
/// <remarks>
/// <para>
/// <b>Request traffic is the only trigger.</b> There is no timer, no <c>BackgroundService</c> and no
/// <c>IHostedService</c>: a purge that runs only once a request has arrived cannot wake an idle
/// database. <see cref="IRefusalRetention"/> decides whether a purge is due - at most once a day per
/// process - and returns a completed task when it is not, which is every request but one a day.
/// </para>
/// <para>
/// <b>Not awaited by the request.</b> The one request a day that claims the purge does not wait for
/// the delete; a failure is logged and the request is unaffected.
/// </para>
/// </remarks>
internal static class RefusalRetentionTrigger
{
    private static readonly string LoggerCategory = typeof(RefusalRetentionTrigger).FullName!;

    /// <summary>
    /// The middleware: start a purge if one is due, then carry on with the request.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <returns>The rest of the pipeline.</returns>
    internal static Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var services = context.RequestServices;
        var purge = services.GetRequiredService<IRefusalRetention>().PurgeIfDueAsync(CancellationToken.None);

        if (!purge.IsCompleted || purge.IsFaulted)
        {
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory);
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
