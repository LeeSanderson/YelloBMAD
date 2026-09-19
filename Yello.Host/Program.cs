using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Yello.Application.Accounts.AuthenticateAccount;
using Yello.Application.Accounts.RegisterAccount;
using Yello.Contracts.Accounts;
using Yello.Contracts.Localisation;
using Yello.Host;
using Yello.Host.Endpoints;
using Yello.Host.Sessions;
using Yello.Infrastructure;

// The composition root, AC4's connectivity check, the product's endpoints, and - from story 1.4 -
// the whole of the cross-origin session contract: the cookie's attributes, CORS, and anti-forgery.
//
// ALL THREE ARE CONFIGURED EXACTLY ONCE, HERE. AD-7's Prevents clause bans "per-endpoint cookie
// configuration" in as many words, which is stronger than AR-14's paraphrase and is the binding
// text. Nothing outside SessionCookiePolicy sets a cookie attribute, and nothing outside
// CrossOriginPolicy decides an allowed origin.
//
// There is no /sync WebSocket (Epic 7) - though the cookie below is deliberately configured in a
// way that does not assume HTTP only, because ARCHITECTURE-SPINE.md:323-325 has that socket
// crossing the same origin boundary with the same cookie.
//
// The request pipeline's cross-cutting behaviours - AUTHORISATION, Space resolution, refusal
// recording, idempotency, rate limiting and the NFR-8 bound checks - still do not exist. Stories
// 1.5 and 1.6 own them, and AR-3 makes a slice that re-implements any of them a defect. Note the
// distinction this story turns on: AUTHENTICATION is wired below, and authorisation is not. There
// is no AddAuthorization, no UseAuthorization and no policy anywhere in this file, because AD-1
// makes every authorisation decision a per-request (Account, Space) resolution that nothing here
// can perform.

var builder = WebApplication.CreateBuilder(args);

// EF CORE'S COMMAND LOG IS AN AD-23 DISCLOSURE, AND MUST STAY OFF.
//
// Found at code review on 2026-09-10 by the assertion written in the same pass - it failed on its
// first run against a real Host, which is the only reason anyone knows.
//
// EF Core logs every statement it generates to Microsoft.EntityFrameworkCore.Database.Command at
// Information, and the two registration paths do not generate the same statements. A NEW address
// reaches SaveChangesAsync and logs four `INSERT INTO` lines - Account, Space, Membership,
// StatusDefinition. A KNOWN address is refused by the existence check and rolls back before any
// insert, so it logs none of them. The response is a uniform 204 either way, and the log beside it
// says exactly which registrations were duplicates.
//
// That is the precise failure AD-23 names and RegistrationLog's own remarks argue against: "a
// uniform response undone by a log line is the same leak by a slower route, and logs travel
// further than responses do." It was invisible to the log test until 2026-09-10 because that test
// counted its own message rather than reading what else was there. Parameter VALUES were never
// exposed - EF redacts them without EnableSensitiveDataLogging, and the address assertion confirms
// it - but the statement shapes alone are the whole disclosure.
//
// Filtered unconditionally rather than per environment. AD-23 is not environment-scoped, and a
// development log is still a log; PRD section 6.4's data-protection gate fires on these same rows.
// A developer who wants to watch SQL can raise this locally for as long as they are looking at it.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

// Read once, here, rather than inside the registration below: the same value is needed by the
// startup connectivity check, and a second GetConnectionString call would be a second place the
// resource name is resolved.
var databaseResource = BuildConstants.DatabaseResourceName();
var connectionString = builder.Configuration.GetConnectionString(databaseResource);

builder.Services.AddYelloInfrastructure(connectionString);

// The slices. Registered here rather than from Yello.Application, which would need a DI package
// its ring does not carry and its per-ring package ban would then have to be reasoned about.
builder.Services.AddScoped<RegisterAccountHandler>();
builder.Services.AddScoped<AuthenticateAccountHandler>();

// AUTHENTICATION. A plain cookie ticket, and no Identity builder of any kind - see
// SessionCookiePolicy for the decision, and Yello.Infrastructure/InfrastructureServices.cs for
// what "the whole of Identity that Yello wires" still means. No new package: cookie
// authentication lives in the Microsoft.AspNetCore.App shared framework, which this project
// (Microsoft.NET.Sdk.Web) already has, so Directory.Packages.props and PackageVersionPinTests'
// exact expected set are both untouched.
var sessionSetting = SessionCookiePolicy.ReadLifetimeSetting(
    builder.Configuration[SessionCookiePolicy.LifetimeConfigurationKey]);

var sessionLifetime = sessionSetting.Lifetime;

builder.Services
    .AddAuthentication(SessionCookiePolicy.SchemeName)
    .AddCookie(SessionCookiePolicy.SchemeName, options => SessionCookiePolicy.Configure(options, sessionLifetime));

// CORS, and it is read by more than CORS. .NET 10 registers an anti-request-forgery middleware by
// default whose trust list IS this policy - so what is configured here decides two defences, and a
// wildcard would disarm both. CrossOriginPolicy fails closed when nothing is configured.
var clientOrigin = builder.Configuration[CrossOriginPolicy.OriginConfigurationKey];

builder.Services.AddCors(options => CrossOriginPolicy.Configure(options, clientOrigin));

