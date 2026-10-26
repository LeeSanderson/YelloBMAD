using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// Story 2.4's <c>Label</c> and <c>TaskLabel</c> tables, each under the Space policy with three predicates, and the
/// <c>Task</c> key the application references. FR-22; AD-2, AR-5, AR-8.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every migration here
/// is - see <see cref="InitialSchema"/> for why the scaffolded form cannot stand.
/// </para>
/// <para>
/// <b>A Label is per Space</b>: a non-nullable <c>SpaceId</c> referencing <c>Space</c> with cascade, unique by
/// <c>(SpaceId, Name)</c> under the database's case-insensitive collation (open question 2), and keyed
/// <c>(SpaceId, Id)</c> for the application to reference (AR-8).
/// </para>
/// <para>
/// <b>Both of an application's references carry the Space</b> (AR-8): <c>(SpaceId, TaskId)</c> to the new
/// <c>AK_Task_SpaceId_Id</c>, cascading - deleting a Task removes its applications - and
/// <c>(SpaceId, LabelId)</c> to <c>AK_Label_SpaceId_Id</c>, <b>not</b> cascading: a Label in use must not be
/// deletable (AC3), and SQL Server refuses a second cascade path into one table anyway (error 1785).
/// </para>
/// <para>
/// <b>Three predicates on each table, like <c>Task</c></b>: FILTER, BLOCK AFTER INSERT and BLOCK AFTER UPDATE, so
/// neither a Label nor an application can be written into, or moved to, another Space.
/// </para>
/// <para>
/// <b>Named <c>LabelTables</c>, not <c>Labels</c></b>: a type called <c>Labels</c> shadows the
/// <c>Yello.Domain.Labels</c> namespace (S4041).
/// </para>
/// <para>
/// <b>Expand only.</b> Two new tables and a unique constraint over an already-unique column break nothing the
/// previous revision runs. <b>Literal names rather than <c>SchemaNames</c></b>, for the reason
/// <see cref="InitialSchema"/> records. <b>No migration is applied at startup</b> (AR-36).
/// </para>
/// </remarks>
public partial class LabelTables : Migration
{
    private static readonly string[] SpaceAndId = ["SpaceId", "Id"];

    private static readonly string[] SpaceAndName = ["SpaceId", "Name"];

    private static readonly string[] InUseProbeColumns = ["SpaceId", "LabelId", "TaskId"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddUniqueConstraint(name: "AK_Task_SpaceId_Id", table: "Task", columns: SpaceAndId);

        migrationBuilder.CreateTable(
            name: "Label",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SpaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Colour = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Label", x => x.Id);
                table.UniqueConstraint("AK_Label_SpaceId_Id", x => new { x.SpaceId, x.Id });
                table.ForeignKey(
                    name: "FK_Label_Space_SpaceId",
                    column: x => x.SpaceId,
                    principalTable: "Space",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TaskLabel",
            columns: table => new
            {
                SpaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LabelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AppliedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TaskLabel", x => new { x.SpaceId, x.TaskId, x.LabelId });
                table.ForeignKey(
                    name: "FK_TaskLabel_Label_SpaceId_LabelId",
                    columns: x => new { x.SpaceId, x.LabelId },
                    principalTable: "Label",
                    principalColumns: SpaceAndId);
                table.ForeignKey(
                    name: "FK_TaskLabel_Task_SpaceId_TaskId",
                    columns: x => new { x.SpaceId, x.TaskId },
                    principalTable: "Task",
                    principalColumns: SpaceAndId,
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_Label_SpaceId_Name", table: "Label", columns: SpaceAndName, unique: true);

        migrationBuilder.CreateIndex(name: "IX_TaskLabel_SpaceId_LabelId_TaskId", table: "TaskLabel", columns: InUseProbeColumns);

        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                ADD FILTER PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[Label],
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[Label] AFTER INSERT,
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[Label] AFTER UPDATE,
                ADD FILTER PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[TaskLabel],
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[TaskLabel] AFTER INSERT,
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[TaskLabel] AFTER UPDATE;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The predicates first: they are bound to the tables' columns, so a table cannot go while the policy still
        // names it.
        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                DROP FILTER PREDICATE ON dbo.[TaskLabel],
                DROP BLOCK PREDICATE ON dbo.[TaskLabel] AFTER INSERT,
                DROP BLOCK PREDICATE ON dbo.[TaskLabel] AFTER UPDATE,
                DROP FILTER PREDICATE ON dbo.[Label],
                DROP BLOCK PREDICATE ON dbo.[Label] AFTER INSERT,
                DROP BLOCK PREDICATE ON dbo.[Label] AFTER UPDATE;
            """);

        migrationBuilder.DropTable(name: "TaskLabel");

        migrationBuilder.DropTable(name: "Label");

        migrationBuilder.DropUniqueConstraint(name: "AK_Task_SpaceId_Id", table: "Task");
    }
}
