using System.Data;
using System.Data.Common;
using System.Net;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts.Tasks;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// The wire and SQL helpers for moving a Task into another Project: the request on either surface, two Projects seeded through
/// the product, the move itself played by the test in its own open transaction, the Task columns a move must leave alone, and
/// a test-only trigger that fails the move's row write.
/// </summary>
/// <remarks>
/// <para>
/// <b>The played move is the product's statement shape</b>: both Projects' Tasks locks in the ordinal order of their lock
/// names (<c>Yello/ProjectTasks/{spaceId:D}/{projectId:D}</c>, the order <c>ApplicationLocks.TakeProjectTasksAsync</c> takes a
/// collection in), then the Task row on its clustered key, then one <c>UPDATE</c> of <c>ProjectId</c>, <c>StatusId</c> and
/// <c>PositionKey</c>. A writer that queues behind it meets the same locks the product's move will take.
/// </para>
/// <para>
/// <b>Seed constraints checked</b> against <c>TaskConfiguration</c> and the Task migrations: the played <c>UPDATE</c> keeps
/// <c>SpaceId</c> (the <c>BLOCK AFTER UPDATE</c> predicate refuses a change of it) and runs under the Space's own session key;
/// the destination is a Project the product created in the same Space, so <c>FK_Task_Project_SpaceId_ProjectId</c>
/// <c>(SpaceId, ProjectId)</c> holds; the Status is one the destination's set holds, read through the product, and a
/// <c>StatusDefinition</c> of the same Space, so the <c>(SpaceId, StatusId)</c> key holds; and the key is the destination
/// column's last key extended by one alphabet character (or <c>V0000</c> in an empty column), read under the destination's
/// lock, so the unique <c>(SpaceId, ProjectId, StatusId, PositionKey)</c> index holds and the key is at most
/// <c>varchar(128)</c>.
/// </para>
/// </remarks>
internal static class ProjectMoveSliceWire
{
    public const string EmptyColumnKey = "V0000";

