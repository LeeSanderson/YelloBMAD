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
/// (<c>UPDLOCK, HOLDLOCK</c> - every key placer's range lock, the bound counts) still lock exactly as before: the race
/// classes that serialise on them are unchanged.
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
/// other sessions' open transactions in this database - the migration is applied before the revision serves traffic).
/// Azure SQL Database already has it on, so the guard makes production a no-op; it changes no row, so it is safe on
/// existing data. <c>Down</c> turns it off the same way, guarded.
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
            IF (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = DB_NAME()) = 1
                ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE;
            """,
            suppressTransaction: true);
}
