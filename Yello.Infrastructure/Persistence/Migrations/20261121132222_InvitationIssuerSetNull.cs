using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// <c>Invitation.IssuedByAccountId</c> becomes nullable, and its key to <c>Account</c> becomes <c>ON DELETE SET NULL</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every migration here is -
/// see <see cref="InitialSchema"/> for why the scaffolded form cannot stand.
/// </para>
/// <para>
/// <b>Why the engine's referential action.</b> An Invitation outlives its issuer's Account, in every Space it was issued
/// in - a Space the issuer was removed from included, which no Space-scoped statement of the deletion reaches. With
/// <c>Restrict</c> the Account's row could never be deleted while one of its Invitations stood.
/// </para>
/// <para>
/// <b>Expand only.</b> A nullable column and a looser key refuse nothing the previous revision writes. <b>Down</b> is
/// lossy once an Account has been deleted: a null issuer cannot be restored, so those rows would have to be revoked or
/// deleted by hand before it runs. <b>Literal names rather than <c>SchemaNames</c></b>, for the reason
/// <see cref="InitialSchema"/> records.
/// </para>
/// </remarks>
public partial class InvitationIssuerSetNull : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Invitation_Account_IssuedByAccountId",
            table: "Invitation");

        migrationBuilder.AlterColumn<Guid>(
            name: "IssuedByAccountId",
            table: "Invitation",
            type: "uniqueidentifier",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier");

        migrationBuilder.AddForeignKey(
            name: "FK_Invitation_Account_IssuedByAccountId",
            table: "Invitation",
            column: "IssuedByAccountId",
            principalTable: "Account",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Invitation_Account_IssuedByAccountId",
            table: "Invitation");

        migrationBuilder.AlterColumn<Guid>(
            name: "IssuedByAccountId",
            table: "Invitation",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.AddForeignKey(
            name: "FK_Invitation_Account_IssuedByAccountId",
            table: "Invitation",
            column: "IssuedByAccountId",
            principalTable: "Account",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }
}