    public static Task<WireResponse> MoveToProjectAsync(Actor actor, Guid spaceId, Guid taskId, HttpContent body, string? key = null) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Post, TaskRoutes.ProjectMoveOf(spaceId, taskId), body, TaskWire.KeyHeader(key));

    public static Task<WireResponse> MoveToProjectWithTokenAsync(Actor actor, string token, Guid spaceId, Guid taskId, HttpContent body, string? key = null) =>
        actor.Session.SendWithTokenAndHeadersAsync(HttpMethod.Post, TaskRoutes.ProjectMoveOf(spaceId, taskId), token, body, TaskWire.KeyHeader(key));

    public static Task<WireResponse> MoveToProjectOnAsync(IsolationSurface surface, Actor actor, string token, Guid taskId, HttpContent body, string? key = null) =>
        surface == IsolationSurface.ApiToken
            ? MoveToProjectWithTokenAsync(actor, token, actor.SpaceId, taskId, body, key)
            : MoveToProjectAsync(actor, actor.SpaceId, taskId, body, key);

    /// <summary>
    /// A source and a destination Project created through the product, each holding Tasks created through the product in the
    /// Space's first Status, in the order given.
    /// </summary>
    public static async Task<TwoProjects> TwoProjectsAsync(Actor owner, string[] inSource, string[] inDestination)
    {
        var source = await TaskWire.CreateProjectAsync(owner, "Source " + Guid.NewGuid().ToString("N")[..8]);
        var destination = await TaskWire.CreateProjectAsync(owner, "Destination " + Guid.NewGuid().ToString("N")[..8]);
        var sourceTasks = new List<Guid>();
        var destinationTasks = new List<Guid>();

        foreach (var title in inSource)
        {
            sourceTasks.Add(TaskWire.IdOf(await TaskWire.CreateTaskAsync(owner, owner.SpaceId, source, title)));
        }

        foreach (var title in inDestination)
        {
            destinationTasks.Add(TaskWire.IdOf(await TaskWire.CreateTaskAsync(owner, owner.SpaceId, destination, title)));
        }

        return new TwoProjects(source, destination, sourceTasks, destinationTasks);
    }

    /// <summary>
    /// The lock names of <paramref name="projectIds"/> in the order the product takes several: ordinal.
    /// </summary>
    public static IReadOnlyList<Guid> InLockOrder(Guid spaceId, params Guid[] projectIds) =>
        [.. projectIds.Distinct().OrderBy(projectId => $"Yello/ProjectTasks/{spaceId:D}/{projectId:D}", StringComparer.Ordinal)];

    /// <summary>
    /// Takes the Tasks locks of <paramref name="projectIds"/> in the caller's open transaction, in the product's order.
    /// </summary>
    public static async Task HoldProjectsAsync(SqlConnection connection, DbTransaction transaction, Guid spaceId, IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken)
    {
        foreach (var projectId in InLockOrder(spaceId, [.. projectIds]))
        {
            await DeleteWire.HoldProjectRangeAsync(connection, transaction, projectId, cancellationToken);
        }
    }

    /// <summary>
    /// The whole cross-Project move, played by the test and left uncommitted: both Projects' locks in order, the Task row,
    /// then the row reparented to the end of the move's Status column in its destination.
    /// </summary>
    /// <returns>The key the played move wrote.</returns>
    public static async Task<string> HoldProjectMoveAsync(SqlConnection connection, DbTransaction transaction, PlayedMove move, CancellationToken cancellationToken)
    {
        await HoldProjectsAsync(connection, transaction, move.SpaceId, [move.SourceId, move.DestinationId], cancellationToken);
        await DeleteWire.HoldTaskRowAsync(connection, transaction, move.TaskId, cancellationToken);

        var key = await KeyAfterLastAsync(connection, transaction, move.DestinationId, move.StatusId, cancellationToken);

        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = "UPDATE dbo.[Task] SET ProjectId = @project, StatusId = @status, PositionKey = @key WHERE Id = @task";
        command.Parameters.Add(new SqlParameter("@task", move.TaskId));
        command.Parameters.Add(new SqlParameter("@project", move.DestinationId));
        command.Parameters.Add(new SqlParameter("@status", move.StatusId));
        command.Parameters.Add(new SqlParameter("@key", SqlDbType.VarChar, 128) { Value = key });

        Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));

        return key;
    }

    /// <summary>
    /// The columns of one Task a cross-Project move must never write, read under the Space's key.
    /// </summary>
    public static async Task<TaskSurvivors> SurvivorsAsync(RegistrationDatabase database, Guid spaceId, Guid taskId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Id, SpaceId, Title, DueDate, AssigneeMembershipId, FirstLabelName, CreatedAt FROM dbo.[Task] WHERE Id = @task";
        command.Parameters.Add(new SqlParameter("@task", taskId));

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken), $"No Task {taskId} in the Space.");

        return new TaskSurvivors(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            await reader.IsDBNullAsync(3, TestContext.Current.CancellationToken) ? null : await reader.GetFieldValueAsync<DateTimeOffset>(3, TestContext.Current.CancellationToken),
            await reader.IsDBNullAsync(4, TestContext.Current.CancellationToken) ? null : reader.GetGuid(4),
            await reader.IsDBNullAsync(5, TestContext.Current.CancellationToken) ? null : reader.GetString(5),
            await reader.GetFieldValueAsync<DateTimeOffset>(6, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Plants an <c>AFTER UPDATE</c> trigger on <c>Task</c> that throws for one Task: the move's single row write fails after
    /// it ran, inside the move's transaction. A test-only object, always dropped by the caller in a <c>finally</c> through
    /// <c>StatusRemovalWire.DropTriggerAsync</c>.
    /// </summary>
    /// <returns>The trigger's name.</returns>
    public static async Task<string> PlantFailingTaskUpdateTriggerAsync(RegistrationDatabase database, Guid taskId)
    {
        var name = "TR_Task_ForcedFailure_" + Guid.NewGuid().ToString("N");

        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Review SQL queries - DDL takes no parameters; both values are GUIDs formatted here.
        command.CommandText =
            $"CREATE TRIGGER dbo.[{name}] ON dbo.[Task] AFTER UPDATE AS " +
            $"IF EXISTS (SELECT 1 FROM inserted WHERE Id = '{taskId:D}') THROW 50000, 'Forced failure after the Task row write.', 1;";
#pragma warning restore CA2100

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        return name;
    }

    /// <summary>
    /// Asserts a <c>200</c> whose body states the destination Project.
    /// </summary>
    public static void AssertMovedInto(WireResponse? answer, Guid destinationId)
    {
        Assert.NotNull(answer);
        Assert.True(answer.StatusCode == HttpStatusCode.OK, $"{answer.StatusCode} {answer.Body}");
        Assert.Equal(destinationId, ProjectMoveWire.ProjectIdIn(answer.Body));
    }

    private static async Task<string> KeyAfterLastAsync(SqlConnection connection, DbTransaction transaction, Guid projectId, Guid statusId, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = "SELECT MAX(PositionKey) FROM dbo.[Task] WHERE ProjectId = @project AND StatusId = @status";
        command.Parameters.Add(new SqlParameter("@project", projectId));
        command.Parameters.Add(new SqlParameter("@status", statusId));

        return await command.ExecuteScalarAsync(cancellationToken) is string last ? last + "1" : EmptyColumnKey;
    }
}

/// <summary>
/// Two Projects of one Space and the Tasks the product created in each, in creation order.
/// </summary>
/// <param name="SourceId">The Project a case moves out of.</param>
/// <param name="DestinationId">The Project a case moves into.</param>
/// <param name="InSource">The source's Tasks.</param>
/// <param name="InDestination">The destination's Tasks.</param>
internal sealed record TwoProjects(Guid SourceId, Guid DestinationId, IReadOnlyList<Guid> InSource, IReadOnlyList<Guid> InDestination);

/// <summary>
/// A cross-Project move the test plays in SQL.
/// </summary>
/// <param name="SpaceId">The Space the holder's connection is opened for.</param>
/// <param name="SourceId">The Task's Project now.</param>
/// <param name="DestinationId">Its Project after the move.</param>
/// <param name="TaskId">The Task.</param>
/// <param name="StatusId">Its Status after the move, one the destination's set holds.</param>
internal sealed record PlayedMove(Guid SpaceId, Guid SourceId, Guid DestinationId, Guid TaskId, Guid StatusId);

/// <summary>
/// What a cross-Project move leaves alone on a Task's row.
/// </summary>
/// <param name="Id">The Task's id.</param>
/// <param name="SpaceId">Its Space.</param>
/// <param name="Title">Its title.</param>
/// <param name="DueDate">Its due date.</param>
/// <param name="AssigneeMembershipId">Its Assignee.</param>
/// <param name="FirstLabelName">Its Label sort key.</param>
/// <param name="CreatedAt">When it was created.</param>
internal sealed record TaskSurvivors(Guid Id, Guid SpaceId, string Title, DateTimeOffset? DueDate, Guid? AssigneeMembershipId, string? FirstLabelName, DateTimeOffset CreatedAt);
