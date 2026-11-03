using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// Story 4.4's row-level-security tightening: <c>Membership</c> gains a <c>BLOCK AFTER UPDATE</c> predicate on
/// <c>dbo.fn_SpaceIsolationPredicate(SpaceId)</c>, like every other Space table the product updates. Lee's Q3 (a); AD-2,
/// AR-12.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why now.</b> <c>Membership</c> becomes the first Space table whose rows the product <i>updates</i> - the Role change -
/// and every other updated Space table (Project, Task, Label, TaskLabel, Invitation) already carries one. <c>SpaceId</c> is
/// init-only in the entity and the guarded statement names its Space, but the database should not rest on either: with
/// this predicate no update can move a Membership into a Space other than the session's.
/// </para>
/// <para>
/// <b>The Space-only function, deliberately</b> - as <c>Membership</c>'s BLOCK AFTER INSERT is (AD-24, story 4.3 C1): the
/// Account-scoped FILTER (<c>fn_MembershipVisibilityPredicate</c>, story 1.7) lets the switcher read an Account's
/// Memberships across Spaces, and nothing written under the <c>AccountId</c> key alone may land a Membership anywhere.
/// Every Role-change path runs under the Space's key: the Role change on the ordinary resolver, and story 5.2's ownership
/// swap on the same.
/// </para>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c> as an empty migration</b> - the model is unchanged, row-level security lives outside it -
/// and the predicate hand-written, as every policy change here is (see <see cref="InitialSchema"/>). <b>Expand only</b>: a
/// predicate the previous revision's statements already satisfy, since it updates no Membership. <b>No migration is applied
/// at startup</b> (AR-36).
/// </para>
/// </remarks>
public partial class MembershipBlockAfterUpdate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                ADD BLOCK PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.[Membership] AFTER UPDATE;
            """);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                DROP BLOCK PREDICATE ON dbo.[Membership] AFTER UPDATE;
            """);
}
