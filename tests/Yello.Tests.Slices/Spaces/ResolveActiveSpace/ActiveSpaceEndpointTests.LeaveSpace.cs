using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Memberships;
using Yello.Contracts.Spaces;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Leaving a Space over the wire: <c>DELETE /api/v1/spaces/{spaceId}/membership</c>, the caller's own Membership, answered
/// <c>204</c> with no body. Leaving is decided by identity, not by Role, so the route declares the capability every Role
/// holds and the statement deletes the caller's own row unless it is the Owner's.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every refusal is anchored</b> on a <c>204</c> from the same route first, so a refusal asserted alone cannot
/// pass against a route that answers the boundary <c>404</c> to everything.
/// </para>
/// <para>
/// <b>The replay is the boundary <c>404</c></b>, not a replayed <c>204</c>: after a committed leave the caller has no
/// Membership, so the pipeline refuses before the idempotency record is consulted, as it does after a Space deletion.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// An Admin, a Member and a Viewer each leave: <c>204</c> with no body, the row gone, its presence here gone, and the
    /// Account keeps everything else - it signs in again, the switcher lists its own Space and its other Membership but not
    /// this one, and a Project it created here (Admin and Member) is still the Space's. One Owner left.
    /// </summary>
    /// <param name="role">The leaver's Role.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("admin")]
    [InlineData("member")]
    [InlineData("viewer")]
    [Trait("Requirement", "FR-14")]
    public async Task A_non_Owner_leaves_and_keeps_its_Account_its_own_Space_and_its_other_Memberships(string role)
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, leaverAddress, otherOwnerAddress) = (RandomAddress(), RandomAddress(), RandomAddress());
        var (spaceId, otherSpaceId, ownSpaceId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var leaverId = Guid.Empty;
        var projectId = Guid.Empty;
        WireResponse? left = null;
        WireResponse? next = null;
        WireResponse? signedInAgain = null;
        WireResponse? switcher = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Left " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);
            _ = await SignedInAsync(client, otherOwnerAddress);
            await SeedNamedSpaceAsync(database, otherSpaceId, "Kept " + Suffix(), await AccountIdOfAsync(database, otherOwnerAddress), Role.Owner);

            var leaver = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), leaverAddress, DisplayName(), role);
            leaverId = await AccountIdOfAsync(database, leaverAddress);

            // Directly: the mail drop answers the first email to an address, and this is the Account's second Membership.
            await wire.AddMembershipAsync(otherSpaceId, leaverId, "member");
            await SeedNamedSpaceAsync(database, ownSpaceId, "Own " + Suffix(), leaverId, Role.Owner);

            if (role != "viewer")
            {
                projectId = await CreateProjectOverWireAsync(leaver, spaceId);
            }

            Assert.Equal(HttpStatusCode.OK, (await leaver.GetAsync(SpaceRoutes.ActiveOf(spaceId))).StatusCode);

            left = await LeaveOverWireAsync(leaver, spaceId);
            next = await leaver.GetAsync(SpaceRoutes.ActiveOf(spaceId));

            var again = new WireSession(client);

            await again.FetchAntiforgeryTokenAsync();
            signedInAgain = await again.SignInAsync(leaverAddress, Password);
            switcher = await again.GetAsync(SpaceRoutes.Directory);
        });

        Assert.True(left?.StatusCode == HttpStatusCode.NoContent, $"The {role}'s leave: {Described(left)}");
        Assert.Equal(string.Empty, left.Body);
        Assert.DoesNotContain(await wire.MembershipRowsInAsync(spaceId), row => row.AccountId == leaverId);
        Assert.Equal(0, await PresenceRowsAsync(database, spaceId, leaverId));
        Assert.True(next?.StatusCode == HttpStatusCode.NotFound, $"The next request in the Space left: {Described(next)}");
        Assert.True(signedInAgain?.StatusCode == HttpStatusCode.NoContent, $"Signing in again: {Described(signedInAgain)}");
        Assert.True(switcher?.StatusCode == HttpStatusCode.OK, $"The switcher: {Described(switcher)}");

        using (var listed = JsonDocument.Parse(switcher.Body))
        {
            var ids = listed.RootElement.EnumerateArray().Select(space => space.GetProperty("id").GetGuid()).ToList();

            Assert.Contains(otherSpaceId, ids);
            Assert.Contains(ownSpaceId, ids);
            Assert.DoesNotContain(spaceId, ids);
        }

        if (role != "viewer")
        {
            Assert.Contains(await wire.ProjectsInAsync(spaceId), project => project.Id == projectId);
        }

        await AssertOneOwnerEachAsync(wire, spaceId, otherSpaceId, ownSpaceId);
    }

    /// <summary>
    /// The Owner cannot leave while it holds ownership: <c>409 ownership-held</c>, its row untouched, nothing recorded, no
    /// address in the body, one Owner left. Its exits are an Ownership Offer accepted or deleting the Space. The anchor, a
    /// Member leaving on the same route, answers <c>204</c> first.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "AD-5")]
    public async Task The_Owner_leaving_is_refused_409_ownership_held_and_keeps_the_Space()
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
            await SeedNamedSpaceAsync(database, spaceId, "Owner cannot leave " + Suffix(), ownerId, Role.Owner);

            var member = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), memberAddress, DisplayName(), "member");

            anchor = await LeaveOverWireAsync(member, spaceId);

            await owner.FetchAntiforgeryTokenAsync();
            refused = await LeaveOverWireAsync(owner, spaceId);
        });

        Assert.True(anchor?.StatusCode == HttpStatusCode.NoContent, $"The Member's anchor: {Described(anchor)}");
        Assert.True(refused?.StatusCode == HttpStatusCode.Conflict, $"The Owner's leave: {Described(refused)}; the anchor was {Described(anchor)}");
        Assert.Equal(ProblemTypes.OwnershipHeld, ProblemType(refused));
        Assert.DoesNotContain("@", refused.Body, StringComparison.Ordinal);
        Assert.Equal("Owner", (await wire.MembershipOfAsync(spaceId, ownerId)).Role);
        Assert.DoesNotContain(await RefusalsOfAsync(database, ownerId), row => row.TargetSpaceId == spaceId);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// A Token may leave its own Space, as a Session may: <c>204</c>. The cascade deletes the Token with the Membership in
    /// the request's own transaction, so the Token's next request is the no-credential <c>401</c>, byte for byte none at
    /// all, and the same Account's Session meets the boundary <c>404</c> there.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "AD-4")]
    public async Task A_Token_leaves_its_own_Space_and_its_next_request_is_the_no_credential_401()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, memberAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var memberId = Guid.Empty;
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Token leaves " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            var member = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), memberAddress, DisplayName(), "member");
            memberId = await AccountIdOfAsync(database, memberAddress);

            var token = await BearerForAsync(member, spaceId);

            answers["before"] = await member.GetWithTokenAsync(SpaceRoutes.ActiveOf(spaceId), token);
            answers["leave"] = await member.SendWithTokenAndHeadersAsync(HttpMethod.Delete, MembershipRoutes.OwnOf(spaceId), token, content: null, NoKey);
            answers["token next"] = await member.GetWithTokenAsync(SpaceRoutes.ActiveOf(spaceId), token);
            answers["no credential"] = await new WireSession(client).GetAnonymouslyAsync(SpaceRoutes.ActiveOf(spaceId));
            answers["session next"] = await member.GetAsync(SpaceRoutes.ActiveOf(spaceId));
        });

        Assert.True(answers["before"].StatusCode == HttpStatusCode.OK, $"The Token before: {Described(answers["before"])}");
        Assert.True(answers["leave"].StatusCode == HttpStatusCode.NoContent, $"The Token's leave: {Described(answers["leave"])}");
        Assert.True(answers["token next"].StatusCode == HttpStatusCode.Unauthorized, $"The Token's next request: {Described(answers["token next"])}");
        Assert.Equal(answers["no credential"].Body, answers["token next"].Body);
        Assert.Equal(answers["no credential"].HeaderNames, answers["token next"].HeaderNames);
        Assert.True(answers["session next"].StatusCode == HttpStatusCode.NotFound, $"The Session's next request: {Described(answers["session next"])}");
        Assert.Equal(0, await wire.TokenCountAsync(memberId, spaceId));
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// A leave replayed under its own key after it committed is the boundary <c>404</c>, byte for byte the same route on a
    /// Space that does not exist, and recorded <c>CrossSpace</c>: the caller has no Membership left for the pipeline to
    /// resolve, so the record is never consulted. The same recorded variance as a replayed Space deletion; the client reads a
    /// <c>404</c> on a same-key resend of a leave as "left".
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "AD-20")]
    public async Task A_leave_replayed_under_its_key_after_it_committed_is_the_boundary_404_and_recorded()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, memberAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var memberId = Guid.Empty;
        var key = Guid.NewGuid().ToString("D");
        var crossSpaceBefore = 0;
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Leave replayed " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            var member = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), memberAddress, DisplayName(), "member");
            memberId = await AccountIdOfAsync(database, memberAddress);
            crossSpaceBefore = (await RefusalsOfAsync(database, memberId)).Count(row => row.TargetSpaceId == spaceId && row.Kind == "CrossSpace");

            answers["original"] = await LeaveOverWireAsync(member, spaceId, key);
            answers["replay"] = await LeaveOverWireAsync(member, spaceId, key);
            answers["absent Space"] = await LeaveOverWireAsync(member, Guid.NewGuid(), key);
        });

        Assert.True(answers["original"].StatusCode == HttpStatusCode.NoContent, $"The original: {Described(answers["original"])}");
        Assert.True(answers["replay"].StatusCode == HttpStatusCode.NotFound, $"The replay: {Described(answers["replay"])}");
        Assert.Equal(ProblemTypes.NotAvailable, ProblemType(answers["replay"]));
        Assert.Equal(answers["absent Space"].Body, answers["replay"].Body);
        Assert.Equal(answers["absent Space"].HeaderNames, answers["replay"].HeaderNames);
        Assert.Equal(crossSpaceBefore + 1, (await RefusalsOfAsync(database, memberId)).Count(row => row.TargetSpaceId == spaceId && row.Kind == "CrossSpace"));
    }

    /// <summary>
    /// A committed leave publishes <c>MembershipChanged</c> once, for the leaver and this Space, and the Owner's refused
    /// leave publishes nothing: the run holds exactly one publish line. Seeded directly, so no accept's publish is counted.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-16")]
    [Trait("Requirement", "AD-9")]
    public async Task A_committed_leave_logs_one_MembershipChanged_and_a_refused_leave_none()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, memberAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var memberId = Guid.Empty;
        WireResponse? left = null;
        WireResponse? refused = null;

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Leave published " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);
            var member = await SignedInAsync(client, memberAddress);
            memberId = await AccountIdOfAsync(database, memberAddress);
            await wire.AddMembershipAsync(spaceId, memberId, "member");

            await owner.FetchAntiforgeryTokenAsync();
            await member.FetchAntiforgeryTokenAsync();
            refused = await LeaveOverWireAsync(owner, spaceId);
            left = await LeaveOverWireAsync(member, spaceId);
        });

        var publishes = log.Split('\n').Where(line => line.Contains(MembershipWire.PublishedEvent, StringComparison.Ordinal)).ToList();

        Assert.True(refused?.StatusCode == HttpStatusCode.Conflict, $"The Owner's leave: {Described(refused)}");
        Assert.True(left?.StatusCode == HttpStatusCode.NoContent, $"The Member's leave: {Described(left)}");
        Assert.True(publishes.Count == 1, $"Expected exactly one publish line; found {publishes.Count}: {string.Join(" | ", publishes)}");
        Assert.Equal(1, PublishesNaming(log, memberId, spaceId));
    }

    /// <summary>
    /// The leave as the browser sends it: the Session's cookie and anti-forgery header, no body, and an
    /// <c>Idempotency-Key</c> when one is given.
    /// </summary>
    private static Task<WireResponse> LeaveOverWireAsync(WireSession session, Guid spaceId, string? key = null) =>
        session.SendWithHeadersAsync(HttpMethod.Delete, MembershipRoutes.OwnOf(spaceId), content: null, KeyHeaders(key));
}
