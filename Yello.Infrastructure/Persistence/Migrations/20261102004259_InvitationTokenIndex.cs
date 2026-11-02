using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// Story 4.3's policy-free <c>InvitationToken</c> index - token hash to Invitation and Space - and its backfill from the
/// Invitations already issued. Lee's Q1 (a); a named AD-2 variance beside <c>ApiToken.BoundSpaceId</c>; FR-11, AD-28.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every migration here is - see
/// <see cref="InitialSchema"/> for why the scaffolded form cannot stand. The backfill is hand-written.
/// </para>
/// <para>
/// <b>Outside the Space policy, deliberately</b>: the view and the accept look a token up by its hash before any Space is
/// known, so a predicate on <c>SESSION_CONTEXT('SpaceId')</c> would hide every row from the one read that needs it. Its
/// Space column is <c>OfferedSpaceId</c>, named in <c>SchemaVarianceTests.NamedVariances</c>. Keyed on the Invitation, its
/// hash unique, and its one foreign key the composite <c>(OfferedSpaceId, InvitationId) -&gt; Invitation (SpaceId, Id)</c>
/// with cascade, so a Space's one deletion statement takes its index rows with its Invitations.
/// </para>
/// <para>
/// <b>The backfill runs with the security policy switched off, inside this migration's transaction</b> - story 2.10's
/// <see cref="ListViewSortKeys"/> precedent, for its reason: a migration's connection carries no <c>SESSION_CONTEXT</c>,
/// so the Invitation FILTER predicate would hide every row, and the one line that sets a session key is
/// <c>SpaceSessionContext</c>'s. The policy is switched off, the one set-based statement copies each Invitation's own hash,
/// id and Space, and the policy is switched back on before the transaction commits; no other connection sees it off.
/// </para>
/// <para>
/// <b>Expand only.</b> A new table breaks nothing the previous revision runs; an Invitation that revision issues during a
/// rolling deploy has no index row until the backfill statement is run again (idempotent: it inserts only the missing
/// rows), and until then its link answers the uniform "no longer valid". <b>Literal names rather than
/// <c>SchemaNames</c></b>, for the reason <see cref="InitialSchema"/> records. <b>No migration is applied at startup</b>
/// (AR-36).
/// </para>
/// </remarks>
public partial class InvitationTokenIndex : Migration
{
    private const string InvitationTokenTable = "InvitationToken";

    private static readonly string[] OfferedSpaceAndInvitation = ["OfferedSpaceId", "InvitationId"];

    private static readonly string[] InvitationSpaceAndId = ["SpaceId", "Id"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: InvitationTokenTable,
            columns: table => new
            {
                InvitationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TokenHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                OfferedSpaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InvitationToken", x => x.InvitationId);
                table.ForeignKey(
                    name: "FK_InvitationToken_Invitation_OfferedSpaceId_InvitationId",
                    columns: x => new { x.OfferedSpaceId, x.InvitationId },
                    principalTable: "Invitation",
                    principalColumns: InvitationSpaceAndId,
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_InvitationToken_OfferedSpaceId_InvitationId", table: InvitationTokenTable, columns: OfferedSpaceAndInvitation);

        migrationBuilder.CreateIndex(name: "UX_InvitationToken_TokenHash", table: InvitationTokenTable, column: "TokenHash", unique: true);

        migrationBuilder.Sql("ALTER SECURITY POLICY dbo.SpaceIsolationPolicy WITH (STATE = OFF);");

        migrationBuilder.Sql("""
            INSERT INTO dbo.[InvitationToken] (InvitationId, TokenHash, OfferedSpaceId)
            SELECT i.Id, i.TokenHash, i.SpaceId
            FROM dbo.[Invitation] i
            WHERE NOT EXISTS (SELECT 1 FROM dbo.[InvitationToken] t WHERE t.InvitationId = i.Id);
            """);

        migrationBuilder.Sql("ALTER SECURITY POLICY dbo.SpaceIsolationPolicy WITH (STATE = ON);");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: InvitationTokenTable);
}
