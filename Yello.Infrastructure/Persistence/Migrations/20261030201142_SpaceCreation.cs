using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// Story 3.1's schema change: the Account-scoped <c>AccountIdempotencyRecord</c> table Space creation replays from,
/// under a row-level security policy of its own on <c>SESSION_CONTEXT('AccountId')</c>. Open question 2 (a), decided
/// by Lee 2026-10-29; AD-18 / AR-25, AD-24 as amended on 2026-10-29.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every migration here is
/// - see <see cref="InitialSchema"/> for why the scaffolded form cannot stand.
/// </para>
/// <para>
/// <b>A new predicate function and a new policy</b>, both covering this one table: <c>fn_AccountIsolationPredicate</c>
/// admits a row whose <c>AccountId</c> is <c>SESSION_CONTEXT('AccountId')</c>, as FILTER and BLOCK AFTER INSERT. Rows are
/// never updated, so no AFTER UPDATE block. A policy of its own rather than a predicate in <c>SpaceIsolationPolicy</c>,
/// so that policy still covers exactly the tables carrying a <c>SpaceId</c> (<c>SpaceIsolationSchemaTests</c>). An unset
/// key is <c>NULL</c>, and <c>NULL = x</c> is unknown, so a connection without the key sees and writes nothing here.
/// </para>
/// <para>
/// <b>Unique on <c>(AccountId, Key)</c></b>: the second insert of a key is how a concurrent first use is refused. The
/// key compares byte for byte. Erased with its Account (FR-3).
/// </para>
/// <para>
/// <b><c>Space</c> and <c>Account</c> gain no policy</b> (open question 5 (a)): a recorded variance in the AD-24
/// amendment, not an omission.
/// </para>
/// <para>
/// <b>Expand only</b>; <b>literal names rather than <c>SchemaNames</c></b>; <b><c>CREATE FUNCTION</c> in its own
/// batch</b>; <b>no migration is applied at startup</b> (AR-36) - each for the reason <see cref="InitialSchema"/>
/// records. <see cref="Down"/> drops the policy before the function it names, then the table.
/// </para>
/// </remarks>
public partial class SpaceCreation : Migration
{
    private static readonly string[] KeyColumns = ["AccountId", "Key"];

    private static readonly string[] AgeColumns = ["AccountId", "CreatedAt"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AccountIdempotencyRecord",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Key = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, collation: "Latin1_General_100_BIN2"),
                Fingerprint = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                StatusCode = table.Column<int>(type: "int", nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                Location = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AccountIdempotencyRecord", x => x.Id);
                table.ForeignKey(
                    name: "FK_AccountIdempotencyRecord_Account_AccountId",
                    column: x => x.AccountId,
                    principalTable: "Account",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        // An Account's expired records are deleted by age, inside its own creation unit of work.
        migrationBuilder.CreateIndex(
            name: "IX_AccountIdempotencyRecord_AccountId_CreatedAt",
            table: "AccountIdempotencyRecord",
            columns: AgeColumns);

        migrationBuilder.CreateIndex(
            name: "IX_AccountIdempotencyRecord_AccountId_Key",
            table: "AccountIdempotencyRecord",
            columns: KeyColumns,
            unique: true);

        // Its own batch: CREATE FUNCTION has to be the first statement in one.
        migrationBuilder.Sql("""
            CREATE FUNCTION dbo.fn_AccountIsolationPredicate(@AccountId uniqueidentifier)
            RETURNS TABLE
            WITH SCHEMABINDING
            AS
            RETURN
                SELECT 1 AS IsAccessible
                WHERE @AccountId = CAST(SESSION_CONTEXT(N'AccountId') AS uniqueidentifier);
            """);

        migrationBuilder.Sql("""
            CREATE SECURITY POLICY dbo.AccountIsolationPolicy
                ADD FILTER PREDICATE dbo.fn_AccountIsolationPredicate(AccountId) ON dbo.AccountIdempotencyRecord,
                ADD BLOCK PREDICATE dbo.fn_AccountIsolationPredicate(AccountId) ON dbo.AccountIdempotencyRecord AFTER INSERT
                WITH (STATE = ON);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The policy first: it is bound to the function and the table's column.
        migrationBuilder.Sql("DROP SECURITY POLICY dbo.AccountIsolationPolicy;");

        migrationBuilder.Sql("DROP FUNCTION dbo.fn_AccountIsolationPredicate;");

        migrationBuilder.DropTable(name: "AccountIdempotencyRecord");
    }
}
