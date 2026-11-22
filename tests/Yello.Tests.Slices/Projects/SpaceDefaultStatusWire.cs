using System.Data.Common;
using System.Net;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;
using Yello.Tests.Slices.Tasks;

namespace Yello.Tests.Slices.Projects;

/// <summary>
/// The wire and SQL helpers the Space-level default Status cases share: the three writes on either surface, the Space-level
/// Status lock held by the test, the Space's whole delta read under its key, every Project's set checked for a name held
/// twice, and a pair of test-only triggers that fail a cascading rename between its two statements.
/// </summary>
/// <remarks>
/// <para>
/// <b>Routes and problem types are the contract's</b> (<see cref="SpaceRoutes"/>); the bodies and the problem body's lists
/// are <see cref="SpaceStatusWire"/>'s, read from the raw JSON.
/// </para>
/// <para>
/// <b>Seed constraints.</b> Nothing here inserts a row: every Project, Task, added Status, rename, place and removal in these
/// cases is written by the product, so <c>FK_StatusDeltaOperation_Project_SpaceId_ProjectId</c>,
/// <c>FK_StatusDeltaOperation_StatusDefinition_SpaceId_StatusId</c>, <c>IX_StatusDeltaOperation_SpaceId_ProjectId_StatusId_Kind</c>,
/// the unique <c>(SpaceId, ProjectId, Sequence)</c> index, the filtered default-name index on <c>StatusDefinition</c> and
/// <c>FK_Task_StatusDefinition</c> hold by construction. The SQL only reads (under the Space's own key), takes an application
/// lock, or creates and drops a trigger on a connection with no Space context.
/// </para>
/// </remarks>
internal static class SpaceDefaultStatusWire
{
    public const string Exclusive = "Exclusive";

    public const string Shared = "Shared";

