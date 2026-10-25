using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// Story 2.1's <c>Project</c> table, under the Space policy with three predicates. FR-17, FR-18;
/// AD-2, AR-5, AR-8.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every
/// migration here is - see <see cref="InitialSchema"/> for why the scaffolded form cannot stand.
/// </para>
/// <para>
/// <b>Three predicates, where every other Space-scoped table has two.</b> FILTER hides another Space's
/// Projects, BLOCK AFTER INSERT refuses a Project written into a Space the session context does not
/// name, and BLOCK AFTER UPDATE - new to this repository - refuses an update whose resulting row names
/// another Space. That last one makes "no operation moves a Project" a database fact rather than a
/// convention (story 2.1, AC7, open question 5).
/// </para>
/// <para>
/// <b>The <c>(SpaceId, Id)</c> unique constraint is for story 2.2's <c>Task</c></b>, whose composite
/// foreign key AR-8 requires: a Task can then never reference a Project in another Space.
/// </para>
/// <para>
/// <b>Expand only.</b> A new table breaks nothing the previous revision runs. <b>Literal names rather
/// than <c>SchemaNames</c></b>, for the reason <see cref="InitialSchema"/> records: an applied migration
/// is history. <b>No migration is applied at startup</b> (AR-36).
/// </para>
/// </remarks>
public partial class Projects : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Project",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SpaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Project", x => x.Id);
                table.UniqueConstraint("AK_Project_SpaceId_Id", x => new { x.SpaceId, x.Id });
                table.ForeignKey(
                    name: "FK_Project_Space_SpaceId",
                    column: x => x.SpaceId,
                    principalTable: "Space",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                ADD FILTER PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.Project,
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.Project AFTER INSERT,
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.Project AFTER UPDATE;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The predicates first: they are bound to the table's column, so the table cannot go while the
        // policy still names it.
        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                DROP FILTER PREDICATE ON dbo.Project,
                DROP BLOCK PREDICATE ON dbo.Project AFTER INSERT,
                DROP BLOCK PREDICATE ON dbo.Project AFTER UPDATE;
            """);

        migrationBuilder.DropTable(name: "Project");
    }
}
