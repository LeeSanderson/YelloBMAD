using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// Story 1.10's schema: the <c>DataProtectionKey</c> table, where the Host keeps the key ring that
/// encrypts the Session and anti-forgery cookies, so a new revision or a cold start can read what the
/// previous process issued. AC12; Finding F1, open question 7.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every
/// migration here is - see <see cref="InitialSchema"/>.
/// </para>
/// <para>
/// <b>Not Space-scoped</b>: no <c>SpaceId</c>, no row-level security predicate. <b>Additive only</b>,
/// so the revision still serving while it is applied is unaffected (the expand/contract rule in
/// <c>docs/operations.md</c>). <b>No migration is applied at startup</b> (AR-36).
/// </para>
/// </remarks>
public partial class DataProtectionKeyRing : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DataProtectionKey",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FriendlyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Xml = table.Column<string>(type: "nvarchar(max)", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_DataProtectionKey", x => x.Id));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DataProtectionKey");
    }
}
