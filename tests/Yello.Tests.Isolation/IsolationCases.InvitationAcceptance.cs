using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Invitations;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// Story 4.3 - view and accept an Invitation: the rows for <c>GET /api/v1/invitations/offer</c> (the token in the
/// <c>Yello-Invitation-Token</c> header, Lee's Q4 (a)) and <c>POST /api/v1/invitations/acceptance</c> (Session only, Lee's
/// Q7 (a)), each stating both surfaces (AC10; I-1, I-7, I-13, I-14, I-17). Written by the story 4.3 ATDD run (2026-11-01)
/// as <c>PendingUntil: "4.3"</c>, and made live by its dev phase (GATE-003).
/// </summary>
/// <remarks>
/// <para>
/// <b>The routes, the token header and the problem types are <c>Yello.Contracts</c>'</b> (<c>InvitationRoutes</c>,
/// <c>ProblemTypes</c>); the dev phase swapped the ATDD stand-ins for them, as 4.2's did. Neither route carries <c>{spaceId}</c> (C7), so these
/// rows are not boundary rows of the usual shape: the actor reaches a Space through a token, and each row says what the
/// token may and may not open.
/// </para>
/// <para>
/// <b>The token comes from the world's mail drop</b> (<see cref="IsolationWorld.Mail"/>, added by this run): an
/// Invitation's token exists in plaintext only in its email. Every token and every invited address a row creates is held as
/// its issuer's secret, so the I-17 log check reads it. Invitations are issued through 4.1's route, revoked through 4.2's,
/// accepted through this story's; <see cref="WireDatabase"/> writes only the expired arm's <c>ExpiresAt</c>.
/// </para>
/// <para>
/// <b>Deviation, recorded:</b> the story names one row for AC6 on "both routes"; here it is two, one per route
/// (<c>ISO-INVITATION-NOT-VALID-SAME-FOR-FIVE-STATES</c> on the view, <c>ISO-ACCEPT-INVITATION-NOT-VALID-SAME-FOR-FIVE-STATES</c>
/// on the accept), because a row names one route and the gap gate keys on it. The Space-deleted arm is the Slices case's
/// (<c>Five_states_are_one_answer_on_the_view_and_on_the_accept</c>): the world has no deletion helper that keeps its other
/// rows' Spaces intact.
/// </para>
/// </remarks>
internal static partial class IsolationCases
{
    /// <summary>
    /// The tokens a five-state row prepared, by the request it returned: each state's token, and the live anchor's.
    /// </summary>
    private static readonly ConditionalWeakTable<CaseRequest, IReadOnlyList<(string State, string Token)>> PreparedStates = new();

    /// <summary>
    /// The invitee's other Spaces' contents before the accept, by its Account (AC8's byte-identical check).
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, string> ContentsBefore = new();

    private static IEnumerable<IsolationCase> InvitationAcceptanceCases() =>
    [
        new IsolationCase(
            "ISO-INVITATION-VIEW-DISCLOSES-ONLY-THE-OFFER", "I-14 (4.3 AC2, AC10)", IsolationVector.DeepLink, HttpMethod.Get, InvitationRoutes.Offer,
            async world =>
            {
                var (issuing, _) = await world.OwnerOfTwoAsync("Io");
                var (_, _, token) = await InviteWithTokenAsync(world, issuing.Actor, InvitationWire.FreshAddress("offer"), "member");

                return ViewRequest(world.A, token) with { Other = issuing.Actor };
            },
            Both(new Served(HttpStatusCode.OK, TheViewDisclosesOnlyTheOfferAsync, "200, the Space name, Role and issuer of the token's Invitation, and nothing of the Space's content"))),

        new IsolationCase(
            "ISO-INVITATION-NOT-VALID-SAME-FOR-FIVE-STATES", "I-13 (4.3 AC6, AC10; Lee's Q6 (a))", IsolationVector.DeepLink, HttpMethod.Get, InvitationRoutes.Offer,
            async world => await FiveStatesAsync(world, accept: false),
            Both(new UniformNotValid())) with
        {
            Assumption = "PRD-12-9",
        },

        new IsolationCase(
            "ISO-ACCEPT-INVITATION-NOT-VALID-SAME-FOR-FIVE-STATES", "I-13 (4.3 AC6, AC10; Lee's Q6 (a), Q7 (a))", IsolationVector.DeepLink, HttpMethod.Post, InvitationRoutes.Acceptance,
            async world => await FiveStatesAsync(world, accept: true),
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new UniformNotValid(),
                [IsolationSurface.ApiToken] = new NoCredential(),
            }) with
        {
            Assumption = "PRD-12-9",
        },

