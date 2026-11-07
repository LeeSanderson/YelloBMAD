using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Invitations;
using Yello.Contracts.Labels;
using Yello.Contracts.Projects;
using Yello.Contracts.Spaces;
using Yello.Contracts.Tasks;
using Yello.Contracts.Memberships;
using Yello.Domain.Accounts;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using LabelWire = Yello.Tests.Slices.Tasks.LabelWire;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 4.4 over the wire: changing a Membership's Role, <c>PATCH /api/v1/spaces/{spaceId}/memberships/{membershipId}</c>
/// with <c>{ role }</c>, under <c>Capability.ChangeMembershipRole</c>, answered <c>200</c> with the updated row (Q9 (a)).
/// The Admin's Qualified cell is enforced by the pipeline-owned acting authority (Lee's Q1 (a)): the Role sets the actor may
/// change from and to, Owner in neither, composed into one guarded <c>UPDATE</c> with the actor-still-holds-its-Role clause.
/// AC4, AC5, AC6, AC11; FR-13, AR-12/AD-5, AR-16/AD-9, NFR-2, V-1, S-2, AD-18, AD-20.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 4.4 ATDD run (2026-11-02).</b> Every case is skipped with the AC it waits
/// for, except <see cref="A_second_Owner_fails_2601_on_the_filtered_index_while_an_Admin_update_of_the_same_row_succeeds"/>,
/// which proves the schema half of AC5 that already holds (left active as a standing gate). The route and the two new problem
/// types are <see cref="MembershipWire"/>'s ATDD stand-ins until Task 3 declares them.
/// </para>
/// <para>
/// <b>Every refusal is anchored</b> on a <c>200</c> from the same route in the same case (the 4.2/4.3 trap: today any path
/// under <c>/memberships</c> is the boundary <c>404</c>), and every failure message states the answers it compared.
/// </para>
/// <para>
/// <b>Seeding.</b> Targets and actors whose Role is the point join through the product (an Invitation at that Role,
/// accepted; <see cref="JoinedThroughTheProductAsync"/>). The <c>MembershipChanged</c> cases (AC6) seed through
/// <see cref="WireDatabase.AddMembershipAsync"/> instead, and say so: under Q18 (a) the accept publishes a
/// <c>MembershipChanged</c> of its own for the new Member, which would bury the count the case is about.
/// </para>
/// <para>
/// <b>The S-2 invariant</b> (<see cref="AssertOneOwnerEachAsync"/>) runs after every case that changes or attempts a Role,
/// over the case's own Spaces (see <see cref="WireDatabase.SpacesNotHoldingExactlyOneOwnerAsync"/> for why not
/// database-wide).
/// </para>
/// <para>
/// <b>Not here:</b> the non-member's boundary <c>404</c> and the Member/Viewer refusals on the Token surface (the isolation
/// rows <c>ISO-CHANGE-ROLE-FOREIGN-SPACE</c> and <c>ISO-RECORD-KIND-CHANGE-ROLE-*</c>, which run both surfaces), the races
/// (<c>.ChangeMembershipRoleRace.cs</c>), and the product-route revocation twin (<c>REV-ROLE-CHANGED-BY-OWNER</c>).
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    private static readonly string[] OwnRowTargets = ["admin", "member", "viewer"];

    private static readonly string[] UnassignableCodes = ["owner", "OWNER", "superuser"];

    /// <summary>
    /// The Owner's changes, by (from, to): the matrix's Allowed cell, every non-Owner Role to every other.
    /// </summary>
    public static TheoryData<string, string> OwnerChanges => new()
    {
        { "member", "admin" },
        { "admin", "viewer" },
        { "viewer", "member" },
        { "admin", "member" },
    };

    /// <summary>
    /// The Admin's in-authority changes (FR-13: "Member and Viewer only").
    /// </summary>
    public static TheoryData<string, string> AdminChanges => new()
    {
        { "member", "viewer" },
        { "viewer", "member" },
    };

    /// <summary>
    /// The Admin's out-of-authority attempts, each by name (see <see cref="An_Admin_beyond_its_authority_is_refused_403_recorded_and_nothing_changes"/>).
    /// </summary>
    public static TheoryData<string> AdminBeyondAuthority => new()
    {
        "member-to-admin",
        "other-admin-to-member",
        "own-row-to-member",
        "owner-row-to-viewer",
    };

    /// <summary>
    /// AC4, FR-13, the matrix's Owner cell - the Owner promotes a Member to Admin, demotes an Admin to Viewer or to Member, and
    /// moves a Viewer to Member: <c>200</c> with the updated row (its id, the target's name and address, the new Role's code,
    /// <c>isYou: false</c>), the stored Role changed, and exactly one Owner left.
    /// </summary>
    /// <param name="from">The target's Role before.</param>
    /// <param name="to">The Role sent.</param>
    /// <returns>A task.</returns>
    [Theory]
    [MemberData(nameof(OwnerChanges))]
    [Trait("Requirement", "FR-13")]
    [Trait("Requirement", "AR-12")]
    public async Task The_Owner_changes_a_non_Owner_Role_and_is_answered_the_updated_row(string from, string to)
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, targetAddress) = (RandomAddress(), RandomAddress());
        var targetName = DisplayName();
        var spaceId = Guid.NewGuid();
        MembershipRow? target = null;
        WireResponse? changed = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Owner changes " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), targetAddress, targetName, from);
            target = await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, targetAddress));

            await owner.FetchAntiforgeryTokenAsync();
            changed = await ChangeRoleOverWireAsync(owner, spaceId, target.Id, to);
        });

        Assert.True(changed?.StatusCode == HttpStatusCode.OK, $"The Owner's {from} -> {to}: {Described(changed)}");
        Assert.Equal(new ChangedRow(target!.Id, targetName, to, IsYou: false), MembershipWire.ParseRow(changed.Body));
        Assert.DoesNotContain(targetAddress, changed.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(StoredRole(to), (await wire.MembershipOfAsync(spaceId, target.AccountId)).Role);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// AC4, FR-13 ("an Admin can change Memberships between Member and Viewer only") - an Admin, joined through the product,
    /// moves a Member to Viewer and a Viewer to Member: <c>200</c>, the stored Role changed, nothing recorded.
    /// </summary>
    /// <param name="from">The target's Role before.</param>
    /// <param name="to">The Role sent.</param>
    /// <returns>A task.</returns>
    [Theory]
    [MemberData(nameof(AdminChanges))]
    [Trait("Requirement", "FR-13")]
    [Trait("Assumption", "PRD-12-4")]
    public async Task An_Admin_changes_a_Member_or_a_Viewer_between_Member_and_Viewer(string from, string to)
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, adminAddress, targetAddress) = (RandomAddress(), RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        MembershipRow? target = null;
        WireResponse? changed = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Admin changes " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            var admin = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), adminAddress, DisplayName(), "admin");
            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), targetAddress, DisplayName(), from);
            target = await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, targetAddress));

            changed = await ChangeRoleOverWireAsync(admin, spaceId, target.Id, to);
        });

        Assert.True(changed?.StatusCode == HttpStatusCode.OK, $"The Admin's {from} -> {to}: {Described(changed)}");
        Assert.Equal(to, MembershipWire.ParseRow(changed.Body).Role);
        Assert.Equal(StoredRole(to), (await wire.MembershipOfAsync(spaceId, target!.AccountId)).Role);
        Assert.DoesNotContain(await RefusalsOfAsync(database, await AccountIdOfAsync(database, adminAddress)), row => row.TargetSpaceId == spaceId);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// AC4, FR-13, PRD section 12 #4, Lee's Q1 (a), AD-20 [SECURITY] - an Admin out of its authority: promoting a Member to
    /// Admin, demoting another Admin to Member, demoting itself, or touching the Owner's row. Each is <c>403
    /// insufficient-role</c> naming <c>change-membership-role</c> - the pipeline's answer, turned from the store's typed
    /// Qualified refusal - recorded once as <c>InsufficientRole</c> against this Space, with the target's stored Role
    /// unchanged and exactly one Owner. Anchor: the same Admin's Viewer to Member on the same route answers <c>200</c> first.
    /// Plants 1 (Admin's <c>To</c> gains Admin) and 8 (the refusal returned unrecorded by the endpoint).
    /// </summary>
    /// <param name="attempt">Which attempt.</param>
    /// <returns>A task.</returns>
    [Theory]
    [MemberData(nameof(AdminBeyondAuthority))]
    [Trait("Requirement", "FR-13")]
    [Trait("Requirement", "AD-20")]
    [Trait("Assumption", "PRD-12-4")]
    public async Task An_Admin_beyond_its_authority_is_refused_403_recorded_and_nothing_changes(string attempt)
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, adminAddress, otherAdminAddress, memberAddress, viewerAddress) = (RandomAddress(), RandomAddress(), RandomAddress(), RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        MembershipRow? before = null;
        WireResponse? anchor = null;
        WireResponse? refused = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            var ownerId = await AccountIdOfAsync(database, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Beyond " + Suffix(), ownerId, Role.Owner);

            var admin = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), adminAddress, DisplayName(), "admin");
            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), otherAdminAddress, DisplayName(), "admin");
            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), memberAddress, DisplayName(), "member");
            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), viewerAddress, DisplayName(), "viewer");

            var (targetAddress, role) = attempt switch
            {
                "member-to-admin" => (memberAddress, "admin"),
                "other-admin-to-member" => (otherAdminAddress, "member"),
                "own-row-to-member" => (adminAddress, "member"),
                "owner-row-to-viewer" => (ownerAddress, "viewer"),
                _ => throw new ArgumentOutOfRangeException(nameof(attempt), attempt, "Not an attempt this case knows."),
            };

            before = await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, targetAddress));

            anchor = await ChangeRoleOverWireAsync(admin, spaceId, (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, viewerAddress))).Id, "member");
            refused = await ChangeRoleOverWireAsync(admin, spaceId, before.Id, role);
        });

        Assert.True(anchor?.StatusCode == HttpStatusCode.OK, $"The Admin's in-authority anchor: {Described(anchor)}");
        Assert.True(refused?.StatusCode == HttpStatusCode.Forbidden, $"The Admin's {attempt}: {Described(refused)}; the anchor on the same route was {Described(anchor)}");
        Assert.Equal(ProblemTypes.InsufficientRole, ProblemType(refused));
        Assert.Equal(MembershipWire.CapabilityCode, CapabilityOf(refused));
        Assert.Equal(before, await wire.MembershipOfAsync(spaceId, before!.AccountId));
        Assert.Equal(
            [(nameof(Capability.ChangeMembershipRole), "InsufficientRole")],
            (await RefusalsOfAsync(database, await AccountIdOfAsync(database, adminAddress))).Where(row => row.TargetSpaceId == spaceId).Select(row => (row.Capability, row.Kind)));
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// AC4, the matrix's Member and Viewer cells - a Member and a Viewer, each joined through the product, are refused by the
    /// pipeline before any slice runs: <c>403 insufficient-role</c>, recorded <c>InsufficientRole</c>, the target unchanged.
    /// Anchor: the Owner's change on the same route answers <c>200</c> first.
    /// </summary>
    /// <param name="lesser">The actor's Role.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("member")]
    [InlineData("viewer")]
    [Trait("Requirement", "FR-13")]
    [Trait("Requirement", "AD-20")]
    public async Task A_Member_or_a_Viewer_is_refused_403_by_the_pipeline_and_recorded(string lesser)
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, actorAddress, targetAddress, anchorAddress) = (RandomAddress(), RandomAddress(), RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        MembershipRow? before = null;
        WireResponse? anchor = null;
        WireResponse? refused = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Lesser " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            var actor = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), actorAddress, DisplayName(), lesser);
            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), targetAddress, DisplayName(), "member");
            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), anchorAddress, DisplayName(), "viewer");

            before = await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, targetAddress));

            await owner.FetchAntiforgeryTokenAsync();
            anchor = await ChangeRoleOverWireAsync(owner, spaceId, (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, anchorAddress))).Id, "member");
            refused = await ChangeRoleOverWireAsync(actor, spaceId, before.Id, "viewer");
        });

        Assert.True(anchor?.StatusCode == HttpStatusCode.OK, $"The Owner's anchor: {Described(anchor)}");
        Assert.True(refused?.StatusCode == HttpStatusCode.Forbidden, $"The {lesser}'s change: {Described(refused)}; the anchor was {Described(anchor)}");
        Assert.Equal(MembershipWire.CapabilityCode, CapabilityOf(refused));
        Assert.Equal(before, await wire.MembershipOfAsync(spaceId, before!.AccountId));
        Assert.Contains(
            await RefusalsOfAsync(database, await AccountIdOfAsync(database, actorAddress)),
            row => row.TargetSpaceId == spaceId && row.Kind == "InsufficientRole" && row.Capability == nameof(Capability.ChangeMembershipRole));
    }

    /// <summary>
    /// AC4, AC5, Q10 (a), Lee's Q2 (a) - the Owner changing its own row, to anything, is <c>409 owner-role-fixed</c>:
    /// Ownership moves only by an Ownership Offer (FR-42, story 5.2). The Owner is in no authority set, so the guarded
    /// statement cannot touch the row; the stored Role stays <c>Owner</c>, nothing is recorded (the Owner holds full authority,
    /// so "insufficient Role" would be untrue), and the Space keeps exactly one Owner. Anchor: the Owner's Member to Viewer on
    /// the same route answers <c>200</c>. Plant 2: Owner added to the Owner's <c>From</c> set - the demotion succeeds and S-2
    /// goes red.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-13")]
    [Trait("Requirement", "AR-12")]
    [Trait("Requirement", "AD-5")]
    public async Task The_Owner_changing_its_own_row_is_refused_409_owner_role_fixed_and_nothing_changes()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, memberAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var ownerId = Guid.Empty;
        WireResponse? anchor = null;
        var refused = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            ownerId = await AccountIdOfAsync(database, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Own row " + Suffix(), ownerId, Role.Owner);

            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), memberAddress, DisplayName(), "member");

            await owner.FetchAntiforgeryTokenAsync();
            anchor = await ChangeRoleOverWireAsync(owner, spaceId, (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, memberAddress))).Id, "viewer");

            var own = await wire.MembershipOfAsync(spaceId, ownerId);

            foreach (var role in OwnRowTargets)
            {
                refused[role] = await ChangeRoleOverWireAsync(owner, spaceId, own.Id, role);
            }
        });

        Assert.True(anchor?.StatusCode == HttpStatusCode.OK, $"The Owner's anchor: {Described(anchor)}");

        foreach (var (role, answer) in refused)
        {
            Assert.True(answer.StatusCode == HttpStatusCode.Conflict, $"The Owner's own row to {role}: {Described(answer)}; the anchor was {Described(anchor)}");
            Assert.Equal(ProblemTypes.OwnerRoleFixed, ProblemType(answer));
        }

        Assert.Equal("Owner", (await wire.MembershipOfAsync(spaceId, ownerId)).Role);
        Assert.DoesNotContain(await RefusalsOfAsync(database, ownerId), row => row.TargetSpaceId == spaceId);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// AC4, AC5, Q10 (a) - <c>owner</c> (in any case) or a code that names no Role, sent by the Owner or an Admin, is <c>400
    /// role-not-assignable</c>: the pipeline's validation, before the store, so nothing is read, written or recorded and the
    /// target keeps its Role. Anchor: the same caller's in-authority change on the same route answers <c>200</c> afterwards,
    /// on a second target. Plant 3's sibling: an <c>owner</c> code reaching the store.
    /// </summary>
    /// <param name="caller">The caller's Role.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("owner")]
    [InlineData("admin")]
    [Trait("Requirement", "FR-13")]
    [Trait("Requirement", "AR-12")]
    public async Task Owner_or_an_unknown_code_in_the_body_is_refused_400_role_not_assignable_and_nothing_changes(string caller)
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, adminAddress, targetAddress, anchorAddress) = (RandomAddress(), RandomAddress(), RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        MembershipRow? before = null;
        WireResponse? anchor = null;
        var refused = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Not assignable " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            var admin = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), adminAddress, DisplayName(), "admin");
            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), targetAddress, DisplayName(), "member");
            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), anchorAddress, DisplayName(), "member");

            var actor = caller == "owner" ? owner : admin;

            await actor.FetchAntiforgeryTokenAsync();
            before = await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, targetAddress));

            foreach (var code in UnassignableCodes)
            {
                refused[code] = await ChangeRoleOverWireAsync(actor, spaceId, before.Id, code);
            }

            anchor = await ChangeRoleOverWireAsync(actor, spaceId, (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, anchorAddress))).Id, "viewer");
        });

        Assert.True(anchor?.StatusCode == HttpStatusCode.OK, $"The {caller}'s anchor: {Described(anchor)}");

        foreach (var (code, answer) in refused)
        {
            Assert.True(answer.StatusCode == HttpStatusCode.BadRequest, $"The {caller}'s change to '{code}': {Described(answer)}; the anchor was {Described(anchor)}");
            Assert.Equal(ProblemTypes.RoleNotAssignable, ProblemType(answer));
        }

        Assert.Equal(before, await wire.MembershipOfAsync(spaceId, before!.AccountId));
        Assert.DoesNotContain(await RefusalsOfAsync(database, await AccountIdOfAsync(database, caller == "owner" ? ownerAddress : adminAddress)), row => row.TargetSpaceId == spaceId);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// AC4, AC9, AD-3, AD-20 [SECURITY] - an id that names no Membership in this Space - one never issued, and another Space's
    /// - is the boundary <c>404 not-available</c> body, byte for byte the answer for a Space that does not exist, and is not
    /// recorded against this Space (4.2's revoke precedent). The other Space's Membership keeps its Role. Anchor: the Owner's
    /// change of its own Member on the same route answers <c>200</c>. The foreign Space is seeded directly
    /// (<see cref="WireDatabase.AddMembershipAsync"/>): the caller is no member there, and acceptance would bury the point.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-20")]
    [Trait("Requirement", "FR-13")]
    public async Task An_id_from_another_Space_or_never_issued_is_the_boundary_404_and_is_not_recorded()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, memberAddress, foreignOwnerAddress, foreignMemberAddress) = (RandomAddress(), RandomAddress(), RandomAddress(), RandomAddress());
        var (spaceId, foreignSpaceId) = (Guid.NewGuid(), Guid.NewGuid());
        var ownerId = Guid.Empty;
        MembershipRow? foreignBefore = null;
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            _ = await SignedInAsync(client, foreignOwnerAddress);
            await SeedNamedSpaceAsync(database, foreignSpaceId, "Elsewhere " + Suffix(), await AccountIdOfAsync(database, foreignOwnerAddress), Role.Owner);
            _ = await SignedInAsync(client, foreignMemberAddress);
            await wire.AddMembershipAsync(foreignSpaceId, await AccountIdOfAsync(database, foreignMemberAddress), "member");
            foreignBefore = await wire.MembershipOfAsync(foreignSpaceId, await AccountIdOfAsync(database, foreignMemberAddress));

            var owner = await SignedInAsync(client, ownerAddress);
            ownerId = await AccountIdOfAsync(database, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Not found " + Suffix(), ownerId, Role.Owner);
            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), memberAddress, DisplayName(), "member");

            await owner.FetchAntiforgeryTokenAsync();
            answers["anchor"] = await ChangeRoleOverWireAsync(owner, spaceId, (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, memberAddress))).Id, "viewer");
            answers["absent Space"] = await ChangeRoleOverWireAsync(owner, Guid.NewGuid(), Guid.NewGuid(), "viewer");
            answers["never issued"] = await ChangeRoleOverWireAsync(owner, spaceId, Guid.NewGuid(), "viewer");
            answers["another Space's"] = await ChangeRoleOverWireAsync(owner, spaceId, foreignBefore.Id, "viewer");
        });

        Assert.True(answers["anchor"].StatusCode == HttpStatusCode.OK, $"The Owner's anchor: {Described(answers["anchor"])}");
        Assert.Equal(HttpStatusCode.NotFound, answers["absent Space"].StatusCode);

        foreach (var arm in new[] { "never issued", "another Space's" })
        {
            var answer = answers[arm];

            Assert.True(answer.StatusCode == HttpStatusCode.NotFound, $"{arm} id: {Described(answer)}; the absent Space answered {Described(answers["absent Space"])}");
            Assert.Equal(ProblemTypes.NotAvailable, ProblemType(answer));
            Assert.Equal(answers["absent Space"].Body, answer.Body);
            Assert.Equal(answers["absent Space"].HeaderNames, answer.HeaderNames);
        }

        Assert.DoesNotContain(await RefusalsOfAsync(database, ownerId), row => row.TargetSpaceId == spaceId);
        Assert.Equal(foreignBefore, await wire.MembershipOfAsync(foreignSpaceId, foreignBefore!.AccountId));
        await AssertOneOwnerEachAsync(wire, spaceId, foreignSpaceId);
    }

    /// <summary>
    /// AC4, Q11 (a) - a change to the Role the target already holds is a <c>200</c> no-op: the unchanged row, nothing written,
    /// nothing recorded and no <c>MembershipChanged</c> published (no log line names the target). A double-click, or a resend
    /// under a new key, never reads as an error.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-13")]
    [Trait("Requirement", "AR-16")]
    public async Task A_same_Role_change_answers_200_with_the_unchanged_row_and_publishes_nothing()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, memberAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        MembershipRow? before = null;
        WireResponse? same = null;

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Same Role " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);
            _ = await SignedInAsync(client, memberAddress);
            await wire.AddMembershipAsync(spaceId, await AccountIdOfAsync(database, memberAddress), "member");
            before = await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, memberAddress));

            await owner.FetchAntiforgeryTokenAsync();
            same = await ChangeRoleOverWireAsync(owner, spaceId, before.Id, "member");
        });

        Assert.True(same?.StatusCode == HttpStatusCode.OK, $"The same-Role change: {Described(same)}");
        Assert.Equal("member", MembershipWire.ParseRow(same.Body).Role);
        Assert.Equal(before, await wire.MembershipOfAsync(spaceId, before!.AccountId));
        Assert.Equal(0, PublishesNaming(log, before.AccountId, spaceId));
    }

    /// <summary>
    /// AC6, AR-16/AD-9, Q7 (a) - a committed Role change publishes <c>MembershipChanged(AccountId, SpaceId)</c> exactly once,
    /// for the target's Account and this Space, observed through its one structured log line (event
    /// <see cref="MembershipWire.PublishedEvent"/>, ids only - no <c>@</c>); no other Account is named by a publish. The
    /// target is seeded directly (see the file's remarks). Plant 9: the publish before commit or on every attempt.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-16")]
    [Trait("Requirement", "AD-9")]
    public async Task A_committed_change_logs_exactly_one_MembershipChanged_publish_for_the_target_and_the_Space()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, memberAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var (ownerId, memberId) = (Guid.Empty, Guid.Empty);
        WireResponse? changed = null;

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            ownerId = await AccountIdOfAsync(database, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Published " + Suffix(), ownerId, Role.Owner);
            _ = await SignedInAsync(client, memberAddress);
            memberId = await AccountIdOfAsync(database, memberAddress);
            await wire.AddMembershipAsync(spaceId, memberId, "member");

            await owner.FetchAntiforgeryTokenAsync();
            changed = await ChangeRoleOverWireAsync(owner, spaceId, (await wire.MembershipOfAsync(spaceId, memberId)).Id, "viewer");
        });

        var publishes = log.Split('\n').Where(line => line.Contains(MembershipWire.PublishedEvent, StringComparison.Ordinal)).ToList();

        Assert.True(changed?.StatusCode == HttpStatusCode.OK, $"The change: {Described(changed)}");
        Assert.True(PublishesNaming(log, memberId, spaceId) == 1, $"Expected one publish naming the target and the Space; the publish lines were: {string.Join(" | ", publishes)}");
        Assert.True(publishes.Count == 1, $"Expected exactly one publish line in the run; found {publishes.Count}: {string.Join(" | ", publishes)}");
        Assert.DoesNotContain(ownerId.ToString("D"), publishes[0], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", publishes[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// AC6, AC11, Q7 (a), Q11 (a) - an out-of-authority <c>403</c>, the Owner's own-row <c>409</c>, an unknown id's
    /// <c>404</c>, a same-Role no-op and an idempotent replay publish nothing: across all of them and one committed change
    /// (the anchor), the run's log holds exactly one publish line, the committed change's. Plant 9: the publish on every
    /// attempt turns the count above one.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-16")]
    [Trait("Requirement", "AD-9")]
    [Trait("Requirement", "AD-18")]
    public async Task Refusals_not_founds_no_ops_and_replays_publish_nothing()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, adminAddress, memberAddress) = (RandomAddress(), RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var memberId = Guid.Empty;
        var key = Guid.NewGuid().ToString("D");
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            var ownerId = await AccountIdOfAsync(database, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Silent " + Suffix(), ownerId, Role.Owner);
            var admin = await SignedInAsync(client, adminAddress);
            await wire.AddMembershipAsync(spaceId, await AccountIdOfAsync(database, adminAddress), "admin");
            _ = await SignedInAsync(client, memberAddress);
            memberId = await AccountIdOfAsync(database, memberAddress);
            await wire.AddMembershipAsync(spaceId, memberId, "member");

            var member = await wire.MembershipOfAsync(spaceId, memberId);

            await owner.FetchAntiforgeryTokenAsync();
            await admin.FetchAntiforgeryTokenAsync();

            answers["403"] = await ChangeRoleOverWireAsync(admin, spaceId, member.Id, "admin");
            answers["409"] = await ChangeRoleOverWireAsync(owner, spaceId, (await wire.MembershipOfAsync(spaceId, ownerId)).Id, "admin");
            answers["404"] = await ChangeRoleOverWireAsync(owner, spaceId, Guid.NewGuid(), "viewer");
            answers["no-op"] = await ChangeRoleOverWireAsync(owner, spaceId, member.Id, "member");
            answers["committed"] = await ChangeRoleOverWireAsync(owner, spaceId, member.Id, "viewer", key);
            answers["replay"] = await ChangeRoleOverWireAsync(owner, spaceId, member.Id, "viewer", key);
        });

        var expected = new Dictionary<string, HttpStatusCode>(StringComparer.Ordinal)
        {
            ["403"] = HttpStatusCode.Forbidden,
            ["409"] = HttpStatusCode.Conflict,
            ["404"] = HttpStatusCode.NotFound,
            ["no-op"] = HttpStatusCode.OK,
            ["committed"] = HttpStatusCode.OK,
            ["replay"] = HttpStatusCode.OK,
        };

        foreach (var (arm, status) in expected)
        {
            Assert.True(answers[arm].StatusCode == status, $"The {arm} arm: expected {status:D}, got {Described(answers[arm])}");
        }

        var publishes = log.Split('\n').Where(line => line.Contains(MembershipWire.PublishedEvent, StringComparison.Ordinal)).ToList();

        Assert.True(publishes.Count == 1, $"Expected one publish (the committed change's) across six requests; found {publishes.Count}: {string.Join(" | ", publishes)}");
        Assert.Equal(1, PublishesNaming(log, memberId, spaceId));
    }

    /// <summary>
    /// AC11, AD-18, AR-25 - the same <c>Idempotency-Key</c> after a committed change replays the original <c>200</c> and body;
    /// the same key on a different body is <c>422 idempotency-key-reused</c>; a malformed key (one character past
    /// <see cref="Idempotency.MaxKeyLength"/>) is <c>400 invalid-idempotency-key</c>. The stored Role is the first change's.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "AR-25")]
    public async Task A_change_replays_under_its_key_a_reused_key_on_another_body_is_422_and_a_malformed_key_400()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, memberAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString("D");
        var memberId = Guid.Empty;
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Replayed " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);
            _ = await SignedInAsync(client, memberAddress);
            memberId = await AccountIdOfAsync(database, memberAddress);
            await wire.AddMembershipAsync(spaceId, memberId, "member");

            var member = await wire.MembershipOfAsync(spaceId, memberId);

            await owner.FetchAntiforgeryTokenAsync();
            answers["original"] = await ChangeRoleOverWireAsync(owner, spaceId, member.Id, "viewer", key);
            answers["replay"] = await ChangeRoleOverWireAsync(owner, spaceId, member.Id, "viewer", key);
            answers["reused"] = await ChangeRoleOverWireAsync(owner, spaceId, member.Id, "admin", key);
            answers["malformed"] = await ChangeRoleOverWireAsync(owner, spaceId, member.Id, "admin", new string('k', Idempotency.MaxKeyLength + 1));
        });

        Assert.True(answers["original"].StatusCode == HttpStatusCode.OK, $"The original: {Described(answers["original"])}");
        Assert.True(answers["replay"].StatusCode == HttpStatusCode.OK && answers["replay"].Body == answers["original"].Body, $"The replay: {Described(answers["replay"])}; the original was {Described(answers["original"])}");
        Assert.True(answers["reused"].StatusCode == HttpStatusCode.UnprocessableContent, $"The reused key: {Described(answers["reused"])}");
        Assert.Equal(ProblemTypes.IdempotencyKeyReused, ProblemType(answers["reused"]));
        Assert.True(answers["malformed"].StatusCode == HttpStatusCode.BadRequest, $"The malformed key: {Described(answers["malformed"])}");
        Assert.Equal(ProblemTypes.InvalidIdempotencyKey, ProblemType(answers["malformed"]));
        Assert.Equal("Viewer", (await wire.MembershipOfAsync(spaceId, memberId)).Role);
    }

    /// <summary>
    /// AC2, AD-23, code review 2026-11-03 (decided by Lee, option (b)) - a Role change's answer carries no email address, and
    /// neither does the idempotency record that stores it: the <c>PATCH</c> body has exactly <c>displayName</c>, <c>id</c>,
    /// <c>isYou</c> and <c>role</c>, and the one <c>IdempotencyRecord</c> written under the change's key holds the row (the
    /// anchor: its id and display name) but neither the address as typed nor its normalised form. The replay is the same
    /// bytes. The target is seeded with <c>AddMembershipAsync</c>: the case is about the record, not how the Member joined.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-23")]
    [Trait("Requirement", "UX-DR34")]
    [Trait("Requirement", "AR-25")]
    public async Task A_Role_change_answer_and_its_idempotency_record_carry_no_email_address()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, memberAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString("D");
        var memberName = DisplayName();
        var memberId = Guid.Empty;
        var membershipId = Guid.Empty;
        WireResponse? changed = null;
        WireResponse? replay = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Addressless " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);
            _ = await SignedInAsync(client, memberAddress);
            memberId = await AccountIdOfAsync(database, memberAddress);
            await wire.AddMembershipAsync(spaceId, memberId, "member");
            await wire.SetDisplayNameAsync(memberId, memberName);
            membershipId = (await wire.MembershipOfAsync(spaceId, memberId)).Id;

            await owner.FetchAntiforgeryTokenAsync();
            changed = await ChangeRoleOverWireAsync(owner, spaceId, membershipId, "viewer", key);
            replay = await ChangeRoleOverWireAsync(owner, spaceId, membershipId, "viewer", key);
        });

        Assert.True(changed?.StatusCode == HttpStatusCode.OK, $"The change: {Described(changed)}");
        Assert.Equal(MembershipWire.ChangedRowMembers, MembershipWire.ObjectMemberNames(changed.Body));
        Assert.Equal(new ChangedRow(membershipId, memberName, "viewer", IsYou: false), MembershipWire.ParseRow(changed.Body));
        Assert.True(replay?.StatusCode == HttpStatusCode.OK && replay.Body == changed.Body, $"The replay: {Described(replay)}; the change was {Described(changed)}");

        var addresses = new[] { memberAddress, EmailAddressNormalisation.Normalise(memberAddress) };

        Assert.All(addresses, address => Assert.DoesNotContain(address, changed.Body, StringComparison.OrdinalIgnoreCase));

        await using var scoped = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = scoped.CreateCommand();

        command.CommandText = "SELECT Body FROM dbo.[IdempotencyRecord] WHERE SpaceId = @space AND [Key] = @key";
        command.Parameters.Add(new SqlParameter("@space", spaceId));
        command.Parameters.Add(new SqlParameter("@key", key));

        var bodies = new List<string>();

        await using (var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken))
        {
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                bodies.Add(await reader.IsDBNullAsync(0, TestContext.Current.CancellationToken) ? string.Empty : reader.GetString(0));
            }
        }

        var stored = Assert.Single(bodies);

        Assert.Contains(membershipId.ToString("D"), stored, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(memberName, stored, StringComparison.Ordinal);
        Assert.All(addresses, address => Assert.DoesNotContain(address, stored, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// AC6, NFR-2 ("reflected on the very next request, no tolerance, on the browser and the API alike"), V-1 - five
    /// capability classes, each on both surfaces. An Admin demoted to Member by the Owner loses Labels, Invitations, the
    /// Space rename and the Membership list on its very next request, by Session and by Token, and keeps Project creation; a
    /// Member demoted to Viewer loses Project and Task creation and keeps reading Projects. Each probe answered a success on
    /// both surfaces before the <c>PATCH</c> (the anchor), and is sent after the <c>PATCH</c>'s <c>200</c> - never after a
    /// delay. The actor joins through the product; its Token is issued for this Space before the change. Plant 10: a Role
    /// cache in the resolver.
    /// </summary>
    /// <param name="from">The actor's Role before.</param>
    /// <param name="to">The Role the Owner sets.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("admin", "member")]
    [InlineData("member", "viewer")]
    [Trait("Requirement", "NFR-2")]
    [Trait("Requirement", "FR-13")]
    [Trait("Requirement", "AD-4")]
    public async Task The_very_next_request_after_a_demotion_is_governed_on_both_surfaces(string from, string to)
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, actorAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var before = new List<string>();
        var after = new List<string>();
        WireResponse? demoted = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            var ownerId = await AccountIdOfAsync(database, ownerAddress);

            // With a Status, so the Task-creation probe has somewhere to land (a seeded Space carries none; dev-phase
            // correction of the ATDD scaffold, recorded in the story's Debug Log).
            await SpaceSeeding.SeedAsync(database, spaceId, context =>
            {
                context.Spaces.Add(NamedSpace(spaceId, "Governed " + Suffix()));
                context.Memberships.Add(SpaceSeeding.NewMembership(spaceId, ownerId, Role.Owner));
                context.StatusDefinitions.Add(SpaceSeeding.NewStatus(spaceId, "To do"));
            }, TestContext.Current.CancellationToken);

            var actor = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), actorAddress, DisplayName(), from);
            var token = await BearerForAsync(actor, spaceId);
            var projectId = await CreateProjectOverWireAsync(owner, spaceId);

            (string Name, HttpMethod Method, string Path, Func<HttpContent?> Body)[] adminLoses =
            [
                ("define a Label", HttpMethod.Post, LabelRoutes.LabelsOf(spaceId), () => LabelWire.DefinitionBody("Label " + Suffix(), LabelWire.Rust)),
                ("list Invitations", HttpMethod.Get, InvitationRoutes.InvitationsOf(spaceId), () => null),
                ("rename the Space", HttpMethod.Patch, SpaceRoutes.ActiveOf(spaceId), () => JsonContent.Create(new { name = "Renamed " + Suffix() }, options: JsonOptions)),
                ("list Memberships", HttpMethod.Get, MembershipRoutes.MembershipsOf(spaceId), () => null),
            ];
            (string Name, HttpMethod Method, string Path, Func<HttpContent?> Body)[] memberLoses =
            [
                ("create a Project", HttpMethod.Post, ProjectRoutes.ListIn(spaceId), () => JsonContent.Create(new { name = "Project " + Suffix() }, options: JsonOptions)),
                ("create a Task", HttpMethod.Post, TaskRoutes.CreateIn(spaceId, projectId), () => TaskWire.TitleBody("Task " + Suffix())),
            ];
            (string Name, HttpMethod Method, string Path, Func<HttpContent?> Body) adminKeeps =
                ("create a Project", HttpMethod.Post, ProjectRoutes.ListIn(spaceId), () => JsonContent.Create(new { name = "Project " + Suffix() }, options: JsonOptions));
            (string Name, HttpMethod Method, string Path, Func<HttpContent?> Body) memberKeeps =
                ("read Projects", HttpMethod.Get, ProjectRoutes.ListIn(spaceId), () => null);
            var lost = from == "admin" ? adminLoses : memberLoses;
            var kept = from == "admin" ? adminKeeps : memberKeeps;

            async Task ProbeAsync(List<string> into, (string Name, HttpMethod Method, string Path, Func<HttpContent?> Body) probe)
            {
                var bySession = await actor.SendWithHeadersAsync(probe.Method, probe.Path, probe.Body(), NoKey, withAntiforgeryToken: probe.Method != HttpMethod.Get);
                var byToken = await actor.SendWithTokenAndHeadersAsync(probe.Method, probe.Path, token, probe.Body(), NoKey);

                into.Add($"{probe.Name} / Session: {Described(bySession)}");
                into.Add($"{probe.Name} / ApiToken: {Described(byToken)}");
            }

            await actor.FetchAntiforgeryTokenAsync();

            foreach (var probe in lost.Append(kept))
            {
                await ProbeAsync(before, probe);
            }

            await owner.FetchAntiforgeryTokenAsync();
            demoted = await ChangeRoleOverWireAsync(owner, spaceId, (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, actorAddress))).Id, to);

            foreach (var probe in lost.Append(kept))
            {
                await ProbeAsync(after, probe);
            }
        });

        var keptCount = 2;

        Assert.True(demoted?.StatusCode == HttpStatusCode.OK, $"The Owner's {from} -> {to}: {Described(demoted)}");
        Assert.True(before.TrueForAll(line => line.Split(": ", 2)[1].StartsWith('2')), "Before the change every probe should succeed on both surfaces:\n" + string.Join('\n', before));
        Assert.True(
            after.SkipLast(keptCount).All(line => line.Split(": ", 2)[1].StartsWith("403", StringComparison.Ordinal)),
            $"After {from} -> {to} every lost capability should be 403 on the very next request, on both surfaces:\n" + string.Join('\n', after));
        Assert.True(after.TakeLast(keptCount).All(line => line.Split(": ", 2)[1].StartsWith('2')), "The kept capability should still succeed on both surfaces:\n" + string.Join('\n', after));
    }

    /// <summary>
    /// AC5, AR-12, AD-5 (the half the schema carries) - under a Space's key, raw SQL setting a second Membership to
    /// <c>Owner</c> fails with <c>2601</c> on <c>UX_Membership_SpaceId_Owner</c>, whatever the application does; an update of
    /// the same row to <c>Admin</c> succeeds, so the refusal is the index and not the row. The Space still holds exactly one
    /// Owner. <b>Green today, left active</b> as the standing proof that "never a second Owner" is the schema's (no earlier
    /// test asserted the index's refusal by number; <c>MigrationBundleEquivalenceTests</c> only lists it). Anchor and plant: the
    /// index dropped in a scratch database turns it red (the dev phase records it). Seeded in one unit under the Space's key:
    /// <c>FK_Membership_Space_SpaceId</c>, <c>FK_Membership_Account_AccountId</c> (both Accounts added in the unit),
    /// <c>AK_Membership_SpaceId_AccountId</c> (two Accounts), one Owner.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-12")]
    [Trait("Requirement", "AD-5")]
    public async Task A_second_Owner_fails_2601_on_the_filtered_index_while_an_Admin_update_of_the_same_row_succeeds()
    {
        var database = Available();
        var wire = Wire();
        var spaceId = Guid.NewGuid();
        var (owner, other) = (SpaceSeeding.NewAccount(), SpaceSeeding.NewAccount());
        var second = SpaceSeeding.NewMembership(spaceId, other.Id, Role.Member);
        var cancellationToken = TestContext.Current.CancellationToken;

        await SpaceSeeding.SeedAsync(database, spaceId, context =>
        {
            context.Spaces.Add(NamedSpace(spaceId, "Two Owners " + Suffix()));
            context.Accounts.Add(owner);
            context.Accounts.Add(other);
            context.Memberships.Add(SpaceSeeding.NewMembership(spaceId, owner.Id, Role.Owner));
            context.Memberships.Add(second);
        }, cancellationToken);

        await using var connection = await database.OpenForSpaceAsync(spaceId, cancellationToken);

        async Task<int> SetRoleAsync(string stored)
        {
            await using var command = connection.CreateCommand();

            command.CommandText = "UPDATE dbo.[Membership] SET Role = @role WHERE Id = @id";
            command.Parameters.Add(new SqlParameter("@role", stored));
            command.Parameters.Add(new SqlParameter("@id", second.Id));

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var refused = await Assert.ThrowsAsync<SqlException>(() => SetRoleAsync("Owner"));

        Assert.True(refused.Number == 2601, $"Expected 2601 (duplicate key in a unique index); got {refused.Number}: {refused.Message}");
        Assert.Contains("UX_Membership_SpaceId_Owner", refused.Message, StringComparison.Ordinal);
        Assert.Equal(1, await SetRoleAsync("Admin"));
        Assert.Equal("Admin", (await wire.MembershipOfAsync(spaceId, other.Id)).Role);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// AC5, Task 5, Lee's Q3 (a), AD-2 - <c>Membership</c> carries a <c>BLOCK AFTER UPDATE</c> predicate on
    /// <c>dbo.fn_SpaceIsolationPredicate(SpaceId)</c> in <c>SpaceIsolationPolicy</c>, like every other Space table the product
    /// updates; its <c>BLOCK AFTER INSERT</c> stays Space-only (AD-24, 4.3 C1) and no other Membership predicate changes. The
    /// dev phase also amends <c>SpaceIsolationSchemaTests.TablesThatCannotMove</c> and the per-table counts (active today, and
    /// red the moment the migration lands). Plant 19: the predicate omitted from the migration.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "AR-12")]
    public async Task Membership_carries_a_BLOCK_AFTER_UPDATE_predicate_on_its_Space()
    {
        _ = Available();

        var predicates = await Wire().QueryAsync(
            """
            SELECT p.predicate_type_desc, ISNULL(p.operation_desc, N''), p.predicate_definition
            FROM sys.security_predicates AS p
            JOIN sys.objects AS o ON o.object_id = p.target_object_id
            WHERE o.name = N'Membership'
            ORDER BY p.predicate_type_desc, p.operation_desc
            """);

        var described = string.Join(" | ", predicates.Select(row => string.Join(' ', row)));

        Assert.True(
            predicates.Count(row => row[0] == "BLOCK" && row[1] == "AFTER UPDATE" && row[2].Contains("fn_SpaceIsolationPredicate", StringComparison.Ordinal) && row[2].Contains("[SpaceId]", StringComparison.Ordinal)) == 1,
            "Expected one BLOCK AFTER UPDATE on fn_SpaceIsolationPredicate([SpaceId]); Membership's predicates are: " + described);
        Assert.True(
            predicates.Count(row => row[0] == "BLOCK" && row[1] == "AFTER INSERT" && row[2].Contains("fn_SpaceIsolationPredicate", StringComparison.Ordinal)) == 1,
            "BLOCK AFTER INSERT must stay Space-only (AD-24); Membership's predicates are: " + described);
        Assert.True(predicates.Count(row => row[0] == "FILTER") == 1, "The FILTER predicate is unchanged; Membership's predicates are: " + described);
    }

    /// <summary>
    /// How many <see cref="MembershipWire.PublishedEvent"/> lines in <paramref name="log"/> name both the Account and the
    /// Space.
    /// </summary>
    private static int PublishesNaming(string log, Guid accountId, Guid spaceId) =>
        log.Split('\n').Count(line =>
            line.Contains(MembershipWire.PublishedEvent, StringComparison.Ordinal)
            && line.Contains(accountId.ToString("D"), StringComparison.OrdinalIgnoreCase)
            && line.Contains(spaceId.ToString("D"), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A Project created through the product on <paramref name="session"/>, for the Task-creation probe.
    /// </summary>
    private static async Task<Guid> CreateProjectOverWireAsync(WireSession session, Guid spaceId)
    {
        await session.FetchAntiforgeryTokenAsync();

        var created = await session.SendWithHeadersAsync(HttpMethod.Post, ProjectRoutes.ListIn(spaceId), JsonContent.Create(new { name = "Probed " + Suffix() }, options: JsonOptions), NoKey);

        Assert.True(created.StatusCode == HttpStatusCode.Created, $"Creating a Project through the product: {Described(created)}");

        using var document = JsonDocument.Parse(created.Body);

        return document.RootElement.GetProperty("id").GetGuid();
    }
}
