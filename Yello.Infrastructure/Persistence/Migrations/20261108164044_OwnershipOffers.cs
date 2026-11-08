using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// The <c>OwnershipOffer</c> table, under the Space policy with three predicates. FR-8; AD-2, AD-26, AD-27.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every migration here is -
/// see <see cref="InitialSchema"/> for why the scaffolded form cannot stand.
/// </para>
/// <para>
/// <b>An offer is per Space</b>: a non-nullable <c>SpaceId</c> referencing <c>Space</c> with cascade, so a Space's deletion
/// takes its offers, and under FILTER, BLOCK AFTER INSERT and BLOCK AFTER UPDATE. <b>It has no key to <c>Membership</c> or
/// <c>Account</c></b>: a retained offer outlives the Memberships it names, and that no pending offer names a vanished one
/// rests on the guarded insert and the written lapse.
/// </para>
/// <para>
/// <b><c>UX_OwnershipOffer_SpaceId_Pending</c></b> lets a Space hold at most one <c>Pending</c> offer. Its expiry is the
/// database clock's: the column default is seven days after <c>SYSDATETIMEOFFSET()</c> - PRD section 12 assumption 3.
/// </para>
/// <para>
/// <b>Expand only.</b> A new table breaks nothing the previous revision runs. <b>Literal names rather than
/// <c>SchemaNames</c></b>, for the reason <see cref="InitialSchema"/> records.
/// </para>
/// </remarks>
public partial class OwnershipOffers : Migration
{
    private static readonly string[] SpaceAndRecipient = ["SpaceId", "RecipientMembershipId"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OwnershipOffer",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SpaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RecipientMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OfferedByMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OffererName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                State = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "DATEADD(day, 7, SYSDATETIMEOFFSET())"),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OwnershipOffer", x => x.Id);
                table.ForeignKey(
                    name: "FK_OwnershipOffer_Space_SpaceId",
                    column: x => x.SpaceId,
                    principalTable: "Space",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_OwnershipOffer_SpaceId_RecipientMembershipId", table: "OwnershipOffer", columns: SpaceAndRecipient);

        migrationBuilder.CreateIndex(
            name: "UX_OwnershipOffer_SpaceId_Pending",
            table: "OwnershipOffer",
            column: "SpaceId",
            unique: true,
            filter: "[State] = N'Pending'");

        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                ADD FILTER PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[OwnershipOffer],
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[OwnershipOffer] AFTER INSERT,
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[OwnershipOffer] AFTER UPDATE;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The predicates first: they are bound to the table's column, so the table cannot go while the policy names it.
        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                DROP FILTER PREDICATE ON dbo.[OwnershipOffer],
                DROP BLOCK PREDICATE ON dbo.[OwnershipOffer] AFTER INSERT,
                DROP BLOCK PREDICATE ON dbo.[OwnershipOffer] AFTER UPDATE;
            """);

        migrationBuilder.DropTable(name: "OwnershipOffer");
    }
}
