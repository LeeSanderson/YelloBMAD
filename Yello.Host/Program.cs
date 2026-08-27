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

// AC4's check lives in StartupConnectivityCheck rather than here so that a test can reach it.
// Two review passes found that nothing asserted this code at all: deleting the call, inverting
// the environment guard inside it or dropping this await was caught by nothing, and AC4 was
// evidenced only by a manual `aspire run` transcript that then went stale when both entry points
// were rewritten. tests/Yello.Tests.Slices/StartupConnectivityCheckTests.cs is the evidence now.
await StartupConnectivityCheck.RunAsync(
    app.Environment,
    app.Configuration,
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Yello.Host.Startup"),
    app.Lifetime.ApplicationStopping);

// RunAsync, not Run: this file is already an async entry point (the connectivity check above
// awaits), and the blocking overload would park the entry-point thread for the process
// lifetime while the async machinery it was started on sits idle. CA1849 and S6966 both say so.
await app.RunAsync();
