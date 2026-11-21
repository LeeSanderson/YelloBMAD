using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Spaces;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// How the offering Owner is told its offer was declined, over the wire:
/// <c>GET /api/v1/spaces/{spaceId}/ownership-offers/declined</c> under <c>Capability.OfferOwnershipTransfer</c>. It answers
/// <c>200</c> with <c>{ id, recipientName }</c> when the Space's most recent offer (latest <c>CreatedAt</c>, ties broken by
/// <c>Id</c>) is <c>Declined</c>, and <c>204</c> with no body otherwise: no offer, a pending one, and a latest offer that was
/// accepted, revoked, lapsed or expired. The name is the recipient's display name now, or <c>null</c> once its Membership is
/// gone. Never an address and never the recipient's id.
/// </summary>
/// <remarks>
/// <para>
/// <b>A new Owner-only read</b> rather than a widened pending read, an email or an accent element: declining sends no email,
/// and the Offer indicator is the only accent element in chrome.
/// </para>
/// <para>
/// <b>Seeding</b> is as in <c>.OfferOwnership.cs</c>: every Membership joins through the product and every offer, decline and
/// revoke goes through the product's routes. The expired offer is a product-made row whose <c>ExpiresAt</c> is moved into the
/// past on the database clock (<see cref="WireDatabase.SetOwnershipOfferExpiresAtAsync"/>).
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    private static readonly string[] DeclinedReadRefusedRoles = ["admin", "member", "viewer"];

    /// <summary>
    /// After the named recipient declines, the Owner's declined read is <c>200</c> with exactly <c>id</c> and
    /// <c>recipientName</c>: the declined offer's id and the recipient's display name, and no <c>@</c> anywhere. The
    /// recipient's own read of the same Space answers <c>204</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-23")]
    [Trait("Requirement", "AD-26")]
    public async Task After_a_decline_the_Owner_reads_it_by_the_recipients_name_and_the_recipients_own_read_is_204()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var recipientName = DisplayName();
        var offerId = Guid.Empty;
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", recipientName, "member"));
            var recipient = space.Members["recipient"];

            offerId = (await OfferedAsync(space.Owner, spaceId, recipient.MembershipId)).Id;

            await recipient.Session.FetchAntiforgeryTokenAsync();
            answers["decline"] = await DeclineOverWireAsync(recipient.Session, spaceId, offerId);
            answers["declined"] = await DeclinedOfferOverWireAsync(space.Owner, spaceId);
            answers["mine"] = await MineOverWireAsync(recipient.Session, spaceId);
        });

        Assert.True(answers["decline"].StatusCode == HttpStatusCode.NoContent, $"The decline: {Described(answers["decline"])}");
        Assert.True(answers["declined"].StatusCode == HttpStatusCode.OK, $"The Owner's declined read: {Described(answers["declined"])}");
        Assert.Equal(OwnershipOfferWire.DeclinedResponseMembers, MembershipWire.ObjectMemberNames(answers["declined"].Body));
        Assert.DoesNotContain("@", answers["declined"].Body, StringComparison.Ordinal);
        Assert.Equal(new DeclinedOwnershipOfferAnswer(offerId, recipientName), OwnershipOfferWire.ParseDeclined(answers["declined"].Body));
        Assert.True(answers["mine"].StatusCode == HttpStatusCode.NoContent, $"The recipient's own read: {Described(answers["mine"])}");
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// The declined read is <c>204</c> with no body whenever the latest offer is not a decline: before any offer, while an
    /// offer is pending, while that offer is stored <c>Pending</c> past its expiry (an expiry is not a decline), and once a new
    /// offer follows a decline. Anchor: between the last two, the same read answers <c>200</c> for the decline.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-27")]
    [Trait("Assumption", "PRD-12-3")]
    public async Task The_declined_read_is_204_with_no_offer_a_pending_offer_an_expired_offer_and_after_a_new_offer()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("other", DisplayName(), "member"));
            var recipient = space.Members["recipient"];

            answers["none"] = await DeclinedOfferOverWireAsync(space.Owner, spaceId);

            var first = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);

            answers["pending"] = await DeclinedOfferOverWireAsync(space.Owner, spaceId);

            await wire.SetOwnershipOfferExpiresAtAsync(spaceId, first.Id, (await wire.DatabaseNowAsync()).AddMinutes(-1));
            answers["expired"] = await DeclinedOfferOverWireAsync(space.Owner, spaceId);

            var second = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);

            await recipient.Session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await DeclineOverWireAsync(recipient.Session, spaceId, second.Id)).StatusCode);
            answers["anchor"] = await DeclinedOfferOverWireAsync(space.Owner, spaceId);

            _ = await OfferedAsync(space.Owner, spaceId, space.Members["other"].MembershipId);
            answers["new offer"] = await DeclinedOfferOverWireAsync(space.Owner, spaceId);
        });

        Assert.True(answers["anchor"].StatusCode == HttpStatusCode.OK, $"The anchor: {Described(answers["anchor"])}");

        foreach (var name in new[] { "none", "pending", "expired", "new offer" })
        {
            Assert.True(answers[name].StatusCode == HttpStatusCode.NoContent, $"The declined read with {name}: {Described(answers[name])}");
            Assert.Equal(string.Empty, answers[name].Body);
        }
    }

    /// <summary>
    /// The latest offer decides: an offer declined, then a later offer to another Member that the Owner revoked, reads
    /// <c>204</c>. A read that filtered on <c>Declined</c> before taking the latest row would surface the old decline and
    /// answer <c>200</c>. Anchor: right after the decline the same read answered <c>200</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-26")]
    public async Task A_decline_followed_by_a_later_offer_that_was_revoked_reads_204()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("other", DisplayName(), "member"));
            var recipient = space.Members["recipient"];
            var first = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);

            await recipient.Session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await DeclineOverWireAsync(recipient.Session, spaceId, first.Id)).StatusCode);
            answers["anchor"] = await DeclinedOfferOverWireAsync(space.Owner, spaceId);

            var second = await OfferedAsync(space.Owner, spaceId, space.Members["other"].MembershipId);

            Assert.Equal(HttpStatusCode.NoContent, (await RevokeOfferOverWireAsync(space.Owner, spaceId, second.Id)).StatusCode);
            answers["after the revoke"] = await DeclinedOfferOverWireAsync(space.Owner, spaceId);
        });

        Assert.True(answers["anchor"].StatusCode == HttpStatusCode.OK, $"The anchor: {Described(answers["anchor"])}");
        Assert.True(answers["after the revoke"].StatusCode == HttpStatusCode.NoContent, $"The declined read after a later revoked offer: {Described(answers["after the revoke"])}");
        Assert.Equal(["Declined", "Revoked"], (await wire.OwnershipOffersInAsync(spaceId)).Select(row => row.State));
    }

    /// <summary>
    /// A retained offer outlives the Membership it names: after the declining recipient leaves the Space, the Owner's declined
    /// read is still <c>200</c> for that offer, with <c>recipientName</c> <c>null</c> and no <c>@</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-23")]
    public async Task After_the_declining_recipient_leaves_the_declined_read_names_no_one()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var offerId = Guid.Empty;
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));
            var recipient = space.Members["recipient"];

            offerId = (await OfferedAsync(space.Owner, spaceId, recipient.MembershipId)).Id;

            await recipient.Session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await DeclineOverWireAsync(recipient.Session, spaceId, offerId)).StatusCode);
            answers["leave"] = await LeaveOverWireAsync(recipient.Session, spaceId);
            answers["declined"] = await DeclinedOfferOverWireAsync(space.Owner, spaceId);
        });

        Assert.True(answers["leave"].StatusCode == HttpStatusCode.NoContent, $"The recipient's leave: {Described(answers["leave"])}");
        Assert.True(answers["declined"].StatusCode == HttpStatusCode.OK, $"The declined read after the leave: {Described(answers["declined"])}");
        Assert.DoesNotContain("@", answers["declined"].Body, StringComparison.Ordinal);
        Assert.Equal(new DeclinedOwnershipOfferAnswer(offerId, null), OwnershipOfferWire.ParseDeclined(answers["declined"].Body));
        Assert.Equal("Declined", Assert.Single(await wire.OwnershipOffersInAsync(spaceId)).State);
    }

    /// <summary>
    /// The declined read is the Owner's alone: an Admin, a Member and a Viewer, each joined through the product, are refused it
    /// with <c>403 insufficient-role</c> naming <c>offer-ownership-transfer</c>, each recorded <c>InsufficientRole</c> against
    /// that capability.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-1")]
    [Trait("Requirement", "UX-DR27")]
    public async Task An_Admin_a_Member_and_a_Viewer_are_refused_the_declined_read_with_the_recorded_403()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var refusalsBefore = new Dictionary<string, int>(StringComparer.Ordinal);
        var actors = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("admin", DisplayName(), "admin"), ("member", DisplayName(), "member"), ("viewer", DisplayName(), "viewer"));
            var offer = await OfferedAsync(space.Owner, spaceId, space.Members["member"].MembershipId);

            await space.Members["member"].Session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await DeclineOverWireAsync(space.Members["member"].Session, spaceId, offer.Id)).StatusCode);

            foreach (var role in DeclinedReadRefusedRoles)
            {
                actors[role] = space.Members[role].AccountId;
                refusalsBefore[role] = (await RefusalsOfAsync(database, actors[role])).Count;
                answers[role] = await DeclinedOfferOverWireAsync(space.Members[role].Session, spaceId);
            }
        });

        foreach (var (role, answer) in answers)
        {
            Assert.True(answer.StatusCode == HttpStatusCode.Forbidden, $"The {role}'s declined read: {Described(answer)}");
            Assert.Equal(ProblemTypes.InsufficientRole, ProblemType(answer));
            Assert.Equal(OwnershipOfferWire.CapabilityCode, CapabilityOf(answer));

            var refusal = Assert.Single((await RefusalsOfAsync(database, actors[role])).Skip(refusalsBefore[role]));

            Assert.Equal("InsufficientRole", refusal.Kind);
            Assert.Equal(nameof(Capability.OfferOwnershipTransfer), refusal.Capability);
        }
    }

    /// <summary>
    /// The Owner's declined read: the Session's cookie, a <c>GET</c>.
    /// </summary>
    private static Task<WireResponse> DeclinedOfferOverWireAsync(WireSession session, Guid spaceId) =>
        session.SendWithHeadersAsync(HttpMethod.Get, OwnershipOfferRoutes.DeclinedOf(spaceId), content: null, NoKey, withAntiforgeryToken: false);
}
