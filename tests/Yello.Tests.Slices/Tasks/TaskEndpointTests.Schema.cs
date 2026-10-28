using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Story 2.2 AC2 and AC8, AR-8, AD-15 and AD-17, read from the migrated catalogue and proved against the
/// database's own policy: <c>Task</c> carries a non-nullable <c>SpaceId</c> directly, under the Space policy
/// with FILTER and BLOCK predicates <c>AFTER INSERT</c> and <c>AFTER UPDATE</c>; it reaches its Project and
/// its Status only through composite keys that carry <c>SpaceId</c>; its position key is declared
/// <c>Latin1_General_100_BIN2</c> from the first migration (Q2 (a)); and no Task holds a Status its Space
/// does not define.
/// </summary>
/// <remarks>
/// <b>Read this beside <c>SpaceIsolationSchemaTests</c>.</b> Its
/// <c>Every_Space_scoped_table_carries_a_filter_and_a_block_predicate</c> expects three predicates for
/// <c>Project</c> and two for every other table; dev-story widens it so <c>Task</c> also expects three, and
/// records the change. <see cref="The_position_key_is_BIN2_and_unique_within_a_Project_and_Status"/> is the one-query
/// check Q2 (a) asked for; the full A-6 / R7 collation assertion - BIN2 on the index key column - and the mixed-case
/// ordering case are story 2.6's, in <c>TaskEndpointTests.MoveSchema.cs</c>.
/// </remarks>
public sealed partial class TaskEndpointTests
{
    /// <summary>
    /// AC8, AD-2, AR-5 - <c>Task.SpaceId</c> is non-nullable on the Task itself, the Space policy holds a
    /// FILTER predicate and BLOCK predicates after insert and after update on <c>Task</c>, and there is no
    /// description column (FR-31 is Epic 7, <c>epics.md:437</c>).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-5")]
    [Trait("Requirement", "NFR-1")]
    public async Task Task_carries_a_non_nullable_SpaceId_under_the_Space_policy_with_three_predicates()
    {
        var database = Available();

        Assert.Equal(
            ["0"],
            await CatalogueAsync(
                database,
                "SELECT CAST(c.is_nullable AS int) FROM sys.columns c WHERE c.object_id = OBJECT_ID(N'dbo.[Task]') AND c.name = N'SpaceId'"));

        var predicates = await CatalogueAsync(
            database,
            """
            SELECT CONCAT(sp.predicate_type_desc COLLATE DATABASE_DEFAULT, N'|', ISNULL(sp.operation_desc COLLATE DATABASE_DEFAULT, N''))
            FROM sys.security_predicates sp
            JOIN sys.security_policies p ON p.object_id = sp.object_id
            WHERE p.name = N'SpaceIsolationPolicy' AND sp.target_object_id = OBJECT_ID(N'dbo.[Task]')
            """);

        Assert.Equal(["BLOCK|AFTER INSERT", "BLOCK|AFTER UPDATE", "FILTER|"], predicates.Order(StringComparer.Ordinal));

        var columns = await CatalogueAsync(database, "SELECT c.name FROM sys.columns c WHERE c.object_id = OBJECT_ID(N'dbo.[Task]')");

        Assert.Contains("SpaceId", columns);
        Assert.DoesNotContain(columns, column => column.Contains("Description", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// AC2 ("permanently to one Space"), AD-2 - under a Space's own key, changing a Task's <c>SpaceId</c> to
    /// another real Space is refused by the database. The statement moves the Project and the Status with it
    /// to that Space's own, so both composite foreign keys are satisfied and the only thing that can refuse
    /// is the <c>AFTER UPDATE</c> block predicate (error 33504). Anchor: the same connection may retitle it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-2")]
    public async Task Moving_a_Task_to_another_Space_is_refused_by_the_database()
    {
        var database = Available();
        Actor? owner = null;
        Actor? other = null;
        var taskId = Guid.Empty;
        var otherProject = Guid.Empty;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            other = await RegisterAsync(client, database);

            var projectId = await CreateProjectAsync(owner, "Stays put");
            otherProject = await CreateProjectAsync(other, "Elsewhere");
            taskId = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Pinned"));
        });

        Assert.NotNull(owner);
        Assert.NotNull(other);

        var otherStatus = (await StatusesAsync(database, other.SpaceId))[0];

        await using var connection = await database.OpenForSpaceAsync(owner.SpaceId, TestContext.Current.CancellationToken);

        Assert.Equal(1, await ExecuteAsync(connection, "UPDATE dbo.[Task] SET Title = N'Retitled in SQL' WHERE Id = @id", ("@id", taskId)));

        var refused = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(
            connection,
            "UPDATE dbo.[Task] SET SpaceId = @space, ProjectId = @project, StatusId = @status WHERE Id = @id",
            ("@space", other.SpaceId),
            ("@project", otherProject),
            ("@status", otherStatus.Id),
            ("@id", taskId)));

        // 33504: the block predicate, not a foreign key (547) or a trigger.
        Assert.Equal(33504, refused.Number);
        Assert.Equal([owner.SpaceId], (await TaskRowsAsync(database, owner.SpaceId)).Select(row => row.SpaceId));
        Assert.Empty(await TaskRowsAsync(database, other.SpaceId));
    }

