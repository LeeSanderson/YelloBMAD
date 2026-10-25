using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Deployment;

/// <summary>
/// The migration bundle - the artefact that applies the schema in Azure - produces exactly the schema
/// the test fixtures get from <c>MigrateAsync</c>, row-level security and the filtered Owner index
/// included, and a second run applies nothing. Under <c>aspire run</c> the same migrations are applied
/// by <c>dotnet ef database update</c> (Q17 (a)), which is <c>MigrateAsync</c>'s path; this class is what
/// ties the two together.
/// Story 1.10 AC2, AC15 (AR-36); Task 3, Q3 (a), Finding F5.
/// </summary>
/// <remarks>
/// <para>
/// <b>The bundle is built outside the suite</b>, by CI or by hand, and named by an environment variable.
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
/// <b>The catalogue compared</b> is read from <c>sys.*</c>, not from EF's model: tables and columns
/// (type, length, precision, scale, nullability, identity, computed, collation),
/// <c>sys.security_policies</c> and <c>sys.security_predicates</c> (definition text, FILTER/BLOCK,
/// operation), every index with its <c>filter_definition</c>, foreign keys with their columns and
/// referential actions, default, check and key constraints (by definition, and by name unless the
/// engine named them), every module's definition (the predicate function), the <c>MAXDOP</c>
/// database-scoped configuration, and the history rows. Anchors: the comparison finds
/// <c>SpaceIsolationPolicy</c>, <c>UX_Membership_SpaceId_Owner</c>, a foreign key and a key constraint
/// before it asserts equality.
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

    /// <summary>
    /// The catalogue, one line per object. Every string column is cast to the database's collation:
    /// the catalogue views mix collations (<c>sys.database_scoped_configurations</c> is
    /// <c>Latin1_General_CI_AS_KS_WS</c>), and <c>CONCAT</c> refuses to combine them.
    /// </summary>
    private static readonly string[] CatalogueQueries =
    [
        """
        SELECT CONCAT('column|', s.name COLLATE DATABASE_DEFAULT, '.', t.name COLLATE DATABASE_DEFAULT, '.', c.name COLLATE DATABASE_DEFAULT, '|', ty.name COLLATE DATABASE_DEFAULT, '|', c.max_length, '|', c.precision, '|', c.scale, '|', c.is_nullable, '|', c.is_identity, '|', c.is_computed, '|', c.collation_name COLLATE DATABASE_DEFAULT)
        FROM sys.columns c
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        """,
        "SELECT CONCAT('policy|', name COLLATE DATABASE_DEFAULT, '|', is_enabled, '|', is_schema_bound) FROM sys.security_policies",
        """
        SELECT CONCAT('predicate|', OBJECT_NAME(p.object_id) COLLATE DATABASE_DEFAULT, '|', OBJECT_NAME(p.target_object_id) COLLATE DATABASE_DEFAULT, '|', p.predicate_type_desc COLLATE DATABASE_DEFAULT, '|', p.operation_desc COLLATE DATABASE_DEFAULT, '|', p.predicate_definition COLLATE DATABASE_DEFAULT)
        FROM sys.security_predicates p
        """,
        """
        SELECT CONCAT('index|', OBJECT_NAME(i.object_id) COLLATE DATABASE_DEFAULT, '|', i.name COLLATE DATABASE_DEFAULT, '|', i.is_unique, '|', i.has_filter, '|', i.filter_definition COLLATE DATABASE_DEFAULT)
        FROM sys.indexes i
        JOIN sys.tables t ON t.object_id = i.object_id
        WHERE i.name IS NOT NULL
        """,
        """
        SELECT CONCAT('fk|', fk.name COLLATE DATABASE_DEFAULT, '|', OBJECT_NAME(fk.parent_object_id) COLLATE DATABASE_DEFAULT, '.', pc.name COLLATE DATABASE_DEFAULT, '|', OBJECT_NAME(fk.referenced_object_id) COLLATE DATABASE_DEFAULT, '.', rc.name COLLATE DATABASE_DEFAULT, '|', fk.delete_referential_action_desc COLLATE DATABASE_DEFAULT, '|', fk.update_referential_action_desc COLLATE DATABASE_DEFAULT)
        FROM sys.foreign_keys fk
        JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
        JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
        JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
        """,
        """
        SELECT CONCAT('default|', OBJECT_NAME(d.parent_object_id) COLLATE DATABASE_DEFAULT, '.', c.name COLLATE DATABASE_DEFAULT, '|', d.definition COLLATE DATABASE_DEFAULT)
        FROM sys.default_constraints d
        JOIN sys.columns c ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id
        """,
        """
        SELECT CONCAT('check|', OBJECT_NAME(parent_object_id) COLLATE DATABASE_DEFAULT, '|', CASE WHEN is_system_named = 1 THEN '' ELSE name COLLATE DATABASE_DEFAULT END, '|', definition COLLATE DATABASE_DEFAULT)
        FROM sys.check_constraints
        """,
        """
        SELECT CONCAT('key|', OBJECT_NAME(parent_object_id) COLLATE DATABASE_DEFAULT, '|', type_desc COLLATE DATABASE_DEFAULT, '|', CASE WHEN is_system_named = 1 THEN '' ELSE name COLLATE DATABASE_DEFAULT END)
        FROM sys.key_constraints
        """,
        "SELECT CONCAT('module|', OBJECT_NAME(object_id) COLLATE DATABASE_DEFAULT, '|', definition COLLATE DATABASE_DEFAULT) FROM sys.sql_modules",
        "SELECT CONCAT('scoped|', name COLLATE DATABASE_DEFAULT, '|', CAST(value AS nvarchar(32)) COLLATE DATABASE_DEFAULT) FROM sys.database_scoped_configurations WHERE name = 'MAXDOP'",
        "SELECT CONCAT('history|', MigrationId COLLATE DATABASE_DEFAULT, '|', ProductVersion COLLATE DATABASE_DEFAULT) FROM dbo.__EFMigrationsHistory",
    ];

    /// <summary>
    /// 1.10-SLC-001 - the bundle and <c>MigrateAsync</c> produce the same catalogue.
    /// </summary>
    [Fact]
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
            Assert.Contains(fromMigrate, line => line.StartsWith("fk|", StringComparison.Ordinal));
            Assert.Contains(fromMigrate, line => line.StartsWith("key|", StringComparison.Ordinal));

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
    [Fact]
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

        try
        {
            await process.WaitForExitAsync(deadline.Token);
        }
        catch (OperationCanceledException)
        {
            // A bundle past its window is killed rather than orphaned, so its database can be dropped.
            process.Kill(entireProcessTree: true);
            throw;
        }

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
