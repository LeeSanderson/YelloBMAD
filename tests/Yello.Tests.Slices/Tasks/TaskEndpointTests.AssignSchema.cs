using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.TaskEditWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// The Assignee and the notification send record, read from the migrated catalogue and proved against the database itself:
/// the Assignee is a composite foreign key <c>(SpaceId, AssigneeMembershipId) → Membership(SpaceId, Id)</c> with NO ACTION,
/// so a Membership of another Space cannot be stored even with the application bypassed; the List's Assignee sort has its
/// composite index; and <c>NotificationSend</c> holds exactly the Space, the kind and the time, under the Space policy.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why NO ACTION and not SET NULL or CASCADE</b>: <c>SET NULL</c> would null the non-nullable <c>SpaceId</c> the key
/// carries; <c>CASCADE</c> would delete the Task, which must survive, and would add a second cascade path from
/// <c>Space</c> (SQL Server 1785). The removal and the leave unassign explicitly instead.
/// </para>
/// <para>
/// <b>Seed constraints checked.</b> The raw <c>UPDATE</c>s run under the Task's own Space key, so the BLOCK AFTER UPDATE
/// predicate admits them (the <c>SpaceId</c> is unchanged); the Membership they name is the Task's own Space's (the anchor)
/// or another Account's Personal Space's (the refusal). The key is checked by the engine regardless of the FILTER predicate,
/// which is what makes the refusal a <c>547</c> rather than a silent miss.
/// </para>
/// </remarks>
public sealed partial class TaskEndpointTests
{
    private static readonly string[] NotificationSendColumns =
        ["Id|uniqueidentifier|NO", "Kind|nvarchar|NO", "RecordedAt|datetimeoffset|NO", "SpaceId|uniqueidentifier|NO"];

    private static readonly string[] AssigneeIndexColumns = ["SpaceId", "ProjectId", "AssigneeMembershipId", "Id"];

    /// <summary>
    /// <c>Task.AssigneeMembershipId</c> is a nullable <c>uniqueidentifier</c>, and its only foreign key reaches
    /// <c>Membership</c> through <c>(SpaceId, AssigneeMembershipId) → (SpaceId, Id)</c>, in that column order, with
    /// <c>delete_referential_action</c> NO ACTION. The Task keeps its earlier keys to Project and Status.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-8")]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "FR-21")]
    public async Task The_Assignee_is_a_composite_key_carrying_SpaceId_to_Membership_with_no_action()
    {
        var database = Available();

        Assert.Equal(
            ["uniqueidentifier|1"],
            await CatalogueAsync(
                database,
                "SELECT CONCAT(TYPE_NAME(c.user_type_id), N'|', CAST(c.is_nullable AS int)) FROM sys.columns c WHERE c.object_id = OBJECT_ID(N'dbo.[Task]') AND c.name = N'AssigneeMembershipId'"));

        var keys = await ForeignKeysOfAsync(database, "Task");

        Assert.Contains("Membership|NO_ACTION|SpaceId>SpaceId,AssigneeMembershipId>Id", keys);
        Assert.Single(keys, key => key.StartsWith("Membership|", StringComparison.Ordinal));
        Assert.Contains(keys, key => key.StartsWith("Project|", StringComparison.Ordinal));
    }

