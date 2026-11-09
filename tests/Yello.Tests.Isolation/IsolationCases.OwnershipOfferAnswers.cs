using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// Answering an Ownership Offer: the rows for the recipient's read, <c>GET /api/v1/spaces/{spaceId}/ownership-offers/mine</c>,
/// the acceptance, <c>POST .../ownership-offers/{offerId}/acceptance</c>, and the decline,
/// <c>POST .../ownership-offers/{offerId}/decline</c>, each stating both surfaces.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every row is live</b>: the three routes fail the route-coverage gate unless these rows are active.
/// </para>
/// <para>
/// <b>Identity-decided, so no record-kind rows.</b> The three routes declare <c>ReadProjectsAndTasks</c>, which every Role
/// holds, and decide by row identity inside the store: nobody is refused by Role, so there is no <c>ISO-RECORD-KIND-...</c>
/// row to write. That is deliberate. A non-recipient's attempt is the unrecorded in-Space not-found, which the
/// <c>...-NAMING-ANOTHER-MEMBERSHIP</c> rows prove byte-identical to a random id.
/// </para>
/// <para>
/// <b>Seeding through the product.</b> A fresh host's Space, a fresh recipient joined through an accepted Invitation, and an
/// offer made by the host's own Session through the offer route. <c>world.A</c> and <c>world.B</c> are never given new Members
/// or offers.
/// </para>
/// </remarks>
internal static partial class IsolationCases
{

