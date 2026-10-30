using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Spaces;
using Yello.Domain.Memberships;
using Yello.Domain.Statuses;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 3.1 over the wire: creating a Space through <c>POST /api/v1/spaces</c>. AC1, AC2, AC5, AC9, AC10
/// (creation half); FR-5, AR-34, AD-2, AD-24.
/// </summary>
/// <remarks>
/// <para>
/// <b>RED-PHASE SCAFFOLDS (story 3.1 ATDD run, 2026-10-29).</b> Every case is skipped with the AC it waits
/// for; no route, request type or problem type of story 3.1 exists yet. Routes are the existing
/// <c>SpaceRoutes.Directory</c> (POST shares the switcher's path, Task 4) and <c>SpaceRoutes.ActiveOf</c>; the
/// one missing contract constant is held here as <see cref="InvalidSpaceNameType"/> and is replaced by
/// <c>ProblemTypes.InvalidSpaceName</c> on activation. Bodies are anonymous objects and answers are raw JSON,
/// so the scaffolds compile today and an added property cannot hide behind a DTO.
/// </para>
/// <para>
/// <b>Why a partial of <see cref="ActiveSpaceEndpointTests"/></b> and not a class of its own: the class already
/// holds a database on the Slices engine and the registration, sign-in, switcher and seeding helpers these
/// cases need. A new class would be one more database for no new capability (the orchestrator's memory rule).
/// The story names <c>Spaces/CreateSpace/</c>; the files sit beside the class instead, so the namespace
/// matches the folder.
/// </para>
/// <para>
/// <b>Seed constraints checked</b> against <c>20260828164716_InitialSchema.cs</c> and
/// <c>20261024143930_AccountScopedMembership.cs</c>. Accounts and their Personal Spaces are written by the
/// product (<c>POST /accounts</c>), so <c>UX_Account_NormalizedEmailAddress</c> holds (every address random).
/// Further Memberships (<see cref="SeedMembershipsAsync"/>) go through <see cref="SpaceSeeding"/>: one B2 block
/// per Space, the <c>Space</c> row in the same unit of work as its one <c>Membership</c>, under that Space's
/// key, so <c>FK_Membership_Space_SpaceId</c> and the RLS <c>BLOCK AFTER INSERT</c> predicate on
/// <c>Membership</c> hold; <c>FK_Membership_Account_AccountId</c> holds because the Account was registered by
/// the product; one Membership per Space keeps <c>IX_Membership_SpaceId_AccountId</c> unique; and a non-Owner
/// Membership leaves its Space with no Owner, which the filtered <c>UX_Membership_SpaceId_Owner</c> permits
/// (as stories 1.9 and 2.1 recorded). Seeded Spaces carry no Statuses: nothing here reads them.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// Story 3.1 Task 4's new stable problem type. Scaffold: replace with <c>ProblemTypes.InvalidSpaceName</c>.
    /// </summary>
    private const string InvalidSpaceNameType = "urn:yello:problem:invalid-space-name";

    /// <summary>
    /// <c>StableCode.Of(Bound.SpacesPerAccount)</c>, the resource key the client already holds.
    /// </summary>
    private const string SpacesPerAccountCode = "spaces-per-account";

    private const int SpacesPerAccount = 50;

    /// <summary>
    /// Story 3.1 Q6 (a), assumed (still open for the dev phase): 1-200 characters after trimming.
    /// </summary>
    private const int SpaceNameMaxLength = 200;

    private const string OwnerRoleName = "Owner";

    private static readonly Dictionary<string, string> NoKey = new(StringComparer.Ordinal);

    /// <summary>
    /// The Roles the bound's seeded Memberships cycle through: every Membership counts, owned or not (AC12).
    /// </summary>
    private static readonly Role[] EveryRole = [Role.Owner, Role.Admin, Role.Member, Role.Viewer];

    /// <summary>
    /// AC1, AC9, Q9 - an authenticated Account creates a Space: <c>201</c>, a <c>Location</c> of the new Space's
    /// active route, a body of exactly <c>{id, name}</c> with the name trimmed; the Space holds exactly one
    /// Membership (the caller, Owner), the default Status set in its order, and no Projects.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1 - POST /api/v1/spaces is not mapped yet (story 3.1 Task 4)")]
    [Trait("Requirement", "FR-5")]
    [Trait("Requirement", "AR-34")]
    public async Task Creating_a_Space_answers_201_and_makes_the_caller_its_one_Owner_with_the_default_Statuses_and_no_Projects()
    {
        var database = Available();
        var address = RandomAddress();
        var name = "Client work " + Suffix();
        WireResponse? created = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            await session.FetchAntiforgeryTokenAsync();

            created = await CreateSpaceAsync(session, "  " + name + "  ");
        });

        Assert.NotNull(created);
        Assert.True(created.StatusCode == HttpStatusCode.Created, $"{created.StatusCode} {created.Body}");

        var (properties, id, answeredName) = CreatedSpace(created);

        Assert.Equal(IdentityProperties, properties);
        Assert.Equal(name, answeredName);
        Assert.Equal(SpaceRoutes.ActiveOf(id), created.Header("Location"));

        var accountId = await AccountIdOfAsync(database, address);
        var membership = Assert.Single(await MembershipsInAsync(database, id));

        Assert.Equal(accountId, membership.AccountId);
        Assert.Equal(OwnerRoleName, membership.Role);
        Assert.Equal(DefaultStatusSet.Names, await StatusNamesInOrderAsync(database, id));
        Assert.Equal(0, await ProjectCountInAsync(database, id));
        Assert.Equal([name], await SpaceNamesOfAsync(database, id));
    }

    /// <summary>
    /// AC2, FR-5, AD-24 - creation has no effect on, and no visibility from, any other Space: the creator's
    /// Personal Space keeps its name, its Statuses, its one Membership and its Projects; another Account's
    /// switcher does not list the new Space. Anchor: the creator's own switcher lists both of its Spaces.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2 - POST /api/v1/spaces is not mapped yet (story 3.1 Task 4)")]
    [Trait("Requirement", "FR-5")]
    [Trait("Requirement", "NFR-1")]
    public async Task Creating_a_Space_leaves_every_other_Space_unchanged_and_unseen()
    {
        var database = Available();
        var (creatorAddress, otherAddress) = (RandomAddress(), RandomAddress());
        WireResponse? created = null;
        WireResponse? creatorsSwitcher = null;
        WireResponse? othersSwitcher = null;
        var personal = Guid.Empty;
        SpaceSnapshot? before = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var creator = await SignedInAsync(client, creatorAddress);
            var other = await SignedInAsync(client, otherAddress);

            personal = Assert.Single(IdentityRows(await creator.GetAsync(SpaceRoutes.Directory))).Id;
            before = await SnapshotAsync(database, personal);

            await creator.FetchAntiforgeryTokenAsync();
            created = await CreateSpaceAsync(creator, "Second " + Suffix());

            creatorsSwitcher = await creator.GetAsync(SpaceRoutes.Directory);
            othersSwitcher = await other.GetAsync(SpaceRoutes.Directory);
        });

        Assert.NotNull(created);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var newId = CreatedSpace(created).Id;

        Assert.Equal([newId, personal], IdentityRows(creatorsSwitcher!).Select(row => row.Id).Order());
        Assert.DoesNotContain(newId, IdentityRows(othersSwitcher!).Select(row => row.Id));
        Assert.DoesNotContain(newId.ToString("D"), othersSwitcher!.Body, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(before, await SnapshotAsync(database, personal));
    }

    /// <summary>
    /// AC5, FR-6 - Space names are not unique across Yello: two Accounts each create a Space with the same name,
    /// both are <c>201</c>, the two ids differ, and each Account's switcher lists its own and never the other's.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC5 - POST /api/v1/spaces is not mapped yet (story 3.1 Task 4)")]
    [Trait("Requirement", "FR-6")]
    public async Task Two_Accounts_may_each_create_a_Space_with_the_same_name_and_neither_sees_the_other()
    {
        var database = Available();
        var shared = "Same name " + Suffix();
        var answers = new List<WireResponse>();
        var switchers = new List<WireResponse>();

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            foreach (var address in new[] { RandomAddress(), RandomAddress() })
            {
                var session = await SignedInAsync(client, address);
                await session.FetchAntiforgeryTokenAsync();

                answers.Add(await CreateSpaceAsync(session, shared));
                switchers.Add(await session.GetAsync(SpaceRoutes.Directory));
            }
        });

        Assert.Equal(2, answers.Count);
        Assert.All(answers, answer => Assert.True(answer.StatusCode == HttpStatusCode.Created, $"{answer.StatusCode} {answer.Body}"));

        var (first, second) = (CreatedSpace(answers[0]).Id, CreatedSpace(answers[1]).Id);

        Assert.NotEqual(first, second);
        Assert.Contains(IdentityRows(switchers[0]), row => row.Id == first && row.Name == shared);
        Assert.DoesNotContain(IdentityRows(switchers[0]), row => row.Id == second);
        Assert.Contains(IdentityRows(switchers[1]), row => row.Id == second && row.Name == shared);
        Assert.DoesNotContain(IdentityRows(switchers[1]), row => row.Id == first);
        Assert.Equal(2, (await SpaceIdsNamedAsync(database, shared)).Count);
    }

    /// <summary>
    /// AC9, Q6 (a) assumed - a name that is empty, blank after trimming, or longer than 200 characters is refused
    /// <c>400 invalid-space-name</c>, never a <c>500</c> and never truncated, and nothing is written. Anchor: a
    /// name of exactly 200 characters is created.
    /// </summary>
    /// <param name="length">The submitted name's length.</param>
    /// <param name="blank">Whether it is spaces only.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC9 - POST /api/v1/spaces and SpaceName.TryNormalise do not exist yet (story 3.1 Tasks 3, 4)")]
    [InlineData(0, false)]
    [InlineData(3, true)]
    [InlineData(SpaceNameMaxLength + 1, false)]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "AR-34")]
    public async Task An_invalid_Space_name_is_refused_400_and_nothing_is_written(int length, bool blank)
    {
        var database = Available();
        var address = RandomAddress();
        var stem = Suffix();
        var refusedName = blank ? new string(' ', length) : string.Concat(Enumerable.Repeat(stem, (length / stem.Length) + 1))[..length];
        var limitName = string.Concat(Enumerable.Repeat(stem, (SpaceNameMaxLength / stem.Length) + 1))[..SpaceNameMaxLength];
        WireResponse? refused = null;
        WireResponse? anchor = null;
        WireResponse? switcher = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            await session.FetchAntiforgeryTokenAsync();

            refused = await CreateSpaceAsync(session, refusedName);
            anchor = await CreateSpaceAsync(session, limitName);
            switcher = await session.GetAsync(SpaceRoutes.Directory);
        });

        Assert.Equal(HttpStatusCode.BadRequest, refused?.StatusCode);
        Assert.Equal(InvalidSpaceNameType, ProblemType(refused!));
        Assert.Equal(HttpStatusCode.Created, anchor?.StatusCode);

        // Exactly the Personal Space and the anchor: the refused name wrote nothing, not even a truncated Space.
        Assert.Equal(2, IdentityRows(switcher!).Count);
        Assert.Contains(IdentityRows(switcher!), row => row.Name == limitName);

        if (!blank && length > 0)
        {
            Assert.Empty(await SpaceIdsNamedAsync(database, refusedName));
            Assert.Single(await SpaceIdsNamedAsync(database, limitName));
        }
    }

    /// <summary>
    /// AD-2 - the new Space's id is the server's: an <c>id</c> carried in the body is ignored, the answered id is
    /// another, and no Space has the body's id.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - POST /api/v1/spaces is not mapped yet (story 3.1 Task 4)")]
    [Trait("Requirement", "AD-2")]
    public async Task The_new_Space_id_is_generated_by_the_server_and_never_taken_from_the_body()
    {
        var database = Available();
        var address = RandomAddress();
        var proposed = Guid.NewGuid();
        WireResponse? created = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            await session.FetchAntiforgeryTokenAsync();

            created = await session.SendWithHeadersAsync(
                HttpMethod.Post,
                SpaceRoutes.Directory,
                JsonContent.Create(new { id = proposed, name = "Proposed " + Suffix() }, options: JsonOptions),
                NoKey);
        });

        Assert.Equal(HttpStatusCode.Created, created?.StatusCode);
        Assert.NotEqual(proposed, CreatedSpace(created!).Id);
        Assert.Empty(await SpaceNamesOfAsync(database, proposed));
    }

    /// <summary>
    /// AC10, CAP-35 - with no Session, creation is refused <c>401</c> and nothing is recorded or written; an API
    /// Token is no credential on this route, answered byte for byte as no credential at all (like the switcher,
    /// <c>ISO-LIST-SPACES</c>). Anchor: the same Account's Session creates.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC10 - POST /api/v1/spaces is not mapped yet (story 3.1 Task 4)")]
    [Trait("Requirement", "AR-10")]
    [Trait("Requirement", "FR-36")]
    public async Task Creation_without_a_Session_is_refused_401_and_a_Token_is_no_credential_on_it()
    {
        var database = Available();
        var address = RandomAddress();
        var tokenName = "Token tried " + Suffix();
        var anonymousName = "Anonymous tried " + Suffix();
        WireResponse? anonymous = null;
        WireResponse? withToken = null;
        WireResponse? anchor = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            var personal = Assert.Single(IdentityRows(await session.GetAsync(SpaceRoutes.Directory))).Id;
            var token = IssuedToken(await IssueAsync(session, personal, "creation attempt")).Plaintext;

            var stranger = new WireSession(client);
            await stranger.FetchAntiforgeryTokenAsync();

            anonymous = await CreateSpaceAsync(stranger, anonymousName);
            withToken = await session.SendWithTokenAndHeadersAsync(HttpMethod.Post, SpaceRoutes.Directory, token, SpaceBody(tokenName), NoKey);

            await session.FetchAntiforgeryTokenAsync();
            anchor = await CreateSpaceAsync(session, "Session created " + Suffix());
        });

        Assert.Equal(HttpStatusCode.Created, anchor?.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous?.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, withToken?.StatusCode);
        Assert.Equal(anonymous!.Body, withToken!.Body);
        Assert.Equal(anonymous.HeaderNames, withToken.HeaderNames);

        Assert.Empty(await SpaceIdsNamedAsync(database, anonymousName));
        Assert.Empty(await SpaceIdsNamedAsync(database, tokenName));
        Assert.Empty(await RefusalsOfAsync(database, await AccountIdOfAsync(database, address)));
    }

    private static JsonContent SpaceBody(string name) => JsonContent.Create(new { name }, options: JsonOptions);

    private static Dictionary<string, string> KeyHeaders(string? key) =>
        key is null ? NoKey : new Dictionary<string, string>(StringComparer.Ordinal) { [Idempotency.HeaderName] = key };

    /// <summary>
    /// Creates a Space through the Session, as the browser does: the cookie and the anti-forgery header the
    /// caller already holds (fetched after sign-in), and an <c>Idempotency-Key</c> when one is given.
    /// </summary>
    private static Task<WireResponse> CreateSpaceAsync(WireSession session, string name, string? key = null) =>
        session.SendWithHeadersAsync(HttpMethod.Post, SpaceRoutes.Directory, SpaceBody(name), KeyHeaders(key));

    private static (string[] Properties, Guid Id, string Name) CreatedSpace(WireResponse created)
    {
        Assert.True(created.StatusCode == HttpStatusCode.Created, $"{created.StatusCode} {created.Body}");

        using var body = JsonDocument.Parse(created.Body);
        var root = body.RootElement;

        return (
            [.. root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)],
            root.GetProperty("id").GetGuid(),
            root.GetProperty("name").GetString() ?? string.Empty);
    }

    /// <summary>
    /// <paramref name="count"/> further Memberships for <paramref name="accountId"/>, each in a Space of its own,
    /// cycling Owner, Admin, Member, Viewer - the bound cases' way to reach 49 or 50 without 49 requests. See the
    /// file's remarks for the constraints each block satisfies.
    /// </summary>
    /// <returns>The seeded Spaces, in seeding order.</returns>
    private static async Task<IReadOnlyList<Guid>> SeedMembershipsAsync(RegistrationDatabase database, Guid accountId, int count)
    {
        var spaces = new List<Guid>();

        for (var index = 0; index < count; index++)
        {
            var spaceId = Guid.NewGuid();
            var role = EveryRole[index % EveryRole.Length];

            await SeedNamedSpaceAsync(database, spaceId, $"Seeded {index:D2} {Suffix()}", accountId, role);
            spaces.Add(spaceId);
        }

        return spaces;
    }

    /// <summary>
    /// The Memberships in one Space, under that Space's own key (the <c>Membership</c> FILTER predicate).
    /// </summary>
    private static async Task<IReadOnlyList<(Guid AccountId, string Role)>> MembershipsInAsync(RegistrationDatabase database, Guid spaceId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT AccountId, Role FROM dbo.[Membership] WHERE SpaceId = @space";
        command.Parameters.Add(new SqlParameter("@space", spaceId));

        var rows = new List<(Guid, string)>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add((reader.GetGuid(0), reader.GetString(1)));
        }

        return rows;
    }

    /// <summary>
    /// Every Membership an Account holds, in any Space, under the <c>AccountId</c> key - story 1.7's predicate
    /// shows exactly that Account's Memberships across Spaces. The live count AC12 is about.
    /// </summary>
    private static async Task<int> MembershipCountOfAsync(RegistrationDatabase database, Guid accountId)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);

        await using (var key = connection.CreateCommand())
        {
            key.CommandText = "EXEC sp_set_session_context @key = N'AccountId', @value = @value, @read_only = 1";
            key.Parameters.Add(new SqlParameter("@value", SqlDbType.Variant) { Value = accountId });
            await key.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT COUNT(*) FROM dbo.[Membership] WHERE AccountId = @account";
        command.Parameters.Add(new SqlParameter("@account", accountId));

        return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyList<string>> StatusNamesInOrderAsync(RegistrationDatabase database, Guid spaceId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Name FROM dbo.[StatusDefinition] ORDER BY Position, Id";

        var names = new List<string>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static async Task<IReadOnlyList<Guid>> StatusIdsInAsync(RegistrationDatabase database, Guid spaceId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Id FROM dbo.[StatusDefinition] ORDER BY Id";

        var ids = new List<Guid>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    private static async Task<int> ProjectCountInAsync(RegistrationDatabase database, Guid spaceId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT COUNT(*) FROM dbo.[Project]";

        return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A Space's name by id. <c>Space</c> carries no policy (story 3.1 Q5 (a): a recorded variance), so this read
    /// needs no session context.
    /// </summary>
    private static async Task<IReadOnlyList<string>> SpaceNamesOfAsync(RegistrationDatabase database, Guid spaceId)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Name FROM dbo.[Space] WHERE Id = @space";
        command.Parameters.Add(new SqlParameter("@space", spaceId));

        var names = new List<string>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    /// <summary>
    /// Every Space with exactly this name. Names in these cases carry a random suffix, so a match is this case's.
    /// </summary>
    private static async Task<IReadOnlyList<Guid>> SpaceIdsNamedAsync(RegistrationDatabase database, string name)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Id FROM dbo.[Space] WHERE Name = @name";
        command.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, SpaceNameMaxLength + 1) { Value = name });

        var ids = new List<Guid>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    /// <summary>
    /// Everything AC2 says creation must not touch in one Space: its name, its Status ids, its Memberships and its
    /// Project count.
    /// </summary>
    private static async Task<SpaceSnapshot> SnapshotAsync(RegistrationDatabase database, Guid spaceId) =>
        new(
            string.Join('|', await SpaceNamesOfAsync(database, spaceId)),
            string.Join('|', await StatusIdsInAsync(database, spaceId)),
            string.Join('|', (await MembershipsInAsync(database, spaceId)).Select(row => $"{row.AccountId:D}:{row.Role}").Order(StringComparer.Ordinal)),
            await ProjectCountInAsync(database, spaceId));

    /// <summary>
    /// One Space's state, as text, so two snapshots compare by value.
    /// </summary>
    /// <param name="Name">The name.</param>
    /// <param name="StatusIds">The Status ids, ordered.</param>
    /// <param name="Memberships">The Memberships as account:role, ordered.</param>
    /// <param name="Projects">The Project count.</param>
    private sealed record SpaceSnapshot(string Name, string StatusIds, string Memberships, int Projects);
}
