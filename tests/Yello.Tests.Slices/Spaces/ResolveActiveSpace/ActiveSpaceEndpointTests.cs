using System.Net;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Accounts;
using Yello.Contracts.Spaces;
using Yello.Domain.Accounts;
using Yello.Domain.Memberships;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Space resolution over the wire, against a Host that is actually running. AC1, AC2; story 1.4's
/// AC3 Membership clause.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every refusal is anchored on a success in the same test.</b> A request to a route that does not
/// exist answers 404, which is exactly AC1's refusal - so a refusal asserted alone would pass
/// against a Host with no Space-scoped route at all. Each case below first shows a member being
/// served, then the refusal.
/// </para>
/// <para>
/// <b>The Account is registered and signed in through the product; its Spaces are seeded by B2's
/// shape.</b> Registration writes a personal Space whose id this test cannot learn without a
/// bypass - <c>Membership</c> is behind the policy - so the Spaces asserted on are seeded with known
/// ids through <see cref="SpaceSeeding"/>, and the Account's id is read from <c>Account</c>, which
/// carries no policy.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-15")]
[Trait("Requirement", "AR-5")]
[Trait("Requirement", "AR-6")]
[Trait("Requirement", "AD-1")]
[Trait("Requirement", "AD-2")]
[Trait("Requirement", "AD-3")]
[Trait("Assumption", "PRD-12-8")]
public sealed class ActiveSpaceEndpointTests(MigratedDatabaseFixture fixture)
    : IClassFixture<MigratedDatabaseFixture>
{
    private const string Password = "a-password-nobody-else-uses-1!";
    private const string ClientOrigin = "https://client.yello.test";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, string> Configured = new(StringComparer.Ordinal)
    {
        ["ClientOrigin"] = ClientOrigin,
    };

    private static readonly Dictionary<string, string> AlreadyExpired = new(StringComparer.Ordinal)
    {
        ["ClientOrigin"] = ClientOrigin,
        ["Session__Lifetime"] = "-00:00:01",
    };

    /// <summary>
    /// AC1 - a member is served their Space; a Space they are not in, and a Space that does not
    /// exist, are both refused with the same 404.
    /// </summary>
    [Fact]
    public async Task A_member_is_served_and_a_non_member_is_refused_exactly_as_a_missing_Space_is()
    {
        var database = Available();
        var address = RandomAddress();
        var memberSpace = Guid.NewGuid();
        var otherSpace = Guid.NewGuid();

        WireResponse? served = null;
        WireResponse? notAMember = null;
        WireResponse? noSuchSpace = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            var accountId = await AccountIdOfAsync(database, address);

            await SeedSpaceAsync(database, memberSpace, "Alpha", accountId);
            await SeedSpaceAsync(database, otherSpace, "Beta", SpaceSeeding.NewAccount());

            served = await session.GetAsync(SpaceRoutes.StatusesOf(memberSpace));
            notAMember = await session.GetAsync(SpaceRoutes.StatusesOf(otherSpace));
            noSuchSpace = await session.GetAsync(SpaceRoutes.StatusesOf(Guid.NewGuid()));
        });

        Assert.NotNull(served);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal(["Alpha"], StatusNames(served));

        Assert.NotNull(notAMember);
        Assert.NotNull(noSuchSpace);

        Assert.Equal(HttpStatusCode.NotFound, notAMember.StatusCode);
        Assert.Equal(ProblemTypes.SpaceNotFound, ProblemType(notAMember));

        // Never distinguished (AD-3): status, body, content type and header names all identical, so
        // nothing in the answer says whether the Space exists.
        Assert.Equal(notAMember.StatusCode, noSuchSpace.StatusCode);
        Assert.Equal(notAMember.Body, noSuchSpace.Body);
        Assert.Equal(notAMember.ContentType, noSuchSpace.ContentType);
        Assert.Equal(notAMember.HeaderNames, noSuchSpace.HeaderNames);

        // And the refused Space's rows are nowhere in the answer.
        Assert.DoesNotContain("Beta", notAMember.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// A request with no Session is refused at the authentication boundary - 401, never the Space
    /// boundary's 404.
    /// </summary>
    /// <remarks>
    /// Against a Space the Account IS a member of, and after that Account has been served it, so the
    /// 401 cannot be a missing route or a refused Membership. AD-3 draws the 403/404 line at the
    /// Space boundary "and nowhere else"; a caller with no Session never reaches it.
    /// </remarks>
    [Fact]
    public async Task A_request_with_no_Session_is_refused_with_401_before_any_Space_is_resolved()
    {
        var database = Available();
        var address = RandomAddress();
        var spaceId = Guid.NewGuid();

        WireResponse? served = null;
        WireResponse? anonymous = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedSpaceAsync(database, spaceId, "Alpha", await AccountIdOfAsync(database, address));

            served = await session.GetAsync(SpaceRoutes.StatusesOf(spaceId));
            anonymous = await session.GetAnonymouslyAsync(SpaceRoutes.StatusesOf(spaceId));
        });

        Assert.NotNull(served);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);

        Assert.NotNull(anonymous);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(ProblemTypes.AuthenticationFailed, ProblemType(anonymous));
        Assert.DoesNotContain("Alpha", anonymous.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Story 1.4's AC3 Membership clause, provable for the first time: an expired Session grants
    /// nothing in a Space the Account is still a member of.
    /// </summary>
    /// <remarks>
    /// Story 1.4 recorded this clause as not proved, because "there is no Space-scoped surface for it
    /// to fail to grant access to until story 1.5". The Host is started with a negative lifetime, so
    /// the ticket is born expired; the Membership row is present and current.
    /// </remarks>
    [Fact]
    public async Task An_expired_Session_grants_nothing_in_a_Space_the_Account_is_still_a_member_of()
    {
        var database = Available();
        var address = RandomAddress();
        var spaceId = Guid.NewGuid();

        WireResponse? response = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, AlreadyExpired, async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedSpaceAsync(database, spaceId, "Alpha", await AccountIdOfAsync(database, address));

            response = await session.GetAsync(SpaceRoutes.StatusesOf(spaceId));
        });

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// AC2 - consecutive requests for different Spaces each return only their own Space's rows.
    /// </summary>
    /// <remarks>
    /// Alternated, and repeated, on one Host - so any carry-over of a Space from one request to the
    /// next through the pool, the DI container or the model cache shows up as the wrong Space's row.
    /// </remarks>
    [Fact]
    public async Task Consecutive_requests_for_different_Spaces_each_return_only_their_own_rows()
    {
        var database = Available();
        var address = RandomAddress();
        var spaceA = Guid.NewGuid();
        var spaceB = Guid.NewGuid();

        var answers = new List<(Guid Space, IReadOnlyList<string> Names)>();

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            var accountId = await AccountIdOfAsync(database, address);

            await SeedSpaceAsync(database, spaceA, "Alpha", accountId);
            await SeedSpaceAsync(database, spaceB, "Beta", accountId);

            foreach (var space in new[] { spaceA, spaceB, spaceA, spaceB })
            {
                var response = await session.GetAsync(SpaceRoutes.StatusesOf(space));

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                answers.Add((space, StatusNames(response)));
            }
        });

        Assert.Equal(4, answers.Count);

        Assert.All(answers, answer =>
            Assert.Equal(answer.Space == spaceA ? ["Alpha"] : ["Beta"], answer.Names));
    }

    private static async Task<WireSession> SignedInAsync(HttpClient client, string address)
    {
        var session = new WireSession(client);

        await session.FetchAntiforgeryTokenAsync();

        var registered = await session.RegisterAsync(
            new RegisterAccountRequest("Ravi-" + Guid.NewGuid().ToString("N"), address, Password));

        Assert.Equal(HttpStatusCode.NoContent, registered.StatusCode);

        var signedIn = await session.SignInAsync(address, Password);

        Assert.Equal(HttpStatusCode.NoContent, signedIn.StatusCode);

        return session;
    }

    /// <summary>
    /// The registered Account's id. <c>Account</c> carries no row-level security policy, so this read
    /// needs no session context and no bypass.
    /// </summary>
    private static async Task<Guid> AccountIdOfAsync(RegistrationDatabase database, string address)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Id FROM dbo.[Account] WHERE NormalizedEmailAddress = @address";
        command.Parameters.Add(new SqlParameter("@address", EmailAddressNormalisation.Normalise(address)));

        var id = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        return Assert.IsType<Guid>(id);
    }

    private static Task SeedSpaceAsync(RegistrationDatabase database, Guid spaceId, string statusName, Guid memberId) =>
        SpaceSeeding.SeedAsync(database, spaceId, context =>
        {
            context.Spaces.Add(SpaceSeeding.NewSpace(spaceId));
            context.StatusDefinitions.Add(SpaceSeeding.NewStatus(spaceId, statusName));
            context.Memberships.Add(SpaceSeeding.NewMembership(spaceId, memberId, Role.Member));
        }, TestContext.Current.CancellationToken);

    private static Task SeedSpaceAsync(RegistrationDatabase database, Guid spaceId, string statusName, Account newMember) =>
        SpaceSeeding.SeedAsync(database, spaceId, context =>
        {
            context.Accounts.Add(newMember);
            context.Spaces.Add(SpaceSeeding.NewSpace(spaceId));
            context.StatusDefinitions.Add(SpaceSeeding.NewStatus(spaceId, statusName));
            context.Memberships.Add(SpaceSeeding.NewMembership(spaceId, newMember.Id, Role.Owner));
        }, TestContext.Current.CancellationToken);

    private static List<string> StatusNames(WireResponse response) =>
        [.. (JsonSerializer.Deserialize<List<StatusResponse>>(response.Body, JsonOptions) ?? []).Select(status => status.Name)];

    private static string? ProblemType(WireResponse response) =>
        JsonSerializer.Deserialize<ProblemResponse>(response.Body, JsonOptions)?.Type;

    private static string RandomAddress() => $"space-{Guid.NewGuid():N}@yello.test";

    private RegistrationDatabase Available()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        return fixture.Database!;
    }
}