    private static IEnumerable<IsolationCase> OwnershipOfferAnswerCases() =>
    [
        // A fresh host's pending offer, accepted by world.A through the host's Space: the offer must stay Pending and the host
        // must stay the Owner.
        new IsolationCase(
            "ISO-ACCEPT-OWNERSHIP-OFFER-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Post, OwnershipOfferRoutes.Acceptance,
            async world =>
            {
                var (host, offerId) = await HostWithAPendingOfferAsync(world, "Ah", "Ar");

                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(OwnershipOfferRoutes.AcceptanceOf(host.SpaceId, offerId), OwnershipOfferRoutes.AcceptanceOf(Guid.NewGuid(), offerId))],
                    host.SpaceId,
                    Other: host);
            },
            Both(new BoundaryNotFound { Then = TheOthersOfferIsStillPendingAndItStillOwnsAsync })),

        new IsolationCase(
            "ISO-DECLINE-OWNERSHIP-OFFER-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Post, OwnershipOfferRoutes.Decline,
            async world =>
            {
                var (host, offerId) = await HostWithAPendingOfferAsync(world, "Dh", "Dr");

                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(OwnershipOfferRoutes.DeclineOf(host.SpaceId, offerId), OwnershipOfferRoutes.DeclineOf(Guid.NewGuid(), offerId))],
                    host.SpaceId,
                    Other: host);
            },
            Both(new BoundaryNotFound { Then = TheOthersOfferIsStillPendingAndItStillOwnsAsync })),

        new IsolationCase(
            "ISO-READ-MY-OWNERSHIP-OFFER-FOREIGN-SPACE", "I-1", IsolationVector.DirectIdentifier, HttpMethod.Get, OwnershipOfferRoutes.Mine,
            world => ForeignAsync(world, OwnershipOfferRoutes.MineOf),
            Both(new BoundaryNotFound())),

        // The recipient's own Personal Space with the offer id of the host's Space, an offer that names the recipient there: it
        // answers as an offer that never existed in its own Space - the unrecorded in-Space not-found, byte-identical to a
        // random id - and the host's offer stays Pending.
        new IsolationCase(
            "ISO-ACCEPT-FOREIGN-OFFER-ID-IN-OWN-SPACE", "I-4, I-13", IsolationVector.DeepLink, HttpMethod.Post, OwnershipOfferRoutes.Acceptance,
            async world =>
            {
                var (host, recipient, _, offerId) = await HostWithAnOfferToAFreshAccountAsync(world, "Gh", "Gr");

                return new CaseRequest(
                    recipient,
                    HttpMethod.Post,
                    [new(OwnershipOfferRoutes.AcceptanceOf(recipient.SpaceId, offerId), OwnershipOfferRoutes.AcceptanceOf(recipient.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null,
                    Other: host);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = TheOthersOfferIsStillPendingAndItStillOwnsAsync })),

        new IsolationCase(
            "ISO-DECLINE-FOREIGN-OFFER-ID-IN-OWN-SPACE", "I-4, I-13", IsolationVector.DeepLink, HttpMethod.Post, OwnershipOfferRoutes.Decline,
            async world =>
            {
                var (host, recipient, _, offerId) = await HostWithAnOfferToAFreshAccountAsync(world, "Hh", "Hr");

                return new CaseRequest(
                    recipient,
                    HttpMethod.Post,
                    [new(OwnershipOfferRoutes.DeclineOf(recipient.SpaceId, offerId), OwnershipOfferRoutes.DeclineOf(recipient.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null,
                    Other: host);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = TheOthersOfferIsStillPendingAndItStillOwnsAsync })),

        // Another Member of the host's Space, on the offer's real id: identical to a random id, unrecorded, nothing written. A
        // non-recipient learns nothing about whom the offer names, and is never given a Role-shaped 403.
        new IsolationCase(
            "ISO-ACCEPT-OFFER-NAMING-ANOTHER-MEMBERSHIP", "I-13", IsolationVector.DeepLink, HttpMethod.Post, OwnershipOfferRoutes.Acceptance,
            async world =>
            {
                var (host, offerId) = await HostWithAPendingOfferAsync(world, "Nh", "Nr");
                var other = await JoinedThroughTheProductAsync(world, host, "Nm", "member");

                return new CaseRequest(
                    other,
                    HttpMethod.Post,
                    [new(OwnershipOfferRoutes.AcceptanceOf(host.SpaceId, offerId), OwnershipOfferRoutes.AcceptanceOf(host.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null,
                    Other: host);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = TheOthersOfferIsStillPendingAndItStillOwnsAsync })),

        new IsolationCase(
            "ISO-DECLINE-OFFER-NAMING-ANOTHER-MEMBERSHIP", "I-13", IsolationVector.DeepLink, HttpMethod.Post, OwnershipOfferRoutes.Decline,
            async world =>
            {
                var (host, offerId) = await HostWithAPendingOfferAsync(world, "Oh", "Or");
                var other = await JoinedThroughTheProductAsync(world, host, "Om", "member");

                return new CaseRequest(
                    other,
                    HttpMethod.Post,
                    [new(OwnershipOfferRoutes.DeclineOf(host.SpaceId, offerId), OwnershipOfferRoutes.DeclineOf(host.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null,
                    Other: host);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = TheOthersOfferIsStillPendingAndItStillOwnsAsync })),

        // The Token-bound-to-another-Space gate, one row per route a Token reaches. The recipient is a Member of the host's
        // Space and its own Token is bound to its Personal Space. By Session it accepts; by that Token the same request is a
        // non-member's, so the offer stays Pending.
        new IsolationCase(
            "ISO-TOKEN-OWN-OTHER-SPACE-ACCEPT-OWNERSHIP-OFFER", "I-12", IsolationVector.TokenOwnOtherSpace, HttpMethod.Post, OwnershipOfferRoutes.Acceptance,
            async world =>
            {
                var (host, recipient, joined, offerId) = await HostWithAnOfferToAFreshAccountAsync(world, "Ta", "Tb");
                var path = OwnershipOfferRoutes.AcceptanceOf(host.SpaceId, offerId);

                return new CaseRequest(recipient, HttpMethod.Post, [new(path, path)], host.SpaceId, Other: joined);
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.NoContent, (_, _, _) => Task.CompletedTask, "204 - a Session carries no Space (FR-2); the Account is the offer's recipient there") { Then = TheOtherHoldsOwnerAsync },
                [IsolationSurface.ApiToken] = new SameAsNonMember { Then = TheOthersOfferIsStillPendingAsync },
            }),

        new IsolationCase(
            "ISO-TOKEN-OWN-OTHER-SPACE-DECLINE-OWNERSHIP-OFFER", "I-12", IsolationVector.TokenOwnOtherSpace, HttpMethod.Post, OwnershipOfferRoutes.Decline,
            async world =>
            {
                var (host, recipient, joined, offerId) = await HostWithAnOfferToAFreshAccountAsync(world, "Tc", "Td");
                var path = OwnershipOfferRoutes.DeclineOf(host.SpaceId, offerId);

                return new CaseRequest(recipient, HttpMethod.Post, [new(path, path)], host.SpaceId, Other: joined);
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.NoContent, (_, _, _) => Task.CompletedTask, "204 - a Session carries no Space (FR-2); the Account is the offer's recipient there") { Then = TheOthersOfferIsDeclinedAsync },
                [IsolationSurface.ApiToken] = new SameAsNonMember { Then = TheOthersOfferIsStillPendingAsync },
            }),

        TokenOwnOtherSpace("READ-MY-OWNERSHIP-OFFER", HttpMethod.Get, OwnershipOfferRoutes.Mine, HttpStatusCode.NoContent, second => OwnershipOfferRoutes.MineOf(second.SpaceId))
            with
        {
            TestDesignId = "I-12",
        },
    ];

    /// <summary>
    /// A fresh host whose Space holds a fresh Account joined through the product as a Member, to which the host has offered
    /// ownership through the offer route.
    /// </summary>
    /// <returns>The host; the recipient seen from its own Personal Space (its Token bound there); the recipient seen from the
    /// host's Space; and the pending offer's id.</returns>
    private static async Task<(ProductActor Host, ProductActor Recipient, ProductActor Joined, Guid OfferId)> HostWithAnOfferToAFreshAccountAsync(IsolationWorld world, string hostLabel, string recipientLabel)
    {
        var host = await world.NewActorAsync(hostLabel);
        var recipient = await world.NewActorAsync(recipientLabel);
        var joined = await JoinedThroughTheProductAsync(world, host, recipient, "member");
        var target = await world.Database.MembershipOfAsync(host.SpaceId, recipient.AccountId);

        HoldsIdentity(host);

        return (host, recipient, joined, await OfferedThroughTheProductAsync(host, host.SpaceId, target.Id));
    }

    /// <summary>
    /// The request's <c>Other</c> - the offer's host - still holds exactly one offer, <c>Pending</c>, and is still its Space's
    /// Owner.
    /// </summary>
    private static async Task TheOthersOfferIsStillPendingAndItStillOwnsAsync(IsolationWorld world, CaseRequest request)
    {
        var other = request.Other ?? throw new InvalidOperationException("The case names no Other whose offer it checks.");

        Assert.Equal("Pending", Assert.Single(await world.Database.OwnershipOffersInAsync(other.SpaceId)).State);
        Assert.Equal("Owner", (await world.Database.MembershipOfAsync(other.SpaceId, other.AccountId)).Role);
    }

    /// <summary>
    /// The request's <c>Other</c> - the recipient seen from the host's Space - now holds <c>Owner</c> there, and the offer is
    /// <c>Accepted</c>.
    /// </summary>
    private static async Task TheOtherHoldsOwnerAsync(IsolationWorld world, CaseRequest request)
    {
        var other = request.Other ?? throw new InvalidOperationException("The case names no Other whose Role it checks.");

        Assert.Equal("Owner", (await world.Database.MembershipOfAsync(other.SpaceId, other.AccountId)).Role);
        Assert.Equal("Accepted", Assert.Single(await world.Database.OwnershipOffersInAsync(other.SpaceId)).State);
    }

    /// <summary>
    /// The request's <c>Other</c> - the recipient seen from the host's Space - still holds <c>Member</c> there, and the offer
    /// is <c>Declined</c>.
    /// </summary>
    private static async Task TheOthersOfferIsDeclinedAsync(IsolationWorld world, CaseRequest request)
    {
        var other = request.Other ?? throw new InvalidOperationException("The case names no Other whose offer it checks.");

        Assert.Equal("Member", (await world.Database.MembershipOfAsync(other.SpaceId, other.AccountId)).Role);
        Assert.Equal("Declined", Assert.Single(await world.Database.OwnershipOffersInAsync(other.SpaceId)).State);
    }
}
