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

    private IsolationWorld(SqlServerEngine engine, RunningHost host, WireDatabase database, MailDrop mail)
    {
        _engine = engine;
        Host = host;
        Database = database;
        Mail = mail;
    }

    /// <summary>
    /// The folder the world's Host drops its mail into. Story 4.3 (ATDD run): an Invitation's token exists in plaintext only
    /// in its email, so a row about the view or the accept reads it here (<see cref="InvitationWire.TokenIn"/>). Rows
    /// before 4.3 read no mail; the Host dropped into <see cref="MailDrop.Unread"/> instead, which nobody read either.
    /// </summary>
    public MailDrop Mail { get; }

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
        var mail = new MailDrop();
        RunningHost host;

        try
        {
            await MigrateAsync(connectionString);

            var forHost = new SqlConnectionStringBuilder(connectionString) { ApplicationName = HostProgramName };

            if (maxPoolSize is { } size)
            {
                forHost.Pooling = true;
                forHost.MaxPoolSize = size;
            }

            host = await RunningHost.StartAsync(forHost.ConnectionString, mail.Settings);
        }
        catch
        {
            mail.Dispose();
            await engine.DropDatabaseAsync(connectionString);
            throw;
        }

        var world = new IsolationWorld(engine, host, new WireDatabase(connectionString), mail);

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
    /// Story 3.4 AC8 (written by its ATDD run): a fresh Account that owns two Spaces - its Personal Space and a second
    /// created through <c>POST /api/v1/spaces</c>, so it carries its default Statuses - with a Token bound to each, and
    /// both populated through the product with distinct randomised names: one Project, a Label, and
    /// <see cref="SeededSpace.FirstSpaceTasks"/> or <see cref="SeededSpace.OtherSpaceTasks"/> Tasks (so their counts
    /// differ), the Label applied to the first Task. Never <see cref="WireDatabase.AddOwnedSpaceAsync"/>, which writes no
    /// Statuses.
    /// </summary>
    /// <param name="label">A label for the display name.</param>
    /// <returns>The two Spaces, each as a view of the one Account.</returns>
    public async Task<(SeededSpace First, SeededSpace Second)> OwnerOfTwoAsync(string label)
    {
        var owner = await NewActorAsync(label);
        var first = await SeedAsync(owner, SeededSpace.FirstSpaceTasks);
        var second = await SeedAsync(await SecondSpaceOfAsync(owner), SeededSpace.OtherSpaceTasks);

        return (first, second);
    }

    /// <summary>
    /// Story 3.4 AC2: a fresh Account (the Owner of its Personal Space, populated) and a second fresh Account's
    /// Personal Space (populated by its own Owner) in which the first holds <paramref name="roleStableCode"/>, with a
    /// Token the first Account issued there. Both views are of the first Account.
    /// </summary>
    /// <param name="roleStableCode">The Role it holds in the other Account's Space.</param>
    /// <returns>Its own Space, and its view of the other.</returns>
    public async Task<(SeededSpace Own, SeededSpace Elsewhere)> OwnerElsewhereAsync(string roleStableCode)
    {
        var owner = await NewActorAsync("Ow");
        var host = await NewActorAsync("Oh");
        var own = await SeedAsync(owner, SeededSpace.FirstSpaceTasks);
        var hosted = await SeedAsync(host, SeededSpace.OtherSpaceTasks);

        await Database.AddMembershipAsync(host.SpaceId, owner.AccountId, roleStableCode);

        var token = await owner.IssueTokenAsync(host.SpaceId, "elsewhere");
        var view = owner.InSpace(host.SpaceId, host.SpaceName, token);

        return (own, hosted with { Actor = view });
    }

    private static async Task<ProductActor> SecondSpaceOfAsync(ProductActor owner)
    {
        var name = $"Second {Guid.NewGuid():N}";

        var created = await owner.SendAsync(
            IsolationSurface.Session,
            HttpMethod.Post,
            Yello.Contracts.Spaces.SpaceRoutes.Directory,
            System.Net.Http.Json.JsonContent.Create(new { name }),
            headers: null);

        Assert.True(created.StatusCode == System.Net.HttpStatusCode.Created, $"Creating a second Space through the product: {created.StatusCode} {created.Body}");

        using var document = System.Text.Json.JsonDocument.Parse(created.Body);
        var spaceId = document.RootElement.GetProperty("id").GetGuid();

        return owner.InSpace(spaceId, name, await owner.IssueTokenAsync(spaceId, "second"));
    }

    private static async Task<SeededSpace> SeedAsync(ProductActor owner, int tasks)
    {
        var projectName = $"Project {Guid.NewGuid():N}";

        owner.HoldsProjectNamed(projectName);

        var projectId = await CreatedIdAsync(owner, Yello.Contracts.Projects.ProjectRoutes.ListIn(owner.SpaceId), new { name = projectName });
        var taskIds = new List<Guid>();

        for (var index = 0; index < tasks; index++)
        {
            var title = $"Task {Guid.NewGuid():N}";

            owner.HoldsTaskTitled(title);
            taskIds.Add(await CreatedIdAsync(owner, Yello.Contracts.Tasks.TaskRoutes.CreateIn(owner.SpaceId, projectId), new { title }));
        }

        var labelName = $"Label {Guid.NewGuid():N}"[..30];

        owner.HoldsLabelNamed(labelName);

        var labelId = await CreatedIdAsync(
            owner,
            Yello.Contracts.Labels.LabelRoutes.LabelsOf(owner.SpaceId),
            new { name = labelName, colour = Yello.Contracts.Labels.LabelColours.Keys[0] });

        var applied = await owner.SendAsync(
            IsolationSurface.Session,
            HttpMethod.Patch,
            Yello.Contracts.Tasks.TaskRoutes.TaskOf(owner.SpaceId, taskIds[0]),
            System.Net.Http.Json.JsonContent.Create(new { labelIds = new[] { labelId } }),
            headers: null);

        Assert.True(applied.StatusCode == System.Net.HttpStatusCode.OK, $"Applying a Label through the product: {applied.StatusCode} {applied.Body}");

        return new SeededSpace(owner, projectId, taskIds, labelId);
    }

    private static async Task<Guid> CreatedIdAsync(ProductActor owner, string path, object body)
    {
        var created = await owner.SendAsync(IsolationSurface.Session, HttpMethod.Post, path, System.Net.Http.Json.JsonContent.Create(body), headers: null);

        Assert.True(created.StatusCode == System.Net.HttpStatusCode.Created, $"Seeding through the product ({path}): {created.StatusCode} {created.Body}");

        using var document = System.Text.Json.JsonDocument.Parse(created.Body);

        return document.RootElement.GetProperty("id").GetGuid();
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
        try
        {
            await Host.DisposeAsync();
        }
        finally
        {
            Mail.Dispose();
            await _engine.DropDatabaseAsync(Database.ConnectionString);
        }
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

/// <summary>
/// One Space seeded through the product by story 3.4's world helpers (AC8): the Account's view of it, its one Project,
/// its Tasks in creation order (all in the Space's first Status), and its one Label, applied to the first Task.
/// </summary>
/// <param name="Actor">The Account, seen from this Space (its Session, and a Token bound here).</param>
/// <param name="ProjectId">The Project.</param>
/// <param name="TaskIds">The Tasks.</param>
/// <param name="LabelId">The Label.</param>
internal sealed record SeededSpace(ProductActor Actor, Guid ProjectId, IReadOnlyList<Guid> TaskIds, Guid LabelId)
{
    /// <summary>
    /// The Tasks seeded in an Account's first (Personal) Space.
    /// </summary>
    public const int FirstSpaceTasks = 2;

    /// <summary>
    /// The Tasks seeded in the other Space - a different number, so a count that crossed shows.
    /// </summary>
    public const int OtherSpaceTasks = 3;

    /// <summary>
    /// The Space.
    /// </summary>
    public Guid SpaceId => Actor.SpaceId;
}
