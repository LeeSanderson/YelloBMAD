using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// A Project's Status delta: Project-owned <c>StatusDefinition</c> rows, the <c>StatusDeltaOperation</c> table under the
/// Space policy with three predicates, and an explicit case-insensitive collation on the Status and Label name columns.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every migration here is - see
/// <see cref="InitialSchema"/> for why the scaffolded form cannot stand. <b>Literal names rather than
/// <c>SchemaNames</c></b>, for the reason <see cref="InitialSchema"/> records.
/// </para>
/// <para>
/// <b>The owning key is NO ACTION</b>: Space already cascades to <c>StatusDefinition</c>, so a cascade from <c>Project</c>
/// would be a second path (1785). The delta cascades from <c>Project</c> and is NO ACTION to <c>StatusDefinition</c> for the
/// same reason, and has no key to <c>Space</c>.
/// </para>
/// <para>
/// <b>The unique <c>(SpaceId, Name)</c> index becomes two filtered ones</b> - defaults by <c>(SpaceId, Name)</c>, owned rows by
/// <c>(SpaceId, OwnerProjectId, Name)</c> - so two Projects may each add a Status of one name.
/// </para>
/// <para>
/// <b>The collation is the one the columns already had</b> (the database's default), declared so a restore onto a
/// differently collated server cannot change it. No value changes. SQL Server refuses to change the collation of a column
/// an index covers (5074), and EF Core does not order that for itself, so each name index is dropped first and recreated
/// after.
/// </para>
/// <para>
/// <b>Expand only.</b> A nullable column and a new table break nothing the previous revision runs: it writes only defaults,
/// which the filtered index still keeps unique.
/// </para>
/// </remarks>
public partial class ProjectStatusDelta : Migration
{
    private static readonly string[] SpaceAndName = ["SpaceId", "Name"];

    private static readonly string[] SpaceOwnerAndName = ["SpaceId", "OwnerProjectId", "Name"];

    private static readonly string[] SpaceAndOwner = ["SpaceId", "OwnerProjectId"];

    private static readonly string[] SpaceAndId = ["SpaceId", "Id"];

    private static readonly string[] SpaceProjectStatusAndKind = ["SpaceId", "ProjectId", "StatusId", "Kind"];

    private static readonly string[] SpaceAndStatus = ["SpaceId", "StatusId"];

