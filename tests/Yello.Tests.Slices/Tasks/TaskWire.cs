using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Accounts;
using Yello.Contracts.ApiTokens;
using Yello.Contracts.Projects;
using Yello.Contracts.Spaces;
using Yello.Domain.Accounts;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// The wire and SQL helpers story 2.2's two slice classes share (<c>TaskEndpointTests</c>,
/// <c>BoardEndpointTests</c>): register through the product, create Projects and Tasks over HTTP on
/// either surface, and read or seed <c>dbo.[Task]</c> under a Space's own key. Written by the story 2.2
/// ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Seed constraints checked</b> against <c>20260828164716_InitialSchema.cs</c> (<c>StatusDefinition</c>:
/// <c>Id</c>, <c>SpaceId</c>, <c>Name</c>, <c>Position</c>, <c>CreatedAt</c>; three seeded per Personal Space at
/// registration), <c>20261024143930_AccountScopedMembership.cs</c> (<c>Membership</c>, under the Space
/// policy), <c>20261025094743_Projects.cs</c> (<c>AK_Project_SpaceId_Id</c>, the key 2.2's composite foreign key
/// targets), and story 2.2 Task 2 for the table that does not exist yet: <c>dbo.[Task]</c> with <c>Id</c>
/// (<c>ValueGeneratedNever</c>, so supplied), <c>SpaceId</c>, <c>ProjectId</c>, <c>StatusId</c>, <c>Title</c>,
/// <c>PositionKey</c> (Q2 (a), <c>varchar</c> under <c>Latin1_General_100_BIN2</c>) and <c>CreatedAt</c>.
/// </para>
/// <para>
/// <see cref="SeedTasksAsync"/> satisfies every constraint Task 2 declares: the composite FK
/// <c>(SpaceId, ProjectId)</c> because the Project is a real one the product created in that Space; the
/// composite FK <c>(SpaceId, StatusId)</c> because the Status is read from that Space's own
/// <c>StatusDefinition</c> rows under its key; the <c>BLOCK AFTER INSERT</c> predicate because it runs under
/// the same Space's session key; and the unique <c>(SpaceId, ProjectId, StatusId, PositionKey)</c> index
/// because each seeded key is a distinct zero-padded number with a prefix of the caller's choosing. The
/// keys are digits only, which every base-62 alphabet orders first, so a key the product appends "after the
/// last" sorts after them. <b>If the migration lands another non-nullable column without a default, or
/// Q2 is decided (b), correct this one statement.</b>
/// </para>
/// </remarks>
internal static class TaskWire
{
    public const string Password = "a-password-nobody-else-uses-1!";

    public const string InsufficientRoleKind = "InsufficientRole";

    public const string CrossSpaceKind = "CrossSpace";

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static readonly Dictionary<string, string> Configured = new(StringComparer.Ordinal)
    {
        ["ClientOrigin"] = "https://client.yello.test",
    };

    public static readonly Dictionary<string, string> NoHeaders = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers and signs in through the product, re-fetches the anti-forgery token (one minted while
    /// anonymous is refused once a Session exists), and reads the Personal Space and the Account id.
    /// </summary>
    public static async Task<Actor> RegisterAsync(HttpClient client, RegistrationDatabase database)
    {
        var session = new WireSession(client);
        var address = $"task-{Guid.NewGuid():N}@yello.test";

        await session.FetchAntiforgeryTokenAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await session.RegisterAsync(new RegisterAccountRequest("Tia-" + Guid.NewGuid().ToString("N")[..8], address, Password))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await session.SignInAsync(address, Password)).StatusCode);
        await session.FetchAntiforgeryTokenAsync();

        var spaces = await session.GetAsync(SpaceRoutes.Directory);

        using var document = JsonDocument.Parse(spaces.Body);
        var spaceId = document.RootElement.EnumerateArray().Single().GetProperty("id").GetGuid();

        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Id FROM dbo.[Account] WHERE NormalizedEmailAddress = @address";
        command.Parameters.Add(new SqlParameter("@address", EmailAddressNormalisation.Normalise(address)));

