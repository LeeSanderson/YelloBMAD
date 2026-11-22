using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Net;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// The wire and SQL helpers for moving every Task of one Status into another Project: the request on either surface, the
/// answer's assertion, and the writes the race cases hold in the product's shape - a bulk move, an occupied Status's
/// removal, and a creation.
/// </summary>
/// <remarks>
/// <para>
/// <b>The played bulk move takes the product's locks</b>: both Projects' Tasks locks in the ordinal order of their lock names
/// (<see cref="ProjectMoveSliceWire.HoldProjectsAsync"/>), then the selected rows rewritten with <c>ProjectId</c>,
/// <c>StatusId</c> and <c>PositionKey</c>. A writer that queues behind it meets the same Project locks the product's bulk move
/// will hold. It writes the rows one statement each rather than in one <c>UPDATE</c>: the locks a queued writer meets are the
/// Project locks and the rows, which are the same either way.
/// </para>
/// <para>
/// <b>Seed constraints checked</b> against <c>TaskConfiguration</c> and the Task migrations:
/// <list type="bullet">
/// <item><description>Every played write keeps <c>SpaceId</c>, since the <c>BLOCK AFTER UPDATE</c> predicate refuses a change,
/// and runs under the Space's own session key.</description></item>
/// <item><description>A played destination is a Project the product created in the same Space, so the composite
/// <c>FK_Task_Project_SpaceId_ProjectId (SpaceId, ProjectId)</c> holds.</description></item>
/// <item><description>Every Status written is one of the Space's <c>StatusDefinition</c> rows, read through the product, so the
/// <c>(SpaceId, StatusId)</c> key holds.</description></item>
/// <item><description>Every key is the target column's last key, read under the target Project's lock, followed by a
/// four-digit ordinal (or <c>V</c> and the ordinal in an empty column): strictly increasing in the column, inside the
/// <c>PositionKey</c> alphabet, at most <c>varchar(128)</c>, so the unique
/// <c>(SpaceId, ProjectId, StatusId, PositionKey)</c> index holds.</description></item>
/// <item><description>A played removal writes its <c>StatusDeltaOperation</c> through
/// <see cref="Projects.StatusRemovalWire.InsertRemoveOperationAsync"/>, after it has moved the Status's Tasks, so no Task is
/// left outside its Project's set when it commits.</description></item>
/// </list>
/// </para>
/// </remarks>
internal static class StatusBulkMoveSliceWire
{
    public static Task<WireResponse> BulkMoveAsync(Actor actor, Guid spaceId, Guid projectId, Guid statusId, HttpContent body, string? key = null) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Post, StatusBulkMoveWire.PathOf(spaceId, projectId, statusId), body, TaskWire.KeyHeader(key));

    public static Task<WireResponse> BulkMoveWithTokenAsync(Actor actor, string token, Guid spaceId, Guid projectId, Guid statusId, HttpContent body, string? key = null) =>
        actor.Session.SendWithTokenAndHeadersAsync(HttpMethod.Post, StatusBulkMoveWire.PathOf(spaceId, projectId, statusId), token, body, TaskWire.KeyHeader(key));

    public static Task<WireResponse> BulkMoveOnAsync(IsolationSurface surface, Actor actor, string token, Guid projectId, Guid statusId, HttpContent body, string? key = null) =>
        surface == IsolationSurface.ApiToken
            ? BulkMoveWithTokenAsync(actor, token, actor.SpaceId, projectId, statusId, body, key)
            : BulkMoveAsync(actor, actor.SpaceId, projectId, statusId, body, key);

    /// <summary>
    /// Asserts a <c>200</c> stating the destination, the Status the Tasks now hold there and how many moved.
    /// </summary>
    public static void AssertBulkMoved(WireResponse? answer, Guid destinationId, Guid statusId, int movedCount)
    {
        Assert.NotNull(answer);
        Assert.True(answer.StatusCode == HttpStatusCode.OK, $"{answer.StatusCode} {answer.Body}");
        Assert.Equal(destinationId, ProjectMoveWire.ProjectIdIn(answer.Body));
        Assert.Equal(statusId, StatusBulkMoveWire.StatusIdIn(answer.Body));
        Assert.Equal(movedCount, StatusBulkMoveWire.MovedCountIn(answer.Body));
    }

    /// <summary>
    /// Creates <paramref name="titles"/> through the product in <paramref name="projectId"/>'s first Status, then moves each
    /// into <paramref name="statusId"/> when that is another Status; answers the ids in creation order, which is their order in
    /// the column.
    /// </summary>
    public static async Task<IReadOnlyList<Guid>> TasksInStatusAsync(Actor owner, Guid projectId, Guid statusId, Guid firstStatusId, params string[] titles)
    {
        var ids = new List<Guid>();

        foreach (var title in titles)
        {
            var id = TaskWire.IdOf(await TaskWire.CreateTaskAsync(owner, owner.SpaceId, projectId, title));

            if (statusId != firstStatusId)
            {
                Assert.Equal(HttpStatusCode.OK, (await MoveWire.MoveTaskAsync(owner, owner.SpaceId, id, statusId, afterTaskId: ids.Count > 0 ? ids[^1] : null)).StatusCode);
            }

            ids.Add(id);
        }

        return ids;
    }

    /// <summary>
    /// The whole bulk move, played by the test and left uncommitted: both Projects' locks in order, then every Task of the
    /// source Status, in its source key order, reparented to the end of the destination column.
    /// </summary>
    /// <returns>The moved Task ids, in the order they now hold in the destination column.</returns>
    public static async Task<IReadOnlyList<Guid>> HoldStatusBulkMoveAsync(SqlConnection connection, DbTransaction transaction, PlayedBulkMove move, CancellationToken cancellationToken)
    {
        await ProjectMoveSliceWire.HoldProjectsAsync(connection, transaction, move.SpaceId, [move.SourceId, move.DestinationId], cancellationToken);

        var selected = await ColumnUnderLockAsync(connection, transaction, move.SourceId, move.FromStatusId, cancellationToken);

        await RewriteAsync(connection, transaction, selected, move.DestinationId, move.ToStatusId, cancellationToken);

        return selected;
    }

    /// <summary>
    /// The removal of an occupied Status from one Project, played by the test and left uncommitted: the Project's lock, its
    /// Tasks in that Status moved to the end of <paramref name="toStatusId"/>'s column, then the removal's delta row.
    /// </summary>
    public static async Task HoldOccupiedRemovalAsync(SqlConnection connection, DbTransaction transaction, Guid spaceId, Guid projectId, Guid statusId, Guid toStatusId, CancellationToken cancellationToken)
    {
        await DeleteWire.HoldProjectRangeAsync(connection, transaction, projectId, cancellationToken);

        var occupants = await ColumnUnderLockAsync(connection, transaction, projectId, statusId, cancellationToken);

        await RewriteAsync(connection, transaction, occupants, projectId, toStatusId, cancellationToken);
        await Projects.StatusRemovalWire.InsertRemoveOperationAsync(connection, transaction, spaceId, projectId, statusId, cancellationToken);
    }

    /// <summary>
    /// A creation into one column, played by the test and left uncommitted: the Project's lock, then one new Task at the end
    /// of the column.
    /// </summary>
    /// <returns>The created Task's id.</returns>
    public static async Task<Guid> HoldCreationAsync(SqlConnection connection, DbTransaction transaction, Guid spaceId, Guid projectId, Guid statusId, CancellationToken cancellationToken)
    {
        await DeleteWire.HoldProjectRangeAsync(connection, transaction, projectId, cancellationToken);

        var taskId = Guid.NewGuid();
        var key = KeyAfter(await LastKeyAsync(connection, transaction, projectId, statusId, cancellationToken), 1);

        await MoveWire.InsertTaskAtAsync(connection, transaction, new TaskPlacement(taskId, spaceId, projectId, statusId, key), "Created behind the lock", cancellationToken);

        return taskId;
    }

    /// <summary>
    /// The key a played write gives the <paramref name="ordinal"/>th Task it appends after <paramref name="last"/>.
    /// </summary>
    public static string KeyAfter(string? last, int ordinal) => (last ?? "V") + ordinal.ToString("D4", CultureInfo.InvariantCulture);

    private static async Task<IReadOnlyList<Guid>> ColumnUnderLockAsync(SqlConnection connection, DbTransaction transaction, Guid projectId, Guid statusId, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = "SELECT Id FROM dbo.[Task] WHERE ProjectId = @project AND StatusId = @status ORDER BY PositionKey, Id";
        command.Parameters.Add(new SqlParameter("@project", projectId));
        command.Parameters.Add(new SqlParameter("@status", statusId));

        var ids = new List<Guid>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    private static async Task RewriteAsync(SqlConnection connection, DbTransaction transaction, IReadOnlyList<Guid> taskIds, Guid projectId, Guid statusId, CancellationToken cancellationToken)
    {
        var last = await LastKeyAsync(connection, transaction, projectId, statusId, cancellationToken);

        for (var index = 0; index < taskIds.Count; index++)
        {
            await using DbCommand command = connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText = "UPDATE dbo.[Task] SET ProjectId = @project, StatusId = @status, PositionKey = @key WHERE Id = @task";
            command.Parameters.Add(new SqlParameter("@task", taskIds[index]));
            command.Parameters.Add(new SqlParameter("@project", projectId));
            command.Parameters.Add(new SqlParameter("@status", statusId));
            command.Parameters.Add(new SqlParameter("@key", SqlDbType.VarChar, 128) { Value = KeyAfter(last, index + 1) });

            Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
        }
    }

    private static async Task<string?> LastKeyAsync(SqlConnection connection, DbTransaction transaction, Guid projectId, Guid statusId, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = "SELECT MAX(PositionKey) FROM dbo.[Task] WHERE ProjectId = @project AND StatusId = @status";
        command.Parameters.Add(new SqlParameter("@project", projectId));
        command.Parameters.Add(new SqlParameter("@status", statusId));

        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }
}

/// <summary>
/// A bulk move the test plays in SQL.
/// </summary>
/// <param name="SpaceId">The Space the holder's connection is opened for.</param>
/// <param name="SourceId">The Project the Tasks leave.</param>
/// <param name="DestinationId">The Project they join.</param>
/// <param name="FromStatusId">The Status whose Tasks move.</param>
/// <param name="ToStatusId">The Status they hold in the destination, one its set holds.</param>
internal sealed record PlayedBulkMove(Guid SpaceId, Guid SourceId, Guid DestinationId, Guid FromStatusId, Guid ToStatusId);
