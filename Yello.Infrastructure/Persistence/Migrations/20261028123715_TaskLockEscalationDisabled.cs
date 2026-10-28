using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// Story 2.9's lock escalation: <c>dbo.[Task]</c> and <c>dbo.[TaskLabel]</c> never escalate a statement's row and key locks
/// to a table lock. AC17, open question 15 (a); <c>deferred-work.md:541</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every migration here is -
/// see <see cref="InitialSchema"/> for why the scaffolded form cannot stand.
/// </para>
/// <para>
/// <b>Why.</b> A Project is deleted whole by one statement (story 2.5, AC3 - never half a Project), and at the NFR-8 bound
/// that is 5,000 Tasks and their Label applications. Measured (<c>BoardPagingTests</c> PG-16): the deletion escalated to
/// an <c>OBJECT X</c> lock on both tables, so a Task written in any other Space queued behind it until it committed - a
/// cross-tenant wait. With escalation disabled the deletion keeps its row and key locks (tens of thousands of them, a few
/// MB of lock memory on a 2 GB engine), and another Space's writes - whose rows those locks never cover - go ahead. Readers
/// never waited on it (<c>READ_COMMITTED_SNAPSHOT</c>, the migration before this one).
/// </para>
/// <para>
/// <b>Safe on existing data</b>: a table option, no row changes. <c>Down</c> restores SQL Server's default
/// (<c>TABLE</c>).
/// </para>
/// </remarks>
public partial class TaskLockEscalationDisabled : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE dbo.[Task] SET (LOCK_ESCALATION = DISABLE);");
        migrationBuilder.Sql("ALTER TABLE dbo.[TaskLabel] SET (LOCK_ESCALATION = DISABLE);");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE dbo.[TaskLabel] SET (LOCK_ESCALATION = TABLE);");
        migrationBuilder.Sql("ALTER TABLE dbo.[Task] SET (LOCK_ESCALATION = TABLE);");
    }
}
