using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Invitations;
using Yello.Contracts.Spaces;
using Yello.Domain.Accounts;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 4.2 over the wire: revoking an Invitation, <c>DELETE /api/v1/spaces/{spaceId}/invitations/{invitationId}</c>
/// (Q1 (a)), authorised by <c>Capability.IssueAndRevokeInvitations</c>, answered <c>204</c>, or <c>409
/// invitation-not-pending</c> for an accepted, revoked or expired Invitation (Q2 (a)), or the unrecorded boundary <c>404</c>
/// for an id that names nothing in this Space (Q3 (a)). AC2, AC3, AC4, AC5, AC7, AC10; FR-12, FR-10, FR-39, AD-5, AD-18,
/// AD-26, AD-27, NFR-6, I-17.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 4.2 ATDD run (2026-11-01).</b> Every case was skipped with the AC it
/// waited for; the dev phase activated each one, saw it red for that reason, and swapped the ATDD stand-ins for
/// <see cref="InvitationRoutes.InvitationOf"/> and <see cref="ProblemTypes.InvitationNotPending"/>.
/// </para>
/// <para>
/// <b>Seeding</b> is as in <c>.ListInvitations.cs</c>: live Invitations through 4.1's issue route, terminal and expired
/// rows through <see cref="WireDatabase.AddInvitationAsync"/> and <see cref="WireDatabase.SetInvitationStateAsync"/>,
/// the issuer's demotion through the product - joined as Admin through an Invitation accepted (4.3), demoted by the Owner's
/// <c>PATCH .../memberships/{id}</c> (story 4.4, AC8) - and its removal through <see cref="WireDatabase.RemoveMembershipAsync"/>
/// (Q12 (a): story 4.5 builds that path). The removal deletes
/// a Membership only; the <c>Invitation</c> has no foreign key to <c>Membership</c>, and its <c>IssuedByAccountId</c> key to
/// <c>Account</c> is untouched (AD-5).
/// </para>
/// <para>
/// <b>Not here:</b> the lock race (<c>.RevokeInvitationRace.cs</c>), the cross-Space and Role refusals on both surfaces (the
/// isolation rows in <c>IsolationCases.Invitations.cs</c>), and the Account-deletion arm of AC3 (ledgered to 5.4, Q12).
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// The terminal arms of AC4, as <see cref="SeedTerminalInvitationAsync"/> names them.
    /// </summary>
    public static TheoryData<string> TerminalInvitations => new() { "Accepted", "Revoked", "Expired" };

    /// <summary>
    /// AC2, FR-12, the matrix - any Owner or Admin revokes, including one who did not issue it: an Admin revokes the
    /// Owner's Invitation and the Owner revokes the Admin's, each <c>204</c> with no body, and each row is then
    /// <c>Revoked</c>. Plant 11: a revoke filtered on <c>IssuedByAccountId == caller</c> answers the first <c>409</c> or
    /// <c>404</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-12")]
    public async Task An_Admin_revokes_the_Owners_Invitation_and_the_Owner_revokes_the_Admins()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, adminAddress) = (RandomAddress(), RandomAddress());
        var (fromOwner, fromAdmin) = (InvitationWire.FreshAddress("owners"), InvitationWire.FreshAddress("admins"));
        var spaceId = Guid.NewGuid();
        WireResponse? byAdmin = null;
        WireResponse? byOwner = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Revoking " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            var admin = await SignedInAsync(client, adminAddress);
            await wire.AddMembershipAsync(spaceId, await AccountIdOfAsync(database, adminAddress), "admin");

            await owner.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(owner, spaceId, fromOwner, "member")).StatusCode);

            await admin.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(admin, spaceId, fromAdmin, "member")).StatusCode);

            byAdmin = await RevokeAsync(admin, spaceId, await InvitationIdOfAsync(wire, spaceId, fromOwner));
            byOwner = await RevokeAsync(owner, spaceId, await InvitationIdOfAsync(wire, spaceId, fromAdmin));
        });

        foreach (var answer in new[] { byAdmin, byOwner })
        {
            Assert.True(answer?.StatusCode == HttpStatusCode.NoContent, $"{answer?.StatusCode} {answer?.Body}");
            Assert.Equal(string.Empty, answer!.Body);
        }

        Assert.All(await wire.InvitationsInAsync(spaceId), row => Assert.Equal("Revoked", row.State));
    }

    /// <summary>
    /// AC5, FR-10's retention clause, SM-4, SM-C3 - a revoke is an <c>UPDATE</c>, never a <c>DELETE</c>: the row is still
    /// there afterwards, <c>State = Revoked</c>, with every other column unchanged - Space, address verbatim and
    /// normalised, Role, issuer and their captured name, <c>ExpiresAt</c>, <c>TokenHash</c> and <c>CreatedAt</c>. Plant 4:
    /// the revoke as <c>ExecuteDelete</c> leaves no row.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-10")]
    [Trait("Requirement", "FR-12")]
    [Trait("Requirement", "AD-27")]
    public async Task A_revoked_Invitation_is_retained_with_every_other_column_unchanged()
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var invitee = InvitationWire.FreshAddress("retained");
        var spaceId = Guid.NewGuid();
        InvitationRow? before = null;
        WireResponse? revoked = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedNamedSpaceAsync(database, spaceId, "Retained " + Suffix(), await AccountIdOfAsync(database, address), Role.Owner);

            await session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(session, spaceId, invitee, "viewer")).StatusCode);

            before = Assert.Single(await wire.InvitationsInAsync(spaceId));
            revoked = await RevokeAsync(session, spaceId, before.Id);
        });

        Assert.True(revoked?.StatusCode == HttpStatusCode.NoContent, $"{revoked?.StatusCode} {revoked?.Body}");

        var after = Assert.Single(await wire.InvitationsInAsync(spaceId));

        Assert.Equal("Pending", before!.State);
        Assert.Equal("Revoked", after.State);
        Assert.Equal(before with { State = "Revoked", TokenHash = after.TokenHash }, after);
        Assert.Equal(before.TokenHash, after.TokenHash);
    }

    /// <summary>
    /// AC2's "can no longer be accepted", Q11 (a) - before 4.3 exists, the revoke is proved through 4.1's AC13: re-issuing the
    /// address answers <c>409 invitation-pending</c> while the Invitation is pending, and <c>204</c> once it is revoked,
    /// because the revoked row no longer satisfies the shared actionable predicate - the predicate 4.3's acceptance composes.
    /// One row stays <c>Revoked</c> and the new one is <c>Pending</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-12")]
    [Trait("Requirement", "AR-31")]
    [Trait("Requirement", "AD-27")]
    public async Task After_a_revoke_the_same_address_can_be_invited_again()
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var invitee = InvitationWire.FreshAddress("again");
        var spaceId = Guid.NewGuid();
        WireResponse? blocked = null;
        WireResponse? revoked = null;
        WireResponse? reissued = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedNamedSpaceAsync(database, spaceId, "Again " + Suffix(), await AccountIdOfAsync(database, address), Role.Owner);

            await session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(session, spaceId, invitee, "member")).StatusCode);

            blocked = await IssueAsync(session, spaceId, invitee, "member");
            revoked = await RevokeAsync(session, spaceId, await InvitationIdOfAsync(wire, spaceId, invitee));
            reissued = await IssueAsync(session, spaceId, invitee, "member");
        });

        Assert.Equal(HttpStatusCode.Conflict, blocked?.StatusCode);
        Assert.Equal(ProblemTypes.InvitationPending, ProblemType(blocked!));
        Assert.True(revoked?.StatusCode == HttpStatusCode.NoContent, $"{revoked?.StatusCode} {revoked?.Body}");
        Assert.True(reissued?.StatusCode == HttpStatusCode.NoContent, $"{reissued?.StatusCode} {reissued?.Body}");
        Assert.Equal(["Pending", "Revoked"], (await wire.InvitationsInAsync(spaceId)).Select(row => row.State).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Story 4.4 AC8 (ledger <c>deferred-work.md:758</c>) - 4.2 AC3's demotion arm through the product's own route: the issuer
    /// joins as Admin through an Invitation at Admin, accepted (4.3), issues two Invitations, and is then demoted to Member by
    /// the Owner through <c>PATCH .../memberships/{id}</c> (<c>200</c>), not through <see cref="WireDatabase.ChangeRoleAsync"/>.
    /// The Owner still lists both with the issuer's name as captured at issue and revokes one (<c>204</c>); a second Admin,
    /// who issued neither, revokes the other (<c>204</c>).
    /// </summary>
    /// <remarks>
    /// Written as a red-phase scaffold by the story 4.4 ATDD run beside 4.2's raw-SQL case; the dev phase deleted that case
    /// once this one passed, and this one took its name. The removal arm below stays raw SQL until story 4.5.
    /// </remarks>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-12")]
    [Trait("Requirement", "FR-13")]
    [Trait("Requirement", "AD-5")]
    public async Task An_Invitation_whose_issuer_was_demoted_stays_listed_and_revocable_by_any_Owner_or_Admin()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, issuerAddress, otherAdminAddress) = (RandomAddress(), RandomAddress(), RandomAddress());
        var issuerName = DisplayName();
        var (first, second) = (InvitationWire.FreshAddress("demoted-1"), InvitationWire.FreshAddress("demoted-2"));
        var spaceId = Guid.NewGuid();
        var issuerId = Guid.Empty;
        WireResponse? demoted = null;
        WireResponse? listed = null;
        WireResponse? byOwner = null;
        WireResponse? byOtherAdmin = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Demoted by PATCH " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            var issuer = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), issuerAddress, issuerName, "admin");
            issuerId = await AccountIdOfAsync(database, issuerAddress);
            var otherAdmin = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), otherAdminAddress, DisplayName(), "admin");

            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(issuer, spaceId, first, "member")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(issuer, spaceId, second, "viewer")).StatusCode);

            await owner.FetchAntiforgeryTokenAsync();
            demoted = await ChangeRoleOverWireAsync(owner, spaceId, (await wire.MembershipOfAsync(spaceId, issuerId)).Id, "member");

            listed = await ListInvitationsAsync(owner, spaceId);
            byOwner = await RevokeAsync(owner, spaceId, await InvitationIdOfAsync(wire, spaceId, first));

            await otherAdmin.FetchAntiforgeryTokenAsync();
            byOtherAdmin = await RevokeAsync(otherAdmin, spaceId, await InvitationIdOfAsync(wire, spaceId, second));
        });

        Assert.True(demoted?.StatusCode == HttpStatusCode.OK, $"The Owner's demotion through the product: {Described(demoted)}");
        Assert.Equal("Member", (await wire.MembershipOfAsync(spaceId, issuerId)).Role);
        Assert.True(listed?.StatusCode == HttpStatusCode.OK, Described(listed));
        Assert.Equal(
            [(first, issuerName), (second, issuerName)],
            InvitationWire.ParseList(listed.Body).Where(row => row.EmailAddress == first || row.EmailAddress == second).Select(row => (row.EmailAddress, row.IssuerName)).OrderBy(row => row.EmailAddress == second));
        Assert.True(byOwner?.StatusCode == HttpStatusCode.NoContent, Described(byOwner));
        Assert.True(byOtherAdmin?.StatusCode == HttpStatusCode.NoContent, Described(byOtherAdmin));
        Assert.All(
            (await wire.InvitationsInAsync(spaceId)).Where(row => row.EmailAddress == first || row.EmailAddress == second),
            row => Assert.Equal("Revoked", row.State));
    }

    /// <summary>
    /// AC3, FR-12, AD-5, Q12 (a), the removal arm - an Admin issues, and their Membership is then removed: the Invitation row
    /// is untouched by the removal, the Owner still lists it with the captured issuer name, and revokes it (<c>204</c>).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-12")]
    [Trait("Requirement", "AD-5")]
    public async Task An_Invitation_whose_issuer_was_removed_stays_listed_and_revocable()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, issuerAddress) = (RandomAddress(), RandomAddress());
        var issuerName = DisplayName();
        var invitee = InvitationWire.FreshAddress("removed");
        var spaceId = Guid.NewGuid();
        InvitationRow? beforeRemoval = null;
        InvitationRow? afterRemoval = null;
        WireResponse? listed = null;
        WireResponse? revoked = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Removed " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            var issuer = await RegisteredAndSignedInAsync(client, issuerAddress, issuerName);
            var issuerId = await AccountIdOfAsync(database, issuerAddress);
            await wire.AddMembershipAsync(spaceId, issuerId, "admin");

            await issuer.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(issuer, spaceId, invitee, "member")).StatusCode);

            beforeRemoval = Assert.Single(await wire.InvitationsInAsync(spaceId));
            await wire.RemoveMembershipAsync(spaceId, issuerId, commit: true);
            afterRemoval = Assert.Single(await wire.InvitationsInAsync(spaceId));

            listed = await ListInvitationsAsync(owner, spaceId);

            await owner.FetchAntiforgeryTokenAsync();
            revoked = await RevokeAsync(owner, spaceId, afterRemoval.Id);
        });

        Assert.Equal(beforeRemoval! with { TokenHash = afterRemoval!.TokenHash }, afterRemoval);
        Assert.Equal(beforeRemoval.TokenHash, afterRemoval.TokenHash);
        Assert.True(listed?.StatusCode == HttpStatusCode.OK, $"{listed?.StatusCode} {listed?.Body}");
        Assert.Equal([(invitee, issuerName)], InvitationWire.ParseList(listed!.Body).Select(row => (row.EmailAddress, row.IssuerName)));
        Assert.True(revoked?.StatusCode == HttpStatusCode.NoContent, $"{revoked?.StatusCode} {revoked?.Body}");
        Assert.Equal("Revoked", Assert.Single(await wire.InvitationsInAsync(spaceId)).State);
    }

    /// <summary>
    /// Removing a Membership does not withdraw the offers it legitimately made. An Admin joins through an accepted
    /// Invitation, issues two, and is then removed by the Owner through the product's <c>DELETE</c>. Both Invitation rows
    /// are untouched by the removal (the issuer key references the Account, not the Membership); the Owner still lists them
    /// with the issuer name captured at issue and revokes one (<c>204</c>); the other's invitee accepts it (<c>200</c>) and
    /// becomes a Member. This is the product-route twin of the raw-SQL case above, which is deleted once this one is green,
    /// and this one then takes its name.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC5 - the issuer cannot be removed through the product yet; DELETE .../memberships/{membershipId} is not mapped")]
    [Trait("Requirement", "FR-12")]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "AD-5")]
    public async Task An_Invitation_whose_issuer_was_removed_through_the_product_stays_listed_revocable_and_acceptable()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, issuerAddress) = (RandomAddress(), RandomAddress());
        var issuerName = DisplayName();
        var (revokedInvitee, acceptedInvitee) = (InvitationWire.FreshAddress("removed-revoked"), InvitationWire.FreshAddress("removed-accepted"));
        var spaceId = Guid.NewGuid();
        IReadOnlyList<InvitationRow> beforeRemoval = [];
        IReadOnlyList<InvitationRow> afterRemoval = [];
        WireResponse? removed = null;
        WireResponse? listed = null;
        WireResponse? revoked = null;
        WireResponse? accepted = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Issuer removed " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            var issuer = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), issuerAddress, issuerName, "admin");

            await issuer.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(issuer, spaceId, revokedInvitee, "member")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(issuer, spaceId, acceptedInvitee, "viewer")).StatusCode);

            beforeRemoval = [.. (await wire.InvitationsInAsync(spaceId)).Where(row => row.EmailAddress == revokedInvitee || row.EmailAddress == acceptedInvitee).OrderBy(row => row.Id)];

            await owner.FetchAntiforgeryTokenAsync();
            removed = await RemoveOverWireAsync(owner, spaceId, (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, issuerAddress))).Id);

            afterRemoval = [.. (await wire.InvitationsInAsync(spaceId)).Where(row => row.EmailAddress == revokedInvitee || row.EmailAddress == acceptedInvitee).OrderBy(row => row.Id)];
            listed = await ListInvitationsAsync(owner, spaceId);
            revoked = await RevokeAsync(owner, spaceId, await InvitationIdOfAsync(wire, spaceId, revokedInvitee));

            var token = InvitationWire.TokenIn(await mail.WaitForEmailToAsync(acceptedInvitee));
            var invitee = await RegisteredAndSignedInAsync(client, acceptedInvitee, DisplayName());

            await invitee.FetchAntiforgeryTokenAsync();
            accepted = await AcceptAsync(invitee, token);
        });

        Assert.True(removed?.StatusCode == HttpStatusCode.NoContent, $"The issuer's removal through the product: {Described(removed)}");
        Assert.Equal(2, beforeRemoval.Count);

        // The token hash is an array, compared by its bytes; the rest of the row by value.
        static (InvitationRow Row, string Hash) ByValue(InvitationRow row) => (row with { TokenHash = Array.Empty<byte>() }, Convert.ToHexString(row.TokenHash));

        Assert.Equal(beforeRemoval.Select(ByValue), afterRemoval.Select(ByValue));
        Assert.True(listed?.StatusCode == HttpStatusCode.OK, $"{listed?.StatusCode} {listed?.Body}");
        Assert.Equal(
            [(acceptedInvitee, issuerName), (revokedInvitee, issuerName)],
            InvitationWire.ParseList(listed!.Body).Select(row => (row.EmailAddress, row.IssuerName)).Order());
        Assert.True(revoked?.StatusCode == HttpStatusCode.NoContent, $"{revoked?.StatusCode} {revoked?.Body}");
        Assert.True(accepted?.StatusCode == HttpStatusCode.OK, $"The removed issuer's Invitation, accepted: {Described(accepted)}");
        Assert.Equal("Viewer", (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, acceptedInvitee))).Role);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// AC4, AC5, FR-12, FR-14, Q2 (a), Q4 (a), AD-26, AD-27 - an accepted, an already-revoked and an expired Invitation are
    /// each refused <c>409 invitation-not-pending</c>: one type for all three, no address in the body, nothing recorded as a
    /// refusal, and the stored row unchanged - the accepted stays <c>Accepted</c>, the expired stays <c>Pending</c> with its
    /// <c>ExpiresAt</c>. Anchor: a live Invitation in the same Space is revoked <c>204</c>, so the route exists. Plant 2: the
    /// revoke without the actionable guard answers <c>204</c> for the accepted and the expired.
    /// </summary>
    /// <param name="terminal">Which terminal state: <c>Accepted</c>, <c>Revoked</c> or <c>Expired</c>.</param>
    /// <returns>A task.</returns>
    [Theory]
    [MemberData(nameof(TerminalInvitations))]
    [Trait("Requirement", "FR-12")]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "AD-27")]
    [Trait("Assumption", "PRD-12-9")]
    public async Task Revoking_an_Invitation_that_is_no_longer_pending_is_refused_409_and_changes_nothing(string terminal)
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var (live, done) = (InvitationWire.FreshAddress("live"), InvitationWire.FreshAddress(terminal.ToUpperInvariant()));
        var spaceId = Guid.NewGuid();
        var accountId = Guid.Empty;
        InvitationRow? before = null;
        WireResponse? anchor = null;
        WireResponse? refused = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            accountId = await AccountIdOfAsync(database, address);
            await SeedNamedSpaceAsync(database, spaceId, "Terminal " + Suffix(), accountId, Role.Owner);

            await session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(session, spaceId, live, "member")).StatusCode);

            var seeded = await SeedTerminalInvitationAsync(wire, spaceId, accountId, done, terminal);

            before = Assert.Single(await wire.InvitationsInAsync(spaceId), row => row.Id == seeded);
            anchor = await RevokeAsync(session, spaceId, await InvitationIdOfAsync(wire, spaceId, live));
            refused = await RevokeAsync(session, spaceId, seeded);
        });

        Assert.True(anchor?.StatusCode == HttpStatusCode.NoContent, $"{anchor?.StatusCode} {anchor?.Body}");
        Assert.True(refused?.StatusCode == HttpStatusCode.Conflict, $"{refused?.StatusCode} {refused?.Body}");
        Assert.Equal(ProblemTypes.InvitationNotPending, ProblemType(refused!));
        Assert.DoesNotContain("@", refused!.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(await RefusalsOfAsync(database, accountId), row => row.TargetSpaceId == spaceId);

        var after = Assert.Single(await wire.InvitationsInAsync(spaceId), row => row.Id == before!.Id);

        Assert.Equal(before! with { TokenHash = after.TokenHash }, after);
        Assert.Equal(terminal == "Expired" ? "Pending" : terminal, after.State);
    }

    /// <summary>
    /// AC4, Q3 (a) [SECURITY], AD-20 - an id that names no Invitation in this Space - one that never existed, and another
    /// Space's - is answered the boundary <c>404 not-available</c> body, byte for byte the answer for a Space that does not
    /// exist, and is <b>not</b> recorded against this Space (an in-Space not-found crossed no boundary; the Label delete's
    /// precedent). The other Space's Invitation stays <c>Pending</c>. Anchor: a live Invitation in this Space is revoked
    /// <c>204</c>. Plant 10: the unknown-id branch answering <c>409</c> or recording a refusal.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-20")]
    [Trait("Requirement", "FR-12")]
    public async Task Revoking_an_id_that_names_nothing_in_this_Space_is_the_boundary_404_and_is_not_recorded()
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var (live, foreignInvitee) = (InvitationWire.FreshAddress("live"), InvitationWire.FreshAddress("foreign"));
        var (spaceId, foreignSpaceId) = (Guid.NewGuid(), Guid.NewGuid());
        var accountId = Guid.Empty;
        var foreignInvitation = Guid.Empty;
        WireResponse? anchor = null;
        WireResponse? absentSpace = null;
        WireResponse? unknown = null;
        WireResponse? foreign = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            accountId = await AccountIdOfAsync(database, address);
            await SeedNamedSpaceAsync(database, spaceId, "Unknown " + Suffix(), accountId, Role.Owner);

            // The other Space's live Invitation, issued through 4.1's route by that Space's own Owner - the caller is not a
            // member there.
            var foreignOwnerAddress = RandomAddress();
            var foreignOwner = await SignedInAsync(client, foreignOwnerAddress);
            await SeedNamedSpaceAsync(database, foreignSpaceId, "Elsewhere " + Suffix(), await AccountIdOfAsync(database, foreignOwnerAddress), Role.Owner);
            await foreignOwner.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(foreignOwner, foreignSpaceId, foreignInvitee, "member")).StatusCode);
            foreignInvitation = await InvitationIdOfAsync(wire, foreignSpaceId, foreignInvitee);

            await session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(session, spaceId, live, "member")).StatusCode);

            anchor = await RevokeAsync(session, spaceId, await InvitationIdOfAsync(wire, spaceId, live));
            absentSpace = await RevokeAsync(session, Guid.NewGuid(), Guid.NewGuid());
            unknown = await RevokeAsync(session, spaceId, Guid.NewGuid());
            foreign = await RevokeAsync(session, spaceId, foreignInvitation);
        });

        Assert.True(anchor?.StatusCode == HttpStatusCode.NoContent, $"{anchor?.StatusCode} {anchor?.Body}");
        Assert.Equal(HttpStatusCode.NotFound, absentSpace?.StatusCode);

        foreach (var answer in new[] { unknown, foreign })
        {
            Assert.True(answer?.StatusCode == HttpStatusCode.NotFound, $"{answer?.StatusCode} {answer?.Body}");
            Assert.Equal(ProblemTypes.NotAvailable, ProblemType(answer!));
            Assert.Equal(absentSpace!.Body, answer!.Body);
            Assert.Equal(absentSpace.HeaderNames, answer.HeaderNames);
        }

        Assert.DoesNotContain(await RefusalsOfAsync(database, accountId), row => row.TargetSpaceId == spaceId);
        Assert.Equal("Pending", Assert.Single(await wire.InvitationsInAsync(foreignSpaceId), row => row.Id == foreignInvitation).State);
    }

    /// <summary>
    /// AC10, AD-18, AR-25 - a replay with the same <c>Idempotency-Key</c> after a committed revoke answers the original
    /// <c>204</c> from the record and writes nothing; a second, distinct revoke (a new key) answers <c>409
    /// invitation-not-pending</c>; a malformed key (one character past <see cref="Idempotency.MaxKeyLength"/>) is <c>400
    /// invalid-idempotency-key</c> and leaves its Invitation <c>Pending</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "AR-25")]
    public async Task A_revoke_replays_under_its_key_and_a_new_key_is_refused_409()
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var (invitee, untouched) = (InvitationWire.FreshAddress("replayed"), InvitationWire.FreshAddress("malformed"));
        var key = Guid.NewGuid().ToString("D");
        var spaceId = Guid.NewGuid();
        WireResponse? original = null;
        WireResponse? replay = null;
        WireResponse? fresh = null;
        WireResponse? malformed = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedNamedSpaceAsync(database, spaceId, "Replay " + Suffix(), await AccountIdOfAsync(database, address), Role.Owner);

            await session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(session, spaceId, invitee, "member")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(session, spaceId, untouched, "member")).StatusCode);

            var id = await InvitationIdOfAsync(wire, spaceId, invitee);

            original = await RevokeAsync(session, spaceId, id, key);
            replay = await RevokeAsync(session, spaceId, id, key);
            fresh = await RevokeAsync(session, spaceId, id, Guid.NewGuid().ToString("D"));
            malformed = await RevokeAsync(session, spaceId, await InvitationIdOfAsync(wire, spaceId, untouched), new string('k', Idempotency.MaxKeyLength + 1));
        });

        Assert.True(original?.StatusCode == HttpStatusCode.NoContent, $"{original?.StatusCode} {original?.Body}");
        Assert.Equal(original!.StatusCode, replay?.StatusCode);
        Assert.Equal(original.Body, replay!.Body);
        Assert.True(fresh?.StatusCode == HttpStatusCode.Conflict, $"{fresh?.StatusCode} {fresh?.Body}");
        Assert.Equal(ProblemTypes.InvitationNotPending, ProblemType(fresh!));
        Assert.Equal(HttpStatusCode.BadRequest, malformed?.StatusCode);
        Assert.Equal(ProblemTypes.InvalidIdempotencyKey, ProblemType(malformed!));

        var rows = await wire.InvitationsInAsync(spaceId);

        Assert.Equal("Revoked", Assert.Single(rows, row => row.EmailAddress == invitee).State);
        Assert.Equal("Pending", Assert.Single(rows, row => row.EmailAddress == untouched).State);
    }

    /// <summary>
    /// AC7, FR-39 ("exactly one email per Invitation"), AD-26's notification rule - a revoke sends nothing: issue to a fresh
    /// address and wait for its one email, revoke, then make one more authenticated request so the piggyback sweep would
    /// run, and issue to a second address whose email is awaited (the anchor: the dispatcher ran after the revoke). The
    /// outbox then holds no row for the revoked address and the drop holds exactly one email to it - scoped to the case's
    /// own address (<c>TESTING-CONVENTIONS.md</c> "Outbox and email"). Plant 9: the revoke enqueues a row.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-39")]
    [Trait("Requirement", "FR-12")]
    [Trait("Requirement", "AD-10")]
    public async Task A_revoke_sends_no_email()
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var (invitee, later) = (InvitationWire.FreshAddress("revoked-mail"), InvitationWire.FreshAddress("later-mail"));
        var spaceId = Guid.NewGuid();
        WireResponse? revoked = null;
        IReadOnlyList<DroppedEmail> emails = [];

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedNamedSpaceAsync(database, spaceId, "Silent " + Suffix(), await AccountIdOfAsync(database, address), Role.Owner);

            await session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(session, spaceId, invitee, "member")).StatusCode);
            _ = await mail.WaitForEmailToAsync(invitee);

            revoked = await RevokeAsync(session, spaceId, await InvitationIdOfAsync(wire, spaceId, invitee));

            Assert.Equal(HttpStatusCode.OK, (await session.GetAsync(SpaceRoutes.ActiveOf(spaceId))).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(session, spaceId, later, "member")).StatusCode);
            _ = await mail.WaitForEmailToAsync(later);

            emails = await mail.ReadAllAsync();
        });

        Assert.True(revoked?.StatusCode == HttpStatusCode.NoContent, $"{revoked?.StatusCode} {revoked?.Body}");
        Assert.DoesNotContain(await wire.OutboxRowsAsync(), row => string.Equals(row.Recipient, invitee, StringComparison.OrdinalIgnoreCase));
        Assert.Single(emails, email => string.Equals(email.To, invitee, StringComparison.OrdinalIgnoreCase));
        Assert.Single(emails, email => string.Equals(email.To, later, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// NFR-6, I-17, AR-34 - neither the invited address (as issued or normalised) nor the issuer's name reaches the Host's
    /// log across a list, a revoke and a refused second revoke. Extends 4.1's log case to 4.2's two routes. Anchor: the log
    /// is the Host's (its listening line is in it) and each request was served as asserted.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-6")]
    [Trait("Requirement", "AD-23")]
    public async Task Neither_the_address_nor_the_issuer_name_reaches_the_Host_log_on_list_or_revoke()
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var displayName = DisplayName();
        var invitee = InvitationWire.FreshAddress("unlogged");
        var spaceId = Guid.NewGuid();

        using var mail = new MailDrop();

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, displayName);
            await SeedNamedSpaceAsync(database, spaceId, "Unlogged " + Suffix(), await AccountIdOfAsync(database, address), Role.Owner);

            await session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(session, spaceId, invitee, "member")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await ListInvitationsAsync(session, spaceId)).StatusCode);

            var id = await InvitationIdOfAsync(wire, spaceId, invitee);

            Assert.Equal(HttpStatusCode.NoContent, (await RevokeAsync(session, spaceId, id)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await RevokeAsync(session, spaceId, id)).StatusCode);
        });

        Assert.Contains(HostProcess.ListeningMarker, log, StringComparison.Ordinal);

        foreach (var secret in new[] { invitee, EmailAddressNormalisation.Normalise(invitee), displayName })
        {
            Assert.DoesNotContain(secret, log, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The revoke as the browser sends it (AC9): the Session's cookie and anti-forgery header, no body, and an
    /// <c>Idempotency-Key</c> when one is given.
    /// </summary>
    private static Task<WireResponse> RevokeAsync(WireSession session, Guid spaceId, Guid invitationId, string? key = null) =>
        session.SendWithHeadersAsync(HttpMethod.Delete, InvitationRoutes.InvitationOf(spaceId, invitationId), content: null, KeyHeaders(key));

    /// <summary>
    /// The id of the one Invitation in <paramref name="spaceId"/> stored with exactly <paramref name="address"/>, read under
    /// the Space's own key.
    /// </summary>
    private static async Task<Guid> InvitationIdOfAsync(WireDatabase wire, Guid spaceId, string address) =>
        Assert.Single(await wire.InvitationsInAsync(spaceId), row => row.EmailAddress == address).Id;
}
