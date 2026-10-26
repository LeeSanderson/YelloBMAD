using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts.Labels;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// The wire and SQL helpers story 2.4's Label cases add beside <see cref="TaskWire"/> and <see cref="TaskEditWire"/>:
/// define, list and delete Labels over HTTP on either surface, apply them through the Task <c>PATCH</c> (Q1 (a)), and
/// read <c>dbo.[Label]</c> and <c>dbo.[TaskLabel]</c> under a Space's own key. Written by the story 2.4 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Routes are <see cref="LabelRoutes"/>'s</b>; bodies are anonymous objects or JSON text, so a case can send what the contract will not type (a hex colour, a non-GUID id).
/// </para>
/// <para>
/// <b>Seed constraints checked</b> against story 2.4 Task 3 for the two tables that do not exist yet:
/// <c>dbo.[Label]</c> (<c>Id</c> supplied - <c>ValueGeneratedNever</c>; <c>SpaceId</c> non-null, FK to
/// <c>Space</c>; <c>Name nvarchar(50)</c>, unique per <c>(SpaceId, Name)</c>; <c>Colour varchar(16)</c>;
/// <c>CreatedAt datetimeoffset</c>; FILTER and BLOCK AFTER INSERT/UPDATE) and <c>dbo.[TaskLabel]</c> (key
/// <c>(SpaceId, TaskId, LabelId)</c>; <c>AppliedAt datetimeoffset</c>; composite FKs <c>(SpaceId, TaskId)</c> to
/// <c>Task (SpaceId, Id)</c> - the <c>AK_Task_SpaceId_Id</c> Task 3 adds - and <c>(SpaceId, LabelId)</c> to
/// <c>Label (SpaceId, Id)</c>; FILTER and BLOCK predicates). <see cref="InsertLabelAsync"/> and
/// <see cref="InsertApplicationAsync"/> run under the Space's own session key (the BLOCK AFTER INSERT predicate),
/// name a Task and a Label the product created in that same Space (both composite FKs), and use a fresh id and a
/// name unique to the call (the key and the unique index). <b>If Task 3 lands another non-nullable column without a
/// default, correct these two statements.</b>
/// </para>
/// </remarks>
internal static class LabelWire
{
    /// <summary>
    /// A palette key the story's Q3 recommends, for any case whose colour does not matter.
    /// </summary>
    public const string Rust = "rust";

    public static JsonContent DefinitionBody(string? name, string? colour) =>
        JsonContent.Create(new { name, colour }, options: TaskWire.JsonOptions);

    public static JsonContent ApplyBody(params Guid[] labelIds) =>
        JsonContent.Create(new { labelIds }, options: TaskWire.JsonOptions);

