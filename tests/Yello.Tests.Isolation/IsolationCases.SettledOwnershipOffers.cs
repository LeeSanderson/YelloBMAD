using System.Net;
using Xunit;
using Yello.Contracts.Memberships;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// Offers that are no longer open, asked about by an Account that holds no Membership in their Space, and the Owner's read
/// of a declined offer, <c>GET /api/v1/spaces/{spaceId}/ownership-offers/declined</c>. Each row states both surfaces.
/// </summary>
/// <remarks>
/// <para>
/// <b>A settled offer never confirms that its Space or the offer exists to a non-member.</b> Three kinds of non-member each
/// get the boundary <c>404</c>, byte-identical to a random Space id and recorded <c>CrossSpace</c>, with the offer and every
/// Role unchanged: a stranger, on an offer that was declined, one that was revoked and one that was accepted; the former
/// recipient, removed by the Owner, on its own lapsed offer; and the former Owner, who left after the acceptance, on the
/// accepted offer. These rows are answered by the pipeline, before any state is read, so they are expected to pass today;
/// they are pending only until they have been run, and the dev phase makes them live first (GATE-003).
/// </para>
/// <para>
/// <b>The "former" rows use a Token bound to the Account's own Space</b>, never one issued in the Space it left: removing a
/// Membership deletes its Tokens in the same statement (the <c>ApiToken</c> key cascades from <c>Membership</c>), so a
/// Token issued there would be an unknown credential, not a non-member's.
/// </para>
/// <para>
/// <b>The declined read's rows are pending</b> until the route is mapped; the develop phase makes them live before review
/// (GATE-003) and swaps <see cref="OwnershipOfferWire.DeclinedRoute"/> and <see cref="OwnershipOfferWire.DeclinedOf"/> for
/// the contract's members.
/// </para>
/// <para>
/// <b>Seeding through the product.</b> Fresh hosts, recipients joined through an accepted Invitation, and offers, declines,
/// revokes, acceptances, removals and leaves sent through their routes. <c>world.A</c> and <c>world.B</c> are never given new
/// Members or offers.
/// </para>
/// </remarks>
internal static partial class IsolationCases
{
    private const string SettledRowsUnverified = "Expected to pass today (the pipeline answers a non-member before any state is read), but not yet run: the ATDD run that would have proved it was stopped for low memory. Make it live first in the dev phase.";

    private const string DeclinedReadPending = "The Owner's declined read is not mapped until the dev phase of the story that tells the Owner of a decline.";

