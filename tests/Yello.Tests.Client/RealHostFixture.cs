using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Xunit;
using Yello.Infrastructure.Persistence;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// The real client in Chromium against a real Host on a real database (story 1.9 AC11): the suite's
/// shared client server and browser, one Host with <c>ClientOrigin</c> set to the client's origin, a
/// database of its own on the suite's one SQL Server, and two Accounts seeded through the real API.
/// </summary>
/// <remarks>
/// <para>
/// <b>Order matters.</b> The client server is already running (an assembly fixture), and the Host's
/// CORS policy names its origin; then the database and the Host; then the Accounts, through the Host's
/// own endpoints.
/// </para>
/// <para>
/// <b>One fake, and only one</b>: the published client reads <c>HostBaseAddress</c> from
/// <c>wwwroot/appsettings.Production.json</c>, which names a host nothing listens on, so each page
/// answers that one file with the real Host's address. Every <c>/api/v1/**</c> request goes to the real
/// Host - its cookie, its preflight, its <c>401</c>.
/// </para>
/// <para>
/// <b>Origins.</b> The client is served on <c>http://localhost:{port}</c> and the Host on
/// <c>https://127.0.0.1:{port}</c> with the development certificate. Chromium keeps the Host's
/// <c>SameSite=None; Secure</c> cookie across those two sites, so the client is not moved to https and
/// the cookie is not relaxed. <c>IgnoreHTTPSErrors</c> is set for the self-signed loopback
/// certificate only.
/// </para>
/// <para>
/// <b>An unconfigured container runtime skips; a missing browser fails</b>, as in the rest of this
/// suite.
/// </para>
/// </remarks>
/// <param name="engine">The process's one SQL Server.</param>
/// <param name="surface">The suite's one client server and browser.</param>
public sealed class RealHostFixture(SqlServerEngine engine, ClientSurfaceFixture surface) : IAsyncLifetime
{
    private RunningHost? _host;
    private string? _connectionString;
    private WireDatabase? _database;
    private ProductActor? _a;
    private ProductActor? _b;

    /// <summary>
    /// The address the client is served on.
    /// </summary>
    public Uri ClientAddress => surface.BaseAddress;

    /// <summary>
    /// The running Host.
    /// </summary>
    internal RunningHost Host => _host ?? throw NotStarted();

    /// <summary>
    /// The class database's raw SQL, for what no endpoint does at scale - story 2.9's 5,000-Task seed
    /// (<see cref="WireDatabase.SeedTasksAsync"/>). Seeding small things stays through the product.
    /// </summary>
    internal WireDatabase Database => _database ?? throw NotStarted();

    /// <summary>
    /// Account A, seeded through the real API.
    /// </summary>
    internal ProductActor A => _a ?? throw NotStarted();

    /// <summary>
    /// Account B, seeded through the real API.
    /// </summary>
    internal ProductActor B => _b ?? throw NotStarted();

    /// <summary>
    /// Skips the calling case when no container runtime is configured.
    /// </summary>
    public static void SkipUnlessAnEngineIsConfigured() =>
        Assert.SkipWhen(SqlServerEngine.UnavailableReason is not null, SqlServerEngine.UnavailableReason ?? string.Empty);

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        if (SqlServerEngine.UnavailableReason is not null)
        {
            return;
        }

        _connectionString = await engine.CreateDatabaseAsync(TestContext.Current.CancellationToken);

        await using (var context = new YelloDbContext(new DbContextOptionsBuilder<YelloDbContext>().UseSqlServer(_connectionString).Options))
        {
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        _host = await RunningHost.StartAsync(
            _connectionString,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ClientOrigin"] = ClientAddress.GetLeftPart(UriPartial.Authority),
            });

        _database = new WireDatabase(_connectionString);
        _a = await ProductActor.RegisterAsync(_host, _database, "Ana");
        _b = await ProductActor.RegisterAsync(_host, _database, "Ben");
    }

    /// <summary>
    /// A fresh Account, seeded through the real API, for a case that ends its Sessions.
    /// </summary>
    /// <returns>The actor.</returns>
    internal Task<ProductActor> NewActorAsync() =>
        ProductActor.RegisterAsync(Host, _database ?? throw NotStarted(), "Cai");

    /// <summary>
    /// A second Space owned by <paramref name="actor"/>, written by SQL (no endpoint creates a Space yet),
    /// so the switcher lists one the client may move to in-app.
    /// </summary>
    /// <param name="actor">The owner.</param>
    /// <returns>The Space's id.</returns>
    internal async Task<Guid> AddSecondSpaceAsync(ProductActor actor)
    {
        var name = actor.DisplayName + " second";
        var id = await (_database ?? throw NotStarted()).AddOwnedSpaceAsync(actor.AccountId, name);

        actor.HoldsSpaceNamed(name);

        return id;
    }

    /// <summary>
    /// A fresh browser context whose client talks to the real Host, signed in as
    /// <paramref name="actor"/> through the real sign-in page.
    /// </summary>
    /// <param name="actor">Who signs in.</param>
    /// <returns>The context and its page.</returns>
    internal async Task<(IBrowserContext Context, IPage Page)> OpenSignedInAsync(ProductActor actor)
    {
        var context = await surface.NewContextAsync(new BrowserNewContextOptions
        {
            Locale = "en-GB",
            IgnoreHTTPSErrors = true,
        });

        try
        {
            var page = await context.NewPageAsync();

            // The one fake: where the Host is.
            await page.RouteAsync("**/appsettings.Production.json", route => route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                ContentType = "application/json",
                Body = JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal) { ["HostBaseAddress"] = Host.Address.GetLeftPart(UriPartial.Authority) }),
            }));

            await page.GotoAsync(new Uri(ClientAddress, "sign-in").ToString());
            await page.Locator("#email-address").FillAsync(actor.EmailAddress, new LocatorFillOptions { Timeout = 30_000 });
            await page.Locator("#password").FillAsync(actor.Password);
            await page.Locator("form button[type=submit]").ClickAsync();

            return (context, page);
        }
        catch
        {
            // The caller only owns the context once it is returned.
            await context.DisposeAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Each teardown runs whatever the one before it did: a failed Host dispose must not leave the
        // database behind.
        try
        {
            if (_host is not null)
            {
                await _host.DisposeAsync();
            }
        }
        finally
        {
            if (_connectionString is not null)
            {
                await engine.DropDatabaseAsync(_connectionString);
            }
        }
    }

    private static InvalidOperationException NotStarted() =>
        new("The real-Host collection did not start; its InitializeAsync failure is the one to read.");
}

/// <summary>
/// The real client against the real Host, for the cases that need one.
/// </summary>
[CollectionDefinition(nameof(RealHostCollection))]
public sealed class RealHostCollection : ICollectionFixture<RealHostFixture>;
