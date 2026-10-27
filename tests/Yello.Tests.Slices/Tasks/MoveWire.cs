using System.Data;
using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts.Tasks;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// The wire and SQL helpers story 2.6's move cases add beside <see cref="TaskWire"/>, <see cref="TaskEditWire"/>,
/// <see cref="LabelWire"/> and <see cref="DeleteWire"/>: send a move over HTTP (with a Session, with a Token, and with no
/// credential at all), read a column's keys, seed Tasks at chosen keys, and play the move itself inside the test's own open
/// transaction as the competing writer of a race. Written by the story 2.6 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>The route and the problem type are scaffold constants until story Task 5 adds them</b>: <see cref="MoveOf"/> is the
/// path Q1 (a) recommends (<c>TaskRoutes.MoveOf</c>, <c>/api/v1/spaces/{spaceId}/tasks/{taskId}/move</c>) and
/// <see cref="TaskPositionUnavailable"/> is Q3 (a)'s type (<c>ProblemTypes.TaskPositionUnavailable</c>, <c>409</c>). Replace
/// both with the contract members when they exist; nothing else here changes.
/// </para>
/// <para>
/// <b>Bodies are anonymous objects or raw JSON</b>, never a contract type: <c>MoveTaskRequest</c> does not exist yet, and a
/// case must be able to send what the contract forbids (a <c>positionKey</c> member, a missing <c>statusId</c>).
/// <c>JsonSerializerDefaults.Web</c> writes <c>afterTaskId: null</c> rather than dropping it - Q2 (a)'s "top of column".
/// </para>
/// <para>
/// <b>Seed constraints checked</b> against <c>20261025115412_TaskTable.cs</c> and <c>20261025143020_TaskDueDate.cs</c> as
/// they stand. <see cref="SeedTaskAtAsync"/> writes one <c>dbo.[Task]</c> row with every non-nullable column supplied
/// (<c>Id</c> - <c>ValueGeneratedNever</c>, <c>SpaceId</c>, <c>ProjectId</c>, <c>StatusId</c>, <c>Title</c>,
/// <c>PositionKey</c>, <c>CreatedAt</c>; <c>DueDate</c> is nullable), under the Space's own session key (the
/// <c>BLOCK AFTER INSERT</c> predicate), naming a Project the product created in that Space (the composite
/// <c>FK_Task_Project_SpaceId_ProjectId</c>) and one of that Space's own Statuses (the composite <c>(SpaceId, StatusId)</c>
/// key, <c>NO_ACTION</c>). The caller chooses the key: it must be unique in its column (the unique
/// <c>IX_Task_SpaceId_ProjectId_StatusId_PositionKey</c>), at most 128 characters (<c>varchar(128)</c>,
/// <c>PositionKey.MaxLength</c>), and - for a key the product must later compare - in the base-62 alphabet. The parameter
/// is typed <c>varchar(128)</c> so a 128-character key is never truncated by the client.
/// </para>
/// <para>
/// <b>The held move is the product's statement shape</b> (story Task 3's recommended order): the Project's Task range in the
/// count shape every key placer takes (<see cref="DeleteWire.HoldProjectRangeAsync"/>), then the Task row on its clustered
/// key (<see cref="DeleteWire.HoldTaskRowAsync"/>), then one <c>UPDATE</c> of that row alone. Story Task 7 makes the move
/// the request in half the races and this holder the competing writer in the other half.
/// </para>
/// </remarks>
internal static class MoveWire
{
    /// <summary>
    /// Q3 (a)'s problem type: a neighbour that is not (or is no longer) a Task in the destination column of the same
    /// Project, and Q10's exhausted gap. Story Task 5 adds it as <c>ProblemTypes.TaskPositionUnavailable</c>.
    /// </summary>
    public const string TaskPositionUnavailable = "urn:yello:problem:task-position-unavailable";

