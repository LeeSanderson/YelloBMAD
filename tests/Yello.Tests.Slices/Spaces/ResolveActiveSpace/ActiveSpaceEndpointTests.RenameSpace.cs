using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Spaces;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 3.1 over the wire: renaming the active Space through <c>PATCH /api/v1/spaces/{spaceId}</c>, an ordinary
/// Space-scoped write the pipeline authorises by <c>Capability.RenameSpace</c>. AC3, AC9, AC10, AC14 (server half);
/// FR-6, AR-10, AR-11, AR-34, CAP-35, I-17.
/// </summary>
/// <remarks>
/// <b>RED-PHASE SCAFFOLDS (story 3.1 ATDD run, 2026-10-29).</b> All skipped: PATCH on <c>SpaceRoutes.Active</c> is not
/// mapped, so every request below answers the unmatched fallback today. Seeded Spaces go through
/// <see cref="SpaceSeeding"/> (one B2 block each: the <c>Space</c> and its one <c>Membership</c> under that Space's
/// key, for an Account the product registered - <c>FK_Membership_Space_SpaceId</c>,
/// <c>FK_Membership_Account_AccountId</c> and the <c>BLOCK AFTER INSERT</c> predicate hold). A Role change is the
/// same block attaching the tracked Membership, as <c>A_Token_acts_with_the_current_Role_read_on_each_request</c>
/// does.
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    private const string RenameSpaceCapabilityCode = "rename-space";

    /// <summary>
    /// AC3, AC9, FR-6 - the Owner renames: <c>200</c> with exactly <c>{id, name, role}</c>, the name trimmed and the
    /// caller's Role; the name is persisted and the switcher lists it. The rename writes the resolved Space only: a
    /// second Space the same Owner holds keeps its name.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 - PATCH /api/v1/spaces/{spaceId} is not mapped yet (story 3.1 Task 5)")]
    [Trait("Requirement", "FR-6")]
    public async Task The_Owner_renames_the_Space_and_only_that_Space_changes()
    {
        var database = Available();
        var address = RandomAddress();
        var (renamed, untouched) = (Guid.NewGuid(), Guid.NewGuid());
        var (oldName, newName, otherName) = ("Before " + Suffix(), "After " + Suffix(), "Other " + Suffix());
        WireResponse? answer = null;
        WireResponse? switcher = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            var accountId = await AccountIdOfAsync(database, address);

            await SeedNamedSpaceAsync(database, renamed, oldName, accountId, Role.Owner);
            await SeedNamedSpaceAsync(database, untouched, otherName, accountId, Role.Owner);

            await session.FetchAntiforgeryTokenAsync();
            answer = await RenameSpaceAsync(session, renamed, "  " + newName + "  ");
            switcher = await session.GetAsync(SpaceRoutes.Directory);
        });

        Assert.True(answer?.StatusCode == HttpStatusCode.OK, $"{answer?.StatusCode} {answer?.Body}");

        var (properties, id, name, role) = ActiveSpaceAnswer(answer!);

        Assert.Equal(ActiveSpaceProperties, properties);
        Assert.Equal((renamed, newName, "owner"), (id, name, role));
        Assert.Equal([newName], await SpaceNamesOfAsync(database, renamed));
        Assert.Equal([otherName], await SpaceNamesOfAsync(database, untouched));
        Assert.Contains(IdentityRows(switcher!), row => row.Id == renamed && row.Name == newName);
        Assert.DoesNotContain(IdentityRows(switcher!), row => row.Name == oldName);
    }

    /// <summary>
    /// AC3, AC10 - an Admin renames: <c>200</c>, answered at the Admin's Role.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 - PATCH /api/v1/spaces/{spaceId} is not mapped yet (story 3.1 Task 5)")]
    [Trait("Requirement", "FR-6")]
    [Trait("Requirement", "FR-16")]
    public async Task An_Admin_renames_the_Space()
    {
        var database = Available();
        var address = RandomAddress();
        var spaceId = Guid.NewGuid();
        var newName = "Admin named " + Suffix();
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedNamedSpaceAsync(database, spaceId, "Before " + Suffix(), await AccountIdOfAsync(database, address), Role.Admin);

            await session.FetchAntiforgeryTokenAsync();
            answer = await RenameSpaceAsync(session, spaceId, newName);
        });

        Assert.True(answer?.StatusCode == HttpStatusCode.OK, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal((spaceId, newName, "admin"), (ActiveSpaceAnswer(answer!).Id, ActiveSpaceAnswer(answer!).Name, ActiveSpaceAnswer(answer!).Role));
        Assert.Equal([newName], await SpaceNamesOfAsync(database, spaceId));
    }

    /// <summary>
    /// AC3, AC10, FR-16, AR-11 - a Member or a Viewer is refused <c>403 insufficient-role</c> carrying
    /// <c>capability: rename-space</c>, the pipeline records one <c>InsufficientRole</c> refusal for the Space, and the
    /// name is unchanged. Anchor: the same caller reads the Space first (<c>200</c>), so the <c>403</c> is the
    /// capability's and not the boundary's.
    /// </summary>
    /// <param name="role">The caller's Role.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC10 - PATCH /api/v1/spaces/{spaceId} is not mapped yet (story 3.1 Task 5)")]
    [InlineData(Role.Member)]
    [InlineData(Role.Viewer)]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "AR-11")]
    public async Task A_Member_or_a_Viewer_is_refused_403_rename_space_and_the_name_is_unchanged(Role role)
    {
        var database = Available();
        var address = RandomAddress();
        var spaceId = Guid.NewGuid();
        var oldName = "Kept " + Suffix();
        var accountId = Guid.Empty;
        WireResponse? read = null;
        WireResponse? refused = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            accountId = await AccountIdOfAsync(database, address);
            await SeedNamedSpaceAsync(database, spaceId, oldName, accountId, role);

            read = await session.GetAsync(SpaceRoutes.ActiveOf(spaceId));

            await session.FetchAntiforgeryTokenAsync();
            refused = await RenameSpaceAsync(session, spaceId, "Not allowed " + Suffix());
        });

        Assert.Equal(HttpStatusCode.OK, read?.StatusCode);
        Assert.True(refused?.StatusCode == HttpStatusCode.Forbidden, $"{refused?.StatusCode} {refused?.Body}");

        var problem = JsonSerializer.Deserialize<ProblemResponse>(refused!.Body, JsonOptions);

        Assert.Equal(ProblemTypes.InsufficientRole, problem?.Type);
        Assert.Equal(RenameSpaceCapabilityCode, problem?.Capability);
        Assert.DoesNotContain(oldName, refused.Body, StringComparison.Ordinal);

        var recorded = Assert.Single(await RefusalsOfAsync(database, accountId));

        Assert.Equal((spaceId, "InsufficientRole", nameof(Capability.RenameSpace)), (recorded.TargetSpaceId, recorded.Kind, recorded.Capability));
        Assert.Equal([oldName], await SpaceNamesOfAsync(database, spaceId));
    }

    /// <summary>
    /// AC10, AD-3, AR-10 - a non-member's rename is the boundary <c>404 not-available</c>, byte for byte the answer
    /// for a Space that does not exist, with one <c>CrossSpace</c> refusal; the foreign Space keeps its name, and the
    /// body names nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC10 - PATCH /api/v1/spaces/{spaceId} is not mapped yet (story 3.1 Task 5)")]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AR-10")]
    public async Task A_non_members_rename_is_the_boundary_404_and_the_foreign_name_is_unchanged()
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
            refused = await RenameSpaceAsync(session, foreign, "Taken over");
            absent = await RenameSpaceAsync(session, Guid.NewGuid(), "Taken over");
        });

        Assert.Equal(HttpStatusCode.NotFound, refused?.StatusCode);
        Assert.Equal(ProblemTypes.NotAvailable, ProblemType(refused!));
        Assert.Equal(absent!.Body, refused!.Body);
        Assert.Equal(absent.HeaderNames, refused.HeaderNames);
        Assert.DoesNotContain(foreignName, refused.Body, StringComparison.Ordinal);

        Assert.Contains(await RefusalsOfAsync(database, accountId), row => row.TargetSpaceId == foreign && row.Kind == "CrossSpace");
        Assert.Equal([foreignName], await SpaceNamesOfAsync(database, foreign));
    }

    /// <summary>
    /// AC9, Q6 (a) assumed - an empty, blank or 201-character name is refused <c>400 invalid-space-name</c> and the
    /// name is unchanged. Anchor: 200 characters are accepted.
    /// </summary>
    /// <param name="length">The submitted name's length.</param>
    /// <param name="blank">Whether it is spaces only.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC9 - PATCH /api/v1/spaces/{spaceId} and SpaceName.TryNormalise do not exist yet (story 3.1 Tasks 3, 5)")]
    [InlineData(0, false)]
    [InlineData(3, true)]
    [InlineData(SpaceNameMaxLength + 1, false)]
    [Trait("Priority", "P1")]
    public async Task An_invalid_rename_is_refused_400_and_the_name_is_unchanged(int length, bool blank)
    {
        var database = Available();
        var address = RandomAddress();
        var spaceId = Guid.NewGuid();
        var oldName = "Kept " + Suffix();
        var limitName = new string('r', SpaceNameMaxLength);
        WireResponse? refused = null;
        WireResponse? afterRefusal = null;
        WireResponse? anchor = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedNamedSpaceAsync(database, spaceId, oldName, await AccountIdOfAsync(database, address), Role.Owner);

            await session.FetchAntiforgeryTokenAsync();
            refused = await RenameSpaceAsync(session, spaceId, new string(blank ? ' ' : 'r', length));
            afterRefusal = await session.GetAsync(SpaceRoutes.ActiveOf(spaceId));
            anchor = await RenameSpaceAsync(session, spaceId, limitName);
        });

        Assert.Equal(HttpStatusCode.BadRequest, refused?.StatusCode);
        Assert.Equal(InvalidSpaceNameType, ProblemType(refused!));
        Assert.Equal(oldName, ActiveSpaceAnswer(afterRefusal!).Name);
        Assert.Equal(HttpStatusCode.OK, anchor?.StatusCode);
        Assert.Equal([limitName], await SpaceNamesOfAsync(database, spaceId));
    }

    /// <summary>
    /// AC10, CAP-35, FR-36 - an API Token reaches rename for its own Space, at the Token's current Role: the Admin's
    /// Token renames (<c>200</c>, the anchor); demoted to Viewer, the same Token is refused <c>403 rename-space</c> on
    /// the very next request and the name stays.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC10 - PATCH /api/v1/spaces/{spaceId} is not mapped yet (story 3.1 Task 5)")]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "AD-6")]
    public async Task A_Token_renames_its_own_Space_at_its_current_Role()
    {
        var database = Available();
        var address = RandomAddress();
        var spaceId = Guid.NewGuid();
        var (admitted, refusedName) = ("By Token " + Suffix(), "By Viewer Token " + Suffix());
        WireResponse? asAdmin = null;
        WireResponse? asViewer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            var membership = SpaceSeeding.NewMembership(spaceId, await AccountIdOfAsync(database, address), Role.Admin);

            await SpaceSeeding.SeedAsync(database, spaceId, context =>
            {
                context.Spaces.Add(NamedSpace(spaceId, "Before " + Suffix()));
                context.Memberships.Add(membership);
            }, TestContext.Current.CancellationToken);

            var token = IssuedToken(await IssueAsync(session, spaceId, "rename script")).Plaintext;

            asAdmin = await session.SendWithTokenAndHeadersAsync(HttpMethod.Patch, SpaceRoutes.ActiveOf(spaceId), token, SpaceBody(admitted), NoKey);

            await SpaceSeeding.SeedAsync(database, spaceId, context =>
            {
                context.Memberships.Attach(membership);
                membership.Role = Role.Viewer;
            }, TestContext.Current.CancellationToken);

            asViewer = await session.SendWithTokenAndHeadersAsync(HttpMethod.Patch, SpaceRoutes.ActiveOf(spaceId), token, SpaceBody(refusedName), NoKey);
        });

        Assert.True(asAdmin?.StatusCode == HttpStatusCode.OK, $"Anchor: {asAdmin?.StatusCode} {asAdmin?.Body}");
        Assert.Equal(HttpStatusCode.Forbidden, asViewer?.StatusCode);
        Assert.Equal(RenameSpaceCapabilityCode, JsonSerializer.Deserialize<ProblemResponse>(asViewer!.Body, JsonOptions)?.Capability);
        Assert.Equal([admitted], await SpaceNamesOfAsync(database, spaceId));
    }

    /// <summary>
    /// AC14, AR-34, I-17 - no Space name reaches the Host's log: neither the created name, nor a rename's old or new
    /// name, nor a refused rename's. Anchored on the log having been captured (the listening line) and on each
    /// request having been served or refused as expected.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC14 - POST and PATCH on /api/v1/spaces are not mapped yet (story 3.1 Tasks 4, 5)")]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "NFR-6")]
    public async Task No_Space_name_reaches_the_log_on_creation_or_rename()
    {
        var database = Available();
        var address = RandomAddress();
        var (created, renamedTo, refusedTo) = ("Logged nowhere " + Suffix(), "Renamed nowhere " + Suffix(), "Refused nowhere " + Suffix());
        var viewerSpace = Guid.NewGuid();
        var viewerSpaceName = "Viewer's " + Suffix();
        var statuses = new List<HttpStatusCode>();

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedNamedSpaceAsync(database, viewerSpace, viewerSpaceName, await AccountIdOfAsync(database, address), Role.Viewer);
            await session.FetchAntiforgeryTokenAsync();

            var answer = await CreateSpaceAsync(session, created);
            statuses.Add(answer.StatusCode);

            statuses.Add((await RenameSpaceAsync(session, CreatedSpace(answer).Id, renamedTo)).StatusCode);
            statuses.Add((await RenameSpaceAsync(session, viewerSpace, refusedTo)).StatusCode);
        });

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.Forbidden], statuses);
        Assert.Contains(HostProcess.ListeningMarker, log, StringComparison.Ordinal);

        foreach (var name in new[] { created, renamedTo, refusedTo, viewerSpaceName })
        {
            Assert.DoesNotContain(name, log, StringComparison.Ordinal);
        }
    }

    private static Task<WireResponse> RenameSpaceAsync(WireSession session, Guid spaceId, string name) =>
        session.SendWithHeadersAsync(HttpMethod.Patch, SpaceRoutes.ActiveOf(spaceId), JsonContent.Create(new { name }, options: JsonOptions), NoKey);
}
