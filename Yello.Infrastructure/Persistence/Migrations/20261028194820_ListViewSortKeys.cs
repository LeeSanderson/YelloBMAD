using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// Story 2.10's List View sort keys: the Label sort key <c>FirstLabelName</c> on <c>dbo.[Task]</c>, backfilled from the
/// Labels each Task already carries, and one composite index per offered sort - <c>(SpaceId, ProjectId, &lt;sort
/// column&gt;, Id)</c>. FR-30; AR-22, AD-29; AC4-AC6, AC13, open questions 3 (a), 5 (a) and 7 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every migration here is - see
/// <see cref="InitialSchema"/> for why the scaffolded form cannot stand. The backfill is hand-written.
/// </para>
/// <para>
/// <b>Expand only</b> (story 1.10's rule). The column is nullable with no default, so the revision still serving - which
/// inserts and edits Tasks without naming it - keeps working against the new schema; the indexes change no statement it
/// runs. What that revision does <i>not</i> do is keep the key true on a Labels edit it serves during the rolling deploy
/// (open question 3, cost ii; ledgered to Lee): re-running the backfill statement below is idempotent and is the remedy.
/// </para>
/// <para>
/// <b>The backfill runs with the security policy switched off, inside this migration's transaction.</b> A migration's
/// connection carries no <c>SESSION_CONTEXT</c>, so the Task FILTER predicate hides every row from it and the
/// <c>UPDATE</c> would touch none; and the one line that sets a session key is <c>SpaceSessionContext</c>'s
/// (<c>SpaceResolutionSeamTests</c>), so the migration cannot name each Space in turn. The policy is switched off, the one
/// statement runs - set-based, each Task correlated to its own Space's <c>TaskLabel</c> and <c>Label</c> rows on
/// <c>SpaceId</c>, so no Label crosses a Space - and the policy is switched back on before the transaction commits. Every
/// other connection sees the policy on throughout: the state change is not visible until commit, and by then it is
/// <c>ON</c> again. If the statement fails, the transaction rolls back with the policy as it was.
/// </para>
/// <para>
/// <b>The key is chip order</b> (story 2.4, open question 12): Label name, then id, in the column's own collation - the
/// order <c>TaskLabelReads</c> lists a Task's Labels in, and the <c>ORDER BY</c> the Labels edit recomputes with.
/// </para>
/// <para>
/// <b>Indexes</b>: each <c>INCLUDE</c>s the other sorts' filter columns, so a Status or due-date filter under another
/// sort is tested in the index rather than by a lookup per candidate row (open question 4 (a)). <c>Down</c> drops the
/// three indexes and then the column; nothing else depends on it.
/// </para>
/// </remarks>
public partial class ListViewSortKeys : Migration
{
    private const string TaskTable = "Task";

    private const string FirstLabelName = "FirstLabelName";

    private const string StatusSortIndex = "IX_Task_SpaceId_ProjectId_StatusId_Id";

    private const string DueDateSortIndex = "IX_Task_SpaceId_ProjectId_DueDate_Id";

    private const string LabelSortIndex = "IX_Task_SpaceId_ProjectId_FirstLabelName_Id";

    private static readonly string[] StatusSortColumns = ["SpaceId", "ProjectId", "StatusId", "Id"];

    private static readonly string[] DueDateSortColumns = ["SpaceId", "ProjectId", "DueDate", "Id"];

    private static readonly string[] LabelSortColumns = ["SpaceId", "ProjectId", FirstLabelName, "Id"];

    private static readonly string[] StatusSortIncludes = ["DueDate"];

    private static readonly string[] DueDateSortIncludes = ["StatusId"];

    private static readonly string[] LabelSortIncludes = ["StatusId", "DueDate"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: FirstLabelName,
            table: TaskTable,
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.Sql("ALTER SECURITY POLICY dbo.SpaceIsolationPolicy WITH (STATE = OFF);");

        migrationBuilder.Sql("""
            UPDATE t SET FirstLabelName = (
                SELECT TOP (1) l.Name FROM dbo.[TaskLabel] tl
                JOIN dbo.[Label] l ON l.SpaceId = tl.SpaceId AND l.Id = tl.LabelId
                WHERE tl.SpaceId = t.SpaceId AND tl.TaskId = t.Id
                ORDER BY l.Name, l.Id)
            FROM dbo.[Task] t
            WHERE EXISTS (SELECT 1 FROM dbo.[TaskLabel] x WHERE x.SpaceId = t.SpaceId AND x.TaskId = t.Id);
            """);

        migrationBuilder.Sql("ALTER SECURITY POLICY dbo.SpaceIsolationPolicy WITH (STATE = ON);");

        migrationBuilder.CreateIndex(name: StatusSortIndex, table: TaskTable, columns: StatusSortColumns)
            .Annotation("SqlServer:Include", StatusSortIncludes);

        migrationBuilder.CreateIndex(name: DueDateSortIndex, table: TaskTable, columns: DueDateSortColumns)
            .Annotation("SqlServer:Include", DueDateSortIncludes);

        migrationBuilder.CreateIndex(name: LabelSortIndex, table: TaskTable, columns: LabelSortColumns)
            .Annotation("SqlServer:Include", LabelSortIncludes);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: LabelSortIndex, table: TaskTable);

        migrationBuilder.DropIndex(name: DueDateSortIndex, table: TaskTable);

        migrationBuilder.DropIndex(name: StatusSortIndex, table: TaskTable);

        migrationBuilder.DropColumn(name: FirstLabelName, table: TaskTable);
    }
}
