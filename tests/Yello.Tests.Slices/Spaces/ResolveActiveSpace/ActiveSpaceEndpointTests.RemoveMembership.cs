using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Memberships;
using Yello.Contracts.Projects;
using Yello.Contracts.Spaces;
using Yello.Contracts.Tasks;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Removing a Membership over the wire: <c>DELETE /api/v1/spaces/{spaceId}/memberships/{membershipId}</c> under
/// <c>Capability.RemoveMembership</c>, answered <c>204</c> with no body. The Admin's Qualified cell is enforced by the
/// pipeline-owned removal authority (the Roles the actor may remove, Owner in none, and the Roles it must still hold),
/// composed into one guarded <c>DELETE</c> under the Space's Memberships lock. The single-row read with its presence,
/// <c>GET</c> on the same path, lives here too, because the removal confirm is its only reader.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every refusal is anchored</b> on a <c>204</c> from the same route in the same case first: an unmapped path
/// answers the boundary <c>404</c>, so a refusal or a not-found asserted alone would pass against a route that does not
/// exist.
/// </para>
/// <para>
/// <b>Seeding.</b> Targets and actors whose Role is the point join through the product (an Invitation at that Role,
/// accepted). The <c>MembershipChanged</c> counts, the presence cases and bulk seed directly
/// (<see cref="WireDatabase.AddMembershipAsync"/>, <see cref="SpaceSeeding"/>): the accept publishes a
/// <c>MembershipChanged</c> of its own and makes its Account present, either of which would bury what the case counts.
/// </para>
/// <para>
/// <b>S-2</b> (<see cref="AssertOneOwnerEachAsync"/>) runs after every case that removes or attempts a removal.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// The Owner removes an Admin, a Member and a Viewer: <c>204</c> with no body, the row gone, the removed Account still
    /// registered, nothing recorded, and exactly one Owner left.
    /// </summary>
    /// <param name="role">The target's Role.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("admin")]
    [InlineData("member")]
    [InlineData("viewer")]
    [Trait("Requirement", "FR-14")]
    public async Task The_Owner_removes_an_Admin_a_Member_or_a_Viewer_and_the_row_is_gone(string role)
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, targetAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var ownerId = Guid.Empty;
        MembershipRow? target = null;
        WireResponse? removed = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            ownerId = await AccountIdOfAsync(database, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Owner removes " + Suffix(), ownerId, Role.Owner);

            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), targetAddress, DisplayName(), role);
            target = await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, targetAddress));

            await owner.FetchAntiforgeryTokenAsync();
            removed = await RemoveOverWireAsync(owner, spaceId, target.Id);
        });

        Assert.True(removed?.StatusCode == HttpStatusCode.NoContent, $"The Owner's removal of a {role}: {Described(removed)}");
        Assert.Equal(string.Empty, removed.Body);
        Assert.DoesNotContain(await wire.MembershipRowsInAsync(spaceId), row => row.AccountId == target!.AccountId);
        Assert.Equal(target!.AccountId, await AccountIdOfAsync(database, targetAddress));
        Assert.DoesNotContain(await RefusalsOfAsync(database, ownerId), row => row.TargetSpaceId == spaceId);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// An Admin, joined through the product, removes a Member and a Viewer: <c>204</c>, the row gone, nothing recorded
    /// against the Admin.
    /// </summary>
    /// <param name="role">The target's Role.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("member")]
    [InlineData("viewer")]
    [Trait("Requirement", "FR-14")]
    [Trait("Assumption", "PRD-12-4")]
    public async Task An_Admin_removes_a_Member_or_a_Viewer(string role)
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, adminAddress, targetAddress) = (RandomAddress(), RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        MembershipRow? target = null;
        WireResponse? removed = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Admin removes " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            var admin = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), adminAddress, DisplayName(), "admin");
            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), targetAddress, DisplayName(), role);
            target = await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, targetAddress));

            removed = await RemoveOverWireAsync(admin, spaceId, target.Id);
        });

        Assert.True(removed?.StatusCode == HttpStatusCode.NoContent, $"The Admin's removal of a {role}: {Described(removed)}");
        Assert.DoesNotContain(await wire.MembershipRowsInAsync(spaceId), row => row.AccountId == target!.AccountId);
        Assert.DoesNotContain(await RefusalsOfAsync(database, await AccountIdOfAsync(database, adminAddress)), row => row.TargetSpaceId == spaceId);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// An Admin removing another Admin or the Owner is beyond its authority ("Admins cannot modify each other"; the Owner is
    /// removable by no one): <c>403 insufficient-role</c> naming <c>remove-membership</c>, recorded once as
    /// <c>InsufficientRole</c> by the pipeline, the target untouched. The anchor, the same Admin removing a Viewer on the same
    /// route, answers <c>204</c> first.
    /// </summary>
    /// <param name="attempt">Whose row the Admin targets.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("other-admin")]
    [InlineData("owner")]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "AD-20")]
    [Trait("Assumption", "PRD-12-4")]
    public async Task An_Admin_removing_another_Admin_or_the_Owner_is_refused_403_recorded_and_nothing_changes(string attempt)
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, adminAddress, otherAdminAddress, viewerAddress) = (RandomAddress(), RandomAddress(), RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        MembershipRow? before = null;
        WireResponse? anchor = null;
        WireResponse? refused = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Beyond removal " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            var admin = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), adminAddress, DisplayName(), "admin");
            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), otherAdminAddress, DisplayName(), "admin");
            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), viewerAddress, DisplayName(), "viewer");

            var targetAddress = attempt switch
            {
                "other-admin" => otherAdminAddress,
                "owner" => ownerAddress,
                _ => throw new ArgumentOutOfRangeException(nameof(attempt), attempt, "Not an attempt this case knows."),
            };

            before = await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, targetAddress));
            anchor = await RemoveOverWireAsync(admin, spaceId, (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, viewerAddress))).Id);
            refused = await RemoveOverWireAsync(admin, spaceId, before.Id);
        });

        Assert.True(anchor?.StatusCode == HttpStatusCode.NoContent, $"The Admin's in-authority anchor: {Described(anchor)}");
        Assert.True(refused?.StatusCode == HttpStatusCode.Forbidden, $"The Admin removing the {attempt}: {Described(refused)}; the anchor was {Described(anchor)}");
        Assert.Equal(ProblemTypes.InsufficientRole, ProblemType(refused));
        Assert.Equal(MembershipWire.RemoveCapabilityCode, CapabilityOf(refused));
        Assert.Equal(before, await wire.MembershipOfAsync(spaceId, before!.AccountId));
        Assert.Equal(
            [(nameof(Capability.RemoveMembership), "InsufficientRole")],
            (await RefusalsOfAsync(database, await AccountIdOfAsync(database, adminAddress))).Where(row => row.TargetSpaceId == spaceId).Select(row => (row.Capability, row.Kind)));
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// "Any Account can remove its own" holds on the item route as well as the leave route, so for an Admin the two routes
    /// agree: removing its own row answers <c>204</c>, the row is gone, and the Admin's very next request in the Space is the
    /// boundary <c>404</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-14")]
    public async Task An_Admin_removing_its_own_row_on_the_item_route_answers_204_and_is_out()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, adminAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var adminId = Guid.Empty;
        WireResponse? removed = null;
        WireResponse? next = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Own removal " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            var admin = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), adminAddress, DisplayName(), "admin");
            adminId = await AccountIdOfAsync(database, adminAddress);

            removed = await RemoveOverWireAsync(admin, spaceId, (await wire.MembershipOfAsync(spaceId, adminId)).Id);
            next = await admin.GetAsync(SpaceRoutes.ActiveOf(spaceId));
        });

        Assert.True(removed?.StatusCode == HttpStatusCode.NoContent, $"The Admin's own removal: {Described(removed)}");
        Assert.DoesNotContain(await wire.MembershipRowsInAsync(spaceId), row => row.AccountId == adminId);
        Assert.True(next?.StatusCode == HttpStatusCode.NotFound, $"The next request after removing oneself: {Described(next)}");
        Assert.Equal(ProblemTypes.NotAvailable, ProblemType(next));
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// The Owner's Membership cannot be removed by anyone while it holds ownership, the Owner included: the Owner on its own
    /// row is <c>409 ownership-held</c>, the row untouched, nothing recorded (the Owner holds full authority, so "insufficient
    /// Role" would be untrue), no address in the body, and one Owner left. The anchor, the Owner removing a Member on the same
    /// route, answers <c>204</c> first.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "AD-5")]
    public async Task The_Owner_removing_its_own_row_is_refused_409_ownership_held_and_nothing_changes()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, memberAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var ownerId = Guid.Empty;
        WireResponse? anchor = null;
        WireResponse? refused = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            ownerId = await AccountIdOfAsync(database, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Owner stays " + Suffix(), ownerId, Role.Owner);
            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), memberAddress, DisplayName(), "member");

            await owner.FetchAntiforgeryTokenAsync();
            anchor = await RemoveOverWireAsync(owner, spaceId, (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, memberAddress))).Id);
            refused = await RemoveOverWireAsync(owner, spaceId, (await wire.MembershipOfAsync(spaceId, ownerId)).Id);
        });

        Assert.True(anchor?.StatusCode == HttpStatusCode.NoContent, $"The Owner's anchor: {Described(anchor)}");
        Assert.True(refused?.StatusCode == HttpStatusCode.Conflict, $"The Owner's own row: {Described(refused)}; the anchor was {Described(anchor)}");
        Assert.Equal(ProblemTypes.OwnershipHeld, ProblemType(refused));
        Assert.DoesNotContain("@", refused.Body, StringComparison.Ordinal);
        Assert.Equal("Owner", (await wire.MembershipOfAsync(spaceId, ownerId)).Role);
        Assert.DoesNotContain(await RefusalsOfAsync(database, ownerId), row => row.TargetSpaceId == spaceId);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// A Member or a Viewer is refused by the pipeline before any slice runs, on another's row and on its own: <c>403</c>
    /// naming <c>remove-membership</c>, recorded <c>InsufficientRole</c>, both rows untouched. They leave through the leave
    /// route instead. The anchor, the Owner removing a Viewer on the same route, answers <c>204</c> first.
    /// </summary>
    /// <param name="lesser">The actor's Role.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("member")]
    [InlineData("viewer")]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "AD-20")]
    public async Task A_Member_or_a_Viewer_is_refused_403_by_the_pipeline_even_on_its_own_row(string lesser)
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, actorAddress, otherAddress, anchorAddress) = (RandomAddress(), RandomAddress(), RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var actorId = Guid.Empty;
        MembershipRow? own = null;
        MembershipRow? other = null;
        WireResponse? anchor = null;
        var refused = new List<WireResponse>();

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Lesser removal " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            var actor = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), actorAddress, DisplayName(), lesser);
            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), otherAddress, DisplayName(), "member");
            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), anchorAddress, DisplayName(), "viewer");

            actorId = await AccountIdOfAsync(database, actorAddress);
            own = await wire.MembershipOfAsync(spaceId, actorId);
            other = await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, otherAddress));

            await owner.FetchAntiforgeryTokenAsync();
            anchor = await RemoveOverWireAsync(owner, spaceId, (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, anchorAddress))).Id);
            refused.Add(await RemoveOverWireAsync(actor, spaceId, other.Id));
            refused.Add(await RemoveOverWireAsync(actor, spaceId, own.Id));
        });

        Assert.True(anchor?.StatusCode == HttpStatusCode.NoContent, $"The Owner's anchor: {Described(anchor)}");
        Assert.All(refused, answer =>
        {
            Assert.True(answer.StatusCode == HttpStatusCode.Forbidden, $"The {lesser}'s removal: {Described(answer)}; the anchor was {Described(anchor)}");
            Assert.Equal(MembershipWire.RemoveCapabilityCode, CapabilityOf(answer));
        });
        Assert.Equal(own, await wire.MembershipOfAsync(spaceId, actorId));
        Assert.Equal(other, await wire.MembershipOfAsync(spaceId, other!.AccountId));
        Assert.Equal(
            2,
            (await RefusalsOfAsync(database, actorId)).Count(row => row.TargetSpaceId == spaceId && row.Kind == "InsufficientRole" && row.Capability == nameof(Capability.RemoveMembership)));
    }

    /// <summary>
    /// An id that names no Membership in this Space - one never issued, and another Space's - is the in-Space not-found: the
    /// boundary <c>404 not-available</c> body byte for byte, not recorded against this Space, and the other Space's
    /// Membership untouched. The anchor, the Owner removing its own Member, answers <c>204</c> first. The foreign Space is
    /// seeded directly: the caller is no member there.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-20")]
    [Trait("Requirement", "FR-14")]
    public async Task Removing_an_id_from_another_Space_or_never_issued_is_the_boundary_404_and_is_not_recorded()
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
            await SeedNamedSpaceAsync(database, spaceId, "Not found removal " + Suffix(), ownerId, Role.Owner);
            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), memberAddress, DisplayName(), "member");

            await owner.FetchAntiforgeryTokenAsync();
            answers["anchor"] = await RemoveOverWireAsync(owner, spaceId, (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, memberAddress))).Id);
            answers["absent Space"] = await RemoveOverWireAsync(owner, Guid.NewGuid(), Guid.NewGuid());
            answers["never issued"] = await RemoveOverWireAsync(owner, spaceId, Guid.NewGuid());
            answers["another Space's"] = await RemoveOverWireAsync(owner, spaceId, foreignBefore.Id);
        });

        Assert.True(answers["anchor"].StatusCode == HttpStatusCode.NoContent, $"The Owner's anchor: {Described(answers["anchor"])}");
        Assert.Equal(HttpStatusCode.NotFound, answers["absent Space"].StatusCode);

        foreach (var arm in new[] { "never issued", "another Space's" })
        {
            var answer = answers[arm];

            Assert.True(answer.StatusCode == HttpStatusCode.NotFound, $"{arm} id: {Described(answer)}; the absent Space answered {Described(answers["absent Space"])}");
            Assert.Equal(answers["absent Space"].Body, answer.Body);
            Assert.Equal(answers["absent Space"].HeaderNames, answer.HeaderNames);
        }

        Assert.DoesNotContain(await RefusalsOfAsync(database, ownerId), row => row.TargetSpaceId == spaceId);
        Assert.Equal(foreignBefore, await wire.MembershipOfAsync(foreignSpaceId, foreignBefore!.AccountId));
        await AssertOneOwnerEachAsync(wire, spaceId, foreignSpaceId);
    }

    /// <summary>
    /// Removal invalidates every API Token the removed Account holds for this Space, in the same transaction, and nothing
    /// else: the Token bound here read <c>200</c> before and its first request after is the no-credential <c>401</c>, byte for
    /// byte a request with no credential; no Token row is left bound here; the same Account's Token for a Space it owns still
    /// reads <c>200</c>. The cascade from <c>Membership</c> does this only if the row is deleted, so a soft delete turns this
    /// red.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "AD-6")]
    [Trait("Requirement", "FR-14")]
    public async Task Removal_kills_the_removed_Accounts_Tokens_for_this_Space_and_no_other()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, targetAddress) = (RandomAddress(), RandomAddress());
        var (spaceId, ownSpaceId) = (Guid.NewGuid(), Guid.NewGuid());
        var targetId = Guid.Empty;
        var before = new Dictionary<string, WireResponse>(StringComparer.Ordinal);
        var after = new Dictionary<string, WireResponse>(StringComparer.Ordinal);
        WireResponse? removed = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Tokens die " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            var target = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), targetAddress, DisplayName(), "member");
            targetId = await AccountIdOfAsync(database, targetAddress);
            await SeedNamedSpaceAsync(database, ownSpaceId, "Tokens live " + Suffix(), targetId, Role.Owner);

            var here = await BearerForAsync(target, spaceId);
            var elsewhere = await BearerForAsync(target, ownSpaceId);
            var anonymous = new WireSession(client);

            before["here"] = await target.GetWithTokenAsync(SpaceRoutes.ActiveOf(spaceId), here);
            before["elsewhere"] = await target.GetWithTokenAsync(SpaceRoutes.ActiveOf(ownSpaceId), elsewhere);

            await owner.FetchAntiforgeryTokenAsync();
            removed = await RemoveOverWireAsync(owner, spaceId, (await wire.MembershipOfAsync(spaceId, targetId)).Id);

            after["here"] = await target.GetWithTokenAsync(SpaceRoutes.ActiveOf(spaceId), here);
            after["no credential"] = await anonymous.GetAnonymouslyAsync(SpaceRoutes.ActiveOf(spaceId));
            after["elsewhere"] = await target.GetWithTokenAsync(SpaceRoutes.ActiveOf(ownSpaceId), elsewhere);
        });

        Assert.True(before.Values.All(answer => answer.StatusCode == HttpStatusCode.OK), "Both Tokens read 200 before the removal: " + string.Join("; ", before.Select(entry => $"{entry.Key} {Described(entry.Value)}")));
        Assert.True(removed?.StatusCode == HttpStatusCode.NoContent, $"The removal: {Described(removed)}");
        Assert.True(after["here"].StatusCode == HttpStatusCode.Unauthorized, $"The Token bound here, after the removal: {Described(after["here"])}");
        Assert.Equal(after["no credential"].StatusCode, after["here"].StatusCode);
        Assert.Equal(after["no credential"].Body, after["here"].Body);
        Assert.Equal(after["no credential"].HeaderNames, after["here"].HeaderNames);
        Assert.Equal(0, await wire.TokenCountAsync(targetId, spaceId));
        Assert.True(after["elsewhere"].StatusCode == HttpStatusCode.OK, $"The Token bound to the Account's own Space, after the removal: {Described(after["elsewhere"])}");
        await AssertOneOwnerEachAsync(wire, spaceId, ownSpaceId);
    }

    /// <summary>
    /// Removal deletes the removed Account's presence in this Space, in the same transaction, and only there: a present
    /// Member's row is gone after the removal, so it stops counting toward the Space's presence bound and a rejoin does not
    /// read as "in this Space now"; its presence in a Space it owns is untouched. Seeded directly; the presence rows are
    /// written by the Account's own requests.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "FR-14")]
    public async Task Removal_deletes_the_removed_Accounts_presence_in_this_Space_only()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, targetAddress) = (RandomAddress(), RandomAddress());
        var (spaceId, ownSpaceId) = (Guid.NewGuid(), Guid.NewGuid());
        var targetId = Guid.Empty;
        var presentBefore = (Here: 0, Elsewhere: 0);
        WireResponse? removed = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Presence goes " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            var target = await SignedInAsync(client, targetAddress);
            targetId = await AccountIdOfAsync(database, targetAddress);
            await wire.AddMembershipAsync(spaceId, targetId, "member");
            await SeedNamedSpaceAsync(database, ownSpaceId, "Presence stays " + Suffix(), targetId, Role.Owner);

            Assert.Equal(HttpStatusCode.OK, (await target.GetAsync(SpaceRoutes.ActiveOf(spaceId))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await target.GetAsync(SpaceRoutes.ActiveOf(ownSpaceId))).StatusCode);
            presentBefore = (await PresenceRowsAsync(database, spaceId, targetId), await PresenceRowsAsync(database, ownSpaceId, targetId));

            await owner.FetchAntiforgeryTokenAsync();
            removed = await RemoveOverWireAsync(owner, spaceId, (await wire.MembershipOfAsync(spaceId, targetId)).Id);
        });

        Assert.Equal((1, 1), presentBefore);
        Assert.True(removed?.StatusCode == HttpStatusCode.NoContent, $"The removal: {Described(removed)}");
        Assert.Equal(0, await PresenceRowsAsync(database, spaceId, targetId));
        Assert.Equal(1, await PresenceRowsAsync(database, ownSpaceId, targetId));
    }

    /// <summary>
    /// A committed removal publishes <c>MembershipChanged</c> exactly once, after commit, for the removed Account and this
    /// Space, observed through its one log line (ids only, no <c>@</c>); the remover is named by no publish. The target is
    /// seeded directly, so no accept's own publish is in the count.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-16")]
    [Trait("Requirement", "AD-9")]
    public async Task A_committed_removal_logs_exactly_one_MembershipChanged_for_the_removed_Account()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, memberAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var (ownerId, memberId) = (Guid.Empty, Guid.Empty);
        WireResponse? removed = null;

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            ownerId = await AccountIdOfAsync(database, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Removal published " + Suffix(), ownerId, Role.Owner);
            _ = await SignedInAsync(client, memberAddress);
            memberId = await AccountIdOfAsync(database, memberAddress);
            await wire.AddMembershipAsync(spaceId, memberId, "member");

            await owner.FetchAntiforgeryTokenAsync();
            removed = await RemoveOverWireAsync(owner, spaceId, (await wire.MembershipOfAsync(spaceId, memberId)).Id);
        });

        var publishes = log.Split('\n').Where(line => line.Contains(MembershipWire.PublishedEvent, StringComparison.Ordinal)).ToList();

        Assert.True(removed?.StatusCode == HttpStatusCode.NoContent, $"The removal: {Described(removed)}");
        Assert.True(publishes.Count == 1, $"Expected exactly one publish line in the run; found {publishes.Count}: {string.Join(" | ", publishes)}");
        Assert.Equal(1, PublishesNaming(log, memberId, spaceId));
        Assert.DoesNotContain(ownerId.ToString("D"), publishes[0], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", publishes[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// A refused removal (<c>403</c>), the Owner's own row (<c>409</c>), an unknown id (<c>404</c>) and a replay publish
    /// nothing: across them and one committed removal, the run's log holds exactly one publish line, the committed removal's.
    /// Recording before the <c>DELETE</c>, or on every attempt, turns the count above one.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-16")]
    [Trait("Requirement", "AD-9")]
    [Trait("Requirement", "AD-18")]
    public async Task Refusals_not_founds_and_replays_of_a_removal_publish_nothing()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, adminAddress, otherAdminAddress, memberAddress) = (RandomAddress(), RandomAddress(), RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var memberId = Guid.Empty;
        var key = Guid.NewGuid().ToString("D");
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            var ownerId = await AccountIdOfAsync(database, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Removal silent " + Suffix(), ownerId, Role.Owner);
            var admin = await SignedInAsync(client, adminAddress);
            await wire.AddMembershipAsync(spaceId, await AccountIdOfAsync(database, adminAddress), "admin");
            _ = await SignedInAsync(client, otherAdminAddress);
            var otherAdminId = await AccountIdOfAsync(database, otherAdminAddress);
            await wire.AddMembershipAsync(spaceId, otherAdminId, "admin");
            _ = await SignedInAsync(client, memberAddress);
            memberId = await AccountIdOfAsync(database, memberAddress);
            await wire.AddMembershipAsync(spaceId, memberId, "member");

            var member = await wire.MembershipOfAsync(spaceId, memberId);

            await owner.FetchAntiforgeryTokenAsync();
            await admin.FetchAntiforgeryTokenAsync();

            answers["403"] = await RemoveOverWireAsync(admin, spaceId, (await wire.MembershipOfAsync(spaceId, otherAdminId)).Id);
            answers["409"] = await RemoveOverWireAsync(owner, spaceId, (await wire.MembershipOfAsync(spaceId, ownerId)).Id);
            answers["404"] = await RemoveOverWireAsync(owner, spaceId, Guid.NewGuid());
            answers["committed"] = await RemoveOverWireAsync(owner, spaceId, member.Id, key);
            answers["replay"] = await RemoveOverWireAsync(owner, spaceId, member.Id, key);
        });

        var expected = new Dictionary<string, HttpStatusCode>(StringComparer.Ordinal)
        {
            ["403"] = HttpStatusCode.Forbidden,
            ["409"] = HttpStatusCode.Conflict,
            ["404"] = HttpStatusCode.NotFound,
            ["committed"] = HttpStatusCode.NoContent,
            ["replay"] = HttpStatusCode.NoContent,
        };

        foreach (var (arm, status) in expected)
        {
            Assert.True(answers[arm].StatusCode == status, $"The {arm} arm: expected {status:D}, got {Described(answers[arm])}");
        }

        var publishes = log.Split('\n').Where(line => line.Contains(MembershipWire.PublishedEvent, StringComparison.Ordinal)).ToList();

        Assert.True(publishes.Count == 1, $"Expected one publish (the committed removal's) across five requests; found {publishes.Count}: {string.Join(" | ", publishes)}");
        Assert.Equal(1, PublishesNaming(log, memberId, spaceId));
    }

    /// <summary>
    /// The same <c>Idempotency-Key</c> after a committed removal replays the original <c>204</c>; the same key on another
    /// target is <c>422 idempotency-key-reused</c> and removes nothing; a malformed key is <c>400 invalid-idempotency-key</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "AR-25")]
    public async Task A_removal_replays_under_its_key_a_reused_key_on_another_target_is_422_and_a_malformed_key_400()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, firstAddress, secondAddress) = (RandomAddress(), RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString("D");
        var secondId = Guid.Empty;
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Removal replayed " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);
            _ = await SignedInAsync(client, firstAddress);
            var firstId = await AccountIdOfAsync(database, firstAddress);
            await wire.AddMembershipAsync(spaceId, firstId, "member");
            _ = await SignedInAsync(client, secondAddress);
            secondId = await AccountIdOfAsync(database, secondAddress);
            await wire.AddMembershipAsync(spaceId, secondId, "viewer");

            var (first, second) = (await wire.MembershipOfAsync(spaceId, firstId), await wire.MembershipOfAsync(spaceId, secondId));

            await owner.FetchAntiforgeryTokenAsync();
            answers["original"] = await RemoveOverWireAsync(owner, spaceId, first.Id, key);
            answers["replay"] = await RemoveOverWireAsync(owner, spaceId, first.Id, key);
            answers["reused"] = await RemoveOverWireAsync(owner, spaceId, second.Id, key);
            answers["malformed"] = await RemoveOverWireAsync(owner, spaceId, second.Id, new string('k', Idempotency.MaxKeyLength + 1));
        });

        Assert.True(answers["original"].StatusCode == HttpStatusCode.NoContent, $"The original: {Described(answers["original"])}");
        Assert.True(answers["replay"].StatusCode == HttpStatusCode.NoContent && answers["replay"].Body.Length == 0, $"The replay: {Described(answers["replay"])}");
        Assert.True(answers["reused"].StatusCode == HttpStatusCode.UnprocessableContent, $"The reused key: {Described(answers["reused"])}");
        Assert.Equal(ProblemTypes.IdempotencyKeyReused, ProblemType(answers["reused"]));
        Assert.True(answers["malformed"].StatusCode == HttpStatusCode.BadRequest, $"The malformed key: {Described(answers["malformed"])}");
        Assert.Equal(ProblemTypes.InvalidIdempotencyKey, ProblemType(answers["malformed"]));
        Assert.Equal("Viewer", (await wire.MembershipOfAsync(spaceId, secondId)).Role);
    }

    /// <summary>
    /// Removal deletes a Membership and nothing a Member made: a Project and a Task the removed Member created through the
    /// product are still there afterwards, with the Space's counts unchanged. That Tasks it was Assignee of become unassigned
    /// is <c>An_assigned_Member_is_removed_and_its_Tasks_are_unassigned_and_survive</c>'s; this holds that nothing is deleted.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-14")]
    public async Task What_the_removed_Member_created_survives_the_removal()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, memberAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var projectId = Guid.Empty;
        var counts = (Projects: 0, Tasks: 0);
        WireResponse? removed = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            var ownerId = await AccountIdOfAsync(database, ownerAddress);

            // With a Status, so the Member's Task has somewhere to land: a seeded Space carries none.
            await SpaceSeeding.SeedAsync(database, spaceId, context =>
            {
                context.Spaces.Add(NamedSpace(spaceId, "Survives " + Suffix()));
                context.Memberships.Add(SpaceSeeding.NewMembership(spaceId, ownerId, Role.Owner));
                context.StatusDefinitions.Add(SpaceSeeding.NewStatus(spaceId, "To do"));
            }, TestContext.Current.CancellationToken);

            var member = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), memberAddress, DisplayName(), "member");

            projectId = await CreateProjectOverWireAsync(member, spaceId);

            var task = await member.SendWithHeadersAsync(HttpMethod.Post, TaskRoutes.CreateIn(spaceId, projectId), TaskWire.TitleBody("Kept " + Suffix()), NoKey);

            Assert.True(task.StatusCode == HttpStatusCode.Created, $"The Member's Task: {Described(task)}");

            counts = ((await wire.ProjectsInAsync(spaceId)).Count, (await wire.TasksInAsync(spaceId)).Count);

            await owner.FetchAntiforgeryTokenAsync();
            removed = await RemoveOverWireAsync(owner, spaceId, (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, memberAddress))).Id);
        });

        Assert.True(removed?.StatusCode == HttpStatusCode.NoContent, $"The removal: {Described(removed)}");
        Assert.Equal(counts, ((await wire.ProjectsInAsync(spaceId)).Count, (await wire.TasksInAsync(spaceId)).Count));
        Assert.Contains(await wire.ProjectsInAsync(spaceId), project => project.Id == projectId);
        Assert.Single(await wire.TasksInAsync(spaceId), row => row.ProjectId == projectId);
    }

    /// <summary>
    /// A removed Account's very next request in the Space, by any route, is governed on both surfaces with no tolerance: by
    /// Session the boundary <c>404</c>, byte for byte the same route on a Space that does not exist, each recorded
    /// <c>CrossSpace</c>; by Token the no-credential <c>401</c>, because the Token died with the Membership. Sampled across
    /// the capability classes: a read (the Project list), a write (Project create), the Members list, and the leave route
    /// itself, whose Token probe is compared with a well-formed Token that never existed. The target is an Admin, so every
    /// other probe answered a success on both surfaces before (the anchor).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-2")]
    [Trait("Requirement", "FR-15")]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-4")]
    public async Task The_very_next_request_after_a_removal_is_the_boundary_404_by_Session_and_401_by_Token()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, targetAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var targetId = Guid.Empty;
        var crossSpaceBefore = 0;
        var before = new List<string>();
        var after = new List<(string Probe, WireResponse Answer, WireResponse Absent)>();
        WireResponse? removed = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Next request " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            var target = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), targetAddress, DisplayName(), "admin");
            targetId = await AccountIdOfAsync(database, targetAddress);

            var token = await BearerForAsync(target, spaceId);
            var absentSpace = Guid.NewGuid();

            (string Name, HttpMethod Method, Func<Guid, string> Path, Func<HttpContent?> Body)[] probes =
            [
                ("read Projects", HttpMethod.Get, ProjectRoutes.ListIn, () => null),
                ("create a Project", HttpMethod.Post, ProjectRoutes.ListIn, () => JsonContent.Create(new { name = "Project " + Suffix() }, options: JsonOptions)),
                ("list Memberships", HttpMethod.Get, MembershipRoutes.MembershipsOf, () => null),
            ];

            await target.FetchAntiforgeryTokenAsync();

            foreach (var probe in probes)
            {
                var bySession = await target.SendWithHeadersAsync(probe.Method, probe.Path(spaceId), probe.Body(), NoKey, withAntiforgeryToken: probe.Method != HttpMethod.Get);
                var byToken = await target.SendWithTokenAndHeadersAsync(probe.Method, probe.Path(spaceId), token, probe.Body(), NoKey);

                before.Add($"{probe.Name} / Session: {Described(bySession)}");
                before.Add($"{probe.Name} / ApiToken: {Described(byToken)}");
            }

            crossSpaceBefore = (await RefusalsOfAsync(database, targetId)).Count(row => row.TargetSpaceId == spaceId && row.Kind == "CrossSpace");

            await owner.FetchAntiforgeryTokenAsync();
            removed = await RemoveOverWireAsync(owner, spaceId, (await wire.MembershipOfAsync(spaceId, targetId)).Id);

            var anonymous = new WireSession(client);

            foreach (var probe in probes)
            {
                after.Add(($"{probe.Name} / Session", await target.SendWithHeadersAsync(probe.Method, probe.Path(spaceId), probe.Body(), NoKey, withAntiforgeryToken: probe.Method != HttpMethod.Get), await target.SendWithHeadersAsync(probe.Method, probe.Path(absentSpace), probe.Body(), NoKey, withAntiforgeryToken: probe.Method != HttpMethod.Get)));
                after.Add(($"{probe.Name} / ApiToken", await target.SendWithTokenAndHeadersAsync(probe.Method, probe.Path(spaceId), token, probe.Body(), NoKey), await anonymous.GetAnonymouslyAsync(probe.Path(spaceId))));
            }

            after.Add(("leave / Session", await LeaveOverWireAsync(target, spaceId), await LeaveOverWireAsync(target, absentSpace)));
            after.Add(("leave / ApiToken", await target.SendWithTokenAndHeadersAsync(HttpMethod.Delete, MembershipRoutes.OwnOf(spaceId), token, content: null, NoKey), await target.SendWithTokenAndHeadersAsync(HttpMethod.Delete, MembershipRoutes.OwnOf(spaceId), WellFormedUnknownToken(), content: null, NoKey)));
        });

        Assert.True(before.TrueForAll(line => line.Split(": ", 2)[1].StartsWith('2')), "Before the removal every probe should succeed on both surfaces:\n" + string.Join('\n', before));
        Assert.True(removed?.StatusCode == HttpStatusCode.NoContent, $"The removal: {Described(removed)}");

        foreach (var (probe, answer, absent) in after)
        {
            var expected = probe.EndsWith("/ ApiToken", StringComparison.Ordinal) ? HttpStatusCode.Unauthorized : HttpStatusCode.NotFound;

            Assert.True(answer.StatusCode == expected, $"{probe} after the removal: {Described(answer)}; the comparison answered {Described(absent)}");
            Assert.Equal(absent.Body, answer.Body);
            Assert.Equal(absent.HeaderNames, answer.HeaderNames);
        }

        var sessionProbes = after.Count(entry => entry.Probe.EndsWith("/ Session", StringComparison.Ordinal));

        Assert.Equal(
            crossSpaceBefore + sessionProbes,
            (await RefusalsOfAsync(database, targetId)).Count(row => row.TargetSpaceId == spaceId && row.Kind == "CrossSpace"));
    }

    /// <summary>
    /// Removal frees a slot under the Memberships-per-Space bound: in a Space holding 100, the Owner removes one Member
    /// (<c>204</c>, 99 left) and a pending Invitation is then accepted (<c>200</c>, 100 again). The accept answers
    /// <c>200</c> with its body, as it does everywhere else. Bulk-seeded under the Space's key.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "AR-30")]
    public async Task A_removal_in_a_full_Space_frees_the_slot_a_pending_acceptance_then_takes()
    {
        var database = Available();
        var wire = Wire();
        Invited? invited = null;
        WireResponse? removed = null;
        WireResponse? accepted = null;
        var afterRemoval = 0;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            invited = await InviteIntoFullSpaceAsync(client, database, wire, mail, "freed", InvitationWire.MembershipsPerSpace);

            var member = (await wire.MembershipRowsInAsync(invited.SpaceId)).First(row => row.Role == "Member");

            await invited.Owner.FetchAntiforgeryTokenAsync();
            removed = await RemoveOverWireAsync(invited.Owner, invited.SpaceId, member.Id);
            afterRemoval = (await MembershipsInAsync(database, invited.SpaceId)).Count;

            var invitee = await RegisteredAndSignedInAsync(client, invited.Invitee, DisplayName());

            await invitee.FetchAntiforgeryTokenAsync();
            accepted = await AcceptAsync(invitee, invited.Token);
        });

        Assert.True(removed?.StatusCode == HttpStatusCode.NoContent, $"The removal at the bound: {Described(removed)}");
        Assert.Equal(InvitationWire.MembershipsPerSpace - 1, afterRemoval);
        Assert.True(accepted?.StatusCode == HttpStatusCode.OK, $"The accept after the removal: {Described(accepted)}");
        Assert.Equal(InvitationWire.MembershipsPerSpace, (await MembershipsInAsync(database, invited!.SpaceId)).Count);
        await AssertOneOwnerEachAsync(wire, invited.SpaceId);
    }

    /// <summary>
    /// The removal confirm's "live session" is presence in this Space only, read against the database clock: the single
    /// row read (<c>GET</c> on the item, under the list's capability) says <c>inSpaceNow: true</c> for an Account that made a
    /// request here within the window, <c>false</c> for one whose presence here has expired, and <c>false</c> for one present
    /// only in another Space it belongs to. The row carries exactly the list row's members and <c>inSpaceNow</c>, the list's
    /// rows agree, and a Member reading the row is refused <c>403</c> naming <c>change-membership-role</c>. Members are seeded
    /// directly, so no accept makes them present.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR36")]
    [Trait("Requirement", "AD-27")]
    [Trait("Requirement", "AD-23")]
    public async Task The_single_row_read_says_in_this_Space_now_only_for_an_unexpired_presence_here()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, presentAddress, expiredAddress, elsewhereAddress) = (RandomAddress(), RandomAddress(), RandomAddress(), RandomAddress());
        var (spaceId, otherSpaceId) = (Guid.NewGuid(), Guid.NewGuid());
        var read = new Dictionary<string, WireResponse>(StringComparer.Ordinal);
        var ids = new Dictionary<string, Guid>(StringComparer.Ordinal);
        WireResponse? list = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Presence read " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            var present = await SignedInAsync(client, presentAddress);
            var presentId = await AccountIdOfAsync(database, presentAddress);
            await wire.AddMembershipAsync(spaceId, presentId, "member");

            _ = await SignedInAsync(client, expiredAddress);
            var expiredId = await AccountIdOfAsync(database, expiredAddress);
            await wire.AddMembershipAsync(spaceId, expiredId, "member");

            var elsewhere = await SignedInAsync(client, elsewhereAddress);
            var elsewhereId = await AccountIdOfAsync(database, elsewhereAddress);
            await wire.AddMembershipAsync(spaceId, elsewhereId, "viewer");
            await SeedNamedSpaceAsync(database, otherSpaceId, "Present elsewhere " + Suffix(), elsewhereId, Role.Owner);

            Assert.Equal(HttpStatusCode.OK, (await present.GetAsync(SpaceRoutes.ActiveOf(spaceId))).StatusCode);
            await RecordPresenceAsync(database, spaceId, [expiredId], DateTimeOffset.UtcNow.AddMinutes(-1));
            Assert.Equal(HttpStatusCode.OK, (await elsewhere.GetAsync(SpaceRoutes.ActiveOf(otherSpaceId))).StatusCode);

            foreach (var (name, accountId) in new[] { ("present", presentId), ("expired", expiredId), ("elsewhere", elsewhereId) })
            {
                ids[name] = (await wire.MembershipOfAsync(spaceId, accountId)).Id;
                read[name] = await ReadMembershipOverWireAsync(owner, spaceId, ids[name]);
            }

            list = await owner.SendWithHeadersAsync(HttpMethod.Get, MembershipRoutes.MembershipsOf(spaceId), content: null, NoKey, withAntiforgeryToken: false);
            read["member's read"] = await ReadMembershipOverWireAsync(present, spaceId, ids["expired"]);
        });

        foreach (var (name, inSpaceNow) in new[] { ("present", true), ("expired", false), ("elsewhere", false) })
        {
            Assert.True(read[name].StatusCode == HttpStatusCode.OK, $"The Owner's read of the {name} row: {Described(read[name])}");
            Assert.Equal(MembershipWire.RowMembers, MembershipWire.ObjectMemberNames(read[name].Body));
            Assert.True(MembershipWire.ParseReadRow(read[name].Body).InSpaceNow == inSpaceNow, $"The {name} row should say inSpaceNow: {inSpaceNow}: {read[name].Body}");
        }

        Assert.True(list?.StatusCode == HttpStatusCode.OK, $"The list: {Described(list)}");

        using (var page = JsonDocument.Parse(list.Body))
        {
            var listed = page.RootElement.GetProperty("memberships").EnumerateArray()
                .ToDictionary(row => row.GetProperty("id").GetGuid(), row => row.GetProperty("inSpaceNow").GetBoolean());

            Assert.Equal((true, false, false), (listed[ids["present"]], listed[ids["expired"]], listed[ids["elsewhere"]]));
        }

        Assert.True(read["member's read"].StatusCode == HttpStatusCode.Forbidden, $"A Member's read: {Described(read["member's read"])}");
        Assert.Equal(MembershipWire.CapabilityCode, CapabilityOf(read["member's read"]));
    }

    /// <summary>
    /// A <c>DELETE</c> of a Membership is narrowed to the Space whose key the connection carries, by the FILTER predicate
    /// alone: under Space A's key, a raw <c>DELETE</c> of a Space B Membership's id deletes nothing and B's row is still
    /// there, while the same statement for an A Membership deletes one row. No migration is needed for the removal's
    /// isolation for this reason; a <c>BLOCK BEFORE DELETE</c> predicate would add nothing here. <b>Green today, left
    /// active</b> as the standing proof. Seeded in one unit per Space under its own key (both Spaces, three Accounts,
    /// <c>FK_Membership_Space_SpaceId</c>, <c>FK_Membership_Account_AccountId</c>, <c>AK_Membership_SpaceId_AccountId</c>,
    /// one Owner each for <c>UX_Membership_SpaceId_Owner</c>).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "FR-14")]
    public async Task A_Membership_DELETE_under_one_Spaces_key_deletes_nothing_in_another_Space()
    {
        var database = Available();
        var wire = Wire();
        var (spaceA, spaceB) = (Guid.NewGuid(), Guid.NewGuid());
        var (ownerA, ownerB, member) = (SpaceSeeding.NewAccount(), SpaceSeeding.NewAccount(), SpaceSeeding.NewAccount());
        var (memberOfA, memberOfB) = (SpaceSeeding.NewMembership(spaceA, member.Id, Role.Member), SpaceSeeding.NewMembership(spaceB, member.Id, Role.Member));
        var cancellationToken = TestContext.Current.CancellationToken;

        await SpaceSeeding.SeedAsync(database, spaceA, context =>
        {
            context.Spaces.Add(NamedSpace(spaceA, "Delete here " + Suffix()));
            context.Accounts.Add(ownerA);
            context.Accounts.Add(member);
            context.Memberships.Add(SpaceSeeding.NewMembership(spaceA, ownerA.Id, Role.Owner));
            context.Memberships.Add(memberOfA);
        }, cancellationToken);

        await SpaceSeeding.SeedAsync(database, spaceB, context =>
        {
            context.Spaces.Add(NamedSpace(spaceB, "Delete there " + Suffix()));
            context.Accounts.Add(ownerB);
            context.Memberships.Add(SpaceSeeding.NewMembership(spaceB, ownerB.Id, Role.Owner));
            context.Memberships.Add(memberOfB);
        }, cancellationToken);

        await using var underA = await database.OpenForSpaceAsync(spaceA, cancellationToken);

        async Task<int> DeleteAsync(Guid membershipId)
        {
            await using var command = underA.CreateCommand();

            command.CommandText = "DELETE FROM dbo.[Membership] WHERE Id = @id";
            command.Parameters.Add(new SqlParameter("@id", membershipId));

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        Assert.Equal(0, await DeleteAsync(memberOfB.Id));
        Assert.Equal(member.Id, (await wire.MembershipOfAsync(spaceB, member.Id)).AccountId);
        Assert.Equal(1, await DeleteAsync(memberOfA.Id));
        Assert.DoesNotContain(await wire.MembershipRowsInAsync(spaceA), row => row.AccountId == member.Id);
        await AssertOneOwnerEachAsync(wire, spaceA, spaceB);
    }

    /// <summary>
    /// The removal as the browser sends it: the Session's cookie and anti-forgery header, no body, and an
    /// <c>Idempotency-Key</c> when one is given.
    /// </summary>
    private static Task<WireResponse> RemoveOverWireAsync(WireSession session, Guid spaceId, Guid membershipId, string? key = null) =>
        session.SendWithHeadersAsync(HttpMethod.Delete, MembershipRoutes.MembershipOf(spaceId, membershipId), content: null, KeyHeaders(key));

    /// <summary>
    /// The single-row read the removal confirm sends when it opens.
    /// </summary>
    private static Task<WireResponse> ReadMembershipOverWireAsync(WireSession session, Guid spaceId, Guid membershipId) =>
        session.SendWithHeadersAsync(HttpMethod.Get, MembershipRoutes.MembershipOf(spaceId, membershipId), content: null, NoKey, withAntiforgeryToken: false);

    /// <summary>
    /// How many presence rows, expired or not, an Account has in a Space, read under that Space's key: the table's FILTER
    /// predicate hides every other Space's rows.
    /// </summary>
    private static async Task<int> PresenceRowsAsync(RegistrationDatabase database, Guid spaceId, Guid accountId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT COUNT(*) FROM dbo.[SpacePresence] WHERE SpaceId = @space AND AccountId = @account";
        command.Parameters.Add(new SqlParameter("@space", spaceId));
        command.Parameters.Add(new SqlParameter("@account", accountId));

        return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture);
    }
}