    public static Task<WireResponse> DefineAsync(Actor actor, Guid spaceId, string? name, string? colour = Rust, string? key = null) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Post, LabelRoutes.LabelsOf(spaceId), DefinitionBody(name, colour), TaskWire.KeyHeader(key));

    public static Task<WireResponse> DefineRawAsync(Actor actor, Guid spaceId, HttpContent body) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Post, LabelRoutes.LabelsOf(spaceId), body, TaskWire.NoHeaders);

    public static Task<WireResponse> DefineWithTokenAsync(Actor actor, string token, Guid spaceId, string? name, string? colour = Rust, string? key = null) =>
        actor.Session.SendWithTokenAndHeadersAsync(HttpMethod.Post, LabelRoutes.LabelsOf(spaceId), token, DefinitionBody(name, colour), TaskWire.KeyHeader(key));

    public static Task<WireResponse> DefineOnAsync(IsolationSurface surface, Actor actor, string token, Guid spaceId, string? name, string? colour = Rust, string? key = null) =>
        surface == IsolationSurface.ApiToken
            ? DefineWithTokenAsync(actor, token, spaceId, name, colour, key)
            : DefineAsync(actor, spaceId, name, colour, key);

    public static Task<WireResponse> ListAsync(Actor actor, Guid spaceId) =>
        actor.Session.GetAsync(LabelRoutes.LabelsOf(spaceId));

    public static Task<WireResponse> ListOnAsync(IsolationSurface surface, Actor actor, string token, Guid spaceId) =>
        surface == IsolationSurface.ApiToken
            ? actor.Session.GetWithTokenAsync(LabelRoutes.LabelsOf(spaceId), token)
            : ListAsync(actor, spaceId);

    public static Task<WireResponse> DeleteAsync(Actor actor, Guid spaceId, Guid labelId, string? key = null) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Delete, LabelRoutes.LabelOf(spaceId, labelId), content: null, TaskWire.KeyHeader(key));

    public static Task<WireResponse> DeleteWithTokenAsync(Actor actor, string token, Guid spaceId, Guid labelId, string? key = null) =>
        actor.Session.SendWithTokenAndHeadersAsync(HttpMethod.Delete, LabelRoutes.LabelOf(spaceId, labelId), token, content: null, TaskWire.KeyHeader(key));

    public static Task<WireResponse> DeleteOnAsync(IsolationSurface surface, Actor actor, string token, Guid spaceId, Guid labelId, string? key = null) =>
        surface == IsolationSurface.ApiToken
            ? DeleteWithTokenAsync(actor, token, spaceId, labelId, key)
            : DeleteAsync(actor, spaceId, labelId, key);

    /// <summary>
    /// Defines a Label through the product and answers its id, failing the case if it was not created.
    /// </summary>
    public static async Task<Guid> SeedAsync(Actor actor, Guid spaceId, string name, string colour = Rust) =>
        TaskWire.IdOf(await DefineAsync(actor, spaceId, name, colour));

    /// <summary>
    /// The <c>labelIds</c> of a Task answer (an edit or a read), in the order served.
    /// </summary>
    public static IReadOnlyList<Guid> LabelIdsOf(string taskBody)
    {
        using var document = JsonDocument.Parse(taskBody);

        return [.. document.RootElement.GetProperty("labelIds").EnumerateArray().Select(id => id.GetGuid())];
    }

    /// <summary>
    /// The ids of a Label list answer, in the order served.
    /// </summary>
    public static IReadOnlyList<Guid> ListedIds(string listBody)
    {
        using var document = JsonDocument.Parse(listBody);

        return [.. document.RootElement.EnumerateArray().Select(row => row.GetProperty("id").GetGuid())];
    }

    /// <summary>
    /// Every Label in the Space, read under its own key and ordered by name then id - the database's view.
    /// </summary>
    public static async Task<IReadOnlyList<LabelRow>> LabelRowsAsync(RegistrationDatabase database, Guid spaceId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Id, SpaceId, Name, Colour FROM dbo.[Label] ORDER BY Name, Id";

        var rows = new List<LabelRow>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add(new LabelRow(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3)));
        }

        return rows;
    }

    /// <summary>
    /// Every application in the Space, as (Task, Label), read under its own key.
    /// </summary>
    public static async Task<IReadOnlyList<(Guid TaskId, Guid LabelId)>> ApplicationsAsync(RegistrationDatabase database, Guid spaceId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT TaskId, LabelId FROM dbo.[TaskLabel] ORDER BY TaskId, LabelId";

        var rows = new List<(Guid TaskId, Guid LabelId)>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add((reader.GetGuid(0), reader.GetGuid(1)));
        }

        return rows;
    }

    /// <summary>
    /// The Labels applied to one Task, by SQL under the Space's key.
    /// </summary>
    public static async Task<IReadOnlyList<Guid>> AppliedToAsync(RegistrationDatabase database, Guid spaceId, Guid taskId) =>
        [.. (await ApplicationsAsync(database, spaceId)).Where(row => row.TaskId == taskId).Select(row => row.LabelId).Order()];

    /// <summary>
    /// Inserts a Label row in the caller's open transaction - the race cases' "other writer". See the class remarks for
    /// every constraint this satisfies.
    /// </summary>
    public static async Task InsertLabelAsync(SqlConnection connection, DbTransaction transaction, Guid labelId, Guid spaceId, string name, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText =
            "INSERT dbo.[Label] (Id, SpaceId, Name, Colour, CreatedAt) VALUES (@id, @space, @name, @colour, SYSDATETIMEOFFSET())";
        command.Parameters.Add(new SqlParameter("@id", labelId));
        command.Parameters.Add(new SqlParameter("@space", spaceId));
        command.Parameters.Add(new SqlParameter("@name", name));
        command.Parameters.Add(new SqlParameter("@colour", Rust));

        Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
    }

    /// <summary>
    /// Applies a Label to a Task by a row in the caller's open transaction - the race cases' "other writer". See the
    /// class remarks for every constraint this satisfies.
    /// </summary>
    public static async Task InsertApplicationAsync(SqlConnection connection, DbTransaction transaction, Guid spaceId, Guid taskId, Guid labelId, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText =
            "INSERT dbo.[TaskLabel] (SpaceId, TaskId, LabelId, AppliedAt) VALUES (@space, @task, @label, SYSDATETIMEOFFSET())";
        command.Parameters.Add(new SqlParameter("@space", spaceId));
        command.Parameters.Add(new SqlParameter("@task", taskId));
        command.Parameters.Add(new SqlParameter("@label", labelId));

        Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
    }

    /// <summary>
    /// Waits until at least <paramref name="expected"/> requests are blocked on a lock, or until
    /// <paramref name="racing"/> has answered without waiting - the race case where waiting is what the product must
    /// do and answering at once is the defect the assertions after it report. Never a delay.
    /// </summary>
    public static async Task WaitUntilBlockedOrAnsweredAsync(SqlConnection observer, int expected, Task racing, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);

        while (!racing.IsCompleted)
        {
            await using var command = observer.CreateCommand();

            command.CommandText = "SELECT COUNT(*) FROM sys.dm_exec_requests WHERE database_id = DB_ID() AND blocking_session_id <> 0 AND wait_type LIKE 'LCK_M_%'";

            if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) >= expected)
            {
                return;
            }

            Assert.True(DateTimeOffset.UtcNow < deadline, $"Fewer than {expected} requests ever waited on a lock, and the racing one never answered.");
        }
    }

    /// <summary>
    /// Takes, in the caller's open transaction, what a Labels edit of <paramref name="taskId"/> takes before it writes:
    /// the Task's row under <c>UPDLOCK, HOLDLOCK</c> on its clustered key, and the Task's <c>TaskLabel</c> range under
    /// the same hints - so a second edit that skipped the Task lock would read the Task's applications and then queue
    /// on the range to insert, instead of queueing before it read them.
    /// </summary>
    public static async Task HoldTaskLabelsAsync(SqlConnection connection, DbTransaction transaction, Guid spaceId, Guid taskId, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT COUNT(*) FROM dbo.[Task] WITH (UPDLOCK, HOLDLOCK, INDEX(1)) WHERE Id = @task;
            SELECT COUNT(*) FROM dbo.[TaskLabel] WITH (UPDLOCK, HOLDLOCK) WHERE SpaceId = @space AND TaskId = @task;
            """;
        command.Parameters.Add(new SqlParameter("@space", spaceId));
        command.Parameters.Add(new SqlParameter("@task", taskId));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        Assert.True(await reader.ReadAsync(cancellationToken));
        Assert.Equal(1, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync(cancellationToken));
        Assert.True(await reader.ReadAsync(cancellationToken));
        Assert.Equal(0, reader.GetInt32(0));
    }

    /// <summary>
    /// Deletes a Label row in the caller's open transaction - the race case where the deletion wins.
    /// </summary>
    public static async Task DeleteLabelRowAsync(SqlConnection connection, DbTransaction transaction, Guid labelId, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = "DELETE dbo.[Label] WHERE Id = @id";
        command.Parameters.Add(new SqlParameter("@id", labelId));

        Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
    }
}

/// <summary>
/// One <c>dbo.[Label]</c> row as story 2.4 Task 3 shapes it.
/// </summary>
/// <param name="Id">The id.</param>
/// <param name="SpaceId">The Space, fixed at creation.</param>
/// <param name="Name">The name.</param>
/// <param name="Colour">The palette key.</param>
internal sealed record LabelRow(Guid Id, Guid SpaceId, string Name, string Colour);
