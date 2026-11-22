using System.Data;
using System.Net;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Merge;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// The wire and SQL helpers for a Task's description: an editor replica made through the port, the change batch and the read
/// on either surface, the change log and the projection read under the Space's own key, and the Task row held in the
/// product's lock shape so an append can be made to queue.
/// </summary>
/// <remarks>
/// <para>
/// <b>An editor is a replica of the real strategy</b> (<see cref="TextMerge.CreateStrategy"/>), under a fresh identity per
/// editor, never <see cref="Guid.Empty"/>, as an opened panel is. A batch carries only changes and the editor's version.
/// </para>
/// <para>
/// <b>Schema assumed</b> (the table does not exist yet): <c>dbo.[TaskDescriptionChange]</c> with <c>Id</c>, <c>SpaceId</c>,
/// <c>TaskId</c>, <c>Sequence</c> (<c>bigint</c>, unique per <c>(SpaceId, TaskId)</c>), <c>Kind</c> stored by name
/// (<c>Change</c> or <c>Snapshot</c>, as every enum here is stored), <c>Payload</c> (<c>varbinary(max)</c>),
/// <c>AuthorAccountId</c> and <c>AuthorName</c> (both nullable) and <c>RecordedAt</c>; and <c>dbo.[Task].Description</c>.
/// If the migration names a column otherwise, correct the statements here and record it.
/// </para>
/// <para>
/// <b>Seed constraints for <see cref="InsertChangeAsync"/></b>, the one raw write of the log, made only by the read-race case
/// in the holding transaction: the Task is one the product created in the Space, so the composite
/// <c>(SpaceId, TaskId)</c> key to <c>Task</c> holds; it runs under the Space's own session key, so the
/// <c>BLOCK AFTER INSERT</c> predicate admits it; its <c>Sequence</c> is the Task's <c>MAX + 1</c> read under the held
/// Task lock, so the unique <c>(SpaceId, TaskId, Sequence)</c> index holds; its author is the registered Owner, so the
/// <c>AuthorAccountId</c> key to <c>Account</c> holds; its payload is a change made by the real strategy.
/// </para>
/// </remarks>
internal static class TaskDescriptionSliceWire
{
    public const string ChangeKind = "Change";

    public const string SnapshotKind = "Snapshot";

    public static ITextMergeStrategy Strategy => TextMerge.CreateStrategy();

    /// <summary>
    /// A fresh editor with no history, as a panel opened on a Task that has never been described.
    /// </summary>
    public static ITextReplica NewEditor() => Strategy.CreateReplica(new ReplicaId(Guid.NewGuid()));

    /// <summary>
    /// An editor bootstrapped from a read answer's state, under a fresh identity.
    /// </summary>
    public static ITextReplica EditorFrom(WireResponse read)
    {
        var state = TaskDescriptionWire.StateIn(read.Body);

        Assert.True(state is not null, $"The read answer carries no state: {read.Body}");

        var loaded = Strategy.LoadReplica(new ReplicaId(Guid.NewGuid()), state);

        Assert.Equal(ChangeAdmission.Admitted, loaded.Admission);

        return loaded.Value!;
    }

    /// <summary>
    /// The batch for <paramref name="changes"/>, carrying <paramref name="editor"/>'s version as it is now.
    /// </summary>
    public static HttpContent BatchOf(ITextReplica editor, params TextChange[] changes) =>
        TaskDescriptionWire.Body(changes.Select(change => change.Encoded.ToArray()), editor.Version.Encoded.ToArray());