// ANTI-FORGERY, as a token system rather than only the automatic middleware. The middleware's
// verdict is recorded on IAntiforgeryValidationFeature and read only by a form consumer, so for
// a JSON API it records a verdict nobody reads - see Yello.Contracts/Accounts/Antiforgery.cs.
// The cookie half takes the same cross-origin attributes as the Session cookie, because it has to
// survive the same journey.
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = Antiforgery.HeaderName;
    options.Cookie.Name = "yello_antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None;
});

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

// ORDER MATTERS AND IS NOT ARBITRARY. CORS runs before authentication so that a cross-origin
// preflight - which carries no cookie by definition - is answered rather than being asked to
// authenticate. Authentication runs before the endpoints, because that is what populates
// HttpContext.User for GET /api/v1/sessions/current to read.
app.UseCors(CrossOriginPolicy.PolicyName);

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Yello.Host.Startup");

// Fails closed rather than silently: with no origin configured every browser request is refused,
// and a Host that does that without saying so is a morning lost to a CORS error in a console.
//
// A MALFORMED origin fails closed the same way and used to say nothing, because this test only
// caught a BLANK one - so the single configuration mistake most likely to be made ('https://host/app',
// a trailing space, a missing scheme) produced exactly the silent morning the warning exists to
// prevent. CrossOriginPolicy.Describe names it. Found at code review on 2026-09-19.
if (string.IsNullOrWhiteSpace(clientOrigin))
{
    SessionLog.NoClientOriginConfigured(startupLogger, CrossOriginPolicy.OriginConfigurationKey);
}
else if (CrossOriginPolicy.Describe(clientOrigin) is { } originProblem)
{
    SessionLog.ClientOriginUnusable(startupLogger, CrossOriginPolicy.OriginConfigurationKey, originProblem);
}
else
{
    // A configured origin that parses as a bare origin. Nothing to report: this is the ordinary
    // case, and a line saying so on every boot would be noise that trains a reader to skim past the
    // two branches above, which are the ones that matter.
}

// THE RESOLVED SESSION LIFETIME, ALWAYS, AND THE PROBLEM WITH IT WHEN THERE IS ONE.
//
// ReadLifetime's own remarks said the process "starts and reports the problem rather than refusing
// to boot" while nothing reported anything - a silent fallback, so a mistyped duration was
// indistinguishable from an absent one. Reporting the resolved value unconditionally is the half
// that matters: a 24x lifetime is only obvious when the number is on screen.
if (sessionSetting.Problem is { } lifetimeProblem)
{
    SessionLog.SessionLifetimeUnusable(
        startupLogger, SessionCookiePolicy.LifetimeConfigurationKey, lifetimeProblem);
}

SessionLog.SessionLifetimeResolved(startupLogger, sessionLifetime);

app.UseAuthentication();

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

// AC4'S DUMMY HASH, MATERIALISED BEFORE THE FIRST REQUEST RATHER THAN BY IT.
//
// The hash a non-existent address is verified against is built on first use. Left to a request,
// that request performs a full hash IN ADDITION to its verification - roughly twice what a known
// address costs on the same cold process, one sample per process, on exactly the axis AC4 requires
// to be indistinguishable. The duration suite cannot see it because it warms each arm before
// measuring, which hides the single occurrence. Found at code review on 2026-09-19.
//
// Placed after the connectivity check so a Host that cannot reach its database still fails for that
// reason first, and costs ~273 ms of startup - the trade stated in IdentityPasswordHasher.
app.Services.WarmPasswordHashing();

// THE ROUTE TABLE. Anti-forgery is attached to the GROUP, by HTTP method, rather than named on each
// route - so AR-14's "every state-changing request" is a property of the verb rather than of anyone
// remembering to write RequireAntiforgeryToken() on the next POST somebody adds. Changed at code
// review on 2026-09-19, which found that the per-route opt-in had no gate behind it: a forgotten
// call shipped an unprotected state change with every test in this story green.
//
// AR-14 reaches registration too. It is unauthenticated, so a token there protects against nothing
// an attacker wants - but under a method-shaped rule that is not a decision anybody has to defend,
// which is how Lee settled question 5 at that review. There is no exception list to keep.
var api = app.MapGroup(string.Empty).AddEndpointFilter(AntiforgeryRequirement.ValidateStateChangingAsync);

// POST /api/v1/accounts. 204 on both the new-address and the already-registered paths - see
// RegisterAccountEndpoint for why the handler it calls returns nothing at all.
api.MapRegisterAccount();

// POST /api/v1/sessions. 204 with the Session cookie, or ONE 401 for every failure cause.
api.MapAuthenticate();

// GET /api/v1/sessions/current. 204 or 401 - and no anti-forgery token, because it changes no state
// and the factory above attaches nothing to a safe method.
api.MapCurrentSession();

// GET /api/v1/antiforgery/token. Issues the request token the two POSTs above must carry.
api.MapAntiforgeryToken();

// RunAsync, not Run: this file is already an async entry point (the connectivity check above
// awaits), and the blocking overload would park the entry-point thread for the process
// lifetime while the async machinery it was started on sits idle. CA1849 and S6966 both say so.
await app.RunAsync();
