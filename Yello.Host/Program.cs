using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Yello.Application.Accounts.RegisterAccount;
using Yello.Contracts.Localisation;
using Yello.Host;
using Yello.Host.Endpoints;
using Yello.Infrastructure;

// The composition root, AC4's connectivity check, and - from story 1.3 - the product's first
// endpoint.
//
// Superseded 2026-08-29 by story 1.3. This previously read "Story 1.1 registers NO endpoints and
// no /sync WebSocket", which described story 1.1's tree and stopped being true when
// POST /api/v1/accounts landed below. Recorded as a correction rather than deleted, because
// stale hand-off prose in these files produced six of story 1.1's review findings and a seventh
// at story 1.2's.
//
// Still true: there is no /sync WebSocket (Epic 7), and the request pipeline's cross-cutting
// behaviours - authorisation, Space resolution, refusal recording, idempotency and the NFR-8
// bound checks - do not exist yet. Stories 1.5 and 1.6 own them, and AR-3 makes a slice that
// re-implements any of them a defect. Registration needs none of them: it is unauthenticated,
// it creates the Space it writes into, and no NFR-8 bound applies to it.

var builder = WebApplication.CreateBuilder(args);

// Read once, here, rather than inside the registration below: the same value is needed by the
// startup connectivity check, and a second GetConnectionString call would be a second place the
// resource name is resolved.
var databaseResource = BuildConstants.DatabaseResourceName();
var connectionString = builder.Configuration.GetConnectionString(databaseResource);

builder.Services.AddYelloInfrastructure(connectionString);

// The slice. Registered here rather than from Yello.Application, which would need a DI package
// its ring does not carry and its per-ring package ban would then have to be reasoned about.
builder.Services.AddScoped<RegisterAccountHandler>();

// The clock, injected rather than read statically, so a test can assert that every row one
// registration writes carries the same instant. The local-time properties on the date types are
// banned APIs at build in any case; this resolves to the UTC one.
builder.Services.AddSingleton(TimeProvider.System);

// The culture a request is served in, from Accept-Language, restricted to the cultures Yello
// actually has resources for. The server needs this for exactly one thing today - composing the
// Personal Space's name - but it is the hook a translation plugs into, and SupportedCultures is
// shared with the client so the two cannot disagree about what a given browser gets.
var cultures = SupportedCultures.All.Select(CultureInfo.GetCultureInfo).ToList();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture(SupportedCultures.Default);
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;
});

var app = builder.Build();

app.UseRequestLocalization();

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

// POST /api/v1/accounts. 204 on both the new-address and the already-registered paths - see
// RegisterAccountEndpoint for why the handler it calls returns nothing at all.
app.MapRegisterAccount();

// RunAsync, not Run: this file is already an async entry point (the connectivity check above
// awaits), and the blocking overload would park the entry-point thread for the process
// lifetime while the async machinery it was started on sits idle. CA1849 and S6966 both say so.
await app.RunAsync();
