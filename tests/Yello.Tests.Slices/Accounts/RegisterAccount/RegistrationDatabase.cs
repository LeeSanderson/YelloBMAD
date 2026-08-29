using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Yello.Infrastructure.Persistence;

namespace Yello.Tests.Slices.Accounts.RegisterAccount;

/// <summary>
/// A migrated database on a Testcontainers SQL Server, and the small number of things a test
/// needs to do to one.
/// </summary>
/// <remarks>
/// <para>
/// <b>A real SQL Server, never an in-memory provider and never SQLite.</b> Neither can exercise
/// row-level security, which is what NFR-1 rests on - and neither has a central package version,
/// so a project referencing one fails to restore rather than failing a review.
/// </para>
/// <para>
/// <b>Cleanup is container disposal, never delete statements.</b> A delete would itself need a
/// session context to see the rows it is removing, so a cleanup that appears to work may be
/// evidence that isolation is broken (<c>TESTING-CONVENTIONS.md:89</c>).
/// </para>
/// <para>
/// <b>Migrations are applied here, by a test, and nowhere near startup.</b> AR-36 forbids
/// applying them at startup and story 1.10 makes it an explicit deploy step; a test asking for a
/// schema is neither of those.
/// </para>
/// </remarks>
/// <param name="connectionString">The running container's connection string.</param>
internal sealed class RegistrationDatabase(string connectionString)
{
    /// <summary>
    /// Creates the schema by running the migrations - which is what makes every assertion here
    /// an assertion about the migration rather than about the model.
    /// </summary>
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync(cancellationToken);
    }

    /// <summary>
    /// A context bound to the container.
    /// </summary>
    public YelloDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<YelloDbContext>().UseSqlServer(connectionString).Options);

    /// <summary>
    /// Opens a connection, sets the row-level security session context to a Space, and hands it
    /// over.
    /// </summary>
    /// <remarks>
    /// A connection per call, deliberately. AD-2 sets the context <c>@read_only = 1</c>, so it
    /// cannot be changed again on the same session - which is the property that makes it a
    /// boundary rather than a suggestion, and which means a test looking at two Spaces needs two
    /// connections. Closing returns the connection to the pool, where
    /// <c>sp_reset_connection</c> clears the context.
    /// </remarks>
    public async Task<SqlConnection> OpenForSpaceAsync(Guid spaceId, CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC sp_set_session_context @key = @key, @value = @value, @read_only = 1";
            command.Parameters.Add(new SqlParameter("@key", SqlDbType.NVarChar, 128) { Value = "SpaceId" });
            command.Parameters.Add(new SqlParameter("@value", SqlDbType.Variant) { Value = spaceId });

            await command.ExecuteNonQueryAsync(cancellationToken);

            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Opens a connection with NO session context set at all.
    /// </summary>
    /// <remarks>
    /// This is the interesting one for isolation: with no context, the predicate compares a
    /// <c>SpaceId</c> against <c>NULL</c>, which is never true - so every Space-scoped row should
    /// be invisible. A test that only ever sets a context can never observe that.
    /// </remarks>
    public async Task<SqlConnection> OpenWithoutSpaceContextAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Counts rows in a table on an already-prepared connection.
    /// </summary>
    /// <remarks>
    /// The table name is interpolated rather than parameterised because SQL Server takes no
    /// parameter in that position - and the values are this class's own constants from
    /// <c>SchemaNames</c>, never anything a caller supplies.
    /// </remarks>
    public static async Task<int> CountAsync(
        SqlConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
        command.CommandText = $"SELECT COUNT(*) FROM dbo.[{table}]";
#pragma warning restore CA2100

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Every text value stored anywhere in the database, on an already-prepared connection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is what makes AC5's "the password appears in none of them" a real assertion.</b>
    /// Checking that <c>Account.PasswordHash</c> is not the password proves almost nothing: the
    /// interesting failure is a password copied somewhere nobody was looking - into a Space name,
    /// an audit column a later story adds, or a diagnostic table. So the assertion reads every
    /// character column of every user table from the catalogue rather than from a list, which
    /// means a table added by a later story is covered without anyone extending this method.
    /// </para>
    /// <para>
    /// Space-scoped rows are only visible with a session context set, so the caller decides which
    /// connection to pass - and passing one scoped to the registered Space is what makes the rows
    /// this story writes readable at all.
    /// </para>
    /// </remarks>
    public static async Task<IReadOnlyList<string>> ReadEveryTextValueAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        var columns = new List<(string Table, string Column)>();

        await using (var discover = connection.CreateCommand())
        {
            discover.CommandText = """
                SELECT t.name AS TableName, c.name AS ColumnName
                FROM sys.columns c
                JOIN sys.tables t ON t.object_id = c.object_id
                JOIN sys.types ty ON ty.user_type_id = c.user_type_id
                WHERE ty.name IN (N'nvarchar', N'varchar', N'nchar', N'char', N'ntext', N'text')
                  AND t.is_ms_shipped = 0
                ORDER BY t.name, c.name
                """;

            await using var reader = await discover.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        var values = new List<string>();

        foreach (var (table, column) in columns)
        {
            await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
            command.CommandText = $"SELECT CAST([{column}] AS nvarchar(max)) FROM dbo.[{table}]";
#pragma warning restore CA2100

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                if (!await reader.IsDBNullAsync(0, cancellationToken))
                {
                    values.Add(reader.GetString(0));
                }
            }
        }

        return values;
    }
}
