using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// Story 1.6's schema: the <c>AccessRefusal</c> record every Space-boundary refusal writes, and the
/// Space-scoped <c>SpacePresence</c> table NFR-8's concurrent-Sessions bound counts. AD-20 / AR-11;
/// AD-25 / AR-30.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every
/// migration here is - see <see cref="InitialSchema"/> for why the scaffolded form cannot stand.
/// </para>
/// <para>
/// <b><c>AccessRefusal</c> is not Space-scoped, and gets no predicate.</b> Its target column is
/// <c>TargetSpaceId</c>, not <c>SpaceId</c>, and it has no foreign key to <c>Space</c>: a refusal for
/// a Space that does not exist is recorded like any other, and the retention purge has to see every
/// row. <c>SpaceIsolationSchemaTests.Every_table_with_a_SpaceId_column_is_covered_by_the_policy</c>
/// detects Space-scoped tables by that exact column name, so this one is outside it by construction.
/// </para>
/// <para>
/// <b><c>SpacePresence</c> is Space-scoped, and gets both predicates</b> - FILTER and BLOCK AFTER
/// INSERT - added to the existing policy, exactly as <c>Membership</c> and <c>StatusDefinition</c>
/// carry them.
/// </para>
/// <para>
/// <b>Literal names rather than <c>SchemaNames</c></b>, for the reason <see cref="InitialSchema"/>
/// records: an applied migration is history. <b>No migration is applied at startup</b> (AR-36).
/// </para>
/// </remarks>
public partial class SpaceBoundaryRefusals : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AccessRefusal",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TargetSpaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Capability = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Outcome = table.Column<int>(type: "int", nullable: false),
                Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_AccessRefusal", x => x.Id));

        // The retention purge deletes by age, and seeks on this rather than scanning the table.
        migrationBuilder.CreateIndex(
            name: "IX_AccessRefusal_OccurredAt",
            table: "AccessRefusal",
            column: "OccurredAt");

        migrationBuilder.CreateTable(
            name: "SpacePresence",
            columns: table => new
            {
                SpaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SpacePresence", x => new { x.SpaceId, x.AccountId });
                table.ForeignKey(
                    name: "FK_SpacePresence_Space_SpaceId",
                    column: x => x.SpaceId,
                    principalTable: "Space",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        // Both halves of the predicate, as every Space-scoped table carries them: FILTER hides another
        // Space's presences from the count, and BLOCK refuses a presence written into a Space the
        // session context does not name.
        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                ADD FILTER PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.SpacePresence,
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.SpacePresence AFTER INSERT;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The predicates first: they are bound to the table's column, so the table cannot go while
        // the policy still names it.
        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                DROP FILTER PREDICATE ON dbo.SpacePresence,
                DROP BLOCK PREDICATE ON dbo.SpacePresence AFTER INSERT;
            """);

        migrationBuilder.DropTable(name: "SpacePresence");
        migrationBuilder.DropTable(name: "AccessRefusal");
    }
}
