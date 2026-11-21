using System.Data.Common;
using System.Net;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts.Projects;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;
using Yello.Tests.Slices.Tasks;

namespace Yello.Tests.Slices.Projects;

/// <summary>
/// The wire and SQL helpers the Status removal cases share: the remove and add-back requests on either surface, the delta
/// rows read under the Space's key, the removal's delta write played by the test inside its own open transaction, and a
/// test-only trigger that fails the removal after its Task migration.
/// </summary>
/// <remarks>
/// <para>
/// <b>Routes and problem types are the contract's</b> (<c>POST …/statuses/{id}/remove</c> with <c>{ destinationStatusId }</c>,
/// <c>POST …/statuses/{id}/restore</c> with no body).
/// </para>
/// <para>
/// <b>The played removal is a <c>Remove</c> row</b> in <c>dbo.StatusDeltaOperation</c>, written after the Project's lock
/// (<see cref="DeleteWire.HoldProjectRangeAsync"/>), exactly what the product writes for an unoccupied Space default. Seed
/// constraints checked against <c>20261121155445_ProjectStatusDelta</c>: every non-nullable column is supplied (<c>Id</c>,
/// <c>SpaceId</c>, <c>ProjectId</c>, <c>StatusId</c>, <c>Kind</c> <c>varchar(16)</c>, <c>Sequence</c>, <c>CreatedAt</c>);
/// <c>FK_StatusDeltaOperation_Project_SpaceId_ProjectId</c> names a Project the product created in that Space;
/// <c>FK_StatusDeltaOperation_StatusDefinition_SpaceId_StatusId</c> names one of that Space's own defaults;
/// <c>IX_StatusDeltaOperation_SpaceId_ProjectId_StatusId_Kind</c> holds because a default is removed once; the Sequence is
/// the Project's next, so a unique <c>(SpaceId, ProjectId, Sequence)</c> index would hold too; and the row is written under
/// the Space's own session key, which the policy's <c>BLOCK AFTER INSERT</c> predicate requires. No <c>CHECK</c> constrains
/// <c>Kind</c>, so the row is writable before the product knows the kind.
/// </para>
/// </remarks>
internal static class StatusRemovalWire
{
    public const string RemoveKind = "Remove";

