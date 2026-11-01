using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// Story 4.1's <c>Invitation</c> table, under the Space policy with three predicates, and the policy-free
/// <c>OutboxMessage</c> table. FR-10, FR-39; AD-2, AD-10, AD-27.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every migration here is -
/// see <see cref="InitialSchema"/> for why the scaffolded form cannot stand.
/// </para>
/// <para>
/// <b>An Invitation is per Space</b>: a non-nullable <c>SpaceId</c> referencing <c>Space</c> with cascade (Lee's Q1:
/// deleting a Space destroys its Invitations, FR-7), keyed <c>(SpaceId, Id)</c>, and under FILTER, BLOCK AFTER INSERT
/// and BLOCK AFTER UPDATE, so no Invitation can be read from, written into or moved to another Space. Its issuer is an
/// <c>Account</c> with no action on delete (FR-12) and it has no reference to <c>Membership</c>.
/// </para>
/// <para>
/// <b>Its expiry is the database clock's</b> (open question 10 (a)): the column default is seven days after
/// <c>SYSDATETIMEOFFSET()</c> - PRD section 12 item 9, an assumption. The normalised address is the Account column's type
/// and <c>Latin1_General_100_BIN2</c> collation (Lee's Q3). The token hash is unique; no unique index is filtered on
/// <c>State</c>, because expiry is derived and an expired Invitation must not block its reissue.
/// </para>
/// <para>
/// <b><c>OutboxMessage</c> has no Space column, no predicate and no foreign key</b> - Lee's Q6, a recorded AD-2
/// variance: the recovery sweep reads it with no session key.
/// </para>
/// <para>
/// <b>Expand only.</b> Two new tables break nothing the previous revision runs. <b>Literal names rather than
/// <c>SchemaNames</c></b>, for the reason <see cref="InitialSchema"/> records. <b>No migration is applied at startup</b>
/// (AR-36).
/// </para>
/// </remarks>
public partial class Invitations : Migration
{
    private static readonly string[] SpaceAndNormalizedAddress = ["SpaceId", "NormalizedEmailAddress"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Invitation",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SpaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmailAddress = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                NormalizedEmailAddress = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false, collation: "Latin1_General_100_BIN2"),
                Role = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                State = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "DATEADD(day, 7, SYSDATETIMEOFFSET())"),
                TokenHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                IssuedByAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IssuerName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Invitation", x => x.Id);
                table.UniqueConstraint("AK_Invitation_SpaceId_Id", x => new { x.SpaceId, x.Id });
                table.ForeignKey(
                    name: "FK_Invitation_Account_IssuedByAccountId",
                    column: x => x.IssuedByAccountId,
                    principalTable: "Account",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Invitation_Space_SpaceId",
                    column: x => x.SpaceId,
                    principalTable: "Space",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "OutboxMessage",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kind = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                Recipient = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                Subject = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                EnqueuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ClaimedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Attempts = table.Column<int>(type: "int", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_OutboxMessage", x => x.Id));

        migrationBuilder.CreateIndex(name: "IX_Invitation_IssuedByAccountId", table: "Invitation", column: "IssuedByAccountId");

        migrationBuilder.CreateIndex(name: "IX_Invitation_SpaceId_NormalizedEmailAddress", table: "Invitation", columns: SpaceAndNormalizedAddress);

        migrationBuilder.CreateIndex(name: "UX_Invitation_TokenHash", table: "Invitation", column: "TokenHash", unique: true);

        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                ADD FILTER PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[Invitation],
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[Invitation] AFTER INSERT,
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[Invitation] AFTER UPDATE;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The predicates first: they are bound to the table's column, so the table cannot go while the policy names it.
        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                DROP FILTER PREDICATE ON dbo.[Invitation],
                DROP BLOCK PREDICATE ON dbo.[Invitation] AFTER INSERT,
                DROP BLOCK PREDICATE ON dbo.[Invitation] AFTER UPDATE;
            """);

        migrationBuilder.DropTable(name: "Invitation");

        migrationBuilder.DropTable(name: "OutboxMessage");
    }
}
