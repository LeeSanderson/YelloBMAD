using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Infrastructure.Persistence;

namespace Yello.Tests.Slices.Accounts.RegisterAccount;

/// <summary>
/// The schema test AD-2 requires of every Space-scoped table this story creates.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists at all.</b> AD-2 (<c>ARCHITECTURE-SPINE.md:86</c>) is blunt: "A Space-scoped
/// table without an RLS policy fails the schema test", and the readiness report (<c>:1003</c>)
/// requires that every entity "carries a schema test asserting the RLS policy in the same story".
/// <c>TESTING-CONVENTIONS.md:137</c> records that story 1.1 wrote none "because there is no
/// schema". This story creates the first schema, so this is the first schema test.
/// </para>
/// <para>
/// <b>Metadata as well as behaviour, deliberately.</b>
/// <see cref="RegisterAccountIntegrationTests.Space_scoped_rows_are_invisible_without_a_session_context"/>
/// proves the policy WORKS. That is the more valuable assertion and it is not enough on its own:
/// it would also pass if a later story dropped the policy and replaced it with an application-side
/// filter, or left the policy in place with <c>STATE = OFF</c> while some other mechanism happened
/// to hide the rows. Reading <c>sys.security_policies</c> asserts that the mechanism is the one
/// the architecture chose, which is what AD-2 actually decides.
/// </para>
/// <para>
/// <b>Derived from <c>SchemaNames.SpaceScopedTables</c>, never from a list written here.</b> A
/// table added to that list by a later story is held to this without anyone remembering to extend
/// the test - and a table added to the schema but not to that list is caught by the count
/// assertion below rather than passing unnoticed, which is the vacuous-gate defect this suite
/// exists to avoid.
/// </para>
/// <para>
/// <b>Where this test lives, and why not in the architecture suite.</b> The story's structure
/// note puts "the schema test" in <c>Yello.Tests.Architecture</c>. It cannot go there: asserting a
/// MIGRATED schema needs a real SQL Server, the architecture suite's ring row does not include
/// <c>Yello.Tests.Shared</c>, and adding it would make the one suite that "takes seconds and
/// should fail before anything slower starts" depend on a container start. Slices is where the
/// container fixture is already reachable, and it is where the story's own testing guidance says
/// to keep this story's cases.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-5")]
[Trait("Requirement", "AD-2")]
[Trait("Requirement", "NFR-1")]
public sealed class SpaceIsolationSchemaTests(MigratedDatabaseFixture fixture)
    : IClassFixture<MigratedDatabaseFixture>
{
    /// <summary>
    /// One FILTER and one BLOCK predicate per Space-scoped table.
    /// </summary>
    private const int PredicatesPerTable = 2;

    [Fact]
    public async Task The_security_policy_exists_and_is_enabled()
    {
        var database = Available();

        await using var connection = await database.OpenWithoutSpaceContextAsync(
            TestContext.Current.CancellationToken);

        var enabled = await ScalarAsync(
            connection,
            """
            SELECT CAST(p.is_enabled AS int)
            FROM sys.security_policies p
            WHERE p.name = @name
            """,
            SchemaNames.SpaceIsolationPolicy,
            TestContext.Current.CancellationToken);

        // Null means no policy at all; 0 means one that was created WITH (STATE = OFF) or later
        // disabled - which reads as present to anything that only checks for existence, and
        // filters nothing.
        Assert.Equal(1, enabled);
    }

    /// <summary>
    /// Every Space-scoped table carries both halves of the predicate.
    /// </summary>
    /// <remarks>
    /// A FILTER predicate alone hides other Spaces' rows from reads and still lets a caller WRITE
    /// a row into a Space it cannot see, which is the more damaging direction and the one a
    /// read-only test would never notice.
    /// </remarks>
    [Fact]
    public async Task Every_Space_scoped_table_carries_a_filter_and_a_block_predicate()
    {
        var database = Available();

        await using var connection = await database.OpenWithoutSpaceContextAsync(
            TestContext.Current.CancellationToken);

        foreach (var table in SchemaNames.SpaceScopedTables)
        {
            var predicates = await ScalarAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM sys.security_predicates sp
                JOIN sys.security_policies p ON p.object_id = sp.object_id
                JOIN sys.tables t ON t.object_id = sp.target_object_id
                WHERE p.name = @policy AND t.name = @name
                """,
                table,
                TestContext.Current.CancellationToken,
                SchemaNames.SpaceIsolationPolicy);

            Assert.Equal(PredicatesPerTable, predicates);
        }
    }

    /// <summary>
    /// The policy covers exactly the tables that carry a SpaceId, and no others.
    /// </summary>
    /// <remarks>
    /// <b>This is the half that stops the test above going stale.</b> Iterating
    /// <c>SchemaNames.SpaceScopedTables</c> proves every table on the list is covered; it says
    /// nothing about a table added to the schema and left off the list, which would be a
    /// Space-scoped table with no policy and a green suite. Reading the catalogue for the
    /// <c>SpaceId</c> column and comparing both directions is what closes that.
    /// </remarks>
    [Fact]
    public async Task Every_table_with_a_SpaceId_column_is_covered_by_the_policy()
    {
        var database = Available();

        await using var connection = await database.OpenWithoutSpaceContextAsync(
            TestContext.Current.CancellationToken);

        var scoped = await ReadNamesAsync(
            connection,
            """
            SELECT t.name
            FROM sys.tables t
            JOIN sys.columns c ON c.object_id = t.object_id
            WHERE c.name = @name AND t.is_ms_shipped = 0
            ORDER BY t.name
            """,
            SchemaNames.SpaceIdColumn,
            TestContext.Current.CancellationToken);

        var covered = await ReadNamesAsync(
            connection,
            """
            SELECT DISTINCT t.name
            FROM sys.security_predicates sp
            JOIN sys.security_policies p ON p.object_id = sp.object_id
            JOIN sys.tables t ON t.object_id = sp.target_object_id
            WHERE p.name = @name
            ORDER BY t.name
            """,
            SchemaNames.SpaceIsolationPolicy,
            TestContext.Current.CancellationToken);

        Assert.Equal(scoped, covered);

        // And that set is the one SchemaNames names, so the constant a migration and a test both
        // read cannot drift away from the schema itself.
        Assert.Equal(
            SchemaNames.SpaceScopedTables.OrderBy(name => name, StringComparer.Ordinal).ToList(),
            scoped);
    }

    /// <summary>
    /// AD-5 / AR-12's filtered unique index exists, is unique, and is actually filtered.
    /// </summary>
    /// <remarks>
    /// The filter is asserted as well as the uniqueness, because they fail differently and only
    /// one of them is visible from behaviour. An index that is unique on <c>SpaceId</c> with NO
    /// filter would permit exactly one Membership per Space of any Role, which breaks sharing
    /// entirely; an index that is filtered but not unique permits two Owners. The behavioural
    /// test in the integration class catches the second and not the first.
    /// </remarks>
    [Fact]
    public async Task The_Owner_Membership_index_is_unique_and_filtered()
    {
        var database = Available();

        await using var connection = await database.OpenWithoutSpaceContextAsync(
            TestContext.Current.CancellationToken);

        var unique = await ScalarAsync(
            connection,
            """
            SELECT CAST(i.is_unique AS int)
            FROM sys.indexes i
            WHERE i.name = @name
            """,
            SchemaNames.MembershipOwnerUniqueIndex,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, unique);

        var filtered = await ScalarAsync(
            connection,
            """
            SELECT CAST(i.has_filter AS int)
            FROM sys.indexes i
            WHERE i.name = @name
            """,
            SchemaNames.MembershipOwnerUniqueIndex,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, filtered);
    }

    /// <summary>
    /// FR-1's uniqueness is an index rather than an application check.
    /// </summary>
    [Fact]
    public async Task The_email_address_uniqueness_index_exists_and_is_not_filtered()
    {
        var database = Available();

        await using var connection = await database.OpenWithoutSpaceContextAsync(
            TestContext.Current.CancellationToken);

        var unique = await ScalarAsync(
            connection,
            "SELECT CAST(i.is_unique AS int) FROM sys.indexes i WHERE i.name = @name",
            SchemaNames.AccountEmailUniqueIndex,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, unique);

        // NOT filtered, and that is FR-3 rather than an omission: a filtered index is how a
        // soft-delete tombstone would keep a deleted Account's address occupied, and FR-3
        // requires that address to be reusable by a new Account inheriting nothing.
        var filtered = await ScalarAsync(
            connection,
            "SELECT CAST(i.has_filter AS int) FROM sys.indexes i WHERE i.name = @name",
            SchemaNames.AccountEmailUniqueIndex,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, filtered);
    }

    private RegistrationDatabase Available()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        return fixture.Database!;
    }

    private static async Task<int?> ScalarAsync(
        SqlConnection connection,
        string sql,
        string name,
        CancellationToken cancellationToken,
        string? policy = null)
    {
        await using var command = connection.CreateCommand();

        // CA2100 disabled: every `sql` this file passes is a catalogue query written as a literal
        // a few lines above the call, and every value that varies is a parameter. There is no
        // caller outside this class - it is private - and no path by which anything a person
        // types reaches the command text.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
        command.CommandText = sql;
#pragma warning restore CA2100

        command.Parameters.Add(new SqlParameter("@name", name));

        if (policy is not null)
        {
            command.Parameters.Add(new SqlParameter("@policy", policy));
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result is null or DBNull ? null : Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<List<string>> ReadNamesAsync(
        SqlConnection connection,
        string sql,
        string name,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();

        // CA2100 disabled: every `sql` this file passes is a catalogue query written as a literal
        // a few lines above the call, and every value that varies is a parameter. There is no
        // caller outside this class - it is private - and no path by which anything a person
        // types reaches the command text.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
        command.CommandText = sql;
#pragma warning restore CA2100

        command.Parameters.Add(new SqlParameter("@name", name));

        var names = new List<string>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
