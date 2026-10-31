namespace Yello.Host;

/// <summary>
/// Every <c>/api</c> response carries <c>Cache-Control: no-store</c>, set once in the pipeline rather than per endpoint.
/// Story 3.2, AC18, open question 2 (a); UX-DR31, AD-7.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why.</b> Nothing Space-scoped may reach browser storage (UX-DR31), so that browser session-restore cannot repopulate
/// a Space after access has ended and one Account's content cannot survive sign-out on a shared machine. The browser's
/// HTTP cache is the same exposure by another door: a Space-scoped <c>GET</c> with no <c>Cache-Control</c> may be kept on
/// disk after sign-out. The client relies on no HTTP caching, so <c>no-store</c> changes nothing it does.
/// </para>
/// <para>
/// <b>Every <c>/api</c> answer the application writes, not only Space-scoped ones</b>, because a rule with exceptions is
/// one an endpoint can fall outside of. The wire case (<c>ActiveSpaceEndpointTests.CacheControl</c>) holds the
/// switcher's <c>200</c>, a Space-scoped <c>200</c>, the boundary's <c>404</c>, an anonymous <c>401</c> and an unmatched
/// path's <c>404</c>. It is set when the response starts, so a middleware later in the pipeline that clears the response
/// (<c>DatabaseAvailability</c>'s <c>503</c>) keeps it; that path has no wire case. A response written by middleware
/// registered before this one (<c>ProductionShape</c>), or Kestrel's own <c>500</c> for an unhandled exception, is not
/// claimed: neither carries Space content. The per-endpoint <c>no-store</c> lines on idempotent replays and on
/// Token creation stay: they state their own reason where the secret is written, and they agree with this.
/// </para>
/// </remarks>
internal static class ApiNoStore
{
    private static readonly PathString Api = new("/api");

    /// <summary>
    /// The middleware: for an <c>/api</c> request, mark the response <c>no-store</c> when it starts.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <returns>A task that completes when the response is written.</returns>
    internal static Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (context.Request.Path.StartsWithSegments(Api, StringComparison.OrdinalIgnoreCase))
        {
            var response = context.Response;

            response.OnStarting(() =>
            {
                // A value that already forbids storing (the cookie handler's "no-cache,no-store" on a dead-cookie 401)
                // is left as it is.
                if (!response.Headers.CacheControl.ToString().Contains("no-store", StringComparison.OrdinalIgnoreCase))
                {
                    response.Headers.CacheControl = "no-store";
                }

                return Task.CompletedTask;
            });
        }

        return next(context);
    }
}
