using Xunit;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// A Task's description against the migrated schema: the change log is a Space-scoped table that carries its Space
/// directly, cascades from its Task and never from its Space, keeps authorship through Account erasure, and is under the
/// policy with all three predicates; the projection is a non-nullable column with an empty default, added expand-only.
/// </summary>
/// <remarks>
/// The table and column names are the story's (<c>TaskDescriptionChange</c>, <c>Task.Description</c>). Every case is red
/// until the migration lands.
/// </remarks>
public sealed partial class TaskEndpointTests
{
    /// <summary>
    /// The log carries <c>SpaceId</c> non-nullable and directly, its id is supplied by the application (no default), and its
    /// order is unique per Task: one unique index over <c>(SpaceId, TaskId, Sequence)</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-19")]
    [Trait("Requirement", "AD-13")]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "AR-34")]
    public async Task The_description_log_carries_its_Space_directly_and_orders_each_Tasks_rows_uniquely()
    {
        var database = Available();

        Assert.Equal(
            ["AuthorAccountId:uniqueidentifier:1", "AuthorName:nvarchar:1", "Id:uniqueidentifier:0", "Kind:nvarchar:0", "Payload:varbinary:0", "RecordedAt:datetimeoffset:0", "Sequence:bigint:0", "SpaceId:uniqueidentifier:0", "TaskId:uniqueidentifier:0"],
            await CatalogueAsync(
                database,
                """
                SELECT CONCAT(c.name, N':', t.name, N':', c.is_nullable)
                FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
                WHERE c.object_id = OBJECT_ID(N'dbo.[TaskDescriptionChange]')
                ORDER BY c.name
                """));

        Assert.Empty(await CatalogueAsync(
            database,
            "SELECT dc.name FROM sys.default_constraints dc WHERE dc.parent_object_id = OBJECT_ID(N'dbo.[TaskDescriptionChange]')"));

        Assert.Contains(
            "SpaceId,TaskId,Sequence",
            await CatalogueAsync(
                database,
                """
                SELECT STRING_AGG(c.name, N',') WITHIN GROUP (ORDER BY ic.key_ordinal)
                FROM sys.indexes i
                JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                WHERE i.object_id = OBJECT_ID(N'dbo.[TaskDescriptionChange]') AND i.is_unique = 1 AND ic.is_included_column = 0
                GROUP BY i.index_id
                """));
    }

    /// <summary>
    /// The log's keys: a composite <c>(SpaceId, TaskId)</c> key to <c>Task</c> that cascades, an <c>AuthorAccountId</c> key to
    /// <c>Account</c> that sets null, and no key to <c>Space</c> (SQL error 1785: a second cascade path). Anchor: the query
    /// reads the Task key it asserts.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-19")]
    [Trait("Requirement", "AR-5")]
    [Trait("Requirement", "FR-3")]
    public async Task The_description_log_cascades_from_its_Task_keeps_its_rows_when_an_Account_is_erased_and_has_no_key_to_its_Space()
    {
        var database = Available();

        Assert.Equal(
            ["Account:AuthorAccountId:SET_NULL", "Task:SpaceId,TaskId:CASCADE"],
            (await CatalogueAsync(
                database,
                """
                SELECT CONCAT(OBJECT_NAME(fk.referenced_object_id) COLLATE DATABASE_DEFAULT, N':',
                    STRING_AGG(c.name COLLATE DATABASE_DEFAULT, N',') WITHIN GROUP (ORDER BY fkc.constraint_column_id), N':',
                    fk.delete_referential_action_desc COLLATE DATABASE_DEFAULT)
                FROM sys.foreign_keys fk
                JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
                JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
                WHERE fk.parent_object_id = OBJECT_ID(N'dbo.[TaskDescriptionChange]')
                GROUP BY fk.object_id, fk.referenced_object_id, fk.delete_referential_action_desc
                """)).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The log is under the Space policy with a FILTER predicate and BLOCK predicates after insert and after update, so a row
    /// can neither be read across Spaces, written into another, nor moved to another. Anchor: <c>Task</c>, read by the same
    /// query, carries the same three.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-5")]
    [Trait("Requirement", "AR-8")]
    [Trait("Requirement", "AD-2")]
    public async Task The_description_log_is_under_the_Space_policy_with_a_filter_and_two_block_predicates()
    {
        var database = Available();

        foreach (var table in new[] { "Task", "TaskDescriptionChange" })
        {
            Assert.Equal(
                ["BLOCK:AFTER INSERT", "BLOCK:AFTER UPDATE", "FILTER:"],
                await CatalogueAsync(
                    database,
                    $"""
                    SELECT CONCAT(sp.predicate_type_desc, N':', sp.operation_desc)
                    FROM sys.security_predicates sp
                    JOIN sys.security_policies p ON p.object_id = sp.object_id
                    WHERE p.name = N'SpaceIsolationPolicy' AND sp.target_object_id = OBJECT_ID(N'dbo.[{table}]')
                    ORDER BY 1
                    """));
        }
    }

    /// <summary>
    /// The projection is <c>dbo.[Task].Description</c>, <c>nvarchar(max)</c>, not nullable, with the default <c>N''</c>, so
    /// the revision before still inserts a Task without naming it. No length is invented: <c>max_length</c> is <c>-1</c>.
    /// Anchor: <c>Title</c>, read by the same query, has its bounded length.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-19")]
    [Trait("Requirement", "AD-13")]
    public async Task The_projection_is_a_non_nullable_unbounded_text_column_defaulting_to_empty()
    {
        var database = Available();

        var columns = await CatalogueAsync(
            database,
            """
            SELECT CONCAT(c.name, N':', t.name, N':', c.max_length, N':', c.is_nullable, N':', COALESCE(dc.definition, N''))
            FROM sys.columns c
            JOIN sys.types t ON t.user_type_id = c.user_type_id
            LEFT JOIN sys.default_constraints dc ON dc.object_id = c.default_object_id
            WHERE c.object_id = OBJECT_ID(N'dbo.[Task]') AND c.name IN (N'Description', N'Title')
            ORDER BY c.name
            """);

        Assert.Equal(2, columns.Count);
        Assert.Equal("Description:nvarchar:-1:0:(N'')", columns[0]);
        Assert.StartsWith("Title:nvarchar:", columns[1], StringComparison.Ordinal);
        Assert.DoesNotContain(":-1:", columns[1], StringComparison.Ordinal);
    }
}
