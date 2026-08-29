namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Every table, column and object name the row-level security policy and the schema test both
/// have to agree on, stated once.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is what the LIVE schema is asserted against, not what the migration is generated
/// from.</b> The distinction matters and is the opposite of the usual "state it once" rule. An
/// applied migration is a historical record: it has already run against real databases, so
/// deriving its SQL from a constant a later story could rename would rewrite history rather than
/// change the future. The migration therefore carries literals, and
/// <c>SpaceIsolationSchemaTests</c> queries <c>sys.security_policies</c>,
/// <c>sys.security_predicates</c> and <c>sys.indexes</c> for the names below.
/// </para>
/// <para>
/// So drift between the two is not prevented - it is <i>detected</i>, which is the useful
/// property here. Rename a table in this file without adding a migration and the schema test
/// fails saying the object is missing, which is exactly the signal a rename with no migration
/// should produce.
/// </para>
/// <para>
/// <b>Table names are singular and match the Glossary exactly.</b> <c>Account</c>, not
/// <c>Users</c>; <c>Space</c>, not <c>Workspace</c>, <c>Tenant</c> or <c>Org</c>. A generic
/// tenant column or ambient tenant middleware is an explicitly rejected shape
/// (<c>decisions-settled.md:18</c>, <c>addendum.md:18</c>), not a naming preference.
/// </para>
/// <para>
/// Properties rather than constants throughout: S2339 refuses a public <c>const</c>, and it is
/// right to here - a constant is copied into every consumer at compile time, so a renamed table
/// would reach a test assembly that had not been rebuilt as the old name.
/// </para>
/// </remarks>
public static class SchemaNames
{
    /// <summary>
    /// The Account table.
    /// </summary>
    public static string AccountTable => "Account";

    /// <summary>
    /// The Space table.
    /// </summary>
    public static string SpaceTable => "Space";

    /// <summary>
    /// The Membership table.
    /// </summary>
    public static string MembershipTable => "Membership";

    /// <summary>
    /// The StatusDefinition table.
    /// </summary>
    public static string StatusDefinitionTable => "StatusDefinition";

    /// <summary>
    /// The column every Space-scoped table carries, and the one the security predicate reads.
    /// </summary>
    public static string SpaceIdColumn => "SpaceId";

    /// <summary>
    /// The <c>SESSION_CONTEXT</c> key the predicate compares against. AD-2 names it, and it is
    /// set <c>@read_only = 1</c> so nothing downstream in the same session can move it.
    /// </summary>
    public static string SpaceIdSessionKey => "SpaceId";

    /// <summary>
    /// The inline table-valued predicate function the policy is built from.
    /// </summary>
    public static string SpacePredicateFunction => "fn_SpaceIsolationPredicate";

    /// <summary>
    /// The security policy that applies the predicate to every Space-scoped table.
    /// </summary>
    public static string SpaceIsolationPolicy => "SpaceIsolationPolicy";

    /// <summary>
    /// The unique index that makes an email address unique across Accounts. FR-1.
    /// </summary>
    public static string AccountEmailUniqueIndex => "UX_Account_NormalizedEmailAddress";

    /// <summary>
    /// The filtered unique index that makes "exactly one Owner per Space" a database fact.
    /// AD-5 / AR-12.
    /// </summary>
    public static string MembershipOwnerUniqueIndex => "UX_Membership_SpaceId_Owner";

    /// <summary>
    /// The tables this story gives a row-level security policy, in the order the policy names
    /// them. Both the migration and the schema test derive from this rather than listing tables
    /// twice.
    /// </summary>
    public static IReadOnlyList<string> SpaceScopedTables { get; } =
        [MembershipTable, StatusDefinitionTable];
}
