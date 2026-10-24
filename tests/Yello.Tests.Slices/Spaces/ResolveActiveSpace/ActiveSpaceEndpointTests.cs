using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
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
public sealed partial class ActiveSpaceEndpointTests(MigratedDatabaseFixture fixture)
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
        Assert.Equal(ProblemTypes.NotAvailable, ProblemType(notAMember));

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
    /// to fail to grant access to until story 1.5". The same Account is first served its Space by a
    /// Host issuing ordinary Sessions, so the Membership demonstrably resolves; a second Host is then
    /// started with a negative lifetime, so the ticket it issues is born expired, against the same
    /// Membership row, still present and current.
    /// </remarks>
    [Fact]
    public async Task An_expired_Session_grants_nothing_in_a_Space_the_Account_is_still_a_member_of()
    {
        var database = Available();
        var address = RandomAddress();
        var spaceId = Guid.NewGuid();

        WireResponse? served = null;
        WireResponse? expired = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedSpaceAsync(database, spaceId, "Alpha", await AccountIdOfAsync(database, address));

            served = await session.GetAsync(SpaceRoutes.StatusesOf(spaceId));
        });

        await HostProcess.RunAsync(fixture.ConnectionString!, AlreadyExpired, async client =>
        {
            var session = await SignedInWithoutRegisteringAsync(client, address);

            expired = await session.GetAsync(SpaceRoutes.StatusesOf(spaceId));
        });

        Assert.NotNull(served);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal(["Alpha"], StatusNames(served));

        Assert.NotNull(expired);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        Assert.Equal(ProblemTypes.AuthenticationFailed, ProblemType(expired));
        Assert.DoesNotContain("Alpha", expired.Body, StringComparison.Ordinal);
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

    // ---------------------------------------------------------------------------------------------
    // Story 1.6 - the refusal contract, the refusal record, the presence bound and retention, over
    // the wire. Written as red-phase scaffolds by the ATDD run on 2026-10-24 and activated by the
    // story. They live in this class rather than a new one because a container-backed class is one
    // more SQL Server engine on a full run, and the cost is per class, not per case.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// AC1 / AC6 / I-5 - both boundary 404s answer RFC 9457 <c>not-available</c>, byte-identical,
    /// carrying no Space name, no Status and no identifier the URL did not already hold.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "AR-10")]
    [Trait("Requirement", "AR-34")]
    public async Task Both_boundary_404s_answer_not_available_and_carry_nothing_beyond_the_URL()
    {
        var database = Available();
        var address = RandomAddress();
        var memberSpace = Guid.NewGuid();
        var otherSpace = Guid.NewGuid();
        var missingSpace = Guid.NewGuid();

        WireResponse? served = null;
        WireResponse? notAMember = null;
        WireResponse? noSuchSpace = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);

            await SeedSpaceAsync(database, memberSpace, "Alpha", await AccountIdOfAsync(database, address));
            await SeedSpaceAsync(database, otherSpace, "Beta", SpaceSeeding.NewAccount());

            served = await session.GetAsync(SpaceRoutes.StatusesOf(memberSpace));
            notAMember = await session.GetAsync(SpaceRoutes.StatusesOf(otherSpace));
            noSuchSpace = await session.GetAsync(SpaceRoutes.StatusesOf(missingSpace));
        });

        Assert.NotNull(served);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);

        Assert.NotNull(notAMember);
        Assert.NotNull(noSuchSpace);

        Assert.Equal(HttpStatusCode.NotFound, notAMember.StatusCode);
        Assert.Equal("urn:yello:problem:not-available", ProblemType(notAMember));
        Assert.StartsWith("application/problem+json", notAMember.ContentType, StringComparison.Ordinal);

        Assert.Equal(notAMember.Body, noSuchSpace.Body);
        Assert.Equal(notAMember.HeaderNames, noSuchSpace.HeaderNames);

        Assert.DoesNotContain("Beta", notAMember.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Seeded", notAMember.Body, StringComparison.Ordinal);

        foreach (var (response, requested) in new[] { (notAMember, otherSpace), (noSuchSpace, missingSpace) })
        {
            Assert.All(
                GuidShaped.Matches(response.Body).Select(match => Guid.Parse(match.Value)),
                carried => Assert.Equal(requested, carried));
        }
    }

    /// <summary>
    /// AC4 / I-13 - each boundary 404 writes exactly one <c>CrossSpace</c> refusal, written by the
    /// pipeline with the acting Account, the target Space, the capability and the outcome; a served
    /// request writes none.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-11")]
    [Trait("Requirement", "AD-20")]
    [Trait("Requirement", "NFR-7")]
    public async Task Each_boundary_404_records_one_CrossSpace_refusal_and_a_served_request_records_none()
    {
        var database = Available();
        var address = RandomAddress();
        var memberSpace = Guid.NewGuid();
        var otherSpace = Guid.NewGuid();
        var missingSpace = Guid.NewGuid();
        var accountId = Guid.Empty;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            accountId = await AccountIdOfAsync(database, address);

            await SeedSpaceAsync(database, memberSpace, "Alpha", accountId);
            await SeedSpaceAsync(database, otherSpace, "Beta", SpaceSeeding.NewAccount());

            Assert.Equal(HttpStatusCode.OK, (await session.GetAsync(SpaceRoutes.StatusesOf(memberSpace))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await session.GetAsync(SpaceRoutes.StatusesOf(otherSpace))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await session.GetAsync(SpaceRoutes.StatusesOf(missingSpace))).StatusCode);
        });

        var rows = await RefusalsOfAsync(database, accountId);

        Assert.Equal(2, rows.Count);
        Assert.Equal([missingSpace, otherSpace], rows.Select(row => row.TargetSpaceId).Order());
        Assert.DoesNotContain(rows, row => row.TargetSpaceId == memberSpace);

        Assert.All(rows, row =>
        {
            Assert.Equal("CrossSpace", row.Kind);
            Assert.Equal("404", row.Outcome);
            Assert.False(string.IsNullOrWhiteSpace(row.Capability));
        });
    }

    /// <summary>
    /// AC4 - a request refused at the authentication boundary is not a Space-boundary refusal, and
    /// is not recorded.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-11")]
    [Trait("Requirement", "AD-20")]
    public async Task A_request_refused_at_authentication_records_no_refusal()
    {
        var database = Available();
        var address = RandomAddress();
        var spaceId = Guid.NewGuid();
        var accountId = Guid.Empty;

        WireResponse? anonymous = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            accountId = await AccountIdOfAsync(database, address);
            await SeedSpaceAsync(database, spaceId, "Alpha", accountId);

            Assert.Equal(HttpStatusCode.NotFound, (await session.GetAsync(SpaceRoutes.StatusesOf(Guid.NewGuid()))).StatusCode);

            anonymous = await session.GetAnonymouslyAsync(SpaceRoutes.StatusesOf(spaceId));
        });

        Assert.NotNull(anonymous);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        // The one row is the anchor's 404 - which proves the table is being written - and the 401
        // added nothing beside it.
        var rows = await RefusalsOfAsync(database, accountId);
        Assert.Single(rows);
        Assert.Equal("404", rows[0].Outcome);
    }

    /// <summary>
    /// Open question 8 - a <c>{spaceId}</c> that is not a GUID never reaches a Space, so it is
    /// answered with the same body a boundary 404 has, and records nothing.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-10")]
    public async Task A_non_GUID_spaceId_is_answered_as_a_boundary_404_is_and_records_nothing()
    {
        var database = Available();
        var address = RandomAddress();
        var accountId = Guid.Empty;

        WireResponse? boundary = null;
        WireResponse? malformed = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            accountId = await AccountIdOfAsync(database, address);

            boundary = await session.GetAsync(SpaceRoutes.StatusesOf(Guid.NewGuid()));
            malformed = await session.GetAsync("/api/v1/spaces/not-a-guid/statuses");
        });

        Assert.NotNull(boundary);
        Assert.NotNull(malformed);

        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
        Assert.Equal(boundary.Body, malformed.Body);
        Assert.Equal(boundary.ContentType, malformed.ContentType);

        // The boundary 404 was recorded; the malformed one reached no Space and was not.
        Assert.Single(await RefusalsOfAsync(database, accountId));
    }

    /// <summary>
    /// AC7 - with 50 distinct Accounts present in a Space, the 51st is refused with the bound, in
    /// the resolution transaction, so nothing is written for it.
    /// </summary>
    [Fact]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "AR-30")]
    [Trait("Requirement", "AD-25")]
    public async Task The_51st_distinct_Account_in_a_Space_is_refused_with_the_bound()
    {
        var database = Available();
        var address = RandomAddress();
        var crowded = Guid.NewGuid();
        var quiet = Guid.NewGuid();

        WireResponse? served = null;
        WireResponse? refused = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            var accountId = await AccountIdOfAsync(database, address);

            await SeedSpaceAsync(database, quiet, "Alpha", accountId);
            var present = await SeedCrowdedSpaceAsync(database, crowded, accountId, PresenceBound);
            await RecordPresenceAsync(database, crowded, present, DateTimeOffset.UtcNow.AddMinutes(10));

            served = await session.GetAsync(SpaceRoutes.StatusesOf(quiet));
            refused = await session.GetAsync(SpaceRoutes.StatusesOf(crowded));
        });

        Assert.NotNull(served);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);

        Assert.NotNull(refused);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        using var body = JsonDocument.Parse(refused.Body);
        Assert.Equal("urn:yello:problem:bound-reached", body.RootElement.GetProperty("type").GetString());
        Assert.Equal(PresenceBound, body.RootElement.GetProperty("limit").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("bound").GetString()));

        // Raised inside the resolution transaction: the refused Account's presence was not written.
        Assert.Equal(PresenceBound, await LivePresenceCountAsync(database, crowded));
    }

    /// <summary>
    /// AC7 - an Account already present is served when the Space is at the bound; the bound counts
    /// distinct Accounts, so it refuses newcomers only.
    /// </summary>
    [Fact]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "AR-30")]
    public async Task An_Account_already_present_is_served_when_the_Space_is_at_the_bound()
    {
        var database = Available();
        var address = RandomAddress();
        var crowded = Guid.NewGuid();

        WireResponse? served = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            var accountId = await AccountIdOfAsync(database, address);

            var others = await SeedCrowdedSpaceAsync(database, crowded, accountId, PresenceBound - 1);
            await RecordPresenceAsync(database, crowded, [.. others, accountId], DateTimeOffset.UtcNow.AddMinutes(10));

            served = await session.GetAsync(SpaceRoutes.StatusesOf(crowded));
        });

        Assert.NotNull(served);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }

    /// <summary>
    /// AC7 / AD-27 - an expired presence frees its slot, because expiry is evaluated in the counting
    /// statement's own <c>WHERE</c>, never by a timer.
    /// </summary>
    [Fact]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "AD-27")]
    public async Task An_expired_presence_frees_a_slot_under_the_bound()
    {
        var database = Available();
        var address = RandomAddress();
        var crowded = Guid.NewGuid();

        WireResponse? served = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            var accountId = await AccountIdOfAsync(database, address);

            var present = await SeedCrowdedSpaceAsync(database, crowded, accountId, PresenceBound);
            await RecordPresenceAsync(database, crowded, present.Take(PresenceBound - 1), DateTimeOffset.UtcNow.AddMinutes(10));
            await RecordPresenceAsync(database, crowded, present.Skip(PresenceBound - 1), DateTimeOffset.UtcNow.AddMinutes(-1));

            served = await session.GetAsync(SpaceRoutes.StatusesOf(crowded));
        });

        Assert.NotNull(served);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }

    /// <summary>
    /// AC4 - refusal records older than 90 days are purged, and younger ones kept, without any
    /// schedule: the purge is triggered by request traffic.
    /// </summary>
    /// <remarks>
    /// A fresh Host process has never purged, so the rows are written <i>before</i> it starts - a
    /// purge triggered by its very first request, sign-in included, must still find them. The purge
    /// may run off the request path, so the old row's disappearance is polled for against a deadline
    /// - waiting on the condition, never on the clock. "At most daily" is asserted where a fake
    /// <see cref="TimeProvider"/> can reach it, in dev-story's unit case, not here.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-11")]
    [Trait("Requirement", "AR-33")]
    [Trait("Requirement", "AD-10")]
    [Trait("Requirement", "AD-20")]
    [Trait("Assumption", "PRD-12-11")]
    public async Task Refusal_records_past_90_days_are_purged_by_request_traffic_and_younger_ones_kept()
    {
        var database = Available();
        var address = RandomAddress();
        var spaceId = Guid.NewGuid();
        var refused = SpaceSeeding.NewAccount();
        var stale = Guid.NewGuid();
        var fresh = Guid.NewGuid();

        // The refused Account exists before its refusals do, for a foreign key to Account if
        // dev-story declares one.
        await SeedSpaceAsync(database, Guid.NewGuid(), "Elsewhere", refused);
        await InsertRefusalAsync(database, stale, refused.Id, DateTimeOffset.UtcNow.AddDays(-91));
        await InsertRefusalAsync(database, fresh, refused.Id, DateTimeOffset.UtcNow.AddDays(-89));

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedSpaceAsync(database, spaceId, "Alpha", await AccountIdOfAsync(database, address));

            Assert.Equal(HttpStatusCode.OK, (await session.GetAsync(SpaceRoutes.StatusesOf(spaceId))).StatusCode);

            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            while (await RefusalExistsAsync(database, stale) && !deadline.IsCancellationRequested)
            {
                await Task.Yield();
            }
        });

        Assert.False(await RefusalExistsAsync(database, stale), "A refusal record 91 days old survived the purge.");
        Assert.True(await RefusalExistsAsync(database, fresh), "A refusal record 89 days old was purged.");
    }

    private const int PresenceBound = 50;

    private static async Task<IReadOnlyList<RefusalRow>> RefusalsOfAsync(RegistrationDatabase database, Guid accountId)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT TargetSpaceId, Capability, CAST(Outcome AS nvarchar(16)), Kind
            FROM dbo.[AccessRefusal] WHERE AccountId = @account
            """;
        command.Parameters.Add(new SqlParameter("@account", accountId));

        var rows = new List<RefusalRow>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add(new RefusalRow(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        }

        return rows;
    }

    /// <summary>
    /// A refusal row written directly, for the purge case. <c>AccessRefusal</c> carries no policy and
    /// no foreign key to <c>Space</c> (story open question 2), so it needs no session context; its
    /// <c>AccountId</c> is a registered Account, which satisfies a foreign key to <c>Account</c> if
    /// dev-story declares one. <c>Id</c> is supplied, because no key is generated by the database.
    /// </summary>
    private static async Task InsertRefusalAsync(RegistrationDatabase database, Guid id, Guid accountId, DateTimeOffset occurredAt)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO dbo.[AccessRefusal] (Id, AccountId, TargetSpaceId, Capability, Outcome, Kind, OccurredAt)
            VALUES (@id, @account, @space, N'ReadProjectsAndTasks', 404, N'CrossSpace', @occurred)
            """;
        command.Parameters.Add(new SqlParameter("@id", id));
        command.Parameters.Add(new SqlParameter("@account", accountId));
        command.Parameters.Add(new SqlParameter("@space", Guid.NewGuid()));
        command.Parameters.Add(new SqlParameter("@occurred", occurredAt));

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<bool> RefusalExistsAsync(RegistrationDatabase database, Guid id)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT COUNT(*) FROM dbo.[AccessRefusal] WHERE Id = @id";
        command.Parameters.Add(new SqlParameter("@id", id));

        return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture) == 1;
    }

    /// <summary>
    /// A Space with <paramref name="others"/> seeded Accounts as Members, plus the signed-in Account
    /// as a Member. Seeded in one B2 block: each <c>Account</c> before the <c>Membership</c> that
    /// references it (<c>FK_Membership_Account_AccountId</c>), the <c>Space</c> before both its
    /// <c>Membership</c> and <c>StatusDefinition</c> rows (<c>FK_Membership_Space_SpaceId</c>,
    /// <c>FK_StatusDefinition_Space_SpaceId</c>), one Membership per Account
    /// (<c>IX_Membership_SpaceId_AccountId</c>, unique), random addresses
    /// (<c>NormalizedEmailAddress</c>, unique).
    /// </summary>
    /// <returns>The seeded Accounts' ids, not including the signed-in one.</returns>
    private static async Task<IReadOnlyList<Guid>> SeedCrowdedSpaceAsync(
        RegistrationDatabase database,
        Guid spaceId,
        Guid signedInAccountId,
        int others)
    {
        var accounts = Enumerable.Range(0, others).Select(_ => SpaceSeeding.NewAccount()).ToList();

        await SpaceSeeding.SeedAsync(database, spaceId, context =>
        {
            context.Spaces.Add(SpaceSeeding.NewSpace(spaceId));
            context.StatusDefinitions.Add(SpaceSeeding.NewStatus(spaceId, "Crowded"));
            context.Memberships.Add(SpaceSeeding.NewMembership(spaceId, signedInAccountId, Role.Member));

            foreach (var account in accounts)
            {
                context.Accounts.Add(account);
                context.Memberships.Add(SpaceSeeding.NewMembership(spaceId, account.Id, Role.Member));
            }
        }, TestContext.Current.CancellationToken);

        return [.. accounts.Select(account => account.Id)];
    }

    /// <summary>
    /// Presence rows written directly, under the Space's own session context - the table is
    /// Space-scoped, so its BLOCK predicate refuses any other. Keyed <c>(SpaceId, AccountId)</c> with
    /// no surrogate id (story open question 1); every Account named holds a Membership in the Space,
    /// which satisfies a foreign key to <c>Account</c>, to <c>Space</c>, or to the Membership.
    /// </summary>
    private static async Task RecordPresenceAsync(
        RegistrationDatabase database,
        Guid spaceId,
        IEnumerable<Guid> accountIds,
        DateTimeOffset expiresAt)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);

        foreach (var accountId in accountIds)
        {
            await using var command = connection.CreateCommand();

            command.CommandText = "INSERT INTO dbo.[SpacePresence] (SpaceId, AccountId, ExpiresAt) VALUES (@space, @account, @expires)";
            command.Parameters.Add(new SqlParameter("@space", spaceId));
            command.Parameters.Add(new SqlParameter("@account", accountId));
            command.Parameters.Add(new SqlParameter("@expires", expiresAt));

            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }

    private static async Task<int> LivePresenceCountAsync(RegistrationDatabase database, Guid spaceId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT COUNT(*) FROM dbo.[SpacePresence] WHERE ExpiresAt > SYSUTCDATETIME()";

        return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture);
    }

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.None, 1000)]
    private static partial Regex GuidShaped { get; }

    private sealed record RefusalRow(Guid TargetSpaceId, string Capability, string Outcome, string Kind);

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

    private static async Task<WireSession> SignedInWithoutRegisteringAsync(HttpClient client, string address)
    {
        var session = new WireSession(client);

        await session.FetchAntiforgeryTokenAsync();

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
