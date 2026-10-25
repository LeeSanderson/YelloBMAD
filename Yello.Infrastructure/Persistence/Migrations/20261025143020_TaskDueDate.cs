using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// Story 2.3's due date: one nullable <c>datetimeoffset</c> column on <c>dbo.[Task]</c>. FR-20; AR-34.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every
/// migration here is - see <see cref="InitialSchema"/> for why the scaffolded form cannot stand.
/// </para>
/// <para>
/// <b>Expand only.</b> The column is nullable and has no default, so the revision still serving - which
/// inserts Tasks without naming it - keeps working against the new schema (story 1.10's rule).
/// </para>
/// <para>
/// <b>Nothing changes in the security policy.</b> Its predicates are per table, not per column, so the
/// Task's FILTER and BLOCK predicates cover the new column as they stand. No index: sorting by due date is
/// story 2.10's, which decides one then.
/// </para>
/// </remarks>
public partial class TaskDueDate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DueDate",
            table: "Task",
            type: "datetimeoffset",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DueDate",
            table: "Task");
    }
}
