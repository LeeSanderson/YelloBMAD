using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Projects;

/// <summary>
/// Story 2.1 AC6, AC7 and the AR-8 key, read from the migrated catalogue and proved against the
/// database's own policy: <c>Project</c> carries a non-nullable <c>SpaceId</c> under the Space policy with
/// a FILTER and BLOCK predicates <c>AFTER INSERT</c> and <c>AFTER UPDATE</c> (Q5 (a)), so no operation can
/// move a Project to another Space; it carries the <c>(SpaceId, Id)</c> key 2.2's <c>Task</c> will
/// reference (Q4 (a)); and the idempotency record is under the Space policy too (Task 7).
/// </summary>
/// <remarks>
/// <b>Read this beside <c>SpaceIsolationSchemaTests</c>.</b> Its
/// <c>Every_Space_scoped_table_carries_a_filter_and_a_block_predicate</c> asserts exactly two predicates
/// per table in <c>SchemaNames.SpaceScopedTables</c>; <c>Project</c> carries three once Q5 (a) is taken.
/// Dev-story must widen that assertion for <c>Project</c> alone (two for every other table) rather than
/// loosen it to "at least two", and record the change.
/// </remarks>
public sealed partial class ProjectEndpointTests
{
    /// <summary>
    /// AC6, AD-2 - <c>Project.SpaceId</c> is non-nullable, and the Space policy holds a FILTER predicate and
    /// BLOCK predicates after insert and after update on <c>Project</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC6 - the Project table and its policy predicates do not exist until story 2.1 Task 3")]
    [Trait("Requirement", "AR-5")]
    [Trait("Requirement", "NFR-1")]
    public async Task Project_carries_a_non_nullable_SpaceId_under_the_Space_policy_with_both_block_predicates()
    {
        var database = Available();

        Assert.Equal(
            ["0"],
            await CatalogueAsync(
                database,
                "SELECT CAST(c.is_nullable AS int) FROM sys.columns c WHERE c.object_id = OBJECT_ID(N'dbo.Project') AND c.name = N'SpaceId'"));

        var predicates = await CatalogueAsync(
            database,
            """
            SELECT CONCAT(sp.predicate_type_desc COLLATE DATABASE_DEFAULT, N'|', ISNULL(sp.operation_desc COLLATE DATABASE_DEFAULT, N''))
            FROM sys.security_predicates sp
            JOIN sys.security_policies p ON p.object_id = sp.object_id
            WHERE p.name = N'SpaceIsolationPolicy' AND sp.target_object_id = OBJECT_ID(N'dbo.Project')
            """);

        Assert.Equal(["BLOCK|AFTER INSERT", "BLOCK|AFTER UPDATE", "FILTER|"], predicates.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// AC7, Q5 (a) - under a Space's own session key, changing a Project's <c>SpaceId</c> to another real
    /// Space is refused by the database. The target Space exists, so <c>FK_Project_Space_SpaceId</c> is
    /// satisfied and the only thing that can refuse is the <c>AFTER UPDATE</c> block predicate. Anchor: the
    /// same connection may change the Project's name.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC7 - the Project table and its AFTER UPDATE block predicate do not exist until story 2.1 Task 3")]
    [Trait("Requirement", "AD-2")]
    public async Task Moving_a_Project_to_another_Space_is_refused_by_the_database()
    {
        var database = Available();
        Actor? owner = null;
        Actor? other = null;
        var projectId = Guid.Empty;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            other = await RegisterAsync(client, database);
            projectId = IdOf(await CreateAsync(owner, "Stays put"));
        });

        Assert.NotNull(owner);
        Assert.NotNull(other);

        await using var connection = await database.OpenForSpaceAsync(owner.SpaceId, TestContext.Current.CancellationToken);

        Assert.Equal(1, await ExecuteAsync(connection, "UPDATE dbo.[Project] SET Name = N'Renamed in SQL' WHERE Id = @id", ("@id", projectId)));

        var refused = await Assert.ThrowsAsync<SqlException>(() =>
            ExecuteAsync(connection, "UPDATE dbo.[Project] SET SpaceId = @other WHERE Id = @id", ("@other", other.SpaceId), ("@id", projectId)));

        // 33504: "The attempted operation failed because the target object ... has a block predicate that
        // conflicts with this operation" - the policy, not a foreign key or a trigger.
        Assert.Equal(33504, refused.Number);
        Assert.Equal([owner.SpaceId], (await ProjectRowsAsync(database, owner.SpaceId)).Select(row => row.SpaceId));
        Assert.Empty(await ProjectRowsAsync(database, other.SpaceId));
    }

    /// <summary>
    /// AR-8, Q4 (a) - <c>Project</c> carries a unique key on <c>(SpaceId, Id)</c>, so story 2.2's
    /// <c>Task</c> can hold a composite foreign key from its first migration.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC6 - the Project table and its (SpaceId, Id) key do not exist until story 2.1 Task 3")]
    [Trait("Requirement", "AR-8")]
    [Trait("Priority", "P1")]
    public async Task Project_has_a_unique_key_on_SpaceId_and_Id_for_composite_references()
    {
        var database = Available();

        var keys = await CatalogueAsync(
            database,
            """
            SELECT STRING_AGG(c.name COLLATE DATABASE_DEFAULT, N',') WITHIN GROUP (ORDER BY ic.key_ordinal)
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID(N'dbo.Project') AND i.is_unique = 1 AND i.has_filter = 0 AND ic.is_included_column = 0
            GROUP BY i.index_id
            """);

        Assert.Contains("SpaceId,Id", keys);
    }

    /// <summary>
    /// AC9, Task 7 - the idempotency record holds Space content (a Project name in a stored body), so it is
    /// under the Space policy like any Space-scoped table, with a non-nullable <c>SpaceId</c>, and unique on
    /// <c>(SpaceId, AccountId, Key)</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - the IdempotencyRecord table does not exist until story 2.1 Task 7")]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "AD-2")]
    public async Task The_idempotency_record_is_under_the_Space_policy_and_unique_per_Space_Account_and_key()
    {
        var database = Available();

        Assert.Equal(
            ["0"],
            await CatalogueAsync(
                database,
                "SELECT CAST(c.is_nullable AS int) FROM sys.columns c WHERE c.object_id = OBJECT_ID(N'dbo.IdempotencyRecord') AND c.name = N'SpaceId'"));

        var predicates = await CatalogueAsync(
            database,
            """
            SELECT sp.predicate_type_desc FROM sys.security_predicates sp
            JOIN sys.security_policies p ON p.object_id = sp.object_id
            WHERE p.name = N'SpaceIsolationPolicy' AND sp.target_object_id = OBJECT_ID(N'dbo.IdempotencyRecord')
            """);

        Assert.Contains("FILTER", predicates);
        Assert.Contains("BLOCK", predicates);

        var keys = await CatalogueAsync(
            database,
            """
            SELECT STRING_AGG(c.name COLLATE DATABASE_DEFAULT, N',') WITHIN GROUP (ORDER BY ic.key_ordinal)
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID(N'dbo.IdempotencyRecord') AND i.is_unique = 1 AND ic.is_included_column = 0
            GROUP BY i.index_id
            """);

        Assert.Contains("SpaceId,AccountId,Key", keys);
    }

    private static async Task<int> ExecuteAsync(SqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Review SQL queries - constant statements written by this class; values are parameters.
        command.CommandText = sql;
#pragma warning restore CA2100

        foreach (var (name, value) in parameters)
        {
            command.Parameters.Add(new SqlParameter(name, value));
        }

        return await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