    /// <summary>
    /// One Task's move path, as <c>TaskRoutes.MoveOf</c> will state it (Q1 (a)).
    /// </summary>
    public static string MoveOf(Guid spaceId, Guid taskId) => TaskRoutes.TaskOf(spaceId, taskId) + "/move";

    /// <summary>
    /// A move request as Q2 (a) shapes it: the destination Status and the Task to land after (<c>null</c> = the top of
    /// the column).
    /// </summary>
    public static JsonContent MoveBody(Guid statusId, Guid? afterTaskId) =>
        JsonContent.Create(new { statusId, afterTaskId }, options: TaskWire.JsonOptions);

    public static Task<WireResponse> MoveTaskAsync(Actor actor, Guid spaceId, Guid taskId, HttpContent body, string? key = null) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Post, MoveOf(spaceId, taskId), body, TaskWire.KeyHeader(key));

    public static Task<WireResponse> MoveTaskAsync(Actor actor, Guid spaceId, Guid taskId, Guid statusId, Guid? afterTaskId, string? key = null) =>
        MoveTaskAsync(actor, spaceId, taskId, MoveBody(statusId, afterTaskId), key);

    public static Task<WireResponse> MoveTaskWithTokenAsync(Actor actor, string token, Guid spaceId, Guid taskId, HttpContent body) =>
        actor.Session.SendWithTokenAndHeadersAsync(HttpMethod.Post, MoveOf(spaceId, taskId), token, body, TaskWire.NoHeaders);

    /// <summary>
    /// The same move with no credential at all - no cookie, no Token, no anti-forgery header: the anchor a Token's answer
    /// on a Session-only route must equal byte for byte (Q1 (a)).
    /// </summary>
    public static Task<WireResponse> MoveTaskWithoutCredentialAsync(HttpClient client, Guid spaceId, Guid taskId, HttpContent body) =>
        new WireSession(client).SendWithHeadersAsync(HttpMethod.Post, MoveOf(spaceId, taskId), body, TaskWire.NoHeaders, withAntiforgeryToken: false);

    /// <summary>
    /// The moved Task's <c>statusId</c> and <c>positionKey</c> from a <c>200</c> answer.
    /// </summary>
    public static (Guid StatusId, string PositionKey) PlacementOf(WireResponse moved)
    {
        using var document = JsonDocument.Parse(moved.Body);

        return (document.RootElement.GetProperty("statusId").GetGuid(), document.RootElement.GetProperty("positionKey").GetString() ?? string.Empty);
    }

    /// <summary>
    /// The Task ids of one column of one Project, in the database's order for that column - <c>PositionKey</c> under
    /// the column's own collation (BIN2), then id - read under the Space's key.
    /// </summary>
    public static async Task<IReadOnlyList<Guid>> ColumnAsync(RegistrationDatabase database, Guid spaceId, Guid projectId, Guid statusId) =>
        [.. (await TaskWire.TaskRowsAsync(database, spaceId)).Where(row => row.ProjectId == projectId && row.StatusId == statusId).Select(row => row.Id)];

    /// <summary>
    /// Every Task's (id, Status, key) in one Project, ordered by id - the before/after snapshot AC3's "no sibling
    /// renumbered" is compared on.
    /// </summary>
    public static async Task<IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)>> PlacementsAsync(RegistrationDatabase database, Guid spaceId, Guid projectId) =>
        [.. (await TaskWire.TaskRowsAsync(database, spaceId))
            .Where(row => row.ProjectId == projectId)
            .Select(row => (row.Id, row.StatusId, row.PositionKey))
            .OrderBy(row => row.Id)];

    /// <summary>
    /// One Task's position key, under the Space's key.
    /// </summary>
    public static async Task<string> KeyOfAsync(RegistrationDatabase database, Guid spaceId, Guid taskId) =>
        (await TaskWire.TaskRowsAsync(database, spaceId)).Single(row => row.Id == taskId).PositionKey;

    /// <summary>
    /// Seeds one Task at a key the caller chooses, in its own connection under the Space's key. See the class remarks for
    /// every constraint it satisfies.
    /// </summary>
    public static async Task<Guid> SeedTaskAtAsync(RegistrationDatabase database, Guid spaceId, Guid projectId, Guid statusId, string positionKey, string title = "Seeded at a key")
    {
        var taskId = Guid.NewGuid();

        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await InsertTaskAtAsync(connection, transaction, new TaskPlacement(taskId, spaceId, projectId, statusId, positionKey), title, TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return taskId;
    }

    /// <summary>
    /// What a placement writes: one new Task row at <paramref name="placement"/>'s key, in the caller's open transaction
    /// (RACE-M2's "the test does the other write"). <c>varchar(128)</c>, unlike <see cref="DeleteWire.InsertTaskAsync"/>.
    /// </summary>
    public static async Task InsertTaskAtAsync(SqlConnection connection, DbTransaction transaction, TaskPlacement placement, string title, CancellationToken cancellationToken)
    {
        var (taskId, spaceId, projectId, statusId, positionKey) = placement;

        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT dbo.[Task] (Id, SpaceId, ProjectId, StatusId, Title, PositionKey, CreatedAt)
            VALUES (@id, @space, @project, @status, @title, @key, SYSDATETIMEOFFSET())
            """;
        command.Parameters.Add(new SqlParameter("@id", taskId));
        command.Parameters.Add(new SqlParameter("@space", spaceId));
        command.Parameters.Add(new SqlParameter("@project", projectId));
        command.Parameters.Add(new SqlParameter("@status", statusId));
        command.Parameters.Add(new SqlParameter("@title", SqlDbType.NVarChar, 200) { Value = title });
        command.Parameters.Add(new SqlParameter("@key", SqlDbType.VarChar, 128) { Value = positionKey });

        Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
    }

    /// <summary>
    /// The whole move story Task 3 recommends, played by the test in its open transaction: the Project's Task range (count
    /// shape), the Task row on its clustered key, then the row alone updated to <paramref name="statusId"/> at
    /// <paramref name="positionKey"/>. Left uncommitted for the caller to commit or roll back.
    /// </summary>
    public static async Task HoldMoveAsync(SqlConnection connection, DbTransaction transaction, Guid projectId, Guid taskId, Guid statusId, string positionKey, CancellationToken cancellationToken)
    {
        await DeleteWire.HoldProjectRangeAsync(connection, transaction, projectId, cancellationToken);
        await DeleteWire.HoldTaskRowAsync(connection, transaction, taskId, cancellationToken);
        await DeleteWire.MoveTaskRowAsync(connection, transaction, taskId, statusId, positionKey, cancellationToken);
    }

    /// <summary>
    /// A key strictly between two ordinal keys, written without the product's generator (which story Task 2 adds): the
    /// lower key extended by one alphabet character. Correct whenever <paramref name="lower"/> is not a prefix-run that
    /// reaches <paramref name="upper"/> - true for every pair these cases use, and asserted.
    /// </summary>
    public static string KeyJustAfter(string lower, string upper)
    {
        var key = lower + "1";

        Assert.True(
            string.CompareOrdinal(lower, key) < 0 && string.CompareOrdinal(key, upper) < 0,
            $"'{key}' does not sort strictly between '{lower}' and '{upper}'.");

        return key;
    }

    /// <summary>
    /// Whether <paramref name="key"/> sorts strictly between <paramref name="lower"/> and <paramref name="upper"/> by
    /// ordinal comparison (<c>null</c> is open-ended), with a message naming all three.
    /// </summary>
    public static void AssertStrictlyBetween(string? lower, string key, string? upper)
    {
        Assert.True(
            (lower is null || string.CompareOrdinal(lower, key) < 0) && (upper is null || string.CompareOrdinal(key, upper) < 0),
            $"'{key}' is not strictly between '{lower ?? "(open)"}' and '{upper ?? "(open)"}'.");
    }
}
