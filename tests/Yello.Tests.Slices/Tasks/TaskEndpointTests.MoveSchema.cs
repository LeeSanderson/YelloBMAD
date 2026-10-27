using System.Net;
using System.Text;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Domain.Accounts;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.DeleteWire;
using static Yello.Tests.Slices.Tasks.MoveWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Story 2.6 AC4 and AC16 against the migrated catalogue: risk R7 / scenario A-6 - the position key's
/// <c>Latin1_General_100_BIN2</c> asserted on the column <b>and</b> on the column as a key column of the unique position
/// index, and the mixed-case ordering case that makes the collation functional rather than declared; and Q12's email
/// index collation (<c>deferred-work.md:198</c>). AR-21 with AD-15. Written by the story 2.6 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>SCH-601 and SCH-602 are green today</b>: 2.2 declared the column BIN2 at its definition and built the index over it,
/// so these are the full A-6 assertion 2.2 left to this story (<c>TaskEndpointTests.Schema.cs:20</c>), run as standing
/// gates of R7 on every engine pull (Q13 (a)). The dev phase proves SCH-602 against a plant - the column re-collated to the
/// default (or a scratch table at the default collation) - and records it; a pass that cannot fail is not a test.
/// </para>
/// <para>
/// <b>SCH-603 and SCH-604 are red</b> (<c>red: AC16</c>) until Q12 (a)'s migration declares
/// <c>NormalizedEmailAddress</c> <c>Latin1_General_100_BIN2</c>: the column takes the database default today, which on the
/// Linux image is a width-insensitive collation, so an address <see cref="EmailAddressNormalisation.Normalise"/> maps to a
/// different string can still equal a registered one in the unique index.
/// </para>
/// </remarks>
public sealed partial class TaskEndpointTests
{
    private const string PositionIndex = "IX_Task_SpaceId_ProjectId_StatusId_PositionKey";

    /// <summary>
    /// SCH-601 (AC4, AC16, R7, A-6, Q11) - <c>PositionKey</c> is a key column of the unique position index, and the index
    /// key carries <c>Latin1_General_100_BIN2</c>, read through <c>sys.indexes</c> → <c>sys.index_columns</c> →
    /// <c>sys.columns</c>, so an index recreated over a re-collated column is caught here and not only on the column. The
    /// index's key is <c>(SpaceId, ProjectId, StatusId, PositionKey)</c> - AC4 names the tuple without <c>SpaceId</c>; Q11
    /// (a) keeps 2.2's leading <c>SpaceId</c> and records the variance. <b>Green today</b>; a standing gate.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-21")]
    [Trait("Requirement", "AD-15")]
    public async Task The_position_index_carries_the_key_column_in_BIN2()
    {
        var database = Available();

        var keyColumns = await CatalogueAsync(
            database,
            $"""
            SELECT CONCAT(c.name COLLATE DATABASE_DEFAULT, N'|', ISNULL(c.collation_name COLLATE DATABASE_DEFAULT, N''), N'|', CAST(i.is_unique AS int))
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID(N'dbo.[Task]') AND i.name = N'{PositionIndex}' AND ic.is_included_column = 0
            ORDER BY ic.key_ordinal
            """);

        // Anchor: the index is there and is read whole, in key order.
        Assert.Equal(4, keyColumns.Count);
        Assert.Equal("PositionKey|Latin1_General_100_BIN2|1", keyColumns[3]);
        Assert.Equal(["SpaceId", "ProjectId", "StatusId", "PositionKey"], keyColumns.Select(column => column.Split('|')[0]));
    }

    /// <summary>
    /// SCH-602 (AC4's reason, AC16, R7 functional half, <c>test-design-architecture.md:506-520</c>) - in one column, keys
    /// that differ only in case (<c>a0</c> and <c>A0</c>, <c>b</c> and <c>B</c>, <c>Z</c> and <c>z</c>) all insert (no false
    /// duplicate on the unique index) and the Board serves them in ordinal order - digits, then upper case, then lower case,
    /// as <see cref="StringComparer.Ordinal"/> orders them - never in a case-insensitive interleaving. <b>Green today</b>;
    /// the dev phase plants a default-collation column and records it caught.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-21")]
    [Trait("Requirement", "AD-15")]
    public async Task Keys_differing_only_in_case_coexist_in_one_column_and_read_back_in_ordinal_order()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        var statusId = Guid.Empty;
        var seeded = new Dictionary<string, Guid>(StringComparer.Ordinal);
        WireResponse? board = null;

