using Xunit;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Accounts.RegisterAccount;

/// <summary>
/// One migrated database for one test class, on the process's one shared SQL Server.
/// </summary>
/// <remarks>
/// <para>
/// <b>A database per class, not an engine per class</b> (story 1.9 Q1 (a)). The engine is the
/// suite's <see cref="SqlServerEngine"/> assembly fixture; this fixture creates a database of its
/// own on it (<c>yello_{guid:N}</c>), runs the migrations into it, and drops it at the end. Classes
/// therefore share the engine and nothing else: every connection string handed out names this
/// class's database as its <c>Initial Catalog</c>, which
/// <c>SharedEngineDatabaseIsolationTests</c> proves rather than assumes.
/// </para>
/// <para>
/// <b>An unconfigured runtime is recorded, not thrown.</b> xunit builds a class fixture before any
/// test in the class runs, so letting <c>InitializeAsync</c> throw would turn "no container runtime
/// on this machine" into a red class. Recording the reason lets each test skip with it instead. A
/// configured runtime whose daemon is dead is different and fails: see
/// <see cref="SqlServerEngine"/>.
/// </para>
/// </remarks>
/// <param name="engine">The process's one SQL Server.</param>
public sealed class MigratedDatabaseFixture(SqlServerEngine engine) : IAsyncLifetime
{
    /// <summary>
    /// The migrated database, or <c>null</c> when no container runtime is configured.
    /// </summary>
    internal RegistrationDatabase? Database { get; private set; }

    /// <summary>
    /// This class's database's connection string, for a test that needs to hand it to a separate
    /// process rather than open it here.
    /// </summary>
    /// <remarks>
    /// The endpoint tests boot <c>Yello.Host</c> and let it connect for itself, which is the point
    /// of them: the wiring under test is the Host's own composition root.
    /// </remarks>
    public string? ConnectionString { get; private set; }

    /// <summary>
    /// Why there is no database, or <c>null</c> when there is one.
    /// </summary>
    public string? UnavailableReason { get; private set; }

    /// <summary>
    /// Whether a test in this class can actually run.
    /// </summary>
    public bool IsAvailable => Database is not null;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        UnavailableReason = SqlServerEngine.UnavailableReason;

        if (UnavailableReason is not null)
        {
            return;
        }

        var connectionString = await engine.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        var database = new RegistrationDatabase(connectionString);

        // Held before migrating, so a failed migration still drops the database on disposal.
        // IsAvailable reads Database, which is set only once the schema is there.
        ConnectionString = connectionString;

        // The schema comes from the migrations, so every assertion in this class is an assertion
        // about what the migration produces rather than about what the model would have produced.
        // Those are different things, and the row-level security policy exists only in the first.
        await database.MigrateAsync(TestContext.Current.CancellationToken);

        Database = database;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (ConnectionString is not null)
        {
            // Cleanup is disposal of the whole database. Never delete statements - those would need
            // a session context to see the rows they are removing, so a cleanup that "works" may be
            // evidence that isolation is broken.
            await engine.DropDatabaseAsync(ConnectionString);
        }

        Database = null;
        ConnectionString = null;
    }
}
