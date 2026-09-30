using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// Story 1.5's schema: the composite key a same-Space reference to a Membership targets, and the
/// serial-plan setting that removes the <c>SESSION_CONTEXT</c> parallel-plan defect. AD-2 / AR-7,
/// AR-8.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every
/// migration here is - see <see cref="InitialSchema"/> for why the scaffolded form cannot stand.
/// </para>
/// <para>
/// <b>No new table, so no new policy.</b> The two Space-scoped tables already carry FILTER and
/// BLOCK predicates from <see cref="InitialSchema"/>, and
/// <c>SpaceIsolationSchemaTests.Every_table_with_a_SpaceId_column_is_covered_by_the_policy</c>
/// fails the build the moment a table with a <c>SpaceId</c> arrives without them.
/// </para>
/// <para>
/// <b>No migration is applied at startup</b> (AR-36). Story 1.10 applies them as an explicit
/// deploy step.
/// </para>
/// </remarks>
public partial class ActiveSpaceResolution : Migration
{
    private static readonly string[] SpaceAndIdColumns = ["SpaceId", "Id"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // AD-2's composite foreign keys, enabled. A reference to a Membership from another
        // Space-scoped row - FR-21's Assignee, story 4.6, is the first - must be the pair
        // (SpaceId, MembershipId) so that "same Space" holds by construction. A composite foreign
        // key needs a matching unique key on the principal, and Membership's primary key is Id
        // alone.
        migrationBuilder.AddUniqueConstraint(
            name: "AK_Membership_SpaceId_Id",
            table: "Membership",
            columns: SpaceAndIdColumns);

        // AD-2 (ARCHITECTURE-SPINE.md:83): a parallel plan reading SESSION_CONTEXT() on a
        // pool-reset session can return another Space's rows, silently and successfully. MAXDOP 1
        // removes that class of defect for every query against this database.
        //
        // Set HERE, in a migration, rather than in deployment configuration, so that it travels
        // with the schema: every database built from these migrations - the test fixture's, a
        // developer's, and production once story 1.10 applies them as a deploy step - carries it,
        // and none can be created without it. Relaxing it is AR-40c's decision and story 1.10's,
        // and the standing rule is to treat it as unrelaxable until Azure SQL's exposure is
        // confirmed and the pooled-connection isolation case is green.
        //
        // Outside the migration's transaction: a database-scoped configuration change is a
        // database-level statement rather than a data change, and is issued on its own.
        migrationBuilder.Sql(
            "ALTER DATABASE SCOPED CONFIGURATION SET MAXDOP = 1;",
            suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // 0 is SQL Server's default: let the server choose.
        migrationBuilder.Sql(
            "ALTER DATABASE SCOPED CONFIGURATION SET MAXDOP = 0;",
            suppressTransaction: true);

        migrationBuilder.DropUniqueConstraint(
            name: "AK_Membership_SpaceId_Id",
            table: "Membership");
    }
}