    public static Task<WireResponse> RemoveAsync(Actor actor, Guid spaceId, Guid projectId, Guid statusId, Guid? destinationStatusId, string? key = null) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Post, ProjectStatusRoutes.RemoveOf(spaceId, projectId, statusId), ProjectStatusWire.RemoveBody(destinationStatusId), TaskWire.KeyHeader(key));

    public static Task<WireResponse> RemoveWithTokenAsync(Actor actor, string token, Guid spaceId, Guid projectId, Guid statusId, Guid? destinationStatusId) =>
        actor.Session.SendWithTokenAndHeadersAsync(HttpMethod.Post, ProjectStatusRoutes.RemoveOf(spaceId, projectId, statusId), token, ProjectStatusWire.RemoveBody(destinationStatusId), TaskWire.NoHeaders);

    public static Task<WireResponse> RemoveRawAsync(Actor actor, Guid spaceId, Guid projectId, Guid statusId, string json) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Post, ProjectStatusRoutes.RemoveOf(spaceId, projectId, statusId), TaskEditWire.RawBody(json), TaskWire.NoHeaders);

    public static Task<WireResponse> RestoreAsync(Actor actor, Guid spaceId, Guid projectId, Guid statusId, string? key = null) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Post, ProjectStatusRoutes.RestoreOf(spaceId, projectId, statusId), content: null, TaskWire.KeyHeader(key));

    public static Task<WireResponse> RestoreWithTokenAsync(Actor actor, string token, Guid spaceId, Guid projectId, Guid statusId) =>
        actor.Session.SendWithTokenAndHeadersAsync(HttpMethod.Post, ProjectStatusRoutes.RestoreOf(spaceId, projectId, statusId), token, content: null, TaskWire.NoHeaders);

    /// <summary>
    /// Removes a Status through the product, failing the case unless the removal answers <c>200</c>, and answers the set.
    /// </summary>
    public static async Task<IReadOnlyList<ProjectStatusEntry>> RemovedAsync(Actor actor, Guid spaceId, Guid projectId, Guid statusId, Guid? destinationStatusId = null)
    {
        var removed = await RemoveAsync(actor, spaceId, projectId, statusId, destinationStatusId);

        Assert.True(removed.StatusCode == HttpStatusCode.OK, $"Removing a Status through the product: {removed.StatusCode} {removed.Body}");

        return ProjectStatusWire.EntriesOf(removed.Body);
    }

    /// <summary>
    /// One Project's delta as (Status, kind, anchor, sequence), ascending by sequence, read under the Space's key.
    /// </summary>
    public static async Task<IReadOnlyList<DeltaRow>> DeltaRowsAsync(RegistrationDatabase database, Guid spaceId, Guid projectId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT StatusId, Kind, AfterStatusId, Sequence FROM dbo.[StatusDeltaOperation] WHERE ProjectId = @project ORDER BY Sequence, Id";
        command.Parameters.Add(new SqlParameter("@project", projectId));

        var rows = new List<DeltaRow>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add(new DeltaRow(
                reader.GetGuid(0),
                reader.GetString(1),
                await reader.IsDBNullAsync(2, TestContext.Current.CancellationToken) ? null : reader.GetGuid(2),
                reader.GetInt32(3)));
        }

        return rows;
    }

    /// <summary>
    /// Writes a <see cref="RemoveKind"/> row for <paramref name="statusId"/> in the caller's open transaction - the removal of
    /// an unoccupied Space default, as the product writes it. See the class remarks for every constraint it satisfies.
    /// </summary>
    public static async Task InsertRemoveOperationAsync(SqlConnection connection, DbTransaction transaction, Guid spaceId, Guid projectId, Guid statusId, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT dbo.[StatusDeltaOperation] (Id, SpaceId, ProjectId, StatusId, Kind, Name, AfterStatusId, Sequence, CreatedAt)
            SELECT NEWID(), @space, @project, @status, 'Remove', NULL, NULL,
                   ISNULL((SELECT MAX(o.Sequence) FROM dbo.[StatusDeltaOperation] o WHERE o.ProjectId = @project), 0) + 1,
                   SYSDATETIMEOFFSET()
            """;
        command.Parameters.Add(new SqlParameter("@space", spaceId));
        command.Parameters.Add(new SqlParameter("@project", projectId));
        command.Parameters.Add(new SqlParameter("@status", statusId));

        Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
    }

    /// <summary>
    /// The whole removal of an unoccupied Space default, played by the test and left uncommitted: the Project's lock, then
    /// the <see cref="RemoveKind"/> row. A Task writer that queues behind it and reads the set only after the lock sees the
    /// removal once this commits.
    /// </summary>
    public static async Task HoldRemovalAsync(SqlConnection connection, DbTransaction transaction, Guid spaceId, Guid projectId, Guid statusId, CancellationToken cancellationToken)
    {
        await DeleteWire.HoldProjectRangeAsync(connection, transaction, projectId, cancellationToken);
        await InsertRemoveOperationAsync(connection, transaction, spaceId, projectId, statusId, cancellationToken);
    }

    /// <summary>
    /// Plants an <c>AFTER INSERT, UPDATE, DELETE</c> trigger on <c>StatusDeltaOperation</c> that throws for one Project: the
    /// removal migrates its Tasks first and writes its delta after, so the throw lands after the migration statement and
    /// before the commit. A test-only object in the class's own database, always dropped by the caller in a <c>finally</c>.
    /// </summary>
    /// <returns>The trigger's name, for <see cref="DropTriggerAsync"/>.</returns>
    public static async Task<string> PlantFailingDeltaTriggerAsync(RegistrationDatabase database, Guid projectId)
    {
        var name = "TR_StatusDeltaOperation_ForcedFailure_" + Guid.NewGuid().ToString("N");

        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Review SQL queries - DDL takes no parameters; both values are GUIDs formatted here.
        command.CommandText =
            $"CREATE TRIGGER dbo.[{name}] ON dbo.[StatusDeltaOperation] AFTER INSERT, UPDATE, DELETE AS " +
            $"IF EXISTS (SELECT 1 FROM inserted WHERE ProjectId = '{projectId:D}') OR EXISTS (SELECT 1 FROM deleted WHERE ProjectId = '{projectId:D}') " +
            "THROW 50000, 'Forced failure after the Task migration.', 1;";
#pragma warning restore CA2100

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        return name;
    }

    public static async Task DropTriggerAsync(RegistrationDatabase database, string name)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Review SQL queries - the name is one PlantFailingDeltaTriggerAsync built from a GUID.
        command.CommandText = $"DROP TRIGGER IF EXISTS dbo.[{name}];";
#pragma warning restore CA2100

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Asserts a refusal: the status, the problem type, and - when <paramref name="anchor"/> is given - the same bytes.
    /// </summary>
    public static void AssertRefused(WireResponse? answer, HttpStatusCode status, string problemType, WireResponse? anchor = null)
    {
        Assert.NotNull(answer);
        Assert.True(answer.StatusCode == status, $"{answer.StatusCode} {answer.Body}");
        Assert.Equal(problemType, ProjectStatusWire.ProblemTypeOf(answer.Body));

        if (anchor is not null)
        {
            Assert.Equal(anchor.StatusCode, answer.StatusCode);
            Assert.Equal(anchor.ContentType, answer.ContentType);
            Assert.Equal(anchor.Body, answer.Body);
        }
    }
}

/// <summary>
/// One delta operation as stored.
/// </summary>
/// <param name="StatusId">The Status it applies to.</param>
/// <param name="Kind">Its kind, as stored.</param>
/// <param name="AfterStatusId">A place's anchor, or <c>null</c>.</param>
/// <param name="Sequence">Its place in the delta's order.</param>
internal sealed record DeltaRow(Guid StatusId, string Kind, Guid? AfterStatusId, int Sequence);
