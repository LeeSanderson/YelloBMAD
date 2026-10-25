namespace Yello.Host.Deployment;

/// <summary>
/// The composition root's story 1.10 additions, in one place: forwarded headers, the persisted key
/// ring and HSTS. Story 1.10 AC11, AC12; NFR-6's transit clause.
/// </summary>
/// <remarks>
/// Kept out of <c>Program.cs</c> so the composition root stays a readable list of steps, and so each
/// conditional lives beside the reason for it.
/// </remarks>
internal static class ProductionShape
{
    /// <summary>
    /// Registers forwarded headers (when configured) and the persisted key ring.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The resolved forwarded-headers setting, for the pipeline and the startup log.</returns>
    internal static ForwardedHeadersSetting AddProductionShape(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var forwarded = ForwardedHeadersPolicy.Read(builder.Configuration[ForwardedHeadersPolicy.EnabledConfigurationKey]);

        if (forwarded.Enabled)
        {
            builder.Services.Configure<ForwardedHeadersOptions>(ForwardedHeadersPolicy.Configure);
        }

        builder.Services.AddPersistedKeyRing();

        return forwarded;
    }

    /// <summary>
    /// Puts forwarded headers first in the pipeline (when configured), then HSTS outside Development.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <param name="forwarded">The resolved forwarded-headers setting.</param>
    /// <remarks>
    /// Outside Development the platform terminates TLS (Container Apps ingress, Static Web Apps), and
    /// HSTS tells a browser to keep using it. HSTS is not sent to a loopback host.
    /// </remarks>
    internal static void UseProductionShape(WebApplication app, ForwardedHeadersSetting forwarded)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(forwarded);

        if (forwarded.Enabled)
        {
            app.UseForwardedHeaders();
        }

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }
    }

    /// <summary>
    /// Logs the resolved settings at startup, unconditionally, like the rate limit and the Session
    /// lifetime.
    /// </summary>
    /// <param name="logger">The startup logger.</param>
    /// <param name="forwarded">The resolved forwarded-headers setting.</param>
    internal static void Report(ILogger logger, ForwardedHeadersSetting forwarded)
    {
        ArgumentNullException.ThrowIfNull(forwarded);

        if (forwarded.Problem is { } problem)
        {
            DeploymentLog.ForwardedHeadersUnusable(logger, ForwardedHeadersPolicy.EnabledConfigurationKey, problem);
        }

        DeploymentLog.ForwardedHeadersResolved(
            logger,
            forwarded.Enabled ? "on" : "off",
            forwarded.Enabled ? "the single right-most forwarded hop" : "the connection itself");

        if (forwarded.Enabled)
        {
            DeploymentLog.MissingAddressSharesOnePartition(logger);
        }

        DeploymentLog.KeyRingPersisted(logger, PersistedKeyRing.ApplicationName);
    }
}