        var accountId = Assert.IsType<Guid>(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));

        return new Actor(session, accountId, spaceId);
    }

    public static async Task<string> IssueTokenAsync(Actor actor, Guid spaceId)
    {
        var issued = await actor.Session.PostAsync(ApiTokenRoutes.IssueIn(spaceId), JsonContent.Create(new IssueApiTokenRequest("task script"), options: JsonOptions));

        Assert.True(issued.StatusCode == HttpStatusCode.Created, $"{issued.StatusCode} {issued.Body}");

        using var document = JsonDocument.Parse(issued.Body);

        return document.RootElement.GetProperty("token").GetString() ?? string.Empty;
    }

    /// <summary>
    /// A non-Owner Membership for an Account the product registered, written under the Space's own key (no
    /// endpoint adds a member until Epic 3), once per Space (<c>AK_Membership_SpaceId_AccountId</c>), never a
    /// second Owner (<c>UX_Membership_SpaceId_Owner</c>). Roles are stored by name.
    /// </summary>
    public static async Task AddMemberAsync(RegistrationDatabase database, Guid spaceId, Guid accountId, string role)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText =
            "INSERT dbo.[Membership] (Id, SpaceId, AccountId, Role, CreatedAt) VALUES (@id, @space, @account, @role, SYSDATETIMEOFFSET())";
        command.Parameters.Add(new SqlParameter("@id", Guid.NewGuid()));
        command.Parameters.Add(new SqlParameter("@space", spaceId));
        command.Parameters.Add(new SqlParameter("@account", accountId));
        command.Parameters.Add(new SqlParameter("@role", SqlDbType.NVarChar, 32) { Value = role });

        Assert.Equal(1, await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Creates a Project through the product (story 2.1's route) and answers its id.
    /// </summary>
    public static async Task<Guid> CreateProjectAsync(Actor actor, string name, Guid? spaceId = null)
    {
        var created = await actor.Session.SendWithHeadersAsync(
            HttpMethod.Post,
            ProjectRoutes.ListIn(spaceId ?? actor.SpaceId),
            JsonContent.Create(new { name }, options: JsonOptions),
            NoHeaders);

        return IdOf(created);
    }

    public static JsonContent TitleBody(string title) => JsonContent.Create(new { title }, options: JsonOptions);

    public static Dictionary<string, string> KeyHeader(string? key) =>
        key is null ? NoHeaders : new Dictionary<string, string>(StringComparer.Ordinal) { [Idempotency.HeaderName] = key };

    public static Task<WireResponse> CreateTaskAsync(Actor actor, Guid spaceId, Guid projectId, string title, string? key = null) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Post, TaskRouteScaffold.CreateIn(spaceId, projectId), TitleBody(title), KeyHeader(key));

    public static Task<WireResponse> CreateTaskWithTokenAsync(Actor actor, string token, Guid spaceId, Guid projectId, string title, string? key = null) =>
        actor.Session.SendWithTokenAndHeadersAsync(HttpMethod.Post, TaskRouteScaffold.CreateIn(spaceId, projectId), token, TitleBody(title), KeyHeader(key));

    public static Task<WireResponse> CreateTaskOnAsync(IsolationSurface surface, Actor actor, string token, Guid projectId, string title, string? key = null) =>
        surface == IsolationSurface.ApiToken
            ? CreateTaskWithTokenAsync(actor, token, actor.SpaceId, projectId, title, key)
            : CreateTaskAsync(actor, actor.SpaceId, projectId, title, key);

    public static Task<WireResponse> BoardAsync(Actor actor, Guid spaceId, Guid projectId) =>
        actor.Session.GetAsync(TaskRouteScaffold.BoardOf(spaceId, projectId));

    public static Guid IdOf(WireResponse created)
    {
        Assert.True(created.StatusCode == HttpStatusCode.Created, $"{created.StatusCode} {created.Body}");

        using var document = JsonDocument.Parse(created.Body);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    public static ProblemResponse? Problem(WireResponse response) =>
        JsonSerializer.Deserialize<ProblemResponse>(response.Body, JsonOptions);

    /// <summary>
    /// The Space's Statuses in effective order - <c>Position</c>, then <c>Id</c> (Task 2's tiebreaker) -
    /// read under its own key.
    /// </summary>
    public static async Task<IReadOnlyList<StatusRow>> StatusesAsync(RegistrationDatabase database, Guid spaceId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Id, Name, Position FROM dbo.[StatusDefinition] ORDER BY Position, Id";

        var rows = new List<StatusRow>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add(new StatusRow(reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2)));
        }

        return rows;
    }

    /// <summary>
    /// <paramref name="count"/> Tasks in one Project and one Status, written by one set-based statement under
    /// the Space's own key - the bound cases' way to reach 4,999 without 4,999 requests. See the class remarks
    /// for every constraint this satisfies.
    /// </summary>
    /// <param name="database">The class's database.</param>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">A Project the product created in it.</param>
    /// <param name="statusId">One of that Space's Statuses.</param>
    /// <param name="count">How many.</param>
    /// <param name="keyPrefix">A digit prefix that keeps this call's position keys apart from another call's.</param>
    public static async Task SeedTasksAsync(RegistrationDatabase database, Guid spaceId, Guid projectId, Guid statusId, int count, string keyPrefix = "0")
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            WITH Numbers AS (
                SELECT TOP (@count) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS N
                FROM sys.all_objects a CROSS JOIN sys.all_objects b)
            INSERT dbo.[Task] (Id, SpaceId, ProjectId, StatusId, Title, PositionKey, CreatedAt)
            SELECT NEWID(), @space, @project, @status, CONCAT(N'Seeded ', N), CONCAT(@prefix, RIGHT(CONCAT('00000', N), 5)), SYSDATETIMEOFFSET()
            FROM Numbers
            """;
        command.Parameters.Add(new SqlParameter("@count", count));
        command.Parameters.Add(new SqlParameter("@space", spaceId));
        command.Parameters.Add(new SqlParameter("@project", projectId));
        command.Parameters.Add(new SqlParameter("@status", statusId));
        command.Parameters.Add(new SqlParameter("@prefix", SqlDbType.VarChar, 8) { Value = keyPrefix });

        Assert.Equal(count, await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Every Task in the Space, read under its own key - the database's view, ordered by Status position,
    /// then position key, then id. A left join, so a Task whose Status the Space does not define is still
    /// read (AD-17's invariant case must be able to see one).
    /// </summary>
    public static async Task<IReadOnlyList<TaskRow>> TaskRowsAsync(RegistrationDatabase database, Guid spaceId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT t.Id, t.SpaceId, t.ProjectId, t.StatusId, t.Title, t.PositionKey, t.CreatedAt
            FROM dbo.[Task] t LEFT JOIN dbo.[StatusDefinition] s ON s.Id = t.StatusId
            ORDER BY s.Position, s.Id, t.PositionKey, t.Id
            """;

        var rows = new List<TaskRow>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add(new TaskRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetGuid(3),
                reader.GetString(4),
                reader.GetString(5),
                await reader.GetFieldValueAsync<DateTimeOffset>(6, TestContext.Current.CancellationToken)));
        }

        return rows;
    }

    /// <summary>
    /// The Space's Task ids in SQL Server's <c>uniqueidentifier</c> order, read under its key - the order the
    /// sequential generator serves, which .NET's <see cref="Guid"/> comparison does not reproduce.
    /// </summary>
    public static async Task<IReadOnlyList<Guid>> TaskIdsInSqlOrderAsync(RegistrationDatabase database, Guid spaceId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Id FROM dbo.[Task] ORDER BY Id";

        var ids = new List<Guid>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    /// <summary>
    /// How many Tasks one Project holds, by SQL under the Space's key.
    /// </summary>
    public static async Task<int> TaskCountAsync(RegistrationDatabase database, Guid spaceId, Guid projectId) =>
        (await TaskRowsAsync(database, spaceId)).Count(row => row.ProjectId == projectId);

    /// <summary>
    /// The kind of every refusal recorded for (Account, Space). <c>AccessRefusal</c> carries no policy.
    /// </summary>
    public static async Task<IReadOnlyList<string>> RefusalKindsAsync(RegistrationDatabase database, Guid accountId, Guid spaceId)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Kind FROM dbo.[AccessRefusal] WHERE AccountId = @account AND TargetSpaceId = @space ORDER BY OccurredAt, Id";
        command.Parameters.Add(new SqlParameter("@account", accountId));
        command.Parameters.Add(new SqlParameter("@space", spaceId));

        var kinds = new List<string>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            kinds.Add(reader.GetString(0));
        }

        return kinds;
    }

    /// <summary>
    /// The first column of a catalogue query, as text, on a connection with no Space context.
    /// </summary>
    public static async Task<IReadOnlyList<string>> CatalogueAsync(RegistrationDatabase database, string sql)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Review SQL queries - constant catalogue queries written by the story 2.2 classes.
        command.CommandText = sql;
