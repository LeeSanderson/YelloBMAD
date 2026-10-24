using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// Story 1.7's second schema change: <c>Account.SessionsValidAfter</c>, the instant sign-out ends an
/// Account's Sessions at. FR-2 / CAP-2; Lee's decision (a) of 2026-10-24.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every
/// migration here is - see <see cref="InitialSchema"/>.
/// </para>
/// <para>
/// <b>Nullable, with no default, and nothing backfilled.</b> <c>NULL</c> means the Account has never
/// signed out, which admits every ticket it holds - exactly the behaviour before this column, so
/// existing Sessions are untouched by applying it. Sign-out writes the instant; every authenticated
/// request compares its ticket's issue instant with it, on read (AD-27). There is no index: the
/// read is by the primary key.
/// </para>
/// <para>
/// <b><c>Account</c> still carries no row-level security policy</b>, and this does not change that -
/// the AD-24 amendment owns the question. <b>No migration is applied at startup</b> (AR-36).
/// </para>
/// </remarks>
public partial class SessionRevocation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "SessionsValidAfter",
            table: "Account",
            type: "datetimeoffset",
            nullable: true);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(
            name: "SessionsValidAfter",
            table: "Account");
}
