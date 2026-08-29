using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// The first schema in the repository: Account, Space, Membership and StatusDefinition, their
/// indexes, and the row-level security policy that scopes the last two to a Space.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand.</b> That is
/// a per-migration cost later stories will also pay, and it is deliberate rather than an
/// oversight. EF writes <c>// &lt;auto-generated /&gt;</c> into the <c>.Designer.cs</c> and the
/// model snapshot - which is why those two are exempt from analysis - and pointedly does not
/// write it here, because this is the file it expects a human to edit. Adding the marker to
/// silence the analysers would be a false claim about a file carrying hand-written, load-bearing
/// DDL, and there is no narrower lever available: this repository has no <c>.editorconfig</c> on
/// purpose (the standard is a <c>GlobalPackageReference</c>), so a per-folder analyser exemption
/// would mean forking the standard to hide DDL nobody needs it to police.
/// </para>
/// <para>
/// What that costs, concretely: a file-scoped namespace, real documentation instead of
/// <c>&lt;inheritdoc /&gt;</c> on the type, the composite index columns lifted into static fields
/// (CA1861), and <c>Up</c> split so no method exceeds the 80-line limit (S138). None of it
/// changes a single statement the migration executes.
/// </para>
/// <para>
/// <b>No migration is applied at startup</b> (AR-36). Story 1.10 applies them as an explicit
/// deploy step.
/// </para>
/// </remarks>
public partial class InitialSchema : Migration
{
    private static readonly string[] SpaceAndAccountColumns = ["SpaceId", "AccountId"];

    private static readonly string[] SpaceAndNameColumns = ["SpaceId", "Name"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateTables(migrationBuilder);
        CreateIndexes(migrationBuilder);
        CreateSpaceIsolationPolicy(migrationBuilder);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Policy first, then the function, then the tables. The policy holds a schema-bound
        // reference to the function and its predicates are bound to the columns, so the reverse
        // order fails partway and leaves a half-dropped schema no later migration can reason
        // about.
        migrationBuilder.Sql("DROP SECURITY POLICY dbo.SpaceIsolationPolicy;");
        migrationBuilder.Sql("DROP FUNCTION dbo.fn_SpaceIsolationPredicate;");

        migrationBuilder.DropTable(name: "Membership");
        migrationBuilder.DropTable(name: "StatusDefinition");
        migrationBuilder.DropTable(name: "Account");
        migrationBuilder.DropTable(name: "Space");
    }