    private static IEnumerable<IsolationCase> SettledOwnershipOfferCases() =>
    [
        // A stranger - world.A - on a fresh host's three settled offers: declined, revoked, accepted.
        new IsolationCase(
            "ISO-ACCEPT-SETTLED-OWNERSHIP-OFFER-FOREIGN-SPACE", "I-1, I-2", IsolationVector.Write, HttpMethod.Post, OwnershipOfferRoutes.Acceptance,
            async world =>
            {
                var settled = await HostWithSettledOffersAsync(world, "Sa", "Sb");

                return SettledRequest(world.A, HttpMethod.Post, settled, OwnershipOfferRoutes.AcceptanceOf);
            },
            Both(new BoundaryNotFound { Then = TheOthersSettledOffersAreUnchangedAsync }),
            PendingUntil: "5.3",
            PendingReason: SettledRowsUnverified),

        new IsolationCase(
            "ISO-DECLINE-SETTLED-OWNERSHIP-OFFER-FOREIGN-SPACE", "I-1, I-2", IsolationVector.Write, HttpMethod.Post, OwnershipOfferRoutes.Decline,
            async world =>
            {
                var settled = await HostWithSettledOffersAsync(world, "Sc", "Sd");

                return SettledRequest(world.A, HttpMethod.Post, settled, OwnershipOfferRoutes.DeclineOf);
            },
            Both(new BoundaryNotFound { Then = TheOthersSettledOffersAreUnchangedAsync }),
            PendingUntil: "5.3",
            PendingReason: SettledRowsUnverified),

        new IsolationCase(
            "ISO-REVOKE-SETTLED-OWNERSHIP-OFFER-FOREIGN-SPACE", "I-1, I-2", IsolationVector.Write, HttpMethod.Delete, OwnershipOfferRoutes.Offer,
            async world =>
            {
                var settled = await HostWithSettledOffersAsync(world, "Se", "Sf");

                return SettledRequest(world.A, HttpMethod.Delete, settled, OwnershipOfferRoutes.OfferOf);
            },
            Both(new BoundaryNotFound { Then = TheOthersSettledOffersAreUnchangedAsync }),
            PendingUntil: "5.3",
            PendingReason: SettledRowsUnverified),

        // The former recipient, removed by the host: its offer is Lapsed, and its own old offer id is the boundary 404 to it.
        new IsolationCase(
            "ISO-ACCEPT-OWNERSHIP-OFFER-AFTER-REMOVAL", "I-1, I-2", IsolationVector.Write, HttpMethod.Post, OwnershipOfferRoutes.Acceptance,
            async world =>
            {
                var (host, former, offerId) = await RecipientRemovedAfterAnOfferAsync(world, "Ra", "Rb");

                return new CaseRequest(
                    former,
                    HttpMethod.Post,
                    [new(OwnershipOfferRoutes.AcceptanceOf(host.SpaceId, offerId), OwnershipOfferRoutes.AcceptanceOf(Guid.NewGuid(), offerId))],
                    host.SpaceId,
                    Other: host);
            },
            Both(new BoundaryNotFound { Then = TheOthersOfferIsLapsedAndItStillOwnsAsync }),
            PendingUntil: "5.3",
            PendingReason: SettledRowsUnverified),

        new IsolationCase(
            "ISO-DECLINE-OWNERSHIP-OFFER-AFTER-REMOVAL", "I-1, I-2", IsolationVector.Write, HttpMethod.Post, OwnershipOfferRoutes.Decline,
            async world =>
            {
                var (host, former, offerId) = await RecipientRemovedAfterAnOfferAsync(world, "Rc", "Rd");

                return new CaseRequest(
                    former,
                    HttpMethod.Post,
                    [new(OwnershipOfferRoutes.DeclineOf(host.SpaceId, offerId), OwnershipOfferRoutes.DeclineOf(Guid.NewGuid(), offerId))],
                    host.SpaceId,
                    Other: host);
            },
            Both(new BoundaryNotFound { Then = TheOthersOfferIsLapsedAndItStillOwnsAsync }),
            PendingUntil: "5.3",
            PendingReason: SettledRowsUnverified),

        // The former Owner: it offered its second Space to a Member, the Member accepted, and it left. Its Token is bound to its
        // first Space, which it still owns.
        new IsolationCase(
            "ISO-REVOKE-OWNERSHIP-OFFER-AFTER-LEAVING", "I-1, I-2", IsolationVector.Write, HttpMethod.Delete, OwnershipOfferRoutes.Offer,
            async world =>
            {
                var (former, spaceId, newOwner, offerId) = await OwnerLeftAfterTheAcceptanceAsync(world, "Lf");

                return new CaseRequest(
                    former,
                    HttpMethod.Delete,
                    [new(OwnershipOfferRoutes.OfferOf(spaceId, offerId), OwnershipOfferRoutes.OfferOf(Guid.NewGuid(), offerId))],
                    spaceId,
                    Other: newOwner);
            },
            Both(new BoundaryNotFound { Then = TheOtherHoldsOwnerAsync }),
            PendingUntil: "5.3",
            PendingReason: SettledRowsUnverified),

        new IsolationCase(
            "ISO-READ-DECLINED-OWNERSHIP-OFFER-FOREIGN-SPACE", "I-1", IsolationVector.DirectIdentifier, HttpMethod.Get, OwnershipOfferWire.DeclinedRoute,
            world => ForeignAsync(world, OwnershipOfferWire.DeclinedOf),
            Both(new BoundaryNotFound()),
            PendingUntil: "5.3",
            PendingReason: DeclinedReadPending),

        // The declined read is the Owner's alone: an Admin of the host's Space, after the host's offer was declined there.
        new IsolationCase(
            "ISO-RECORD-KIND-READ-DECLINED-OWNERSHIP-OFFER-ADMIN", "V-1, C-6, NFR-7", IsolationVector.RecordKind, HttpMethod.Get, OwnershipOfferWire.DeclinedRoute,
            async world =>
            {
                var (host, _, joined, offerId) = await HostWithAnOfferToAFreshAccountAsync(world, "Ya", "Yb");

                await SentThroughTheProductAsync(joined, HttpMethod.Post, OwnershipOfferRoutes.DeclineOf(host.SpaceId, offerId));

                var admin = await JoinedThroughTheProductAsync(world, host, "Yc", "admin");
                var path = OwnershipOfferWire.DeclinedOf(host.SpaceId);

                return new CaseRequest(admin, HttpMethod.Get, [new(path, path)], host.SpaceId, Other: host);
            },
            Both(new RoleRefused(OwnershipOfferWire.CapabilityCode)),
            PendingUntil: "5.3",
            PendingReason: DeclinedReadPending),

        TokenOwnOtherSpace("READ-DECLINED-OWNERSHIP-OFFER", HttpMethod.Get, OwnershipOfferWire.DeclinedRoute, HttpStatusCode.NoContent, second => OwnershipOfferWire.DeclinedOf(second.SpaceId))
            with
        {
            TestDesignId = "I-12",
            PendingUntil = "5.3",
            PendingReason = DeclinedReadPending,
        },
    ];

