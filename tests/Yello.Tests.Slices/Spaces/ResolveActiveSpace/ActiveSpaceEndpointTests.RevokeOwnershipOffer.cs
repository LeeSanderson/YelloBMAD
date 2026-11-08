using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// The Owner's side of a pending offer over the wire: the read, <c>GET /api/v1/spaces/{spaceId}/ownership-offers/pending</c>
/// (<c>200</c> with the offer, or <c>204</c> when no offer is actionable), and the revoke,
/// <c>DELETE .../ownership-offers/{offerId}</c> (<c>204</c>), both under <c>Capability.OfferOwnershipTransfer</c>. A revoke is a
/// guarded <c>UPDATE</c> to <c>Revoked</c> that moves no Role; an offer no longer pending is <c>409
/// ownership-offer-not-pending</c>; an id naming no offer in this Space is the unrecorded boundary <c>404</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Status codes follow the comparable endpoints:</b> the revoke answers as the Invitation revoke does (<c>204</c>,
/// <c>409</c> with a stable type for a row no longer pending, the boundary <c>404</c> for an unknown id). The read's <c>204</c>
/// for "nothing pending" has no Space-scoped precedent (the Session check, <c>GET /api/v1/session</c>, is the one other
/// bodiless <c>GET</c>).
/// </para>
/// <para>
/// <b>Seeding</b> is as in <c>.OfferOwnership.cs</c>. Terminal and time-expired offers are written directly
/// (<see cref="WireDatabase.AddOwnershipOfferAsync"/>) after the live offer has been revoked, because a Space holds at most
/// one <c>Pending</c> row.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// The stored states a revoke must refuse: two terminal states, and a <c>Pending</c> row past its expiry.
    /// </summary>
    public static TheoryData<string> OffersNoLongerPending => new() { "Revoked", "Lapsed", "Expired" };

    /// <summary>
    /// The Owner revokes the pending offer: <c>204</c> with no body; the row is retained with <c>State = Revoked</c> and
    /// every other column unchanged; every Membership and Role is exactly as before; the read then answers <c>204</c>; and a
    /// new offer is <c>201</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AR-12")]
    public async Task The_Owner_revokes_a_pending_offer_and_every_Membership_and_Role_is_as_it_was()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        IReadOnlyList<MembershipRow> before = [];
        OwnershipOfferRow? pending = null;
        WireResponse? revoked = null;
        WireResponse? readAfter = null;
        WireResponse? offeredAgain = null;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("admin", DisplayName(), "admin"));
            var offer = await OfferedAsync(space.Owner, spaceId, space.Members["recipient"].MembershipId);

            before = await wire.MembershipRowsInAsync(spaceId);
            pending = Assert.Single(await wire.OwnershipOffersInAsync(spaceId));
            revoked = await RevokeOfferOverWireAsync(space.Owner, spaceId, offer.Id);
            readAfter = await PendingOfferOverWireAsync(space.Owner, spaceId);
            offeredAgain = await OfferOverWireAsync(space.Owner, spaceId, space.Members["admin"].MembershipId);
        });

        Assert.True(revoked?.StatusCode == HttpStatusCode.NoContent, $"The revoke: {Described(revoked)}");
        Assert.Equal(string.Empty, revoked.Body);
        Assert.Equal(pending! with { State = "Revoked" }, Assert.Single(await wire.OwnershipOffersInAsync(spaceId), row => row.Id == pending.Id));
        Assert.Equal(before, await wire.MembershipRowsInAsync(spaceId));
        Assert.True(readAfter?.StatusCode == HttpStatusCode.NoContent, $"The read after the revoke: {Described(readAfter)}");
        Assert.True(offeredAgain?.StatusCode == HttpStatusCode.Created, $"A new offer after the revoke: {Described(offeredAgain)}");
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// An offer already revoked, lapsed, or still stored <c>Pending</c> but past its expiry is not actionable: its revoke is
    /// <c>409 ownership-offer-not-pending</c> with no address in the body, nothing is recorded as a refusal, and the stored row
    /// is unchanged (the expired one stays <c>Pending</c>: expiry is never written). Anchor: the Space's live offer was revoked
    /// <c>204</c> first.
    /// </summary>
    /// <param name="state">Which non-actionable offer.</param>
    /// <returns>A task.</returns>
    [Theory]
    [MemberData(nameof(OffersNoLongerPending))]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AD-27")]
    [Trait("Assumption", "PRD-12-3")]
    public async Task Revoking_an_offer_that_is_no_longer_pending_is_409_and_changes_nothing(string state)
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var ownerId = Guid.Empty;
        OwnershipOfferRow? seeded = null;
        WireResponse? anchor = null;
        WireResponse? refused = null;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));
            var recipient = space.Members["recipient"].MembershipId;

            ownerId = space.OwnerAccountId;
            anchor = await RevokeOfferOverWireAsync(space.Owner, spaceId, (await OfferedAsync(space.Owner, spaceId, recipient)).Id);

            var now = await wire.DatabaseNowAsync();
            var id = state == "Expired"
                ? await wire.AddOwnershipOfferAsync(spaceId, recipient, space.OwnerMembershipId, "Captured " + Suffix(), "Pending", now.AddMinutes(-1))
                : await wire.AddOwnershipOfferAsync(spaceId, recipient, space.OwnerMembershipId, "Captured " + Suffix(), state, now.AddDays(OwnershipOfferWire.ExpiryDays));

            seeded = Assert.Single(await wire.OwnershipOffersInAsync(spaceId), row => row.Id == id);
            refused = await RevokeOfferOverWireAsync(space.Owner, spaceId, id);
        });

        Assert.True(anchor?.StatusCode == HttpStatusCode.NoContent, $"The anchor: {Described(anchor)}");
        Assert.True(refused?.StatusCode == HttpStatusCode.Conflict, $"Revoking the {state} offer: {Described(refused)}");
        Assert.Equal(ProblemTypes.OwnershipOfferNotPending, ProblemType(refused));
        Assert.DoesNotContain("@", refused.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(await RefusalsOfAsync(database, ownerId), row => row.TargetSpaceId == spaceId);
        Assert.Equal(seeded, Assert.Single(await wire.OwnershipOffersInAsync(spaceId), row => row.Id == seeded!.Id));
    }

    /// <summary>
    /// An id naming no offer in this Space - one that never existed, and another Space's pending offer - is answered the
    /// boundary <c>404 not-available</c>, byte for byte the answer for a Space that does not exist, and is not recorded
    /// against this Space. The other Space's offer stays <c>Pending</c>. Anchor: this Space's own offer is revoked
    /// <c>204</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-20")]
    [Trait("Requirement", "FR-8")]
    public async Task Revoking_an_id_that_names_no_offer_in_this_Space_is_the_boundary_404_and_is_not_recorded()
    {
        var database = Available();
        var (spaceId, foreignSpaceId) = (Guid.NewGuid(), Guid.NewGuid());
        var ownerId = Guid.Empty;
        var foreignOffer = Guid.Empty;
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var foreign = await OfferableSpaceAsync(client, database, wire, mail, foreignSpaceId, DisplayName(), ("recipient", DisplayName(), "member"));

            foreignOffer = (await OfferedAsync(foreign.Owner, foreignSpaceId, foreign.Members["recipient"].MembershipId)).Id;

            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));
            var own = await OfferedAsync(space.Owner, spaceId, space.Members["recipient"].MembershipId);

            ownerId = space.OwnerAccountId;
            answers["absent"] = await RevokeOfferOverWireAsync(space.Owner, Guid.NewGuid(), Guid.NewGuid());
            answers["unknown"] = await RevokeOfferOverWireAsync(space.Owner, spaceId, Guid.NewGuid());
            answers["foreign"] = await RevokeOfferOverWireAsync(space.Owner, spaceId, foreignOffer);
            answers["anchor"] = await RevokeOfferOverWireAsync(space.Owner, spaceId, own.Id);
        });

        Assert.True(answers["anchor"].StatusCode == HttpStatusCode.NoContent, $"The anchor: {Described(answers["anchor"])}");
        Assert.Equal(HttpStatusCode.NotFound, answers["absent"].StatusCode);

        foreach (var name in new[] { "unknown", "foreign" })
        {
            Assert.True(answers[name].StatusCode == HttpStatusCode.NotFound, $"The {name} id: {Described(answers[name])}");
            Assert.Equal(ProblemTypes.NotAvailable, ProblemType(answers[name]));
            Assert.Equal(answers["absent"].Body, answers[name].Body);
            Assert.Equal(answers["absent"].HeaderNames, answers[name].HeaderNames);
        }

        Assert.DoesNotContain(await RefusalsOfAsync(database, ownerId), row => row.TargetSpaceId == spaceId);
        Assert.Equal("Pending", Assert.Single(await wire.OwnershipOffersInAsync(foreignSpaceId), row => row.Id == foreignOffer).State);
    }

    /// <summary>
    /// A replay with the same <c>Idempotency-Key</c> after a committed revoke answers the original <c>204</c>; a second revoke
    /// under a new key is <c>409 ownership-offer-not-pending</c>; a malformed key is <c>400 invalid-idempotency-key</c> and
    /// leaves its offer as it was.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "AD-26")]
    public async Task A_revoke_replays_under_its_key_and_a_new_key_is_refused_409_ownership_offer_not_pending()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString("D");
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("next", DisplayName(), "member"));
            var offer = await OfferedAsync(space.Owner, spaceId, space.Members["recipient"].MembershipId);

            answers["original"] = await RevokeOfferOverWireAsync(space.Owner, spaceId, offer.Id, key);
            answers["replay"] = await RevokeOfferOverWireAsync(space.Owner, spaceId, offer.Id, key);
            answers["fresh"] = await RevokeOfferOverWireAsync(space.Owner, spaceId, offer.Id, Guid.NewGuid().ToString("D"));

            var next = await OfferedAsync(space.Owner, spaceId, space.Members["next"].MembershipId);

            answers["malformed"] = await RevokeOfferOverWireAsync(space.Owner, spaceId, next.Id, new string('k', Idempotency.MaxKeyLength + 1));
        });

        Assert.True(answers["original"].StatusCode == HttpStatusCode.NoContent, $"The original: {Described(answers["original"])}");
        Assert.Equal(answers["original"].StatusCode, answers["replay"].StatusCode);
        Assert.Equal(answers["original"].Body, answers["replay"].Body);
        Assert.True(answers["fresh"].StatusCode == HttpStatusCode.Conflict, $"A second revoke: {Described(answers["fresh"])}");
        Assert.Equal(ProblemTypes.OwnershipOfferNotPending, ProblemType(answers["fresh"]));
        Assert.True(answers["malformed"].StatusCode == HttpStatusCode.BadRequest, $"The malformed key: {Described(answers["malformed"])}");
        Assert.Equal(ProblemTypes.InvalidIdempotencyKey, ProblemType(answers["malformed"]));
        Assert.Equal(["Pending", "Revoked"], (await wire.OwnershipOffersInAsync(spaceId)).Select(row => row.State).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The Owner reads the pending offer: <c>200</c>, exactly the offer's five members, the recipient by Membership id and by
    /// display name (never an address: no <c>@</c> anywhere), the stored <c>ExpiresAt</c>, and a <c>ReadAt</c> taken from the
    /// database clock during the request, from which the client counts the days left.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR14")]
    [Trait("Requirement", "AD-27")]
    [Trait("Requirement", "AD-23")]
    [Trait("Assumption", "PRD-12-3")]
    public async Task The_pending_read_names_the_recipient_and_the_expiry_with_the_database_time()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var recipientName = DisplayName();
        var recipientMembership = Guid.Empty;
        var (before, after) = (DateTimeOffset.MinValue, DateTimeOffset.MinValue);
        WireResponse? read = null;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", recipientName, "admin"));

            recipientMembership = space.Members["recipient"].MembershipId;
            _ = await OfferedAsync(space.Owner, spaceId, recipientMembership);

            before = await wire.DatabaseNowAsync();
            read = await PendingOfferOverWireAsync(space.Owner, spaceId);
            after = await wire.DatabaseNowAsync();
        });

        Assert.True(read?.StatusCode == HttpStatusCode.OK, $"The read: {Described(read)}");
        Assert.Equal(OwnershipOfferWire.ResponseMembers, MembershipWire.ObjectMemberNames(read.Body));
        Assert.DoesNotContain("@", read.Body, StringComparison.Ordinal);

        var answer = OwnershipOfferWire.Parse(read.Body);
        var row = Assert.Single(await wire.OwnershipOffersInAsync(spaceId));

        Assert.Equal((row.Id, recipientMembership, recipientName, row.ExpiresAt), (answer.Id, answer.RecipientMembershipId, answer.RecipientName, answer.ExpiresAt));
        Assert.InRange(answer.ReadAt, before, after);
    }

    /// <summary>
    /// With no actionable offer the read answers <c>204</c> with no body: before any offer, and with a <c>Pending</c> row
    /// past its expiry (a time-expired offer reads as none, by the shared predicate in the statement's own <c>WHERE</c>).
    /// Anchor: the same route answered <c>200</c> for a live offer in another Space of the same Owner.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR14")]
    [Trait("Requirement", "AD-27")]
    [Trait("Requirement", "AR-31")]
    [Trait("Assumption", "PRD-12-3")]
    public async Task The_pending_read_answers_204_when_no_offer_is_actionable()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var anchorSpaceId = Guid.NewGuid();
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var anchor = await OfferableSpaceAsync(client, database, wire, mail, anchorSpaceId, DisplayName(), ("recipient", DisplayName(), "member"));

            _ = await OfferedAsync(anchor.Owner, anchorSpaceId, anchor.Members["recipient"].MembershipId);
            answers["anchor"] = await PendingOfferOverWireAsync(anchor.Owner, anchorSpaceId);

            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));

            answers["none"] = await PendingOfferOverWireAsync(space.Owner, spaceId);

            var now = await wire.DatabaseNowAsync();

            _ = await wire.AddOwnershipOfferAsync(spaceId, space.Members["recipient"].MembershipId, space.OwnerMembershipId, "Captured " + Suffix(), "Pending", now.AddMinutes(-1));
            answers["expired"] = await PendingOfferOverWireAsync(space.Owner, spaceId);
        });

        Assert.True(answers["anchor"].StatusCode == HttpStatusCode.OK, $"The anchor: {Described(answers["anchor"])}");

        foreach (var name in new[] { "none", "expired" })
        {
            Assert.True(answers[name].StatusCode == HttpStatusCode.NoContent, $"The read with {name}: {Described(answers[name])}");
            Assert.Equal(string.Empty, answers[name].Body);
        }
    }

    /// <summary>
    /// The Ownership routes are the Owner's alone: an Admin, joined through the product, is refused both the read and the
    /// revoke of the pending offer (<c>403</c> naming <c>offer-ownership-transfer</c>, each recorded <c>InsufficientRole</c>),
    /// and the offer stays <c>Pending</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "UX-DR14")]
    [Trait("Requirement", "AD-1")]
    public async Task An_Admin_is_refused_the_read_and_the_revoke_of_the_pending_offer()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var adminId = Guid.Empty;
        var refusalsBefore = 0;
        WireResponse? read = null;
        WireResponse? revoke = null;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("admin", DisplayName(), "admin"));
            var admin = space.Members["admin"];
            var offer = await OfferedAsync(space.Owner, spaceId, space.Members["recipient"].MembershipId);

            adminId = admin.AccountId;
            refusalsBefore = (await RefusalsOfAsync(database, adminId)).Count;

            await admin.Session.FetchAntiforgeryTokenAsync();
            read = await PendingOfferOverWireAsync(admin.Session, spaceId);
            revoke = await RevokeOfferOverWireAsync(admin.Session, spaceId, offer.Id);
        });

        foreach (var (name, answer) in new[] { ("read", read), ("revoke", revoke) })
        {
            Assert.True(answer?.StatusCode == HttpStatusCode.Forbidden, $"The Admin's {name}: {Described(answer)}");
            Assert.Equal(ProblemTypes.InsufficientRole, ProblemType(answer));
            Assert.Equal(OwnershipOfferWire.CapabilityCode, CapabilityOf(answer));
        }

        var refusals = (await RefusalsOfAsync(database, adminId)).Skip(refusalsBefore).ToList();

        Assert.Equal(2, refusals.Count);
        Assert.All(refusals, refusal => Assert.Equal("InsufficientRole", refusal.Kind));
        Assert.All(refusals, refusal => Assert.Equal(nameof(Capability.OfferOwnershipTransfer), refusal.Capability));
        Assert.Equal("Pending", Assert.Single(await wire.OwnershipOffersInAsync(spaceId)).State);
    }
}
