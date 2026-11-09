using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Memberships;
using Yello.Contracts.Spaces;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// The recipient's acceptance over the wire: <c>POST /api/v1/spaces/{spaceId}/ownership-offers/{offerId}/acceptance</c>, no
/// body, <c>204</c>. It is declared under <c>ReadProjectsAndTasks</c>, which every Role holds, and decided by row identity
/// inside the store: only the Membership the offer names may accept, and only while the offer is actionable. The swap is
/// two ordered <c>ExecuteUpdate</c> statements in one transaction under the Memberships lock: the offering Owner to
/// <c>Admin</c>, then the recipient to <c>Owner</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Answers, by Lee's Q1 (a) and the story's Q7 (a) and Q8 (a):</b> a named recipient on an actionable offer gets
/// <c>204</c>; the named recipient on an offer no longer actionable gets <c>409 ownership-offer-not-pending</c>; anyone else
/// (another Member, an Admin, the Owner itself), an unknown id and another Space's id all get the in-Space not-found, byte for
/// byte the boundary's <c>404</c> and unrecorded; an offer whose offerer is no longer the Owner is <c>409
/// ownership-offer-not-pending</c> with everything rolled back; a promote that meets a second Owner is <c>409
/// owner-role-fixed</c>. Never a Role-shaped <c>403</c>.
/// </para>
/// <para>
/// <b>Seeding.</b> Recipients join through the product (an Invitation the Owner issues and the joiner accepts) and every live
/// offer is made through the offer route. Seeded directly, and only these: a terminal or time-expired offer (reached only by
/// seeding the past, after the live offer was revoked, so the Space holds at most one <c>Pending</c> row), and the offer whose
/// <c>OfferedByMembershipId</c> names an Admin (no request can make one). The offer table has no foreign key to
/// <c>Membership</c>, so the two Membership ids are whatever the case names.
/// </para>
/// <para>
/// <b>Mail.</b> Every case joins Members through an Invitation and makes an offer, both of which enqueue email, so every case
/// stops the pooled Hosts first (<see cref="SoleDispatcherWireAsync"/>) and runs a Host of its own with its own
/// <see cref="MailDrop"/>.
/// </para>
/// <para>
/// <b>Publishes are compared, not counted from zero</b>: the Owner of a seeded Space is never joined, so a publish naming it
/// can only be the acceptance's; a recipient is compared with a bystander who joined the same way and was offered nothing.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// The Roles a recipient may hold when it accepts: any Role but the Owner's.
    /// </summary>
    public static TheoryData<string> AcceptingRoles => new() { "admin", "member", "viewer" };

    /// <summary>
    /// The stored offers the named recipient may no longer accept: three terminal states, and a <c>Pending</c> row past its
    /// expiry.
    /// </summary>
    public static TheoryData<string> OffersNoLongerOpenToTheRecipient => new() { "Revoked", "Lapsed", "Declined", "Expired" };

    /// <summary>
    /// The named recipient, at any Role, accepts: <c>204</c> with no body; the offer row is retained with
    /// <c>State = Accepted</c> and every other column unchanged; the recipient's Membership is <c>Owner</c>, the offering
    /// Owner's is <c>Admin</c>, and every other Membership is exactly as before; no <c>Pending</c> offer remains; the Space holds
    /// exactly one Owner. The <c>Yello-Role</c> header on the <c>204</c> echoes the Role judged at request start, the
    /// recipient's old one. The Viewer row is the one that proves identity over Role: a Viewer holds no write capability.
    /// </summary>
    /// <param name="role">The recipient's Role when it accepts.</param>
    /// <returns>A task.</returns>
    [Theory]
    [MemberData(nameof(AcceptingRoles))]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AR-13")]
    [Trait("Requirement", "AR-12")]
    public async Task A_named_recipient_of_any_Role_accepts_and_becomes_the_sole_Owner_while_the_offerer_becomes_an_Admin(string role)
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var (ownerMembership, recipientMembership) = (Guid.Empty, Guid.Empty);
        IReadOnlyList<MembershipRow> before = [];
        OwnershipOfferRow? pending = null;
        WireResponse? accepted = null;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), role), ("bystander", DisplayName(), "member"));
            var recipient = space.Members["recipient"];
            var offer = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);

            (ownerMembership, recipientMembership) = (space.OwnerMembershipId, recipient.MembershipId);
            before = await wire.MembershipRowsInAsync(spaceId);
            pending = Assert.Single(await wire.OwnershipOffersInAsync(spaceId));

            await recipient.Session.FetchAntiforgeryTokenAsync();
            accepted = await AcceptOverWireAsync(recipient.Session, spaceId, offer.Id);
        });

        Assert.True(accepted?.StatusCode == HttpStatusCode.NoContent, $"The {role}'s acceptance: {Described(accepted)}");
        Assert.Equal(string.Empty, accepted.Body);
        Assert.Equal(role, accepted.Header(RoleHeader.HeaderName));
        Assert.Equal(pending! with { State = "Accepted" }, Assert.Single(await wire.OwnershipOffersInAsync(spaceId)));

        var expected = Swapped(before, ownerMembership, recipientMembership);

        Assert.Equal(expected, await wire.MembershipRowsInAsync(spaceId));
        Assert.DoesNotContain(await wire.OwnershipOffersInAsync(spaceId), row => row.State == "Pending");
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// Nobody but the named Membership can accept: another Member, an Admin, and the Owner on its own offer each get the
    /// in-Space not-found on the offer's real id - <c>404 not-available</c>, byte for byte the answer for a random id in the
    /// same Space and for a Space that does not exist - none is recorded against the Space, the offer stays <c>Pending</c>,
    /// and every Role is unchanged. A non-recipient learns nothing about whom the offer names, and is never given a
    /// Role-shaped <c>403</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-20")]
    public async Task A_Member_an_Admin_and_the_Owner_who_are_not_named_get_the_in_Space_404_and_nothing_changes()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        IReadOnlyList<MembershipRow> before = [];
        var actors = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("member", DisplayName(), "member"), ("admin", DisplayName(), "admin"));
            var offer = await OfferedAsync(space.Owner, spaceId, space.Members["recipient"].MembershipId);

            before = await wire.MembershipRowsInAsync(spaceId);
            actors["owner"] = space.OwnerAccountId;
            actors["member"] = space.Members["member"].AccountId;
            actors["admin"] = space.Members["admin"].AccountId;

            await space.Members["member"].Session.FetchAntiforgeryTokenAsync();
            await space.Members["admin"].Session.FetchAntiforgeryTokenAsync();

            answers["absent"] = await AcceptOverWireAsync(space.Owner, Guid.NewGuid(), Guid.NewGuid());
            answers["random"] = await AcceptOverWireAsync(space.Members["member"].Session, spaceId, Guid.NewGuid());
            answers["member"] = await AcceptOverWireAsync(space.Members["member"].Session, spaceId, offer.Id);
            answers["admin"] = await AcceptOverWireAsync(space.Members["admin"].Session, spaceId, offer.Id);
            answers["owner"] = await AcceptOverWireAsync(space.Owner, spaceId, offer.Id);
        });

        Assert.Equal(HttpStatusCode.NotFound, answers["absent"].StatusCode);

        foreach (var name in new[] { "random", "member", "admin", "owner" })
        {
            Assert.True(answers[name].StatusCode == HttpStatusCode.NotFound, $"The {name}'s acceptance: {Described(answers[name])}");
            Assert.Equal(ProblemTypes.NotAvailable, ProblemType(answers[name]));
            Assert.Equal(answers["absent"].Body, answers[name].Body);
            Assert.Equal(answers["absent"].HeaderNames, answers[name].HeaderNames);
        }

        foreach (var (name, accountId) in actors)
        {
            Assert.True(
                !(await RefusalsOfAsync(database, accountId)).Any(row => row.TargetSpaceId == spaceId),
                $"The {name}'s refused acceptance was recorded against the Space.");
        }

        Assert.Equal("Pending", Assert.Single(await wire.OwnershipOffersInAsync(spaceId)).State);
        Assert.Equal(before, await wire.MembershipRowsInAsync(spaceId));
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// The named recipient may not accept an offer that is no longer actionable - revoked, lapsed, declined, or still stored
    /// <c>Pending</c> past its expiry: <c>409 ownership-offer-not-pending</c> with no address in the body, nothing recorded,
    /// the stored row unchanged (the expired one stays <c>Pending</c>: expiry is never written), and every Role unchanged.
    /// Anchor: the Space's live offer was revoked <c>204</c> first.
    /// </summary>
    /// <param name="state">Which offer.</param>
    /// <returns>A task.</returns>
    [Theory]
    [MemberData(nameof(OffersNoLongerOpenToTheRecipient))]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AD-27")]
    [Trait("Assumption", "PRD-12-3")]
    public async Task An_offer_no_longer_open_to_its_recipient_is_409_and_changes_nothing(string state)
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var recipientAccount = Guid.Empty;
        IReadOnlyList<MembershipRow> before = [];
        OwnershipOfferRow? seeded = null;
        WireResponse? anchor = null;
        WireResponse? refused = null;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));
            var recipient = space.Members["recipient"];

            recipientAccount = recipient.AccountId;
            anchor = await RevokeOfferOverWireAsync(space.Owner, spaceId, (await OfferedAsync(space.Owner, spaceId, recipient.MembershipId)).Id);

            var now = await wire.DatabaseNowAsync();
            var id = state == "Expired"
                ? await wire.AddOwnershipOfferAsync(spaceId, recipient.MembershipId, space.OwnerMembershipId, "Captured " + Suffix(), "Pending", now.AddMinutes(-1))
                : await wire.AddOwnershipOfferAsync(spaceId, recipient.MembershipId, space.OwnerMembershipId, "Captured " + Suffix(), state, now.AddDays(OwnershipOfferWire.ExpiryDays));

            seeded = Assert.Single(await wire.OwnershipOffersInAsync(spaceId), row => row.Id == id);
            before = await wire.MembershipRowsInAsync(spaceId);

            await recipient.Session.FetchAntiforgeryTokenAsync();
            refused = await AcceptOverWireAsync(recipient.Session, spaceId, id);
        });

        Assert.True(anchor?.StatusCode == HttpStatusCode.NoContent, $"The anchor: {Described(anchor)}");
        Assert.True(refused?.StatusCode == HttpStatusCode.Conflict, $"Accepting the {state} offer: {Described(refused)}");
        Assert.Equal(ProblemTypes.OwnershipOfferNotPending, ProblemType(refused));
        Assert.DoesNotContain("@", refused.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(await RefusalsOfAsync(database, recipientAccount), row => row.TargetSpaceId == spaceId);
        Assert.Equal(seeded, Assert.Single(await wire.OwnershipOffersInAsync(spaceId), row => row.Id == seeded!.Id));
        Assert.Equal(before, await wire.MembershipRowsInAsync(spaceId));
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// An offer whose offerer is no longer the Owner cannot be accepted: the demote writes no row, so the store answers
    /// <c>409 ownership-offer-not-pending</c> and the unit of work rolls back the offer's <c>Accepted</c>. Nothing is promoted
    /// without a demotion; the offer stays <c>Pending</c>; every Role is unchanged; the Space holds one Owner. No request can
    /// make such an offer, so it is seeded: a <c>Pending</c> row naming the recipient, offered by an Admin's Membership.
    /// Plant 7 (the demote's exact-1 check removed) turns this red.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AR-12")]
    public async Task An_offer_whose_offerer_is_no_longer_the_Owner_is_409_and_rolls_back()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        IReadOnlyList<MembershipRow> before = [];
        OwnershipOfferRow? seeded = null;
        WireResponse? refused = null;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("admin", DisplayName(), "admin"));
            var recipient = space.Members["recipient"];
            var now = await wire.DatabaseNowAsync();
            var id = await wire.AddOwnershipOfferAsync(spaceId, recipient.MembershipId, space.Members["admin"].MembershipId, "Captured " + Suffix(), "Pending", now.AddDays(OwnershipOfferWire.ExpiryDays));

            seeded = Assert.Single(await wire.OwnershipOffersInAsync(spaceId), row => row.Id == id);
            before = await wire.MembershipRowsInAsync(spaceId);

            await recipient.Session.FetchAntiforgeryTokenAsync();
            refused = await AcceptOverWireAsync(recipient.Session, spaceId, id);
        });

        Assert.True(refused?.StatusCode == HttpStatusCode.Conflict, $"Accepting an offer whose offerer is an Admin: {Described(refused)}");
        Assert.Equal(ProblemTypes.OwnershipOfferNotPending, ProblemType(refused));
        Assert.Equal(seeded, Assert.Single(await wire.OwnershipOffersInAsync(spaceId)));
        Assert.Equal(before, await wire.MembershipRowsInAsync(spaceId));
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// The <c>2601 -&gt; 409 owner-role-fixed</c> mapping on the swap's promote, reached deterministically with no plant left
    /// in the tree. For this Space only, an <c>AFTER UPDATE</c> trigger on <c>Membership</c> sets a third Membership (an Admin)
    /// to <c>Owner</c> the moment the offering Owner's row turns <c>Admin</c>; that succeeds, because the demote left zero
    /// Owners. The acceptance's promote then fails <c>2601</c> on <c>UX_Membership_SpaceId_Owner</c>, and the store answers
    /// <c>409 owner-role-fixed</c>: everything is rolled back (the original Owner still <c>Owner</c>, the third still
    /// <c>Admin</c>, the recipient unchanged), the offer is still <c>Pending</c>, the Space holds one Owner, and no publish names
    /// the original Owner. Anchor: the same acceptance in another Space, with no trigger, answers <c>204</c>.
    /// </summary>
    /// <remarks>
    /// <c>RECURSIVE_TRIGGERS</c> is off by default, so the trigger's own <c>UPDATE</c> does not fire it again; its condition
    /// would not match that row anyway. <c>SET NOCOUNT ON</c> keeps the trigger's row out of the demote's rowcount, which the
    /// store checks for exactly 1. The trigger is planted after all seeding (it would otherwise sit on the joins' writes) and
    /// dropped in a <c>finally</c>.
    /// </remarks>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AD-5")]
    [Trait("Requirement", "AR-12")]
    public async Task A_promote_that_meets_a_second_Owner_is_409_owner_role_fixed_and_rolls_back_everything()
    {
        var database = Available();
        var (spaceId, anchorSpaceId) = (Guid.NewGuid(), Guid.NewGuid());
        var (ownerAccount, recipientAccount, thirdAccount) = (Guid.Empty, Guid.Empty, Guid.Empty);
        IReadOnlyList<MembershipRow> before = [];
        OwnershipOfferRow? pending = null;
        WireResponse? anchor = null;
        WireResponse? refused = null;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var anchorSpace = await OfferableSpaceAsync(client, database, wire, mail, anchorSpaceId, DisplayName(), ("recipient", DisplayName(), "member"));
            var anchorOffer = await OfferedAsync(anchorSpace.Owner, anchorSpaceId, anchorSpace.Members["recipient"].MembershipId);

            await anchorSpace.Members["recipient"].Session.FetchAntiforgeryTokenAsync();
            anchor = await AcceptOverWireAsync(anchorSpace.Members["recipient"].Session, anchorSpaceId, anchorOffer.Id);

            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("third", DisplayName(), "admin"));
            var recipient = space.Members["recipient"];
            var offer = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);

            (ownerAccount, recipientAccount, thirdAccount) = (space.OwnerAccountId, recipient.AccountId, space.Members["third"].AccountId);
            before = await wire.MembershipRowsInAsync(spaceId);
            pending = Assert.Single(await wire.OwnershipOffersInAsync(spaceId));

            await recipient.Session.FetchAntiforgeryTokenAsync();

            var trigger = await PlantSecondOwnerTriggerAsync(database, space.OwnerMembershipId, space.Members["third"].MembershipId);

            try
            {
                refused = await AcceptOverWireAsync(recipient.Session, spaceId, offer.Id);
            }
            finally
            {
                await DropTriggerAsync(database, trigger);
            }
        });

        Assert.True(anchor?.StatusCode == HttpStatusCode.NoContent, $"The anchor: {Described(anchor)}");
        Assert.True(refused?.StatusCode == HttpStatusCode.Conflict, $"The acceptance meeting a second Owner: {Described(refused)}");
        Assert.Equal(ProblemTypes.OwnerRoleFixed, ProblemType(refused));
        Assert.Equal(before, await wire.MembershipRowsInAsync(spaceId));
        Assert.Equal(pending, Assert.Single(await wire.OwnershipOffersInAsync(spaceId)));
        await AssertOneOwnerEachAsync(wire, spaceId, anchorSpaceId);
        Assert.Equal(0, PublishesNaming(log, ownerAccount, spaceId));
        Assert.Equal(PublishesNaming(log, thirdAccount, spaceId), PublishesNaming(log, recipientAccount, spaceId));
    }

    /// <summary>
    /// The previous Owner keeps its access, as an Admin: after the acceptance its next Space read answers <c>200</c> with
    /// <c>Yello-Role: admin</c>; it can still do an Admin thing (list the Members, <c>200</c>); it is refused the Owner-only
    /// things - deleting the Space and offering ownership - each with a recorded <c>403 insufficient-role</c>; its row is
    /// <c>Admin</c> and every other Membership but the recipient's is unchanged.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-9")]
    [Trait("Requirement", "AR-12")]
    public async Task The_former_Owner_keeps_its_access_as_an_Admin_after_the_acceptance()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var (ownerAccount, ownerMembership, recipientMembership) = (Guid.Empty, Guid.Empty, Guid.Empty);
        var refusalsBefore = 0;
        IReadOnlyList<MembershipRow> before = [];
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("bystander", DisplayName(), "viewer"));
            var recipient = space.Members["recipient"];
            var offer = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);

            (ownerAccount, ownerMembership, recipientMembership) = (space.OwnerAccountId, space.OwnerMembershipId, recipient.MembershipId);
            before = await wire.MembershipRowsInAsync(spaceId);

            await recipient.Session.FetchAntiforgeryTokenAsync();
            answers["accept"] = await AcceptOverWireAsync(recipient.Session, spaceId, offer.Id);

            refusalsBefore = (await RefusalsOfAsync(database, ownerAccount)).Count;
            answers["read"] = await space.Owner.GetAsync(SpaceRoutes.ActiveOf(spaceId));
            answers["members"] = await space.Owner.GetAsync(MembershipRoutes.MembershipsOf(spaceId));

            await space.Owner.FetchAntiforgeryTokenAsync();
            answers["delete"] = await DeleteSpaceOverWireAsync(space.Owner, spaceId, null);
            answers["offer"] = await OfferOverWireAsync(space.Owner, spaceId, space.Members["bystander"].MembershipId);
        });

        Assert.True(answers["accept"].StatusCode == HttpStatusCode.NoContent, $"The acceptance: {Described(answers["accept"])}");
        Assert.True(answers["read"].StatusCode == HttpStatusCode.OK, $"The former Owner's Space read: {Described(answers["read"])}");
        Assert.Equal("admin", answers["read"].Header(RoleHeader.HeaderName));
        Assert.True(answers["members"].StatusCode == HttpStatusCode.OK, $"The former Owner lists the Members: {Described(answers["members"])}");

        foreach (var name in new[] { "delete", "offer" })
        {
            Assert.True(answers[name].StatusCode == HttpStatusCode.Forbidden, $"The former Owner's {name}: {Described(answers[name])}");
            Assert.Equal(ProblemTypes.InsufficientRole, ProblemType(answers[name]));
        }

        var refusals = (await RefusalsOfAsync(database, ownerAccount)).Skip(refusalsBefore).ToList();

        Assert.Equal(2, refusals.Count);
        Assert.All(refusals, refusal => Assert.Equal("InsufficientRole", refusal.Kind));
        Assert.Contains(refusals, refusal => refusal.Capability == nameof(Capability.OfferOwnershipTransfer));
        Assert.Contains(refusals, refusal => refusal.Capability == nameof(Capability.DeleteSpace));

        var expected = Swapped(before, ownerMembership, recipientMembership);

        Assert.Equal(expected, await wire.MembershipRowsInAsync(spaceId));
        Assert.DoesNotContain(await wire.OwnershipOffersInAsync(spaceId), row => row.State == "Pending");
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// The new Owner is bound as any Owner is. After the acceptance: its leave is <c>409 ownership-held</c>; the former Owner
    /// (now an Admin) removing it is the recorded <c>403 insufficient-role</c> an Admin gets on the Owner's row; the former
    /// Owner changing its Role is the recorded <c>403 insufficient-role</c> an Admin gets on the Owner's row; the new Owner offering ownership onward to the former Owner is <c>201</c>; and the former
    /// Owner then leaving is <c>204</c>, the end of the hand-over. The Space holds one Owner after each. Account deletion does
    /// not exist yet; that half is met vacuously and owned by the deletion story.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "AR-12")]
    public async Task The_new_Owner_is_bound_as_any_Owner_is_and_the_former_Owner_can_leave()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var formerOwnerAccount = Guid.Empty;
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);
        var ownersAfter = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));
            var recipient = space.Members["recipient"];
            var offer = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);

            formerOwnerAccount = space.OwnerAccountId;

            await recipient.Session.FetchAntiforgeryTokenAsync();
            answers["accept"] = await AcceptOverWireAsync(recipient.Session, spaceId, offer.Id);

            async Task StepAsync(string name, Func<Task<WireResponse>> act)
            {
                answers[name] = await act();
                ownersAfter[name] = await wire.SpacesNotHoldingExactlyOneOwnerAsync([spaceId]);
            }

            await space.Owner.FetchAntiforgeryTokenAsync();
            await StepAsync("new Owner leaves", () => LeaveOverWireAsync(recipient.Session, spaceId));
            await StepAsync("former Owner removes the new Owner", () => RemoveOverWireAsync(space.Owner, spaceId, recipient.MembershipId));
            await StepAsync("former Owner changes the new Owner's Role", () => ChangeRoleOverWireAsync(space.Owner, spaceId, recipient.MembershipId, "member"));
            await StepAsync("new Owner offers onward", () => OfferOverWireAsync(recipient.Session, spaceId, space.OwnerMembershipId));
            await StepAsync("former Owner leaves", () => LeaveOverWireAsync(space.Owner, spaceId));
        });

        Assert.True(answers["accept"].StatusCode == HttpStatusCode.NoContent, $"The acceptance: {Described(answers["accept"])}");

        var held = answers["new Owner leaves"];

        Assert.True(held.StatusCode == HttpStatusCode.Conflict, $"The new Owner's leave: {Described(held)}");
        Assert.Equal(ProblemTypes.OwnershipHeld, ProblemType(held));

        var removal = answers["former Owner removes the new Owner"];

        Assert.True(removal.StatusCode == HttpStatusCode.Forbidden, $"The former Owner removing the new Owner: {Described(removal)}");
        Assert.Equal(ProblemTypes.InsufficientRole, ProblemType(removal));

        var change = answers["former Owner changes the new Owner's Role"];

        Assert.True(change.StatusCode == HttpStatusCode.Forbidden, $"The former Owner changing the new Owner's Role: {Described(change)}");
        Assert.Equal(ProblemTypes.InsufficientRole, ProblemType(change));
        Assert.True(answers["new Owner offers onward"].StatusCode == HttpStatusCode.Created, $"The onward offer: {Described(answers["new Owner offers onward"])}");
        Assert.True(answers["former Owner leaves"].StatusCode == HttpStatusCode.NoContent, $"The former Owner's leave: {Described(answers["former Owner leaves"])}");
        Assert.DoesNotContain(await wire.MembershipRowsInAsync(spaceId), row => row.AccountId == formerOwnerAccount);

        foreach (var (step, broken) in ownersAfter)
        {
            Assert.True(broken.Count == 0, $"After '{step}': {string.Join("; ", broken)}");
        }
    }

    /// <summary>
    /// An acceptance sends no email to anyone. The offer's own email to the recipient is awaited first; then the recipient
    /// accepts; then an Invitation to a fresh address is awaited as a fence, so a late dispatch cannot hide behind a quick
    /// read. Neither the recipient nor the former Owner received anything after the offer's email, and the Space's send
    /// record holds only the offer's <c>ownership-offered</c> row.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-43")]
    [Trait("Requirement", "AR-33")]
    public async Task An_acceptance_sends_no_email_to_either_party()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        OfferSpace? space = null;
        WireResponse? accepted = null;
        IReadOnlyList<DroppedEmail> beforeAccept = [];
        IReadOnlyList<DroppedEmail> dropped = [];

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));
            var recipient = space.Members["recipient"];
            var offer = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);
            var fence = InvitationWire.FreshAddress("accept-fence");

            _ = await mail.WaitForEmailsToAsync(recipient.Address, OwnershipOfferWire.IsOwnershipOfferEmail);
            beforeAccept = await mail.ReadAllAsync();

            await recipient.Session.FetchAntiforgeryTokenAsync();
            accepted = await AcceptOverWireAsync(recipient.Session, spaceId, offer.Id);

            // The fence is issued by the new Owner, who now holds the Invitation capability.
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(recipient.Session, spaceId, fence, "member")).StatusCode);
            _ = await mail.WaitForEmailToAsync(fence);

            dropped = await mail.ReadAllAsync();
        });

        Assert.True(accepted?.StatusCode == HttpStatusCode.NoContent, $"The acceptance: {Described(accepted)}");

        foreach (var address in new[] { space!.Members["recipient"].Address, space.OwnerAddress })
        {
            Assert.Equal(
                beforeAccept.Count(email => string.Equals(email.To, address, StringComparison.OrdinalIgnoreCase)),
                dropped.Count(email => string.Equals(email.To, address, StringComparison.OrdinalIgnoreCase)));
        }

        Assert.Equal([OwnershipOfferWire.OwnershipOfferedKind], (await wire.NotificationSendsInAsync(spaceId)).Where(record => record.Kind.StartsWith("ownership", StringComparison.Ordinal)).Select(record => record.Kind));
    }

    /// <summary>
    /// A committed acceptance publishes <c>MembershipChanged</c> once per affected Account - exactly one line naming (former
    /// Owner, Space) and one more naming (recipient, Space) than a bystander who joined the same way - because a lease carries
    /// the Role and two Roles moved. Refusals (a non-recipient's <c>404</c>, a second acceptance's <c>409</c>) and a replay of
    /// the key publish nothing more. Plant 6 (the former Owner's <c>Record</c> dropped) turns this red.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-16")]
    [Trait("Requirement", "AD-9")]
    [Trait("Requirement", "AD-18")]
    public async Task An_acceptance_publishes_MembershipChanged_once_for_each_of_the_two_Accounts_and_refusals_publish_nothing()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString("D");
        var (ownerAccount, recipientAccount, bystanderAccount) = (Guid.Empty, Guid.Empty, Guid.Empty);
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("bystander", DisplayName(), "member"));
            var recipient = space.Members["recipient"];
            var bystander = space.Members["bystander"];
            var offer = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);

            (ownerAccount, recipientAccount, bystanderAccount) = (space.OwnerAccountId, recipient.AccountId, bystander.AccountId);

            await recipient.Session.FetchAntiforgeryTokenAsync();
            await bystander.Session.FetchAntiforgeryTokenAsync();

            answers["not named"] = await AcceptOverWireAsync(bystander.Session, spaceId, offer.Id);
            answers["accept"] = await AcceptOverWireAsync(recipient.Session, spaceId, offer.Id, key);
            answers["replay"] = await AcceptOverWireAsync(recipient.Session, spaceId, offer.Id, key);
            answers["again"] = await AcceptOverWireAsync(recipient.Session, spaceId, offer.Id, Guid.NewGuid().ToString("D"));
        });

        var publishes = log.Split('\n').Where(line => line.Contains(MembershipWire.PublishedEvent, StringComparison.Ordinal)).ToList();
        var described = string.Join(" | ", publishes);

        Assert.True(answers["accept"].StatusCode == HttpStatusCode.NoContent, $"The acceptance: {Described(answers["accept"])}");
        Assert.Equal(HttpStatusCode.NotFound, answers["not named"].StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, answers["replay"].StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, answers["again"].StatusCode);
        Assert.True(PublishesNaming(log, ownerAccount, spaceId) == 1, $"Expected one publish naming the former Owner; the publish lines were: {described}");
        Assert.True(
            PublishesNaming(log, recipientAccount, spaceId) == PublishesNaming(log, bystanderAccount, spaceId) + 1,
            $"Expected the recipient named once more than the bystander; the publish lines were: {described}");
        Assert.All(publishes, line => Assert.DoesNotContain("@", line, StringComparison.Ordinal));
    }

    /// <summary>
    /// The acceptance under its <c>Idempotency-Key</c>: a replay answers the original <c>204</c> and changes nothing more; the
    /// same key on a different offer id is <c>422 idempotency-key-reused</c>; a malformed key is <c>400
    /// invalid-idempotency-key</c>; and a second acceptance under a new key is <c>409 ownership-offer-not-pending</c> - the
    /// guard refuses it, not the key. The recipient is the one Owner, the former Owner an Admin, and the offer
    /// <c>Accepted</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "AD-26")]
    public async Task An_acceptance_replays_under_its_key_and_a_new_key_is_refused_409_ownership_offer_not_pending()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString("D");
        var (ownerAccount, recipientAccount) = (Guid.Empty, Guid.Empty);
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));
            var recipient = space.Members["recipient"];
            var offer = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);

            (ownerAccount, recipientAccount) = (space.OwnerAccountId, recipient.AccountId);

            await recipient.Session.FetchAntiforgeryTokenAsync();
            answers["malformed"] = await AcceptOverWireAsync(recipient.Session, spaceId, offer.Id, new string('k', Idempotency.MaxKeyLength + 1));
            answers["original"] = await AcceptOverWireAsync(recipient.Session, spaceId, offer.Id, key);
            answers["replay"] = await AcceptOverWireAsync(recipient.Session, spaceId, offer.Id, key);
            answers["reused"] = await AcceptOverWireAsync(recipient.Session, spaceId, Guid.NewGuid(), key);
            answers["fresh"] = await AcceptOverWireAsync(recipient.Session, spaceId, offer.Id, Guid.NewGuid().ToString("D"));
        });

        Assert.True(answers["malformed"].StatusCode == HttpStatusCode.BadRequest, $"The malformed key: {Described(answers["malformed"])}");
        Assert.Equal(ProblemTypes.InvalidIdempotencyKey, ProblemType(answers["malformed"]));
        Assert.True(answers["original"].StatusCode == HttpStatusCode.NoContent, $"The original: {Described(answers["original"])}");
        Assert.Equal(answers["original"].StatusCode, answers["replay"].StatusCode);
        Assert.Equal(answers["original"].Body, answers["replay"].Body);
        Assert.True(answers["reused"].StatusCode == HttpStatusCode.UnprocessableContent, $"The reused key: {Described(answers["reused"])}");
        Assert.Equal(ProblemTypes.IdempotencyKeyReused, ProblemType(answers["reused"]));
        Assert.True(answers["fresh"].StatusCode == HttpStatusCode.Conflict, $"A second acceptance: {Described(answers["fresh"])}");
        Assert.Equal(ProblemTypes.OwnershipOfferNotPending, ProblemType(answers["fresh"]));
        Assert.Equal("Accepted", Assert.Single(await wire.OwnershipOffersInAsync(spaceId)).State);
        Assert.Equal("Owner", (await wire.MembershipOfAsync(spaceId, recipientAccount)).Role);
        Assert.Equal("Admin", (await wire.MembershipOfAsync(spaceId, ownerAccount)).Role);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// Never zero Owners, never two, across every Space. The case runs its own batch - two acceptances in two Spaces and a
    /// held race of two acceptances of one offer in a third - and then reads every Space in the database. The class's
    /// database is shared with earlier stories' cases, some of which leave a Space with no Owner on purpose
    /// (<see cref="WireDatabase.SpacesNotHoldingExactlyOneOwnerAsync"/>'s remark), so the invariant read is "no Space is
    /// broken that was not broken before the batch", and every Space the batch touched holds exactly one Owner. Readers see
    /// only committed state under RCSI, which is the observable state the criterion names. Order-independent: it relies on
    /// no other case having run.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AR-12")]
    [Trait("Requirement", "AD-5")]
    public async Task No_Space_in_the_database_holds_zero_or_two_Owners_after_the_acceptance_cases()
    {
        var database = Available();
        Guid[] spaceIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        var accepted = new List<WireResponse>();

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();
        var brokenBefore = await wire.SpacesNotHoldingExactlyOneOwnerAsync(await wire.AllSpaceIdsAsync());

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            foreach (var (spaceId, role) in new[] { (spaceIds[0], "admin"), (spaceIds[1], "viewer") })
            {
                var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), role));
                var offer = await OfferedAsync(space.Owner, spaceId, space.Members["recipient"].MembershipId);

                await space.Members["recipient"].Session.FetchAntiforgeryTokenAsync();
                accepted.Add(await AcceptOverWireAsync(space.Members["recipient"].Session, spaceId, offer.Id));
            }

            var raced = await OfferableSpaceAsync(client, database, wire, mail, spaceIds[2], DisplayName(), ("recipient", DisplayName(), "member"));
            var racedOffer = await OfferedAsync(raced.Owner, spaceIds[2], raced.Members["recipient"].MembershipId);
            var session = raced.Members["recipient"].Session;

            await MadePresentAsync(session, spaceIds[2]);
            await session.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(spaceIds[2], cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeApplicationLockAsync(holder, held, MembershipsLockOf(spaceIds[2]), cancellationToken);

            var first = AcceptOverWireAsync(session, spaceIds[2], racedOffer.Id, Guid.NewGuid().ToString("D"));
            var second = AcceptOverWireAsync(session, spaceIds[2], racedOffer.Id, Guid.NewGuid().ToString("D"));

            await Yello.Tests.Slices.Tasks.TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(first, second), cancellationToken);
            await held.CommitAsync(cancellationToken);

            accepted.AddRange(await Task.WhenAll(first, second));
        });

        Assert.Equal(3, accepted.Count(answer => answer.StatusCode == HttpStatusCode.NoContent));

        var brokenAfter = await wire.SpacesNotHoldingExactlyOneOwnerAsync(await wire.AllSpaceIdsAsync());

        Assert.Empty(brokenAfter.Except(brokenBefore, StringComparer.Ordinal));
        Assert.Empty(await wire.SpacesNotHoldingExactlyOneOwnerAsync(spaceIds));
    }

    /// <summary>
    /// The acceptance as the browser sends it: the Session's cookie and anti-forgery header, no body, and an
    /// <c>Idempotency-Key</c> when one is given.
    /// </summary>
    private static Task<WireResponse> AcceptOverWireAsync(WireSession session, Guid spaceId, Guid offerId, string? key = null) =>
        session.SendWithHeadersAsync(HttpMethod.Post, OwnershipOfferRoutes.AcceptanceOf(spaceId, offerId), content: null, KeyHeaders(key));

    /// <summary>
    /// <paramref name="before"/> as a committed acceptance leaves it: the offering Owner's row <c>Admin</c>, the recipient's
    /// <c>Owner</c>, every other row as it was.
    /// </summary>
    private static List<MembershipRow> Swapped(IEnumerable<MembershipRow> before, Guid formerOwnerMembership, Guid recipientMembership) =>
        [.. before.Select(row =>
        {
            if (row.Id == recipientMembership)
            {
                return row with { Role = "Owner" };
            }

            return row.Id == formerOwnerMembership ? row with { Role = "Admin" } : row;
        })];

    /// <summary>
    /// Plants an <c>AFTER UPDATE</c> trigger on <c>Membership</c> that, when <paramref name="ownerMembershipId"/> turns from
    /// <c>Owner</c> to <c>Admin</c>, sets <paramref name="thirdMembershipId"/> to <c>Owner</c>. A test-only object in this
    /// class's own database, dropped by the caller in a <c>finally</c>; it acts on no other row.
    /// </summary>
    /// <returns>The trigger's name, for <see cref="DropTriggerAsync"/>.</returns>
    private static async Task<string> PlantSecondOwnerTriggerAsync(RegistrationDatabase database, Guid ownerMembershipId, Guid thirdMembershipId)
    {
        var name = "TR_Membership_SecondOwner_" + Guid.NewGuid().ToString("N");

        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Review SQL queries - DDL takes no parameters; the name and both ids are GUIDs formatted here.
        command.CommandText =
            $"CREATE TRIGGER dbo.[{name}] ON dbo.[Membership] AFTER UPDATE AS " +
            "SET NOCOUNT ON; " +
            $"IF EXISTS (SELECT 1 FROM inserted AS i JOIN deleted AS d ON d.Id = i.Id WHERE i.Id = '{ownerMembershipId:D}' AND d.Role = N'Owner' AND i.Role = N'Admin') " +
            $"UPDATE dbo.[Membership] SET Role = N'Owner' WHERE Id = '{thirdMembershipId:D}';";
#pragma warning restore CA2100

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        return name;
    }
}
