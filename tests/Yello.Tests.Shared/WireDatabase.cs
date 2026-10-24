using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace Yello.Tests.Shared;

/// <summary>
/// The raw SQL the wire suites need, for what no endpoint does yet: reading the refusal record and the
/// Token rows, adding a second owned Space, and the revocation events (Membership removal, Space and
/// Account deletion, a Role change).
/// </summary>
/// <remarks>
/// <para>
/// <b>Raw SQL only where no endpoint exists</b> (story 1.9 Task 3). Seeding is through the product -
/// <see cref="ProductActor"/> registers, signs in and issues a Token over HTTP. The events here arrive
/// as endpoints in stories 3.3, 4.5 and 5.4; until then the schema and the very next request's answer
/// are what these statements let a case prove. They mirror
/// <c>ActiveSpaceEndpointTests.Tokens.cs</c>'s helpers, under the same session keys.
/// </para>
/// <para>
/// <b>One helper for both suites</b>, not one per suite: the statements are the same, and two copies of
/// a session-key dance is one more place for them to drift apart.
/// </para>
/// <para>
/// <c>Account</c>, <c>Space</c>, <c>AccessRefusal</c> and <c>ApiToken</c> carry no row-level security
/// policy; <c>Membership</c> does, so a statement on it runs under the Space's key (or, for an Account's
/// Memberships across Spaces, the <c>AccountId</c> key story 1.7 added).
/// </para>
/// </remarks>
/// <param name="connectionString">The class's own database.</param>
public sealed class WireDatabase(string connectionString)
{
    /// <summary>
    /// The class's own database's connection string.
    /// </summary>
    public string ConnectionString => connectionString;

    /// <summary>
    /// The Account registered with <paramref name="emailAddress"/>, exactly as it was submitted.
    /// </summary>
    /// <param name="emailAddress">The address.</param>
    /// <returns>Its id.</returns>
    public async Task<Guid> AccountIdOfAsync(string emailAddress)
    {
        var id = await ScalarAsync(
            "SELECT Id FROM dbo.[Account] WHERE EmailAddress = @address",
            ("@address", emailAddress)).ConfigureAwait(false);

        return id is Guid guid ? guid : throw new InvalidOperationException($"No Account is registered as {emailAddress}.");
    }

    /// <summary>
    /// The kind of every refusal recorded for (<paramref name="accountId"/>, <paramref name="targetSpaceId"/>),
    /// oldest first.
    /// </summary>
    /// <param name="accountId">The acting Account.</param>
    /// <param name="targetSpaceId">The Space in the route.</param>
    /// <returns>The kinds, as stored (<c>CrossSpace</c>, <c>InsufficientRole</c>).</returns>
    public async Task<IReadOnlyList<string>> RefusalKindsAsync(Guid accountId, Guid targetSpaceId)
    {
        await using var connection = await OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        command.CommandText =
            "SELECT Kind FROM dbo.[AccessRefusal] WHERE AccountId = @account AND TargetSpaceId = @space ORDER BY OccurredAt, Id";
        command.Parameters.Add(new SqlParameter("@account", accountId));
        command.Parameters.Add(new SqlParameter("@space", targetSpaceId));

        var kinds = new List<string>();

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            kinds.Add(reader.GetString(0));
        }

