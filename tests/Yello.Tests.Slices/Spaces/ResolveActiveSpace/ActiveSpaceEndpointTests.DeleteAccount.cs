using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Spaces;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Deleting one's own Account over the wire: <c>DELETE /api/v1/account</c>, Session only. An Account that owns a Space is
/// refused <c>409 account-owns-spaces</c> naming every owned Space by identity alone; otherwise it leaves every Space it
/// belongs to - unassigned, its pending offers lapsed, its Tokens and presence gone - and its Account row is erased.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every registered Account owns its Personal Space</b>, so a case that deletes an Account first deletes that Space
/// through the product. The refusal cases keep it, and it is the Space they expect named.
/// </para>
/// <para>
/// <b>Seeding.</b> The first Membership joins through an accepted Invitation; a second or third Membership of the same
/// address is added with <see cref="WireDatabase.AddMembershipAsync"/>, because the mail drop answers the first email to an
/// address. Tasks, assignments, offers, Tokens and presence are made through their routes.
/// </para>
/// <para>
/// <b>The route and the problem types are stand-ins</b> (<see cref="AccountDeletionWire"/>) until the contract names them.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    private const string SessionCookie = "yello_session";

    /// <summary>
    /// An Invitation outlives its issuer's Account even in a Space the issuer had already been removed from, where no
    /// Space-scoped step of the deletion runs: the cascade from <c>Account</c> must reach the row through the Space-only
    /// policy on <c>Invitation</c>. The row stays, still pending, with no issuer; the Owner's list shows it with no issuer
    /// name and can still revoke it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC4 - DELETE /api/v1/account is not mapped and Invitation.IssuedByAccountId is Restrict, not nullable SET NULL (Q3 (a), Lee); riskiest assumption: the SET NULL cascade reaches Invitation rows past RLS")]
    [Trait("Requirement", "FR-3")]
    [Trait("Requirement", "FR-12")]
    [Trait("Requirement", "AD-5")]
    [Trait("Requirement", "UX-DR10")]
    public async Task An_Invitation_issued_in_a_Space_its_issuer_was_removed_from_survives_the_issuers_deletion_with_no_issuer()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, issuerAddress, issuerName) = (RandomAddress(), RandomAddress(), DisplayName());
        var invitee = InvitationWire.FreshAddress("orphaned");
        var spaceId = Guid.NewGuid();
        var issuerId = Guid.Empty;
        var invitationId = Guid.Empty;
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Issuer removed " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            var issuer = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), issuerAddress, issuerName, "admin");
            issuerId = await AccountIdOfAsync(database, issuerAddress);

            answers["issued"] = await IssueAsync(issuer, spaceId, invitee, "member");
            invitationId = await InvitationIdOfAsync(wire, spaceId, invitee);

            await owner.FetchAntiforgeryTokenAsync();
            answers["removed"] = await RemoveOverWireAsync(owner, spaceId, (await wire.MembershipOfAsync(spaceId, issuerId)).Id);

            _ = await DeletePersonalSpaceAsync(issuer, database, issuerName);
            answers["deleted"] = await AccountDeletionWire.DeleteAsync(issuer);

            answers["listed"] = await ListInvitationsAsync(owner, spaceId);
            answers["revoked"] = await RevokeAsync(owner, spaceId, invitationId);
        });

        Assert.True(answers["issued"].StatusCode == HttpStatusCode.NoContent, $"The Admin's issue: {Described(answers["issued"])}");
        Assert.True(answers["removed"].StatusCode == HttpStatusCode.NoContent, $"The Owner's removal of the issuer: {Described(answers["removed"])}");
        Assert.True(answers["deleted"].StatusCode == HttpStatusCode.NoContent, $"The issuer's deletion: {Described(answers["deleted"])}");
        Assert.Equal(0, await AccountRowsAsync(database, issuerId));

        Assert.True(answers["listed"].StatusCode == HttpStatusCode.OK, $"The Owner's list: {Described(answers["listed"])}");

        var listed = Assert.Single(InvitationWire.ParseList(answers["listed"].Body), row => row.Id == invitationId);

        Assert.Null(listed.IssuerName);
        Assert.DoesNotContain(issuerName, answers["listed"].Body, StringComparison.Ordinal);
        Assert.DoesNotContain(issuerId.ToString("D"), answers["listed"].Body, StringComparison.OrdinalIgnoreCase);

        Assert.True(answers["revoked"].StatusCode == HttpStatusCode.NoContent, $"The Owner's revoke of the orphaned Invitation: {Described(answers["revoked"])}");

        var stored = await InvitationIssuerOfAsync(database, spaceId, invitationId);

        Assert.True(stored.Found, "The Invitation row is gone: the issuer's deletion took another Account's work with it.");
        Assert.Null(stored.IssuedByAccountId);
        Assert.Equal("Revoked", stored.State);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// An Account that owns nothing, Admin of one Space, Member of a second and Viewer of a third, deletes itself:
    /// <c>204</c> with no body and the Session cookie cleared. It has gone from every Space - no Membership, presence or Token
    /// row - and each Space keeps exactly one Owner and everything its members made: the Tasks the Account was assigned
    /// survive unassigned, and the offer naming it is <c>Lapsed</c> with the Owner's pending read back to <c>204</c>. Both
    /// Tokens are the no-credential <c>401</c>. One <c>MembershipChanged</c> per Space left, and the address in no log line.
    /// Nothing anywhere still carries the Account's id or its Memberships' ids except the terminal offer, which has no key.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1 - DELETE /api/v1/account is not mapped, so the Account leaves no Space and is not erased")]
    [Trait("Requirement", "FR-3")]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "FR-21")]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "AD-5")]
    [Trait("Requirement", "AD-6")]
    [Trait("Requirement", "AD-9")]
    [Trait("Requirement", "AD-26")]
    public async Task An_Account_in_three_Spaces_is_erased_from_each_and_every_Space_keeps_its_work()
    {
        var database = Available();
        var wire = Wire();
        var (address, name) = (RandomAddress(), DisplayName());
        var (adminSpace, memberSpace, viewerSpace) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var leaverId = Guid.Empty;
        var personalSpace = Guid.Empty;
        var memberships = new Dictionary<Guid, Guid>();
        var assigned = new Dictionary<Guid, (Guid TaskId, string Title)>();
        var offerId = Guid.Empty;
        var tokens = new List<(Guid SpaceId, string Plaintext)>();
        var refusalsBefore = 0;
        string? clearedCookie = null;
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var (adminOwnerAddress, memberOwnerAddress, viewerOwnerAddress) = (RandomAddress(), RandomAddress(), RandomAddress());
            var adminOwner = await SignedInAsync(client, adminOwnerAddress);
            await SeedAssignableSpaceAsync(database, adminSpace, "Admin of " + Suffix(), await AccountIdOfAsync(database, adminOwnerAddress));
            var memberOwner = await SignedInAsync(client, memberOwnerAddress);
            await SeedAssignableSpaceAsync(database, memberSpace, "Member of " + Suffix(), await AccountIdOfAsync(database, memberOwnerAddress));
            _ = await SignedInAsync(client, viewerOwnerAddress);
            await SeedNamedSpaceAsync(database, viewerSpace, "Viewer of " + Suffix(), await AccountIdOfAsync(database, viewerOwnerAddress), Role.Owner);

            var leaver = await JoinedThroughTheProductAsync(client, wire, mail, (adminOwner, adminSpace), address, name, "admin");
            leaverId = await AccountIdOfAsync(database, address);

            // Directly: the mail drop answers the first email to an address, and these are the Account's later Memberships.
            await wire.AddMembershipAsync(memberSpace, leaverId, "member");
            await wire.AddMembershipAsync(viewerSpace, leaverId, "viewer");

            foreach (var space in new[] { adminSpace, memberSpace, viewerSpace })
            {
                memberships[space] = (await wire.MembershipOfAsync(space, leaverId)).Id;
            }

            foreach (var (owner, space) in new[] { (adminOwner, adminSpace), (memberOwner, memberSpace) })
            {
                await owner.FetchAntiforgeryTokenAsync();

                var title = "Assigned " + Suffix();
                var (_, taskId) = await TaskThroughTheProductAsync(owner, space, title);
                var assignment = await AssignOverWireAsync(owner, space, taskId, memberships[space]);

                Assert.True(assignment.StatusCode == HttpStatusCode.OK, $"Assigning through the product: {Described(assignment)}");
                assigned[space] = (taskId, title);
            }

            offerId = (await OfferedAsync(memberOwner, memberSpace, memberships[memberSpace])).Id;

            tokens.Add((adminSpace, await BearerForAsync(leaver, adminSpace)));
            tokens.Add((memberSpace, await BearerForAsync(leaver, memberSpace)));

            // The Viewer's first request in its Space writes its presence row; a foreign Space id records a refusal.
            Assert.Equal(HttpStatusCode.OK, (await leaver.GetAsync(SpaceRoutes.ActiveOf(viewerSpace))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await leaver.GetAsync(SpaceRoutes.ActiveOf(Guid.NewGuid()))).StatusCode);
            refusalsBefore = (await RefusalsOfAsync(database, leaverId)).Count;

            personalSpace = await DeletePersonalSpaceAsync(leaver, database, name);

            answers["deleted"] = await AccountDeletionWire.DeleteAsync(leaver);
            clearedCookie = leaver.SetCookieFor(SessionCookie);

            foreach (var (space, plaintext) in tokens)
            {
                answers[$"token {space:D}"] = await new WireSession(client).GetWithTokenAsync(SpaceRoutes.ActiveOf(space), plaintext);
                answers[$"none {space:D}"] = await new WireSession(client).GetAnonymouslyAsync(SpaceRoutes.ActiveOf(space));
            }

            answers["pending read"] = await PendingOfferOverWireAsync(memberOwner, memberSpace);
            answers["membership list"] = await ListMembershipsAsync(adminOwner, adminSpace);
        });

        var spaces = new[] { adminSpace, memberSpace, viewerSpace };

        Assert.True(refusalsBefore > 0, "The anchor failed: the Account had recorded no refusal for the erasure to remove.");
        Assert.True(answers["deleted"].StatusCode == HttpStatusCode.NoContent, $"The deletion: {Described(answers["deleted"])}");
        Assert.Equal(string.Empty, answers["deleted"].Body);
        Assert.NotNull(clearedCookie);
        Assert.Contains("expires=Thu, 01 Jan 1970", clearedCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await AccountRowsAsync(database, leaverId));

        foreach (var space in spaces)
        {
            Assert.DoesNotContain(await wire.MembershipRowsInAsync(space), row => row.AccountId == leaverId);
            Assert.Equal(0, await PresenceRowsAsync(database, space, leaverId));
        }

        foreach (var (space, (taskId, title)) in assigned)
        {
            Assert.Contains(await wire.TasksInAsync(space), task => task.Id == taskId && task.Title == title);
            Assert.Null(await wire.AssigneeOfAsync(space, taskId));
        }

        Assert.Equal("Lapsed", Assert.Single(await wire.OwnershipOffersInAsync(memberSpace), offer => offer.Id == offerId).State);
        Assert.True(answers["pending read"].StatusCode == HttpStatusCode.NoContent, $"The Owner's pending read after the lapse: {Described(answers["pending read"])}");
        await AssertNoPendingOfferNamesAMissingMembershipAsync(wire, memberSpace);

        foreach (var (space, _) in tokens)
        {
            var (token, none) = (answers[$"token {space:D}"], answers[$"none {space:D}"]);

            Assert.True(token.StatusCode == HttpStatusCode.Unauthorized, $"The Token bound to {space:D}: {Described(token)}");
            Assert.Equal(none.Body, token.Body);
            Assert.Equal(none.HeaderNames, token.HeaderNames);
            Assert.Equal(0, await wire.TokenCountAsync(leaverId, space));
        }

        Assert.True(answers["membership list"].StatusCode == HttpStatusCode.OK, $"The Admin Space's list: {Described(answers["membership list"])}");
        Assert.DoesNotContain(name, answers["membership list"].Body, StringComparison.Ordinal);
        Assert.DoesNotContain(address, answers["membership list"].Body, StringComparison.OrdinalIgnoreCase);

        // The Admin Space's accept published once already; each leave publishes once more.
        Assert.Equal(2, PublishesNaming(log, leaverId, adminSpace));
        Assert.Equal(1, PublishesNaming(log, leaverId, memberSpace));
        Assert.Equal(1, PublishesNaming(log, leaverId, viewerSpace));
        Assert.DoesNotContain(address, log, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            ["OwnershipOffer.RecipientMembershipId"],
            await ColumnsStillNamingAsync(database, leaverId, [.. memberships.Values], [.. spaces, personalSpace]));

        await AssertOneOwnerEachAsync(wire, spaces);
    }

    /// <summary>
    /// An Account that owns its Personal Space and is a Member elsewhere is refused <c>409 account-owns-spaces</c>, and the
    /// refusal names exactly the Personal Space - its id and name, nothing else - and never the Space it merely belongs to.
    /// Nothing changes: the Membership, the Personal Space, the Account and its Session all stand, and no refusal is recorded.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2 - DELETE /api/v1/account is not mapped, so no owned-Space refusal exists")]
    [Trait("Requirement", "FR-3")]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "AD-5")]
    [Trait("Requirement", "AD-24")]
    public async Task An_Owner_is_refused_409_naming_exactly_its_owned_Space_and_nothing_changes()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, address, name) = (RandomAddress(), RandomAddress(), DisplayName());
        var memberSpace = Guid.NewGuid();
        var accountId = Guid.Empty;
        var personalSpace = Guid.Empty;
        var refusalsBefore = 0;
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            _ = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, memberSpace, "Belongs to " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            var caller = await RegisteredAndSignedInAsync(client, address, name);
            accountId = await AccountIdOfAsync(database, address);
            personalSpace = await PersonalSpaceOfAsync(database, name);
            await wire.AddMembershipAsync(memberSpace, accountId, "member");
            await caller.FetchAntiforgeryTokenAsync();
            refusalsBefore = (await RefusalsOfAsync(database, accountId)).Count;

            answers["refused"] = await AccountDeletionWire.DeleteAsync(caller);
            answers["switcher"] = await caller.GetAsync(SpaceRoutes.Directory);
        });

        var refused = answers["refused"];

        Assert.True(refused.StatusCode == HttpStatusCode.Conflict, $"The Owner's deletion: {Described(refused)}");
        Assert.Equal(AccountDeletionWire.AccountOwnsSpacesType, ProblemType(refused));

        var named = AccountDeletionWire.OwnedSpacesIn(refused.Body);

        Assert.Equal([personalSpace], named.Select(space => space.Id));
        Assert.All(named, space => Assert.Equal(AccountDeletionWire.OwnedSpaceMembers, space.Members));
        Assert.DoesNotContain(memberSpace.ToString("D"), refused.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", refused.Body, StringComparison.Ordinal);

        Assert.Equal(1, await AccountRowsAsync(database, accountId));
        Assert.Equal("Member", (await wire.MembershipOfAsync(memberSpace, accountId)).Role);
        Assert.Equal("Owner", (await wire.MembershipOfAsync(personalSpace, accountId)).Role);
        Assert.Equal(refusalsBefore, (await RefusalsOfAsync(database, accountId)).Count);
        Assert.True(answers["switcher"].StatusCode == HttpStatusCode.OK, $"The Session after the refusal: {Described(answers["switcher"])}");
        await AssertOneOwnerEachAsync(wire, memberSpace, personalSpace);
    }

    /// <summary>
    /// An Owner of two Spaces and Member of a third is refused naming both owned Spaces, ordered by name then id, and not
    /// the third. Neither owned Space nor the Membership changes.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2 - DELETE /api/v1/account is not mapped, so no owned-Space refusal exists")]
    [Trait("Requirement", "FR-3")]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "AD-24")]
    public async Task An_Owner_of_two_Spaces_and_Member_of_a_third_is_refused_naming_both_in_name_order()
    {
        var database = Available();
        var wire = Wire();
        var (otherOwnerAddress, address, name) = (RandomAddress(), RandomAddress(), DisplayName());
        var (ownedSpace, memberSpace) = (Guid.NewGuid(), Guid.NewGuid());

        // A name that sorts before any Personal Space name ("Ravi-..."), so the order is decided by name.
        var ownedName = "Aardvark " + Suffix();
        var accountId = Guid.Empty;
        var personalSpace = Guid.Empty;
        WireResponse? refused = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            _ = await SignedInAsync(client, otherOwnerAddress);
            await SeedNamedSpaceAsync(database, memberSpace, "Merely belongs " + Suffix(), await AccountIdOfAsync(database, otherOwnerAddress), Role.Owner);

            var caller = await RegisteredAndSignedInAsync(client, address, name);
            accountId = await AccountIdOfAsync(database, address);
            personalSpace = await PersonalSpaceOfAsync(database, name);
            await SeedNamedSpaceAsync(database, ownedSpace, ownedName, accountId, Role.Owner);
            await wire.AddMembershipAsync(memberSpace, accountId, "member");
            await caller.FetchAntiforgeryTokenAsync();

            refused = await AccountDeletionWire.DeleteAsync(caller);
        });

        Assert.True(refused?.StatusCode == HttpStatusCode.Conflict, $"The deletion: {Described(refused)}");
        Assert.Equal(AccountDeletionWire.AccountOwnsSpacesType, ProblemType(refused));

        var named = AccountDeletionWire.OwnedSpacesIn(refused.Body);

        Assert.Equal([ownedSpace, personalSpace], named.Select(space => space.Id));
        Assert.Equal(ownedName, named[0].Name);
        Assert.DoesNotContain(memberSpace.ToString("D"), refused.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Member", (await wire.MembershipOfAsync(memberSpace, accountId)).Role);
        Assert.Equal(1, await AccountRowsAsync(database, accountId));
        await AssertOneOwnerEachAsync(wire, ownedSpace, memberSpace, personalSpace);
    }

    /// <summary>
    /// Making an offer is not an exit: an Owner whose offer is still pending is refused naming the Space, and the offer
    /// stays <c>Pending</c> with the Owner still Owner.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2 - DELETE /api/v1/account is not mapped, so the Owner with a pending offer is not refused")]
    [Trait("Requirement", "FR-3")]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "AD-5")]
    public async Task An_Owner_with_a_pending_offer_is_refused_and_the_offer_stays_pending()
    {
        var database = Available();
        var wire = Wire();
        var (address, name) = (RandomAddress(), DisplayName());
        var spaceId = Guid.NewGuid();
        var accountId = Guid.Empty;
        var offerId = Guid.Empty;
        WireResponse? refused = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await RegisteredAndSignedInAsync(client, address, name);
            accountId = await AccountIdOfAsync(database, address);
            await SeedNamedSpaceAsync(database, spaceId, "Offered away " + Suffix(), accountId, Role.Owner);

            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), RandomAddress(), DisplayName(), "member");
            var memberId = (await wire.MembershipRowsInAsync(spaceId)).Single(row => row.Role == "Member").Id;

            await owner.FetchAntiforgeryTokenAsync();
            offerId = (await OfferedAsync(owner, spaceId, memberId)).Id;

            _ = await DeletePersonalSpaceAsync(owner, database, name);
            refused = await AccountDeletionWire.DeleteAsync(owner);
        });

        Assert.True(refused?.StatusCode == HttpStatusCode.Conflict, $"The offering Owner's deletion: {Described(refused)}");
        Assert.Equal(AccountDeletionWire.AccountOwnsSpacesType, ProblemType(refused));
        Assert.Equal([spaceId], AccountDeletionWire.OwnedSpacesIn(refused.Body).Select(space => space.Id));
        Assert.Equal("Pending", Assert.Single(await wire.OwnershipOffersInAsync(spaceId), offer => offer.Id == offerId).State);
        Assert.Equal("Owner", (await wire.MembershipOfAsync(spaceId, accountId)).Role);
        Assert.Equal(1, await AccountRowsAsync(database, accountId));
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// The address is free again and inherits nothing: after the deletion the same address, in other case, registers a new
    /// Account with a new id, exactly one Membership (its new Personal Space, as Owner), no Token and no refusal, and both
    /// former Spaces answer it the boundary <c>404</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC7 - DELETE /api/v1/account is not mapped, so the address is never freed")]
    [Trait("Requirement", "FR-3")]
    [Trait("Requirement", "FR-1")]
    public async Task The_deleted_address_registers_again_as_a_new_Account_that_inherits_nothing()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, address, name) = (RandomAddress(), RandomAddress(), DisplayName());
        var (joinedSpace, addedSpace) = (Guid.NewGuid(), Guid.NewGuid());
        var (formerId, newId) = (Guid.Empty, Guid.Empty);
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            var ownerId = await AccountIdOfAsync(database, ownerAddress);
            await SeedNamedSpaceAsync(database, joinedSpace, "Former joined " + Suffix(), ownerId, Role.Owner);
            await SeedNamedSpaceAsync(database, addedSpace, "Former added " + Suffix(), ownerId, Role.Owner);

            var former = await JoinedThroughTheProductAsync(client, wire, mail, (owner, joinedSpace), address, name, "member");
            formerId = await AccountIdOfAsync(database, address);
            await wire.AddMembershipAsync(addedSpace, formerId, "member");

            _ = await DeletePersonalSpaceAsync(former, database, name);
            answers["deleted"] = await AccountDeletionWire.DeleteAsync(former);

            var again = await RegisteredAndSignedInAsync(client, address.ToUpperInvariant(), DisplayName());
            newId = await AccountIdOfAsync(database, address);

            answers["joined"] = await again.GetAsync(SpaceRoutes.ActiveOf(joinedSpace));
            answers["added"] = await again.GetAsync(SpaceRoutes.ActiveOf(addedSpace));
        });

        Assert.True(answers["deleted"].StatusCode == HttpStatusCode.NoContent, $"The deletion: {Described(answers["deleted"])}");
        Assert.NotEqual(formerId, newId);
        Assert.Equal(0, await AccountRowsAsync(database, formerId));

        var held = await wire.MembershipsOfAccountAsync(newId);

        Assert.Equal(["Owner"], held.Select(membership => membership.Role));
        Assert.DoesNotContain(held, membership => membership.SpaceId == joinedSpace || membership.SpaceId == addedSpace);
        Assert.Equal(0, await wire.TokenCountAsync(newId, held[0].SpaceId));

        // Exactly the two refusals its own requests to the former Spaces just recorded: none came with the address.
        Assert.Equal(2, (await RefusalsOfAsync(database, newId)).Count);

        foreach (var key in new[] { "joined", "added" })
        {
            Assert.True(answers[key].StatusCode == HttpStatusCode.NotFound, $"The new Account in the {key} Space: {Described(answers[key])}");
            Assert.Equal(ProblemTypes.NotAvailable, ProblemType(answers[key]));
        }
    }

    /// <summary>
    /// A deleted Account's Session is refused before anything it asks for runs: replaying an Invitation accept with the
    /// cookie held before the deletion is the dead-cookie <c>401</c>, never <c>403 invitation-for-another-account</c>, and the
    /// Invitation stays pending with no Membership written.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC11 - DELETE /api/v1/account is not mapped, so the Session never dies")]
    [Trait("Requirement", "FR-2")]
    [Trait("Requirement", "FR-3")]
    [Trait("Requirement", "NFR-6")]
    public async Task A_deleted_Accounts_Session_replaying_an_Invitation_accept_is_the_401_and_not_the_other_Account_refusal()
    {
        var database = Available();
        var wire = Wire();
        var (address, name) = (RandomAddress(), DisplayName());
        var accountId = Guid.Empty;
        Invited? invited = null;
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var doomed = await RegisteredAndSignedInAsync(client, address, name);
            accountId = await AccountIdOfAsync(database, address);

            invited = await InviteAsync(client, database, wire, mail, "replayed", "member", address);

            await doomed.FetchAntiforgeryTokenAsync();
            _ = await DeletePersonalSpaceAsync(doomed, database, name);

            answers["deleted"] = await AccountDeletionWire.DeleteAndKeepTheOldCookieAsync(doomed);
            answers["replayed"] = await AcceptAsync(doomed, invited.Token);
        });

        Assert.True(answers["deleted"].StatusCode == HttpStatusCode.NoContent, $"The deletion: {Described(answers["deleted"])}");

        var replayed = answers["replayed"];

        Assert.True(replayed.StatusCode == HttpStatusCode.Unauthorized, $"The dead Session's accept: {Described(replayed)}");
        Assert.NotEqual(ProblemTypes.InvitationForAnotherAccount, ProblemType(replayed));
        Assert.Contains(replayed.Headers.GetValueOrDefault("Set-Cookie") ?? [], value => value.StartsWith(SessionCookie + "=;", StringComparison.Ordinal));
        Assert.Equal("Pending", Assert.Single(await wire.InvitationsInAsync(invited!.SpaceId), row => row.Id == invited.InvitationId).State);
        Assert.DoesNotContain(await wire.MembershipRowsInAsync(invited.SpaceId), row => row.AccountId == accountId);
    }

    /// <summary>
    /// The Account deletes its Personal Space through the product (an Account that owns a Space cannot be deleted), and
    /// returns the Space's id.
    /// </summary>
    private static async Task<Guid> DeletePersonalSpaceAsync(WireSession session, RegistrationDatabase database, string displayName)
    {
        var personal = await PersonalSpaceOfAsync(database, displayName);

        await session.FetchAntiforgeryTokenAsync();

        var deleted = await DeleteSpaceOverWireAsync(session, personal, key: null);

        Assert.True(deleted.StatusCode == HttpStatusCode.NoContent, $"Deleting the Personal Space through the product: {Described(deleted)}");

        return personal;
    }

    /// <summary>
    /// One Invitation's stored issuer and state, under its Space's own key; <c>Found</c> is false when the row is gone. Read
    /// here rather than through <see cref="WireDatabase.InvitationsInAsync"/>, whose row type holds a non-null issuer.
    /// </summary>
    private static async Task<(bool Found, Guid? IssuedByAccountId, string State)> InvitationIssuerOfAsync(RegistrationDatabase database, Guid spaceId, Guid invitationId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT IssuedByAccountId, State FROM dbo.[Invitation] WHERE SpaceId = @space AND Id = @invitation";
        command.Parameters.Add(new SqlParameter("@space", spaceId));
        command.Parameters.Add(new SqlParameter("@invitation", invitationId));

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        if (!await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            return (false, null, string.Empty);
        }

        return (true, await reader.IsDBNullAsync(0, TestContext.Current.CancellationToken) ? null : reader.GetGuid(0), reader.GetString(1));
    }

    /// <summary>
    /// "Table.Column" for every <c>uniqueidentifier</c> column that names an Account (<c>Account.Id</c> and any
    /// <c>*AccountId</c>) or a Membership (<c>Membership.Id</c> and any <c>*MembershipId</c>) and still holds
    /// <paramref name="accountId"/> or one of <paramref name="membershipIds"/>. Every table is read with no key, under the
    /// <c>AccountId</c> key and under each of <paramref name="spaceIds"/>' keys, so a row hidden by a policy is never read as
    /// a row erased. The columns come from the catalogue, so a column added later is inventoried without editing this.
    /// </summary>
    private static async Task<IReadOnlyList<string>> ColumnsStillNamingAsync(
        RegistrationDatabase database,
        Guid accountId,
        IReadOnlyList<Guid> membershipIds,
        IReadOnlyList<Guid> spaceIds)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var columns = new List<(string Table, string Column, bool ByMembership)>();

        await using (var catalogue = await database.OpenWithoutSpaceContextAsync(cancellationToken))
        await using (var command = catalogue.CreateCommand())
        {
            command.CommandText = """
                SELECT c.TABLE_NAME, c.COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS c
                JOIN INFORMATION_SCHEMA.TABLES t ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME
                WHERE c.TABLE_SCHEMA = 'dbo' AND t.TABLE_TYPE = 'BASE TABLE' AND c.DATA_TYPE = 'uniqueidentifier'
                  AND (c.COLUMN_NAME LIKE '%AccountId' OR c.COLUMN_NAME LIKE '%MembershipId'
                       OR (c.TABLE_NAME IN ('Account', 'Membership') AND c.COLUMN_NAME = 'Id'))
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var (table, column) = (reader.GetString(0), reader.GetString(1));

                columns.Add((table, column, column.EndsWith("MembershipId", StringComparison.Ordinal) || (table == "Membership" && column == "Id")));
            }
        }

        Assert.Contains(columns, entry => entry is { Table: "Membership", Column: "AccountId" });

        var holding = new SortedSet<string>(StringComparer.Ordinal);
        var connections = new List<SqlConnection>
        {
            await database.OpenWithoutSpaceContextAsync(cancellationToken),
            await SpaceIsolationLayerTests.OpenForAccountAsync(database, accountId),
        };

        foreach (var spaceId in spaceIds)
        {
            connections.Add(await database.OpenForSpaceAsync(spaceId, cancellationToken));
        }

        try
        {
            foreach (var connection in connections)
            {
                foreach (var (table, column, byMembership) in columns)
                {
                    if (await CountNamingAsync(connection, table, column, byMembership ? membershipIds : [accountId]) > 0)
                    {
                        holding.Add($"{table}.{column}");
                    }
                }
            }
        }
        finally
        {
            foreach (var connection in connections)
            {
                await connection.DisposeAsync();
            }
        }

        return [.. holding];
    }

    private static async Task<int> CountNamingAsync(SqlConnection connection, string table, string column, IReadOnlyList<Guid> values)
    {
        Assert.Matches(CatalogueName, table);
        Assert.Matches(CatalogueName, column);

        if (values.Count == 0)
        {
            return 0;
        }

        await using var command = connection.CreateCommand();

        var names = values.Select((_, index) => $"@v{index.ToString(CultureInfo.InvariantCulture)}").ToList();

#pragma warning disable CA2100 // Review SQL queries - table and column names come from the catalogue and are checked above; the values are parameters.
        command.CommandText = $"SELECT COUNT(*) FROM dbo.[{table}] WHERE [{column}] IN ({string.Join(", ", names)})";
#pragma warning restore CA2100

        for (var index = 0; index < values.Count; index++)
        {
            command.Parameters.Add(new SqlParameter(names[index], values[index]));
        }

        return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture);
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CatalogueName { get; }
}
