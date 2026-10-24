using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yello.Contracts.Spaces;
using Yello.Application.Spaces;
using Yello.Domain;
using Yello.Domain.Accounts;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;
using Yello.Infrastructure;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// The Space boundary's refusals, driven through the real pipeline filter against a real resolver
/// and a migrated database. AC2, AC3, AC4, AC10; I-1, I-2, I-6, I-9.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the ATDD run on 2026-10-24, activated by story 1.6.</b> One
/// container-backed class holds all of them, so the cost on a full run is one engine.
/// </para>
/// <para>
/// <b>Driven directly, not over the wire - story open question 4.</b> The product has one
/// Space-scoped endpoint and every Role may call it, so no request can produce a 403. The filter is
/// called as <c>ActiveSpaceUnitOfWorkTests</c> calls it, but with the production services behind it
/// and an endpoint whose metadata declares the capability under test. No product endpoint is added
/// to manufacture a 403.
/// </para>
/// <para>
/// <b>Seeded Memberships, on purpose</b> (<c>implementation-readiness-report-2026-08-22.md:964-968</c>):
/// story 4.7 re-exercises the matrix through real Invitation and Role-change paths.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-15")]
[Trait("Requirement", "FR-16")]
[Trait("Requirement", "AR-9")]
[Trait("Requirement", "AR-10")]
[Trait("Requirement", "AD-3")]
public sealed class SpaceBoundaryPipelineTests(MigratedDatabaseFixture fixture)
    : IClassFixture<MigratedDatabaseFixture>
{
    private const string InsufficientRole = "urn:yello:problem:insufficient-role";
    private const string NotAvailable = "urn:yello:problem:not-available";

    /// <summary>
    /// Every Role-decided capability, by the name story 1.6 Task 2 gives it.
    /// </summary>
    private static readonly string[] EveryCapability =
    [
        "ReadProjectsAndTasks", "ManageProjects", "ManageTasks", "EditTaskDescriptionCollaboratively",
        "AssignTask", "ConfigureSpaceDefaultStatuses", "ConfigureProjectStatusDelta", "ManageLabels",
        "IssueAndRevokeInvitations", "ChangeMembershipRole", "RemoveMembership", "RenameSpace",
        "OfferOwnershipTransfer", "DeleteSpace", "IssueApiTokenForOneself",
    ];

    /// <summary>
    /// AC2 / AC3 / I-9 - at each Role, the matrix decides: an allowed capability reaches the
    /// endpoint, and a denied one is answered 403 with capability-shaped copy before it does.
    /// </summary>
    /// <param name="role">The seeded Membership's Role.</param>
    /// <param name="capability">The capability the endpoint declares.</param>
    /// <param name="allowed">Whether <c>role-capability-matrix.md</c> lets that Role through.</param>
    /// <remarks>
    /// The Admin rows marked "Member and Viewer only" are allowed at the Role gate; the target
    /// constraint is stories 4.4 and 4.5's, so <c>ChangeMembershipRole</c> passes here for Admin.
    /// </remarks>
    [Theory]
    [InlineData(Role.Owner, "ReadProjectsAndTasks", true)]
    [InlineData(Role.Admin, "ReadProjectsAndTasks", true)]
    [InlineData(Role.Member, "ReadProjectsAndTasks", true)]
    [InlineData(Role.Viewer, "ReadProjectsAndTasks", true)]
    [InlineData(Role.Owner, "ManageTasks", true)]
    [InlineData(Role.Admin, "ManageTasks", true)]
    [InlineData(Role.Member, "ManageTasks", true)]
    [InlineData(Role.Viewer, "ManageTasks", false)]
    [InlineData(Role.Owner, "ConfigureSpaceDefaultStatuses", true)]
    [InlineData(Role.Admin, "ConfigureSpaceDefaultStatuses", true)]
    [InlineData(Role.Member, "ConfigureSpaceDefaultStatuses", false)]
    [InlineData(Role.Viewer, "ConfigureSpaceDefaultStatuses", false)]
    [InlineData(Role.Owner, "ChangeMembershipRole", true)]
    [InlineData(Role.Admin, "ChangeMembershipRole", true)]
    [InlineData(Role.Member, "ChangeMembershipRole", false)]
    [InlineData(Role.Viewer, "ChangeMembershipRole", false)]
    [InlineData(Role.Owner, "DeleteSpace", true)]
    [InlineData(Role.Admin, "DeleteSpace", false)]
    [InlineData(Role.Member, "DeleteSpace", false)]
    [InlineData(Role.Viewer, "DeleteSpace", false)]
    public async Task The_matrix_decides_whether_a_member_reaches_the_endpoint(Role role, string capability, bool allowed)
    {
        var database = Available();
        var member = SpaceSeeding.NewAccount();
        var spaceId = Guid.NewGuid();

        await SeedSpaceWithMemberAsync(database, spaceId, member, role);

        var endpointReached = false;
        var result = await ResolveAsync(member.Id, spaceId, capability, () => endpointReached = true);

        if (allowed)
        {
            Assert.True(endpointReached);
            Assert.Equal(StatusCodes.Status200OK, StatusOf(result));
            return;
        }

        Assert.False(endpointReached, "A denied capability reached the endpoint: the check must precede it (AR-3).");
        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(result));

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(InsufficientRole, problem.ProblemDetails.Type);

        // Capability-shaped, never object-shaped: a stable code the client renders from its own
        // resources, and nothing naming the Space.
        // The exact code of the capability that was denied - not merely some code - so a pipeline that
        // named the wrong capability would be caught.
        Assert.True(problem.ProblemDetails.Extensions.TryGetValue("capability", out var code));
        Assert.Equal(StableCode.Of(Enum.Parse<Capability>(capability)), code?.ToString());
        Assert.DoesNotContain(spaceId.ToString(), problem.ProblemDetails.Title ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(spaceId.ToString(), problem.ProblemDetails.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(spaceId.ToString(), problem.ProblemDetails.Extensions.Values.Select(value => value?.ToString()));
    }

    /// <summary>
    /// AC3 / I-1 - every capability is refused for an Account holding no Membership in the Space,
    /// with the boundary's one 404 and never a 403 that would disclose the Space exists.
    /// </summary>
    [Fact]
    public async Task Every_capability_is_refused_with_the_boundary_404_for_a_non_member()
    {
        var database = Available();
        var outsider = SpaceSeeding.NewAccount();
        var owner = SpaceSeeding.NewAccount();
        var outsidersOwn = Guid.NewGuid();
        var spaceId = Guid.NewGuid();

        await SeedSpaceWithMemberAsync(database, outsidersOwn, outsider, Role.Owner);
        await SeedSpaceWithMemberAsync(database, spaceId, owner, Role.Owner);

        // Anchored: the outsider IS served in a Space of their own, so a 404 below is the boundary
        // and not a broken harness.
        Assert.Equal(StatusCodes.Status200OK, StatusOf(await ResolveAsync(outsider.Id, outsidersOwn, "DeleteSpace", () => { })));

        foreach (var capability in EveryCapability)
        {
            var endpointReached = false;
            var result = await ResolveAsync(outsider.Id, spaceId, capability, () => endpointReached = true);

            Assert.False(endpointReached);
            Assert.Equal(StatusCodes.Status404NotFound, StatusOf(result));
            Assert.Equal(NotAvailable, Assert.IsType<ProblemHttpResult>(result).ProblemDetails.Type);
        }
    }

    /// <summary>
    /// AC4 - a 403 writes one <c>InsufficientRole</c> refusal carrying the Account, the target Space,
    /// the capability attempted and the outcome.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-11")]
    [Trait("Requirement", "AD-20")]
    [Trait("Requirement", "NFR-7")]
    public async Task A_403_records_one_InsufficientRole_refusal()
    {
        var database = Available();
        var viewer = SpaceSeeding.NewAccount();
        var spaceId = Guid.NewGuid();

        await SeedSpaceWithMemberAsync(database, spaceId, viewer, Role.Viewer);

        Assert.Equal(StatusCodes.Status200OK, StatusOf(await ResolveAsync(viewer.Id, spaceId, "ReadProjectsAndTasks", () => { })));
        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(await ResolveAsync(viewer.Id, spaceId, "ManageTasks", () => { })));

        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT TargetSpaceId, Capability, CAST(Outcome AS nvarchar(16)), Kind
            FROM dbo.[AccessRefusal] WHERE AccountId = @account
            """;
        command.Parameters.Add(new SqlParameter("@account", viewer.Id));

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken), "The 403 wrote no refusal record.");
        Assert.Equal(spaceId, reader.GetGuid(0));
        Assert.Equal("ManageTasks", reader.GetString(1));
        Assert.Equal("403", reader.GetString(2));
        Assert.Equal("InsufficientRole", reader.GetString(3));
        Assert.False(await reader.ReadAsync(TestContext.Current.CancellationToken), "The served read was recorded as a refusal.");
    }

    /// <summary>
    /// AC10 / I-6 - a Space that does not exist and a Space the caller holds no Membership in take
    /// indistinguishable time to refuse, by the B3 method.
    /// </summary>
    /// <remarks>
    /// Server-side, interleaved, 21 samples per arm, medians, 20% of the slower median -
    /// <see cref="DurationIndistinguishability"/>, and no second method. It means something only
    /// beside the oracle below, which proves the method can see the one piece of work a broken arm
    /// would skip.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-10")]
    public async Task A_missing_Space_and_a_Space_without_Membership_take_the_same_time_to_refuse()
    {
        var database = Available();
        var (caller, otherSpace) = await SeedCallerAndForeignSpaceAsync(database);
        var missingSpace = Guid.NewGuid();

        var verdict = await DurationIndistinguishability.CompareAsync(
            () => ResolveAsync(caller, otherSpace, "ReadProjectsAndTasks", () => { }).AsTask(),
            () => ResolveAsync(caller, missingSpace, "ReadProjectsAndTasks", () => { }).AsTask());

        Assert.True(verdict.AreIndistinguishable, verdict.Describe());
    }

    /// <summary>
    /// AC10's permanent planted oracle - one arm skips the <c>AccessRefusal</c> write, and the method
    /// must tell the two apart. Without this, the assertion above could be passing because the
    /// method cannot see a skipped record write at all.
    /// </summary>
    /// <remarks>
    /// <b>Activated first, before the design was built on it.</b> The B3 method was built for a
    /// ~270 ms missing hash, and a skipped single-row insert on its own connection is a much smaller
    /// effect. Measured on activation (2026-10-24, five runs): about 6.5 ms against 3.5 ms, a 44-48%
    /// gap against the 20% tolerance, while the real pair differed by 1-4%. The insert is about half
    /// of a boundary refusal's cost, which is what makes it visible.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-10")]
    public async Task The_duration_method_tells_a_refusal_that_skips_its_record_from_one_that_writes_it()
    {
        var database = Available();
        var (caller, otherSpace) = await SeedCallerAndForeignSpaceAsync(database);
        var missingSpace = Guid.NewGuid();

        var verdict = await DurationIndistinguishability.CompareAsync(
            () => ResolveAsync(caller, otherSpace, "ReadProjectsAndTasks", () => { }).AsTask(),
            () => ResolveAsync(caller, missingSpace, "ReadProjectsAndTasks", () => { }, skipRecord: true).AsTask());

        Assert.False(
            verdict.AreIndistinguishable,
            "The planted arm skips the refusal record and the method could not tell: " + verdict.Describe());
    }

    // ---------------------------------------------------------------------------------------------
    // Story 1.8 - a Token bound to one Space, met at the route of another. Written by the story 1.8
    // ATDD run on 2026-10-24. Driven through the production filter, as the cases above are, because
    // the mismatch branch and its duration are decided inside ActiveSpaceResolution and a planted
    // early return cannot be put into a separate Host process. No new engine: this class's.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Story 1.8 AC2 / AD-20 - a Token bound to Space A, met at Space B's route where its Account is
    /// also a member, is refused with the boundary 404 exactly as a non-member is, and recorded as one
    /// <c>CrossSpace</c> refusal against B, the route's Space. Anchored on the same Token served at A.
    /// </summary>
    /// <remarks>
    /// The establishment returns <c>EstablishedSpaceRequest(AccountId, SpaceId, BoundSpaceId)</c>, as
    /// the Token branch of <c>SpaceRequestEstablishment</c> does.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "AD-6")]
    [Trait("Requirement", "AD-20")]
    public async Task A_Token_bound_elsewhere_is_refused_with_the_boundary_404_and_recorded_against_the_route_Space()
    {
        var database = Available();
        var owner = SpaceSeeding.NewAccount();
        var bound = Guid.NewGuid();
        var other = Guid.NewGuid();

        await SeedSpaceWithMemberAsync(database, bound, owner, Role.Owner);
        await SeedSpaceWithMemberAsync(database, other, SpaceSeeding.NewAccount(), Role.Owner);

        // The Token's Account IS a member of B too: AC2's "including for Spaces that same Account
        // owns" - the refusal must come from the binding, not from a missing Membership.
        await AddMembershipAsync(database, other, owner.Id, Role.Admin);

        var servedReached = false;
        var served = await ResolveAsync(EstablishedWithBoundSpace(owner.Id, bound, bound), "ReadProjectsAndTasks", () => servedReached = true);

        Assert.True(servedReached);
        Assert.Equal(StatusCodes.Status200OK, StatusOf(served));

        var refusedReached = false;
        var refused = await ResolveAsync(EstablishedWithBoundSpace(owner.Id, other, bound), "ReadProjectsAndTasks", () => refusedReached = true);

        Assert.False(refusedReached, "A Token bound to another Space reached the endpoint.");
        Assert.Equal(StatusCodes.Status404NotFound, StatusOf(refused));
        Assert.Equal(NotAvailable, Assert.IsType<ProblemHttpResult>(refused).ProblemDetails.Type);

        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT TargetSpaceId, Kind FROM dbo.[AccessRefusal] WHERE AccountId = @account";
        command.Parameters.Add(new SqlParameter("@account", owner.Id));

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken), "The mismatch wrote no refusal record.");
        Assert.Equal(other, reader.GetGuid(0));
        Assert.Equal("CrossSpace", reader.GetString(1));
        Assert.False(await reader.ReadAsync(TestContext.Current.CancellationToken), "More than one refusal was recorded.");
    }

    /// <summary>
    /// Story 1.8 AC2 / AD-3 - the mismatch branch costs what a non-member resolution costs, by the B3
    /// method: a Token bound to A met at B, against an Account with no Membership in B.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "AR-10")]
    public async Task A_Token_mismatch_and_a_non_member_take_the_same_time_to_refuse()
    {
        var database = Available();
        var (caller, otherSpace) = await SeedCallerAndForeignSpaceAsync(database);
        var tokenHolder = SpaceSeeding.NewAccount();
        var bound = Guid.NewGuid();

        await SeedSpaceWithMemberAsync(database, bound, tokenHolder, Role.Owner);

        var verdict = await DurationIndistinguishability.CompareAsync(
            () => ResolveAsync(EstablishedWithBoundSpace(tokenHolder.Id, otherSpace, bound), "ReadProjectsAndTasks", () => { }).AsTask(),
            () => ResolveAsync(caller, otherSpace, "ReadProjectsAndTasks", () => { }).AsTask());

        Assert.True(verdict.AreIndistinguishable, verdict.Describe());
    }

    /// <summary>
    /// Story 1.8's permanent planted oracle - one arm's resolver answers "no Membership" without
    /// reading <c>Membership</c>, which is exactly what a mismatch branch that skipped the resolver
    /// would do, and the method must tell the two apart. Without this, the comparison above could pass
    /// because the method cannot see a skipped Membership read at all.
    /// </summary>
    /// <remarks>
    /// <b>Active today:</b> it needs nothing story 1.8 builds - both arms are non-member refusals of
    /// the same Space, one of them with the read planted out.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-10")]
    public async Task The_duration_method_tells_a_refusal_that_skips_the_Membership_read_from_one_that_makes_it()
    {
        var database = Available();
        var (caller, otherSpace) = await SeedCallerAndForeignSpaceAsync(database);

        var verdict = await DurationIndistinguishability.CompareAsync(
            () => ResolveAsync(caller, otherSpace, "ReadProjectsAndTasks", () => { }).AsTask(),
            () => ResolveAsync(new FixedEstablishment(caller, otherSpace), "ReadProjectsAndTasks", () => { }, skipResolver: true).AsTask());

        Assert.False(
            verdict.AreIndistinguishable,
            "The planted arm skips the Membership read and the method could not tell: " + verdict.Describe());
    }

    /// <summary>
    /// An establishment that reports a bound Space beside the route's, as the Token branch does.
    /// </summary>
    private static PreparedEstablishment EstablishedWithBoundSpace(Guid accountId, Guid spaceId, Guid boundSpaceId) =>
        new(new EstablishedSpaceRequest(accountId, spaceId, boundSpaceId));

    /// <summary>
    /// A second Membership for an existing Account, in a Space that already exists, under that
    /// Space's context (<c>AK_Membership_SpaceId_AccountId</c>: the Account holds none there yet;
    /// <c>UX_Membership_SpaceId_Owner</c>: not an Owner, the Space has one).
    /// </summary>
    private static Task AddMembershipAsync(RegistrationDatabase database, Guid spaceId, Guid accountId, Role role) =>
        SpaceSeeding.SeedAsync(
            database,
            spaceId,
            context => context.Memberships.Add(SpaceSeeding.NewMembership(spaceId, accountId, role)),
            TestContext.Current.CancellationToken);

    /// <summary>
    /// The caller, and a Space owned by someone else. The caller is given a Space of their own so
    /// their <c>Account</c> row is written in a B2 block; it is never requested here.
    /// </summary>
    private static async Task<(Guid Caller, Guid OtherSpace)> SeedCallerAndForeignSpaceAsync(RegistrationDatabase database)
    {
        var caller = SpaceSeeding.NewAccount();
        var owner = SpaceSeeding.NewAccount();
        var otherSpace = Guid.NewGuid();

        await SeedSpaceWithMemberAsync(database, Guid.NewGuid(), caller, Role.Owner);
        await SeedSpaceWithMemberAsync(database, otherSpace, owner, Role.Owner);

        return (caller.Id, otherSpace);
    }

    /// <summary>
    /// One B2 block: the <c>Account</c> before the <c>Membership</c> referencing it
    /// (<c>FK_Membership_Account_AccountId</c>), the <c>Space</c> before its <c>Membership</c> and
    /// <c>StatusDefinition</c> (<c>FK_Membership_Space_SpaceId</c>,
    /// <c>FK_StatusDefinition_Space_SpaceId</c>), all under the Space's own session context so the
    /// BLOCK predicates admit the rows.
    /// </summary>
    private static Task SeedSpaceWithMemberAsync(RegistrationDatabase database, Guid spaceId, Account member, Role role) =>
        SpaceSeeding.SeedAsync(database, spaceId, context =>
        {
            context.Accounts.Add(member);
            context.Spaces.Add(SpaceSeeding.NewSpace(spaceId));
            context.StatusDefinitions.Add(SpaceSeeding.NewStatus(spaceId, "Seeded"));
            context.Memberships.Add(SpaceSeeding.NewMembership(spaceId, member.Id, role));
        }, TestContext.Current.CancellationToken);

    /// <summary>
    /// One request through the production pipeline filter, with the production services behind it,
    /// to an endpoint declaring <paramref name="capability"/>.
    /// </summary>
    private ValueTask<object?> ResolveAsync(
        Guid accountId,
        Guid spaceId,
        string capability,
        Action onEndpoint,
        bool skipRecord = false) =>
        ResolveAsync(new FixedEstablishment(accountId, spaceId), capability, onEndpoint, skipRecord);

    /// <summary>
    /// The same, with the establishment supplied - story 1.8's bound-Space cases and the planted
    /// skipped Membership read.
    /// </summary>
    private async ValueTask<object?> ResolveAsync(
        ISpaceRequestEstablishment establishment,
        string capability,
        Action onEndpoint,
        bool skipRecord = false,
        bool skipResolver = false)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(TimeProvider.System)
            .AddYelloInfrastructure(fixture.ConnectionString)
            .AddSingleton(establishment);

        if (skipRecord)
        {
            services.AddScoped(_ => DispatchProxy.Create<IAccessRefusalRecorder, SkippingRecorder>());
        }

        if (skipResolver)
        {
            services.AddScoped(_ => DispatchProxy.Create<IActiveSpaceResolver, SkippingResolver>());
        }

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };

        httpContext.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse(SpaceRoutes.Statuses),
            order: 0,
            new EndpointMetadataCollection(RequiredCapability(capability)),
            displayName: "story 1.6 capability probe"));

        return await ActiveSpaceResolution.ResolveAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ =>
            {
                onEndpoint();
                return ValueTask.FromResult<object?>(TypedResults.Ok(new List<int> { 1 }));
            });
    }

    private static RequiredCapability RequiredCapability(string capability) =>
        new(Enum.Parse<Capability>(capability));

    private static int? StatusOf(object? result) => (result as IStatusCodeHttpResult)?.StatusCode;

    private RegistrationDatabase Available()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        return fixture.Database!;
    }

    private sealed class FixedEstablishment(Guid accountId, Guid spaceId) : ISpaceRequestEstablishment
    {
        public EstablishedSpaceRequest Establish(HttpContext httpContext) => new(accountId, spaceId);
    }

    private sealed class PreparedEstablishment(EstablishedSpaceRequest established) : ISpaceRequestEstablishment
    {
        public EstablishedSpaceRequest Establish(HttpContext httpContext) => established;
    }

    /// <summary>
    /// Story 1.8's planted defect for the duration oracle: a resolver that answers "no Membership"
    /// without reading anything - the shape of a mismatch branch that returns before the resolver.
    /// Public and unsealed because <see cref="DispatchProxy"/> derives from it at run time.
    /// </summary>
    public class SkippingResolver : DispatchProxy
    {
        /// <inheritdoc />
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Task.FromResult<IActiveSpaceUnitOfWork?>(null);
    }

    /// <summary>
    /// The planted defect for AC10's oracle: a refusal recorder that records nothing. Public and
    /// unsealed because <see cref="DispatchProxy"/> derives from it at run time.
    /// </summary>
    public class SkippingRecorder : DispatchProxy
    {
        /// <inheritdoc />
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.ReturnType switch
            {
                { } type when type == typeof(Task) => Task.CompletedTask,
                { } type when type == typeof(ValueTask) => ValueTask.CompletedTask,
                _ => null,
            };
    }
}
