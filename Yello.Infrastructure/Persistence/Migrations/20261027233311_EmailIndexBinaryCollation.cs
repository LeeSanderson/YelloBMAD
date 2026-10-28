using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// Story 2.6's email-index collation: <c>Account.NormalizedEmailAddress</c>, the column FR-1's unique index is built on,
/// compares in <c>Latin1_General_100_BIN2</c>. Open question 12 (a); <c>deferred-work.md:198</c>; AD-15's collation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every migration here is -
/// see <see cref="InitialSchema"/> for why the scaffolded form cannot stand.
/// </para>
/// <para>
/// <b>Why.</b> <c>EmailAddressNormalisation.Normalise</c> decides which addresses are the same; until now the unique
/// index then compared the stored values again under the database default, which on the engine's image is width- and
/// kana-insensitive, so two addresses the function keeps apart could still collide - and AD-23 would answer that
/// collision as a silent <c>204</c>. A binary collation compares exactly what is stored.
/// </para>
/// <para>
/// <b>Safe on existing data</b>: a stricter collation only separates values that were equal before, so no row can
/// become a duplicate. SQL Server cannot alter a column's collation while an index is built on it, so the index is
/// dropped and built again around the change, in the same migration's transaction.
/// </para>
/// <para>
/// <b><c>Down</c> is not safe on every database</b>: once BIN2 has admitted two addresses that only the old collation
/// equates (full-width and ASCII letters, say), rebuilding the unique index under the database default would fail. So
/// <c>Down</c> first looks for such a pair and stops with an error naming the cause, before it changes anything.
/// </para>
/// </remarks>
public partial class EmailIndexBinaryCollation : Migration
{
    private const string Table = "Account";

    private const string Column = "NormalizedEmailAddress";

    private const string Index = "UX_Account_NormalizedEmailAddress";

    private const string ColumnType = "nvarchar(254)";

    private const int Longest = 254;

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropIndex(name: Index, table: Table);

        migrationBuilder.AlterColumn<string>(
            name: Column,
            table: Table,
            type: ColumnType,
            maxLength: Longest,
            nullable: false,
            collation: "Latin1_General_100_BIN2",
            oldClrType: typeof(string),
            oldType: ColumnType,
            oldMaxLength: Longest);

        migrationBuilder.CreateIndex(name: Index, table: Table, column: Column, unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql(
            "IF EXISTS (SELECT 1 FROM dbo.[Account] GROUP BY NormalizedEmailAddress COLLATE DATABASE_DEFAULT HAVING COUNT(*) > 1) "
            + "THROW 50000, 'EmailIndexBinaryCollation cannot be reverted: two Accounts hold addresses the database default collation treats as equal.', 1;");

        migrationBuilder.DropIndex(name: Index, table: Table);

        migrationBuilder.AlterColumn<string>(
            name: Column,
            table: Table,
            type: ColumnType,
            maxLength: Longest,
            nullable: false,
            oldClrType: typeof(string),
            oldType: ColumnType,
            oldMaxLength: Longest,
            oldCollation: "Latin1_General_100_BIN2");

        migrationBuilder.CreateIndex(name: Index, table: Table, column: Column, unique: true);
    }
}
