using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// Story 1.8's schema: the <c>ApiToken</c> table, bound by a cascading composite foreign key to the
/// Membership it was issued under. FR-36 / CAP-36; AD-6, NFR-6.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every
/// migration here is - see <see cref="InitialSchema"/>. Literal names rather than
/// <c>SchemaNames</c>, for the reason given there.
/// </para>
/// <para>
/// <b>Membership's <c>(SpaceId, AccountId)</c> unique index becomes a unique constraint.</b> A
/// foreign key needs a key to target, and the constraint's own index replaces the one it supersedes
/// rather than indexing the pair twice. Uniqueness holds throughout.
/// </para>
/// <para>
/// <b>No row-level security policy on <c>ApiToken</c></b>, on purpose: the authentication lookup by
/// <c>SecretHash</c> runs before any Space is known (story 1.8, open question 4). <b>No migration is
/// applied at startup</b> (AR-36).
/// </para>
/// </remarks>
public partial class ApiTokens : Migration
{
    private static readonly string[] MembershipPair = ["SpaceId", "AccountId"];

    private static readonly string[] TokenPair = ["BoundSpaceId", "AccountId"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Membership_SpaceId_AccountId",
            table: "Membership");

        migrationBuilder.AddUniqueConstraint(
            name: "AK_Membership_SpaceId_AccountId",
            table: "Membership",
            columns: MembershipPair);

        migrationBuilder.CreateTable(
            name: "ApiToken",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BoundSpaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                SecretHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApiToken", x => x.Id);
                table.ForeignKey(
                    name: "FK_ApiToken_Membership_BoundSpaceId_AccountId",
                    columns: x => new { x.BoundSpaceId, x.AccountId },
                    principalTable: "Membership",
                    principalColumns: MembershipPair,
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ApiToken_AccountId",
            table: "ApiToken",
            column: "AccountId");

        // The foreign key's own index, so a Membership delete finds its Tokens by seek.
        migrationBuilder.CreateIndex(
            name: "IX_ApiToken_BoundSpaceId_AccountId",
            table: "ApiToken",
            columns: TokenPair);

        migrationBuilder.CreateIndex(
            name: "UX_ApiToken_SecretHash",
            table: "ApiToken",
            column: "SecretHash",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ApiToken");

        migrationBuilder.DropUniqueConstraint(
            name: "AK_Membership_SpaceId_AccountId",
            table: "Membership");

        migrationBuilder.CreateIndex(
            name: "IX_Membership_SpaceId_AccountId",
            table: "Membership",
            columns: MembershipPair,
            unique: true);
    }
}
