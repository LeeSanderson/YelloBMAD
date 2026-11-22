using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// A Task's description: the <c>TaskDescriptionChange</c> log under the Space policy with three predicates, and the
/// <c>Task.Description</c> projection.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every migration here is - see
/// <see cref="InitialSchema"/> for why the scaffolded form cannot stand. <b>Literal names rather than
/// <c>SchemaNames</c></b>, for the reason <see cref="InitialSchema"/> records.
/// </para>
/// <para>
/// <b>The log cascades from its Task through <c>(SpaceId, TaskId)</c> and has no key to <c>Space</c></b> (1785). Its author
/// key sets null, as the Invitation issuer's does, so Account erasure keeps every row and its captured name.
/// </para>
/// <para>
/// <b>Expand only.</b> The projection is <c>NOT NULL DEFAULT N''</c>, so the previous revision, which never names it, still
/// inserts Tasks; the log is a new table it never reads. No length is set on the projection: none is stated.
/// </para>
/// </remarks>
public partial class TaskDescriptionLog : Migration
{
    private static readonly string[] SpaceAndId = ["SpaceId", "Id"];

    private static readonly string[] SpaceTaskAndSequence = ["SpaceId", "TaskId", "Sequence"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "Task",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: string.Empty);

        migrationBuilder.CreateTable(
            name: "TaskDescriptionChange",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SpaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sequence = table.Column<long>(type: "bigint", nullable: false),
                Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                Payload = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                AuthorAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                AuthorName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TaskDescriptionChange", x => x.Id);
                table.ForeignKey(
                    name: "FK_TaskDescriptionChange_Account_AuthorAccountId",
                    column: x => x.AuthorAccountId,
                    principalTable: "Account",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_TaskDescriptionChange_Task_SpaceId_TaskId",
                    columns: x => new { x.SpaceId, x.TaskId },
                    principalTable: "Task",
                    principalColumns: SpaceAndId,
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_TaskDescriptionChange_AuthorAccountId",
            table: "TaskDescriptionChange",
            column: "AuthorAccountId");

        migrationBuilder.CreateIndex(
            name: "IX_TaskDescriptionChange_SpaceId_TaskId_Sequence",
            table: "TaskDescriptionChange",
            columns: SpaceTaskAndSequence,
            unique: true);

        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                ADD FILTER PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[TaskDescriptionChange],
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[TaskDescriptionChange] AFTER INSERT,
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[TaskDescriptionChange] AFTER UPDATE;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The predicates first: they are bound to the table's column, so the table cannot go while the policy names it.
        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                DROP FILTER PREDICATE ON dbo.[TaskDescriptionChange],
                DROP BLOCK PREDICATE ON dbo.[TaskDescriptionChange] AFTER INSERT,
                DROP BLOCK PREDICATE ON dbo.[TaskDescriptionChange] AFTER UPDATE;
            """);

        migrationBuilder.DropTable(name: "TaskDescriptionChange");

        migrationBuilder.DropColumn(name: "Description", table: "Task");
    }
}
