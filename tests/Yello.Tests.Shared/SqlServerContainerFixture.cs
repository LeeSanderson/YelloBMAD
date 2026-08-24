using DotNet.Testcontainers.Builders;
using Testcontainers.MsSql;
using Xunit;

namespace Yello.Tests.Shared;

/// <summary>
/// The shared SQL Server container every suite runs against, and the mechanism by which a
/// later story asserts a migrated schema.
/// </summary>
/// <remarks>
/// <para>
/// This closes a test-design entry criterion that has no owning story: "All suites: shared
/// Testcontainers SQL Server fixture running <c>mssql/server:2025-latest</c>".
/// </para>
/// <para>
/// It is a real SQL Server, never an in-memory provider. An in-memory provider cannot
/// exercise row-level security, which is what NFR-1 rests on - so
/// <c>Microsoft.EntityFrameworkCore.InMemory</c> is not merely unreferenced but has no
/// central version at all, and Gate A asserts both halves of that ban.
/// </para>
/// <para>
/// <b>Story 1.1 writes no schema assertion</b>: there is no schema. Story 1.3 creates the
/// first three tables. What is seeded here is the mechanism story 2.6 needs for risk R7 -
/// AD-15's <c>Latin1_General_100_BIN2</c> collation is irreversible, because
/// <c>ALTER DATABASE ... COLLATE</c> is unsupported on Azure SQL, so it has to be asserted
/// against a migrated database rather than reviewed.
/// </para>
/// <para>
/// <b>Topology for later stories.</b> One container is amortised across collections. The
/// single exception is the pooled-connection isolation case in story 1.9, which needs its
/// own container with pool size pinned to 1 and parallelism disabled - a pooled connection
/// carrying a stale session context is the thing that case exists to catch, and it cannot
/// be observed on a shared pool.
/// </para>
/// <para>
/// <b>No <c>Task.Delay</c>.</b> Readiness is a wait strategy, never a sleep. The test
/// design calls this out as "cheaper to enforce as a convention from story 1.1 than to
/// unpick later", and it binds to every suite, not just this fixture.
/// </para>
/// </remarks>
/// <remarks>
/// Sealed deliberately. Fixtures are consumed through xunit's <c>IClassFixture</c> /
/// <c>ICollectionFixture</c>, never by inheritance, and sealing settles CA1816: with no
/// derived types there is no finalizer for <see cref="DisposeAsync"/> to suppress. Story
/// 1.9's single-connection variant will be its own fixture, not a subclass of this one.
/// </remarks>
public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    /// <summary>
    /// The SQL Server image, pinned to the tag AC4 names. The tag floats by CU rather than
    /// being pinned by digest, which is a stated open question for Lee rather than an
    /// oversight: it is the only container image in the stack, so builds are not
    /// reproducible across time until a digest is chosen.
    /// </summary>
    public const string Image = "mcr.microsoft.com/mssql/server:2025-latest";

    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage(Image)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(MsSqlBuilder.MsSqlPort))
        .Build();

    /// <summary>
    /// The ADO.NET connection string for the running container. Valid only after
    /// <see cref="InitializeAsync"/> has completed.
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>Starts the container and waits for the engine to accept connections.</summary>
    public ValueTask InitializeAsync() => new(_container.StartAsync());

    /// <summary>
    /// Disposes the container, which is also how test data is cleaned up.
    /// </summary>
    /// <remarks>
    /// Cleanup is by container disposal or transaction rollback - never by delete
    /// statements, which would themselves need an RLS session context to see the rows they
    /// are trying to remove.
    /// </remarks>
    public ValueTask DisposeAsync() => new(_container.DisposeAsync().AsTask());
}
