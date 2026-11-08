using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// The Ownership Offer's schema, read from the catalogue of this class's migrated database: the table under the Space policy
/// with its filter and both block predicates, its one foreign key (the cascade from <c>Space</c>; none to <c>Membership</c> or
/// <c>Account</c>), its database-clock expiry, and the filtered unique index that lets a Space hold at most one
/// <c>Pending</c> offer - proved by the database refusing a second raw insert, so it is the index that refuses and not the
/// code.
/// </summary>
/// <remarks>
/// <b>Catalogue cases read the database only</b>; no Host starts. Until the migration exists each catalogue query returns no
/// rows. The index case writes two rows directly under the Space's key in a Space seeded with its Owner, whose Account is
/// registered through the pooled Host first (<c>FK_Membership_Account_AccountId</c>); the offer rows name that Space
/// (<c>FK_OwnershipOffer_Space_SpaceId</c>) and Membership ids no key checks.
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// <c>dbo.SpaceIsolationPolicy</c> carries a FILTER, a BLOCK AFTER INSERT and a BLOCK AFTER UPDATE predicate on
    /// <c>dbo.OwnershipOffer</c>, so an offer cannot be read, written or moved under another Space's key.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC12 - the OwnershipOffers migration and its RLS predicates do not exist yet")]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "AR-4")]
    public async Task The_OwnershipOffer_table_is_under_the_Space_policy_with_its_filter_and_both_block_predicates()
    {
        _ = Available();

        var predicates = await Wire().QueryAsync("""
            SELECT p.predicate_type_desc, ISNULL(p.operation_desc, N'')
            FROM sys.security_predicates p
            JOIN sys.security_policies s ON s.object_id = p.object_id
            WHERE s.name = N'SpaceIsolationPolicy' AND p.target_object_id = OBJECT_ID(N'dbo.OwnershipOffer')
            ORDER BY 1, 2
            """);

        Assert.Equal(
            ["BLOCK AFTER INSERT", "BLOCK AFTER UPDATE", "FILTER "],
            predicates.Select(row => $"{row[0]} {row[1]}"));
    }

    /// <summary>
    /// The offer's only foreign key is the cascade from its Space: a Space deletion stays one statement and takes its offers,
    /// and a retained offer naming a removed Membership or a deleted Account fails no key, because none reaches
    /// <c>Membership</c> or <c>Account</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC11, AC12 - the OwnershipOffers migration does not exist yet (Lee's Q2 (a))")]
    [Trait("Requirement", "FR-7")]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "AD-5")]
    public async Task An_OwnershipOffer_cascades_from_its_Space_and_references_nothing_else()
    {
        _ = Available();

        var keys = await Wire().QueryAsync("""
            SELECT OBJECT_NAME(f.referenced_object_id), f.delete_referential_action_desc, f.name
            FROM sys.foreign_keys f
            WHERE f.parent_object_id = OBJECT_ID(N'dbo.OwnershipOffer')
            ORDER BY 1
            """);

        Assert.Equal(["Space CASCADE FK_OwnershipOffer_Space_SpaceId"], keys.Select(row => $"{row[0]} {row[1]} {row[2]}"));
    }

    /// <summary>
    /// <c>ExpiresAt</c> is a <c>datetimeoffset</c> whose default is seven days after <c>SYSDATETIMEOFFSET()</c>, so the stamp
    /// and the shared actionable predicate read one clock; <c>State</c> is a string of at most 16 characters, as the
    /// Invitation's is.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC10 - the OwnershipOffers migration and its ExpiresAt default do not exist yet")]
    [Trait("Requirement", "AR-31")]
    [Trait("Requirement", "AD-27")]
    [Trait("Assumption", "PRD-12-3")]
    public async Task The_offer_ExpiresAt_defaults_to_seven_days_after_the_database_clock()
    {
        _ = Available();

        var columns = await Wire().QueryAsync("""
            SELECT c.name, TYPE_NAME(c.user_type_id), c.max_length, LOWER(REPLACE(REPLACE(ISNULL(d.definition, N''), N'(', N''), N')', N''))
            FROM sys.columns c
            LEFT JOIN sys.default_constraints d ON d.object_id = c.default_object_id
            WHERE c.object_id = OBJECT_ID(N'dbo.OwnershipOffer') AND c.name IN (N'ExpiresAt', N'State')
            ORDER BY c.name
            """);

        Assert.Equal(2, columns.Count);

        var expiresAt = columns[0];
        var state = columns[1];

        Assert.Equal("datetimeoffset", expiresAt[1]);
        Assert.Contains("sysdatetimeoffset", expiresAt[3], StringComparison.Ordinal);
        Assert.Contains("day", expiresAt[3], StringComparison.Ordinal);
        Assert.Contains(OwnershipOfferWire.ExpiryDays.ToString(System.Globalization.CultureInfo.InvariantCulture), expiresAt[3], StringComparison.Ordinal);
        Assert.Equal(["State", "nvarchar", "32"], state[..3]);
    }

    /// <summary>
    /// The filtered unique index <c>UX_OwnershipOffer_SpaceId_Pending</c> exists on <c>(SpaceId)</c> filtered to
    /// <c>State = Pending</c>, and the database itself refuses a second <c>Pending</c> row in one Space with error 2601 naming
    /// it. A <c>Revoked</c> row and a <c>Pending</c> row coexist, and another Space's <c>Pending</c> row is unaffected.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 - the OwnershipOffers migration and UX_OwnershipOffer_SpaceId_Pending do not exist yet")]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AR-12")]
    public async Task The_database_refuses_a_second_pending_offer_in_a_Space_by_its_filtered_unique_index()
    {
        var database = Available();
        var wire = Wire();
        var (spaceId, otherSpaceId) = (Guid.NewGuid(), Guid.NewGuid());
        var address = RandomAddress();

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            _ = await SignedInAsync(client, address);
            var accountId = await AccountIdOfAsync(database, address);

            await SeedNamedSpaceAsync(database, spaceId, "Indexed " + Suffix(), accountId, Role.Owner);
            await SeedNamedSpaceAsync(database, otherSpaceId, "Indexed elsewhere " + Suffix(), accountId, Role.Owner);
        });

        var index = Assert.Single(await wire.QueryAsync("""
            SELECT i.name, i.is_unique, ISNULL(i.filter_definition, N''),
                   STRING_AGG(c.name, N',') WITHIN GROUP (ORDER BY ic.key_ordinal)
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID(N'dbo.OwnershipOffer') AND i.name = N'UX_OwnershipOffer_SpaceId_Pending'
            GROUP BY i.name, i.is_unique, i.filter_definition
            """));

        Assert.Equal("True", index[1]);
        Assert.Contains("[State]", index[2], StringComparison.Ordinal);
        Assert.Contains("Pending", index[2], StringComparison.Ordinal);
        Assert.Equal("SpaceId", index[3]);

        var (owner, expires) = ((await wire.MembershipRowsInAsync(spaceId)).Single().Id, (await wire.DatabaseNowAsync()).AddDays(OwnershipOfferWire.ExpiryDays));

        _ = await wire.AddOwnershipOfferAsync(spaceId, Guid.NewGuid(), owner, "Indexer", "Revoked", expires);
        _ = await wire.AddOwnershipOfferAsync(spaceId, Guid.NewGuid(), owner, "Indexer", "Pending", expires);
        _ = await wire.AddOwnershipOfferAsync(otherSpaceId, Guid.NewGuid(), owner, "Indexer", "Pending", expires);

        var refused = await Assert.ThrowsAsync<SqlException>(() => wire.AddOwnershipOfferAsync(spaceId, Guid.NewGuid(), owner, "Indexer", "Pending", expires));

        Assert.Equal(2601, refused.Number);
        Assert.Contains("UX_OwnershipOffer_SpaceId_Pending", refused.Message, StringComparison.Ordinal);
        Assert.Equal(["Pending", "Revoked"], (await wire.OwnershipOffersInAsync(spaceId)).Select(row => row.State).Order(StringComparer.Ordinal));
        Assert.Single(await wire.OwnershipOffersInAsync(otherSpaceId));
    }
}
