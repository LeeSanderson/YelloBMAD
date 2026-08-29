using Xunit;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Accounts.RegisterAccount;

/// <summary>
/// One migrated SQL Server for one test class.
/// </summary>
/// <remarks>
/// <para>
/// <b>This does not settle the container topology, and must not be read as doing so.</b>
/// <c>deferred-work.md:8</c> defers that decision to <b>story 1.9</b>, which owns the
/// single-connection pooled variant and is the first story that has to make the sharing model
/// real. What this is, is xunit's ordinary <c>IClassFixture</c> lifetime - which
/// <c>SqlServerContainerFixture</c>'s own remarks already anticipate ("one instance per
/// collection, or per class under IClassFixture"). Nothing here reuses a container across
/// classes, across suites or across processes, and nothing assumes it could.
/// </para>
/// <para>
/// <b>Why not construct the fixture per test, as story 1.1's smoke test does.</b> That pattern is
/// right for one or two cases and unusable for a class of them: each SQL Server is roughly 2 GB
/// and takes tens of seconds to become ready, so eight tests would mean eight sequential engine
/// starts. The per-test pattern also exists to allow a clean skip when no container runtime is
/// present, and that is the property this class has to preserve - which is the whole reason it is
/// a wrapper rather than a direct <c>IClassFixture&lt;SqlServerContainerFixture&gt;</c>.
/// </para>
/// <para>
/// <b>An unavailable runtime is recorded, not thrown.</b> xunit builds a class fixture before any
/// test in the class runs, so letting <c>InitializeAsync</c> throw would turn "Rancher Desktop is
/// stopped" into eight red tests with a container stack trace. Recording the reason lets each
/// test skip with it instead - a stopped local backend is a condition, not a defect.
/// </para>
/// </remarks>
public sealed class MigratedDatabaseFixture : IAsyncLifetime
{
    private SqlServerContainerFixture? _container;

    /// <summary>
    /// The migrated database, or <c>null</c> when no container runtime was reachable.
    /// </summary>
    internal RegistrationDatabase? Database { get; private set; }

    /// <summary>
    /// The migrated container's connection string, for a test that needs to hand it to a separate
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
        if (!SqlServerContainerFixture.IsContainerRuntimeAvailable())
        {
            UnavailableReason =
                "No container runtime is reachable, so there is no SQL Server to migrate. This " +
                "is a skip rather than a failure because a stopped Rancher Desktop backend is a " +
                "local condition, not a defect in the solution.";

            return;
        }

        _container = new SqlServerContainerFixture();
        await _container.InitializeAsync();

        var database = new RegistrationDatabase(_container.ConnectionString);

        // The schema comes from the migrations, so every assertion in this class is an assertion
        // about what the migration produces rather than about what the model would have produced.
        // Those are different things, and the row-level security policy exists only in the first.
        await database.MigrateAsync(TestContext.Current.CancellationToken);

        Database = database;
        ConnectionString = _container.ConnectionString;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            // Cleanup is container disposal. Never delete statements - those would need a session
            // context to see the rows they are removing, so a cleanup that "works" may be
            // evidence that isolation is broken.
            await _container.DisposeAsync();
            _container = null;
        }

        Database = null;
        ConnectionString = null;
    }
}
