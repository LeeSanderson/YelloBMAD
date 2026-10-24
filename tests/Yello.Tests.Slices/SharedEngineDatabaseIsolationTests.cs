using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices;

/// <summary>
/// Sharing one engine across test classes must not share data across them: every class's database is
/// its own, and a row written by one class is invisible to another. Story 1.9 Task 2, AC12.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a test and not a remark.</b> Before story 1.9 each class had an engine of its own,
/// so cross-class leakage could not happen. One engine per process makes it a new risk: a class that
/// connected to <c>master</c>, or to another class's database, would read rows it never wrote and a
/// case asserting "only my rows" could pass or fail by which class ran first. The engine closes that by
/// handing every class a database of its own; this class proves the three properties that make it
/// hold.
/// </para>
/// <para>
/// <b>Planted signal first.</b> The row is read back from its own database before its absence from the
/// other is asserted, so the absence cannot be a write that never happened.
/// </para>
/// </remarks>
/// <param name="engine">The process's one SQL Server.</param>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-35")]
[Trait("Requirement", "NFR-1")]
public sealed class SharedEngineDatabaseIsolationTests(SqlServerEngine engine)
{
    [Fact]
    public async Task Two_classes_on_one_engine_get_two_databases_and_neither_sees_the_others_rows()
    {
        Assert.SkipWhen(SqlServerEngine.UnavailableReason is not null, SqlServerEngine.UnavailableReason ?? string.Empty);

        var cancellation = TestContext.Current.CancellationToken;
        var first = await engine.CreateDatabaseAsync(cancellation);
        var second = await engine.CreateDatabaseAsync(cancellation);

        try
        {
            var firstName = new SqlConnectionStringBuilder(first).InitialCatalog;
            var secondName = new SqlConnectionStringBuilder(second).InitialCatalog;

            // Each connection string names its own catalogue, never master and never the other's.
            Assert.StartsWith("yello_", firstName, StringComparison.Ordinal);
            Assert.StartsWith("yello_", secondName, StringComparison.Ordinal);
            Assert.NotEqual(firstName, secondName);
            Assert.Equal(firstName, await ScalarAsync(first, "SELECT DB_NAME()"));
            Assert.Equal(secondName, await ScalarAsync(second, "SELECT DB_NAME()"));

            var marker = Guid.NewGuid().ToString("N");
            await ScalarAsync(first, $"CREATE TABLE dbo.Marker (Value nvarchar(32) NOT NULL); INSERT dbo.Marker VALUES ('{marker}'); SELECT 1");

            // The planted signal, read back where it was written.
            Assert.Equal(marker, await ScalarAsync(first, "SELECT TOP (1) Value FROM dbo.Marker"));

            // And absent from the other class's database: not even the table exists there.
            Assert.Equal("absent", await ScalarAsync(second, "SELECT CASE WHEN OBJECT_ID('dbo.Marker') IS NULL THEN 'absent' ELSE 'present' END"));
        }
        finally
        {
            await engine.DropDatabaseAsync(first);
            await engine.DropDatabaseAsync(second);
        }
    }

    [Fact]
    public async Task A_dropped_database_is_gone_from_the_engine()
    {
        Assert.SkipWhen(SqlServerEngine.UnavailableReason is not null, SqlServerEngine.UnavailableReason ?? string.Empty);

        var cancellation = TestContext.Current.CancellationToken;
        var database = await engine.CreateDatabaseAsync(cancellation);
        var name = new SqlConnectionStringBuilder(database).InitialCatalog;
        var server = await engine.ServerConnectionStringAsync(cancellation);

        // Anchor: the database exists before the drop.
        Assert.Equal("1", await ScalarAsync(server, $"SELECT COUNT(*) FROM sys.databases WHERE name = '{name}'"));

        await engine.DropDatabaseAsync(database);

        Assert.Equal("0", await ScalarAsync(server, $"SELECT COUNT(*) FROM sys.databases WHERE name = '{name}'"));
    }

    private static async Task<string?> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Test-only statements built from GUIDs and literals above; no input reaches them.
        command.CommandText = sql;
#pragma warning restore CA2100

        return (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))?.ToString();
    }
}
