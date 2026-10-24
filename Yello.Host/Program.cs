using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Yello.Application.Accounts.AuthenticateAccount;
using Yello.Application.Accounts.RegisterAccount;
using Yello.Application.Accounts.SignOut;
using Yello.Application.Spaces.ListSpaces;
using Yello.Application.Statuses.ListStatuses;
using Yello.Contracts.Accounts;
using Yello.Contracts.Localisation;
using Yello.Host;
using Yello.Host.Endpoints;
using Yello.Host.Sessions;
using Yello.Host.Spaces;
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
// THE SPACE BOUNDARY is the request pipeline's, from stories 1.5 and 1.6: ActiveSpaceResolution,
// attached to the route group below, resolves the active Space from the Membership row for every
// route whose pattern carries {spaceId}, scopes that request's unit of work to it in the database,
// checks the endpoint's declared capability against the FR-16 Role matrix, consults NFR-8's
// concurrent-presence bound, and records every 403 and every boundary 404 as an AccessRefusal. The
// two password-hashing endpoints are rate-limited by named policy. AR-3 makes a slice that
// re-implements any of these a defect. Idempotency (AR-25) is not here yet: it is story 2.1's, the
// first endpoint that creates something a client could retry. There is still no AddAuthorization,
// no UseAuthorization and no authorization policy anywhere in this file: AD-1 makes every
// authorisation decision a per-request (Account, Space) resolution, and the Role it reads comes off
// the Membership row on each request, never off a principal a policy could inspect.

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
builder.Services.AddScoped<SignOutHandler>();
builder.Services.AddScoped<ListStatusesHandler>();
builder.Services.AddScoped<ListSpacesHandler>();

// The Account-scoped context (AD-24), established by the pipeline for the two surfaces that name it.
builder.Services.AddAccountScopedResolution();

// How a Space-scoped request establishes its Account and its Space. The Session half only; story
// 1.8 adds the API Token half behind the same interface, so the two surfaces differ in this one
// step and share everything after it (AD-4).
builder.Services.AddSingleton<ISpaceRequestEstablishment, SessionSpaceRequestEstablishment>();

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

// THE RATE LIMIT ON THE TWO PASSWORD-HASHING ENDPOINTS. Both spend a deliberate ~273 ms of CPU on any
// well-formed body, unauthenticated - see CredentialHashingRateLimit for the two limits and why the
// partition is never the submitted address. The per-client permits are configurable so the wire
// tests can run fast, and the resolved value is logged below unconditionally.
var permitsSetting = CredentialHashingRateLimit.ReadPermitsSetting(
    builder.Configuration[CredentialHashingRateLimit.PermitsPerWindowConfigurationKey]);

builder.Services.AddRateLimiter(options =>
    CredentialHashingRateLimit.Configure(options, permitsSetting.PermitsPerWindow));

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

if (permitsSetting.Problem is { } permitsProblem)
{
    RateLimitLog.PermitsUnusable(
        startupLogger, CredentialHashingRateLimit.PermitsPerWindowConfigurationKey, permitsProblem);
}

RateLimitLog.PermitsResolved(startupLogger, permitsSetting.PermitsPerWindow, CredentialHashingRateLimit.Window);

app.UseAuthentication();

// After routing (which WebApplication places first) so the named policy on the two endpoints is
// visible, and after authentication; before any endpoint runs, so a refused request costs no hash.
app.UseRateLimiter();

// THE REFUSAL RECORDS' RETENTION PURGE, triggered by request traffic and at most once a day - never
// by a timer, so an idle database is never woken (AD-10). See RefusalRetentionTrigger.
app.Use(RefusalRetentionTrigger.InvokeAsync);

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
//
// SPACE RESOLUTION IS ATTACHED THE SAME WAY, by the shape of the route: every endpoint whose pattern
// carries {spaceId} is resolved because of what it is. A Space-scoped request with no Session is
// answered 401 FIRST, before anti-forgery - the authentication boundary, not a malformed request
// (story 1.6, decision 11). Resolution itself runs after anti-forgery, so a forged state change is
// refused before any Space is read. The group stamps every endpoint it maps, and the Host refuses to
// start with a {spaceId} route that lacks the stamp - one mapped on `app` would skip all of this.
// ApiRouteGroup holds the order, so a test can hold it too.
var api = app.MapApiGroup();

// POST /api/v1/accounts. 204 on both the new-address and the already-registered paths - see
// RegisterAccountEndpoint for why the handler it calls returns nothing at all. Rate-limited.
api.MapRegisterAccount().RequireRateLimiting(CredentialHashingRateLimit.PolicyName);

// POST /api/v1/sessions. 204 with the Session cookie, or ONE 401 for every failure cause.
// Rate-limited, under the same policy and the same uniform 429.
api.MapAuthenticate().RequireRateLimiting(CredentialHashingRateLimit.PolicyName);

// DELETE /api/v1/sessions. Sign-out: ends the Account's Sessions on the server (SessionsValidAfter)
// and expires the cookie. 204 with a Session or without one; anti-forgery required, as on every
// state-changing request.
api.MapSignOut();

// GET /api/v1/sessions/current. 204 or 401 - and no anti-forgery token, because it changes no state
// and the factory above attaches nothing to a safe method.
api.MapCurrentSession();

// GET /api/v1/antiforgery/token. Issues the request token the two POSTs above must carry.
api.MapAntiforgeryToken();

// GET /api/v1/spaces/{spaceId}/statuses. The first Space-scoped route: 200, 401, or one 404 for a
// Space that does not exist and a Space the caller is not a member of alike. Declares the read
// capability, which every Role holds.
api.MapListStatuses();

// GET /api/v1/spaces/{spaceId}. The active Space's own answer for the context bar - its name and the
// caller's Role, as the pipeline resolved them on this request. Space-scoped like the route above.
api.MapActiveSpace();

// GET /api/v1/spaces. The Space switcher - AD-24's first Account-scoped surface: 200 with the caller's
// Spaces as identity only (an empty list included), or 401 without a Session. Never a refusal
// surface, and never reachable with an API Token (CAP-35; story 1.8).
api.MapListSpaces();

// Every other /api path - a malformed {spaceId} included - answers the same not-available body a
// boundary refusal does, and records nothing. Mapped on `app`, outside the group: no filter has
// anything to say about a path that matches no route.
app.MapUnmatchedApi();

// The route table is complete. Refuse to start with a Space-scoped route the pipeline would not
// protect - see SpaceScopedRouteCheck.
SpaceScopedRouteCheck.EnsureEveryRouteIsResolved(app);

// RunAsync, not Run: this file is already an async entry point (the connectivity check above
// awaits), and the blocking overload would park the entry-point thread for the process
// lifetime while the async machinery it was started on sits idle. CA1849 and S6966 both say so.
await app.RunAsync();
