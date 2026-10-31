using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Accounts;
using Yello.Contracts.Projects;
using Yello.Contracts.Spaces;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;
using Actor = Yello.Tests.Slices.Tasks.Actor;
using LabelWire = Yello.Tests.Slices.Tasks.LabelWire;
using TaskEditWire = Yello.Tests.Slices.Tasks.TaskEditWire;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 3.3 over the wire: the Owner deletes the active Space through <c>DELETE /api/v1/spaces/{spaceId}</c>, and the
/// Owner-only counts read the confirm states. AC1, AC4-AC6, AC9-AC11; FR-7, FR-36, AD-1, AD-3, AD-6, AD-18, AD-20,
/// AD-24, NFR-2, I-17, V-3, S-1.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 3.3 ATDD run (2026-10-31).</b> Every case is skipped with
/// <c>red: AC&lt;n&gt;</c> until the dev phase maps the route; activate one at a time. Partials of the existing
/// <see cref="ActiveSpaceEndpointTests"/>: no new container class, no new engine.
/// </para>
/// <para>
/// <b>Assumed open questions</b>, each the story's recommendation: Q1 (a) - the deletion route validates and looks up an
/// <c>Idempotency-Key</c> but records nothing, so a replay after a committed deletion is the boundary <c>404</c>; Q2 (a) -
/// <c>GET /api/v1/spaces/{spaceId}/deletion</c> answers <c>{projects, tasks, otherMembers}</c> and declares
/// <c>DeleteSpace</c> (its path is written here as <see cref="DeletionSummaryOf"/> until <c>SpaceRoutes</c> names it -
/// retarget on activation); Q3 (a) - <c>DELETE</c> carries no body and the server does not check the typed name; Q9 - a
/// Personal Space that is the Account's only Space is deletable.
/// </para>
/// <para>
/// <b>Seeding goes through the product</b> (<see cref="TaskWire"/>, <see cref="LabelWire"/>): the Space is created by
/// <c>POST /api/v1/spaces</c> (its Owner Membership and three Statuses by the provisioning), Projects, Tasks, the Label and
/// its applications, the Tokens and the presence rows by their own routes. The only rows written by SQL are the extra
/// Memberships, by <see cref="TaskWire.AddMemberAsync"/> under the Space's own key, for Accounts the product registered:
/// <c>FK_Membership_Space_SpaceId</c>, <c>FK_Membership_Account_AccountId</c>, <c>AK_Membership_SpaceId_AccountId</c>
/// (one each) and <c>UX_Membership_SpaceId_Owner</c> (never a second Owner) all hold. Roles are stored by name
/// (<c>"Member"</c>, <c>"Admin"</c>), as <c>BoardEndpointTests</c> writes them.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    private const string DeleteSpaceCapabilityCode = "delete-space";

    private static readonly string[] DeletionSummaryProperties = ["otherMembers", "projects", "tasks"];

    /// <summary>
    /// The Space-scoped tables the deletion's cascades must empty for the Space, each read under the Space's own key.
    /// <c>ApiToken</c> (no policy, keyed <c>BoundSpaceId</c>) and <c>AccessRefusal</c> (no policy, no FK) are read apart.
    /// </summary>
    private static readonly string[] SpaceScopedTables =
        ["Membership", "Project", "Task", "TaskLabel", "Label", "StatusDefinition", "SpacePresence", "IdempotencyRecord"];

    /// <summary>
    /// AC1, AC4, AC10, FR-7, §6.2, V-3 - the Owner deletes a populated Space (two Projects, a Task on every Status, an
    /// applied Label, three Memberships, Tokens for two Accounts, two presence rows and an idempotency record) with an
    /// <c>Idempotency-Key</c>, as the client always sends: <c>204</c> with no body; afterwards every Space-scoped table holds
    /// nothing for it, its Tokens are gone, the <c>Space</c> row is gone, and the <c>AccessRefusal</c> row a stranger's
    /// refusal wrote for it survives (NFR-7: no FK, 90 days). Anchored on every count before. <c>Task → StatusDefinition</c>
    /// and <c>TaskLabel → Label</c> are <c>NO ACTION</c>, so one statement succeeding over this data is the proof, not an
    /// assumption. The key exercises Q1: a pipeline that records it after the deletion meets 547 and answers <c>500</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1, AC4, AC10 - DELETE /api/v1/spaces/{spaceId} is not mapped yet, and the pipeline would record its key against a deleted Space (story 3.3 Tasks 3, 4; Q1)")]
    [Trait("Requirement", "FR-7")]
    [Trait("Requirement", "AD-6")]
    [Trait("Requirement", "AD-24")]
    [Trait("Requirement", "NFR-7")]
    [Trait("Assumption", "PRD-12-2")]
    public async Task The_Owner_deletes_a_populated_Space_and_every_row_hanging_from_it_goes_with_it()
    {
        var database = Available();
        var spaceName = "Doomed " + Suffix();
        PopulatedSpace? seeded = null;
        SpaceRowCounts? before = null;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            seeded = await PopulateSpaceAsync(client, database, spaceName);

            // A stranger's refusal names the Space: the AccessRefusal row the deletion must leave alone.
            var stranger = await TaskWire.RegisterAsync(client, database);
            Assert.Equal(HttpStatusCode.NotFound, (await stranger.Session.GetAsync(SpaceRoutes.ActiveOf(seeded.SpaceId))).StatusCode);

            before = await SpaceRowCountsAsync(database, seeded.SpaceId);
            answer = await DeleteSpaceOverWireAsync(seeded.Owner.Session, seeded.SpaceId, Guid.NewGuid().ToString("D"));
        });

        Assert.NotNull(seeded);
        Assert.NotNull(before);

        // The anchor: the Space really held a row in every table the cascade reaches, and a refusal naming it.
        Assert.All(SpaceScopedTables, table => Assert.True(before.Tables[table] > 0, $"The seed wrote no {table} row, so its absence afterwards would prove nothing."));
        Assert.Equal(2, before.Tokens);
        Assert.Equal(1, before.SpaceRow);
        Assert.True(before.Refusals > 0, "No refusal names the Space before the deletion.");

        Assert.True(answer?.StatusCode == HttpStatusCode.NoContent, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal(string.Empty, answer!.Body);

        var after = await SpaceRowCountsAsync(database, seeded.SpaceId);

        Assert.All(SpaceScopedTables, table => Assert.True(after.Tables[table] == 0, $"{after.Tables[table]} {table} row(s) survived the deletion."));
        Assert.Equal(0, after.Tokens);
        Assert.Equal(0, after.SpaceRow);
        Assert.Equal(before.Refusals, after.Refusals);
    }

    /// <summary>
    /// AC1, AC9, FR-7, FR-16, AD-3, AR-11 - an Admin, a Member or a Viewer is refused <c>403 insufficient-role</c> carrying
    /// <c>capability: delete-space</c>; the pipeline records one <c>InsufficientRole</c> refusal for <c>DeleteSpace</c>;
    /// the body names no Space; the Space is intact. Anchor: the same caller reads the Space first (<c>200</c>), so the
    /// <c>403</c> is the capability's and not the boundary's.
    /// </summary>
    /// <param name="role">The caller's Role.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC1, AC9 - DELETE /api/v1/spaces/{spaceId} is not mapped yet, so it answers 405 rather than 403 delete-space (story 3.3 Task 4)")]
    [InlineData(Role.Admin)]
    [InlineData(Role.Member)]
    [InlineData(Role.Viewer)]
    [Trait("Requirement", "FR-7")]
    [Trait("Requirement", "AR-11")]
    public async Task An_Admin_a_Member_or_a_Viewer_is_refused_403_delete_space_and_the_Space_is_intact(Role role)
    {
        var database = Available();
        var address = RandomAddress();
        var spaceId = Guid.NewGuid();
        var spaceName = "Kept " + Suffix();
        var accountId = Guid.Empty;
        WireResponse? read = null;
        WireResponse? refused = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            accountId = await AccountIdOfAsync(database, address);
            await SeedNamedSpaceAsync(database, spaceId, spaceName, accountId, role);

            read = await session.GetAsync(SpaceRoutes.ActiveOf(spaceId));

            await session.FetchAntiforgeryTokenAsync();
            refused = await DeleteSpaceOverWireAsync(session, spaceId, Guid.NewGuid().ToString("D"));
        });

        Assert.Equal(HttpStatusCode.OK, read?.StatusCode);
        Assert.True(refused?.StatusCode == HttpStatusCode.Forbidden, $"{refused?.StatusCode} {refused?.Body}");

        var problem = JsonSerializer.Deserialize<ProblemResponse>(refused!.Body, JsonOptions);

        Assert.Equal(ProblemTypes.InsufficientRole, problem?.Type);
        Assert.Equal(DeleteSpaceCapabilityCode, problem?.Capability);
        Assert.DoesNotContain(spaceName, refused.Body, StringComparison.Ordinal);

        var recorded = Assert.Single(await RefusalsOfAsync(database, accountId));

        Assert.Equal((spaceId, "InsufficientRole", nameof(Capability.DeleteSpace)), (recorded.TargetSpaceId, recorded.Kind, recorded.Capability));
        Assert.Equal([spaceName], await SpaceNamesOfAsync(database, spaceId));
    }

    /// <summary>
    /// AC9, AD-1, AD-3, AD-20 - a non-member's deletion, and a deletion of a Space that does not exist, are the boundary
    /// <c>404 not-available</c>, byte for byte the same; one <c>CrossSpace</c> refusal is recorded for the foreign Space; the
    /// foreign Space keeps its name; the body names nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - DELETE /api/v1/spaces/{spaceId} is not mapped yet, so it answers 405 rather than the boundary 404 (story 3.3 Task 4)")]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AR-10")]
    public async Task A_non_members_deletion_is_the_boundary_404_and_the_foreign_Space_is_intact()
    {
        var database = Available();
        var address = RandomAddress();
        var foreign = Guid.NewGuid();
        var foreignName = "Foreign " + Suffix();
        var accountId = Guid.Empty;
        WireResponse? refused = null;
        WireResponse? absent = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            accountId = await AccountIdOfAsync(database, address);
            await SeedForeignSpaceAsync(database, foreign, foreignName);

            await session.FetchAntiforgeryTokenAsync();
            refused = await DeleteSpaceOverWireAsync(session, foreign, Guid.NewGuid().ToString("D"));
            absent = await DeleteSpaceOverWireAsync(session, Guid.NewGuid(), Guid.NewGuid().ToString("D"));
        });

        Assert.Equal(HttpStatusCode.NotFound, refused?.StatusCode);
        Assert.Equal(ProblemTypes.NotAvailable, ProblemType(refused!));
        Assert.Equal(absent!.StatusCode, refused!.StatusCode);
        Assert.Equal(absent.Body, refused.Body);
        Assert.Equal(absent.HeaderNames, refused.HeaderNames);
        Assert.DoesNotContain(foreignName, refused.Body, StringComparison.Ordinal);

        Assert.Contains(await RefusalsOfAsync(database, accountId), row => row.TargetSpaceId == foreign && row.Kind == "CrossSpace");
        Assert.Equal([foreignName], await SpaceNamesOfAsync(database, foreign));
    }

    /// <summary>
    /// AC4, AC11, FR-36, AD-6, V-3, NFR-2, S-1 - the deletion and the Token invalidation are one transaction, and they govern
    /// the very next request. The negative anchor first: the product's statement shape (one <c>DELETE</c> of the
    /// <c>Space</c> under its own key) rolled back leaves both Tokens working and every row in place. Then the Owner's
    /// deletion: a Token bound to the deleted Space - the Owner's and a Member's - is <c>401 authentication-failed</c> on
    /// its own Space's route and on another route; the Owner's Token for another Space still answers <c>200</c>; the
    /// Owner's Session is the boundary <c>404</c> on the deleted Space and <c>200</c> on its other one.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC4, AC11 - DELETE /api/v1/spaces/{spaceId} is not mapped yet (story 3.3 Task 4)")]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "AD-6")]
    [Trait("Requirement", "NFR-2")]
    [Trait("Assumption", "PRD-12-2")]
    public async Task Deleting_the_Space_ends_its_Tokens_on_the_next_request_and_leaves_the_Owners_other_Space_working()
    {
        var database = Available();
        PopulatedSpace? seeded = null;
        SpaceRowCounts? before = null;
        SpaceRowCounts? afterRollback = null;
        var answers = new Dictionary<string, HttpStatusCode>(StringComparer.Ordinal);

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            seeded = await PopulateSpaceAsync(client, database, "Token-bound " + Suffix());
            before = await SpaceRowCountsAsync(database, seeded.SpaceId);

            // S-1's negative anchor: the deletion, rolled back, takes nothing with it.
            await DeleteSpaceInSqlAsync(database, seeded.SpaceId, commit: false);
            afterRollback = await SpaceRowCountsAsync(database, seeded.SpaceId);
            answers["owner token, after rollback"] = (await seeded.Owner.Session.GetWithTokenAsync(SpaceRoutes.ActiveOf(seeded.SpaceId), seeded.OwnerToken)).StatusCode;

            answers["delete"] = (await DeleteSpaceOverWireAsync(seeded.Owner.Session, seeded.SpaceId, Guid.NewGuid().ToString("D"))).StatusCode;

            answers["owner token, own route"] = (await seeded.Owner.Session.GetWithTokenAsync(SpaceRoutes.ActiveOf(seeded.SpaceId), seeded.OwnerToken)).StatusCode;
            answers["owner token, statuses route"] = (await seeded.Owner.Session.GetWithTokenAsync(SpaceRoutes.StatusesOf(seeded.SpaceId), seeded.OwnerToken)).StatusCode;
            answers["owner token, another Space"] = (await seeded.Owner.Session.GetWithTokenAsync(SpaceRoutes.ActiveOf(seeded.Owner.SpaceId), seeded.OwnerToken)).StatusCode;
            answers["member token, own route"] = (await seeded.Member.Session.GetWithTokenAsync(SpaceRoutes.ActiveOf(seeded.SpaceId), seeded.MemberToken)).StatusCode;
            answers["owner kept token"] = (await seeded.Owner.Session.GetWithTokenAsync(SpaceRoutes.ActiveOf(seeded.Owner.SpaceId), seeded.OwnerKeptToken)).StatusCode;
            answers["owner session, deleted Space"] = (await seeded.Owner.Session.GetAsync(SpaceRoutes.ActiveOf(seeded.SpaceId))).StatusCode;
            answers["owner session, other Space"] = (await seeded.Owner.Session.GetAsync(SpaceRoutes.ActiveOf(seeded.Owner.SpaceId))).StatusCode;
        });

        Assert.Equal(before!.Flattened, afterRollback!.Flattened);
        Assert.Equal(HttpStatusCode.OK, answers["owner token, after rollback"]);
        Assert.Equal(HttpStatusCode.NoContent, answers["delete"]);

        Assert.Equal(HttpStatusCode.Unauthorized, answers["owner token, own route"]);
        Assert.Equal(HttpStatusCode.Unauthorized, answers["owner token, statuses route"]);
        Assert.Equal(HttpStatusCode.Unauthorized, answers["owner token, another Space"]);
        Assert.Equal(HttpStatusCode.Unauthorized, answers["member token, own route"]);
        Assert.Equal(HttpStatusCode.OK, answers["owner kept token"]);
        Assert.Equal(HttpStatusCode.NotFound, answers["owner session, deleted Space"]);
        Assert.Equal(HttpStatusCode.OK, answers["owner session, other Space"]);

        Assert.Equal([seeded!.Owner.SpaceId], (await TokenRowsOfAsync(database, seeded.Owner.AccountId)).Select(row => row.BoundSpaceId));
        Assert.Empty(await TokenRowsOfAsync(database, seeded.Member.AccountId));
    }

    /// <summary>
    /// AC5, FR-7, I-11 - another Account holding Membership in the deleted Space loses it and nothing else: its switcher no
    /// longer lists it, its Session is the boundary <c>404</c> there, and it keeps its own Personal Space, its Membership of
    /// a third Space, and its Account. Anchored on the switcher listing all three before.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC5 - DELETE /api/v1/spaces/{spaceId} is not mapped yet (story 3.3 Task 4)")]
    [Trait("Requirement", "FR-7")]
    [Trait("Requirement", "FR-9")]
    public async Task Another_Account_loses_only_the_deleted_Space()
    {
        var database = Available();
        PopulatedSpace? seeded = null;
        WireResponse? listedBefore = null;
        WireResponse? listedAfter = null;
        var answers = new Dictionary<string, HttpStatusCode>(StringComparer.Ordinal);

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            seeded = await PopulateSpaceAsync(client, database, "Shared " + Suffix());

            // The Member also belongs to the Owner's Personal Space: a third Membership the deletion must not touch.
            await TaskWire.AddMemberAsync(database, seeded.Owner.SpaceId, seeded.Member.AccountId, "Member");

            listedBefore = await seeded.Member.Session.GetAsync(SpaceRoutes.Directory);
            answers["delete"] = (await DeleteSpaceOverWireAsync(seeded.Owner.Session, seeded.SpaceId, Guid.NewGuid().ToString("D"))).StatusCode;
            listedAfter = await seeded.Member.Session.GetAsync(SpaceRoutes.Directory);

            answers["deleted"] = (await seeded.Member.Session.GetAsync(SpaceRoutes.ActiveOf(seeded.SpaceId))).StatusCode;
            answers["own"] = (await seeded.Member.Session.GetAsync(SpaceRoutes.ActiveOf(seeded.Member.SpaceId))).StatusCode;
            answers["third"] = (await seeded.Member.Session.GetAsync(SpaceRoutes.ActiveOf(seeded.Owner.SpaceId))).StatusCode;
        });

        Assert.Equal(
            new[] { seeded!.SpaceId, seeded.Member.SpaceId, seeded.Owner.SpaceId }.Order(),
            IdentityRows(listedBefore!).Select(row => row.Id).Order());

        Assert.Equal(HttpStatusCode.NoContent, answers["delete"]);
        Assert.Equal(
            new[] { seeded.Member.SpaceId, seeded.Owner.SpaceId }.Order(),
            IdentityRows(listedAfter!).Select(row => row.Id).Order());
        Assert.Equal(HttpStatusCode.NotFound, answers["deleted"]);
        Assert.Equal(HttpStatusCode.OK, answers["own"]);
        Assert.Equal(HttpStatusCode.OK, answers["third"]);
        Assert.Equal(2, await MembershipCountOfAsync(database, seeded.Member.AccountId));
        Assert.Equal(1, await AccountRowsAsync(database, seeded.Member.AccountId));
    }

    /// <summary>
    /// AC6, Q9, FR-7, UX-DR35, §6.2 - an Account may delete its Personal Space when it is its only Space: <c>204</c>; the
    /// Account persists and its Session still works; the switcher answers <c>200 []</c>; nothing is created for it; and it
    /// can then create a Space itself (<c>201</c>). No Space is undeletable. Anchored on the switcher listing exactly the
    /// Personal Space before.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC6 - DELETE /api/v1/spaces/{spaceId} is not mapped yet (story 3.3 Task 4; Q9)")]
    [Trait("Requirement", "FR-7")]
    [Trait("Requirement", "UX-DR35")]
    [Trait("Assumption", "PRD-12-2")]
    public async Task An_Account_may_delete_its_last_Space_belongs_to_none_and_can_create_one()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? listedBefore = null;
        WireResponse? deleted = null;
        WireResponse? listedAfter = null;
        WireResponse? session = null;
        WireResponse? created = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await TaskWire.RegisterAsync(client, database);

            listedBefore = await owner.Session.GetAsync(SpaceRoutes.Directory);
            deleted = await DeleteSpaceOverWireAsync(owner.Session, owner.SpaceId, Guid.NewGuid().ToString("D"));
            listedAfter = await owner.Session.GetAsync(SpaceRoutes.Directory);
            session = await owner.Session.GetAsync(SessionRoutes.Current);
            created = await CreateSpaceAsync(owner.Session, "Started again " + Suffix(), Guid.NewGuid().ToString("D"));
        });

        Assert.Equal([owner!.SpaceId], IdentityRows(listedBefore!).Select(row => row.Id));
        Assert.True(deleted?.StatusCode == HttpStatusCode.NoContent, $"{deleted?.StatusCode} {deleted?.Body}");
        Assert.Equal(HttpStatusCode.OK, listedAfter?.StatusCode);
        Assert.Empty(IdentityRows(listedAfter!));
        Assert.True(session?.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent, $"The Session ended with the last Space: {session?.StatusCode}");
        Assert.Equal(1, await AccountRowsAsync(database, owner.AccountId));
        Assert.Empty(await SpaceNamesOfAsync(database, owner.SpaceId));
        Assert.Equal(HttpStatusCode.Created, created?.StatusCode);
    }

    /// <summary>
    /// AC6, AD-25 (3.1's live count) - an Account at 50 Spaces is refused a 51st (the anchor), deletes one it owns, and can
    /// then create one: the deletion frees a slot with no counter to maintain.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC6 - DELETE /api/v1/spaces/{spaceId} is not mapped yet (story 3.3 Task 4)")]
    [Trait("Priority", "P2")]
    [Trait("Requirement", "AR-30")]
    [Trait("Requirement", "AD-25")]
    public async Task An_Account_at_fifty_Spaces_deletes_one_and_can_create_one()
    {
        var database = Available();
        var address = RandomAddress();
        WireResponse? atTheBound = null;
        WireResponse? deleted = null;
        WireResponse? afterDeletion = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            var accountId = await AccountIdOfAsync(database, address);

            // The Personal Space plus 49 seeded: 50. The first seeded is an Owner Membership (EveryRole[0]).
            var seededSpaces = await SeedMembershipsAsync(database, accountId, SpacesPerAccount - 1);

            await session.FetchAntiforgeryTokenAsync();
            atTheBound = await CreateSpaceAsync(session, "Refused at fifty " + Suffix());
            deleted = await DeleteSpaceOverWireAsync(session, seededSpaces[0], Guid.NewGuid().ToString("D"));
            afterDeletion = await CreateSpaceAsync(session, "Admitted after a deletion " + Suffix());
        });

        Assert.Equal(HttpStatusCode.Conflict, atTheBound?.StatusCode);
        Assert.True(deleted?.StatusCode == HttpStatusCode.NoContent, $"{deleted?.StatusCode} {deleted?.Body}");
        Assert.Equal(HttpStatusCode.Created, afterDeletion?.StatusCode);
    }

    /// <summary>
    /// AC9, AC12, Q1 (a), AD-18, NFR-5 - a deletion with key K answers <c>204</c>; the same request replayed with K never
    /// applies twice and never answers <c>500</c>: the Space's scope is gone, so the replay is the boundary <c>404</c>,
    /// byte for byte the answer for a Space that does not exist. No idempotency row survives for the Space.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - DELETE /api/v1/spaces/{spaceId} is not mapped yet, and Q1's route marker does not exist (story 3.3 Task 4)")]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "NFR-5")]
    public async Task A_replayed_deletion_with_the_same_key_is_the_boundary_404_and_never_500()
    {
        var database = Available();
        var key = Guid.NewGuid().ToString("D");
        Actor? owner = null;
        WireResponse? first = null;
        WireResponse? replay = null;
        WireResponse? absent = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await TaskWire.RegisterAsync(client, database);

            first = await DeleteSpaceOverWireAsync(owner.Session, owner.SpaceId, key);
            replay = await DeleteSpaceOverWireAsync(owner.Session, owner.SpaceId, key);
            absent = await DeleteSpaceOverWireAsync(owner.Session, Guid.NewGuid(), key);
        });

        Assert.True(first?.StatusCode == HttpStatusCode.NoContent, $"{first?.StatusCode} {first?.Body}");
        Assert.True(replay?.StatusCode == HttpStatusCode.NotFound, $"The replay answered {replay?.StatusCode}: {replay?.Body}");
        Assert.Equal(absent!.Body, replay!.Body);
        Assert.Equal(absent.HeaderNames, replay.HeaderNames);
        Assert.Equal(0, (await SpaceRowCountsAsync(database, owner!.SpaceId)).Tables["IdempotencyRecord"]);
    }

    /// <summary>
    /// AC9, AD-18 - a malformed <c>Idempotency-Key</c> (one character past <see cref="Idempotency.MaxKeyLength"/>) on the
    /// deletion is <c>400 invalid-idempotency-key</c> and deletes nothing; anchored on the same request with a valid key
    /// being admitted afterwards.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - DELETE /api/v1/spaces/{spaceId} is not mapped yet (story 3.3 Task 4)")]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "AD-18")]
    public async Task A_malformed_key_on_the_deletion_is_refused_400_and_deletes_nothing()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? malformed = null;
        var namesAfterRefusal = 0;
        WireResponse? anchor = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await TaskWire.RegisterAsync(client, database);

            malformed = await DeleteSpaceOverWireAsync(owner.Session, owner.SpaceId, new string('k', Idempotency.MaxKeyLength + 1));
            namesAfterRefusal = (await SpaceNamesOfAsync(database, owner.SpaceId)).Count;
            anchor = await DeleteSpaceOverWireAsync(owner.Session, owner.SpaceId, Guid.NewGuid().ToString("D"));
        });

        Assert.Equal(HttpStatusCode.BadRequest, malformed?.StatusCode);
        Assert.Equal(ProblemTypes.InvalidIdempotencyKey, ProblemType(malformed!));
        Assert.Equal(1, namesAfterRefusal);
        Assert.Equal(HttpStatusCode.NoContent, anchor?.StatusCode);
    }

    /// <summary>
    /// AC2, AC9, Q2 (a), §6.2 - the Owner's counts read answers exactly <c>{projects, tasks, otherMembers}</c>, counted live
    /// (two Projects, one Task per Status plus one, two other Members) and naming nothing: no Space, Project, Task or
    /// Label name is in the body.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2, AC9 - the counts read (Q2 (a), GET /api/v1/spaces/{spaceId}/deletion) is not mapped yet (story 3.3 Tasks 4, 5)")]
    [Trait("Requirement", "FR-7")]
    [Trait("Requirement", "UX-DR37")]
    public async Task The_counts_read_states_Projects_Tasks_and_other_Members_and_names_nothing()
    {
        var database = Available();
        var spaceName = "Counted " + Suffix();
        PopulatedSpace? seeded = null;
        WireResponse? summary = null;
        var statusCount = 0;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            seeded = await PopulateSpaceAsync(client, database, spaceName);
            statusCount = (await TaskWire.StatusesAsync(database, seeded.SpaceId)).Count;
            summary = await seeded.Owner.Session.GetAsync(DeletionSummaryOf(seeded.SpaceId));
        });

        Assert.True(summary?.StatusCode == HttpStatusCode.OK, $"{summary?.StatusCode} {summary?.Body}");

        using var body = JsonDocument.Parse(summary!.Body);
        var root = body.RootElement;

        Assert.Equal(DeletionSummaryProperties, root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(2, root.GetProperty("projects").GetInt32());
        Assert.Equal(statusCount + 1, root.GetProperty("tasks").GetInt32());
        Assert.Equal(2, root.GetProperty("otherMembers").GetInt32());

        foreach (var name in seeded!.Names.Append(spaceName))
        {
            Assert.DoesNotContain(name, summary.Body, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC9, Q2 (a), AD-3 - the counts read is the Owner's alone: an Admin gets <c>403 insufficient-role</c> with
    /// <c>delete-space</c> and an <c>InsufficientRole</c> record; a non-member gets the boundary <c>404</c> byte-identical to
    /// an absent Space's. Anchored on the Owner's own <c>200</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - the counts read (Q2 (a)) is not mapped yet (story 3.3 Task 4)")]
    [Trait("Requirement", "AR-11")]
    [Trait("Requirement", "AD-3")]
    public async Task The_counts_read_is_the_Owners_alone()
    {
        var database = Available();
        PopulatedSpace? seeded = null;
        WireResponse? asOwner = null;
        WireResponse? asAdmin = null;
        WireResponse? asStranger = null;
        WireResponse? absent = null;
        Actor? stranger = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            seeded = await PopulateSpaceAsync(client, database, "Counted " + Suffix());
            stranger = await TaskWire.RegisterAsync(client, database);

            asOwner = await seeded.Owner.Session.GetAsync(DeletionSummaryOf(seeded.SpaceId));
            asAdmin = await seeded.Admin.Session.GetAsync(DeletionSummaryOf(seeded.SpaceId));
            asStranger = await stranger.Session.GetAsync(DeletionSummaryOf(seeded.SpaceId));
            absent = await stranger.Session.GetAsync(DeletionSummaryOf(Guid.NewGuid()));
        });

        Assert.Equal(HttpStatusCode.OK, asOwner?.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, asAdmin?.StatusCode);
        Assert.Equal(DeleteSpaceCapabilityCode, JsonSerializer.Deserialize<ProblemResponse>(asAdmin!.Body, JsonOptions)?.Capability);
        Assert.Contains(await RefusalsOfAsync(database, seeded!.Admin.AccountId), row => row.TargetSpaceId == seeded.SpaceId && row.Kind == "InsufficientRole");

        Assert.Equal(HttpStatusCode.NotFound, asStranger?.StatusCode);
        Assert.Equal(absent!.Body, asStranger!.Body);
        Assert.Equal(absent.HeaderNames, asStranger.HeaderNames);
        Assert.Contains(await RefusalsOfAsync(database, stranger!.AccountId), row => row.TargetSpaceId == seeded.SpaceId && row.Kind == "CrossSpace");
    }

    /// <summary>
    /// AC9, AR-34, I-17 - no Space name reaches the Host's log on a deletion, its counts read, an Admin's refused deletion
    /// or a stranger's. Anchored on the log having been captured and on each answer being the one expected.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - DELETE /api/v1/spaces/{spaceId} and the counts read are not mapped yet (story 3.3 Task 4; SpaceLog.SpaceDeleted)")]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "NFR-6")]
    public async Task No_Space_name_reaches_the_log_on_deletion_or_its_refusals()
    {
        var database = Available();
        var spaceName = "Logged nowhere " + Suffix();
        var statuses = new List<HttpStatusCode>();

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var seeded = await PopulateSpaceAsync(client, database, spaceName);
            var stranger = await TaskWire.RegisterAsync(client, database);

            statuses.Add((await seeded.Owner.Session.GetAsync(DeletionSummaryOf(seeded.SpaceId))).StatusCode);
            statuses.Add((await DeleteSpaceOverWireAsync(seeded.Admin.Session, seeded.SpaceId, Guid.NewGuid().ToString("D"))).StatusCode);
            statuses.Add((await DeleteSpaceOverWireAsync(stranger.Session, seeded.SpaceId, Guid.NewGuid().ToString("D"))).StatusCode);
            statuses.Add((await DeleteSpaceOverWireAsync(seeded.Owner.Session, seeded.SpaceId, Guid.NewGuid().ToString("D"))).StatusCode);
        });

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.NoContent], statuses);
        Assert.Contains(HostProcess.ListeningMarker, log, StringComparison.Ordinal);
        Assert.DoesNotContain(spaceName, log, StringComparison.Ordinal);
    }

    /// <summary>
    /// The deletion as the browser sends it: the Session's cookie and anti-forgery header, no body (Q3 (a)), and an
    /// <c>Idempotency-Key</c> when one is given.
    /// </summary>
    private static Task<WireResponse> DeleteSpaceOverWireAsync(WireSession session, Guid spaceId, string? key) =>
        session.SendWithHeadersAsync(HttpMethod.Delete, SpaceRoutes.ActiveOf(spaceId), content: null, KeyHeaders(key));

    /// <summary>
    /// Q2 (a)'s counts read for one Space. <b>Assumed path</b> - retarget to the <c>SpaceRoutes</c> constant the dev phase
    /// adds (story 3.3 Task 5), and record the retarget if the path differs.
    /// </summary>
    private static string DeletionSummaryOf(Guid spaceId) => SpaceRoutes.ActiveOf(spaceId) + "/deletion";

    /// <summary>
    /// A Space the Owner creates through the product, populated as AC10 asks: two Projects (the second created with an
    /// <c>Idempotency-Key</c>, so an <c>IdempotencyRecord</c> row exists), one Task moved onto each of the Space's Statuses
    /// and one more, a Label applied to every Task in the first Project, a Member and an Admin, Tokens bound to it for the
    /// Owner and the Member, a Token for the Owner's Personal Space, and presence rows for the Owner and the Member.
    /// </summary>
    private static async Task<PopulatedSpace> PopulateSpaceAsync(HttpClient client, RegistrationDatabase database, string name)
    {
        var owner = await TaskWire.RegisterAsync(client, database);
        var member = await TaskWire.RegisterAsync(client, database);
        var admin = await TaskWire.RegisterAsync(client, database);

        var spaceId = CreatedSpace(await CreateSpaceAsync(owner.Session, name, Guid.NewGuid().ToString("D"))).Id;
        var inSpace = owner with { SpaceId = spaceId };

        await TaskWire.AddMemberAsync(database, spaceId, member.AccountId, "Member");
        await TaskWire.AddMemberAsync(database, spaceId, admin.AccountId, "Admin");

        var (atlas, beacon, urgent) = ("Atlas " + Suffix(), "Beacon " + Suffix(), "Urgent " + Suffix());
        var first = await TaskWire.CreateProjectAsync(inSpace, atlas, spaceId);
        var second = TaskWire.IdOf(await owner.Session.SendWithHeadersAsync(
            HttpMethod.Post,
            ProjectRoutes.ListIn(spaceId),
            JsonContent.Create(new { name = beacon }, options: JsonOptions),
            KeyHeaders(Guid.NewGuid().ToString("D"))));
        var label = await LabelWire.SeedAsync(inSpace, spaceId, urgent);

        foreach (var status in await TaskWire.StatusesAsync(database, spaceId))
        {
            var taskId = TaskWire.IdOf(await TaskWire.CreateTaskAsync(inSpace, spaceId, first, "On " + status.Name));

            Assert.Equal(HttpStatusCode.OK, (await TaskEditWire.EditTaskAsync(inSpace, spaceId, taskId, TaskEditWire.Body(new { statusId = status.Id }))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await TaskEditWire.EditTaskAsync(inSpace, spaceId, taskId, LabelWire.ApplyBody(label))).StatusCode);
        }

        _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(inSpace, spaceId, second, "In Beacon"));

        var ownerToken = await TaskWire.IssueTokenAsync(owner, spaceId);
        var memberToken = await TaskWire.IssueTokenAsync(member, spaceId);
        var ownerKeptToken = await TaskWire.IssueTokenAsync(owner, owner.SpaceId);

        // The Member's first request in the Space writes its presence row; the Owner's writes were its first.
        Assert.Equal(HttpStatusCode.OK, (await member.Session.GetAsync(SpaceRoutes.ActiveOf(spaceId))).StatusCode);

        return new PopulatedSpace(owner, member, admin, spaceId, ownerToken, memberToken, ownerKeptToken, [atlas, beacon, urgent]);
    }

    /// <summary>
    /// Every row the deletion is about, for one Space: each Space-scoped table under the Space's own key (the FILTER
    /// predicate shows its own rows to its own key, so a row hidden by the policy is never mistaken for one deleted),
    /// <c>ApiToken</c> by <c>BoundSpaceId</c>, the <c>Space</c> row, and the <c>AccessRefusal</c> rows naming it - the last
    /// three on a connection with no key, since none of them carries a policy.
    /// </summary>
    private static async Task<SpaceRowCounts> SpaceRowCountsAsync(RegistrationDatabase database, Guid spaceId)
    {
        var tables = new Dictionary<string, int>(StringComparer.Ordinal);

        await using (var scoped = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken))
        {
            foreach (var table in SpaceScopedTables)
            {
                tables[table] = await CountAsync(scoped, $"SELECT COUNT(*) FROM dbo.[{table}] WHERE SpaceId = @space", spaceId);
            }
        }

        await using var unscoped = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);

        return new SpaceRowCounts(
            tables,
            await CountAsync(unscoped, "SELECT COUNT(*) FROM dbo.[ApiToken] WHERE BoundSpaceId = @space", spaceId),
            await CountAsync(unscoped, "SELECT COUNT(*) FROM dbo.[Space] WHERE Id = @space", spaceId),
            await CountAsync(unscoped, "SELECT COUNT(*) FROM dbo.[AccessRefusal] WHERE TargetSpaceId = @space", spaceId));
    }

    private static async Task<int> CountAsync(SqlConnection connection, string sql, Guid spaceId)
    {
        await using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Review SQL queries - table names come from the constant list above; the value is a parameter.
        command.CommandText = sql;
#pragma warning restore CA2100
        command.Parameters.Add(new SqlParameter("@space", spaceId));

        return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The product's deletion statement shape (story 3.3 Task 3: one <c>DELETE</c> of the resolved Space under its own
    /// key, the cascades doing the rest), committed or rolled back. Rolled back, it is S-1's negative anchor; committed, it
    /// is the competing writer of a race - never cleanup.
    /// </summary>
    private static async Task DeleteSpaceInSqlAsync(RegistrationDatabase database, Guid spaceId, bool commit)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using System.Data.Common.DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = "DELETE FROM dbo.[Space] WHERE Id = @space";
        command.Parameters.Add(new SqlParameter("@space", spaceId));

        Assert.Equal(1, await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

        if (commit)
        {
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// How many <c>Account</c> rows carry this id - 1 while the Account persists. <c>Account</c> carries no policy.
    /// </summary>
    private static async Task<int> AccountRowsAsync(RegistrationDatabase database, Guid accountId)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);

        return await CountAsync(connection, "SELECT COUNT(*) FROM dbo.[Account] WHERE Id = @space", accountId);
    }

    /// <summary>
    /// A Space populated by <see cref="PopulateSpaceAsync"/>.
    /// </summary>
    /// <param name="Owner">Its Owner, whose own <see cref="Actor.SpaceId"/> is its Personal Space.</param>
    /// <param name="Member">A Member, registered through the product.</param>
    /// <param name="Admin">An Admin, registered through the product.</param>
    /// <param name="SpaceId">The populated Space.</param>
    /// <param name="OwnerToken">The Owner's Token bound to it.</param>
    /// <param name="MemberToken">The Member's Token bound to it.</param>
    /// <param name="OwnerKeptToken">The Owner's Token bound to its Personal Space.</param>
    /// <param name="Names">The Project and Label names it holds.</param>
    private sealed record PopulatedSpace(
        Actor Owner,
        Actor Member,
        Actor Admin,
        Guid SpaceId,
        string OwnerToken,
        string MemberToken,
        string OwnerKeptToken,
        IReadOnlyList<string> Names);

    /// <summary>
    /// One Space's rows, as counts.
    /// </summary>
    /// <param name="Tables">Each Space-scoped table's count for it.</param>
    /// <param name="Tokens">Its <c>ApiToken</c> rows.</param>
    /// <param name="SpaceRow">Its <c>Space</c> row: 1 or 0.</param>
    /// <param name="Refusals">The <c>AccessRefusal</c> rows naming it.</param>
    private sealed record SpaceRowCounts(IReadOnlyDictionary<string, int> Tables, int Tokens, int SpaceRow, int Refusals)
    {
        /// <summary>
        /// Every count as one line, so two snapshots compare by value.
        /// </summary>
        public string Flattened =>
            string.Join(' ', Tables.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => $"{entry.Key}={entry.Value}"))
            + $" ApiToken={Tokens} Space={SpaceRow} AccessRefusal={Refusals}";
    }
}
