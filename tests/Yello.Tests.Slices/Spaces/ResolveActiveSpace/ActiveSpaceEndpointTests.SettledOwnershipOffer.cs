using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// The states an Ownership Offer leaves <c>Pending</c> for, over the wire. A settled offer - accepted, declined, revoked,
/// lapsed by event, lapsed because a new offer superseded it after its expiry, or stored <c>Pending</c> past its expiry - is
/// <c>409 ownership-offer-not-pending</c> to the party its transition belongs to (the recipient for an acceptance or a
/// decline, the Owner for a revoke) and changes nothing. The Owner must make a new offer, and only the new one is acceptable:
/// an acceptance by offer id can never land on a newer offer, which is why a stale tab never transfers a Space. Expiry by the
/// passage of time writes nothing and so sends nothing; the only request-path write of <c>Lapsed</c> on an expired row is a new
/// offer superseding it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Seeding.</b> Through the product wherever the product can reach the state: offers, declines, revokes, removals and
/// joins go through their routes. Expiry is reached only by seeding the past on the database clock: a product-made offer's
/// <c>ExpiresAt</c> is moved with <see cref="WireDatabase.SetOwnershipOfferExpiresAtAsync"/>, never a fake clock. The
/// terminal rows of the decline theory are seeded with <see cref="WireDatabase.AddOwnershipOfferAsync"/> after the Space's
/// live offer was revoked (the existing anchor), as the acceptance's theory does; the <c>Accepted</c> row is seeded because
/// the product cannot reach it with the offerer still the Owner.
/// </para>
/// <para>
/// <b>"Lost to a concurrent offer"</b> is not a row here: the losing request is a second <b>offer</b>, refused
/// <c>409 ownership-offer-pending</c> with no row written, so no offer id exists to transition. It is proved by
/// <c>Two_offers_held_on_the_Memberships_lock_commit_one_and_refuse_the_other_409</c>.
/// </para>
/// <para>
/// <b>Mail.</b> Every case joins Members through an Invitation and makes an offer, so every case stops the pooled Hosts first
/// (<see cref="SoleDispatcherWireAsync"/>) and runs a Host of its own with its own <see cref="MailDrop"/>. An absence of
/// email is fenced by an Invitation to a fresh address that must arrive.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// Neither a decline nor a revoke publishes or emails. The recipient declines its offer after the offer's own email has
    /// arrived; a second offer, to a bystander, is revoked after its own email has arrived; then a fence Invitation is awaited.
    /// No publish names the Owner (never joined), and the recipient and the bystander are each named exactly as often as a
    /// control Member that joined the same way and was never offered anything, neither offered Member nor the Owner received anything after its offer, and the Space's
    /// send record holds only the two <c>ownership-offered</c> rows. Every Membership row is as it was.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "FR-43")]
    [Trait("Requirement", "AR-16")]
    public async Task A_decline_and_a_revoke_publish_nothing_and_send_no_email()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var (ownerAccount, recipientAccount, bystanderAccount, controlAccount) = (Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty);
        OfferSpace? space = null;
        IReadOnlyList<MembershipRow> before = [];
        IReadOnlyList<DroppedEmail> beforeDecline = [];
        IReadOnlyList<DroppedEmail> beforeRevoke = [];
        IReadOnlyList<DroppedEmail> dropped = [];
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("bystander", DisplayName(), "member"), ("control", DisplayName(), "member"));
            var recipient = space.Members["recipient"];
            var bystander = space.Members["bystander"];
            var fence = InvitationWire.FreshAddress("settled-fence");

            (ownerAccount, recipientAccount, bystanderAccount, controlAccount) = (space.OwnerAccountId, recipient.AccountId, bystander.AccountId, space.Members["control"].AccountId);
            before = await wire.MembershipRowsInAsync(spaceId);

            var declined = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);

            _ = await mail.WaitForEmailsToAsync(recipient.Address, OwnershipOfferWire.IsOwnershipOfferEmail);
            beforeDecline = await mail.ReadAllAsync();

            await recipient.Session.FetchAntiforgeryTokenAsync();
            answers["decline"] = await DeclineOverWireAsync(recipient.Session, spaceId, declined.Id);

            var revoked = await OfferedAsync(space.Owner, spaceId, bystander.MembershipId);

            _ = await mail.WaitForEmailsToAsync(bystander.Address, OwnershipOfferWire.IsOwnershipOfferEmail);
            beforeRevoke = await mail.ReadAllAsync();
            answers["revoke"] = await RevokeOfferOverWireAsync(space.Owner, spaceId, revoked.Id);

            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(space.Owner, spaceId, fence, "member")).StatusCode);
            _ = await mail.WaitForEmailToAsync(fence);

            dropped = await mail.ReadAllAsync();
        });

        Assert.True(answers["decline"].StatusCode == HttpStatusCode.NoContent, $"The decline: {Described(answers["decline"])}");
        Assert.True(answers["revoke"].StatusCode == HttpStatusCode.NoContent, $"The revoke: {Described(answers["revoke"])}");
        Assert.Equal(0, PublishesNaming(log, ownerAccount, spaceId));
        Assert.Equal(PublishesNaming(log, controlAccount, spaceId), PublishesNaming(log, recipientAccount, spaceId));
        Assert.Equal(PublishesNaming(log, controlAccount, spaceId), PublishesNaming(log, bystanderAccount, spaceId));

        int To(IEnumerable<DroppedEmail> emails, string address) =>
            emails.Count(email => string.Equals(email.To, address, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(To(beforeDecline, space!.Members["recipient"].Address), To(dropped, space.Members["recipient"].Address));
        Assert.Equal(To(beforeRevoke, space.Members["bystander"].Address), To(dropped, space.Members["bystander"].Address));
        Assert.Equal(To(beforeDecline, space.OwnerAddress), To(dropped, space.OwnerAddress));
        Assert.Equal(
            [OwnershipOfferWire.OwnershipOfferedKind, OwnershipOfferWire.OwnershipOfferedKind],
            (await wire.NotificationSendsInAsync(spaceId)).Where(record => record.Kind.StartsWith("ownership", StringComparison.Ordinal)).Select(record => record.Kind));
        Assert.Equal(before, await wire.MembershipRowsInAsync(spaceId));
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// A declined offer cannot be accepted, and the Owner must make a new one: the recipient declines (<c>204</c>), the Owner
    /// offers the same recipient again (<c>201</c>), the old id's acceptance is <c>409 ownership-offer-not-pending</c> and the
    /// new id's is <c>204</c>. The old id is the one a stale dialog would hold, and it never lands on the newer offer. The old
    /// row stays <c>Declined</c>; the Space holds one Owner, the recipient.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AR-12")]
    public async Task After_a_decline_only_a_new_offer_can_be_accepted()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var (oldId, newId, recipientAccount) = (Guid.Empty, Guid.Empty, Guid.Empty);
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));
            var recipient = space.Members["recipient"];

            recipientAccount = recipient.AccountId;
            oldId = (await OfferedAsync(space.Owner, spaceId, recipient.MembershipId)).Id;

            await recipient.Session.FetchAntiforgeryTokenAsync();
            answers["decline"] = await DeclineOverWireAsync(recipient.Session, spaceId, oldId);
            answers["new offer"] = await OfferOverWireAsync(space.Owner, spaceId, recipient.MembershipId);
            newId = answers["new offer"].StatusCode == HttpStatusCode.Created ? OwnershipOfferWire.Parse(answers["new offer"].Body).Id : Guid.Empty;
            answers["old accept"] = await AcceptOverWireAsync(recipient.Session, spaceId, oldId);
            answers["new accept"] = await AcceptOverWireAsync(recipient.Session, spaceId, newId);
        });

        Assert.True(answers["decline"].StatusCode == HttpStatusCode.NoContent, $"The decline: {Described(answers["decline"])}");
        Assert.True(answers["new offer"].StatusCode == HttpStatusCode.Created, $"The new offer: {Described(answers["new offer"])}");
        Assert.True(answers["old accept"].StatusCode == HttpStatusCode.Conflict, $"Accepting the declined offer: {Described(answers["old accept"])}");
        Assert.Equal(ProblemTypes.OwnershipOfferNotPending, ProblemType(answers["old accept"]));
        Assert.True(answers["new accept"].StatusCode == HttpStatusCode.NoContent, $"Accepting the new offer: {Described(answers["new accept"])}");

        var offers = await wire.OwnershipOffersInAsync(spaceId);

        Assert.Equal("Declined", Assert.Single(offers, row => row.Id == oldId).State);
        Assert.Equal("Accepted", Assert.Single(offers, row => row.Id == newId).State);
        Assert.Equal("Owner", (await wire.MembershipOfAsync(spaceId, recipientAccount)).Role);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// An offer lapsed by event cannot be accepted, even by the same Account come back: the Owner removes the recipient (the
    /// offer is written <c>Lapsed</c>), the same Account is invited again and joins through the product with a <b>new</b>
    /// Membership, and the Owner offers that Membership (<c>201</c>). The old id's acceptance is the in-Space not-found -
    /// byte for byte the answer for a Space that does not exist, and unrecorded - because the old offer names a Membership that
    /// no longer exists and so does not name the caller. The new id's acceptance is <c>204</c>. The old row stays
    /// <c>Lapsed</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AD-3")]
    public async Task After_a_lapse_by_removal_only_a_new_offer_to_the_rejoined_Membership_can_be_accepted()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var (oldId, newId, recipientAccount, oldMembership, newMembership) = (Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty);
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));
            var recipient = space.Members["recipient"];

            (recipientAccount, oldMembership) = (recipient.AccountId, recipient.MembershipId);
            oldId = (await OfferedAsync(space.Owner, spaceId, oldMembership)).Id;
            answers["removal"] = await RemoveOverWireAsync(space.Owner, spaceId, oldMembership);

            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(space.Owner, spaceId, recipient.Address, "member")).StatusCode);

            var invitations = await mail.WaitForEmailsToAsync(recipient.Address, email => !OwnershipOfferWire.IsOwnershipOfferEmail(email), 2);

            await recipient.Session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.OK, (await AcceptAsync(recipient.Session, InvitationWire.TokenIn(invitations[^1]))).StatusCode);

            newMembership = (await wire.MembershipOfAsync(spaceId, recipientAccount)).Id;
            newId = (await OfferedAsync(space.Owner, spaceId, newMembership)).Id;

            await recipient.Session.FetchAntiforgeryTokenAsync();
            answers["absent"] = await AcceptOverWireAsync(recipient.Session, Guid.NewGuid(), Guid.NewGuid());
            answers["old accept"] = await AcceptOverWireAsync(recipient.Session, spaceId, oldId);
            answers["new accept"] = await AcceptOverWireAsync(recipient.Session, spaceId, newId);
        });

        Assert.True(answers["removal"].StatusCode == HttpStatusCode.NoContent, $"The removal: {Described(answers["removal"])}");
        Assert.NotEqual(oldMembership, newMembership);
        Assert.True(answers["old accept"].StatusCode == HttpStatusCode.NotFound, $"Accepting the lapsed offer: {Described(answers["old accept"])}");
        Assert.Equal(ProblemTypes.NotAvailable, ProblemType(answers["old accept"]));
        Assert.Equal(answers["absent"].Body, answers["old accept"].Body);
        Assert.Equal(answers["absent"].HeaderNames, answers["old accept"].HeaderNames);
        Assert.DoesNotContain(await RefusalsOfAsync(database, recipientAccount), row => row.TargetSpaceId == spaceId);
        Assert.True(answers["new accept"].StatusCode == HttpStatusCode.NoContent, $"Accepting the new offer: {Described(answers["new accept"])}");

        var offers = await wire.OwnershipOffersInAsync(spaceId);

        Assert.Equal("Lapsed", Assert.Single(offers, row => row.Id == oldId).State);
        Assert.Equal("Accepted", Assert.Single(offers, row => row.Id == newId).State);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// An offer past its expiry is superseded only by a new offer, the one request-path write of <c>Lapsed</c> on an expired
    /// row: a product offer's <c>ExpiresAt</c> is moved one minute into the database clock's past and the row is still
    /// <c>Pending</c>; a new offer to the same recipient is <c>201</c> and the old row is now <c>Lapsed</c>, its
    /// <c>ExpiresAt</c> as seeded. The old id's acceptance is <c>409 ownership-offer-not-pending</c> and the new id's is
    /// <c>204</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-27")]
    [Trait("Requirement", "AR-31")]
    [Trait("Assumption", "PRD-12-3")]
    public async Task After_an_expiry_superseded_by_a_new_offer_only_the_new_offer_can_be_accepted()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var (oldId, newId) = (Guid.Empty, Guid.Empty);
        var expiredAt = DateTimeOffset.MinValue;
        OwnershipOfferRow? expired = null;
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));
            var recipient = space.Members["recipient"];

            oldId = (await OfferedAsync(space.Owner, spaceId, recipient.MembershipId)).Id;
            expiredAt = (await wire.DatabaseNowAsync()).AddMinutes(-1);
            await wire.SetOwnershipOfferExpiresAtAsync(spaceId, oldId, expiredAt);
            expired = Assert.Single(await wire.OwnershipOffersInAsync(spaceId));

            answers["new offer"] = await OfferOverWireAsync(space.Owner, spaceId, recipient.MembershipId);
            newId = answers["new offer"].StatusCode == HttpStatusCode.Created ? OwnershipOfferWire.Parse(answers["new offer"].Body).Id : Guid.Empty;

            await recipient.Session.FetchAntiforgeryTokenAsync();
            answers["old accept"] = await AcceptOverWireAsync(recipient.Session, spaceId, oldId);
            answers["new accept"] = await AcceptOverWireAsync(recipient.Session, spaceId, newId);
        });

        Assert.Equal("Pending", expired!.State);
        Assert.True(answers["new offer"].StatusCode == HttpStatusCode.Created, $"The new offer: {Described(answers["new offer"])}");
        Assert.True(answers["old accept"].StatusCode == HttpStatusCode.Conflict, $"Accepting the superseded offer: {Described(answers["old accept"])}");
        Assert.Equal(ProblemTypes.OwnershipOfferNotPending, ProblemType(answers["old accept"]));
        Assert.True(answers["new accept"].StatusCode == HttpStatusCode.NoContent, $"Accepting the new offer: {Described(answers["new accept"])}");

        var offers = await wire.OwnershipOffersInAsync(spaceId);

        Assert.Equal(expired with { State = "Lapsed" }, Assert.Single(offers, row => row.Id == oldId));
        Assert.Equal("Accepted", Assert.Single(offers, row => row.Id == newId).State);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// The named recipient may not decline an offer that is no longer actionable - accepted, revoked, lapsed, already
    /// declined, or still stored <c>Pending</c> past its expiry: <c>409 ownership-offer-not-pending</c> with no address in the
    /// body, nothing recorded, the stored row unchanged (the expired one stays <c>Pending</c>), and every Membership unchanged.
    /// Anchor: the Space's live offer was revoked <c>204</c> first.
    /// </summary>
    /// <param name="state">Which offer.</param>
    /// <returns>A task.</returns>
    [Theory]
    [MemberData(nameof(OffersNoLongerOpenToTheRecipient))]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AR-13")]
    [Trait("Assumption", "PRD-12-3")]
    public async Task A_settled_offer_is_409_to_its_recipients_decline_and_changes_nothing(string state)
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
            refused = await DeclineOverWireAsync(recipient.Session, spaceId, id);
        });

        Assert.True(anchor?.StatusCode == HttpStatusCode.NoContent, $"The anchor: {Described(anchor)}");
        Assert.True(refused?.StatusCode == HttpStatusCode.Conflict, $"Declining the {state} offer: {Described(refused)}");
        Assert.Equal(ProblemTypes.OwnershipOfferNotPending, ProblemType(refused));
        Assert.DoesNotContain("@", refused.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(await RefusalsOfAsync(database, recipientAccount), row => row.TargetSpaceId == spaceId);
        Assert.Equal(seeded, Assert.Single(await wire.OwnershipOffersInAsync(spaceId), row => row.Id == seeded!.Id));
        Assert.Equal(before, await wire.MembershipRowsInAsync(spaceId));
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// The superseded offer is settled for every party: a product offer moved past its expiry and then superseded by a new
    /// offer to another Member is <c>Lapsed</c>; its recipient's decline and the Owner's revoke of it are each <c>409
    /// ownership-offer-not-pending</c> with no address in the body, nothing is recorded, the old row stays <c>Lapsed</c>, the
    /// new offer stays <c>Pending</c>, and every Membership is unchanged.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AD-27")]
    [Trait("Assumption", "PRD-12-3")]
    public async Task A_superseded_expired_offer_is_409_to_its_recipients_decline_and_to_the_Owners_revoke()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var (ownerAccount, recipientAccount, oldId) = (Guid.Empty, Guid.Empty, Guid.Empty);
        IReadOnlyList<MembershipRow> before = [];
        IReadOnlyList<OwnershipOfferRow> offersBefore = [];
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("other", DisplayName(), "member"));
            var recipient = space.Members["recipient"];

            (ownerAccount, recipientAccount) = (space.OwnerAccountId, recipient.AccountId);
            oldId = (await OfferedAsync(space.Owner, spaceId, recipient.MembershipId)).Id;
            await wire.SetOwnershipOfferExpiresAtAsync(spaceId, oldId, (await wire.DatabaseNowAsync()).AddMinutes(-1));
            _ = await OfferedAsync(space.Owner, spaceId, space.Members["other"].MembershipId);

            before = await wire.MembershipRowsInAsync(spaceId);
            offersBefore = await wire.OwnershipOffersInAsync(spaceId);

            await recipient.Session.FetchAntiforgeryTokenAsync();
            answers["decline"] = await DeclineOverWireAsync(recipient.Session, spaceId, oldId);
            answers["revoke"] = await RevokeOfferOverWireAsync(space.Owner, spaceId, oldId);
        });

        Assert.Equal("Lapsed", Assert.Single(offersBefore, row => row.Id == oldId).State);
        Assert.Equal("Pending", Assert.Single(offersBefore, row => row.Id != oldId).State);

        foreach (var (name, answer) in answers)
        {
            Assert.True(answer.StatusCode == HttpStatusCode.Conflict, $"The {name} of the superseded offer: {Described(answer)}");
            Assert.Equal(ProblemTypes.OwnershipOfferNotPending, ProblemType(answer));
            Assert.DoesNotContain("@", answer.Body, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(await RefusalsOfAsync(database, recipientAccount), row => row.TargetSpaceId == spaceId);
        Assert.DoesNotContain(await RefusalsOfAsync(database, ownerAccount), row => row.TargetSpaceId == spaceId);
        Assert.Equal(offersBefore, await wire.OwnershipOffersInAsync(spaceId));
        Assert.Equal(before, await wire.MembershipRowsInAsync(spaceId));
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// The Space is resolved before the idempotency record is read: the recipient declines under a key (<c>204</c>), the Owner
    /// removes the recipient, and the same key from the same Session is then the boundary <c>404 not-available</c> - byte for
    /// byte the answer for a Space that does not exist, recorded <c>CrossSpace</c> - and not the recorded <c>204</c>. The
    /// offer stays <c>Declined</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "AD-3")]
    public async Task A_decline_replayed_after_its_recipient_was_removed_is_the_boundary_404_and_not_the_recorded_204()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString("D");
        var recipientAccount = Guid.Empty;
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));
            var recipient = space.Members["recipient"];
            var offer = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);

            recipientAccount = recipient.AccountId;

            await recipient.Session.FetchAntiforgeryTokenAsync();
            answers["original"] = await DeclineOverWireAsync(recipient.Session, spaceId, offer.Id, key);
            answers["removal"] = await RemoveOverWireAsync(space.Owner, spaceId, recipient.MembershipId);
            answers["absent"] = await DeclineOverWireAsync(recipient.Session, Guid.NewGuid(), offer.Id, Guid.NewGuid().ToString("D"));
            answers["replay"] = await DeclineOverWireAsync(recipient.Session, spaceId, offer.Id, key);
        });

        Assert.True(answers["original"].StatusCode == HttpStatusCode.NoContent, $"The original: {Described(answers["original"])}");
        Assert.True(answers["removal"].StatusCode == HttpStatusCode.NoContent, $"The removal: {Described(answers["removal"])}");
        Assert.True(answers["replay"].StatusCode == HttpStatusCode.NotFound, $"The replay after the removal: {Described(answers["replay"])}");
        Assert.Equal(ProblemTypes.NotAvailable, ProblemType(answers["replay"]));
        Assert.Equal(answers["absent"].Body, answers["replay"].Body);
        Assert.Equal(answers["absent"].HeaderNames, answers["replay"].HeaderNames);
        Assert.Contains(await RefusalsOfAsync(database, recipientAccount), row => row.TargetSpaceId == spaceId && row.Kind == "CrossSpace");
        Assert.Equal("Declined", Assert.Single(await wire.OwnershipOffersInAsync(spaceId)).State);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// Expiry by the passage of time writes nothing, so there is no event to send anything on. A product offer's own email is
    /// awaited, then its <c>ExpiresAt</c> is moved one minute into the database clock's past. The Owner's pending read and the
    /// recipient's read each answer <c>204</c>; the recipient's acceptance and decline and the Owner's revoke are each
    /// <c>409 ownership-offer-not-pending</c>; the stored row is still <c>Pending</c> with the seeded <c>ExpiresAt</c>, read
    /// straight from the table; after a fence Invitation neither party has received anything more and the send record holds
    /// only the one <c>ownership-offered</c> row; no publish names the Owner, and the recipient is named as often as a
    /// bystander.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-27")]
    [Trait("Requirement", "AR-31")]
    [Trait("Requirement", "FR-43")]
    [Trait("Assumption", "PRD-12-3")]
    public async Task Expiry_by_time_writes_nothing_sends_nothing_and_every_transition_is_409()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var (ownerAccount, recipientAccount, bystanderAccount) = (Guid.Empty, Guid.Empty, Guid.Empty);
        OfferSpace? space = null;
        OwnershipOfferRow? expired = null;
        IReadOnlyList<DroppedEmail> beforeExpiry = [];
        IReadOnlyList<DroppedEmail> dropped = [];
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("bystander", DisplayName(), "member"));
            var recipient = space.Members["recipient"];
            var fence = InvitationWire.FreshAddress("expiry-fence");
            var offer = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);

            (ownerAccount, recipientAccount, bystanderAccount) = (space.OwnerAccountId, recipient.AccountId, space.Members["bystander"].AccountId);

            _ = await mail.WaitForEmailsToAsync(recipient.Address, OwnershipOfferWire.IsOwnershipOfferEmail);
            beforeExpiry = await mail.ReadAllAsync();

            await wire.SetOwnershipOfferExpiresAtAsync(spaceId, offer.Id, (await wire.DatabaseNowAsync()).AddMinutes(-1));
            expired = Assert.Single(await wire.OwnershipOffersInAsync(spaceId));

            await recipient.Session.FetchAntiforgeryTokenAsync();
            answers["pending read"] = await PendingOfferOverWireAsync(space.Owner, spaceId);
            answers["mine"] = await MineOverWireAsync(recipient.Session, spaceId);
            answers["accept"] = await AcceptOverWireAsync(recipient.Session, spaceId, offer.Id);
            answers["decline"] = await DeclineOverWireAsync(recipient.Session, spaceId, offer.Id);
            answers["revoke"] = await RevokeOfferOverWireAsync(space.Owner, spaceId, offer.Id);

            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(space.Owner, spaceId, fence, "member")).StatusCode);
            _ = await mail.WaitForEmailToAsync(fence);

            dropped = await mail.ReadAllAsync();
        });

        foreach (var name in new[] { "pending read", "mine" })
        {
            Assert.True(answers[name].StatusCode == HttpStatusCode.NoContent, $"The {name} of the expired offer: {Described(answers[name])}");
        }

        foreach (var name in new[] { "accept", "decline", "revoke" })
        {
            Assert.True(answers[name].StatusCode == HttpStatusCode.Conflict, $"The {name} of the expired offer: {Described(answers[name])}");
            Assert.Equal(ProblemTypes.OwnershipOfferNotPending, ProblemType(answers[name]));
        }

        Assert.Equal("Pending", expired!.State);
        Assert.Equal(expired, Assert.Single(await wire.OwnershipOffersInAsync(spaceId)));

        foreach (var address in new[] { space!.Members["recipient"].Address, space.OwnerAddress })
        {
            Assert.Equal(
                beforeExpiry.Count(email => string.Equals(email.To, address, StringComparison.OrdinalIgnoreCase)),
                dropped.Count(email => string.Equals(email.To, address, StringComparison.OrdinalIgnoreCase)));
        }

        Assert.Equal([OwnershipOfferWire.OwnershipOfferedKind], (await wire.NotificationSendsInAsync(spaceId)).Where(record => record.Kind.StartsWith("ownership", StringComparison.Ordinal)).Select(record => record.Kind));
        Assert.Equal(0, PublishesNaming(log, ownerAccount, spaceId));
        Assert.Equal(PublishesNaming(log, bystanderAccount, spaceId), PublishesNaming(log, recipientAccount, spaceId));
        await AssertOneOwnerEachAsync(wire, spaceId);
    }
}
