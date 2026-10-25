using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Deployment;

/// <summary>
/// The migration bundle - the one artefact that applies the schema in Azure and under
/// <c>aspire run</c> - produces exactly the schema the test fixtures get from <c>MigrateAsync</c>,
/// row-level security and the filtered Owner index included, and a second run applies nothing.
/// Story 1.10 AC2, AC15 (AR-36); Task 3, Q3 (a), Finding F5.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the ATDD run on 2026-10-25.</b> No bundle is built yet.
/// </para>
/// <para>
/// <b>Where the bundle comes from.</b> <c>YELLO_MIGRATION_BUNDLE</c> names the executable
/// (<c>dotnet ef migrations bundle --project Yello.Infrastructure --startup-project Yello.Infrastructure
/// --self-contained -r &lt;rid&gt; -o artifacts/efbundle</c>). Unset, the class skips with that reason,
/// because a local run cannot assume the bundle was built. In CI the step that builds the bundle sets
/// it, and <c>YELLO_REQUIRE_MIGRATION_BUNDLE=1</c> turns the skip into a failure, so the test cannot
/// pass by skipping there.
/// </para>
/// <para>
/// <b>On the shared engine</b>: two databases of this class's own (<see cref="SqlServerEngine.CreateDatabaseAsync"/>),
/// never an engine (<c>ContainerTopologyGateTests</c>). Each is dropped at the end.
/// </para>
/// <para>
/// <b>The catalogue compared</b> is read from <c>sys.*</c>, not from EF's model: tables and columns,
/// <c>sys.security_policies</c> and <c>sys.security_predicates</c> (definition text, FILTER/BLOCK,
/// operation), every index with its <c>filter_definition</c>, every module's definition (the predicate
/// function), the <c>MAXDOP</c> database-scoped configuration, and the history rows. Anchors: the
/// comparison finds <c>SpaceIsolationPolicy</c> and <c>UX_Membership_SpaceId_Owner</c> before it
/// asserts equality.
/// </para>
/// </remarks>
/// <param name="engine">The process's one SQL Server.</param>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-36")]
[Trait("Requirement", "AD-2")]
public sealed class MigrationBundleEquivalenceTests(SqlServerEngine engine)
{
    private const string BundleVariable = "YELLO_MIGRATION_BUNDLE";

    private const string RequireBundleVariable = "YELLO_REQUIRE_MIGRATION_BUNDLE";

    private static readonly TimeSpan BundleWindow = TimeSpan.FromMinutes(5);

    private static readonly string[] CatalogueQueries =
    [
        """
        SELECT CONCAT('column|', s.name, '.', t.name, '.', c.name, '|', ty.name, '|', c.max_length, '|', c.is_nullable, '|', c.collation_name)
        FROM sys.columns c
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        """,
        "SELECT CONCAT('policy|', name, '|', is_enabled, '|', is_schema_bound) FROM sys.security_policies",
        """
        SELECT CONCAT('predicate|', OBJECT_NAME(p.object_id), '|', OBJECT_NAME(p.target_object_id), '|', p.predicate_type_desc, '|', p.operation_desc, '|', p.predicate_definition)
        FROM sys.security_predicates p
        """,
        """
        SELECT CONCAT('index|', OBJECT_NAME(i.object_id), '|', i.name, '|', i.is_unique, '|', i.has_filter, '|', i.filter_definition)
        FROM sys.indexes i
        JOIN sys.tables t ON t.object_id = i.object_id
        WHERE i.name IS NOT NULL
        """,
        "SELECT CONCAT('module|', OBJECT_NAME(object_id), '|', definition) FROM sys.sql_modules",
        "SELECT CONCAT('scoped|', name, '|', CAST(value AS nvarchar(32))) FROM sys.database_scoped_configurations WHERE name = 'MAXDOP'",
        "SELECT CONCAT('history|', MigrationId, '|', ProductVersion) FROM dbo.__EFMigrationsHistory",
    ];

