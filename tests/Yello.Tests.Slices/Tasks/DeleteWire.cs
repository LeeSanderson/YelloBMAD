using System.Data.Common;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts.Projects;
using Yello.Contracts.Tasks;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// The wire and SQL helpers story 2.5's deletion cases add beside <see cref="TaskWire"/>, <see cref="TaskEditWire"/> and
/// <see cref="LabelWire"/>: delete a Task or a Project over HTTP on either surface (no body - 2.4's "DELETE carries no
/// body"), and play, inside the test's own open transaction, every competing writer a deletion races - the Status edit,
/// the Labels edit, the creation and the deletion itself. Written by the story 2.5 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Routes are the contract's.</b> The Task's item route is <see cref="TaskRoutes.TaskOf"/>; the Project's item route
/// is <see cref="ProjectRoutes.ProjectOf"/> - the path 2.1's rename already serves and story Task 4 names
/// <c>ProjectRoutes.Project</c>. Both exist today, so nothing here needs a scaffold constant.
/// </para>
/// <para>
/// <b>Each holder takes exactly what story Task 3 says the product takes, in the product's statement shape</b>
/// (<c>TaskStore</c>, <c>BoundCounter</c>): the Project's Tasks lock is the application lock
/// <c>Yello/ProjectTasks/{spaceId}/{projectId}</c> since story 2.9 (it was the range
/// <c>SELECT COUNT(*) FROM dbo.[Task] WITH (UPDLOCK, HOLDLOCK) WHERE ProjectId = @p</c>, and the helpers keep "range" in
/// their names); a Task or a Project row is locked
/// <c>WITH (UPDLOCK, HOLDLOCK, INDEX(1))</c> on its clustered key, so the test and the request meet on the same key (the
/// 2.4 deadlock was two lockers on two indexes of one row).
/// </para>
/// <para>
/// <b>Seed constraints checked</b> against <c>20261025115412_TaskTable.cs</c>, <c>20261025143020_TaskDueDate.cs</c>
/// and <c>20261025175001_LabelTables.cs</c> as they stand: <c>dbo.[Task]</c> takes <c>Id</c> (supplied -
/// <c>ValueGeneratedNever</c>), <c>SpaceId</c>, <c>ProjectId</c>, <c>StatusId</c>, <c>Title</c>, <c>PositionKey</c>,
/// <c>CreatedAt</c> and a nullable <c>DueDate</c>. <see cref="InsertTaskAsync"/> runs under the Space's own session key
/// (<c>BLOCK AFTER INSERT</c>), names a Project the product created in that Space
/// (<c>FK_Task_Project_SpaceId_ProjectId</c>, composite, <c>CASCADE</c>) and one of that Space's Statuses
/// (<c>(SpaceId, StatusId)</c>, <c>NO_ACTION</c>), with a key the caller computes after the column's last
/// (the unique <c>(SpaceId, ProjectId, StatusId, PositionKey)</c> index). <see cref="DeleteTaskRowAsync"/> and
/// <see cref="DeleteProjectRowAsync"/> delete through the FILTER predicate under the same key, and the database's own
/// cascades (<c>TaskLabel→Task</c>, <c>Task→Project</c>, both <c>CASCADE</c>) remove what hangs from the row - exactly
/// what the product's <c>ExecuteDeleteAsync</c> relies on. These are the competing writer of a race, never cleanup
/// (TESTING-CONVENTIONS: cleanup is by disposal).
/// </para>
/// </remarks>
internal static class DeleteWire
{
    public static Task<WireResponse> DeleteTaskAsync(Actor actor, Guid spaceId, Guid taskId, string? key = null) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Delete, TaskRoutes.TaskOf(spaceId, taskId), content: null, TaskWire.KeyHeader(key));

    public static Task<WireResponse> DeleteTaskWithTokenAsync(Actor actor, string token, Guid spaceId, Guid taskId, string? key = null) =>
        actor.Session.SendWithTokenAndHeadersAsync(HttpMethod.Delete, TaskRoutes.TaskOf(spaceId, taskId), token, content: null, TaskWire.KeyHeader(key));

    public static Task<WireResponse> DeleteTaskOnAsync(IsolationSurface surface, Actor actor, string token, Guid spaceId, Guid taskId, string? key = null) =>
        surface == IsolationSurface.ApiToken
            ? DeleteTaskWithTokenAsync(actor, token, spaceId, taskId, key)
            : DeleteTaskAsync(actor, spaceId, taskId, key);

    public static Task<WireResponse> DeleteProjectAsync(Actor actor, Guid spaceId, Guid projectId, string? key = null) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Delete, ProjectRoutes.ProjectOf(spaceId, projectId), content: null, TaskWire.KeyHeader(key));

    public static Task<WireResponse> DeleteProjectWithTokenAsync(Actor actor, string token, Guid spaceId, Guid projectId, string? key = null) =>
        actor.Session.SendWithTokenAndHeadersAsync(HttpMethod.Delete, ProjectRoutes.ProjectOf(spaceId, projectId), token, content: null, TaskWire.KeyHeader(key));

    public static Task<WireResponse> DeleteProjectOnAsync(IsolationSurface surface, Actor actor, string token, Guid spaceId, Guid projectId, string? key = null) =>
        surface == IsolationSurface.ApiToken
            ? DeleteProjectWithTokenAsync(actor, token, spaceId, projectId, key)
            : DeleteProjectAsync(actor, spaceId, projectId, key);

    /// <summary>
    /// The Board answer's columns as (Status id, true total, the Task ids listed), in the order served.
    /// </summary>
    public static IReadOnlyList<(Guid StatusId, int Total, IReadOnlyList<Guid> TaskIds)> Columns(WireResponse board)
    {
        using var document = JsonDocument.Parse(board.Body);

        var columns = new List<(Guid StatusId, int Total, IReadOnlyList<Guid> TaskIds)>();

        foreach (var column in document.RootElement.GetProperty("columns").EnumerateArray())
        {
            List<Guid> taskIds = [.. column.GetProperty("tasks").EnumerateArray().Select(task => task.GetProperty("id").GetGuid())];

            columns.Add((column.GetProperty("statusId").GetGuid(), column.GetProperty("total").GetInt32(), taskIds));
        }

        return columns;
    }

    /// <summary>
    /// The ids a Project listing answer serves, in order.
    /// </summary>
    public static IReadOnlyList<Guid> ListedProjectIds(WireResponse listing)
    {
        using var document = JsonDocument.Parse(listing.Body);

        return [.. document.RootElement.EnumerateArray().Select(row => row.GetProperty("id").GetGuid())];
    }

    /// <summary>
    /// The applications of any of <paramref name="taskIds"/>, read under the Space's own key - so a row the filter hid
    /// is never mistaken for a row that is gone: the Space's own rows are visible to its own key.
    /// </summary>
    public static async Task<int> ApplicationsOfAsync(RegistrationDatabase database, Guid spaceId, IReadOnlyCollection<Guid> taskIds) =>
        (await LabelWire.ApplicationsAsync(database, spaceId)).Count(row => taskIds.Contains(row.TaskId));

    /// <summary>
    /// Takes the Project's Tasks lock in the caller's open transaction - the lock a creation's bound count, a Status edit's
    /// key placement, a move and a deletion all take, so they meet on it. Story 2.9 made it an application lock named for
    /// the connection's Space and the Project (<c>Yello/ProjectTasks/{spaceId}/{projectId}</c>, <c>ApplicationLocks</c>); it
    /// was the Project's Task range under <c>UPDLOCK, HOLDLOCK</c>, whose last key could lie in another Space. The Space is
    /// the connection's <c>SESSION_CONTEXT</c>, so the holder is opened for the Project's Space (the name is built as
    /// <see cref="HoldProjectsPerSpaceAsync"/> builds its own).
    /// </summary>
    public static async Task HoldProjectRangeAsync(SqlConnection connection, DbTransaction transaction, Guid projectId, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText =
            """
            DECLARE @resource nvarchar(255) = CONCAT(N'Yello/ProjectTasks/', LOWER(CONVERT(nvarchar(36), CAST(SESSION_CONTEXT(N'SpaceId') AS uniqueidentifier))), N'/', LOWER(CONVERT(nvarchar(36), @project)));
            IF SESSION_CONTEXT(N'SpaceId') IS NULL THROW 51001, 'The holder is not opened for a Space.', 1;
            EXEC sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction';
            """;
        command.Parameters.Add(new SqlParameter("@project", projectId));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Takes the connection's Space's Projects-per-Space lock in the caller's open transaction - the lock a Project creation's
    /// bound count and a Project deletion take first (story 2.9, open question 14 (a)).
    /// </summary>
    public static async Task HoldProjectsPerSpaceAsync(SqlConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText =
            """
            DECLARE @resource nvarchar(255) = CONCAT(N'Yello/ProjectsPerSpace/', LOWER(CONVERT(nvarchar(36), CAST(SESSION_CONTEXT(N'SpaceId') AS uniqueidentifier))));
            EXEC sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction';
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Locks one Task row on its clustered key in the caller's open transaction - the edit's Task lock and (story
    /// Task 3, step 3) the deletion's.
    /// </summary>
    public static async Task HoldTaskRowAsync(SqlConnection connection, DbTransaction transaction, Guid taskId, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM dbo.[Task] WITH (UPDLOCK, HOLDLOCK, INDEX(1)) WHERE Id = @task";
        command.Parameters.Add(new SqlParameter("@task", taskId));

        Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Locks one Project row on its clustered key in the caller's open transaction - story Task 3's Project deletion,
    /// step 1.
    /// </summary>
    public static async Task HoldProjectRowAsync(SqlConnection connection, DbTransaction transaction, Guid projectId, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM dbo.[Project] WITH (UPDLOCK, HOLDLOCK, INDEX(1)) WHERE Id = @project";
        command.Parameters.Add(new SqlParameter("@project", projectId));

        Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The whole Task deletion story Task 3 recommends, played by the test in its open transaction: the Project's Task
    /// range, the Task row, then the row deleted - its <c>TaskLabel</c> rows go by the database's cascade. Left
    /// uncommitted for the caller to commit or roll back.
    /// </summary>
    public static async Task DeleteTaskRowAsync(SqlConnection connection, DbTransaction transaction, Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        await HoldProjectRangeAsync(connection, transaction, projectId, cancellationToken);
        await HoldTaskRowAsync(connection, transaction, taskId, cancellationToken);

        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = "DELETE dbo.[Task] WHERE Id = @task";
        command.Parameters.Add(new SqlParameter("@task", taskId));

        Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
    }

    /// <summary>
    /// The whole Project deletion story Task 3 recommends, played by the test in its open transaction: the Space's
    /// Projects-per-Space lock (story 2.9), the Project row, the Project's Tasks lock, then the row deleted - its Tasks and
    /// their applications go by the database's cascades. Left uncommitted for the caller.
    /// </summary>
    public static async Task DeleteProjectRowAsync(SqlConnection connection, DbTransaction transaction, Guid projectId, CancellationToken cancellationToken)
    {
        await HoldProjectsPerSpaceAsync(connection, transaction, cancellationToken);
        await HoldProjectRowAsync(connection, transaction, projectId, cancellationToken);
        await HoldProjectRangeAsync(connection, transaction, projectId, cancellationToken);

        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = "DELETE dbo.[Project] WHERE Id = @project";
        command.Parameters.Add(new SqlParameter("@project", projectId));

        Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
    }

    /// <summary>
    /// What a Status edit writes once it holds the range and the row: the Task moved to <paramref name="statusId"/> at
    /// <paramref name="positionKey"/>. In the caller's open transaction.
    /// </summary>
    public static async Task MoveTaskRowAsync(SqlConnection connection, DbTransaction transaction, Guid taskId, Guid statusId, string positionKey, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = "UPDATE dbo.[Task] SET StatusId = @status, PositionKey = @key WHERE Id = @task";
        command.Parameters.Add(new SqlParameter("@task", taskId));
        command.Parameters.Add(new SqlParameter("@status", statusId));
        command.Parameters.Add(new SqlParameter("@key", System.Data.SqlDbType.VarChar, 64) { Value = positionKey });

        Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
    }

    /// <summary>
    /// What a creation writes once it has counted under the range and read its Project: one new Task row in
    /// <paramref name="placement"/>'s Space, Project and Status at its key. In the caller's open transaction; see the
    /// class remarks for every constraint it satisfies.
    /// </summary>
    public static async Task InsertTaskAsync(SqlConnection connection, DbTransaction transaction, TaskPlacement placement, CancellationToken cancellationToken)
    {
        var (taskId, spaceId, projectId, statusId, positionKey) = placement;

        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT dbo.[Task] (Id, SpaceId, ProjectId, StatusId, Title, PositionKey, CreatedAt)
            VALUES (@id, @space, @project, @status, N'Created by the test', @key, SYSDATETIMEOFFSET())
            """;
        command.Parameters.Add(new SqlParameter("@id", taskId));
        command.Parameters.Add(new SqlParameter("@space", spaceId));
        command.Parameters.Add(new SqlParameter("@project", projectId));
        command.Parameters.Add(new SqlParameter("@status", statusId));
        command.Parameters.Add(new SqlParameter("@key", System.Data.SqlDbType.VarChar, 64) { Value = positionKey });

        Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
    }

    /// <summary>
    /// Reads the Project row without a lock hint in the caller's open transaction - what a creation does after it has
    /// counted under the range (the read a deletion that skipped the range could invalidate).
    /// </summary>
    public static async Task ReadProjectAsync(SqlConnection connection, DbTransaction transaction, Guid projectId, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM dbo.[Project] WHERE Id = @project";
        command.Parameters.Add(new SqlParameter("@project", projectId));

        Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// Where <see cref="DeleteWire.InsertTaskAsync"/> places a new Task row.
/// </summary>
/// <param name="TaskId">The new Task's id, supplied (<c>ValueGeneratedNever</c>).</param>
/// <param name="SpaceId">The Space whose key the connection holds.</param>
/// <param name="ProjectId">A Project the product created in that Space.</param>
/// <param name="StatusId">One of that Space's Statuses.</param>
/// <param name="PositionKey">A key after the column's last.</param>
internal sealed record TaskPlacement(Guid TaskId, Guid SpaceId, Guid ProjectId, Guid StatusId, string PositionKey);
