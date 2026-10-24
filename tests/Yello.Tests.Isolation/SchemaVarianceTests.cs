using Xunit;

namespace Yello.Tests.Isolation;

/// <summary>
/// The named isolation variances (<c>deferred-work.md:382</c>, story 1.9 Task 10): every table with a
/// column whose name ends in <c>SpaceId</c> is under the Space security policy, or is one of the
/// tables named here with the reason it is not.
/// </summary>
/// <remarks>
/// <c>SpaceIsolationSchemaTests:127</c> in Slices sees only columns named exactly <c>SpaceId</c>, so
/// <c>AccessRefusal.TargetSpaceId</c> and <c>ApiToken.BoundSpaceId</c> are outside it by their names
/// rather than by a decision anyone can read. This case makes the decision the list.
/// </remarks>
[Trait("Suite", "Isolation")]
[Trait("Priority", "P0")]
[Trait("Requirement", "NFR-1")]
[Trait("Requirement", "AD-2")]
public sealed class SchemaVarianceTests
{
    /// <summary>
    /// Tables holding a Space identifier that are outside the policy by design, with why.
    /// </summary>
    private static readonly Dictionary<string, string> NamedVariances = new(StringComparer.Ordinal)
    {
        ["AccessRefusal.TargetSpaceId"] = "written after the rollback with no session context; AD-20",
        ["ApiToken.BoundSpaceId"] = "looked up by hash before any Space is known; story 1.8 Q4",
    };

    private const string SpaceIdColumnsAndTheirPolicy = """
        SELECT t.name, c.name,
               CASE WHEN EXISTS (
                   SELECT 1 FROM sys.security_predicates p
                   WHERE p.target_object_id = t.object_id AND p.predicate_type_desc = 'FILTER')
               THEN 'covered' ELSE 'uncovered' END
        FROM sys.tables t
        JOIN sys.columns c ON c.object_id = t.object_id
        WHERE c.name LIKE '%SpaceId'
        ORDER BY t.name, c.name
        """;

    /// <summary>
    /// 1.9-SCHEMA-001 - every <c>*SpaceId</c> column is under the policy or a named variance, and every
    /// named variance still exists. Plant (Task 12): a new table with an <c>OtherSpaceId</c> column and
    /// no policy is caught.
    /// </summary>
    [Fact(Skip = "red: AC2 - the shared engine and a migrated database per class do not exist yet (story 1.9 Tasks 2 and 10)")]
    public async Task Every_SpaceId_column_is_under_the_Space_policy_or_a_named_variance()
    {
        await using var world = await IsolationWorld.StartAsync();

        var columns = await world.QueryCatalogAsync(SpaceIdColumnsAndTheirPolicy);

        // Vacuity guard: the query sees a table known to be under the policy before it judges any.
        Assert.Contains(columns, row => row[0] == "Membership" && row[1] == "SpaceId" && row[2] == "covered");

        var unexplained = columns
            .Where(row => row[2] == "uncovered" && !NamedVariances.ContainsKey($"{row[0]}.{row[1]}"))
            .Select(row => $"{row[0]}.{row[1]}")
            .ToList();

        Assert.True(unexplained.Count == 0, "A Space identifier sits outside the policy with no named reason: " + string.Join(", ", unexplained));

        var stale = NamedVariances.Keys
            .Where(variance => !columns.Any(row => $"{row[0]}.{row[1]}" == variance && row[2] == "uncovered"))
            .ToList();

        Assert.True(stale.Count == 0, "A named variance no longer describes the schema; remove or correct it: " + string.Join(", ", stale));
    }
}
