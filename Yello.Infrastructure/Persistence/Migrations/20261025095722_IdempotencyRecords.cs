using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// Story 2.1's <c>IdempotencyRecord</c> table: the response to each <c>Idempotency-Key</c>, so a retried
/// write is applied once. AD-18 / AR-25; AD-2.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every
/// migration here is - see <see cref="InitialSchema"/> for why the scaffolded form cannot stand.
/// </para>
/// <para>
/// <b>Space-scoped, with both predicates</b> - FILTER and BLOCK AFTER INSERT - because a recorded body
/// holds Space content (a Project's name). Rows are never updated, so it takes no AFTER UPDATE block.
/// </para>
/// <para>
/// <b>Unique on <c>(SpaceId, AccountId, Key)</c></b>: a key belongs to one Account in one Space, and the
/// second insert of a key is how a concurrent first use is refused. The key compares byte for byte.
/// </para>
/// <para>
/// <b>Expand only</b>; <b>literal names rather than <c>SchemaNames</c></b>; <b>no migration is applied
/// at startup</b> (AR-36) - each for the reason <see cref="InitialSchema"/> records.
/// </para>
/// </remarks>
public partial class IdempotencyRecords : Migration
{
    private static readonly string[] KeyColumns = ["SpaceId", "AccountId", "Key"];

    private static readonly string[] AgeColumns = ["SpaceId", "CreatedAt"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "IdempotencyRecord",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SpaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                table.PrimaryKey("PK_IdempotencyRecord", x => x.Id);
                table.ForeignKey(
                    name: "FK_IdempotencyRecord_Space_SpaceId",
                    column: x => x.SpaceId,
                    principalTable: "Space",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_IdempotencyRecord_SpaceId_AccountId_Key",
            table: "IdempotencyRecord",
            columns: KeyColumns,
            unique: true);

        // A Space's expired records are deleted by age, inside its own unit of work.
        migrationBuilder.CreateIndex(
            name: "IX_IdempotencyRecord_SpaceId_CreatedAt",
            table: "IdempotencyRecord",
            columns: AgeColumns);

        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                ADD FILTER PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.IdempotencyRecord,
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.IdempotencyRecord AFTER INSERT;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The predicates first: they are bound to the table's column.
        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                DROP FILTER PREDICATE ON dbo.IdempotencyRecord,
                DROP BLOCK PREDICATE ON dbo.IdempotencyRecord AFTER INSERT;
            """);

        migrationBuilder.DropTable(name: "IdempotencyRecord");
    }
}
