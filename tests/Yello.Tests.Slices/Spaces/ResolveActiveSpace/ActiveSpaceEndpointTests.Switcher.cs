using System.Data.SqlTypes;
using System.Net;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Accounts;
using Yello.Contracts.Spaces;
using Yello.Domain.Memberships;
using Yello.Domain.Spaces;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 1.7 over the wire: the Space switcher (<c>GET /api/v1/spaces</c>) and the active Space's
/// own answer (<c>GET /api/v1/spaces/{spaceId}</c>). AC1, AC2, AC3, AC4; test-design I-11 and I-10.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every case anchors on a success first.</b> Without the routes both paths fall through to
/// <c>UnmatchedApiEndpoint</c> and answer <c>404 not-available</c>, so each case fails on its first
/// anchor rather than passing an absence assertion over nothing.
/// </para>
/// <para>
/// <b>In this class rather than a new one</b>, as a second partial file: a container-backed class is
/// one more SQL Server engine on a full run, and this machine has no headroom left
/// (<c>TESTING-CONVENTIONS.md:327-332</c>).
/// </para>
/// <para>
/// <b>The wire shape is read as raw JSON, never through a DTO.</b> I-11 says the switcher returns
/// Space identity and nothing else; a deserialised DTO silently drops a property it does not
/// declare, so a <c>role</c> or a <c>memberCount</c> added to the response would pass a DTO-based
/// assertion. The property set of every row is asserted exactly.
/// </para>
/// <para>
/// <b>The registered Account's Personal Space is found by name.</b> Registration mints its id and
/// <c>Membership</c> is behind the policy, but <c>Space</c> carries no policy and its name is
/// composed from the display name (<c>PersonalSpaceName.From</c>), which these cases make unique per
/// test. That is a read of a policy-free table, not a bypass.
/// </para>
/// <para>
/// <b>Seed constraints checked</b> against <c>20260828164716_InitialSchema.cs</c>: every
/// <c>Membership</c>'s Account is the registered one or is added in the same block before it
/// (<c>FK_Membership_Account_AccountId</c>); every <c>Space</c> is added in the block that first
/// writes a <c>Membership</c> into it (<c>FK_Membership_Space_SpaceId</c>); at most one Owner per
/// Space (<c>UX_Membership_SpaceId_Owner</c>, filtered unique); one Membership per Account per
/// Space (<c>IX_Membership_SpaceId_AccountId</c>, unique); each block runs under its own Space's
/// session context, which the AFTER INSERT BLOCK predicate on <c>Membership</c> requires.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    private static readonly string[] IdentityProperties = ["id", "name"];
    private static readonly string[] ActiveSpaceProperties = ["id", "name", "role"];

    /// <summary>
    /// AC1 / AC2 / I-11 - the switcher lists exactly the Spaces the Account holds a Membership in,
    /// each as an id and a name and nothing else, and another Account's Space is not among them.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-9")]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "AD-24")]
    public async Task The_switcher_lists_exactly_the_Spaces_the_Account_holds_a_Membership_in()
    {
        var database = Available();
        var (addressA, nameA) = (RandomAddress(), DisplayName());
        var (addressB, nameB) = (RandomAddress(), DisplayName());
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        var s3 = Guid.NewGuid();

        WireResponse? listedForA = null;
        WireResponse? listedForB = null;
        var personalA = Guid.Empty;
        var personalB = Guid.Empty;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var sessionA = await RegisteredAndSignedInAsync(client, addressA, nameA);
            var accountA = await AccountIdOfAsync(database, addressA);
            personalA = await PersonalSpaceOfAsync(database, nameA);

            await SeedNamedSpaceAsync(database, s1, "Alpha " + Suffix(), accountA, Role.Owner);
            await SeedNamedSpaceAsync(database, s2, "Beta " + Suffix(), accountA, Role.Viewer);

            listedForA = await sessionA.GetAsync(SpaceRoutes.Directory);

            var sessionB = await RegisteredAndSignedInAsync(client, addressB, nameB);
            var accountB = await AccountIdOfAsync(database, addressB);
            personalB = await PersonalSpaceOfAsync(database, nameB);

            await SeedNamedSpaceAsync(database, s3, "Gamma " + Suffix(), accountB, Role.Owner);

            listedForB = await sessionB.GetAsync(SpaceRoutes.Directory);
        });

        // The success anchor first: A is answered, and the answer holds A's Spaces.
        Assert.NotNull(listedForA);
        Assert.Equal(HttpStatusCode.OK, listedForA.StatusCode);

        var rowsA = IdentityRows(listedForA);

        Assert.Equal(new[] { personalA, s1, s2 }.Order(), rowsA.Select(row => row.Id).Order());

        // Then the absences: B's Space is not A's to see, and the reverse.
        Assert.DoesNotContain(rowsA, row => row.Id == s3 || row.Id == personalB);

        Assert.NotNull(listedForB);
        Assert.Equal(HttpStatusCode.OK, listedForB.StatusCode);

        var rowsB = IdentityRows(listedForB);

        Assert.Equal(new[] { personalB, s3 }.Order(), rowsB.Select(row => row.Id).Order());

        // AC2: a row is a name - exactly two properties, no count, no Role, no badge.
        Assert.All(rowsA.Concat(rowsB), row => Assert.Equal(IdentityProperties, row.Properties));
        Assert.All(rowsA.Concat(rowsB), row => Assert.False(string.IsNullOrWhiteSpace(row.Name)));
        Assert.Contains(rowsA, row => row.Id == s1 && row.Name.StartsWith("Alpha ", StringComparison.Ordinal));
    }

    /// <summary>
    /// AC1 / Q6 - an Account holding no Membership is answered <c>200 []</c>: the switcher is not a
    /// refusal surface, so an empty list is an answer, not a 404, and nothing is recorded.
    /// </summary>
    /// <remarks>
    /// Registration always provisions a Personal Space, so the only way to reach "no Membership" in
    /// Epic 1 is to remove it - done here under that Space's own session context, as leaving a Space
    /// will do in epic 3. The anchor is the same Account being listed its Personal Space first.
    /// </remarks>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "FR-9")]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "AD-24")]
    public async Task The_switcher_answers_an_Account_holding_no_Membership_with_an_empty_list()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var accountId = Guid.Empty;

        WireResponse? before = null;
        WireResponse? after = null;
        var personal = Guid.Empty;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, name);
            accountId = await AccountIdOfAsync(database, address);
            personal = await PersonalSpaceOfAsync(database, name);

            before = await session.GetAsync(SpaceRoutes.Directory);

            await RemoveMembershipAsync(database, personal, accountId);

            after = await session.GetAsync(SpaceRoutes.Directory);
        });

        Assert.NotNull(before);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal([personal], IdentityRows(before).Select(row => row.Id));

        Assert.NotNull(after);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);

        using var body = JsonDocument.Parse(after.Body);
        Assert.Equal(JsonValueKind.Array, body.RootElement.ValueKind);
        Assert.Equal(0, body.RootElement.GetArrayLength());

        Assert.Empty(await RefusalsOfAsync(database, accountId));
    }

    /// <summary>
    /// AC1 - the switcher's order is deterministic: by name, then by id, because display names and
    /// therefore Space names are not unique (<c>sprint-change-proposal-2026-09-13.md:175</c>).
    /// </summary>
    /// <remarks>
    /// The tie is broken in <b>SQL Server's</b> <c>uniqueidentifier</c> order, which is what an
    /// <c>ORDER BY Id</c> in the database produces and which differs from <see cref="Guid.CompareTo(Guid)"/>
    /// - so the expectation is computed with <see cref="SqlGuid"/>. If dev-story orders in memory
    /// instead, correct this scaffold and record it in the Debug Log.
    /// </remarks>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "FR-9")]
    [Trait("Requirement", "AR-29")]
    public async Task The_switcher_orders_Spaces_by_name_then_id()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var first = Guid.NewGuid();
        var twinA = Guid.NewGuid();
        var twinB = Guid.NewGuid();
        var personal = Guid.Empty;
        var twinName = "Beta twin " + Suffix();

        WireResponse? listed = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, name);
            var accountId = await AccountIdOfAsync(database, address);
            personal = await PersonalSpaceOfAsync(database, name);

            await SeedNamedSpaceAsync(database, twinA, twinName, accountId, Role.Member);
            await SeedNamedSpaceAsync(database, first, "Alpha " + Suffix(), accountId, Role.Member);
            await SeedNamedSpaceAsync(database, twinB, twinName, accountId, Role.Member);

            listed = await session.GetAsync(SpaceRoutes.Directory);
        });

        Assert.NotNull(listed);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);

        // "Alpha" < "Beta" < "Ravi-..." (the Personal Space) under any collation this product uses.
        var twins = new[] { twinA, twinB }.OrderBy(id => new SqlGuid(id)).ToArray();

        Assert.Equal([first, twins[0], twins[1], personal], IdentityRows(listed).Select(row => row.Id));
    }

    /// <summary>
    /// AC3 - the switcher is Account-scoped by the pipeline, so a request with no Session is refused
    /// at the authentication boundary - <c>401 authentication-failed</c>, never the Space boundary's
    /// 404 - and is not recorded as a refusal.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-3")]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "AD-24")]
    public async Task The_switcher_refuses_a_request_without_a_Session_with_401_and_records_nothing()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var accountId = Guid.Empty;

        WireResponse? served = null;
        WireResponse? anonymous = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, name);
            accountId = await AccountIdOfAsync(database, address);

            served = await session.GetAsync(SpaceRoutes.Directory);

            // One recorded boundary 404, so the table is shown to be written to.
            Assert.Equal(HttpStatusCode.NotFound, (await session.GetAsync(SpaceRoutes.StatusesOf(Guid.NewGuid()))).StatusCode);

            anonymous = await session.GetAnonymouslyAsync(SpaceRoutes.Directory);
        });

        Assert.NotNull(served);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);

        Assert.NotNull(anonymous);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(ProblemTypes.AuthenticationFailed, ProblemType(anonymous));
        Assert.DoesNotContain(name, anonymous.Body, StringComparison.Ordinal);

        var rows = await RefusalsOfAsync(database, accountId);
        Assert.Single(rows);
        Assert.Equal("404", rows[0].Outcome);
    }

    /// <summary>
    /// AC4 / I-10 (request path) - the acting Role is the destination's, read on that request: an
    /// Account that is Owner in one Space and Viewer in another is told exactly that as it moves
    /// between them, on one Session, with no carry-over in either direction.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-9")]
    [Trait("Requirement", "NFR-2")]
    [Trait("Requirement", "AR-4")]
    [Trait("Requirement", "AD-1")]
    public async Task Each_Space_reports_the_Role_held_there_with_no_carry_over_across_switches()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var owned = Guid.NewGuid();
        var viewed = Guid.NewGuid();
        var ownedName = "Alpha " + Suffix();
        var viewedName = "Beta " + Suffix();

        var answers = new List<(Guid Requested, WireResponse Response)>();

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, name);
            var accountId = await AccountIdOfAsync(database, address);

            await SeedNamedSpaceAsync(database, owned, ownedName, accountId, Role.Owner);
            await SeedNamedSpaceAsync(database, viewed, viewedName, accountId, Role.Viewer);

            // CAP-2: one Session throughout, never re-authenticated.
            foreach (var space in new[] { owned, viewed, owned, viewed })
            {
                answers.Add((space, await session.GetAsync(SpaceRoutes.ActiveOf(space))));
            }
        });

        Assert.Equal(4, answers.Count);

        Assert.All(answers, answer =>
        {
            Assert.Equal(HttpStatusCode.OK, answer.Response.StatusCode);

            var (properties, id, spaceName, role) = ActiveSpaceAnswer(answer.Response);

            Assert.Equal(ActiveSpaceProperties, properties);
            Assert.Equal(answer.Requested, id);
            Assert.Equal(answer.Requested == owned ? ownedName : viewedName, spaceName);
            Assert.Equal(answer.Requested == owned ? "owner" : "viewer", role);
        });
    }

    /// <summary>
    /// AC4 / AD-1 - the Role comes from the Membership row as it is on this request: a Role changed
    /// between two requests reaches the second answer, with no re-authentication.
    /// </summary>
    [Fact]
    [Trait("Requirement", "NFR-2")]
    [Trait("Requirement", "AR-4")]
    [Trait("Requirement", "AD-1")]
    public async Task A_Role_changed_between_two_requests_reaches_the_next_answer_about_the_Space()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var spaceId = Guid.NewGuid();

        WireResponse? before = null;
        WireResponse? after = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, name);
            var accountId = await AccountIdOfAsync(database, address);
            var membership = SpaceSeeding.NewMembership(spaceId, accountId, Role.Viewer);

            await SpaceSeeding.SeedAsync(database, spaceId, context =>
            {
                context.Spaces.Add(NamedSpace(spaceId, "Alpha " + Suffix()));
                context.Memberships.Add(membership);
            }, TestContext.Current.CancellationToken);

            before = await session.GetAsync(SpaceRoutes.ActiveOf(spaceId));

            await SpaceSeeding.SeedAsync(database, spaceId, context =>
            {
                context.Memberships.Attach(membership);
                membership.Role = Role.Admin;
            }, TestContext.Current.CancellationToken);

            after = await session.GetAsync(SpaceRoutes.ActiveOf(spaceId));
        });

        Assert.NotNull(before);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal("viewer", ActiveSpaceAnswer(before).Role);

        Assert.NotNull(after);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Equal("admin", ActiveSpaceAnswer(after).Role);
    }

    /// <summary>
    /// AC4 / AD-3 - the active Space's answer is a Space-scoped route like any other: a Space the
    /// Account is not in and one that does not exist are refused with the same boundary 404, and
    /// each is recorded as one <c>CrossSpace</c> refusal. No new refusal code.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "AR-10")]
    [Trait("Requirement", "AR-11")]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-20")]
    public async Task The_active_Space_answer_refuses_a_non_member_and_a_missing_Space_alike_and_records_each()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var memberSpace = Guid.NewGuid();
        var otherSpace = Guid.NewGuid();
        var missingSpace = Guid.NewGuid();
        var otherName = "Beta " + Suffix();
        var accountId = Guid.Empty;

        WireResponse? served = null;
        WireResponse? notAMember = null;
        WireResponse? noSuchSpace = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, name);
            accountId = await AccountIdOfAsync(database, address);

            await SeedNamedSpaceAsync(database, memberSpace, "Alpha " + Suffix(), accountId, Role.Member);

            var stranger = SpaceSeeding.NewAccount();

            await SpaceSeeding.SeedAsync(database, otherSpace, context =>
            {
                context.Accounts.Add(stranger);
                context.Spaces.Add(NamedSpace(otherSpace, otherName));
                context.Memberships.Add(SpaceSeeding.NewMembership(otherSpace, stranger.Id, Role.Owner));
            }, TestContext.Current.CancellationToken);

            served = await session.GetAsync(SpaceRoutes.ActiveOf(memberSpace));
            notAMember = await session.GetAsync(SpaceRoutes.ActiveOf(otherSpace));
            noSuchSpace = await session.GetAsync(SpaceRoutes.ActiveOf(missingSpace));
        });

        Assert.NotNull(served);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);

        Assert.NotNull(notAMember);
        Assert.NotNull(noSuchSpace);

        Assert.Equal(HttpStatusCode.NotFound, notAMember.StatusCode);
        Assert.Equal(ProblemTypes.NotAvailable, ProblemType(notAMember));
        Assert.Equal(notAMember.Body, noSuchSpace.Body);
        Assert.Equal(notAMember.ContentType, noSuchSpace.ContentType);
        Assert.Equal(notAMember.HeaderNames, noSuchSpace.HeaderNames);
        Assert.DoesNotContain(otherName, notAMember.Body, StringComparison.Ordinal);

        var rows = await RefusalsOfAsync(database, accountId);

        Assert.Equal(new[] { missingSpace, otherSpace }.Order(), rows.Select(row => row.TargetSpaceId).Order());
        Assert.All(rows, row =>
        {
            Assert.Equal("CrossSpace", row.Kind);
            Assert.Equal("404", row.Outcome);
            Assert.Equal(nameof(Capability.ReadProjectsAndTasks), row.Capability);
        });
    }

    private static string DisplayName() => "Ravi-" + Guid.NewGuid().ToString("N");

    private static string Suffix() => Guid.NewGuid().ToString("N")[..8];

    private static Space NamedSpace(Guid spaceId, string name) =>
        new() { Id = spaceId, Name = name, CreatedAt = DateTimeOffset.UtcNow };

    /// <summary>
    /// A Space with a chosen name and one Membership in it, for an Account that already exists.
    /// </summary>
    private static Task SeedNamedSpaceAsync(RegistrationDatabase database, Guid spaceId, string name, Guid accountId, Role role) =>
        SpaceSeeding.SeedAsync(database, spaceId, context =>
        {
            context.Spaces.Add(NamedSpace(spaceId, name));
            context.Memberships.Add(SpaceSeeding.NewMembership(spaceId, accountId, role));
        }, TestContext.Current.CancellationToken);

    private static async Task<WireSession> RegisteredAndSignedInAsync(HttpClient client, string address, string displayName)
    {
        var session = new WireSession(client);

        await session.FetchAntiforgeryTokenAsync();

        var registered = await session.RegisterAsync(new RegisterAccountRequest(displayName, address, Password));

        Assert.Equal(HttpStatusCode.NoContent, registered.StatusCode);

        var signedIn = await session.SignInAsync(address, Password);

        Assert.Equal(HttpStatusCode.NoContent, signedIn.StatusCode);

        return session;
    }

    /// <summary>
    /// The registered Account's Personal Space, by its name. <c>Space</c> carries no policy, so this
    /// needs no session context; the display name is unique per test, so exactly one row matches.
    /// </summary>
    private static async Task<Guid> PersonalSpaceOfAsync(RegistrationDatabase database, string displayName)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Id FROM dbo.[Space] WHERE CHARINDEX(@name, Name) > 0";
        command.Parameters.Add(new SqlParameter("@name", displayName));

        var ids = new List<Guid>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            ids.Add(reader.GetGuid(0));
        }

        return Assert.Single(ids);
    }

    /// <summary>
    /// Removes an Account's Membership of a Space, under that Space's own session context - the
    /// FILTER predicate hides the row from any other.
    /// </summary>
    private static async Task RemoveMembershipAsync(RegistrationDatabase database, Guid spaceId, Guid accountId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "DELETE FROM dbo.[Membership] WHERE SpaceId = @space AND AccountId = @account";
        command.Parameters.Add(new SqlParameter("@space", spaceId));
        command.Parameters.Add(new SqlParameter("@account", accountId));

        Assert.Equal(1, await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
    }

    private static List<IdentityRow> IdentityRows(WireResponse response)
    {
        using var body = JsonDocument.Parse(response.Body);

        return [.. body.RootElement.EnumerateArray().Select(row => new IdentityRow(
            [.. row.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)],
            row.GetProperty("id").GetGuid(),
            row.GetProperty("name").GetString() ?? string.Empty))];
    }

    private static (string[] Properties, Guid Id, string Name, string Role) ActiveSpaceAnswer(WireResponse response)
    {
        using var body = JsonDocument.Parse(response.Body);
        var root = body.RootElement;

        return (
            [.. root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)],
            root.GetProperty("id").GetGuid(),
            root.GetProperty("name").GetString() ?? string.Empty,
            root.GetProperty("role").GetString() ?? string.Empty);
    }

    private sealed record IdentityRow(string[] Properties, Guid Id, string Name);
}
