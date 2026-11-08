using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// A Task's Assignee, the List View's Assignee sort index, and the <c>NotificationSend</c> record under the Space policy.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every migration here is - see
/// <see cref="InitialSchema"/> for why the scaffolded form cannot stand.
/// </para>
/// <para>
/// <b>The Assignee carries its Space</b>: <c>(SpaceId, AssigneeMembershipId)</c> references the Membership's
/// <c>(SpaceId, Id)</c>, so another Space's Membership cannot be stored. NO ACTION: <c>SET NULL</c> would null the
/// <c>SpaceId</c> the key shares, and <c>CASCADE</c> would delete the Task and add a second cascade path from <c>Space</c>
/// (1785). The removal and the leave unassign explicitly before their <c>DELETE</c>; the <c>(SpaceId,
/// AssigneeMembershipId)</c> index EF adds for the key serves that statement and the key's own check on a Membership's
/// deletion.
/// </para>
/// <para>
/// <b>The Assignee sort's index</b> is <c>(SpaceId, ProjectId, AssigneeMembershipId, Id)</c>, including the Status and due
/// date, as the Label sort's includes the other filters' columns.
/// </para>
/// <para>
/// <b><c>NotificationSend</c> holds the Space, the kind and the time, and nothing else</b>, with the three predicates every
/// Space table carries - FILTER, BLOCK AFTER INSERT and BLOCK AFTER UPDATE - and a cascading key to <c>Space</c>, so a
/// Space's erasure takes its records. Its time is the database clock's, in UTC.
/// </para>
/// <para>
/// <b>Expand only.</b> A nullable column no earlier statement names, an index, a key every existing (unassigned) row
/// satisfies, and a new table; nothing to backfill. <b>Literal names rather than <c>SchemaNames</c></b>, for the reason
/// <see cref="InitialSchema"/> records. <b>No migration is applied at startup</b>.
/// </para>
/// </remarks>
public partial class TaskAssignee : Migration
{
    private static readonly string[] AssigneeKeyColumns = ["SpaceId", "AssigneeMembershipId"];

    private static readonly string[] MembershipKeyColumns = ["SpaceId", "Id"];

    private static readonly string[] AssigneeSortColumns = ["SpaceId", "ProjectId", "AssigneeMembershipId", "Id"];

    private static readonly string[] AssigneeSortIncludes = ["StatusId", "DueDate"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "AssigneeMembershipId",
            table: "Task",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "NotificationSend",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SpaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SWITCHOFFSET(SYSDATETIMEOFFSET(), '+00:00')"),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NotificationSend", x => x.Id);
                table.ForeignKey(
                    name: "FK_NotificationSend_Space_SpaceId",
                    column: x => x.SpaceId,
                    principalTable: "Space",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_Task_SpaceId_AssigneeMembershipId", table: "Task", columns: AssigneeKeyColumns);

        migrationBuilder.CreateIndex(name: "IX_Task_SpaceId_ProjectId_AssigneeMembershipId_Id", table: "Task", columns: AssigneeSortColumns)
            .Annotation("SqlServer:Include", AssigneeSortIncludes);

        migrationBuilder.CreateIndex(name: "IX_NotificationSend_SpaceId", table: "NotificationSend", column: "SpaceId");

        migrationBuilder.AddForeignKey(
            name: "FK_Task_Membership_SpaceId_AssigneeMembershipId",
            table: "Task",
            columns: AssigneeKeyColumns,
            principalTable: "Membership",
            principalColumns: MembershipKeyColumns);

        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                ADD FILTER PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[NotificationSend],
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[NotificationSend] AFTER INSERT,
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[NotificationSend] AFTER UPDATE;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The predicates first: they are bound to the table's column, so the table cannot go while the policy still names it.
        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                DROP FILTER PREDICATE ON dbo.[NotificationSend],
                DROP BLOCK PREDICATE ON dbo.[NotificationSend] AFTER INSERT,
                DROP BLOCK PREDICATE ON dbo.[NotificationSend] AFTER UPDATE;
            """);

        migrationBuilder.DropForeignKey(name: "FK_Task_Membership_SpaceId_AssigneeMembershipId", table: "Task");

        migrationBuilder.DropTable(name: "NotificationSend");

        migrationBuilder.DropIndex(name: "IX_Task_SpaceId_AssigneeMembershipId", table: "Task");

        migrationBuilder.DropIndex(name: "IX_Task_SpaceId_ProjectId_AssigneeMembershipId_Id", table: "Task");

        migrationBuilder.DropColumn(name: "AssigneeMembershipId", table: "Task");
    }
}
