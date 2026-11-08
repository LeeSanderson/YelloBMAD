using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// Offering ownership of a Space: the rows for <c>POST /api/v1/spaces/{spaceId}/ownership-offers</c>,
/// <c>GET .../ownership-offers/pending</c> and <c>DELETE .../ownership-offers/{offerId}</c> (I-1, I-2, I-4, I-12, I-13, I-17;
/// V-1), each stating both surfaces.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pending until the offer story's dev phase</b>, which makes every row live before review (GATE-003). The routes are
/// <see cref="OwnershipOfferWire"/>'s stand-ins; the dev phase swaps them for the <c>OwnershipOfferRoutes</c> members, and
/// <see cref="OwnershipOfferWire.InvalidOfferRecipientProblemType"/> for <c>ProblemTypes.InvalidOfferRecipient</c>. The three
/// routes fail the route-coverage gate unless these rows are active.
/// </para>
/// <para>
/// <b>Seeding through the product.</b> Lesser Roles join a fresh host's Space through an accepted Invitation and hold a Token
/// issued there, so both surfaces carry the Role; offers are made by the host's own Session through the offer route.
/// <c>world.A</c> and <c>world.B</c> are never given new Members or offers: other rows rely on their Spaces as they are.
/// </para>
/// <para>
/// <b>The email's confinement (I-14) is proved in the Slices suite</b>
/// (<c>The_offer_email_names_the_Space_and_the_offerer_and_discloses_nothing_else</c>), which reads mail through a Host and a
/// <see cref="MailDrop"/> of its own; this suite's world shares one Host and one drop across rows.
/// </para>
/// <para>
/// <b>Timing.</b> No row is added to <c>IsolationTimingTests</c>: the foreign-Membership <c>400</c> is one byte-identical body
/// decided by one guarded statement whatever the id, and the in-Space not-found is the floored boundary body, which
/// <c>ISO-TIMING-IN-SPACE</c> already proves.
/// </para>
/// </remarks>
internal static partial class IsolationCases
{
    private const string OfferPending = "The Ownership Offer routes are not mapped until the offer story's dev phase.";