    /// <summary>
    /// A fresh host whose Space holds a fresh Member, joined through the product, and three settled offers to it, each settled
    /// through its own route and in this order: one the Member declined, one the host revoked, one the Member accepted. The
    /// Member is the Space's Owner afterwards.
    /// </summary>
    /// <returns>The host; the Member seen from the host's Space; the three offers' ids.</returns>
    private static async Task<(ProductActor Host, ProductActor Recipient, Guid Declined, Guid Revoked, Guid Accepted)> HostWithSettledOffersAsync(IsolationWorld world, string hostLabel, string recipientLabel)
    {
        var host = await world.NewActorAsync(hostLabel);
        var recipient = await JoinedThroughTheProductAsync(world, host, recipientLabel, "member");
        var target = await world.Database.MembershipOfAsync(host.SpaceId, recipient.AccountId);

        HoldsIdentity(host);

        var declined = await OfferedThroughTheProductAsync(host, host.SpaceId, target.Id);

        await SentThroughTheProductAsync(recipient, HttpMethod.Post, OwnershipOfferRoutes.DeclineOf(host.SpaceId, declined));

        var revoked = await OfferedThroughTheProductAsync(host, host.SpaceId, target.Id);

        await SentThroughTheProductAsync(host, HttpMethod.Delete, OwnershipOfferRoutes.OfferOf(host.SpaceId, revoked));

        var accepted = await OfferedThroughTheProductAsync(host, host.SpaceId, target.Id);

        await SentThroughTheProductAsync(recipient, HttpMethod.Post, OwnershipOfferRoutes.AcceptanceOf(host.SpaceId, accepted));

        return (host, recipient, declined, revoked, accepted);
    }

    /// <summary>
    /// A request by <paramref name="actor"/> naming each of the three settled offers in the host's Space, each anchored on the
    /// same offer id under a Space that does not exist.
    /// </summary>
    private static CaseRequest SettledRequest(
        ProductActor actor,
        HttpMethod method,
        (ProductActor Host, ProductActor Recipient, Guid Declined, Guid Revoked, Guid Accepted) settled,
        Func<Guid, Guid, string> route) =>
        new(
            actor,
            method,
            [.. new[] { settled.Declined, settled.Revoked, settled.Accepted }.Select(offerId => new CasePath(route(settled.Host.SpaceId, offerId), route(Guid.NewGuid(), offerId)))],
            settled.Host.SpaceId,
            Other: settled.Recipient);