    /// <summary>
    /// At the database: under one Space's key, setting a Task's Assignee to another Space's real Membership is refused
    /// by the composite key (547), with the application bypassed entirely. Anchor: the same connection may set it to the
    /// Task's own Space's Membership, and clear it again.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-8")]
    [Trait("Requirement", "AD-2")]
    public async Task The_database_refuses_an_Assignee_from_another_Space()
    {
        var database = Available();
        var wire = new WireDatabase(fixture.ConnectionString!);
        Actor? owner = null;
        Actor? other = null;
        var taskId = Guid.Empty;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            other = await RegisterAsync(client, database);
            (_, taskId) = await ProjectWithTaskAsync(owner, "Guarded by the key");
        });

        Assert.NotNull(owner);
        Assert.NotNull(other);

        var own = (await wire.MembershipOfAsync(owner.SpaceId, owner.AccountId)).Id;
        var foreign = (await wire.MembershipOfAsync(other.SpaceId, other.AccountId)).Id;

        await using var connection = await database.OpenForSpaceAsync(owner.SpaceId, TestContext.Current.CancellationToken);

        const string assign = "UPDATE dbo.[Task] SET AssigneeMembershipId = @membership WHERE SpaceId = @space AND Id = @id";

        Assert.Equal(1, await ExecuteAsync(connection, assign, ("@membership", own), ("@space", owner.SpaceId), ("@id", taskId)));
        Assert.Equal(1, await ExecuteAsync(connection, "UPDATE dbo.[Task] SET AssigneeMembershipId = NULL WHERE SpaceId = @space AND Id = @id", ("@space", owner.SpaceId), ("@id", taskId)));

        var refused = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(connection, assign, ("@membership", foreign), ("@space", owner.SpaceId), ("@id", taskId)));

        Assert.Equal(547, refused.Number);
        Assert.Null(await wire.AssigneeOfAsync(owner.SpaceId, taskId));
    }

    /// <summary>
    /// At the database: the Assignee sort's index exists on <c>Task</c> with key columns exactly
    /// <c>(SpaceId, ProjectId, AssigneeMembershipId, Id)</c>, in that order.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-29")]
    [Trait("Requirement", "AR-22")]
    [Trait("Requirement", "FR-30")]
    public async Task The_Assignee_sort_has_its_composite_index()
    {
        var database = Available();

        var indexes = await CatalogueAsync(
            database,
            """
            SELECT STRING_AGG(c.name COLLATE DATABASE_DEFAULT, N',') WITHIN GROUP (ORDER BY ic.key_ordinal)
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID(N'dbo.[Task]') AND i.name = N'IX_Task_SpaceId_ProjectId_AssigneeMembershipId_Id' AND ic.is_included_column = 0
            GROUP BY i.index_id
            """);

        Assert.Equal([string.Join(',', AssigneeIndexColumns)], indexes);
    }

    /// <summary>
    /// Never message content or recipient address: <c>NotificationSend</c> has exactly the columns
    /// <c>Id</c>, <c>SpaceId</c>, <c>Kind</c> and <c>RecordedAt</c>, none nullable, so no recipient, subject, body, Task or
    /// Account can be held; it sits under the Space policy with a FILTER predicate and BLOCK predicates after insert and after
    /// update; and it reaches <c>Space</c> with cascade, so a Space's erasure takes its records.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FS-NFR-2")]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "NFR-1")]
    public async Task The_send_record_holds_only_the_Space_the_kind_and_the_time_under_the_Space_policy()
    {
        var database = Available();

        var columns = await CatalogueAsync(
            database,
            """
            SELECT CONCAT(COLUMN_NAME COLLATE DATABASE_DEFAULT, N'|', DATA_TYPE COLLATE DATABASE_DEFAULT, N'|', IS_NULLABLE COLLATE DATABASE_DEFAULT)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'NotificationSend'
            """);

        Assert.Equal(NotificationSendColumns, columns.Order(StringComparer.Ordinal));

        var predicates = await CatalogueAsync(
            database,
            """
            SELECT CONCAT(sp.predicate_type_desc COLLATE DATABASE_DEFAULT, N'|', ISNULL(sp.operation_desc COLLATE DATABASE_DEFAULT, N''))
            FROM sys.security_predicates sp
            JOIN sys.security_policies p ON p.object_id = sp.object_id
            WHERE p.name = N'SpaceIsolationPolicy' AND sp.target_object_id = OBJECT_ID(N'dbo.[NotificationSend]')
            """);

        Assert.Equal(ThreePredicates, predicates.Order(StringComparer.Ordinal));
        Assert.Equal(["Space|CASCADE|SpaceId>Id"], await ForeignKeysOfAsync(database, "NotificationSend"));
    }
}