    private static void CreateTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Account",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmailAddress = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                NormalizedEmailAddress = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_Account", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Space",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_Space", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Membership",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SpaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Membership", x => x.Id);

                // Restrict, not cascade. SQL Server refuses two cascade paths into one table, so
                // one of this pair has to be restricted - and this is the right one, because
                // FR-3 makes account deletion an explicit sequence rather than a side effect.
                table.ForeignKey(
                    name: "FK_Membership_Account_AccountId",
                    column: x => x.AccountId,
                    principalTable: "Account",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                // Cascade: a Membership in no Space cannot exist, so deleting a Space has to
                // take its Memberships with it.
                table.ForeignKey(
                    name: "FK_Membership_Space_SpaceId",
                    column: x => x.SpaceId,
                    principalTable: "Space",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "StatusDefinition",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SpaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Position = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StatusDefinition", x => x.Id);
                table.ForeignKey(
                    name: "FK_StatusDefinition_Space_SpaceId",
                    column: x => x.SpaceId,
                    principalTable: "Space",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });
    }

    private static void CreateIndexes(MigrationBuilder migrationBuilder)
    {
        // FR-1's uniqueness, on the normalised column. Not filtered, and never a soft-delete
        // tombstone: FR-3 requires a deleted Account's address to be reusable.
        migrationBuilder.CreateIndex(
            name: "UX_Account_NormalizedEmailAddress",
            table: "Account",
            column: "NormalizedEmailAddress",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Membership_AccountId",
            table: "Membership",
            column: "AccountId");

        migrationBuilder.CreateIndex(
            name: "IX_Membership_SpaceId_AccountId",
            table: "Membership",
            columns: SpaceAndAccountColumns,
            unique: true);

        // AD-5 / AR-12: exactly one Owner per Space, as a database fact rather than an
        // application check. Application code cannot hold this under concurrency, and AD-22
        // requires "an Account holding zero Spaces or two" to be a failed transaction - which is
        // only true if the constraint is somewhere a transaction can fail.
        migrationBuilder.CreateIndex(
            name: "UX_Membership_SpaceId_Owner",
            table: "Membership",
            column: "SpaceId",
            unique: true,
            filter: "[Role] = N'Owner'");

        migrationBuilder.CreateIndex(
            name: "IX_StatusDefinition_SpaceId_Name",
            table: "StatusDefinition",
            columns: SpaceAndNameColumns,
            unique: true);
    }

    /// <summary>
    /// Row-level security for the Space-scoped tables this migration creates. AD-2 / AR-5..AR-8,
    /// and the whole basis of NFR-1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The policy ships in the same migration as the tables it protects.</b> The readiness
    /// report (<c>:1003</c>) states "every one carries a schema test asserting the RLS policy in
    /// the same story", and AD-2 (<c>:86</c>) is blunt: "A Space-scoped table without an RLS
    /// policy fails the schema test." A migration that created the tables and left the policy to
    /// a later one would ship a window, however short, in which Space-scoped rows are readable
    /// across Spaces.
    /// </para>
    /// <para>
    /// <b>Which tables, and why not all four.</b> Lee's decision of 2026-08-28: Membership and
    /// StatusDefinition are Space-scoped and get the policy here. Account and Space deliberately
    /// do not, and that is recorded rather than silently omitted - the spine never states whether
    /// they carry policies or under which predicate, and Membership additionally has to stay
    /// readable <i>across</i> Spaces for story 1.7's Space switcher under a
    /// <c>SESSION_CONTEXT('AccountId')</c> predicate AD-24 has not been amended to describe.
    /// That amendment is readiness issue 3 and is already due before Epic 3; pre-empting it
    /// inside a migration would be inventing an architecture decision.
    /// </para>
    /// <para>
    /// <b>Literal names rather than <c>SchemaNames</c>, deliberately.</b> An applied migration is
    /// a historical record - it has already run against real databases - so deriving its text
    /// from a constant a later story might rename would rewrite history instead of changing the
    /// future. <c>SchemaNames</c> is what the live schema is asserted against, so a rename with
    /// no accompanying migration surfaces as a failing schema test.
    /// </para>
    /// </remarks>
    private static void CreateSpaceIsolationPolicy(MigrationBuilder migrationBuilder)
    {
        // Its own batch: CREATE FUNCTION has to be the first statement in one, and each Sql call
        // is issued as a separate command.
        migrationBuilder.Sql("""
            CREATE FUNCTION dbo.fn_SpaceIsolationPredicate(@SpaceId uniqueidentifier)
            RETURNS TABLE
            WITH SCHEMABINDING
            AS
            RETURN
                SELECT 1 AS IsAccessible
                WHERE @SpaceId = CAST(SESSION_CONTEXT(N'SpaceId') AS uniqueidentifier);
            """);

        // FILTER hides rows belonging to another Space from every read; BLOCK ... AFTER INSERT
        // refuses a write that would place a row in a Space the session context does not name.
        // Both halves are needed: a filter alone lets a caller write a row into another Space and
        // merely not see it afterwards, which is the more damaging direction.
        migrationBuilder.Sql("""
            CREATE SECURITY POLICY dbo.SpaceIsolationPolicy
                ADD FILTER PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.Membership,
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.Membership AFTER INSERT,
                ADD FILTER PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.StatusDefinition,
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.StatusDefinition AFTER INSERT
                WITH (STATE = ON);
            """);
    }
}
