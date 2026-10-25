using System.Net.Http.Json;
using System.Text;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts.Tasks;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// The wire and SQL helpers story 2.3's edit cases add beside <see cref="TaskWire"/>: read and edit one Task
/// over HTTP on either surface, and read its due date and Status position under the Space's own key.
/// Written by the story 2.3 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bodies are JSON text or anonymous objects</b>, never a contract type, so a case can send what Q1 (a)
/// distinguishes: an absent member (unchanged), a member set to <c>null</c> (<c>dueDate</c> cleared, <c>title</c>
/// or <c>statusId</c> refused) and a member set to a value. <c>JsonSerializerDefaults.Web</c> writes a
/// <c>null</c> member rather than dropping it.
/// </para>
/// <para>
/// <b><see cref="DueDateAsync"/> reads a column that does not exist yet</b> (story 2.3 Task 3:
/// <c>DueDate datetimeoffset NULL</c>). It is called only by skipped cases; <see cref="TaskWire.TaskRowsAsync"/>
/// is left as it is so 2.2's active cases keep running.
/// </para>
/// </remarks>
internal static class TaskEditWire
{
    public static JsonContent Body(object members) => JsonContent.Create(members, options: TaskWire.JsonOptions);

    public static StringContent RawBody(string json) => new(json, Encoding.UTF8, "application/json");

    public static Task<WireResponse> EditTaskAsync(Actor actor, Guid spaceId, Guid taskId, HttpContent body, string? key = null) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Patch, TaskRoutes.TaskOf(spaceId, taskId), body, TaskWire.KeyHeader(key));

    public static Task<WireResponse> EditTaskWithTokenAsync(Actor actor, string token, Guid spaceId, Guid taskId, HttpContent body, string? key = null) =>
        actor.Session.SendWithTokenAndHeadersAsync(HttpMethod.Patch, TaskRoutes.TaskOf(spaceId, taskId), token, body, TaskWire.KeyHeader(key));

    public static Task<WireResponse> EditTaskOnAsync(IsolationSurface surface, Actor actor, string token, Guid taskId, HttpContent body, string? key = null) =>
        surface == IsolationSurface.ApiToken
            ? EditTaskWithTokenAsync(actor, token, actor.SpaceId, taskId, body, key)
            : EditTaskAsync(actor, actor.SpaceId, taskId, body, key);

    public static Task<WireResponse> ViewTaskAsync(Actor actor, Guid spaceId, Guid taskId) =>
        actor.Session.GetAsync(TaskRoutes.TaskOf(spaceId, taskId));

    public static Task<WireResponse> ViewTaskWithTokenAsync(Actor actor, string token, Guid spaceId, Guid taskId) =>
        actor.Session.GetWithTokenAsync(TaskRoutes.TaskOf(spaceId, taskId), token);

    /// <summary>
    /// One Project with one Task in it, created through the product by <paramref name="owner"/>.
    /// </summary>
    public static async Task<(Guid ProjectId, Guid TaskId)> ProjectWithTaskAsync(Actor owner, string title)
    {
        var projectId = await TaskWire.CreateProjectAsync(owner, "Project " + Guid.NewGuid().ToString("N")[..8]);
        var taskId = TaskWire.IdOf(await TaskWire.CreateTaskAsync(owner, owner.SpaceId, projectId, title));

        return (projectId, taskId);
    }

    /// <summary>
    /// One Task's row, read under the Space's own key - or <c>null</c> when the Space holds no such Task.
    /// </summary>
    public static async Task<TaskRow?> RowAsync(RegistrationDatabase database, Guid spaceId, Guid taskId) =>
        (await TaskWire.TaskRowsAsync(database, spaceId)).SingleOrDefault(row => row.Id == taskId);

    /// <summary>
    /// One Task's stored due date, under the Space's own key: <c>null</c> for <c>NULL</c>. Reads story 2.3 Task 3's
    /// column.
    /// </summary>
    public static async Task<DateTimeOffset?> DueDateAsync(RegistrationDatabase database, Guid spaceId, Guid taskId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT DueDate FROM dbo.[Task] WHERE Id = @id";
        command.Parameters.Add(new SqlParameter("@id", taskId));

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken), $"No Task {taskId} in the Space.");

        return await reader.IsDBNullAsync(0, TestContext.Current.CancellationToken)
            ? null
            : await reader.GetFieldValueAsync<DateTimeOffset>(0, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The Space's Tasks whose Status is not one of their own Space's Statuses - AD-17's invariant as a query
    /// (story 2.2 SCH-006's shape; until 6.1's deltas every Project's effective set is the Space's defaults).
    /// </summary>
    public static async Task<int> TasksOutsideTheirEffectiveSetAsync(RegistrationDatabase database, Guid spaceId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT COUNT(*) FROM dbo.[Task] t
            WHERE NOT EXISTS (SELECT 1 FROM dbo.[StatusDefinition] s WHERE s.SpaceId = t.SpaceId AND s.Id = t.StatusId)
            """;

        return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }
}