#pragma warning restore CA2100

        var values = new List<string>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            values.Add(Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
        }

        return values;
    }

    /// <summary>
    /// Polls until at least <paramref name="expected"/> requests on this database are blocked on any lock
    /// (<c>LCK_M_%</c>), with a deadline and no sleep; fails if the racing requests answer first, because then
    /// they never waited on the held range at all. Story 2.2 Task 7: the rule for every race case - control
    /// the interleaving with a lock, never fire requests together and hope. Used by Project's BND-002 too.
    /// </summary>
    public static async Task WaitUntilBlockedOnALockAsync(SqlConnection observer, int expected, Task racing, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);

        while (await BlockedOnALockAsync(observer, cancellationToken) < expected)
        {
            Assert.False(racing.IsCompleted, "A creation answered without waiting on the held bound-count range.");

            if (DateTimeOffset.UtcNow >= deadline)
            {
                Assert.Fail($"Fewer than {expected} creations ever waited on the held bound-count range.");
            }
        }
    }

    private static async Task<int> BlockedOnALockAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT COUNT(*) FROM sys.dm_exec_requests WHERE database_id = DB_ID() AND blocking_session_id <> 0 AND wait_type LIKE 'LCK_M_%'";

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Runs one statement with parameters and answers the rows it affected.
    /// </summary>
    public static async Task<int> ExecuteAsync(SqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Review SQL queries - constant statements written by the story 2.2 classes; values are parameters.
        command.CommandText = sql;
#pragma warning restore CA2100

        foreach (var (name, value) in parameters)
        {
            command.Parameters.Add(new SqlParameter(name, value));
        }

        return await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}

