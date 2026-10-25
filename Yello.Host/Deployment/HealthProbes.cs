namespace Yello.Host.Deployment;

/// <summary>
/// The liveness and readiness probes the platform polls. Story 1.10 AC6 (AD-10 / AR-33); open
/// question 15 (a); Finding F6.
/// </summary>
/// <remarks>
/// <para>
/// <b>From process state, with no database round trip.</b> If the process can answer, it is alive and
/// ready to take a request; whether the database is awake is not the probe's question, and asking it
/// would wake a paused database every few seconds and spend the free allowance doing so.
/// </para>
/// <para>
/// <b>Plain endpoints, and no <c>AddHealthChecks()</c>.</b> That call registers a hosted service - the
/// timer-shaped thing AD-10 forbids - even with no publisher configured. A gate fails the build on it.
/// </para>
/// <para>
/// <b>Outside <c>/api</c></b>: they are not product routes, so they take no authentication, no
/// anti-forgery, no rate limit and no Space resolution, and the isolation gap gate's route sweep does
/// not see them. Their bodies say nothing about the system.
/// </para>
/// </remarks>
internal static class HealthProbes
{
    /// <summary>
    /// Maps <c>GET /health/live</c> and <c>GET /health/ready</c>.
    /// </summary>
    /// <param name="app">The application.</param>
    internal static void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/health/live", static () => Results.Text("ok"));
        app.MapGet("/health/ready", static () => Results.Text("ok"));
    }
}
