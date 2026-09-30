using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Yello.Domain.Memberships;
using Yello.Infrastructure.Persistence;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// AC5's pooled-connection reuse case - two requests for different Spaces served consecutively on
/// one physical connection.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own class, and that is not tidiness.</b> <c>TESTING-CONVENTIONS.md:347-350</c>: "the
/// pooled-connection isolation case needs its own container, with pool size pinned to 1 and
/// parallelism disabled. A pooled connection carrying a stale session context is the thing that
/// case exists to catch, and it cannot be observed on a shared pool." A class fixture is one
/// container, so a separate class is how that is obtained without settling the sharing model.
/// </para>
/// <para>
/// <b>What this story owns and what story 1.9 owns.</b> The behaviour is story 1.5's acceptance
/// criterion, so it is asserted here against a locally pinned pool. The isolation suite's own
/// harness for it - test id <b>I-8</b>, its own collection with parallelism disabled, run on both
/// surfaces per AD-4 - is story 1.9's (<c>test-design-architecture.md:329-337</c>), and this is not
/// that case. <c>Yello.Tests.Isolation</c> cannot be populated until the API-token surface exists
/// in story 1.8, and a case built against the wrong topology is precisely the one that gets
/// quarantined for flakiness.
/// </para>
/// <para>
/// <b>Through the product's own resolver and reader</b>, not a hand-rolled session-context call: the
/// claim is that the product's per-unit-of-work path cannot leak across a reused connection, so the
/// product's path is what runs.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-5")]
[Trait("Requirement", "AR-6")]
[Trait("Requirement", "AD-2")]
[Trait("Requirement", "NFR-1")]
public sealed class PooledConnectionIsolationTests(MigratedDatabaseFixture fixture)
    : IClassFixture<MigratedDatabaseFixture>
{
    /// <summary>
    /// One connection in the pool, so the second open is guaranteed to be a reuse of the first
    /// physical connection rather than a fresh one.
    /// </summary>
    private const int PinnedPoolSize = 1;

    /// <summary>
    /// AC5 - the second Space's request sees only the second Space's rows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The leak this guards against is silent and successful: a connection returned to the pool
    /// still carrying Space A's session context, handed to a request for Space B, answering Space
    /// B's query with Space A's rows and no error anywhere. NFR-1 has no acceptable failure rate,
    /// so this is the case that most needs to be real rather than decorative.
    /// </para>
    /// <para>
    /// <b>Two Spaces with different ids, and the assertion is on identity rather than count.</b> A
    /// count of one would be satisfied by either Space's row. <b>And the reuse is asserted, not
    /// assumed</b>: both requests report the same server session id, so the test cannot pass on a
    /// pool that quietly handed out a fresh connection.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task A_reused_pooled_connection_returns_only_the_second_Spaces_rows()
    {
        var database = new RegistrationDatabase(PinnedPool());
        var account = SpaceSeeding.NewAccount();
        var spaceA = Guid.NewGuid();
        var spaceB = Guid.NewGuid();

        await SpaceSeeding.SeedAsync(database, spaceA, context =>
        {
            context.Accounts.Add(account);
            context.Spaces.Add(SpaceSeeding.NewSpace(spaceA));
            context.Memberships.Add(SpaceSeeding.NewMembership(spaceA, account.Id, Role.Member));
            context.StatusDefinitions.Add(SpaceSeeding.NewStatus(spaceA, "Alpha"));
        }, TestContext.Current.CancellationToken);

        await SpaceSeeding.SeedAsync(database, spaceB, context =>
        {
            context.Spaces.Add(SpaceSeeding.NewSpace(spaceB));
            context.Memberships.Add(SpaceSeeding.NewMembership(spaceB, account.Id, Role.Member));
            context.StatusDefinitions.Add(SpaceSeeding.NewStatus(spaceB, "Beta"));
        }, TestContext.Current.CancellationToken);

        var first = await ServeAsync(database, account.Id, spaceA);
        var second = await ServeAsync(database, account.Id, spaceB);

        Assert.Equal(first.Session, second.Session);

        Assert.Equal(["Alpha"], first.Statuses);
        Assert.Equal(["Beta"], second.Statuses);
    }

    /// <summary>
    /// One request: its own context and scope, a resolved unit of work, the product's reader.
    /// </summary>
    private static async Task<(short Session, IReadOnlyList<string> Statuses)> ServeAsync(
        RegistrationDatabase database,
        Guid accountId,
        Guid spaceId)
    {
        var scope = new SpaceScope();

        await using var context = database.CreateContext(scope);
        await using var unitOfWork = await new MembershipActiveSpaceResolver(context, scope)
            .BeginAsync(accountId, spaceId, TestContext.Current.CancellationToken);

        Assert.NotNull(unitOfWork);

        var session = await context.Database
            .SqlQueryRaw<short>("SELECT @@SPID AS [Value]")
            .SingleAsync(TestContext.Current.CancellationToken);

        var statuses = await new StatusDefinitionReader(context).ListAsync(TestContext.Current.CancellationToken);

        await unitOfWork.CompleteAsync(TestContext.Current.CancellationToken);

        return (session, [.. statuses.Select(status => status.Name)]);
    }

    private string PinnedPool()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        return new SqlConnectionStringBuilder(fixture.ConnectionString!)
        {
            Pooling = true,
            MaxPoolSize = PinnedPoolSize,
        }.ConnectionString;
    }
}