    /// <summary>
    /// A fresh host that offered ownership to a fresh Account joined through the product, then removed that Membership, so the
    /// offer was written <c>Lapsed</c>.
    /// </summary>
    /// <returns>The host; the former recipient seen from its own Personal Space (its Token bound there); the offer's id.</returns>
    private static async Task<(ProductActor Host, ProductActor Former, Guid OfferId)> RecipientRemovedAfterAnOfferAsync(IsolationWorld world, string hostLabel, string recipientLabel)
    {
        var (host, recipient, _, offerId) = await HostWithAnOfferToAFreshAccountAsync(world, hostLabel, recipientLabel);
        var membership = await world.Database.MembershipOfAsync(host.SpaceId, recipient.AccountId);

        await SentThroughTheProductAsync(host, HttpMethod.Delete, MembershipRoutes.MembershipOf(host.SpaceId, membership.Id));

        return (host, recipient, offerId);
    }

    /// <summary>
    /// A fresh Account owning two Spaces offers ownership of the second to a fresh Member joined through the product; the
    /// Member accepts; the former Owner, now an Admin there, leaves the second Space.
    /// </summary>
    /// <returns>The former Owner seen from its first Space (its Token bound there); the second Space; the new Owner seen from
    /// it; the accepted offer's id.</returns>
    private static async Task<(ProductActor Former, Guid SpaceId, ProductActor NewOwner, Guid OfferId)> OwnerLeftAfterTheAcceptanceAsync(IsolationWorld world, string label)
    {
        var (first, second) = await world.OwnerOfTwoAsync(label);
        var newOwner = await JoinedThroughTheProductAsync(world, second.Actor, label + "m", "member");
        var target = await world.Database.MembershipOfAsync(second.SpaceId, newOwner.AccountId);
        var offerId = await OfferedThroughTheProductAsync(second.Actor, second.SpaceId, target.Id);

        await SentThroughTheProductAsync(newOwner, HttpMethod.Post, OwnershipOfferRoutes.AcceptanceOf(second.SpaceId, offerId));
        await SentThroughTheProductAsync(second.Actor, HttpMethod.Delete, MembershipRoutes.OwnOf(second.SpaceId));

        HoldsIdentity(first.Actor);

        return (first.Actor, second.SpaceId, newOwner, offerId);
    }

    /// <summary>
    /// A bodiless request by <paramref name="actor"/>'s Session, asserted <c>204</c>.
    /// </summary>
    private static async Task SentThroughTheProductAsync(ProductActor actor, HttpMethod method, string path)
    {
        var answer = await actor.SendAsync(IsolationSurface.Session, method, path, body: null, headers: null);

        Assert.True(answer.StatusCode == HttpStatusCode.NoContent, $"{method} {path} through the product: {answer.StatusCode} {answer.Body}");
    }

    /// <summary>
    /// The request's <c>Other</c> - the Member seen from the host's Space - still holds <c>Owner</c> there, and the Space's
    /// three offers are still declined, revoked and accepted, in that order.
    /// </summary>
    private static async Task TheOthersSettledOffersAreUnchangedAsync(IsolationWorld world, CaseRequest request)
    {
        var other = request.Other ?? throw new InvalidOperationException("The case names no Other whose offers it checks.");

        Assert.Equal(["Declined", "Revoked", "Accepted"], (await world.Database.OwnershipOffersInAsync(other.SpaceId)).Select(row => row.State));
        Assert.Equal("Owner", (await world.Database.MembershipOfAsync(other.SpaceId, other.AccountId)).Role);
    }

    /// <summary>
    /// The request's <c>Other</c> - the offer's host - holds exactly one offer, <c>Lapsed</c>, and is still its Space's Owner.
    /// </summary>
    private static async Task TheOthersOfferIsLapsedAndItStillOwnsAsync(IsolationWorld world, CaseRequest request)
    {
        var other = request.Other ?? throw new InvalidOperationException("The case names no Other whose offer it checks.");

        Assert.Equal("Lapsed", Assert.Single(await world.Database.OwnershipOffersInAsync(other.SpaceId)).State);
        Assert.Equal("Owner", (await world.Database.MembershipOfAsync(other.SpaceId, other.AccountId)).Role);
    }
}
