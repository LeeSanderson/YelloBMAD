using Microsoft.Data.SqlClient;
using Yello.Host;

// The composition root, and AC4's connectivity check. Nothing else.
//
// Story 1.1 registers NO endpoints and no /sync WebSocket. The template's "Hello World!"
// MapGet was removed: Task 2's scope boundary is explicit that this story creates no
// endpoints, and an endpoint nobody asked for is something a later story then has to reason
// about deleting.
//
// Endpoints arrive with the stories that own them. The request pipeline's behaviours -
// authorisation, Space resolution, refusal recording, idempotency and the NFR-8 bound
// checks - are owned by stories 1.5 and 1.6; AR-3 makes a slice that re-implements any of
// them a defect.

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

await CheckDatabaseConnectivityAsync(app);

// RunAsync, not Run: this file is already an async entry point (the connectivity check above
// awaits), and the blocking overload would park the entry-point thread for the process
// lifetime while the async machinery it was started on sits idle. CA1849 and S6966 both say so.
await app.RunAsync();

// AC4: "a working connection from Host to container".
//
// Shape matters here, and three shapes were available. This is the one that is NOT a health
// check and NOT a periodic task:
//
//   * AR-33 requires liveness and readiness probes to answer from process state with no
//     database round trip, so this cannot hang off a probe. The liveness and readiness
//     endpoints themselves are story 1.10's.
//   * AR-33 also forbids any component touching the database on an unconditional timer, so
//     it cannot be a background service or a periodic check.
//   * AR-36 forbids running migrations at startup, so this opens a connection and does
//     nothing else - no schema, no EnsureCreated, no migrate.
//
// It therefore runs exactly once, at startup, in Development only, and logs the outcome. A
// failure is logged rather than thrown: the check exists to tell a developer whether Aspire
// wired the container, and a Host that refuses to start would report that badly.
static async Task CheckDatabaseConnectivityAsync(WebApplication app)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Yello.Host.Startup");
    var resource = BuildConstants.DatabaseResourceName;

    if (!app.Environment.IsDevelopment())
    {
        // Said out loud rather than left implicit. Without this branch "the check passed",
        // "the check failed" and "the check never ran" are indistinguishable outside
        // Development - and the third is the one a reader is most likely to mistake for the
        // first.
        StartupLog.ConnectivityCheckSkipped(logger, app.Environment.EnvironmentName);
        return;
    }

    var connectionString = app.Configuration.GetConnectionString(resource);

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        StartupLog.ConnectionStringMissing(logger, resource);
        return;
    }

    // A deadline, and a token that Ctrl+C reaches. Without both, a reachable-but-unready
    // endpoint delays Kestrel binding for the driver's full retry budget on every Development
    // start, and the wait is not interruptible.
    const int timeoutSeconds = 15;

    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(app.Lifetime.ApplicationStopping);
    timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

    try
    {
        // Pooling off, and a connect timeout that matches the deadline above. This connection
        // is opened once and discarded; leaving it pooled would put a connection carrying this
        // startup session's context back into the pool for the application to draw on later,
        // which is precisely the shape story 1.9's pooled-connection isolation case exists to
        // catch.
        var settings = new SqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
            ConnectTimeout = timeoutSeconds,
        };

        await using var connection = new SqlConnection(settings.ConnectionString);
        await connection.OpenAsync(timeout.Token);

        StartupLog.ConnectivityConfirmed(logger, connection.ServerVersion, connection.Database);
    }
    catch (OperationCanceledException)
    {
        StartupLog.ConnectivityTimedOut(logger, timeoutSeconds);
    }
    catch (SqlException exception)
    {
        StartupLog.ConnectivityFailed(logger, resource, exception);
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException or NotSupportedException)
    {
        // Not a SqlException, and previously not caught at all - so it killed the process at
        // startup, which is exactly the outcome the comment above says was designed out.
        // Verified against Microsoft.Data.SqlClient 7.0.2: an unrecognised keyword in the
        // connection string, or a non-boolean value for the integrated security keyword,
        // throws ArgumentException from the builder rather than from Open, and reading
        // ServerVersion on a connection that never opened throws InvalidOperationException.
        // Neither is a SqlException, so both escaped the only handler there was.
        StartupLog.ConnectionStringUnusable(logger, resource, exception.Message, exception);
    }
}
