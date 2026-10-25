using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Deployment;

/// <summary>
/// The Host's database identity can read and write data and nothing more: it cannot switch off the
/// row-level security policy or relax <c>MAXDOP</c>. Only the migration identity alters schema.
/// Story 1.10 AC15 (AR-36, <c>deferred-work.md:242</c>); Tasks 6 and 12 section 9.
/// </summary>
/// <remarks>
/// <para>
/// <b>A standing gate.</b> It proves the permission model the runbook grants (<c>db_datareader</c> and
/// <c>db_datawriter</c> for the app identity, P2) on the local engine, which is the half of AC15's
/// identity split that can be decided without Azure. In Azure the users are contained database users
/// with passwords held only in Key Vault (open question 8 (b), <c>docs/operations.md</c> section 9);
/// locally they are SQL logins with the same roles. The roles are what is under test, not the login
/// type.
/// </para>
/// <para>
/// <b>Why it matters.</b> NFR-1 rests on the policy, and AD-2's <c>MAXDOP = 1</c> is AR-40c's
/// mitigation. An app identity that could run <c>ALTER SECURITY POLICY ... STATE = OFF</c> would turn
/// a SQL injection into a cross-Space read.
/// </para>
/// <para>
/// <b>Anchors:</b> the identity really is connected and can read (it reads
/// <c>__EFMigrationsHistory</c>), and after both refusals the policy is still enabled and
/// <c>MAXDOP</c> is still 1, read as the administrator.
/// </para>
/// <para>
/// <b>Cleanup.</b> The login is server-level, so it is dropped at the end; the database goes with the
/// class fixture. The login name and password are generated per run and never leave this process.
/// </para>
/// </remarks>
/// <param name="fixture">This class's migrated database on the shared engine.</param>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-36")]
[Trait("Requirement", "NFR-1")]
[Trait("Requirement", "AD-2")]
public sealed class DatabaseIdentityTests(MigratedDatabaseFixture fixture) : IClassFixture<MigratedDatabaseFixture>
{
    /// <summary>
    /// 1.10-SLC-003 - a data-only identity cannot disable the Space policy or change <c>MAXDOP</c>.
    /// </summary>
    /// <remarks>
    /// <b>Active - green today, a standing gate.</b> Plant: add the identity to <c>db_owner</c> instead;
    /// both refusals then fail to happen.
    /// </remarks>
    [Fact]
    public async Task The_app_identity_reads_and_writes_but_cannot_alter_isolation()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        var cancellationToken = TestContext.Current.CancellationToken;
        var administrator = fixture.ConnectionString!;
        var login = "yello_app_" + Guid.NewGuid().ToString("N")[..12];
        var password = "Pw-" + Guid.NewGuid().ToString("N") + "-aA1!";

        await CreateDataOnlyIdentityAsync(administrator, login, password, cancellationToken);

        var asApp = new SqlConnectionStringBuilder(administrator)
        {
            UserID = login,
            Password = password,
            IntegratedSecurity = false,
            Pooling = false,
        }.ConnectionString;

        try
        {
            await using var connection = new SqlConnection(asApp);
            await connection.OpenAsync(cancellationToken);

            // Anchor: connected, and able to read.
            Assert.True(await ScalarAsync(connection, "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory", cancellationToken) > 0);

            await Assert.ThrowsAsync<SqlException>(() =>
                ExecuteAsync(connection, "ALTER SECURITY POLICY dbo.SpaceIsolationPolicy WITH (STATE = OFF);", cancellationToken));

            await Assert.ThrowsAsync<SqlException>(() =>
                ExecuteAsync(connection, "ALTER DATABASE SCOPED CONFIGURATION SET MAXDOP = 0;", cancellationToken));
        }
        finally
        {
            await DropLoginAsync(administrator, login);
        }

        await using var admin = new SqlConnection(administrator);
        await admin.OpenAsync(cancellationToken);

        Assert.Equal(1, await ScalarAsync(admin, "SELECT CAST(is_enabled AS int) FROM sys.security_policies WHERE name = 'SpaceIsolationPolicy'", cancellationToken));
        Assert.Equal(1, await ScalarAsync(admin, "SELECT CAST(value AS int) FROM sys.database_scoped_configurations WHERE name = 'MAXDOP'", cancellationToken));
    }

    private static async Task CreateDataOnlyIdentityAsync(string administrator, string login, string password, CancellationToken cancellationToken)
    {
        var master = new SqlConnectionStringBuilder(administrator) { InitialCatalog = "master" }.ConnectionString;

        await using (var connection = new SqlConnection(master))
        {
            await connection.OpenAsync(cancellationToken);

            // The password is generated above from a GUID and passed as a literal because CREATE LOGIN
            // takes no parameter for it; the login name is generated the same way.
            await ExecuteAsync(connection, $"CREATE LOGIN [{login}] WITH PASSWORD = N'{password}', CHECK_POLICY = OFF;", cancellationToken);
        }

        await using var database = new SqlConnection(administrator);
        await database.OpenAsync(cancellationToken);

        await ExecuteAsync(
            database,
            $"CREATE USER [{login}] FOR LOGIN [{login}]; ALTER ROLE db_datareader ADD MEMBER [{login}]; ALTER ROLE db_datawriter ADD MEMBER [{login}];",
            cancellationToken);
    }

    private static async Task DropLoginAsync(string administrator, string login)
    {
        var master = new SqlConnectionStringBuilder(administrator) { InitialCatalog = "master" }.ConnectionString;

        try
        {
            await using var connection = new SqlConnection(master);
            await connection.OpenAsync();
            await ExecuteAsync(connection, $"DROP LOGIN [{login}];", CancellationToken.None);
        }
        catch (SqlException)
        {
            // Best effort: the engine's disposal removes the login with the container.
        }
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Review SQL queries - every name and the password are generated in this class from GUIDs.
        command.CommandText = sql;
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> ScalarAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Review SQL queries - constant text in this class.
        command.CommandText = sql;
#pragma warning restore CA2100

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }
}
