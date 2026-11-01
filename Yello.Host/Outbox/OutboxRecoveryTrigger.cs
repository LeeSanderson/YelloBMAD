using Yello.Application.Outbox;

namespace Yello.Host.Outbox;

/// <summary>
/// Gives the outbox's recovery sweep its chance on a request that has already reached the database, off the request
/// path. AD-10 / AR-33, A-9; story 4.1, AC6. The <c>RefusalRetentionTrigger</c> shape.
/// </summary>
/// <remarks>
/// <para>
/// <b>Request traffic is the trigger</b>, beside the one sweep at process start. There is no timer, no
/// <c>BackgroundService</c> and no <c>IHostedService</c>. <see cref="IOutboxRecovery.SweepIfDue"/> claims at most one
/// sweep a minute per process and returns at once otherwise.
/// </para>
/// <para>
/// <b>Only after an authenticated request</b>, which has already read the database (the Session's revocation instant,
/// or the Token's row) - so an anonymous scanner never starts a database write. <b>Not awaited</b>: the sweep runs on a
/// scope of its own and logs its own failure.
/// </para>
/// </remarks>
internal static class OutboxRecoveryTrigger
{
    /// <summary>
    /// The middleware: start a sweep if one is due and the request is authenticated, then carry on with the request.
    /// Placed after authentication.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <returns>The rest of the pipeline.</returns>
    internal static Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (context.User.Identity?.IsAuthenticated == true)
        {
            context.RequestServices.GetRequiredService<IOutboxRecovery>().SweepIfDue();
        }

        return next(context);
    }
}