        string[] keys = ["b", "A0", "z", "a0", "B", "Z", "0z"];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projectId = await CreateProjectAsync(owner, "Mixed case");
            statusId = (await StatusesAsync(database, owner.SpaceId))[1].Id;

            foreach (var key in keys)
            {
                seeded[key] = await SeedTaskAtAsync(database, owner.SpaceId, projectId, statusId, key, "Keyed " + key);
            }

            board = await BoardAsync(owner, owner.SpaceId, projectId);
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.OK, board?.StatusCode);

        var expected = keys.Order(StringComparer.Ordinal).Select(key => seeded[key]).ToList();

        // Anchor: the ordinal order is not the case-insensitive one, so the assertion can tell them apart.
        Assert.NotEqual(keys.Order(StringComparer.Ordinal), keys.Order(StringComparer.OrdinalIgnoreCase));
        Assert.Equal(keys.Length, seeded.Count);
        Assert.Equal(expected, Columns(board!).Single(column => column.StatusId == statusId).TaskIds);
        Assert.Equal(expected, await ColumnAsync(database, owner.SpaceId, projectId, statusId));
    }

    /// <summary>
    /// SCH-603 (AC16, Q12 (a), <c>deferred-work.md:198</c>) - <c>Account.NormalizedEmailAddress</c>, the column the email
    /// unique index is built on, is declared <c>Latin1_General_100_BIN2</c>, so the index compares exactly what
    /// <see cref="EmailAddressNormalisation.Normalise"/> stores.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC16 - Q12 (a)'s migration has not declared NormalizedEmailAddress BIN2; it takes the database default")]
    [Trait("Requirement", "FR-1")]
    [Trait("Requirement", "AD-15")]
    public async Task The_normalised_email_column_is_BIN2()
    {
        var database = Available();

        Assert.Equal(
            ["Latin1_General_100_BIN2"],
            await CatalogueAsync(
                database,
                "SELECT c.collation_name FROM sys.columns c WHERE c.object_id = OBJECT_ID(N'dbo.[Account]') AND c.name = N'NormalizedEmailAddress'"));
    }

    /// <summary>
    /// SCH-604 (AC16, Q12 (a), FR-1) - an address <see cref="EmailAddressNormalisation.Normalise"/> maps to a <b>different</b>
    /// string from a registered one - the same letters in full-width forms - is not equal to it in the column the unique
    /// index compares, so it could never collide with the registered Account (AD-23 would answer that collision as a
    /// silent <c>204</c>). Under the default, width-insensitive collation the two compare equal and this fails.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC16 - Q12 (a): under the default collation a full-width spelling equals the registered address")]
    [Trait("Requirement", "FR-1")]
    [Trait("Requirement", "AD-23")]
    public async Task An_address_that_normalises_differently_never_equals_a_registered_one_in_the_email_index()
    {
        var database = Available();
        Actor? owner = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
        });

        Assert.NotNull(owner);

        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var read = connection.CreateCommand();

        read.CommandText = "SELECT NormalizedEmailAddress FROM dbo.[Account] WHERE Id = @id";
        read.Parameters.Add(new SqlParameter("@id", owner.AccountId));

        var stored = Assert.IsType<string>(await read.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        var widened = EmailAddressNormalisation.Normalise(FullWidth(stored));

        // Anchor: the function really does map the two to different strings.
        Assert.NotEqual(stored, widened);

        await using var probe = connection.CreateCommand();

        probe.CommandText = "SELECT COUNT(*) FROM dbo.[Account] WHERE NormalizedEmailAddress = @widened";
        probe.Parameters.Add(new SqlParameter("@widened", System.Data.SqlDbType.NVarChar, 256) { Value = widened });

        Assert.Equal(0, Convert.ToInt32(await probe.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture));

        // Anchor: the exact stored value is found, so a zero above is about the collation, not the query.
        await using var exact = connection.CreateCommand();

        exact.CommandText = "SELECT COUNT(*) FROM dbo.[Account] WHERE NormalizedEmailAddress = @stored";
        exact.Parameters.Add(new SqlParameter("@stored", System.Data.SqlDbType.NVarChar, 256) { Value = stored });

        Assert.Equal(1, Convert.ToInt32(await exact.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The full-width form of every ASCII letter in <paramref name="text"/> (U+FF21-U+FF3A, U+FF41-U+FF5A); everything
    /// else is kept.
    /// </summary>
    private static string FullWidth(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var character in text)
        {
            builder.Append(character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') ? char.ConvertFromUtf32(character + 0xFEE0) : character.ToString());
        }

        return builder.ToString();
    }
}
