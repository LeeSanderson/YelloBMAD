using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.LabelWire;
using static Yello.Tests.Slices.Tasks.TaskEditWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Story 2.4 AC3 and AC6, AR-5, AR-8, AD-2, read from the migrated catalogue and proved against the database's own
/// policy: <c>Label</c> and <c>TaskLabel</c> each carry a non-nullable <c>SpaceId</c> under the Space policy with
/// FILTER and BLOCK predicates; <c>TaskLabel</c> reaches <c>Task</c> and <c>Label</c> only through keys that carry
/// <c>SpaceId</c>; and the database itself refuses a Label that is still applied. Written red by the story 2.4 ATDD run.
/// </summary>
/// <remarks>
/// <b>Read this beside <c>SpaceIsolationSchemaTests</c></b>, which derives its table list from
/// <c>SchemaNames.SpaceScopedTables</c>: once Task 3 appends both names there, it holds the same three predicates
/// generically. These cases name the two tables so a missed append is still caught.
/// </remarks>
public sealed partial class TaskEndpointTests
{
    private static readonly string[] ThreePredicates = ["BLOCK|AFTER INSERT", "BLOCK|AFTER UPDATE", "FILTER|"];

    /// <summary>
    /// AC6, AD-2, AR-5 - <c>Label</c> and <c>TaskLabel</c> each carry a non-nullable <c>SpaceId</c>, and the Space policy
    /// holds a FILTER predicate and BLOCK predicates after insert and after update on each.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("Label")]
    [InlineData("TaskLabel")]
    [Trait("Requirement", "AR-5")]
    [Trait("Requirement", "NFR-1")]
    public async Task A_Label_table_carries_a_non_nullable_SpaceId_under_the_Space_policy_with_three_predicates(string table)
    {
        var database = Available();

        Assert.Equal(
            ["0"],
            await CatalogueAsync(
                database,
                $"SELECT CAST(c.is_nullable AS int) FROM sys.columns c WHERE c.object_id = OBJECT_ID(N'dbo.[{table}]') AND c.name = N'SpaceId'"));

        var predicates = await CatalogueAsync(
            database,
            $"""
            SELECT CONCAT(sp.predicate_type_desc COLLATE DATABASE_DEFAULT, N'|', ISNULL(sp.operation_desc COLLATE DATABASE_DEFAULT, N''))
            FROM sys.security_predicates sp
            JOIN sys.security_policies p ON p.object_id = sp.object_id
            WHERE p.name = N'SpaceIsolationPolicy' AND sp.target_object_id = OBJECT_ID(N'dbo.[{table}]')
            """);

        Assert.Equal(ThreePredicates, predicates.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// AC6 ("no Label is reachable from another Space"), AD-2 - under a Space's own key, changing a Label's
    /// <c>SpaceId</c> to another real Space is refused by the AFTER UPDATE block predicate (33504), not by a key.
    /// Anchor: the same connection may rename it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-2")]
    public async Task Moving_a_Label_to_another_Space_is_refused_by_the_database()
    {
        var database = Available();
        Actor? owner = null;
        Actor? other = null;
        var labelId = Guid.Empty;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            other = await RegisterAsync(client, database);
            labelId = await SeedAsync(owner, owner.SpaceId, "Pinned");
        });

        Assert.NotNull(owner);
        Assert.NotNull(other);

        await using var connection = await database.OpenForSpaceAsync(owner.SpaceId, TestContext.Current.CancellationToken);

        Assert.Equal(1, await ExecuteAsync(connection, "UPDATE dbo.[Label] SET Name = N'Renamed in SQL' WHERE Id = @id", ("@id", labelId)));

        var refused = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(
            connection,
            "UPDATE dbo.[Label] SET SpaceId = @space WHERE Id = @id",
            ("@space", other.SpaceId),
            ("@id", labelId)));

