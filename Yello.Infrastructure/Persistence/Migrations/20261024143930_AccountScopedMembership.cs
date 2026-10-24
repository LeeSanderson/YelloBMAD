using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yello.Infrastructure.Persistence.Migrations;

/// <summary>
/// Story 1.7's schema change: <c>Membership</c> becomes readable by Account as well as by Space, for
/// the Space switcher and nothing else. AD-24 / AR-29; AD-2 / AR-5, AR-6.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded by <c>dotnet ef</c>, then written by hand</b>, as every migration here is - see
/// <see cref="InitialSchema"/> for why the scaffolded form cannot stand. The model change behind it
/// is a query filter, which EF Core does not migrate, so the scaffold was empty: everything below is
/// the database half of that same disjunction.
/// </para>
/// <para>
/// <b>One table's FILTER predicate is replaced; nothing else moves.</b> A new predicate function
/// admits a <c>Membership</c> row when its <c>SpaceId</c> is <c>SESSION_CONTEXT('SpaceId')</c>
/// <i>or</i> its <c>AccountId</c> is <c>SESSION_CONTEXT('AccountId')</c> (story 1.7, open question 1,
/// option a). <c>Membership</c>'s BLOCK predicate stays on the Space-only function - the switcher
/// never writes - and <c>StatusDefinition</c> and <c>SpacePresence</c> are untouched, so the
/// <c>AccountId</c> key admits nothing anywhere else. An unset key is <c>NULL</c>, and <c>NULL = x</c>
/// is unknown, so a connection carrying neither key still sees nothing.
/// </para>
/// <para>
/// <b>Replaced inside the existing policy rather than added as a second one.</b> The alternative -
/// a second security policy on <c>Membership</c> keyed on <c>AccountId</c> - would split one table's
/// isolation across two policies. <c>ALTER SECURITY POLICY ... ALTER FILTER PREDICATE</c> swaps the
/// function in place; a function bound to a policy cannot be dropped while the policy names it, so
/// the new one is created first and <see cref="Down"/> restores the original predicate before
/// dropping it. Verified by execution against SQL Server 2025 on every build
/// (<c>SpaceIsolationLayerTests</c>, <c>SpaceIsolationSchemaTests</c>) rather than trusted from
/// documentation.
/// </para>
/// <para>
/// <b>Literal names rather than <c>SchemaNames</c></b>, for the reason <see cref="InitialSchema"/>
/// records: an applied migration is history. <b>No migration is applied at startup</b> (AR-36).
/// <see cref="InitialSchema"/>'s remark that the switcher's predicate is "an architecture decision"
/// is left as written, because it was true when applied; this migration is the decision.
/// </para>
/// </remarks>
public partial class AccountScopedMembership : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Its own batch: CREATE FUNCTION has to be the first statement in one, and each Sql call is
        // issued as a separate command.
        migrationBuilder.Sql("""
            CREATE FUNCTION dbo.fn_MembershipVisibilityPredicate(@SpaceId uniqueidentifier, @AccountId uniqueidentifier)
            RETURNS TABLE
            WITH SCHEMABINDING
            AS
            RETURN
                SELECT 1 AS IsAccessible
                WHERE @SpaceId = CAST(SESSION_CONTEXT(N'SpaceId') AS uniqueidentifier)
                    OR @AccountId = CAST(SESSION_CONTEXT(N'AccountId') AS uniqueidentifier);
            """);

        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                ALTER FILTER PREDICATE dbo.fn_MembershipVisibilityPredicate(SpaceId, AccountId) ON dbo.Membership;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Story 1.3's predicate restored exactly, then the function no policy names any more.
        migrationBuilder.Sql("""
            ALTER SECURITY POLICY dbo.SpaceIsolationPolicy
                ALTER FILTER PREDICATE dbo.fn_SpaceIsolationPredicate(SpaceId) ON dbo.Membership;
            """);

        migrationBuilder.Sql("DROP FUNCTION dbo.fn_MembershipVisibilityPredicate;");
    }
}
