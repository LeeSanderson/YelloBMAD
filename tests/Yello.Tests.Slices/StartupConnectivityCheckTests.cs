using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Yello.Host;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices;

/// <summary>
/// AC4's automated coverage: the startup connectivity check opens a real connection to a real
/// SQL Server in Development, and does not run outside it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> Two review passes found the same gap. Nothing among the architecture
/// suite's assertions touched <c>Yello.Host</c>'s startup path, so deleting the check, inverting
/// its <c>IsDevelopment()</c> guard or dropping the <c>await</c> was caught by nothing - AC4 was
/// evidenced by a single manual <c>dotnet aspire run</c> transcribed into the story record. The
/// second pass then found that the transcript had gone stale: both <c>Yello.Host/Program.cs</c>
/// and <c>Yello.AppHost/Program.cs</c> were materially rewritten while the recorded evidence
/// stayed put. A transcript cannot notice that; a test can.
/// </para>
/// <para>
/// <b>Why a test rather than a source-level gate.</b> A gate asserting that
/// <c>Program.cs</c> contains the call would be cheap and would run in seconds with the rest of
/// Gate A - but it is a text assertion, and the failure mode most worth catching is an
/// <i>inverted</i> guard, which reads identically. Hence the second test below, which is the one
/// a source scan could not express.
/// </para>
/// <para>
/// <b>Why here.</b> <c>Yello.Tests.Slices</c> is the fifth suite and the only one that is not
/// release-gating, it already starts a container for <see cref="SharedFixtureSmokeTest"/>, and
/// it already references <c>Yello.Host</c> under the dependency rule. It is also not among the
/// suites AC5's zero-tests clause names.
/// </para>
/// <para>
/// The two container-backed cases skip when no runtime is reachable, on the same condition and
/// for the same reason as the fixture smoke test. The third case needs no container and so runs
/// everywhere, including where the runtime is absent.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "AR-1")]
public sealed class StartupConnectivityCheckTests
{
    /// <summary>
    /// EventId 1001, <c>ConnectivityConfirmed</c> - the check ran and opened a connection.
    /// </summary>
    private const int ConnectivityConfirmed = 1001;

    /// <summary>
    /// EventId 1005, <c>ConnectivityCheckSkipped</c> - the check deliberately did not run.
    /// </summary>
    private const int ConnectivityCheckSkipped = 1005;

    /// <summary>
    /// EventId 1000, <c>ConnectionStringMissing</c> - nothing injected the connection string.
    /// </summary>
    private const int ConnectionStringMissing = 1000;

    [Fact]
    public async Task The_check_opens_a_real_connection_in_Development()
    {
        Assert.SkipUnless(
            SqlServerContainerFixture.IsContainerRuntimeAvailable(),
            "No container runtime is reachable, so there is no engine to connect to.");

        await using var fixture = new SqlServerContainerFixture();
        await fixture.InitializeAsync();

        var logger = new CapturingLogger();

        await StartupConnectivityCheck.RunAsync(
            new StubEnvironment("Development"),
            ConfigurationWithConnectionString(fixture.ConnectionString),
            logger,
            TestContext.Current.CancellationToken);

        // The positive assertion is the whole point: AC4 says "a working connection from Host to
        // container", so the evidence has to be that a connection was actually opened, not that
        // the code path was entered.
        Assert.Contains(ConnectivityConfirmed, logger.EventIds);
        Assert.DoesNotContain(ConnectivityCheckSkipped, logger.EventIds);
    }

    /// <summary>
    /// The inverted-guard case. This is the assertion a source-level gate cannot make.
    /// </summary>
    /// <remarks>
    /// AR-33 forbids the application touching the database to answer a probe and forbids any
    /// component doing so on an unconditional timer; AR-36 forbids migrating at startup. The
    /// Development-only restriction is how this check stays inside all three. If the guard were
    /// inverted, every deployed environment would open a database connection during startup and
    /// this test - not a reviewer - is what says so.
    /// </remarks>
    [Fact]
    public async Task The_check_does_not_run_outside_Development_even_when_a_database_is_reachable()
    {
        Assert.SkipUnless(
            SqlServerContainerFixture.IsContainerRuntimeAvailable(),
            "No container runtime is reachable. This case needs a database that WOULD answer, so " +
            "that a passing result means the guard held rather than that nothing was listening.");

        await using var fixture = new SqlServerContainerFixture();
        await fixture.InitializeAsync();

        var logger = new CapturingLogger();

        await StartupConnectivityCheck.RunAsync(
            new StubEnvironment("Production"),
            ConfigurationWithConnectionString(fixture.ConnectionString),
            logger,
            TestContext.Current.CancellationToken);

        Assert.Contains(ConnectivityCheckSkipped, logger.EventIds);
        Assert.DoesNotContain(ConnectivityConfirmed, logger.EventIds);
    }

    /// <summary>
    /// Costs no container: the check reports a missing connection string and returns, which is
    /// the outcome when the Host is started outside <c>Yello.AppHost</c>.
    /// </summary>
    [Fact]
    public async Task The_check_reports_a_missing_connection_string_rather_than_failing()
    {
        var logger = new CapturingLogger();

        await StartupConnectivityCheck.RunAsync(
            new StubEnvironment("Development"),
            new ConfigurationBuilder().Build(),
            logger,
            TestContext.Current.CancellationToken);

        Assert.Contains(ConnectionStringMissing, logger.EventIds);
    }

    /// <summary>
    /// Configuration carrying the Aspire-injected connection string under the resource name the
    /// build stamped into <c>Yello.Host</c>.
    /// </summary>
    /// <remarks>
    /// The resource name is read from the Host assembly's metadata rather than written here.
    /// Writing it would create the second source of truth that
    /// <c>TestingConventionTests.Values_shared_between_projects_are_stated_once_in_the_build</c>
    /// exists to prevent - and would make this test pass while the Host looked for a different
    /// key, which is the exact defect the shared-value work closed.
    /// </remarks>
    private static IConfiguration ConfigurationWithConnectionString(string connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DatabaseResourceName()}"] = connectionString,
            })
            .Build();

    private static string DatabaseResourceName() =>
        typeof(AssemblyMarker).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key.Equals("Yello.DatabaseResourceName", StringComparison.Ordinal))
            .Value!;

    /// <summary>
    /// An <see cref="IHostEnvironment"/> whose only interesting property is its name.
    /// </summary>
    private sealed class StubEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Yello.Host";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }

    /// <summary>
    /// Records the event ids written to it. The check's only output is its log, so the log is
    /// what has to be asserted.
    /// </summary>
    private sealed class CapturingLogger : ILogger
    {
        private readonly List<int> _eventIds = [];

        public IReadOnlyList<int> EventIds => _eventIds;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        // Every level enabled, so a level the shipped configuration happens to filter cannot
        // make this test agree with a check that emitted nothing. That was a real defect: the
        // skipped-check line was written at Debug while both appsettings files set Information.
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => _eventIds.Add(eventId.Id);
    }
}
