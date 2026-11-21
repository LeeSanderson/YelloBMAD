using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// A unique index on <c>StatusDeltaOperation (SpaceId, ProjectId, Sequence)</c>: two operations of one Project's delta never
/// share a place in its order.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then brought up to the coding standard by hand</b>, as every migration here is -
/// see <see cref="InitialSchema"/> for why the scaffolded form cannot stand.
/// </para>
/// <para>
/// <b>Expand only.</b> Every writer of the previous revision takes the next sequence under the Project's lock and deletes
/// the operation it replaces before inserting, so no row it writes collides. <b>Down</b> drops the index and loses nothing.
/// <b>Literal names rather than <c>SchemaNames</c></b>, for the reason <see cref="InitialSchema"/> records.
/// </para>
/// </remarks>
public partial class StatusDeltaSequenceUnique : Migration
{
    private static readonly string[] SequenceColumns = ["SpaceId", "ProjectId", "Sequence"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_StatusDeltaOperation_SpaceId_ProjectId_Sequence",
            table: "StatusDeltaOperation",
            columns: SequenceColumns,
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_StatusDeltaOperation_SpaceId_ProjectId_Sequence",
            table: "StatusDeltaOperation");
    }
}