        Assert.Equal(33504, refused.Number);
        Assert.Equal([owner.SpaceId], (await LabelRowsAsync(database, owner.SpaceId)).Select(row => row.SpaceId));
        Assert.Empty(await LabelRowsAsync(database, other.SpaceId));
    }

    /// <summary>
    /// AC3, AR-8, AD-2 - <c>TaskLabel</c> reaches <c>Task</c> through <c>(SpaceId, TaskId)</c> with cascade (2.5's Task
    /// delete removes applications) and <c>Label</c> through <c>(SpaceId, LabelId)</c> with no action (what makes AC3
    /// hold in the database; SQL Server would refuse a second cascade path anyway, error 1785); it has no foreign key of
    /// its own to <c>Space</c>. <c>Label</c> reaches <c>Space</c> with cascade, as <c>Project</c> does.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-8")]
    [Trait("Requirement", "FR-22")]
    public async Task TaskLabel_reaches_Task_and_Label_only_through_keys_that_carry_SpaceId()
    {
        var database = Available();

        Assert.Equal(
            [
                "Label|NO_ACTION|SpaceId>SpaceId,LabelId>Id",
                "Task|CASCADE|SpaceId>SpaceId,TaskId>Id",
            ],
            (await ForeignKeysOfAsync(database, "TaskLabel")).Order(StringComparer.Ordinal));

        Assert.Equal(["Space|CASCADE|SpaceId>Id"], await ForeignKeysOfAsync(database, "Label"));
    }

    /// <summary>
    /// AC6, AC10, AR-8 - the keys the composite references and the in-use probe need: <c>Task (SpaceId, Id)</c>
    /// (<c>AK_Task_SpaceId_Id</c>, new in Task 3), <c>Label (SpaceId, Id)</c>, <c>Label (SpaceId, Name)</c> (Q2), the
    /// key <c>TaskLabel (SpaceId, TaskId, LabelId)</c>, and an index leading <c>(SpaceId, LabelId, TaskId)</c> so the
    /// delete's probe and its range lock stay inside one Space.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-8")]
    [Trait("Priority", "P1")]
    public async Task The_Label_tables_carry_the_keys_their_references_and_the_in_use_probe_need()
    {
        var database = Available();

        Assert.Contains("SpaceId,Id", await UniqueKeysAsync(database, "Task"));
        Assert.Contains("SpaceId,Id", await UniqueKeysAsync(database, "Label"));
        Assert.Contains("SpaceId,Name", await UniqueKeysAsync(database, "Label"));
        Assert.Contains("SpaceId,TaskId,LabelId", await UniqueKeysAsync(database, "TaskLabel"));

        var indexes = await CatalogueAsync(
            database,
            """
            SELECT STRING_AGG(c.name COLLATE DATABASE_DEFAULT, N',') WITHIN GROUP (ORDER BY ic.key_ordinal)
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID(N'dbo.[TaskLabel]') AND ic.is_included_column = 0
            GROUP BY i.index_id
            """);

        Assert.Contains(indexes, index => index.StartsWith("SpaceId,LabelId", StringComparison.Ordinal));
    }

    /// <summary>
    /// AC3 ("no Task ever references a Label that does not exist") at the database: deleting a Label that is still
    /// applied is refused by the foreign key (547) even with the application layer bypassed. Anchor: an unused Label
    /// deletes on the same connection.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-22")]
    [Trait("Requirement", "AR-8")]
    public async Task The_database_refuses_deleting_a_Label_that_is_still_applied()
    {
        var database = Available();
        Actor? owner = null;
        var applied = Guid.Empty;
        var unused = Guid.Empty;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var (_, taskId) = await ProjectWithTaskAsync(owner, "Carrier");

            applied = await SeedAsync(owner, owner.SpaceId, "Applied");
            unused = await SeedAsync(owner, owner.SpaceId, "Unused");

            Assert.Equal(System.Net.HttpStatusCode.OK, (await EditTaskAsync(owner, owner.SpaceId, taskId, ApplyBody(applied))).StatusCode);
        });

        Assert.NotNull(owner);

        await using var connection = await database.OpenForSpaceAsync(owner.SpaceId, TestContext.Current.CancellationToken);

        Assert.Equal(1, await ExecuteAsync(connection, "DELETE dbo.[Label] WHERE Id = @id", ("@id", unused)));

        var refused = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(connection, "DELETE dbo.[Label] WHERE Id = @id", ("@id", applied)));

        Assert.Equal(547, refused.Number);
        Assert.Equal(["Applied"], (await LabelRowsAsync(database, owner.SpaceId)).Select(row => row.Name));
    }

    /// <summary>
    /// "Referenced table|delete action|parent>referenced, ..." for every foreign key of <paramref name="table"/>.
    /// </summary>
    private static Task<IReadOnlyList<string>> ForeignKeysOfAsync(Accounts.RegisterAccount.RegistrationDatabase database, string table) =>
        CatalogueAsync(
            database,
            $"""
            SELECT CONCAT(
                OBJECT_NAME(fk.referenced_object_id), N'|',
                fk.delete_referential_action_desc COLLATE DATABASE_DEFAULT, N'|',
                STRING_AGG(CONCAT(pc.name COLLATE DATABASE_DEFAULT, N'>', rc.name COLLATE DATABASE_DEFAULT), N',') WITHIN GROUP (ORDER BY fkc.constraint_column_id))
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
            JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
            WHERE fk.parent_object_id = OBJECT_ID(N'dbo.[{table}]')
            GROUP BY fk.object_id, fk.referenced_object_id, fk.delete_referential_action_desc
            """);
}