    /// <summary>
    /// 1.10-SLC-001 - the bundle and <c>MigrateAsync</c> produce the same catalogue.
    /// </summary>
    [Fact(Skip = "red: AC2/AC15 - no migration bundle is built yet (Task 3, Q3 (a))")]
    public async Task The_bundle_applies_exactly_the_schema_the_migrations_do()
    {
        var bundle = BundleOrSkip();
        var cancellationToken = TestContext.Current.CancellationToken;

        var viaBundle = await engine.CreateDatabaseAsync(cancellationToken);
        var viaMigrate = await engine.CreateDatabaseAsync(cancellationToken);

        try
        {
            var run = await RunBundleAsync(bundle, viaBundle, cancellationToken);
            Assert.True(run.ExitCode == 0, $"The bundle exited {run.ExitCode}:\n{run.Output}");

            await new RegistrationDatabase(viaMigrate).MigrateAsync(cancellationToken);

            var fromBundle = await CatalogueAsync(viaBundle, cancellationToken);
            var fromMigrate = await CatalogueAsync(viaMigrate, cancellationToken);

            Assert.Contains(fromMigrate, line => line.StartsWith("policy|SpaceIsolationPolicy|1", StringComparison.Ordinal));
            Assert.Contains(fromMigrate, line => line.Contains("|UX_Membership_SpaceId_Owner|1|1|", StringComparison.Ordinal));
            Assert.Contains("scoped|MAXDOP|1", fromMigrate);

            Assert.Equal(fromMigrate, fromBundle);
        }
        finally
        {
            await engine.DropDatabaseAsync(viaBundle);
            await engine.DropDatabaseAsync(viaMigrate);
        }
    }

    /// <summary>
    /// 1.10-SLC-002 - a second run of the bundle applies nothing and exits 0, so the deploy job can run
    /// on every push.
    /// </summary>
    [Fact(Skip = "red: AC15 - no migration bundle is built yet (Task 3)")]
    public async Task A_second_bundle_run_applies_nothing_and_succeeds()
    {
        var bundle = BundleOrSkip();
        var cancellationToken = TestContext.Current.CancellationToken;

        var database = await engine.CreateDatabaseAsync(cancellationToken);

        try
        {
            var first = await RunBundleAsync(bundle, database, cancellationToken);
            Assert.True(first.ExitCode == 0, $"The first run exited {first.ExitCode}:\n{first.Output}");

            var before = await CatalogueAsync(database, cancellationToken);

            var second = await RunBundleAsync(bundle, database, cancellationToken);
            Assert.True(second.ExitCode == 0, $"The second run exited {second.ExitCode}:\n{second.Output}");

            Assert.Contains(before, line => line.StartsWith("history|", StringComparison.Ordinal));
            Assert.Equal(before, await CatalogueAsync(database, cancellationToken));
        }
        finally
        {
            await engine.DropDatabaseAsync(database);
        }
    }

    /// <summary>
    /// The bundle's path, or a skip with the reason - a failure instead when CI requires the bundle.
    /// </summary>
    private static FileInfo BundleOrSkip()
    {
        Assert.SkipWhen(SqlServerEngine.UnavailableReason is not null, SqlServerEngine.UnavailableReason ?? string.Empty);

        var configured = Environment.GetEnvironmentVariable(BundleVariable);
        var required = Environment.GetEnvironmentVariable(RequireBundleVariable) == "1";

        if (string.IsNullOrWhiteSpace(configured))
        {
            Assert.False(required, $"{RequireBundleVariable}=1 but {BundleVariable} is not set, so the bundle cannot be proved (AC15).");
            Assert.Skip($"{BundleVariable} is not set: build the migration bundle first (story 1.10 Task 3).");
        }

        var bundle = new FileInfo(configured);

        Assert.True(bundle.Exists, $"{BundleVariable} names '{configured}', which does not exist.");

        return bundle;
    }

    private static async Task<(int ExitCode, string Output)> RunBundleAsync(FileInfo bundle, string connectionString, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(bundle.FullName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add("--connection");
        startInfo.ArgumentList.Add(connectionString);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start '{bundle.FullName}'.");

        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(BundleWindow);

        await process.WaitForExitAsync(deadline.Token);

        return (process.ExitCode, await output + await error);
    }

    private static async Task<List<string>> CatalogueAsync(string connectionString, CancellationToken cancellationToken)
    {
        var lines = new List<string>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var query in CatalogueQueries)
        {
            await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Review SQL queries - constant catalogue queries declared in this class.
            command.CommandText = query;
#pragma warning restore CA2100

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                lines.Add(reader.GetString(0));
            }
        }

        lines.Sort(StringComparer.Ordinal);

        return lines;
    }
}
