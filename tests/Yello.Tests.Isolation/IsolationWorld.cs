using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Yello.Infrastructure.Persistence;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// What every isolation case runs against: a real Host on a database of its own on the process's one
/// SQL Server, and two Accounts - A and B - each registered through the product with its own Personal
/// Space, signed in, and holding a Token for that Space.
/// </summary>
/// <remarks>
/// <para>
/// <b>One world per test class</b> (story 1.9 Task 3: a long-lived Host with a log cursor). The cases
/// in a class run one after another on it, so every case reads deltas - the refusal rows it added, the
/// log lines since its cursor - rather than totals, and no case depends on another having run first.
/// A case that needs an Account in a different state (a second owned Space) registers a fresh one
/// with <see cref="NewActorAsync"/> rather than changing A or B.
/// </para>
/// <para>
/// <b>The schema is the migrations', applied to this world's own database</b> through
/// <see cref="YelloDbContext"/>. AR-36 forbids the Host applying them at startup.
/// </para>
/// </remarks>
internal sealed class IsolationWorld : IAsyncDisposable
{
    /// <summary>
    /// The Host's <c>Application Name</c>, so a side connection can count the Host's physical sessions.
    /// </summary>
    public static string HostProgramName => "Yello.Host.Isolation";

    private readonly SqlServerEngine _engine;

    private readonly List<ProductActor> _actors = [];

    private IsolationWorld(SqlServerEngine engine, RunningHost host, WireDatabase database)
    {
        _engine = engine;
        Host = host;
        Database = database;
    }

    /// <summary>
    /// Account A.
    /// </summary>
    public ProductActor A => _actors[0];

    /// <summary>
    /// Account B.
    /// </summary>
    public ProductActor B => _actors[1];

    /// <summary>
    /// The Host.
    /// </summary>
    public RunningHost Host { get; }

    /// <summary>
    /// The world's own database.
    /// </summary>
    public WireDatabase Database { get; }

    /// <summary>
    /// Every secret the world holds: each actor's password, Token plaintexts, Session cookie value and
    /// Space name in both its raw and its JSON-escaped form.
    /// </summary>
    public IReadOnlyList<string> Secrets => [.. _actors.SelectMany(actor => actor.Secrets).Where(secret => secret.Length > 0)];

    /// <summary>
    /// Starts a world: a database of its own, migrated; a Host on it; A and B through the product.
    /// </summary>
    /// <param name="engine">The process's one SQL Server.</param>
    /// <param name="maxPoolSize">When set, the Host's pool is pinned to this many connections.</param>
    /// <returns>The world.</returns>
    public static Task<IsolationWorld> StartAsync(SqlServerEngine engine, int? maxPoolSize = null)
    {
        ArgumentNullException.ThrowIfNull(engine);

        return StartCoreAsync(engine, maxPoolSize);
    }

    private static async Task<IsolationWorld> StartCoreAsync(SqlServerEngine engine, int? maxPoolSize)
    {
        var connectionString = await engine.CreateDatabaseAsync(TestContext.Current.CancellationToken);

        await MigrateAsync(connectionString);

        var forHost = new SqlConnectionStringBuilder(connectionString) { ApplicationName = HostProgramName };

        if (maxPoolSize is { } size)
        {
            forHost.Pooling = true;
            forHost.MaxPoolSize = size;
        }

        var host = await RunningHost.StartAsync(forHost.ConnectionString);
        var world = new IsolationWorld(engine, host, new WireDatabase(connectionString));

        try
        {
            await world.NewActorAsync("Ada");
            await world.NewActorAsync("Bo");
        }
        catch
        {
            await world.DisposeAsync();
            throw;
        }

        return world;
    }

    /// <summary>
    /// Registers a fresh Account through the product, for a case that needs one in a state A and B
    /// must not be left in.
    /// </summary>
    /// <param name="label">A label for its display name.</param>
    /// <returns>The actor.</returns>
    public async Task<ProductActor> NewActorAsync(string label)
    {
        var actor = await ProductActor.RegisterAsync(Host, Database, label);

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

    /// <summary>
    /// The physical sessions the Host holds on this world's database.
    /// </summary>
    /// <returns>The count.</returns>
    public Task<int> HostSessionCountAsync() => Database.SessionCountAsync(HostProgramName);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Host.DisposeAsync();
        await _engine.DropDatabaseAsync(Database.ConnectionString);
    }

    /// <summary>
    /// Runs the migrations into <paramref name="connectionString"/>'s database.
    /// </summary>
    /// <param name="connectionString">A database of the caller's own.</param>
    /// <returns>A task.</returns>
    public static async Task MigrateAsync(string connectionString)
    {
        await using var context = new YelloDbContext(
            new DbContextOptionsBuilder<YelloDbContext>().UseSqlServer(connectionString).Options);

        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }
}

/// <summary>
/// A class's world, shared by its cases. An unconfigured container runtime is recorded and each case
/// skips with it; a configured but dead one fails (story 1.9 Q1).
/// </summary>
/// <param name="engine">The process's one SQL Server.</param>
public sealed class IsolationWorldFixture(SqlServerEngine engine) : IAsyncLifetime
{
    private IsolationWorld? _world;

    /// <summary>
    /// The world, or a skip with the reason there is none.
    /// </summary>
    internal IsolationWorld World
    {
        get
        {
            Assert.SkipWhen(SqlServerEngine.UnavailableReason is not null, SqlServerEngine.UnavailableReason ?? string.Empty);

            return _world ?? throw new InvalidOperationException("The isolation world did not start; its InitializeAsync failure is the one to read.");
        }
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        if (SqlServerEngine.UnavailableReason is null)
        {
            _world = await IsolationWorld.StartAsync(engine);
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

/// <summary>
/// The pooled-connection case's world: the Host's pool pinned to one connection (AC4, AC12).
/// </summary>
/// <param name="engine">The process's one SQL Server.</param>
public sealed class PinnedPoolWorldFixture(SqlServerEngine engine) : IAsyncLifetime
{
    /// <summary>
    /// One connection, so consecutive requests for different Spaces share it.
    /// </summary>
    public static int PinnedPoolSize => 1;

    private IsolationWorld? _world;

    /// <summary>
    /// The world, or a skip with the reason there is none.
    /// </summary>
    internal IsolationWorld World
    {
        get
        {
            Assert.SkipWhen(SqlServerEngine.UnavailableReason is not null, SqlServerEngine.UnavailableReason ?? string.Empty);

            return _world ?? throw new InvalidOperationException("The pinned-pool world did not start; its InitializeAsync failure is the one to read.");
        }
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        if (SqlServerEngine.UnavailableReason is null)
        {
            _world = await IsolationWorld.StartAsync(engine, PinnedPoolSize);
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