    public static Task<WireResponse> SendChangesAsync(Actor actor, Guid spaceId, Guid taskId, HttpContent body, string? key = null) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Post, TaskDescriptionWire.ChangesOf(spaceId, taskId), body, TaskWire.KeyHeader(key));

    public static Task<WireResponse> SendChangesWithTokenAsync(Actor actor, string token, Guid spaceId, Guid taskId, HttpContent body, string? key = null) =>
        actor.Session.SendWithTokenAndHeadersAsync(HttpMethod.Post, TaskDescriptionWire.ChangesOf(spaceId, taskId), token, body, TaskWire.KeyHeader(key));

    public static Task<WireResponse> SendChangesOnAsync(IsolationSurface surface, Actor actor, string token, Guid taskId, HttpContent body, string? key = null) =>
        surface == IsolationSurface.ApiToken
            ? SendChangesWithTokenAsync(actor, token, actor.SpaceId, taskId, body, key)
            : SendChangesAsync(actor, actor.SpaceId, taskId, body, key);

    public static Task<WireResponse> ReadAsync(Actor actor, Guid spaceId, Guid taskId) =>
        actor.Session.GetAsync(TaskDescriptionWire.Of(spaceId, taskId));

    public static Task<WireResponse> ReadWithTokenAsync(Actor actor, string token, Guid spaceId, Guid taskId) =>
        actor.Session.GetWithTokenAsync(TaskDescriptionWire.Of(spaceId, taskId), token);

    /// <summary>
    /// Asserts a <c>200</c> change answer that refused <paramref name="refused"/> changes.
    /// </summary>
    public static void AssertAnswered(WireResponse? answer, int refused)
    {
        Assert.NotNull(answer);
        Assert.True(answer.StatusCode == HttpStatusCode.OK, $"{answer.StatusCode} {answer.Body}");
        Assert.Equal(refused, TaskDescriptionWire.RefusedCountIn(answer.Body));
        Assert.NotNull(TaskDescriptionWire.VersionIn(answer.Body));
    }

    /// <summary>
    /// A Project and one Task in it, created through the product; answers the Task's id.
    /// </summary>
    public static async Task<Guid> TaskAsync(Actor owner, string title = "Describe me")
    {
        var projectId = await TaskWire.CreateProjectAsync(owner, $"Described {Guid.NewGuid():N}"[..24]);

        return TaskWire.IdOf(await TaskWire.CreateTaskAsync(owner, owner.SpaceId, projectId, title));
    }

    /// <summary>
    /// Every row of one Task's log, in <c>Sequence</c> order, read under the Space's own key.
    /// </summary>
    public static async Task<IReadOnlyList<DescriptionRow>> RowsAsync(RegistrationDatabase database, Guid spaceId, Guid taskId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT Id, Sequence, CONVERT(nvarchar(32), Kind), Payload, AuthorAccountId, AuthorName, RecordedAt
            FROM dbo.[TaskDescriptionChange]
            WHERE TaskId = @task
            ORDER BY Sequence
            """;
        command.Parameters.Add(new SqlParameter("@task", taskId));

        var rows = new List<DescriptionRow>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add(new DescriptionRow(
                reader.GetGuid(0),
                reader.GetInt64(1),
                reader.GetString(2),
                await reader.GetFieldValueAsync<byte[]>(3, TestContext.Current.CancellationToken),
                await reader.IsDBNullAsync(4, TestContext.Current.CancellationToken) ? null : reader.GetGuid(4),
                await reader.IsDBNullAsync(5, TestContext.Current.CancellationToken) ? null : reader.GetString(5),
                await reader.GetFieldValueAsync<DateTimeOffset>(6, TestContext.Current.CancellationToken)));
        }

        return rows;
    }

    /// <summary>
    /// Every log row in the Space, whichever Task it belongs to - for the deletions, which must leave none.
    /// </summary>
    public static async Task<int> RowCountInSpaceAsync(RegistrationDatabase database, Guid spaceId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT COUNT(*) FROM dbo.[TaskDescriptionChange] WHERE SpaceId = @space";
        command.Parameters.Add(new SqlParameter("@space", spaceId));

        return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The projection column, read as a column.
    /// </summary>
    public static async Task<string?> DescriptionAsync(RegistrationDatabase database, Guid spaceId, Guid taskId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Description FROM dbo.[Task] WHERE Id = @task";
        command.Parameters.Add(new SqlParameter("@task", taskId));

        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) as string;
    }

    /// <summary>
    /// The text the log rebuilds to, read the way the server is to read it: the latest snapshot, then every change after it.
    /// </summary>
    public static string RebuiltText(IReadOnlyList<DescriptionRow> rows)
    {
        var strategy = Strategy;
        var snapshot = rows.LastOrDefault(row => row.Kind == SnapshotKind);
        ITextReplica replica;

        if (snapshot is null)
        {
            replica = strategy.CreateReplica(new ReplicaId(Guid.NewGuid()));
        }
        else
        {
            var loaded = strategy.LoadReplica(new ReplicaId(Guid.NewGuid()), snapshot.Payload);

            Assert.Equal(ChangeAdmission.Admitted, loaded.Admission);
            replica = loaded.Value!;
        }

        foreach (var row in rows.Where(row => row.Kind == ChangeKind && row.Sequence > (snapshot?.Sequence ?? 0)))
        {
            var change = strategy.DecodeChange(row.Payload);

            Assert.Equal(ChangeAdmission.Admitted, change.Admission);
            Assert.Equal(ChangeAdmission.Admitted, replica.Apply(change.Value!));
        }

        return replica.Text;
    }

    /// <summary>
    /// Opens a connection under the Space's key, begins a transaction and takes the Task row as the product's writers do
    /// (<c>UPDLOCK, HOLDLOCK, INDEX(1)</c>), so an append made meanwhile queues on it.
    /// </summary>
    public static async Task<(SqlConnection Connection, SqlTransaction Transaction)> HoldTaskAsync(RegistrationDatabase database, Guid spaceId, Guid taskId)
    {
        var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        var transaction = Assert.IsType<SqlTransaction>(await connection.BeginTransactionAsync(TestContext.Current.CancellationToken));
        await using var command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = "SELECT Id FROM dbo.[Task] WITH (UPDLOCK, HOLDLOCK, INDEX(1)) WHERE Id = @task";
        command.Parameters.Add(new SqlParameter("@task", taskId));

        Assert.Equal(taskId, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));

        return (connection, transaction);
    }

    /// <summary>
    /// Appends one change row and re-projects, in the holding transaction, in the product's shape: the next sequence, the
    /// author and the name captured now, then the projection column. See the seed constraints above.
    /// </summary>
    public static async Task InsertChangeAsync(SqlConnection connection, SqlTransaction transaction, Guid spaceId, Guid taskId, TextChange change, (Guid AccountId, string Name) author, string projection)
    {
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT dbo.[TaskDescriptionChange] (Id, SpaceId, TaskId, Sequence, Kind, Payload, AuthorAccountId, AuthorName, RecordedAt)
                SELECT NEWID(), @space, @task, COALESCE(MAX(Sequence), 0) + 1, N'Change', @payload, @author, @name, SYSDATETIMEOFFSET()
                FROM dbo.[TaskDescriptionChange] WHERE TaskId = @task
                """;
            insert.Parameters.Add(new SqlParameter("@space", spaceId));
            insert.Parameters.Add(new SqlParameter("@task", taskId));
            insert.Parameters.Add(new SqlParameter("@payload", SqlDbType.VarBinary, -1) { Value = change.Encoded.ToArray() });
            insert.Parameters.Add(new SqlParameter("@author", author.AccountId));
            insert.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 256) { Value = author.Name });

            Assert.Equal(1, await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        }

        await using var project = connection.CreateCommand();

        project.Transaction = transaction;
        project.CommandText = "UPDATE dbo.[Task] SET Description = @text WHERE Id = @task";
        project.Parameters.Add(new SqlParameter("@task", taskId));
        project.Parameters.Add(new SqlParameter("@text", SqlDbType.NVarChar, -1) { Value = projection });

        Assert.Equal(1, await project.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
    }
}

/// <summary>
/// One row of a Task's description log.
/// </summary>
/// <param name="Id">The id.</param>
/// <param name="Sequence">Its place in the log.</param>
/// <param name="Kind"><c>Change</c> or <c>Snapshot</c>.</param>
/// <param name="Payload">The encoded change, or the encoded replica state for a snapshot.</param>
/// <param name="AuthorAccountId">The writing Account, or <c>null</c> for a snapshot or an erased Account.</param>
/// <param name="AuthorName">The name captured at write time, or <c>null</c> for a snapshot.</param>
/// <param name="RecordedAt">When it was written, UTC.</param>
internal sealed record DescriptionRow(Guid Id, long Sequence, string Kind, byte[] Payload, Guid? AuthorAccountId, string? AuthorName, DateTimeOffset RecordedAt);