    private static IEnumerable<IsolationCase> OwnershipOfferCases() =>
    [
        // B's Space, B's own Owner Membership in the body: a write that leaked would put an offer in B's Space.
        new IsolationCase(
            "ISO-OFFER-OWNERSHIP-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Post, OwnershipOfferWire.OffersRoute,
            async world =>
            {
                var theirs = await world.Database.MembershipOfAsync(world.B.SpaceId, world.B.AccountId);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(OwnershipOfferWire.OffersOf(world.B.SpaceId), OwnershipOfferWire.OffersOf(Guid.NewGuid()))],
                    world.B.SpaceId,
                    Body: () => OwnershipOfferWire.Body(theirs.Id));
            },
            Both(new BoundaryNotFound { Then = NoOfferInEitherSpaceAsync }),
            PendingUntil: "5.1",
            PendingReason: OfferPending),

        new IsolationCase(
            "ISO-READ-OWNERSHIP-OFFER-FOREIGN-SPACE", "I-1", IsolationVector.DirectIdentifier, HttpMethod.Get, OwnershipOfferWire.PendingRoute,
            world => ForeignAsync(world, OwnershipOfferWire.PendingOf),
            Both(new BoundaryNotFound()),
            PendingUntil: "5.1",
            PendingReason: OfferPending),

        // A fresh host's pending offer, revoked by world.A through the host's Space: the offer must stay Pending.
        new IsolationCase(
            "ISO-REVOKE-OWNERSHIP-OFFER-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Delete, OwnershipOfferWire.OfferRoute,
            async world =>
            {
                var (host, offerId) = await HostWithAPendingOfferAsync(world, "Fh", "Fr");

                return new CaseRequest(
                    world.A,
                    HttpMethod.Delete,
                    [new(OwnershipOfferWire.OfferOf(host.SpaceId, offerId), OwnershipOfferWire.OfferOf(Guid.NewGuid(), offerId))],
                    host.SpaceId,
                    Other: host);
            },
            Both(new BoundaryNotFound { Then = TheOthersOfferIsStillPendingAsync }),
            PendingUntil: "5.1",
            PendingReason: OfferPending),

        new IsolationCase(
            "ISO-RECORD-KIND-OFFER-OWNERSHIP-ADMIN", "V-1, C-6, NFR-7", IsolationVector.RecordKind, HttpMethod.Post, OwnershipOfferWire.OffersRoute,
            world => LesserRoleOffersOwnershipAsync(world, "admin"),
            Both(new RoleRefused(OwnershipOfferWire.CapabilityCode) { Then = NoOfferInTheTargetSpaceAsync }),
            PendingUntil: "5.1",
            PendingReason: OfferPending),

        new IsolationCase(
            "ISO-RECORD-KIND-OFFER-OWNERSHIP-MEMBER", "V-1, C-6, NFR-7", IsolationVector.RecordKind, HttpMethod.Post, OwnershipOfferWire.OffersRoute,
            world => LesserRoleOffersOwnershipAsync(world, "member"),
            Both(new RoleRefused(OwnershipOfferWire.CapabilityCode) { Then = NoOfferInTheTargetSpaceAsync }),
            PendingUntil: "5.1",
            PendingReason: OfferPending),

        new IsolationCase(
            "ISO-RECORD-KIND-OFFER-OWNERSHIP-VIEWER", "V-1, C-6, NFR-7", IsolationVector.RecordKind, HttpMethod.Post, OwnershipOfferWire.OffersRoute,
            world => LesserRoleOffersOwnershipAsync(world, "viewer"),
            Both(new RoleRefused(OwnershipOfferWire.CapabilityCode) { Then = NoOfferInTheTargetSpaceAsync }),
            PendingUntil: "5.1",
            PendingReason: OfferPending),

        // The revoke's twin: an Admin of the host's Space revokes the host's pending offer. The Ownership routes are the
        // Owner's alone, so the pipeline refuses before the guarded statement runs and the offer stays Pending.
        new IsolationCase(
            "ISO-RECORD-KIND-REVOKE-OWNERSHIP-OFFER-ADMIN", "V-1, C-6, NFR-7", IsolationVector.RecordKind, HttpMethod.Delete, OwnershipOfferWire.OfferRoute,
            async world =>
            {
                var (host, offerId) = await HostWithAPendingOfferAsync(world, "Rv", "Rw");
                var admin = await JoinedThroughTheProductAsync(world, host, "Rx", "admin");
                var path = OwnershipOfferWire.OfferOf(host.SpaceId, offerId);

                return new CaseRequest(admin, HttpMethod.Delete, [new(path, path)], host.SpaceId, Other: host);
            },
            Both(new RoleRefused(OwnershipOfferWire.CapabilityCode) { Then = TheOthersOfferIsStillPendingAsync }),
            PendingUntil: "5.1",
            PendingReason: OfferPending),

        // The ISO-ASSIGN-FOREIGN-MEMBERSHIP template: B's real Membership id in the body of A's own offer answers byte for
        // byte what a random id does, nothing is recorded, and no offer exists in either Space.
        new IsolationCase(
            "ISO-OFFER-OWNERSHIP-FOREIGN-MEMBERSHIP", "I-2, I-13", IsolationVector.Write, HttpMethod.Post, OwnershipOfferWire.OffersRoute,
            async world =>
            {
                var foreign = await world.Database.MembershipOfAsync(world.B.SpaceId, world.B.AccountId);
                var path = OwnershipOfferWire.OffersOf(world.A.SpaceId);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(path, path)],
                    TargetSpaceId: null,
                    Body: () => OwnershipOfferWire.Body(foreign.Id),
                    AnchorBody: () => OwnershipOfferWire.Body(Guid.NewGuid()));
            },
            Both(new SameAsAnchor(HttpStatusCode.BadRequest, ExpectedType: OwnershipOfferWire.InvalidOfferRecipientProblemType) { Then = NoOfferInEitherSpaceAsync }),
            PendingUntil: "5.1",
            PendingReason: OfferPending),

        // A fresh host's offer id under world.A's own Space answers as an offer that never existed there: the unrecorded
        // in-Space not-found, byte-identical to the boundary, and the host's offer stays Pending.
        new IsolationCase(
            "ISO-REVOKE-FOREIGN-OFFER-ID-IN-OWN-SPACE", "I-4, I-13", IsolationVector.DeepLink, HttpMethod.Delete, OwnershipOfferWire.OfferRoute,
            async world =>
            {
                var (host, offerId) = await HostWithAPendingOfferAsync(world, "Gh", "Gr");

                return new CaseRequest(
                    world.A,
                    HttpMethod.Delete,
                    [new(OwnershipOfferWire.OfferOf(world.A.SpaceId, offerId), OwnershipOfferWire.OfferOf(world.A.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null,
                    Other: host);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = TheOthersOfferIsStillPendingAsync }),
            PendingUntil: "5.1",
            PendingReason: OfferPending),

        // The 3.4 gate, one row per route a Token reaches. The Account owns both Spaces and its Token is bound to the first. By
        // Session it offers a Member of the second ownership; by the first Space's Token the same request is a non-member's.
        new IsolationCase(
            "ISO-TOKEN-OWN-OTHER-SPACE-OFFER-OWNERSHIP", "I-12", IsolationVector.TokenOwnOtherSpace, HttpMethod.Post, OwnershipOfferWire.OffersRoute,
            async world =>
            {
                var (first, second) = await world.OwnerOfTwoAsync("Tp");
                var member = await JoinedThroughTheProductAsync(world, second.Actor, "Tn", "member");
                var target = await world.Database.MembershipOfAsync(second.SpaceId, member.AccountId);
                var path = OwnershipOfferWire.OffersOf(second.SpaceId);

                return new CaseRequest(first.Actor, HttpMethod.Post, [new(path, path)], second.SpaceId, Body: () => OwnershipOfferWire.Body(target.Id), Other: member);
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.Created, (_, _, _) => Task.CompletedTask, "201 - a Session carries no Space (FR-2); the Account owns SA2") { Then = TheOthersSpaceHoldsOnePendingOfferAsync },
                [IsolationSurface.ApiToken] = new SameAsNonMember { Then = TheOthersSpaceHoldsNoOfferAsync },
            },
            PendingUntil: "5.1",
            PendingReason: OfferPending),

        TokenOwnOtherSpace("READ-OWNERSHIP-OFFER", HttpMethod.Get, OwnershipOfferWire.PendingRoute, HttpStatusCode.NoContent, second => OwnershipOfferWire.PendingOf(second.SpaceId))
            with
        {
            TestDesignId = "I-12",
            PendingUntil = "5.1",
            PendingReason = OfferPending,
        },

        new IsolationCase(
            "ISO-TOKEN-OWN-OTHER-SPACE-REVOKE-OWNERSHIP-OFFER", "I-12", IsolationVector.TokenOwnOtherSpace, HttpMethod.Delete, OwnershipOfferWire.OfferRoute,
            async world =>
            {
                var (first, second) = await world.OwnerOfTwoAsync("Tu");
                var member = await JoinedThroughTheProductAsync(world, second.Actor, "Tt", "member");
                var target = await world.Database.MembershipOfAsync(second.SpaceId, member.AccountId);
                var offerId = await OfferedThroughTheProductAsync(second.Actor, second.SpaceId, target.Id);
                var path = OwnershipOfferWire.OfferOf(second.SpaceId, offerId);

                return new CaseRequest(first.Actor, HttpMethod.Delete, [new(path, path)], second.SpaceId, Other: member);
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.NoContent, (_, _, _) => Task.CompletedTask, "204 - a Session carries no Space (FR-2); the Account owns SA2") { Then = TheOthersSpaceHoldsNoPendingOfferAsync },
                [IsolationSurface.ApiToken] = new SameAsNonMember { Then = TheOthersSpaceHoldsOnePendingOfferAsync },
            },
            PendingUntil: "5.1",
            PendingReason: OfferPending),
    ];

    /// <summary>
    /// A fresh host whose Space holds a fresh Member, joined through the product, to whom the host has offered ownership
    /// through the offer route.
    /// </summary>
    /// <returns>The host, seen from its Space, and the pending offer's id.</returns>
    private static async Task<(ProductActor Host, Guid OfferId)> HostWithAPendingOfferAsync(IsolationWorld world, string hostLabel, string recipientLabel)
    {
        var host = await world.NewActorAsync(hostLabel);
        var recipient = await JoinedThroughTheProductAsync(world, host, recipientLabel, "member");
        var target = await world.Database.MembershipOfAsync(host.SpaceId, recipient.AccountId);

        HoldsIdentity(host);

        return (host, await OfferedThroughTheProductAsync(host, host.SpaceId, target.Id));
    }

    /// <summary>
    /// An offer made by <paramref name="owner"/>'s Session through the offer route, asserted <c>201</c>.
    /// </summary>
    /// <returns>The offer's id.</returns>
    private static async Task<Guid> OfferedThroughTheProductAsync(ProductActor owner, Guid spaceId, Guid recipientMembershipId)
    {
        var offered = await owner.SendAsync(IsolationSurface.Session, HttpMethod.Post, OwnershipOfferWire.OffersOf(spaceId), OwnershipOfferWire.Body(recipientMembershipId), headers: null);

        Assert.True(offered.StatusCode == HttpStatusCode.Created, $"Offering through the product: {offered.StatusCode} {offered.Body}");

        return OwnershipOfferWire.Parse(offered.Body).Id;
    }

    /// <summary>
    /// A fresh host's Space in which a fresh Account holds <paramref name="roleStableCode"/>, joined through the product, and
    /// offers ownership to its own Membership - a recipient the guarded insert would accept, so only the capability refuses.
    /// </summary>
    private static async Task<CaseRequest> LesserRoleOffersOwnershipAsync(IsolationWorld world, string roleStableCode)
    {
        var label = roleStableCode switch
        {
            "admin" => "Oa",
            "member" => "Om",
            _ => "Ov",
        };
        var host = await world.NewActorAsync(label + "h");
        var actor = await JoinedThroughTheProductAsync(world, host, label, roleStableCode);
        var own = await world.Database.MembershipOfAsync(host.SpaceId, actor.AccountId);
        var path = OwnershipOfferWire.OffersOf(host.SpaceId);

        return new CaseRequest(actor, HttpMethod.Post, [new(path, path)], host.SpaceId, Body: () => OwnershipOfferWire.Body(own.Id), Other: host);
    }

    /// <summary>
    /// No offer exists in A's Space or in B's.
    /// </summary>
    private static async Task NoOfferInEitherSpaceAsync(IsolationWorld world, CaseRequest request)
    {
        Assert.Empty(await world.Database.OwnershipOffersInAsync(world.A.SpaceId));
        Assert.Empty(await world.Database.OwnershipOffersInAsync(world.B.SpaceId));
    }

    /// <summary>
    /// No offer exists in the Space the refused request named.
    /// </summary>
    private static async Task NoOfferInTheTargetSpaceAsync(IsolationWorld world, CaseRequest request) =>
        Assert.Empty(await world.Database.OwnershipOffersInAsync(request.TargetSpaceId ?? throw new InvalidOperationException("The case names no target Space.")));

    /// <summary>
    /// The request's <c>Other</c> - the offer's host - still holds exactly one offer, <c>Pending</c>.
    /// </summary>
    private static async Task TheOthersOfferIsStillPendingAsync(IsolationWorld world, CaseRequest request)
    {
        var other = request.Other ?? throw new InvalidOperationException("The case names no Other whose offer it checks.");

        Assert.Equal("Pending", Assert.Single(await world.Database.OwnershipOffersInAsync(other.SpaceId)).State);
    }

    /// <summary>
    /// The Space the request's <c>Other</c> is seen from holds exactly one <c>Pending</c> offer.
    /// </summary>
    private static async Task TheOthersSpaceHoldsOnePendingOfferAsync(IsolationWorld world, CaseRequest request)
    {
        var other = request.Other ?? throw new InvalidOperationException("The case names no Other whose Space it checks.");

        Assert.Single(await world.Database.OwnershipOffersInAsync(other.SpaceId), row => row.State == "Pending");
    }

    /// <summary>
    /// The Space the request's <c>Other</c> is seen from holds no offer at all.
    /// </summary>
    private static async Task TheOthersSpaceHoldsNoOfferAsync(IsolationWorld world, CaseRequest request)
    {
        var other = request.Other ?? throw new InvalidOperationException("The case names no Other whose Space it checks.");

        Assert.Empty(await world.Database.OwnershipOffersInAsync(other.SpaceId));
    }

    /// <summary>
    /// The Space the request's <c>Other</c> is seen from holds an offer, and none of its offers is <c>Pending</c>.
    /// </summary>
    private static async Task TheOthersSpaceHoldsNoPendingOfferAsync(IsolationWorld world, CaseRequest request)
    {
        var other = request.Other ?? throw new InvalidOperationException("The case names no Other whose Space it checks.");
        var offers = await world.Database.OwnershipOffersInAsync(other.SpaceId);

        Assert.NotEmpty(offers);
        Assert.DoesNotContain(offers, row => row.State == "Pending");
    }
}
