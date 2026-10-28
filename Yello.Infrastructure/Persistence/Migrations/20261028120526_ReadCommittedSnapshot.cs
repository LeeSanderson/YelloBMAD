using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// Story 2.9's read isolation: <c>READ_COMMITTED_SNAPSHOT</c> is on in every database these migrations create or update,
/// so a Space-scoped read never waits on a writer. AC13, open question 10 (a); <c>deferred-work.md:313</c>, <c>:495</c>,
/// <c>:547</c>, <c>:548</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every migration here is -
/// see <see cref="InitialSchema"/> for why the scaffolded form cannot stand.
/// </para>
/// <para>
/// <b>Why.</b> Under locking READ COMMITTED a Board read, a Project listing or a presence count queued behind any
/// uncommitted write in the Space - a presence insert, a 5,000-Task Project deletion, a creation. Under RCSI each
/// statement reads the last committed version instead, so readers and writers stop blocking each other. Hinted reads
/// (<c>UPDLOCK, HOLDLOCK</c>, <c>READCOMMITTEDLOCK</c>) and application locks still lock. A read that decides a write must
/// take one of them: the key placers and the bound counts take their application lock first (<c>ApplicationLocks</c>,
/// story 2.9 open questions 14 and 17), and the reads that relied on waiting for a writer carry
/// <c>READCOMMITTEDLOCK</c> (open question 18; the Idempotency-Key lookup, at code review).
/// </para>
/// <para>
/// <b>Per statement, not per request</b> (open question 10 (a)): a column's count, its page and its Labels are separate
/// statements and may each see a different committed state, bounded to the writes that commit between them and
/// corrected by the next read. Snapshot isolation for whole requests was rejected: presence writes run inside every
/// resolution transaction and would meet update conflict 3960.
/// </para>
/// <para>
/// <b>Guarded and outside a transaction.</b> <c>ALTER DATABASE … SET READ_COMMITTED_SNAPSHOT</c> cannot run inside a user
/// transaction (<c>suppressTransaction</c>) and needs the database to itself (<c>WITH ROLLBACK IMMEDIATE</c>, which ends
/// other sessions' open transactions in this database). Azure SQL Database already has it on, so the guard makes
/// production a no-op; it changes no row, so it is safe on existing data. On a SQL Server target where it is off, the
/// statement rolls back every open transaction in the database, the running revision's included: apply it there with
/// the application stopped (Testcontainers and CI databases have no other sessions).
/// </para>
/// <para>
/// <b><c>Down</c> leaves Azure SQL Database alone</b> (story 2.9 code review): RCSI is that service's default, which this
/// migration never set, so rolling back must not turn it off and kill the live revision's transactions. Elsewhere
/// (<c>SERVERPROPERTY('EngineEdition')</c> not 5) it turns it off the same way, guarded.
/// </para>
/// </remarks>
public partial class ReadCommittedSnapshot : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            IF (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = DB_NAME()) = 0
                ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
            """,
            suppressTransaction: true);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            IF CAST(SERVERPROPERTY('EngineEdition') AS int) <> 5
                AND (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = DB_NAME()) = 1
                ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE;
            """,
            suppressTransaction: true);
}