    private static readonly string[] SpaceAndAfterStatus = ["SpaceId", "AfterStatusId"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        DeclareNameCollations(migrationBuilder);

        migrationBuilder.AddColumn<Guid>(name: "OwnerProjectId", table: "StatusDefinition", type: "uniqueidentifier", nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_StatusDefinition_SpaceId_Name",
            table: "StatusDefinition",
            columns: SpaceAndName,
            unique: true,
            filter: "[OwnerProjectId] IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_StatusDefinition_SpaceId_OwnerProjectId_Name",
            table: "StatusDefinition",
            columns: SpaceOwnerAndName,
            unique: true,
            filter: "[OwnerProjectId] IS NOT NULL");

        migrationBuilder.AddForeignKey(
            name: "FK_StatusDefinition_Project_SpaceId_OwnerProjectId",
            table: "StatusDefinition",
            columns: SpaceAndOwner,
            principalTable: "Project",
            principalColumns: SpaceAndId);

        CreateDeltaTable(migrationBuilder);

        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                ADD FILTER PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[StatusDeltaOperation],
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[StatusDeltaOperation] AFTER INSERT,
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[StatusDeltaOperation] AFTER UPDATE;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The predicates first: they are bound to the table's column, so the table cannot go while the policy names it.
        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                DROP FILTER PREDICATE ON dbo.[StatusDeltaOperation],
                DROP BLOCK PREDICATE ON dbo.[StatusDeltaOperation] AFTER INSERT,
                DROP BLOCK PREDICATE ON dbo.[StatusDeltaOperation] AFTER UPDATE;
            """);

        migrationBuilder.DropTable(name: "StatusDeltaOperation");

        migrationBuilder.DropForeignKey(name: "FK_StatusDefinition_Project_SpaceId_OwnerProjectId", table: "StatusDefinition");
        migrationBuilder.DropIndex(name: "IX_StatusDefinition_SpaceId_OwnerProjectId_Name", table: "StatusDefinition");
        migrationBuilder.DropIndex(name: "IX_StatusDefinition_SpaceId_Name", table: "StatusDefinition");
        migrationBuilder.DropColumn(name: "OwnerProjectId", table: "StatusDefinition");

        migrationBuilder.DropIndex(name: "IX_Label_SpaceId_Name", table: "Label");

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "StatusDefinition",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(100)",
            oldMaxLength: 100,
            oldCollation: "SQL_Latin1_General_CP1_CI_AS");

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Label",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(50)",
            oldMaxLength: 50,
            oldCollation: "SQL_Latin1_General_CP1_CI_AS");

        migrationBuilder.CreateIndex(name: "IX_Label_SpaceId_Name", table: "Label", columns: SpaceAndName, unique: true);
        migrationBuilder.CreateIndex(name: "IX_StatusDefinition_SpaceId_Name", table: "StatusDefinition", columns: SpaceAndName, unique: true);
    }

    /// <summary>
    /// Drops the two name indexes, declares the collation on both name columns, and recreates the Label's index. The
    /// Status definition's index comes back filtered, after the owning column exists.
    /// </summary>
    private static void DeclareNameCollations(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_StatusDefinition_SpaceId_Name", table: "StatusDefinition");
        migrationBuilder.DropIndex(name: "IX_Label_SpaceId_Name", table: "Label");

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "StatusDefinition",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: false,
            collation: "SQL_Latin1_General_CP1_CI_AS",
            oldClrType: typeof(string),
            oldType: "nvarchar(100)",
            oldMaxLength: 100);

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Label",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: false,
            collation: "SQL_Latin1_General_CP1_CI_AS",
            oldClrType: typeof(string),
            oldType: "nvarchar(50)",
            oldMaxLength: 50);

        migrationBuilder.CreateIndex(name: "IX_Label_SpaceId_Name", table: "Label", columns: SpaceAndName, unique: true);
    }

    private static void CreateDeltaTable(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StatusDeltaOperation",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SpaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kind = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, collation: "SQL_Latin1_General_CP1_CI_AS"),
                AfterStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Sequence = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StatusDeltaOperation", x => x.Id);
                table.ForeignKey(
                    name: "FK_StatusDeltaOperation_Project_SpaceId_ProjectId",
                    columns: x => new { x.SpaceId, x.ProjectId },
                    principalTable: "Project",
                    principalColumns: SpaceAndId,
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_StatusDeltaOperation_StatusDefinition_SpaceId_AfterStatusId",
                    columns: x => new { x.SpaceId, x.AfterStatusId },
                    principalTable: "StatusDefinition",
                    principalColumns: SpaceAndId);
                table.ForeignKey(
                    name: "FK_StatusDeltaOperation_StatusDefinition_SpaceId_StatusId",
                    columns: x => new { x.SpaceId, x.StatusId },
                    principalTable: "StatusDefinition",
                    principalColumns: SpaceAndId);
            });

        migrationBuilder.CreateIndex(name: "IX_StatusDeltaOperation_SpaceId_AfterStatusId", table: "StatusDeltaOperation", columns: SpaceAndAfterStatus);

        migrationBuilder.CreateIndex(
            name: "IX_StatusDeltaOperation_SpaceId_ProjectId_StatusId_Kind",
            table: "StatusDeltaOperation",
            columns: SpaceProjectStatusAndKind,
            unique: true);

        migrationBuilder.CreateIndex(name: "IX_StatusDeltaOperation_SpaceId_StatusId", table: "StatusDeltaOperation", columns: SpaceAndStatus);
    }
}
