using Microsoft.Data.SqlClient;

namespace Yello.Host;

/// <summary>
/// AC4: "a working connection from Host to container". Opens the Aspire-injected connection
/// exactly once at startup, in Development only, and logs the outcome.
/// </summary>
/// <remarks>
/// <para>
/// <b>Shape matters here, and three shapes were available.</b> This is the one that is NOT a
/// health check and NOT a periodic task:
/// </para>
/// <list type="bullet">
/// <item>AR-33 requires liveness and readiness probes to answer from process state with no
/// database round trip, so this cannot hang off a probe. The probe endpoints themselves are
/// story 1.10's.</item>
/// <item>AR-33 also forbids any component touching the database on an unconditional timer, so
/// it cannot be a background service or a periodic check.</item>
/// <item>AR-36 forbids running migrations at startup, so this opens a connection and does
/// nothing else - no schema, no <c>EnsureCreated</c>, no migrate.</item>
/// </list>
/// <para>
/// A failure is logged rather than thrown: the check exists to tell a developer whether Aspire
/// wired the container, and a Host that refuses to start would report that badly.
/// </para>
/// <para>
/// <b>Why this is a class rather than a local function in <c>Program.cs</c>.</b> Two review
/// passes found the same thing about AC4 - that nothing among the architecture suite's
/// assertions touches it, so deleting the check, inverting its <c>IsDevelopment()</c> guard or
/// dropping the <c>await</c> was caught by nothing, and AC4 was evidenced only by a manual
/// <c>aspire run</c> transcribed into the story record. A transcript also goes stale silently:
/// both entry points were rewritten while the transcript stayed put. Lee's call was an
/// integration test, and a static local function inside a top-level program cannot be reached by
/// one. The parameters are the four things the check actually depends on, so a test can supply a
/// real container's connection string and a non-Development environment independently - which is
/// what makes the guard itself testable rather than merely the happy path.
/// </para>
/// </remarks>
public static class StartupConnectivityCheck
{
    /// <summary>
    /// How long to wait for the engine before giving up and letting Kestrel bind anyway.
    /// </summary>
    private const int TimeoutSeconds = 15;

    /// <summary>
    /// Runs the check. Never throws: every outcome is a log line.
    /// </summary>
    /// <param name="environment">Decides whether the check runs at all.</param>
    /// <param name="configuration">Where the Aspire-injected connection string is read from.</param>
    /// <param name="logger">The check's only output.</param>
    /// <param name="applicationStopping">
    /// The host's shutdown token. See the note in <see cref="OpenOnceAndLogAsync"/> about what
    /// linking to it does and does not buy.
    /// </param>
    public static async Task RunAsync(
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken applicationStopping)
    {
        // No argument null-guards. Nullable reference types are enabled solution-wide, so the
        // compiler polices the call sites - and a guard here would contradict this method's one
        // contract, which is that it never throws: every outcome is a log line.
        if (!environment.IsDevelopment())
        {
            // Said out loud rather than left implicit. Without this branch "the check passed",
            // "the check failed" and "the check never ran" are indistinguishable outside
            // Development - and the third is the one a reader is most likely to mistake for the
            // first. The level matters as much as the branch: at Debug this line was filtered
            // out by the shipped configuration, which made the branch a no-op.
            StartupLog.ConnectivityCheckSkipped(logger, environment.EnvironmentName);
            return;
        }

        string resource;

        // Guarded, and below the environment check. This reads assembly metadata and throws when
        // it is absent; reading it eagerly killed the Host before any handler existed, and did so
        // even in Production and Staging, which never use the value. Its own catch rather than
        // the one below, because "the build did not stamp the value" and "the connection string
        // is unusable" are both InvalidOperationException and are not the same problem.
        try
        {
            resource = BuildConstants.DatabaseResourceName();
        }
        catch (InvalidOperationException exception)
        {
            StartupLog.BuildMetadataUnreadable(logger, exception.Message, exception);
            return;
        }

        var connectionString = configuration.GetConnectionString(resource);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            StartupLog.ConnectionStringMissing(logger, resource);
            return;
        }

        await OpenOnceAndLogAsync(logger, resource, connectionString, applicationStopping);
    }

    /// <summary>
    /// The connection attempt itself, split out so that deciding <i>whether</i> to check and
    /// performing the check are separately readable.
    /// </summary>
    private static async Task OpenOnceAndLogAsync(
        ILogger logger,
        string resource,
        string connectionString,
        CancellationToken applicationStopping)
    {
        // A deadline. The link to ApplicationStopping is worth keeping but does NOT make this
        // interruptible by Ctrl+C, which an earlier version of this comment claimed:
        // ConsoleLifetime registers its SIGINT handler inside IHost.StartAsync - that is, inside
        // app.RunAsync(), which has not been called when this runs - so Ctrl+C during this window
        // terminates the process outright. The deadline is what bounds the wait; the link is what
        // stops this outliving a shutdown begun by anything else, and the handler below tells the
        // two apart rather than reporting a shutdown as "gave up waiting for SQL Server".
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(applicationStopping);
        timeout.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

        try
        {
            // Pooling off, and a connect timeout that matches the deadline above. This connection
            // is opened once and discarded; leaving it pooled would put a connection carrying
            // this startup session's context back into the pool for the application to draw on
            // later, which is precisely the shape story 1.9's pooled-connection isolation case
            // exists to catch.
            var settings = new SqlConnectionStringBuilder(connectionString)
            {
                Pooling = false,
                ConnectTimeout = TimeoutSeconds,
            };

            await using var connection = new SqlConnection(settings.ConnectionString);
            await connection.OpenAsync(timeout.Token);

            StartupLog.ConnectivityConfirmed(logger, connection.ServerVersion, connection.Database);
        }
        catch (OperationCanceledException)
        {
            // Which cancellation was it? Reporting a clean shutdown as "gave up waiting for SQL
            // Server after 15s" misdiagnoses the one case that is not a problem at all.
            if (applicationStopping.IsCancellationRequested)
            {
                StartupLog.ConnectivityAbandoned(logger);
            }
            else
            {
                StartupLog.ConnectivityTimedOut(logger, TimeoutSeconds);
            }
        }
        catch (SqlException exception)
        {
            StartupLog.ConnectivityFailed(logger, resource, exception);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException or NotSupportedException)
        {
            // Not a SqlException, and once not caught at all - so it killed the process at
            // startup, which is exactly the outcome designed out above. Verified against
            // Microsoft.Data.SqlClient 7.0.2: an unrecognised keyword in the connection string,
            // or a non-boolean value for the integrated security keyword, throws
            // ArgumentException from the builder rather than from Open, and reading ServerVersion
            // on a connection that never opened throws InvalidOperationException. Neither is a
            // SqlException, so both escaped the only handler there was.
            StartupLog.ConnectionStringUnusable(logger, resource, exception.Message, exception);
        }
    }
}