    public static Task<WireResponse> AddAsync(Actor actor, Guid spaceId, string? name, Guid? afterStatusId = null, string? key = null) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Post, SpaceRoutes.StatusesOf(spaceId), SpaceStatusWire.AddBody(name, afterStatusId), TaskWire.KeyHeader(key));

    public static Task<WireResponse> AddWithTokenAsync(Actor actor, string token, Guid spaceId, string name) =>
        actor.Session.SendWithTokenAndHeadersAsync(HttpMethod.Post, SpaceRoutes.StatusesOf(spaceId), token, SpaceStatusWire.AddBody(name), TaskWire.NoHeaders);

    public static Task<WireResponse> RenameAsync(Actor actor, Guid spaceId, Guid statusId, string? name, bool? replaceProjectNames = null, string? key = null) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Patch, SpaceRoutes.StatusOf(spaceId, statusId), SpaceStatusWire.RenameBody(name, replaceProjectNames), TaskWire.KeyHeader(key));

    public static Task<WireResponse> RenameWithTokenAsync(Actor actor, string token, Guid spaceId, Guid statusId, string name) =>
        actor.Session.SendWithTokenAndHeadersAsync(HttpMethod.Patch, SpaceRoutes.StatusOf(spaceId, statusId), token, SpaceStatusWire.RenameBody(name), TaskWire.NoHeaders);

    public static Task<WireResponse> PlaceAsync(Actor actor, Guid spaceId, Guid statusId, Guid? afterStatusId, string? key = null) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Post, SpaceRoutes.StatusPlaceOf(spaceId, statusId), SpaceStatusWire.PlaceBody(afterStatusId), TaskWire.KeyHeader(key));

    public static Task<WireResponse> PlaceWithTokenAsync(Actor actor, string token, Guid spaceId, Guid statusId, Guid? afterStatusId) =>
        actor.Session.SendWithTokenAndHeadersAsync(HttpMethod.Post, SpaceRoutes.StatusPlaceOf(spaceId, statusId), token, SpaceStatusWire.PlaceBody(afterStatusId), TaskWire.NoHeaders);

    public static Task<WireResponse> RawAsync(Actor actor, HttpMethod method, string path, string json) =>
        actor.Session.SendWithHeadersAsync(method, path, TaskEditWire.RawBody(json), TaskWire.NoHeaders);

    public static Task<WireResponse> RemoveAsync(
        Actor actor,
        Guid spaceId,
        Guid statusId,
        Guid? destinationStatusId,
        IReadOnlyList<(Guid ProjectId, Guid DestinationStatusId)>? exceptions = null,
        string? key = null) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Post, SpaceRoutes.StatusRemoveOf(spaceId, statusId), SpaceStatusWire.RemoveBody(destinationStatusId, exceptions), TaskWire.KeyHeader(key));

    public static Task<WireResponse> RemoveWithTokenAsync(Actor actor, string token, Guid spaceId, Guid statusId, Guid? destinationStatusId) =>
        actor.Session.SendWithTokenAndHeadersAsync(HttpMethod.Post, SpaceRoutes.StatusRemoveOf(spaceId, statusId), token, SpaceStatusWire.RemoveBody(destinationStatusId), TaskWire.NoHeaders);

    /// <summary>
    /// Removes a default at Space level through the product, failing the case unless it answers <c>200</c>, and answers the
    /// Space's defaults.
    /// </summary>
    public static async Task<IReadOnlyList<SpaceStatusRow>> RemovedAsync(
        Actor actor,
        Guid spaceId,
        Guid statusId,
        Guid? destinationStatusId = null,
        IReadOnlyList<(Guid ProjectId, Guid DestinationStatusId)>? exceptions = null)
    {
        var removed = await RemoveAsync(actor, spaceId, statusId, destinationStatusId, exceptions);

        Assert.True(removed.StatusCode == HttpStatusCode.OK, $"Removing a Space default through the product: {removed.StatusCode} {removed.Body}");

        return SpaceStatusWire.DefaultsOf(removed.Body);
    }

    /// <summary>
    /// Every delta operation in the Space as (Project, Status, kind, anchor), ordered so two reads compare, under its key.
    /// </summary>
    public static async Task<IReadOnlyList<(Guid ProjectId, Guid StatusId, string Kind, Guid? AfterStatusId)>> SpaceDeltaRowsAsync(RegistrationDatabase database, Guid spaceId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT ProjectId, StatusId, Kind, AfterStatusId FROM dbo.[StatusDeltaOperation] ORDER BY ProjectId, Sequence, Id";

        var rows = new List<(Guid ProjectId, Guid StatusId, string Kind, Guid? AfterStatusId)>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), await reader.IsDBNullAsync(3, TestContext.Current.CancellationToken) ? null : reader.GetGuid(3)));
        }

        return rows;
    }

    /// <summary>
    /// Plants a trigger that fails a Space-level removal at its last statement: <c>AFTER DELETE</c> on <c>StatusDefinition</c>,
    /// throwing when the removed default's row goes. Every Task migration, re-anchored place and op delete has run by then,
    /// so the throw proves the whole request is one transaction. A test-only object in the class's database, always dropped by
    /// the caller in a <c>finally</c> (<see cref="DropTriggersAsync"/>).
    /// </summary>
    /// <returns>The trigger's name.</returns>
    public static async Task<string> PlantFailingDefinitionDeleteTriggerAsync(RegistrationDatabase database, Guid statusId)
    {
        var name = "TR_StatusDefinition_ForcedRemovalFailure_" + Guid.NewGuid().ToString("N");

        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Review SQL queries - DDL takes no parameters; the id is a GUID formatted here.
        command.CommandText =
            $"CREATE TRIGGER dbo.[{name}] ON dbo.[StatusDefinition] AFTER DELETE AS " +
            $"IF EXISTS (SELECT 1 FROM deleted WHERE Id = '{statusId:D}') " +
            "THROW 50000, 'Forced failure at the removed default''s delete.', 1;";
#pragma warning restore CA2100

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        return name;
    }

    /// <summary>
    /// Renames a default at Space level through the product, failing the case unless it answers <c>200</c>, and answers the
    /// Space's defaults.
    /// </summary>
    public static async Task<IReadOnlyList<SpaceStatusRow>> RenamedAsync(Actor actor, Guid spaceId, Guid statusId, string name, bool? replaceProjectNames = null)
    {
        var renamed = await RenameAsync(actor, spaceId, statusId, name, replaceProjectNames);

        Assert.True(renamed.StatusCode == HttpStatusCode.OK, $"Renaming a Space default through the product: {renamed.StatusCode} {renamed.Body}");

        return SpaceStatusWire.DefaultsOf(renamed.Body);
    }

    /// <summary>
    /// Takes the connection's Space's Status-defaults lock in the caller's open transaction, in <paramref name="mode"/>: the
    /// lock a Space-level write takes Exclusive and every Project delta write takes Shared before its Project's lock. The
    /// name is built from the connection's <c>SESSION_CONTEXT</c>, as <see cref="DeleteWire.HoldProjectRangeAsync"/> builds
    /// the Project's, so the holder must be opened for the Space.
    /// </summary>
    public static async Task HoldStatusDefaultsAsync(SqlConnection connection, DbTransaction transaction, string mode, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText =
            """
            IF SESSION_CONTEXT(N'SpaceId') IS NULL THROW 51001, 'The holder is not opened for a Space.', 1;
            DECLARE @resource nvarchar(255) = CONCAT(N'Yello/StatusDefaults/', LOWER(CONVERT(nvarchar(36), CAST(SESSION_CONTEXT(N'SpaceId') AS uniqueidentifier))));
            DECLARE @granted int;
            EXEC @granted = sp_getapplock @Resource = @resource, @LockMode = @mode, @LockOwner = 'Transaction';
            IF @granted < 0 THROW 51002, 'The Status-defaults lock was not granted to the holder.', 1;
            """;
        command.Parameters.Add(new SqlParameter("@mode", System.Data.SqlDbType.NVarChar, 32) { Value = mode });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// How many delta operations the whole Space holds, every Project together, read under its key.
    /// </summary>
    public static async Task<int> SpaceDeltaCountAsync(RegistrationDatabase database, Guid spaceId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT COUNT(*) FROM dbo.[StatusDeltaOperation]";

        return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Every <c>Rename</c> operation in the Space as (Project, Status, name), ordered so two reads compare, under its key.
    /// </summary>
    public static async Task<IReadOnlyList<(Guid ProjectId, Guid StatusId, string Name)>> RenameOperationsAsync(RegistrationDatabase database, Guid spaceId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT ProjectId, StatusId, Name FROM dbo.[StatusDeltaOperation] WHERE Kind = 'Rename' ORDER BY ProjectId, StatusId";

        var rows = new List<(Guid ProjectId, Guid StatusId, string Name)>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add((reader.GetGuid(0), reader.GetGuid(1), await reader.IsDBNullAsync(2, TestContext.Current.CancellationToken) ? string.Empty : reader.GetString(2)));
        }

        return rows;
    }

    /// <summary>
    /// No Project's effective set holds one name twice, compared ignoring case, and each set is non-empty (the anchor).
    /// </summary>
    public static async Task AssertNoSetHoldsANameTwiceAsync(Actor actor, Guid spaceId, IEnumerable<Guid> projectIds)
    {
        foreach (var projectId in projectIds)
        {
            var names = (await ProjectStatusSliceWire.EffectiveAsync(actor, spaceId, projectId)).Select(entry => entry.Name).ToList();

            Assert.NotEmpty(names);
            Assert.True(
                names.Distinct(StringComparer.OrdinalIgnoreCase).Count() == names.Count,
                $"Project {projectId} holds a name twice: {string.Join(", ", names)}");
        }
    }

    /// <summary>
    /// Plants two triggers that fail a cascading rename of <paramref name="statusId"/> at whichever of its two statements
    /// runs second, so the throw lands after the first has run whatever order the store chooses: one on
    /// <c>StatusDefinition</c> that throws when the default's row changes once <paramref name="projectId"/>'s <c>Rename</c> of it
    /// is gone, and one on <c>StatusDeltaOperation</c> that throws when that op is deleted once the default already carries
    /// <paramref name="newName"/>. Each reads inside the statement's own transaction, so it sees the other statement's
    /// uncommitted work. Test-only objects in the class's database, always dropped by the caller in a <c>finally</c>.
    /// </summary>
    /// <returns>The two trigger names, for <see cref="DropTriggersAsync"/>.</returns>
    public static async Task<string[]> PlantFailingCascadeTriggersAsync(RegistrationDatabase database, Guid projectId, Guid statusId, string newName)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var onDefinition = "TR_StatusDefinition_ForcedFailure_" + suffix;
        var onDelta = "TR_StatusDeltaOperation_ForcedFailure_" + suffix;
        var quotedName = newName.Replace("'", "''", StringComparison.Ordinal);

        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);

        foreach (var statement in new[]
        {
            $"CREATE TRIGGER dbo.[{onDefinition}] ON dbo.[StatusDefinition] AFTER UPDATE AS " +
            $"IF EXISTS (SELECT 1 FROM inserted WHERE Id = '{statusId:D}') " +
            $"AND NOT EXISTS (SELECT 1 FROM dbo.[StatusDeltaOperation] WHERE ProjectId = '{projectId:D}' AND StatusId = '{statusId:D}' AND Kind = 'Rename') " +
            "THROW 50000, 'Forced failure after the cascade deleted its operations.', 1;",

            $"CREATE TRIGGER dbo.[{onDelta}] ON dbo.[StatusDeltaOperation] AFTER DELETE AS " +
            $"IF EXISTS (SELECT 1 FROM deleted WHERE ProjectId = '{projectId:D}' AND StatusId = '{statusId:D}' AND Kind = 'Rename') " +
            $"AND EXISTS (SELECT 1 FROM dbo.[StatusDefinition] WHERE Id = '{statusId:D}' AND Name = N'{quotedName}') " +
            "THROW 50000, 'Forced failure after the cascade renamed the default.', 1;",
        })
        {
            await using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Review SQL queries - DDL takes no parameters; the ids are GUIDs formatted here and the name is a test literal, quoted.
            command.CommandText = statement;
#pragma warning restore CA2100

            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        return [onDefinition, onDelta];
    }

    public static async Task DropTriggersAsync(RegistrationDatabase database, IEnumerable<string> names)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);

        foreach (var name in names)
        {
            await using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Review SQL queries - each name is one PlantFailingCascadeTriggersAsync built from a GUID.
            command.CommandText = $"DROP TRIGGER IF EXISTS dbo.[{name}];";
#pragma warning restore CA2100

            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }
}