/// <summary>
/// A registered, signed-in Account and its Personal Space.
/// </summary>
/// <param name="Session">Its browser-shaped client.</param>
/// <param name="AccountId">Its id.</param>
/// <param name="SpaceId">Its Personal Space.</param>
internal sealed record Actor(WireSession Session, Guid AccountId, Guid SpaceId);

/// <summary>
/// One <c>dbo.StatusDefinition</c> row.
/// </summary>
/// <param name="Id">The id.</param>
/// <param name="Name">The name.</param>
/// <param name="Position">Its position in the Space's set.</param>
internal sealed record StatusRow(Guid Id, string Name, int Position);

/// <summary>
/// One <c>dbo.[Task]</c> row as story 2.2 Task 2 shapes it.
/// </summary>
/// <param name="Id">The id.</param>
/// <param name="SpaceId">The Space, fixed at creation.</param>
/// <param name="ProjectId">The Project.</param>
/// <param name="StatusId">The Status.</param>
/// <param name="Title">The title.</param>
/// <param name="PositionKey">The position key within its column.</param>
/// <param name="CreatedAt">When it was created, UTC.</param>
internal sealed record TaskRow(Guid Id, Guid SpaceId, Guid ProjectId, Guid StatusId, string Title, string PositionKey, DateTimeOffset CreatedAt);