    /// <summary>
    /// AR-8, AD-2 - <c>Task</c> references its Project through <c>(SpaceId, ProjectId)</c> → <c>Project
    /// (SpaceId, Id)</c> with cascade, and its Status through <c>(SpaceId, StatusId)</c> →
    /// <c>StatusDefinition (SpaceId, Id)</c> with no action; and there is no separate foreign key from
    /// <c>Task</c> to <c>Space</c>, which would be a second cascade path (SQL Server error 1785).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-8")]
    public async Task Task_reaches_its_Project_and_Status_only_through_keys_that_carry_SpaceId()
    {
        var database = Available();

        var keys = await CatalogueAsync(
            database,
            """
            SELECT CONCAT(
                OBJECT_NAME(fk.referenced_object_id), N'|',
                fk.delete_referential_action_desc COLLATE DATABASE_DEFAULT, N'|',
                STRING_AGG(CONCAT(pc.name COLLATE DATABASE_DEFAULT, N'>', rc.name COLLATE DATABASE_DEFAULT), N',') WITHIN GROUP (ORDER BY fkc.constraint_column_id))
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
            JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
            WHERE fk.parent_object_id = OBJECT_ID(N'dbo.[Task]')
            GROUP BY fk.object_id, fk.referenced_object_id, fk.delete_referential_action_desc
            """);

        Assert.Equal(
            [
                "Project|CASCADE|SpaceId>SpaceId,ProjectId>Id",
                "StatusDefinition|NO_ACTION|SpaceId>SpaceId,StatusId>Id",
            ],
            keys.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// AR-8, Task 2 - <c>StatusDefinition</c> carries a unique key on <c>(SpaceId, Id)</c>, so a Task's
    /// Status can never be another Space's.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-8")]
    [Trait("Priority", "P1")]
    public async Task StatusDefinition_has_a_unique_key_on_SpaceId_and_Id_for_composite_references()
    {
        var database = Available();

        Assert.Contains("SpaceId,Id", await UniqueKeysAsync(database, "StatusDefinition"));
    }

    /// <summary>
    /// AD-15, AR-21, Q2 (a) - the position key column is declared <c>Latin1_General_100_BIN2</c> at its
    /// definition (the collation cannot be retrofitted on Azure SQL), and a unique index orders it within
    /// <c>(SpaceId, ProjectId, StatusId)</c>, <c>SpaceId</c> leading so a seek and its range lock stay in one
    /// Space.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-21")]
    [Trait("Requirement", "AD-15")]
    [Trait("Priority", "P1")]
    public async Task The_position_key_is_BIN2_and_unique_within_a_Project_and_Status()
    {
        var database = Available();

        Assert.Equal(
            ["Latin1_General_100_BIN2"],
            await CatalogueAsync(
                database,
                "SELECT c.collation_name FROM sys.columns c WHERE c.object_id = OBJECT_ID(N'dbo.[Task]') AND c.name = N'PositionKey'"));

        Assert.Contains("SpaceId,ProjectId,StatusId,PositionKey", await UniqueKeysAsync(database, "Task"));
    }

    /// <summary>
    /// AD-17, AR-24, S-2 (query level) - after Tasks are created through the product in two Spaces, no Task
    /// holds a Status that its own Space does not define. Anchored: there are Tasks to have checked.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "AR-24")]
    public async Task No_Task_holds_a_Status_its_Space_does_not_define()
    {
        var database = Available();
        Actor? first = null;
        Actor? second = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            first = await RegisterAsync(client, database);
            second = await RegisterAsync(client, database);

            foreach (var actor in new[] { first, second })
            {
                var projectId = await CreateProjectAsync(actor, "Invariant");

                _ = IdOf(await CreateTaskAsync(actor, actor.SpaceId, projectId, "One"));
                _ = IdOf(await CreateTaskAsync(actor, actor.SpaceId, projectId, "Two"));
            }
        });

        Assert.NotNull(first);
        Assert.NotNull(second);

        foreach (var actor in new[] { first, second })
        {
            var tasks = await TaskRowsAsync(database, actor.SpaceId);
            var statuses = (await StatusesAsync(database, actor.SpaceId)).Select(status => status.Id).ToHashSet();

            Assert.Equal(2, tasks.Count);
            Assert.All(tasks, task => Assert.Contains(task.StatusId, statuses));
        }
    }

    private static Task<IReadOnlyList<string>> UniqueKeysAsync(Accounts.RegisterAccount.RegistrationDatabase database, string table) =>
        CatalogueAsync(
            database,
            $"""
            SELECT STRING_AGG(c.name COLLATE DATABASE_DEFAULT, N',') WITHIN GROUP (ORDER BY ic.key_ordinal)
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID(N'dbo.[{table}]') AND i.is_unique = 1 AND i.has_filter = 0 AND ic.is_included_column = 0
            GROUP BY i.index_id
            """);
}
