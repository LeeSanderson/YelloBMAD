using Xunit;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Story 2.3 Task 3 against the migrated schema: the due date is a nullable <c>datetimeoffset</c>, added
/// expand-only, and still no description column (AD-12, AC6). Written red by the story 2.3 ATDD run.
/// </summary>
public sealed partial class TaskEndpointTests
{
    /// <summary>
    /// AC7, AR-34, A-15 - <c>dbo.[Task].DueDate</c> is <c>datetimeoffset</c> and nullable (a Task has a due date
    /// optionally, <c>prd.md:387</c>; nullable is also what keeps the migration expand-only for the revision
    /// still serving), and no column carries a description. Anchor: <c>CreatedAt</c>, read by the same query,
    /// is the non-nullable <c>datetimeoffset</c> 2.2 wrote.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "AD-12")]
    public async Task The_due_date_is_a_nullable_datetimeoffset_and_no_column_holds_a_description()
    {
        var database = Available();

        var columns = await CatalogueAsync(
            database,
            """
            SELECT CONCAT(c.name, N':', t.name, N':', c.is_nullable)
            FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
            WHERE c.object_id = OBJECT_ID(N'dbo.[Task]') AND c.name IN (N'CreatedAt', N'DueDate')
            ORDER BY c.name
            """);

        Assert.Equal(["CreatedAt:datetimeoffset:0", "DueDate:datetimeoffset:1"], columns);

        Assert.Empty(await CatalogueAsync(
            database,
            "SELECT name FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Task]') AND name LIKE N'%Description%'"));
    }
}
