using System.Data.SqlTypes;
using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Invitations;
using Yello.Contracts.Spaces;
using Yello.Domain.Accounts;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 4.2 over the wire: the list of a Space's pending Invitations, <c>GET /api/v1/spaces/{spaceId}/invitations</c>, the
/// same path as 4.1's issue, authorised by <c>Capability.IssueAndRevokeInvitations</c> (Q16 (a)). AC1, AC5, AC6; FR-12,
/// FR-10's retention clause, UX-DR14, AR-31/AD-27, AD-23 and section 6.1.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 4.2 ATDD run (2026-11-01)</b>, each skipped with the AC it waited for;
/// activated by the dev phase once <c>GET</c> on the path was mapped. The row shape is the
/// harness's <see cref="ListedInvitation"/> until <c>InvitationResponse</c> exists.
/// </para>
/// <para>
/// <b>Seeding.</b> Accounts are registered through the product; Spaces are seeded by <see cref="SpaceSeeding"/> with one
/// Membership (<c>FK_Membership_Space_SpaceId</c>, <c>FK_Membership_Account_AccountId</c>,
/// <c>AK_Membership_SpaceId_AccountId</c>, <c>BLOCK AFTER INSERT</c>), and a second member through
/// <see cref="WireDatabase.AddMembershipAsync"/> (never an Owner, so <c>UX_Membership_SpaceId_Owner</c> holds). Live
/// pending Invitations are issued through 4.1's route. Only the terminal and expired rows - which no endpoint reaches yet -
/// are written by <see cref="WireDatabase.AddInvitationAsync"/> and, for a terminal state,
/// <see cref="WireDatabase.SetInvitationStateAsync"/>, whose constraints are: <c>FK_Invitation_Space_SpaceId</c> (the Space is seeded first), <c>FK_Invitation_Account_IssuedByAccountId</c>
/// (the issuer is an Account the product registered), <c>UX_Invitation_TokenHash</c> (32 random bytes per row),
/// <c>State</c> <c>nvarchar(16)</c> holding a member name, and the <c>BLOCK AFTER INSERT</c> predicate (the connection carries
/// the Space's key). All checked against migration <c>20261101140742_Invitations</c>.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// AC1, Q5 (a), Q8 (a), UX-DR14 - an Admin's list shows every pending Invitation in the Space, the Owner's as well as
    /// its own: <c>200</c>, a JSON array whose rows carry exactly <c>id</c>, <c>emailAddress</c>, <c>role</c> (the stable
    /// code), <c>issuerName</c> (captured at issue) and <c>issuedAt</c> (the row's <c>CreatedAt</c>), newest first with the
    /// id as the tie-break in the database's own <c>uniqueidentifier</c> order.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-12")]
    [Trait("Requirement", "UX-DR14")]
    public async Task The_list_shows_each_pending_Invitations_address_Role_issuer_and_date_newest_first()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, adminAddress) = (RandomAddress(), RandomAddress());
        var (ownerName, adminName) = (DisplayName(), DisplayName());
        var (fromOwner, fromAdmin) = (InvitationWire.FreshAddress("from-owner"), InvitationWire.FreshAddress("from-admin"));
        var spaceId = Guid.NewGuid();
        WireResponse? listed = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await RegisteredAndSignedInAsync(client, ownerAddress, ownerName);
            await SeedNamedSpaceAsync(database, spaceId, "Listing " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            var admin = await RegisteredAndSignedInAsync(client, adminAddress, adminName);
            await wire.AddMembershipAsync(spaceId, await AccountIdOfAsync(database, adminAddress), "admin");

            await owner.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(owner, spaceId, fromOwner, "member")).StatusCode);

            await admin.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(admin, spaceId, fromAdmin, "viewer")).StatusCode);

            listed = await ListInvitationsAsync(admin, spaceId);
        });

        Assert.True(listed?.StatusCode == HttpStatusCode.OK, $"{listed?.StatusCode} {listed?.Body}");
        Assert.All(InvitationWire.ListMemberNames(listed!.Body), names => Assert.Equal(InvitationWire.ListedMembers, names));

        var rows = await wire.InvitationsInAsync(spaceId);
        var expected = rows
            .OrderByDescending(row => row.CreatedAt)
            .ThenByDescending(row => new SqlGuid(row.Id))
            .Select(row => new ListedInvitation(row.Id, row.EmailAddress, RoleCodeOfStored(row.Role), row.IssuerName, row.CreatedAt))
            .ToList();

        Assert.Equal(2, expected.Count);
        Assert.Equal(expected, InvitationWire.ParseList(listed.Body));
        Assert.Equal(
            [(fromOwner, "member", ownerName), (fromAdmin, "viewer", adminName)],
            expected.Select(row => (row.EmailAddress, row.Role, row.IssuerName)).OrderBy(row => row.EmailAddress == fromAdmin));
    }

    /// <summary>
    /// AC6, AC5, Q4 (a), AD-27 - the list is what is outstanding: of one live pending, one revoked, one accepted and one
    /// expired Invitation in the Space, it returns exactly the live one (the anchor, so an empty list cannot pass), and
    /// names none of the other three addresses. Reading the list writes nothing: the expired row is still stored
    /// <c>Pending</c> and all four rows remain. Plant 1: the list without <c>OfferPredicates.Actionable</c> returns four.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR14")]
    [Trait("Requirement", "FR-10")]
    [Trait("Requirement", "AR-31")]
    [Trait("Requirement", "AD-27")]
    public async Task The_list_shows_only_live_pending_Invitations_and_never_revoked_accepted_or_expired_ones()
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var live = InvitationWire.FreshAddress("live");
        var (revoked, accepted, expired) = (InvitationWire.FreshAddress("revoked"), InvitationWire.FreshAddress("accepted"), InvitationWire.FreshAddress("expired"));
        var spaceId = Guid.NewGuid();
        WireResponse? listed = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            var ownerId = await AccountIdOfAsync(database, address);
            await SeedNamedSpaceAsync(database, spaceId, "Outstanding " + Suffix(), ownerId, Role.Owner);

            await session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(session, spaceId, live, "member")).StatusCode);

            await SeedTerminalInvitationAsync(wire, spaceId, ownerId, revoked, "Revoked");
            await SeedTerminalInvitationAsync(wire, spaceId, ownerId, accepted, "Accepted");
            await SeedTerminalInvitationAsync(wire, spaceId, ownerId, expired, "Expired");

            listed = await ListInvitationsAsync(session, spaceId);
        });

        Assert.True(listed?.StatusCode == HttpStatusCode.OK, $"{listed?.StatusCode} {listed?.Body}");
        Assert.Equal([live], InvitationWire.ParseList(listed!.Body).Select(row => row.EmailAddress));

        foreach (var terminal in new[] { revoked, accepted, expired })
        {
            Assert.DoesNotContain(terminal, listed.Body, StringComparison.OrdinalIgnoreCase);
        }

        var rows = await wire.InvitationsInAsync(spaceId);

        Assert.Equal(4, rows.Count);
        Assert.Equal("Pending", Assert.Single(rows, row => row.EmailAddress == expired).State);
    }

    /// <summary>
    /// AC1, AC6 - a Space with no Invitation lists <c>[]</c>, not a <c>404</c> or a <c>204</c>; anchored in the same case:
    /// once one is issued, the same caller's list holds exactly it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR14")]
    public async Task The_list_is_empty_for_a_Space_with_no_Invitations()
    {
        var database = Available();
        var address = RandomAddress();
        var invitee = InvitationWire.FreshAddress("first");
        var spaceId = Guid.NewGuid();
        WireResponse? empty = null;
        WireResponse? one = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedNamedSpaceAsync(database, spaceId, "Empty " + Suffix(), await AccountIdOfAsync(database, address), Role.Owner);

            empty = await ListInvitationsAsync(session, spaceId);

            await session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(session, spaceId, invitee, "admin")).StatusCode);

            one = await ListInvitationsAsync(session, spaceId);
        });

        Assert.True(empty?.StatusCode == HttpStatusCode.OK, $"{empty?.StatusCode} {empty?.Body}");
        Assert.Equal("[]", empty!.Body.Trim());
        Assert.Equal(HttpStatusCode.OK, one?.StatusCode);
        Assert.Equal([invitee], InvitationWire.ParseList(one!.Body).Select(row => row.EmailAddress));
    }

    /// <summary>
    /// AC1, Q16 (a) [SECURITY], AD-23, section 6.1 - the list carries email addresses, so it is an Owner-and-Admin read: a
    /// Member's and a Viewer's <c>GET</c> is <c>403 insufficient-role</c> naming <c>issue-and-revoke-invitations</c>, with
    /// one <c>InsufficientRole</c> record, and the body names no address. Anchor: the same caller reads the Space (<c>200</c>)
    /// and the Space holds a pending Invitation. Plant 6: the list under <c>ReadProjectsAndTasks</c> answers <c>200</c>.
    /// </summary>
    /// <param name="role">The caller's Role.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(Role.Member)]
    [InlineData(Role.Viewer)]
    [Trait("Requirement", "FR-12")]
    [Trait("Requirement", "AD-23")]
    [Trait("Requirement", "AR-11")]
    public async Task A_Member_or_a_Viewer_listing_Invitations_is_refused_403_and_sees_no_address(Role role)
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var invitee = InvitationWire.FreshAddress("hidden");
        var spaceId = Guid.NewGuid();
        var accountId = Guid.Empty;
        WireResponse? read = null;
        WireResponse? refused = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            accountId = await AccountIdOfAsync(database, address);
            await SeedNamedSpaceAsync(database, spaceId, "Hidden " + Suffix(), accountId, role);

            var now = await wire.DatabaseNowAsync();

            await wire.AddInvitationAsync(spaceId, accountId, "Seeder", invitee, EmailAddressNormalisation.Normalise(invitee), "Member", now.AddDays(InvitationWire.ExpiryDays));

            read = await session.GetAsync(SpaceRoutes.ActiveOf(spaceId));
            refused = await ListInvitationsAsync(session, spaceId);
        });

        Assert.Equal(HttpStatusCode.OK, read?.StatusCode);
        Assert.True(refused?.StatusCode == HttpStatusCode.Forbidden, $"{refused?.StatusCode} {refused?.Body}");

        var problem = JsonSerializer.Deserialize<ProblemResponse>(refused!.Body, JsonOptions);

        Assert.Equal(ProblemTypes.InsufficientRole, problem?.Type);
        Assert.Equal(InvitationWire.CapabilityCode, problem?.Capability);
        Assert.DoesNotContain("@", refused.Body, StringComparison.Ordinal);

        var recorded = Assert.Single(await RefusalsOfAsync(database, accountId));

        Assert.Equal((spaceId, "InsufficientRole", "IssueAndRevokeInvitations"), (recorded.TargetSpaceId, recorded.Kind, recorded.Capability));
    }

    /// <summary>
    /// The list as the browser asks for it: the Session's cookie, a <c>GET</c> on the Space's Invitations path.
    /// </summary>
    private static Task<WireResponse> ListInvitationsAsync(WireSession session, Guid spaceId) =>
        session.GetAsync(InvitationRoutes.InvitationsOf(spaceId));

    /// <summary>
    /// The wire's Role code for a Role as the table stores it (its member name): the form 4.1's request and 4.2's list use.
    /// </summary>
    private static string RoleCodeOfStored(string stored) => stored switch
    {
        "Admin" => "admin",
        "Member" => "member",
        "Viewer" => "viewer",
        _ => throw new ArgumentOutOfRangeException(nameof(stored), stored, "An Invitation offers Admin, Member or Viewer."),
    };

    /// <summary>
    /// A row no endpoint can yet produce, written directly (R12): <c>Revoked</c> or <c>Accepted</c> with a live expiry, or
    /// <c>Expired</c> - stored <c>Pending</c> with an <c>ExpiresAt</c> a minute past by the database clock (AD-27: expired is
    /// never written). The issuer is a registered Account (<c>FK_Invitation_Account_IssuedByAccountId</c>).
    /// </summary>
    private static async Task<Guid> SeedTerminalInvitationAsync(WireDatabase wire, Guid spaceId, Guid issuerId, string address, string terminal)
    {
        var now = await wire.DatabaseNowAsync();
        var (state, expiresAt) = terminal switch
        {
            "Expired" => ("Pending", now.AddMinutes(-1)),
            "Revoked" or "Accepted" => (terminal, now.AddDays(InvitationWire.ExpiryDays)),
            _ => throw new ArgumentOutOfRangeException(nameof(terminal), terminal, "Revoked, Accepted or Expired."),
        };

        var id = await wire.AddInvitationAsync(spaceId, issuerId, "Seeder", address, EmailAddressNormalisation.Normalise(address), "Member", expiresAt);

        if (state != "Pending")
        {
            await wire.SetInvitationStateAsync(spaceId, id, state);
        }

        return id;
    }
}
