using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// Story 2.2's <c>Task</c> table, under the Space policy with three predicates, and the
/// <c>StatusDefinition</c> key its Status reference needs. FR-19, FR-28; AD-2, AD-15, AR-5, AR-8, AR-21.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every
/// migration here is - see <see cref="InitialSchema"/> for why the scaffolded form cannot stand.
/// </para>
/// <para>
/// <b>Both references carry the Space</b> (AR-8): <c>(SpaceId, ProjectId)</c> to <c>AK_Project_SpaceId_Id</c>
/// (story 2.1), cascading, and <c>(SpaceId, StatusId)</c> to the new <c>AK_StatusDefinition_SpaceId_Id</c>,
/// not cascading. There is no foreign key to <c>Space</c>: SQL Server refuses a second cascade path into
/// one table (error 1785), and the Project reference already constrains the Space.
/// </para>
/// <para>
/// <b>The position key is <c>varchar</c> under <c>Latin1_General_100_BIN2</c></b>, declared here because a
/// collation cannot be retrofitted onto a populated, indexed column (AR-21), and unique within its
/// <c>(SpaceId, ProjectId, StatusId)</c> (AD-15).
/// </para>
/// <para>
/// <b>Three predicates, like <c>Project</c></b>: FILTER, BLOCK AFTER INSERT, and BLOCK AFTER UPDATE, which
/// makes "belongs permanently to one Space" (AC2) a database fact.
/// </para>
/// <para>
/// <b>Named <c>TaskTable</c>, not <c>Tasks</c></b>: a type called <c>Tasks</c> shadows the
/// <c>System.Threading.Tasks</c> namespace (S4041).
/// </para>
/// <para>
/// <b>Expand only.</b> A new table and a new unique constraint over an already-unique column break nothing
/// the previous revision runs. <b>Literal names rather than <c>SchemaNames</c></b>, for the reason
/// <see cref="InitialSchema"/> records. <b>No migration is applied at startup</b> (AR-36).
/// </para>
/// </remarks>
public partial class TaskTable : Migration
{
    private static readonly string[] SpaceAndId = ["SpaceId", "Id"];

    private static readonly string[] ColumnColumns = ["SpaceId", "ProjectId", "StatusId", "PositionKey"];

    private static readonly string[] StatusReferenceColumns = ["SpaceId", "StatusId"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddUniqueConstraint(name: "AK_StatusDefinition_SpaceId_Id", table: "StatusDefinition", columns: SpaceAndId);

        migrationBuilder.CreateTable(
            name: "Task",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SpaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                PositionKey = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false, collation: "Latin1_General_100_BIN2"),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Task", x => x.Id);
                table.ForeignKey(
                    name: "FK_Task_Project_SpaceId_ProjectId",
                    columns: x => new { x.SpaceId, x.ProjectId },
                    principalTable: "Project",
                    principalColumns: SpaceAndId,
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Task_StatusDefinition_SpaceId_StatusId",
                    columns: x => new { x.SpaceId, x.StatusId },
                    principalTable: "StatusDefinition",
                    principalColumns: SpaceAndId);
            });

        migrationBuilder.CreateIndex(name: "IX_Task_SpaceId_ProjectId_StatusId_PositionKey", table: "Task", columns: ColumnColumns, unique: true);

        migrationBuilder.CreateIndex(name: "IX_Task_SpaceId_StatusId", table: "Task", columns: StatusReferenceColumns);

        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                ADD FILTER PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[Task],
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[Task] AFTER INSERT,
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[Task] AFTER UPDATE;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The predicates first: they are bound to the table's column, so the table cannot go while the
        // policy still names it.
        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                DROP FILTER PREDICATE ON dbo.[Task],
                DROP BLOCK PREDICATE ON dbo.[Task] AFTER INSERT,
                DROP BLOCK PREDICATE ON dbo.[Task] AFTER UPDATE;
            """);

        migrationBuilder.DropTable(name: "Task");

        migrationBuilder.DropUniqueConstraint(name: "AK_StatusDefinition_SpaceId_Id", table: "StatusDefinition");
    }
}
