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
    /// The driver's own connect timeout, kept below <see cref="TimeoutSeconds"/> so the two do
    /// not race to report the same failure.
    /// </summary>
    private const int DriverTimeoutSeconds = 12;

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
        // Two sources rather than one, so the catch can tell WHICH fired. Sampling
        // applicationStopping after the await raced: a real 15-second timeout could be reported
        // as "the Host is shutting down. This is not a connectivity failure", which misdiagnoses
        // the one case that is a problem. The deadline is checked first for the same reason - if
        // both have fired, the honest report is the timeout.
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, applicationStopping);

        try
        {
            // Pooling off, and a connect timeout that matches the deadline above. This connection
            // is opened once and discarded; leaving it pooled would put a connection carrying
            // this startup session's context back into the pool for the application to draw on
            // later, which is precisely the shape story 1.9's pooled-connection isolation case
            // exists to catch.
            // The driver's timeout is deliberately SHORTER than the deadline above. Equal values
            // raced, so an unreachable engine reported either "gave up waiting" (Warning) or
            // "could not open the connection" (Error) depending on which fired first. Letting the
            // driver finish first makes the outcome deterministic and the more informative of the
            // two, and leaves the deadline as the backstop it is meant to be.
            var settings = new SqlConnectionStringBuilder(connectionString)
            {
                Pooling = false,
                ConnectTimeout = DriverTimeoutSeconds,
            };

            await using var connection = new SqlConnection(settings.ConnectionString);
            await connection.OpenAsync(timeout.Token);

            StartupLog.ConnectivityConfirmed(logger, connection.ServerVersion, connection.Database);
        }
        catch (OperationCanceledException)
        {
            // Deadline first: if both fired, a timeout is the honest report. Note that
            // ConnectivityAbandoned is close to unreachable in production - ApplicationStopping
            // cannot fire before app.RunAsync(), and this runs before it - so it is defensive
            // rather than expected, and is exercised by passing a pre-cancelled token.
            if (deadline.IsCancellationRequested)
            {
                StartupLog.ConnectivityTimedOut(logger, TimeoutSeconds);
            }
            else
            {
                StartupLog.ConnectivityAbandoned(logger);
            }
        }
        catch (SqlException exception)
        {
            StartupLog.ConnectivityFailed(logger, resource, exception);
        }
        // S2221 disabled here, deliberately and narrowly. The rule is right in general and this
        // is the case it is wrong about: the method's entire contract, stated in its summary and
        // relied on by Program.cs which has no handler of its own, is that it never throws. An
        // allow-list of exception types IS the defect - the previous list of four omitted
        // everything the Entra authentication modes raise, so an unanticipated type killed the
        // Host before Kestrel bound, which is the outcome this class exists to prevent. A check
        // whose only output is a log line must not be the reason a Host refuses to start.
#pragma warning disable S2221 // "Catch a list of specific exception subtype or use exception filters"
        catch (Exception exception) when (exception is not OutOfMemoryException)
#pragma warning restore S2221
        {
            // Everything else, because this method's one contract is that it never throws - and
            // an allow-list of four exception types was not that. Verified against
            // Microsoft.Data.SqlClient 7.0.2: an unrecognised keyword in the connection string,
            // or a non-boolean value for the integrated security keyword, throws
            // ArgumentException from the builder rather than from Open, and reading ServerVersion
            // on a connection that never opened throws InvalidOperationException. Neither is a
            // SqlException, so both escaped the original handler. The list then still omitted
            // everything the Entra authentication modes raise, and Program.cs has no handler of
            // its own - so an unanticipated type killed the Host before Kestrel bound, which is
            // exactly the outcome this class's summary says is designed out. A check whose only
            // job is to log cannot be the reason a Host refuses to start.
            StartupLog.ConnectionStringUnusable(logger, resource, exception.Message, exception);
        }
    }
}
