using Microsoft.EntityFrameworkCore;
using Xunit;
using Yello.Infrastructure.Persistence;
using Yello.Tests.Shared;

namespace Yello.Tests.Revocation;

/// <summary>
/// One Host on a database of its own on the process's one SQL Server, for a class of revocation cases.
/// Each case registers a fresh Account through the product, because every case destroys what it was
/// given.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lee's decisions, 2026-10-24, are the expectations:</b> (1) sign-out ends Sessions server-side
/// through <c>SessionsValidAfter</c>, so a replayed pre-sign-out cookie grants nothing; (2) a Token on
/// sign-out is no credential.
/// </para>
/// <para>
/// <b>Raw SQL only where no endpoint exists yet</b> - Membership removal, Space and Account deletion, a
/// Role change - through <see cref="WireDatabase"/>, under the session keys the schema requires. Their
/// endpoints arrive in 3.3, 4.5 and 5.4, and each owner adds the endpoint-driven row to this list.
/// </para>
/// </remarks>
internal sealed class RevocationWorld : IAsyncDisposable
{
    private readonly SqlServerEngine _engine;

    private readonly List<ProductActor> _actors = [];

    private RevocationWorld(SqlServerEngine engine, RunningHost host, WireDatabase database)
    {
        _engine = engine;
        Host = host;
        Database = database;
    }

    /// <summary>
    /// The Host.
    /// </summary>
    public RunningHost Host { get; }

    /// <summary>
    /// The world's own database.
    /// </summary>
    public WireDatabase Database { get; }

    /// <summary>
    /// Every secret any actor in this world has held.
    /// </summary>
    public IReadOnlyList<string> Secrets => [.. _actors.SelectMany(actor => actor.Secrets).Where(secret => secret.Length > 0)];

    /// <summary>
    /// Starts a world: a database of its own, migrated, and a Host on it.
    /// </summary>
    /// <param name="engine">The process's one SQL Server.</param>
    /// <returns>The world.</returns>
    public static async Task<RevocationWorld> StartAsync(SqlServerEngine engine)
    {
        var connectionString = await engine.CreateDatabaseAsync(TestContext.Current.CancellationToken);

        await using (var context = new YelloDbContext(new DbContextOptionsBuilder<YelloDbContext>().UseSqlServer(connectionString).Options))
        {
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        return new RevocationWorld(engine, await RunningHost.StartAsync(connectionString), new WireDatabase(connectionString));
    }

    /// <summary>
    /// A fresh Account: registered, signed in, holding one Token for its Personal Space.
    /// </summary>
    /// <returns>The actor.</returns>
    public async Task<ProductActor> NewActorAsync()
    {
        var actor = await ProductActor.RegisterAsync(Host, Database, "Rev");

        _actors.Add(actor);

        return actor;
    }

    /// <summary>
    /// The same request with no credential at all - the anchor <c>401</c>.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <param name="path">The route.</param>
    /// <returns>The answer.</returns>
    public Task<WireResponse> SendWithoutCredentialAsync(HttpMethod method, string path) =>
        ProductActor.SendWithoutCredentialAsync(Host.Client, method, path);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Host.DisposeAsync();
        await _engine.DropDatabaseAsync(Database.ConnectionString);
    }
}

/// <summary>
/// A class's revocation world. An unconfigured container runtime is recorded and each case skips with
/// it; a configured but dead one fails (story 1.9 Q1).
/// </summary>
/// <param name="engine">The process's one SQL Server.</param>
public sealed class RevocationWorldFixture(SqlServerEngine engine) : IAsyncLifetime
{
    private RevocationWorld? _world;

    /// <summary>
    /// The world, or a skip with the reason there is none.
    /// </summary>
    internal RevocationWorld World
    {
        get
        {
            Assert.SkipWhen(SqlServerEngine.UnavailableReason is not null, SqlServerEngine.UnavailableReason ?? string.Empty);

            return _world ?? throw new InvalidOperationException("The revocation world did not start; its InitializeAsync failure is the one to read.");
        }
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        if (SqlServerEngine.UnavailableReason is null)
        {
            _world = await RevocationWorld.StartAsync(engine);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_world is not null)
        {
            await _world.DisposeAsync();
        }
    }
}