        new IsolationCase(
            "ISO-ACCEPT-INVITATION-NO-SESSION", "I-2, AD-4 (4.3 AC4, AC10; Lee's Q7 (a))", IsolationVector.Api, HttpMethod.Post, InvitationRoutes.Acceptance,
            async world =>
            {
                var inviter = await world.NewActorAsync("Ns");
                var invitee = await world.NewActorAsync("Ni");
                var (_, _, token) = await InviteWithTokenAsync(world, inviter, invitee.EmailAddress, "member");

                return AcceptRequest(invitee, token) with { Other = inviter };
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.OK, TheActorJoinedTheInvitedSpaceAndNoSessionIsRefusedAsync, "200, the addressee joins the inviter's Space; the same request with no credential is the 401 anchor"),
                [IsolationSurface.ApiToken] = new NoCredential { Then = TheActorJoinedNothingAndTheInvitationIsPendingAsync },
            }),

        new IsolationCase(
            "ISO-ACCEPT-INVITATION-OTHER-ACCOUNT", "I-2, I-13 (4.3 AC7, AC10; Q13 (a))", IsolationVector.Write, HttpMethod.Post, InvitationRoutes.Acceptance,
            async world =>
            {
                var inviter = await world.NewActorAsync("Oa");
                var (_, _, token) = await InviteWithTokenAsync(world, inviter, InvitationWire.FreshAddress("someone-else"), "admin");

                return AcceptRequest(world.A, token) with { Other = inviter };
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.Forbidden, TheMismatchIsStatedWithoutAnAddressAndNothingIsWrittenAsync, "403 invitation-for-another-account, no address, nothing written"),
                [IsolationSurface.ApiToken] = new NoCredential { Then = TheActorJoinedNothingAndTheInvitationIsPendingAsync },
            }),

        new IsolationCase(
            "ISO-ACCEPT-JOINS-ONLY-THE-INVITED-SPACE", "I-2, UJ-2 (4.3 AC8, AC10)", IsolationVector.Write, HttpMethod.Post, InvitationRoutes.Acceptance,
            async world =>
            {
                var (own, elsewhere) = await world.OwnerElsewhereAsync("member");
                var inviter = await world.NewActorAsync("Ij");
                var invitee = own.Actor;
                var (_, _, token) = await InviteWithTokenAsync(world, inviter, invitee.EmailAddress, "member");

                ContentsBefore[invitee.AccountId] = await ContentsOfAsync(world, own.SpaceId, elsewhere.SpaceId);

                return AcceptRequest(invitee, token) with { Other = inviter };
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.OK, TheInviteeJoinedOnlyTheInvitedSpaceAsync, "200, one Account, Memberships B (Owner), C (unchanged) and A (invited), B and C unchanged"),
                [IsolationSurface.ApiToken] = new NoCredential { Then = TheActorJoinedNothingAndTheInvitationIsPendingAsync },
            }),

        new IsolationCase(
            "ISO-INVITER-SEES-NOTHING-OF-THE-INVITEES-OTHER-SPACES", "I-1, UJ-2 (4.3 AC8, AC10)", IsolationVector.DirectIdentifier, HttpMethod.Get, SpaceRoutes.Active,
            async world =>
            {
                var (own, elsewhere) = await world.OwnerElsewhereAsync("viewer");
                var inviter = await world.NewActorAsync("Is");
                var invitee = own.Actor;
                var (_, _, token) = await InviteWithTokenAsync(world, inviter, invitee.EmailAddress, "member");
                var accepted = await invitee.SendAsync(IsolationSurface.Session, HttpMethod.Post, InvitationRoutes.Acceptance, InvitationWire.AcceptBody(token), headers: null);

                Assert.True(accepted.StatusCode == HttpStatusCode.OK, $"Accepting through the product: {accepted.StatusCode} {accepted.Body}");

                return new CaseRequest(
                    inviter,
                    HttpMethod.Get,
                    [new(SpaceRoutes.ActiveOf(own.SpaceId), SpaceRoutes.ActiveOf(Guid.NewGuid()))],
                    own.SpaceId,
                    Other: elsewhere.Actor);
            },
            Both(new BoundaryNotFound { Then = TheInviterReadsNothingOfTheInviteesOtherSpacesAsync })),

        new IsolationCase(
            "ISO-ACCEPT-INVITATION-TOKEN-FROM-ANOTHER-SPACE", "I-2, I-4 (4.3 AC7, AC10)", IsolationVector.DeepLink, HttpMethod.Post, InvitationRoutes.Acceptance,
            async world =>
            {
                var ownerOfX = await world.NewActorAsync("Tx");
                var ownerOfY = await world.NewActorAsync("Ty");
                var holder = await world.NewActorAsync("Th");

                _ = await InviteWithTokenAsync(world, ownerOfX, holder.EmailAddress, "member");

                var (_, _, tokenOfY) = await InviteWithTokenAsync(world, ownerOfY, InvitationWire.FreshAddress("y-addressee"), "member");

                return AcceptRequest(holder, tokenOfY) with { Other = ownerOfY };
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.Forbidden, TheMismatchIsStatedWithoutAnAddressAndNothingIsWrittenAsync, "403 invitation-for-another-account; never a Membership in Y"),
                [IsolationSurface.ApiToken] = new NoCredential { Then = TheActorJoinedNothingAndTheInvitationIsPendingAsync },
            }),

        new IsolationCase(
            "ISO-TIMING-INVITATION-NOT-VALID-SAME-DURATION", "I-7, I-13 (4.3 AC6; Lee's Q6 (a))", IsolationVector.Timing, HttpMethod.Get, InvitationRoutes.Offer,
            async world => await FiveStatesAsync(world, accept: false),
            Both(new NotValidIndistinguishable()),
            Runner: CaseRunner.Timing) with
        {
            Assumption = "PRD-12-9",
        },

        // Code review of story 4.3: the accept's uniform 404 is held to the same floor and does the same work (the sentinel
        // key, the lock on the sentinel's resource, the same screen), so it is timed as the view is. A Token is no
        // credential on the accept (Lee's Q7 (a)), so that surface is the byte-identical 401.
        new IsolationCase(
            "ISO-TIMING-ACCEPT-INVITATION-NOT-VALID-SAME-DURATION", "I-7, I-13 (4.3 AC6; Lee's Q6 (a), Q7 (a))", IsolationVector.Timing, HttpMethod.Post, InvitationRoutes.Acceptance,
            async world => await FiveStatesAsync(world, accept: true),
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new NotValidIndistinguishable(),
                [IsolationSurface.ApiToken] = new NoCredential(),
            },
            Runner: CaseRunner.Timing) with
        {
            Assumption = "PRD-12-9",
        },
    ];

    /// <summary>
    /// The view as <paramref name="actor"/> sends it, the token in its header.
    /// </summary>
    private static CaseRequest ViewRequest(ProductActor actor, string token) =>
        new(actor, HttpMethod.Get, [new(InvitationRoutes.Offer, InvitationRoutes.Offer)], TargetSpaceId: null, Headers: InvitationWire.TokenHeaders(token));

    /// <summary>
    /// The accept as <paramref name="actor"/> sends it, the token in its body.
    /// </summary>
    private static CaseRequest AcceptRequest(ProductActor actor, string token) =>
        new(actor, HttpMethod.Post, [new(InvitationRoutes.Acceptance, InvitationRoutes.Acceptance)], TargetSpaceId: null, Body: () => InvitationWire.AcceptBody(token));

    /// <summary>
    /// An Invitation issued through 4.1's route on <paramref name="issuer"/>'s Session, in its Space, to
    /// <paramref name="address"/>; the token is read from the world's mail drop. The address and the token are held as the
    /// issuer's secrets (I-17).
    /// </summary>
    /// <returns>The Invitation's id, its address and its token.</returns>
    private static async Task<(Guid Id, string Address, string Token)> InviteWithTokenAsync(IsolationWorld world, ProductActor issuer, string address, string role)
    {
        issuer.HoldsSecret(address);

        var issued = await issuer.SendAsync(IsolationSurface.Session, HttpMethod.Post, InvitationRoutes.InvitationsOf(issuer.SpaceId), InvitationWire.Body(address, role), headers: null);

        Assert.True(issued.StatusCode == HttpStatusCode.NoContent, $"Seeding an Invitation through the product: {issued.StatusCode} {issued.Body}");

        var token = InvitationWire.TokenIn(await world.Mail.WaitForEmailToAsync(address));

        issuer.HoldsSecret(token);

        var row = Assert.Single(await world.Database.InvitationsInAsync(issuer.SpaceId), candidate => candidate.EmailAddress == address);

        return (row.Id, address, token);
    }

    /// <summary>
    /// A fresh inviter's Space holding a live, a revoked (4.2's route), an accepted (this story's route, by a fresh Account
    /// it names) and an expired (<c>ExpiresAt</c> moved past by the R12 method) Invitation, plus a well-formed token never
    /// issued and each malformed shape. The request is A's - a link holder no Invitation names - on the view or the accept.
    /// </summary>
    private static async Task<CaseRequest> FiveStatesAsync(IsolationWorld world, bool accept)
    {
        var inviter = await world.NewActorAsync("Fs");
        var acceptor = await world.NewActorAsync("Fa");
        var (_, _, live) = await InviteWithTokenAsync(world, inviter, InvitationWire.FreshAddress("live"), "member");
        var (revokedId, _, revoked) = await InviteWithTokenAsync(world, inviter, InvitationWire.FreshAddress("revoked"), "member");
        var (_, _, accepted) = await InviteWithTokenAsync(world, inviter, acceptor.EmailAddress, "member");
        var (expiredId, _, expired) = await InviteWithTokenAsync(world, inviter, InvitationWire.FreshAddress("expired"), "member");

        var revoke = await inviter.SendAsync(IsolationSurface.Session, HttpMethod.Delete, InvitationRoutes.InvitationOf(inviter.SpaceId, revokedId));

        Assert.True(revoke.StatusCode == HttpStatusCode.NoContent, $"Revoking through the product: {revoke.StatusCode} {revoke.Body}");

        var acceptance = await acceptor.SendAsync(IsolationSurface.Session, HttpMethod.Post, InvitationRoutes.Acceptance, InvitationWire.AcceptBody(accepted), headers: null);

        Assert.True(acceptance.StatusCode == HttpStatusCode.OK, $"Accepting through the product: {acceptance.StatusCode} {acceptance.Body}");

        await world.Database.SetInvitationExpiresAtAsync(inviter.SpaceId, expiredId, (await world.Database.DatabaseNowAsync()).AddMinutes(-1));

        var states = new List<(string State, string Token)>
        {
            ("live", live),
            ("revoked", revoked),
            ("accepted", accepted),
            ("expired", expired),
            ("unrecognised", InvitationWire.UnrecognisedToken()),
        };

        states.AddRange(InvitationWire.MalformedTokens().Select((token, index) => ($"malformed-{index}", token)));

        var request = (accept ? AcceptRequest(world.A, revoked) : ViewRequest(world.A, revoked)) with { Other = inviter };

        PreparedStates.AddOrUpdate(request, states);

        return request;
    }

    /// <summary>
    /// The view or the accept of <paramref name="token"/>, as <paramref name="request"/>'s actor on <paramref name="surface"/>.
    /// </summary>
    internal static Task<WireResponse> SendWithTokenAsync(CaseRequest request, IsolationSurface surface, string token) =>
        request.Method == HttpMethod.Get
            ? request.Actor.SendAsync(surface, HttpMethod.Get, InvitationRoutes.Offer, body: null, InvitationWire.TokenHeaders(token))
            : request.Actor.SendAsync(surface, HttpMethod.Post, InvitationRoutes.Acceptance, InvitationWire.AcceptBody(token), headers: null);

    /// <summary>
    /// The states a five-state row prepared.
    /// </summary>
    internal static IReadOnlyList<(string State, string Token)> StatesOf(CaseRequest request) =>
        PreparedStates.TryGetValue(request, out var states)
            ? states
            : throw new InvalidOperationException("The row prepared no Invitation states.");

    /// <summary>
    /// I-14 for the view: the body carries the issuer's Space name, the Role and the issuer's display name, and nothing
    /// else of that Space - none of its owner's other secrets (its Project, Task and Label names, password, Token, cookie),
    /// no address, no id. Story 4.3.
    /// </summary>
    private static Task TheViewDisclosesOnlyTheOfferAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var issuer = request.Other ?? throw new InvalidOperationException("The row names no issuer.");
        var offer = InvitationWire.ParseOffer(answer.Body);
        var escapedName = JsonSerializer.Serialize(issuer.SpaceName);

        Assert.Subset(InvitationWire.OfferMembers.ToHashSet(StringComparer.Ordinal), InvitationWire.MemberNames(answer.Body).ToHashSet(StringComparer.Ordinal));
        Assert.Equal((issuer.SpaceName, "member", issuer.DisplayName), (offer.SpaceName, offer.Role, offer.IssuerName));
        Assert.DoesNotContain("@", answer.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(issuer.SpaceId.ToString("D"), answer.Body, StringComparison.OrdinalIgnoreCase);

        foreach (var secret in issuer.Secrets.Where(secret => !issuer.SpaceName.Contains(secret, StringComparison.Ordinal) && !escapedName.Contains(secret, StringComparison.Ordinal)))
        {
            Assert.DoesNotContain(secret, answer.Body, StringComparison.Ordinal);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// The addressee joined the inviter's Space at the offered Role, and the same request with no credential at all is the
    /// <c>401</c> anchor, byte for byte (Q7 (a): the accept needs a Session). Story 4.3.
    /// </summary>
    private static async Task TheActorJoinedTheInvitedSpaceAndNoSessionIsRefusedAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var inviter = request.Other ?? throw new InvalidOperationException("The row names no inviter.");

        Assert.Equal(inviter.SpaceId, InvitationWire.AcceptedSpaceId(answer.Body));
        Assert.Contains((inviter.SpaceId, "Member"), await world.Database.MembershipsOfAccountAsync(request.Actor.AccountId));

        var anchor = await world.SendWithoutCredentialAsync(HttpMethod.Post, InvitationRoutes.Acceptance);

        Assert.Equal(HttpStatusCode.Unauthorized, anchor.StatusCode);
    }

    /// <summary>
    /// The refused accept wrote nothing: the actor holds no Membership in the inviter's Space, and the inviter's
    /// Invitations are all still <c>Pending</c>. Story 4.3.
    /// </summary>
    private static async Task TheActorJoinedNothingAndTheInvitationIsPendingAsync(IsolationWorld world, CaseRequest request)
    {
        var inviter = request.Other ?? throw new InvalidOperationException("The row names no inviter.");
        var rows = await world.Database.InvitationsInAsync(inviter.SpaceId);

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Equal("Pending", row.State));
        Assert.DoesNotContain(await world.Database.MembershipsOfAccountAsync(request.Actor.AccountId), membership => membership.SpaceId == inviter.SpaceId);
    }

    /// <summary>
    /// AC7's answer: <c>403 invitation-for-another-account</c>, no <c>@</c> and neither address, and nothing written. Story
    /// 4.3.
    /// </summary>
    private static async Task TheMismatchIsStatedWithoutAnAddressAndNothingIsWrittenAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        using (var body = JsonDocument.Parse(answer.Body))
        {
            Assert.Equal(ProblemTypes.InvitationForAnotherAccount, body.RootElement.GetProperty("type").GetString());
        }

        Assert.DoesNotContain("@", answer.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(request.Actor.EmailAddress.Split('@')[0], answer.Body, StringComparison.OrdinalIgnoreCase);

        await TheActorJoinedNothingAndTheInvitationIsPendingAsync(world, request);
    }

    /// <summary>
    /// AC8: one Account for the address; its Memberships are exactly its own Space (Owner), the other Account's Space
    /// (unchanged Role) and the inviter's (the offered Role); and its own and the other Space's contents are unchanged.
    /// Story 4.3.
    /// </summary>
    private static async Task TheInviteeJoinedOnlyTheInvitedSpaceAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var inviter = request.Other ?? throw new InvalidOperationException("The row names no inviter.");
        var invitee = request.Actor;
        var memberships = await world.Database.MembershipsOfAccountAsync(invitee.AccountId);

        Assert.Equal(inviter.SpaceId, InvitationWire.AcceptedSpaceId(answer.Body));
        Assert.Equal(1, await world.Database.AccountCountAsync(invitee.EmailAddress));
        Assert.Equal(3, memberships.Count);
        Assert.Contains((invitee.SpaceId, "Owner"), memberships);
        Assert.Contains((inviter.SpaceId, "Member"), memberships);
        Assert.Contains(memberships, membership => membership.SpaceId != invitee.SpaceId && membership.SpaceId != inviter.SpaceId && membership.Role == "Member");

        var other = memberships.Single(membership => membership.SpaceId != invitee.SpaceId && membership.SpaceId != inviter.SpaceId).SpaceId;

        Assert.Equal(ContentsBefore[invitee.AccountId], await ContentsOfAsync(world, invitee.SpaceId, other));
    }

    /// <summary>
    /// AC8's other half: after the invitee joined the inviter's Space, the inviter still meets the boundary <c>404</c> on
    /// the invitee's other Space too, and nothing the inviter reads in its own Space - the Space, its Invitations, its
    /// Projects - names anything the invitee holds elsewhere. Story 4.3.
    /// </summary>
    private static async Task TheInviterReadsNothingOfTheInviteesOtherSpacesAsync(IsolationWorld world, CaseRequest request)
    {
        var inviter = request.Actor;
        var elsewhere = request.Other ?? throw new InvalidOperationException("The row names the invitee's other Space through Other.");

        Assert.Equal(HttpStatusCode.NotFound, (await inviter.SendAsync(IsolationSurface.Session, HttpMethod.Get, SpaceRoutes.ActiveOf(elsewhere.SpaceId))).StatusCode);

        foreach (var path in new[] { SpaceRoutes.ActiveOf(inviter.SpaceId), InvitationRoutes.InvitationsOf(inviter.SpaceId), Yello.Contracts.Projects.ProjectRoutes.ListIn(inviter.SpaceId) })
        {
            var read = await inviter.SendAsync(IsolationSurface.Session, HttpMethod.Get, path);

            Assert.True(read.StatusCode == HttpStatusCode.OK, $"{path}: {read.StatusCode} {read.Body}");
            Assert.All(elsewhere.Secrets, secret => Assert.DoesNotContain(secret, read.Body, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Two Spaces' Projects, Tasks and Labels, as one comparable text.
    /// </summary>
    private static async Task<string> ContentsOfAsync(IsolationWorld world, params Guid[] spaces)
    {
        var parts = new List<string>();

        foreach (var spaceId in spaces)
        {
            parts.AddRange((await world.Database.ProjectsInAsync(spaceId)).Select(project => $"{spaceId:N} P {project.Id:N} {project.Name}").Order(StringComparer.Ordinal));
            parts.AddRange((await world.Database.TasksInAsync(spaceId)).Select(task => $"{spaceId:N} T {task.Id:N} {task.ProjectId:N} {task.Title}").Order(StringComparer.Ordinal));
            parts.AddRange((await world.Database.LabelsInAsync(spaceId)).Select(label => $"{spaceId:N} L {label.Id:N} {label.Name}").Order(StringComparer.Ordinal));
        }

        return string.Join('\n', parts);
    }
}

/// <summary>
/// Story 4.3 AC6, I-13, Lee's Q6 (a): every non-live state the row prepared - revoked, accepted, expired, unrecognised and
/// each malformed token - is answered the same <c>404 invitation-not-valid</c>, byte for byte, never the boundary's
/// <c>not-available</c>; the live anchor answers differently (the offer on the view, the mismatch on the accept); nothing is
/// recorded; the body discloses nothing (I-13).
/// </summary>
internal sealed record UniformNotValid() : Expectation("404 invitation-not-valid, byte-identical for every non-live state, nothing recorded")
{
    /// <inheritdoc />
    protected override async Task VerifyCoreAsync(IsolationWorld world, CaseRequest request, IsolationSurface surface)
    {
        var states = IsolationCases.StatesOf(request);
        var before = await world.Database.RefusalCountAsync(request.Actor.AccountId);
        var live = await IsolationCases.SendWithTokenAsync(request, surface, states.Single(state => state.State == "live").Token);
        var answers = new List<(string State, WireResponse Answer)>();

        foreach (var (state, token) in states.Where(state => state.State != "live"))
        {
            answers.Add((state, await IsolationCases.SendWithTokenAsync(request, surface, token)));
        }

        var (_, first) = answers[0];

        Assert.True(first.StatusCode == HttpStatusCode.NotFound, $"{first.StatusCode} {first.Body}");
        Assert.Contains(ProblemTypes.InvitationNotValid, first.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(ProblemTypes.NotAvailable, first.Body, StringComparison.Ordinal);
        Assert.NotEqual(live.StatusCode, first.StatusCode);

        foreach (var (state, answer) in answers)
        {
            AssertSameBytes(first, answer, state);
            AssertDisclosesNothing(world, answer, InvitationRoutes.Offer);
            Assert.DoesNotContain("@", answer.Body, StringComparison.Ordinal);
        }

        Assert.Equal(before, await world.Database.RefusalCountAsync(request.Actor.AccountId));
    }
}

/// <summary>
/// Story 4.3 AC6, I-7, Lee's Q6 (a): the view's uniform answer takes the same time whatever the state - a revoked token
/// against one never issued, then an expired token against one never issued - by the one duration method (21 interleaved
/// samples, median, 20%, the Host's own figure). The permanent oracle beside it is
/// <c>IsolationTimingTests.The_duration_method_tells_the_not_valid_answer_from_an_arm_that_does_no_work</c>.
/// </summary>
internal sealed record NotValidIndistinguishable() : Expectation("the uniform 404 takes the same time for a revoked, an expired and an unrecognised token")
{
    /// <inheritdoc />
    protected override async Task VerifyCoreAsync(IsolationWorld world, CaseRequest request, IsolationSurface surface)
    {
        var states = IsolationCases.StatesOf(request);

        foreach (var known in new[] { "revoked", "expired" })
        {
            var token = states.Single(state => state.State == known).Token;

            var verdict = await DurationIndistinguishability.CompareMeasuredAsync(
                () => world.Host.ServerDurationOfAsync(() => IsolationCases.SendWithTokenAsync(request, surface, token)),
                () => world.Host.ServerDurationOfAsync(() => IsolationCases.SendWithTokenAsync(request, surface, InvitationWire.UnrecognisedToken())));

            TimingRecord.Add($"invitation not valid ({known} vs unrecognised) / {surface}: {verdict.Describe()}");
            Assert.True(verdict.AreIndistinguishable, $"{known} vs unrecognised / {surface}: {verdict.Describe()}");
        }
    }
}
