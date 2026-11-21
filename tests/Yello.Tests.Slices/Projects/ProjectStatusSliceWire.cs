using System.Net;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts.Boards;
using Yello.Contracts.Projects;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;
using Yello.Tests.Slices.Tasks;

namespace Yello.Tests.Slices.Projects;

/// <summary>
/// The wire and SQL helpers the Project Status delta cases share: the read and the three writes on either surface, the
/// Board's columns as (id, name), and the rows behind them read under the Space's own key.
/// </summary>
/// <remarks>
/// <para>
/// <b>Routes and problem types are <see cref="ProjectStatusWire"/>'s stand-ins</b> until the contract members exist.
/// </para>
/// <para>
/// <b>The SQL names the schema the story recommends</b>: <c>StatusDefinition.OwnerProjectId</c> (nullable; <c>NULL</c> is a
/// Space default) and the delta table <c>dbo.StatusDeltaOperation</c> keyed by <c>(SpaceId, ProjectId, StatusId, Kind)</c>.
/// A different name in the migration is corrected in this one file and recorded.
/// </para>
/// <para>
/// <b>Seed constraints checked</b> against <c>20260828164716_InitialSchema</c> (<c>StatusDefinition</c>, three per Space at
/// registration; <c>AK_StatusDefinition_SpaceId_Id</c>), <c>20261025094743_Projects</c> (<c>AK_Project_SpaceId_Id</c>) and
/// the Task migrations (<c>FK_Task_StatusDefinition</c> on <c>(SpaceId, StatusId)</c>, NO ACTION). Nothing is seeded by SQL:
/// every Project, Task and Status in these cases is written by the product, so the composite keys hold by construction
/// - a Task's Status is always one the product admitted for its Project.
/// </para>
/// </remarks>
internal static class ProjectStatusSliceWire
{
    public static Task<WireResponse> ReadAsync(Actor actor, Guid spaceId, Guid projectId) =>
        actor.Session.GetAsync(ProjectStatusRoutes.CollectionOf(spaceId, projectId));

    public static Task<WireResponse> ReadWithTokenAsync(Actor actor, string token, Guid spaceId, Guid projectId) =>
        actor.Session.GetWithTokenAsync(ProjectStatusRoutes.CollectionOf(spaceId, projectId), token);