        return kinds;
    }

    /// <summary>
    /// The number of Tokens <paramref name="accountId"/> holds bound to <paramref name="boundSpaceId"/>.
    /// </summary>
    /// <param name="accountId">The Account.</param>
    /// <param name="boundSpaceId">The Space.</param>
    /// <returns>The count.</returns>
    public async Task<int> TokenCountAsync(Guid accountId, Guid boundSpaceId) =>
        Convert.ToInt32(
            await ScalarAsync(
                "SELECT COUNT(*) FROM dbo.[ApiToken] WHERE AccountId = @account AND BoundSpaceId = @space",
                ("@account", accountId),
                ("@space", boundSpaceId)).ConfigureAwait(false),
            System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// A second Space owned by <paramref name="accountId"/>: the <c>Space</c> row, then its Owner
    /// <c>Membership</c>, under that Space's key (no endpoint creates a Space yet).
    /// </summary>
    /// <param name="accountId">The owner.</param>
    /// <param name="name">The Space's name.</param>
    /// <returns>The new Space's id.</returns>
    public async Task<Guid> AddOwnedSpaceAsync(Guid accountId, string name)
    {
        var spaceId = Guid.NewGuid();

        await using var connection = await OpenForSpaceAsync(spaceId).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);

        await ExecuteAsync(
            connection,
            transaction,
            "INSERT dbo.[Space] (Id, Name, CreatedAt) VALUES (@space, @name, SYSDATETIMEOFFSET())",
            ("@space", spaceId),
            ("@name", name)).ConfigureAwait(false);

        await ExecuteAsync(
            connection,
            transaction,
            "INSERT dbo.[Membership] (Id, SpaceId, AccountId, Role, CreatedAt) VALUES (@id, @space, @account, N'Owner', SYSDATETIMEOFFSET())",
            ("@id", Guid.NewGuid()),
            ("@space", spaceId),
            ("@account", accountId)).ConfigureAwait(false);

        await transaction.CommitAsync().ConfigureAwait(false);

        return spaceId;
    }

    /// <summary>
    /// Removes a Membership under its Space's own key, committed or rolled back. The rolled-back form
    /// is the negative anchor: the refusals after a committed removal are the removal, not the attempt.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="accountId">The member.</param>
    /// <param name="commit">Whether to commit.</param>
    /// <returns>A task.</returns>
    public async Task RemoveMembershipAsync(Guid spaceId, Guid accountId, bool commit)
    {
        await using var connection = await OpenForSpaceAsync(spaceId).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);

        var removed = await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM dbo.[Membership] WHERE SpaceId = @space AND AccountId = @account",
            ("@space", spaceId),
            ("@account", accountId)).ConfigureAwait(false);

        if (removed != 1)
        {
            throw new InvalidOperationException($"Expected to remove one Membership; removed {removed}.");
        }

        if (commit)
        {
            await transaction.CommitAsync().ConfigureAwait(false);
        }
        else
        {
            await transaction.RollbackAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Deletes a Space under its own key, so the FILTER predicate shows every Space-scoped row the
    /// cascades must reach (<c>Membership</c>, <c>StatusDefinition</c>, <c>SpacePresence</c>, and
    /// <c>ApiToken</c> through <c>Membership</c>).
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>A task.</returns>
    public async Task DeleteSpaceAsync(Guid spaceId)
    {
        await using var connection = await OpenForSpaceAsync(spaceId).ConfigureAwait(false);

        var deleted = await ExecuteAsync(connection, null, "DELETE FROM dbo.[Space] WHERE Id = @space", ("@space", spaceId)).ConfigureAwait(false);

        if (deleted != 1)
        {
            throw new InvalidOperationException($"Expected to delete one Space; deleted {deleted}.");
        }
    }

    /// <summary>
    /// Deletes an Account the only way the schema allows - every Membership first (<c>Restrict</c>),
    /// then the Account - in one transaction, under the <c>AccountId</c> key.
    /// </summary>
    /// <param name="accountId">The Account.</param>
    /// <returns>A task.</returns>
    public async Task DeleteAccountAsync(Guid accountId)
    {
        await using var connection = await OpenAsync().ConfigureAwait(false);
        await SetKeyAsync(connection, "AccountId", accountId).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);

        await ExecuteAsync(connection, transaction, "DELETE FROM dbo.[Membership] WHERE AccountId = @account", ("@account", accountId)).ConfigureAwait(false);

        var deleted = await ExecuteAsync(connection, transaction, "DELETE FROM dbo.[Account] WHERE Id = @account", ("@account", accountId)).ConfigureAwait(false);

        if (deleted != 1)
        {
            throw new InvalidOperationException($"Expected to delete one Account; deleted {deleted}.");
        }

        await transaction.CommitAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Changes one Membership's Role under its Space's key.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="accountId">The member.</param>
    /// <param name="roleStableCode">The new Role's stable code, for example <c>viewer</c>.</param>
    /// <returns>A task.</returns>
    public async Task ChangeRoleAsync(Guid spaceId, Guid accountId, string roleStableCode)
    {
        ArgumentException.ThrowIfNullOrEmpty(roleStableCode);

        // Roles are stored by their names (MembershipConfiguration's HasConversion<string>); the stable
        // code is the name in lower case.
        var stored = char.ToUpperInvariant(roleStableCode[0]) + roleStableCode[1..];

        await using var connection = await OpenForSpaceAsync(spaceId).ConfigureAwait(false);

        var changed = await ExecuteAsync(
            connection,
            null,
            "UPDATE dbo.[Membership] SET Role = @role WHERE SpaceId = @space AND AccountId = @account",
            ("@role", stored),
            ("@space", spaceId),
            ("@account", accountId)).ConfigureAwait(false);

        if (changed != 1)
        {
            throw new InvalidOperationException($"Expected to change one Membership; changed {changed}.");
        }
    }

    /// <summary>
    /// The Account's <c>SessionsValidAfter</c> stamp, or <c>null</c>.
    /// </summary>
    /// <param name="accountId">The Account.</param>
    /// <returns>The stamp.</returns>
    public async Task<DateTimeOffset?> SessionsValidAfterAsync(Guid accountId) =>
        await ScalarAsync("SELECT SessionsValidAfter FROM dbo.[Account] WHERE Id = @account", ("@account", accountId)).ConfigureAwait(false)
            is DateTimeOffset stamped
            ? stamped
            : null;

    /// <summary>
    /// The physical sessions open on this database whose program name is <paramref name="programName"/>.
    /// </summary>
    /// <param name="programName">The client's <c>Application Name</c>.</param>
    /// <returns>The count.</returns>
    public async Task<int> SessionCountAsync(string programName) =>
        Convert.ToInt32(
            await ScalarAsync(
                "SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE database_id = DB_ID() AND program_name = @program",
                ("@program", programName)).ConfigureAwait(false),
            System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// The rows of a catalogue query, each column as text.
    /// </summary>
    /// <param name="sql">A constant query written by a test.</param>
    /// <returns>The rows.</returns>
    public async Task<IReadOnlyList<string[]>> QueryAsync(string sql)
    {
        await using var connection = await OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Review SQL queries - test-written constant catalogue queries only.
        command.CommandText = sql;
#pragma warning restore CA2100

        var rows = new List<string[]>();

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var row = new string[reader.FieldCount];

            for (var column = 0; column < row.Length; column++)
            {
                row[column] = await reader.IsDBNullAsync(column).ConfigureAwait(false)
                    ? string.Empty
                    : Convert.ToString(reader.GetValue(column), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            }

            rows.Add(row);
        }

        return rows;
    }

    private async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(connectionString);

        try
        {
            await connection.OpenAsync().ConfigureAwait(false);

            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task<SqlConnection> OpenForSpaceAsync(Guid spaceId)
    {
        var connection = await OpenAsync().ConfigureAwait(false);

        try
        {
            await SetKeyAsync(connection, "SpaceId", spaceId).ConfigureAwait(false);

            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task SetKeyAsync(SqlConnection connection, string key, Guid value)
    {
        await using var command = connection.CreateCommand();

        command.CommandText = "EXEC sp_set_session_context @key = @key, @value = @value, @read_only = 1";
        command.Parameters.Add(new SqlParameter("@key", SqlDbType.NVarChar, 128) { Value = key });
        command.Parameters.Add(new SqlParameter("@value", SqlDbType.Variant) { Value = value });

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private async Task<object?> ScalarAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Review SQL queries - constant statements in this class; values are parameters.
        command.CommandText = sql;
#pragma warning restore CA2100

        foreach (var (name, value) in parameters)
        {
            command.Parameters.Add(new SqlParameter(name, value));
        }

        var result = await command.ExecuteScalarAsync().ConfigureAwait(false);

        return result is DBNull ? null : result;
    }

    private static async Task<int> ExecuteAsync(
        SqlConnection connection,
        DbTransaction? transaction,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
#pragma warning disable CA2100 // Review SQL queries - constant statements in this class; values are parameters.
        command.CommandText = sql;
#pragma warning restore CA2100

        foreach (var (name, value) in parameters)
        {
            command.Parameters.Add(new SqlParameter(name, value));
        }

        return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
