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
if (app.Environment.IsDevelopment())
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Yello.Host.Startup");
    var connectionString = app.Configuration.GetConnectionString("yello");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        StartupLog.ConnectionStringMissing(logger);
    }
    else
    {
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            StartupLog.ConnectivityConfirmed(logger, connection.ServerVersion, connection.Database);
        }
        catch (SqlException exception)
        {
            StartupLog.ConnectivityFailed(logger, exception);
        }
    }
}

// RunAsync, not Run: this file is already an async entry point (the connectivity check above
// awaits), and the blocking overload would park the entry-point thread for the process
// lifetime while the async machinery it was started on sits idle. CA1849 and S6966 both say so.
await app.RunAsync();
