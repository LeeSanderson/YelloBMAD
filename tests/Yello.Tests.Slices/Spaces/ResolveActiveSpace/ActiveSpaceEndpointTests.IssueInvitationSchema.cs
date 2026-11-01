using Xunit;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 4.1's schema, read from the catalogue of this class's migrated database: the Invitation table under the Space
/// policy with its filter and both block predicates (AD-2), its foreign keys (Q1, Lee: cascade from the Space; the issuer
/// restricted; no Membership reference - AC12, FR-12), its address column matching the Account's exactly (AC3, Q3), its
/// database-clock expiry (AC8, Q10 (a)), its unique token hash and no filtered unique index on <c>State</c> (AC13), and
/// the outbox with no Space column and no policy (Lee's Q6).
/// </summary>
/// <remarks>
/// <b>Written as red-phase scaffolds by the story 4.1 ATDD run (2026-11-01).</b> The migration <c>&lt;ts&gt;_Invitations</c>
/// creates both tables; until it exists each catalogue query returns no rows. These read the database only - no Host
/// starts.
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// AC1, AD-2, I-1 - <c>dbo.SpaceIsolationPolicy</c> carries a FILTER, a BLOCK AFTER INSERT and a BLOCK AFTER UPDATE
    /// predicate on <c>dbo.Invitation</c>, so a row cannot be read, written or moved under another Space's key.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1 - the Invitations migration adds the table to the security policy in story 4.1 Task 4")]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "AR-4")]
    public async Task The_Invitation_table_is_under_the_Space_policy_with_its_filter_and_both_block_predicates()
    {
        _ = Available();

        var predicates = await Wire().QueryAsync("""
            SELECT p.predicate_type_desc, ISNULL(p.operation_desc, N'')
            FROM sys.security_predicates p
            JOIN sys.security_policies s ON s.object_id = p.object_id
            WHERE s.name = N'SpaceIsolationPolicy' AND p.target_object_id = OBJECT_ID(N'dbo.Invitation')
            ORDER BY 1, 2
            """);

        Assert.Equal(
            ["BLOCK AFTER INSERT", "BLOCK AFTER UPDATE", "FILTER "],
            predicates.Select(row => $"{row[0]} {row[1]}"));
    }

    /// <summary>
    /// AC12, Q1 (Lee), FR-7, FR-12, AD-5 - <c>Invitation</c> cascades from its Space (the Space's deletion stays one
    /// statement), references its issuer's Account with <c>NO ACTION</c> (an Invitation survives the issuer's demotion and
    /// removal), and has no foreign key to <c>Membership</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC12 - the Invitations migration creates the table and its foreign keys in story 4.1 Task 4")]
    [Trait("Requirement", "FR-7")]
    [Trait("Requirement", "FR-12")]
    public async Task An_Invitation_cascades_from_its_Space_restricts_its_issuer_and_references_no_Membership()
    {
        _ = Available();

        var keys = await Wire().QueryAsync("""
            SELECT OBJECT_NAME(f.referenced_object_id), f.delete_referential_action_desc
            FROM sys.foreign_keys f
            WHERE f.parent_object_id = OBJECT_ID(N'dbo.Invitation')
            ORDER BY 1
            """);

        Assert.Equal(["Account NO_ACTION", "Space CASCADE"], keys.Select(row => $"{row[0]} {row[1]}"));
    }

    /// <summary>
    /// AC3, AC12, Q3 (Lee) - the address columns match the Account's exactly: <c>NormalizedEmailAddress</c> is the same
    /// type, length and <c>Latin1_General_100_BIN2</c> collation as <c>Account.NormalizedEmailAddress</c>, so 4.3's
    /// comparison is one function and one collation; <c>EmailAddress</c> holds 254 and <c>IssuerName</c> 128.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 - the Invitations migration creates the address columns in story 4.1 Task 4")]
    [Trait("Requirement", "FR-10")]
    public async Task The_normalised_address_column_matches_the_Accounts_exactly()
    {
        _ = Available();

        var columns = await Wire().QueryAsync("""
            SELECT OBJECT_NAME(c.object_id), c.name, TYPE_NAME(c.user_type_id), c.max_length, ISNULL(c.collation_name, N'')
            FROM sys.columns c
            WHERE (c.object_id = OBJECT_ID(N'dbo.Invitation') AND c.name IN (N'EmailAddress', N'NormalizedEmailAddress', N'IssuerName'))
               OR (c.object_id = OBJECT_ID(N'dbo.Account') AND c.name = N'NormalizedEmailAddress')
            """);

        string Shape(string table, string column) =>
            string.Join(' ', Assert.Single(columns, row => row[0] == table && row[1] == column).Skip(2));

        Assert.Equal(Shape("Account", "NormalizedEmailAddress"), Shape("Invitation", "NormalizedEmailAddress"));
        Assert.EndsWith("Latin1_General_100_BIN2", Shape("Invitation", "NormalizedEmailAddress"), StringComparison.Ordinal);
        Assert.StartsWith("nvarchar 508", Shape("Invitation", "EmailAddress"), StringComparison.Ordinal);
        Assert.StartsWith("nvarchar 256", Shape("Invitation", "IssuerName"), StringComparison.Ordinal);
    }

    /// <summary>
    /// AC8, AR-31, AD-27, Q10 (a), A-15 - <c>ExpiresAt</c> is a <c>datetimeoffset</c> whose default is seven days after
    /// <c>SYSDATETIMEOFFSET()</c>, so the stamp and the shared predicate read one clock; no C# clock stamps it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC8 - the ExpiresAt column default arrives with the Invitations migration, story 4.1 Task 4")]
    [Trait("Requirement", "AR-31")]
    [Trait("Requirement", "AD-27")]
    [Trait("Assumption", "PRD-12-9")]
    public async Task ExpiresAt_defaults_to_seven_days_after_the_database_clock()
    {
        _ = Available();

        var column = Assert.Single(await Wire().QueryAsync("""
            SELECT TYPE_NAME(c.user_type_id), LOWER(REPLACE(REPLACE(ISNULL(d.definition, N''), N'(', N''), N')', N''))
            FROM sys.columns c
            LEFT JOIN sys.default_constraints d ON d.object_id = c.default_object_id
            WHERE c.object_id = OBJECT_ID(N'dbo.Invitation') AND c.name = N'ExpiresAt'
            """));

        Assert.Equal("datetimeoffset", column[0]);
        Assert.Contains("sysdatetimeoffset", column[1], StringComparison.Ordinal);
        Assert.Contains("day", column[1], StringComparison.Ordinal);
        Assert.Contains(InvitationScaffold.ExpiryDays.ToString(System.Globalization.CultureInfo.InvariantCulture), column[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// AC13, Task 3, Task 4 - the token hash is unique (4.3 resolves an Invitation by it), the duplicate checks have their
    /// <c>(SpaceId, NormalizedEmailAddress)</c> index, and no unique index is filtered on <c>State</c>: expiry is derived,
    /// so such an index would block the reissue FR-39 requires.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC13 - the Invitation indexes arrive with the Invitations migration, story 4.1 Task 4")]
    [Trait("Requirement", "FR-39")]
    [Trait("Requirement", "AD-27")]
    public async Task The_token_hash_is_unique_and_no_unique_index_is_filtered_on_State()
    {
        _ = Available();

        var indexes = await Wire().QueryAsync("""
            SELECT i.name, i.is_unique, ISNULL(i.filter_definition, N''),
                   STRING_AGG(c.name, N',') WITHIN GROUP (ORDER BY ic.key_ordinal)
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID(N'dbo.Invitation')
            GROUP BY i.name, i.is_unique, i.filter_definition
            """);

        Assert.Contains(indexes, row => row[3] == "TokenHash" && row[1] == "True");
        Assert.Contains(indexes, row => row[3].StartsWith("SpaceId,NormalizedEmailAddress", StringComparison.Ordinal));
        Assert.DoesNotContain(indexes, row => row[1] == "True" && row[2].Contains("State", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Q6 (Lee), AD-2 variance, AD-10 - <c>OutboxMessage</c> has no Space column, no security predicate and no foreign key,
    /// so the process-start sweep reads it with no session key (no second key-setting line). Anchor: the table exists with
    /// its rendered-content columns.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC6 - the OutboxMessage table arrives with the Invitations migration, story 4.1 Task 4")]
    [Trait("Requirement", "AR-33")]
    [Trait("Requirement", "AD-10")]
    public async Task The_outbox_has_no_Space_column_no_policy_and_no_foreign_key()
    {
        _ = Available();
        var wire = Wire();

        var columns = (await wire.QueryAsync("SELECT name FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.OutboxMessage') ORDER BY name"))
            .Select(row => row[0])
            .ToList();
        var predicates = await wire.QueryAsync("SELECT 1 FROM sys.security_predicates WHERE target_object_id = OBJECT_ID(N'dbo.OutboxMessage')");
        var keys = await wire.QueryAsync("SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.OutboxMessage')");

        Assert.Superset(new HashSet<string>(["Attempts", "Body", "ClaimedAt", "EnqueuedAt", "Id", "Kind", "Recipient", "Subject"], StringComparer.Ordinal), columns.ToHashSet(StringComparer.Ordinal));
        Assert.DoesNotContain(columns, column => column.Contains("Space", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(predicates);
        Assert.Empty(keys);
    }
}