    public static Task<WireResponse> AddAsync(Actor actor, Guid spaceId, Guid projectId, string? name, string? key = null) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Post, ProjectStatusRoutes.CollectionOf(spaceId, projectId), ProjectStatusWire.NameBody(name), TaskWire.KeyHeader(key));

    public static Task<WireResponse> AddWithTokenAsync(Actor actor, string token, Guid spaceId, Guid projectId, string name) =>
        actor.Session.SendWithTokenAndHeadersAsync(HttpMethod.Post, ProjectStatusRoutes.CollectionOf(spaceId, projectId), token, ProjectStatusWire.NameBody(name), TaskWire.NoHeaders);

    public static Task<WireResponse> AddAfterAsync(Actor actor, Guid spaceId, Guid projectId, string name, Guid afterStatusId) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Post, ProjectStatusRoutes.CollectionOf(spaceId, projectId), ProjectStatusWire.NameAfterBody(name, afterStatusId), TaskWire.NoHeaders);

    public static Task<WireResponse> RenameAsync(Actor actor, Guid spaceId, Guid projectId, Guid statusId, string? name) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Patch, ProjectStatusRoutes.ItemOf(spaceId, projectId, statusId), ProjectStatusWire.NameBody(name), TaskWire.NoHeaders);

    public static Task<WireResponse> RenameWithTokenAsync(Actor actor, string token, Guid spaceId, Guid projectId, Guid statusId, string name) =>
        actor.Session.SendWithTokenAndHeadersAsync(HttpMethod.Patch, ProjectStatusRoutes.ItemOf(spaceId, projectId, statusId), token, ProjectStatusWire.NameBody(name), TaskWire.NoHeaders);

    public static Task<WireResponse> PlaceAsync(Actor actor, Guid spaceId, Guid projectId, Guid statusId, Guid? afterStatusId) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Post, ProjectStatusRoutes.PlaceOf(spaceId, projectId, statusId), ProjectStatusWire.PlaceBody(afterStatusId), TaskWire.NoHeaders);

    public static Task<WireResponse> PlaceWithTokenAsync(Actor actor, string token, Guid spaceId, Guid projectId, Guid statusId, Guid? afterStatusId) =>
        actor.Session.SendWithTokenAndHeadersAsync(HttpMethod.Post, ProjectStatusRoutes.PlaceOf(spaceId, projectId, statusId), token, ProjectStatusWire.PlaceBody(afterStatusId), TaskWire.NoHeaders);

    /// <summary>
    /// Adds <paramref name="name"/> to a Project through the product, failing the case unless it is admitted, and answers the
    /// new Status's id - the one <c>added-here</c> entry carrying that name.
    /// </summary>
    public static async Task<Guid> AddedAsync(Actor actor, Guid spaceId, Guid projectId, string name)
    {
        var added = await AddAsync(actor, spaceId, projectId, name);

        Assert.True(added.StatusCode == HttpStatusCode.Created, $"Adding a Status through the product: {added.StatusCode} {added.Body}");

        return ProjectStatusWire.EntriesOf(added.Body).Single(entry => entry.Name == name && entry.Source == ProjectStatusSources.AddedHere).Id;
    }

    /// <summary>
    /// The Project's effective set as the product reads it, failing the case unless the read is a <c>200</c>.
    /// </summary>
    public static async Task<IReadOnlyList<ProjectStatusEntry>> EffectiveAsync(Actor actor, Guid spaceId, Guid projectId)
    {
        var read = await ReadAsync(actor, spaceId, projectId);

        Assert.True(read.StatusCode == HttpStatusCode.OK, $"Reading a Project's Statuses: {read.StatusCode} {read.Body}");

        return ProjectStatusWire.EntriesOf(read.Body);
    }

    /// <summary>
    /// The Board's columns as (Status id, name), in the order served, failing the case unless the Board is a <c>200</c>.
    /// </summary>
    public static async Task<IReadOnlyList<(Guid StatusId, string Name)>> BoardColumnsAsync(Actor actor, Guid spaceId, Guid projectId)
    {
        var board = await actor.Session.GetAsync(BoardRoutes.BoardOf(spaceId, projectId));

        Assert.True(board.StatusCode == HttpStatusCode.OK, $"Reading a Board: {board.StatusCode} {board.Body}");

        using var document = JsonDocument.Parse(board.Body);

        return
        [
            .. document.RootElement.GetProperty("columns").EnumerateArray()
                .Select(column => (column.GetProperty("statusId").GetGuid(), column.GetProperty("name").GetString() ?? string.Empty)),
        ];
    }

    /// <summary>
    /// The Space's own Status read (<c>GET …/statuses</c>) as (id, name), in the order served.
    /// </summary>
    public static async Task<IReadOnlyList<(Guid Id, string Name)>> SpaceReadAsync(Actor actor, Guid spaceId)
    {
        var read = await actor.Session.GetAsync(SpaceRoutes.StatusesOf(spaceId));

        Assert.True(read.StatusCode == HttpStatusCode.OK, $"Reading the Space's Statuses: {read.StatusCode} {read.Body}");

        using var document = JsonDocument.Parse(read.Body);

        return [.. document.RootElement.EnumerateArray().Select(row => (row.GetProperty("id").GetGuid(), row.GetProperty("name").GetString() ?? string.Empty))];
    }

    /// <summary>
    /// The Space's default Statuses (no owning Project) in Space order - <c>Position</c>, then <c>Id</c> - read under its key.
    /// </summary>
    public static async Task<IReadOnlyList<StatusRow>> DefaultsAsync(RegistrationDatabase database, Guid spaceId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Id, Name, Position FROM dbo.[StatusDefinition] WHERE OwnerProjectId IS NULL ORDER BY Position, Id";

        var rows = new List<StatusRow>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add(new StatusRow(reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2)));
        }

        return rows;
    }

    /// <summary>
    /// Every Status definition in the Space as (id, name, owning Project), read under its key.
    /// </summary>
    public static async Task<IReadOnlyList<(Guid Id, string Name, Guid? OwnerProjectId)>> DefinitionsAsync(RegistrationDatabase database, Guid spaceId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Id, Name, OwnerProjectId FROM dbo.[StatusDefinition] ORDER BY Position, Id";

        var rows = new List<(Guid Id, string Name, Guid? OwnerProjectId)>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add((reader.GetGuid(0), reader.GetString(1), await reader.IsDBNullAsync(2, TestContext.Current.CancellationToken) ? null : reader.GetGuid(2)));
        }

        return rows;
    }

    /// <summary>
    /// How many delta operations one Project holds, read under the Space's key.
    /// </summary>
    public static async Task<int> DeltaCountAsync(RegistrationDatabase database, Guid spaceId, Guid projectId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT COUNT(*) FROM dbo.[StatusDeltaOperation] WHERE ProjectId = @project";
        command.Parameters.Add(new SqlParameter("@project", projectId));

        return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The Space's Tasks whose Status is outside their own Project's effective set - the Status half of S-2, by the one
    /// statement <see cref="WireDatabase.TasksOutsideTheirProjectsSetSql"/> states.
    /// </summary>
    public static async Task<int> TasksOutsideTheirProjectsSetAsync(RegistrationDatabase database, Guid spaceId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Review SQL queries - a fixed statement; no input reaches it.
        command.CommandText = WireDatabase.TasksOutsideTheirProjectsSetSql;
#pragma warning restore CA2100

        return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// S-2 after a case's writes: no Task in the Space holds a Status outside its own Project's set. Anchored by the caller on
    /// the Space holding Tasks at all.
    /// </summary>
    public static async Task AssertNoTaskOutsideItsProjectsSetAsync(RegistrationDatabase database, Guid spaceId) =>
        Assert.Equal(0, await TasksOutsideTheirProjectsSetAsync(database, spaceId));
}
