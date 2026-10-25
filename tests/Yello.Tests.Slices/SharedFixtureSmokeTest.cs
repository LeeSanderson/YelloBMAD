using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices;

/// <summary>
/// Proves that the shared engine (<see cref="SqlServerEngine"/>, over
/// <see cref="SqlServerContainerFixture"/>) actually starts, and that the engine behind the pinned
/// image tag is the one the architecture was verified against.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this test exists.</b> Story 1.1 applied "an absence assertion must be validated
/// against a planted signal, or it is not a test" rigorously to every absence claim, and not
/// at all to the one presence claim it made. The fixture had no consumer, so
/// <c>InitializeAsync</c> had never run: the AR-1 pairing of Testcontainers 4.6.0 with
/// xunit.v3 4.0.0 was verified at compile time only, and the test design's entry criterion -
/// "a shared Testcontainers SQL Server fixture <i>running</i> mssql/server:2025-latest" - was
/// unproven. Testcontainers.MsSql locates <c>sqlcmd</c> inside the container at runtime,
/// against an image generation that has moved that path before, so compile-time agreement was
/// never the interesting half.
/// </para>
/// <para>
/// <b>Why it lives here.</b> <c>Yello.Tests.Slices</c> is the fifth test project and the only
/// one that is not release-gating, so a container start costs no release-gate latency. It is
/// also not one of the four suites AC5's zero-tests clause names - those are isolation,
/// revocation, merge conformance and architecture - so this does not breach AC5. It does mean
/// the suite has gained its first test, which is why <c>--ignore-exit-code 8</c> has come off
/// its project file.
/// </para>
/// <para>
/// <b>Why it skips rather than fails.</b> With no container runtime present the fixture cannot
/// start, and a suite that fails for that reason is one a developer learns to ignore. This
/// machine runs Rancher Desktop with a backend that is routinely stopped, so the skip is the
/// difference between a signal and noise. CI, where a runtime is always present, gets the real
/// assertion.
/// </para>
/// </remarks>
/// <param name="engine">The process's one SQL Server (story 1.9: no class starts its own).</param>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "AR-1")]
public sealed class SharedFixtureSmokeTest(SqlServerEngine engine)
{
    /// <summary>
    /// SQL Server 2025 reports major version 17. The image tag is <c>2025-latest</c>, so
    /// anything else means the tag no longer names the engine this architecture was verified
    /// against - which is a thing to be told about, not a thing to tolerate.
    /// </summary>
    private const string ExpectedEngineMajorVersion = "17.";

    [Fact]
    public async Task The_shared_fixture_starts_the_pinned_SQL_Server_engine()
    {
        // An unconfigured runtime skips locally (a stopped Rancher Desktop backend is a local condition,
        // not a defect) and throws on a CI agent (YELLO_REQUIRE_CONTAINER_RUNTIME=1, story 1.10 AC10).
        var unavailable = SqlServerEngine.UnavailableReason;
        Assert.SkipUnless(unavailable is null, unavailable ?? string.Empty);

        await using var connection = new SqlConnection(
            await engine.ServerConnectionStringAsync(TestContext.Current.CancellationToken));
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        Assert.StartsWith(ExpectedEngineMajorVersion, connection.ServerVersion, StringComparison.Ordinal);
    }

    /// <summary>
    /// The precondition on <see cref="SqlServerContainerFixture.ConnectionString"/> used to be
    /// stated only in a comment, which meant reading it early produced whatever Testcontainers
    /// happened to return rather than a sentence naming the mistake.
    /// </summary>
    /// <remarks>
    /// Costs no container: this is the one thing about the fixture that can be asserted
    /// without starting anything, so it runs everywhere including where the runtime is absent.
    /// It constructs the fixture and never starts it, which is why this class is on
    /// <c>ContainerTopologyGateTests</c>' list of constructions that start no engine.
    /// </remarks>
    [Fact]
    public void The_connection_string_refuses_to_answer_before_the_container_is_started()
    {
        var fixture = new SqlServerContainerFixture();

        var exception = Assert.Throws<InvalidOperationException>(() => fixture.ConnectionString);

        Assert.Contains("not running", exception.Message, StringComparison.Ordinal);
    }
}
