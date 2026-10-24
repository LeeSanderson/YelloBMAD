using Microsoft.Data.SqlClient;
using Xunit;

namespace Yello.Tests.Shared;

/// <summary>
/// The one SQL Server a test process starts, and the factory for each test class's own database
/// on it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Declared once per suite as an xunit v3 assembly fixture</b>
/// (<c>[assembly: AssemblyFixture(typeof(SqlServerEngine))]</c>), so xunit creates one instance per
/// test assembly - one per process under Microsoft.Testing.Platform - and disposes it at the end of
/// the run. A test class or class fixture takes it as a constructor parameter. Story 1.9 Q1 (a).
/// </para>
/// <para>
/// <b>Started on first use, not at assembly start.</b> Most of a suite's classes need no database
/// (the inverted-guard cases, the probe cases, the gates), and an assembly fixture that started an
/// engine eagerly would couple every one of them to a container start. The first
/// <see cref="CreateDatabaseAsync"/> starts it; every later call reuses it. A failed start is kept
/// and re-thrown to every later caller, so one dead runtime costs one deadline rather than one per
/// class.
/// </para>
/// <para>
/// <b>A database per class is the isolation between classes.</b> Each database is named
/// <c>yello_{guid:N}</c> and every connection string handed out names it as its
/// <c>Initial Catalog</c>, so two classes on one engine share nothing but the process. The schema
/// is per database too: the migration's <c>ALTER DATABASE SCOPED CONFIGURATION SET MAXDOP = 1</c> is
/// database-scoped, and row-level security policies are objects in the database that holds them.
/// </para>
/// <para>
/// <b>A dead runtime fails; an unconfigured one skips</b> (Q1's sub-question). Before starting, the
/// engine pings the daemon with <see cref="SqlServerContainerFixture.ProbeContainerRuntimeAsync(TimeSpan)"/>
/// and fails with its reason, rather than leaving the start to time out five minutes later.
/// </para>
/// </remarks>
public sealed class SqlServerEngine : IAsyncLifetime
{
    private static readonly TimeSpan ProbeDeadline = TimeSpan.FromSeconds(15);

    private readonly SemaphoreSlim _gate = new(1, 1);

    private SqlServerContainerFixture? _container;

    private Exception? _failure;

    private int _databasesCreated;

    /// <summary>
    /// How many databases this engine has created - each one a test class's own.
    /// </summary>
    public int DatabasesCreated => Volatile.Read(ref _databasesCreated);

    /// <summary>
    /// Why there is no engine for this process, or <c>null</c> when a runtime is configured. A
    /// consumer skips with this; a configured runtime that is dead fails instead.
    /// </summary>
    public static string? UnavailableReason =>
        SqlServerContainerFixture.IsContainerRuntimeAvailable()
            ? null
            : "No container runtime is configured, so there is no SQL Server for this suite. This is a skip " +
              "rather than a failure because an absent Rancher Desktop is a local condition, not a defect.";

    /// <inheritdoc />
    /// <remarks>Nothing starts here: see the class remarks on starting at first use.</remarks>
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// The server's connection string, on <c>master</c>. For the cases whose subject is the engine or
    /// the Host's connection to it, not a schema.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The connection string.</returns>
    public async Task<string> ServerConnectionStringAsync(CancellationToken cancellationToken)
    {
        var container = await StartedAsync(cancellationToken).ConfigureAwait(false);

        return container.ConnectionString;
    }

    /// <summary>
    /// Creates an empty database of the caller's own on the engine and returns its connection string.
    /// </summary>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A connection string whose <c>Initial Catalog</c> is the new database.</returns>
    public async Task<string> CreateDatabaseAsync(CancellationToken cancellationToken)
    {
        var server = await ServerConnectionStringAsync(cancellationToken).ConfigureAwait(false);
        var name = "yello_" + Guid.NewGuid().ToString("N");

        await using (var connection = new SqlConnection(server))
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Review SQL queries - the name is generated above from a GUID; no input reaches it.
            command.CommandText = $"CREATE DATABASE [{name}]";
#pragma warning restore CA2100
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        Interlocked.Increment(ref _databasesCreated);

        return new SqlConnectionStringBuilder(server) { InitialCatalog = name }.ConnectionString;
    }

    /// <summary>
    /// Drops a database this engine created. Best effort: the engine's disposal removes everything
    /// anyway, and a drop that fails must not replace a test's own failure.
    /// </summary>
    /// <param name="connectionString">The connection string <see cref="CreateDatabaseAsync"/> returned.</param>
    /// <returns>A task.</returns>
    /// <remarks>
    /// Cleanup is still by disposal, never by delete statements: the whole database goes, so no row
    /// needs a session context to be seen.
    /// </remarks>
    public async Task DropDatabaseAsync(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var name = builder.InitialCatalog;

        if (_container is null || !IsGeneratedName(name))
        {
            return;
        }

        try
        {
            using (var pooled = new SqlConnection(connectionString))
            {
                SqlConnection.ClearPool(pooled);
            }

            await using var connection = new SqlConnection(new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" }.ConnectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Review SQL queries - only a name this engine generated passes IsGeneratedName.
            command.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];";
#pragma warning restore CA2100
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException)
        {
            // Best effort, as documented above.
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
            _container = null;
        }

        _gate.Dispose();
    }

    private static bool IsGeneratedName(string name) =>
        name.Length == "yello_".Length + 32
        && name.StartsWith("yello_", StringComparison.Ordinal)
        && name["yello_".Length..].All(char.IsAsciiHexDigitLower);

    private async Task<SqlServerContainerFixture> StartedAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_failure is not null)
            {
                throw new InvalidOperationException("The shared SQL Server engine failed to start earlier in this run.", _failure);
            }

            if (_container is not null)
            {
                return _container;
            }

            var dead = await SqlServerContainerFixture.ProbeContainerRuntimeAsync(ProbeDeadline).ConfigureAwait(false);

            if (dead is not null)
            {
                _failure = new InvalidOperationException(
                    "A container runtime is configured but its daemon did not answer, so the shared SQL Server " +
                    "cannot start. This fails rather than skipping: an isolation suite that skips on a dead " +
                    $"runtime is a vacuous gate. {dead}");

                throw _failure;
            }

            var container = new SqlServerContainerFixture();

            try
            {
                await container.InitializeAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                _failure = exception;
                await container.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            _container = container;

            return container;
        }
        finally
        {
            _gate.Release();
        }
    }
}
