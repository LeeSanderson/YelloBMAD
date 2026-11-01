using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Invitations;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// Story 4.1 - issue an Invitation: the rows for <c>POST /api/v1/spaces/{spaceId}/invitations</c> (AC9; I-2, I-7, I-12;
/// A-4, A-5), each stating both surfaces. Written by the story 4.1 ATDD run (2026-11-01) as <c>PendingUntil: "4.1"</c>;
/// converted by its dev phase (GATE-003). Story 4.2 - list and revoke Invitations: the rows for <c>GET</c> on the same path
/// and <c>DELETE</c> on <c>.../invitations/{invitationId}</c> (AC8), written by the story 4.2 ATDD run as
/// <c>PendingUntil: "4.2"</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The route is <see cref="InvitationRoutes.Issue"/></b>; the ATDD run's temporary scaffold constant was swapped for it
/// by the dev phase.
/// </para>
/// <para>
/// <b>I-14 (the email discloses nothing) is not a row here</b>: the isolation world's Host has no mail drop, so it is the
/// Slices wire test <c>ActiveSpaceEndpointTests.The_email_names_the_Space_the_Role_and_the_issuer_and_discloses_nothing_else</c>
/// (the 3.4 Q4 precedent). Its API counterpart is the same outbox and template, which the Token arms of the rows below
/// reach through the same pipeline (AD-4).
/// </para>
/// <para>
/// <b>I-17.</b> Every invited address a row sends is added to its actor's secrets, so the row log check sees it. The
/// token is never known to these rows (no mail drop); the Slices case
/// <c>Neither_the_invited_address_nor_the_token_reaches_the_Host_log</c> covers it.
/// </para>
/// </remarks>
internal static partial class IsolationCases
{
    private static IEnumerable<IsolationCase> InvitationCases() =>
    [
        new(
            "ISO-ISSUE-INVITATION-FOREIGN-SPACE", "I-2 (4.1 AC9)", IsolationVector.Write, HttpMethod.Post, InvitationRoutes.Issue,
            world =>
            {
                var invitee = InvitationWire.FreshAddress("foreign");

                world.A.HoldsSecret(invitee);

                return Task.FromResult(new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(InvitationRoutes.InvitationsOf(world.B.SpaceId), InvitationRoutes.InvitationsOf(Guid.NewGuid()))],
                    world.B.SpaceId,
                    Body: () => InvitationWire.Body(invitee, "member")));
            },
            Both(new BoundaryNotFound { Then = NoInvitationOrOutboxRowForTheCaseAddressAsync })),

        new(
            "ISO-RECORD-KIND-ISSUE-INVITATION-MEMBER", "V-1, C-6, NFR-7 (4.1 AC1, AC9)", IsolationVector.RecordKind, HttpMethod.Post, InvitationRoutes.Issue,
            world => LesserRoleIssuesAnInvitationAsync(world, "member"),
            Both(new RoleRefused(InvitationWire.CapabilityCode) { Then = NoInvitationOrOutboxRowForTheCaseAddressAsync })),

        new(
            "ISO-RECORD-KIND-ISSUE-INVITATION-VIEWER", "V-1, C-6, NFR-7 (4.1 AC1, AC9)", IsolationVector.RecordKind, HttpMethod.Post, InvitationRoutes.Issue,
            world => LesserRoleIssuesAnInvitationAsync(world, "viewer"),
            Both(new RoleRefused(InvitationWire.CapabilityCode) { Then = NoInvitationOrOutboxRowForTheCaseAddressAsync })),

        TokenOwnOtherSpace(
            "ISSUE-INVITATION",
            HttpMethod.Post,
            InvitationRoutes.Issue,
            HttpStatusCode.NoContent,
            second => InvitationRoutes.InvitationsOf(second.SpaceId),
            () => InvitationWire.Body(InvitationWire.FreshAddress("token-elsewhere"), "member"))
            with
        {
            TestDesignId = "I-12 (4.1 AC9; the 3.4 AC3 gate)",
        },

        new(
            "ISO-ISSUE-INVITATION-NO-ACCOUNT-ORACLE", "I-7 (4.1 AC5, AC9; duration: ISO-TIMING-ISSUE-INVITATION-NO-ACCOUNT-ORACLE)", IsolationVector.Api, HttpMethod.Post, InvitationRoutes.Issue,
            FreshActorIssuesInItsOwnSpaceAsync,
            Both(new SameAnswerWhetherOrNotTheAddressHasAnAccount())),

        new(
            "ISO-TIMING-ISSUE-INVITATION-NO-ACCOUNT-ORACLE", "I-7 (4.1 AC5)", IsolationVector.Timing, HttpMethod.Post, InvitationRoutes.Issue,
            FreshActorIssuesInItsOwnSpaceAsync,
            Both(new IssueIndistinguishableWhetherOrNotTheAddressHasAnAccount()),
            Runner: CaseRunner.Timing),

        // Story 4.2 - list and revoke Invitations (AC8; I-1, I-2, I-3, I-4, I-12, I-13, I-17). Written by the story 4.2
        // ATDD run (2026-11-01) as PendingUntil "4.2": GATE-003 fails the suite if any is still pending at review. The
        // revoke's route is InvitationWire.RevokeRoute until Task 2 declares InvitationRoutes.Invitation. Every seeded
        // address is held as its issuer's secret, so the I-17 log check reads it.
        new(
            "ISO-LIST-INVITATIONS-FOREIGN-SPACE", "I-1 (4.2 AC8)", IsolationVector.DirectIdentifier, HttpMethod.Get, InvitationRoutes.Issue,
            async world =>
            {
                _ = await IssueThroughTheProductAsync(world, world.B, "listed-foreign");

                return await ForeignAsync(world, InvitationRoutes.InvitationsOf);
            },
            Both(new BoundaryNotFound()),
            PendingUntil: "4.2",
            PendingReason: Story42Pending),

        new(
            "ISO-LIST-INVITATIONS-OWN-SPACE", "I-3 (4.2 AC8)", IsolationVector.Listing, HttpMethod.Get, InvitationRoutes.Issue,
            async world =>
            {
                _ = await IssueThroughTheProductAsync(world, world.A, "listed-own");
                _ = await IssueThroughTheProductAsync(world, world.B, "listed-theirs");

                return await OwnAsync(world, InvitationRoutes.InvitationsOf(world.A.SpaceId));
            },
            Both(new Served(HttpStatusCode.OK, ListsExactlyTheCallersPendingInvitationsAsync, "200, exactly SA's actionable Invitations, none of SB's ids or addresses")),
            PendingUntil: "4.2",
            PendingReason: Story42Pending),

        new(
            "ISO-RECORD-KIND-LIST-INVITATIONS-MEMBER", "V-1, C-6, NFR-7 (4.2 AC1, AC8; Q16 (a))", IsolationVector.RecordKind, HttpMethod.Get, InvitationRoutes.Issue,
            world => LesserRoleTouchesItsInvitationAsync(world, "member", revoke: false),
            Both(new RoleRefused(InvitationWire.CapabilityCode) { Then = TheActorsInvitationsAreStillPendingAsync }),
            PendingUntil: "4.2",
            PendingReason: Story42Pending),

        new(
            "ISO-RECORD-KIND-LIST-INVITATIONS-VIEWER", "V-1, C-6, NFR-7 (4.2 AC1, AC8; Q16 (a))", IsolationVector.RecordKind, HttpMethod.Get, InvitationRoutes.Issue,
            world => LesserRoleTouchesItsInvitationAsync(world, "viewer", revoke: false),
            Both(new RoleRefused(InvitationWire.CapabilityCode) { Then = TheActorsInvitationsAreStillPendingAsync }),
            PendingUntil: "4.2",
            PendingReason: Story42Pending),

        new(
            "ISO-REVOKE-INVITATION-FOREIGN-SPACE", "I-2 (4.2 AC8)", IsolationVector.Write, HttpMethod.Delete, InvitationWire.RevokeRoute,
            async world =>
            {
                var (foreign, _) = await IssueThroughTheProductAsync(world, world.B, "revoked-foreign");

                return new CaseRequest(
                    world.A,
                    HttpMethod.Delete,
                    [new(InvitationWire.RevokePathOf(world.B.SpaceId, foreign), InvitationWire.RevokePathOf(Guid.NewGuid(), foreign))],
                    world.B.SpaceId);
            },
            Both(new BoundaryNotFound { Then = TheNamedInvitationIsStillPendingAsync }),
            PendingUntil: "4.2",
            PendingReason: Story42Pending),

        new(
            "ISO-INVITATION-FOREIGN-ID-IN-OWN-SPACE", "I-4, I-13 (4.2 AC4, AC8; Q3 (a))", IsolationVector.DeepLink, HttpMethod.Delete, InvitationWire.RevokeRoute,
            async world =>
            {
                var (foreign, _) = await IssueThroughTheProductAsync(world, world.B, "foreign-id");

                // The ISO-LABEL-FOREIGN-ID-IN-OWN-SPACE model: B's id under A's own Space path answers as an id that never
                // existed there - the unrecorded in-Space not-found (Q3 (a)), never a 409 that would admit the id exists.
                return new CaseRequest(
                    world.A,
                    HttpMethod.Delete,
                    [new(InvitationWire.RevokePathOf(world.A.SpaceId, foreign), InvitationWire.RevokePathOf(world.A.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = TheNamedInvitationIsStillPendingAsync }),
            PendingUntil: "4.2",
            PendingReason: Story42Pending),

        new(
            "ISO-RECORD-KIND-REVOKE-INVITATION-MEMBER", "V-1, C-6, NFR-7 (4.2 AC2, AC8)", IsolationVector.RecordKind, HttpMethod.Delete, InvitationWire.RevokeRoute,
            world => LesserRoleTouchesItsInvitationAsync(world, "member", revoke: true),
            Both(new RoleRefused(InvitationWire.CapabilityCode) { Then = TheNamedInvitationIsStillPendingAsync }),
            PendingUntil: "4.2",
            PendingReason: Story42Pending),

        new(
            "ISO-RECORD-KIND-REVOKE-INVITATION-VIEWER", "V-1, C-6, NFR-7 (4.2 AC2, AC8)", IsolationVector.RecordKind, HttpMethod.Delete, InvitationWire.RevokeRoute,
            world => LesserRoleTouchesItsInvitationAsync(world, "viewer", revoke: true),
            Both(new RoleRefused(InvitationWire.CapabilityCode) { Then = TheNamedInvitationIsStillPendingAsync }),
            PendingUntil: "4.2",
            PendingReason: Story42Pending),

        TokenOwnOtherSpace(
            "LIST-INVITATIONS",
            HttpMethod.Get,
            InvitationRoutes.Issue,
            HttpStatusCode.OK,
            second => InvitationRoutes.InvitationsOf(second.SpaceId))
            with
        {
            TestDesignId = "I-12 (4.2 AC8; the 3.4 AC3 gate)",
            PendingUntil = "4.2",
            PendingReason = Story42Pending,
        },

        new(
            "ISO-TOKEN-OWN-OTHER-SPACE-REVOKE-INVITATION", "I-12 (4.2 AC8; the 3.4 AC3 gate)", IsolationVector.TokenOwnOtherSpace, HttpMethod.Delete, InvitationWire.RevokeRoute,
            async world =>
            {
                var (first, second) = await world.OwnerOfTwoAsync("Tr");
                var (invitation, _) = await IssueThroughTheProductAsync(world, second.Actor, "token-elsewhere-revoke");
                var path = InvitationWire.RevokePathOf(second.SpaceId, invitation);

                return new CaseRequest(first.Actor, HttpMethod.Delete, [new(path, path)], second.SpaceId, Other: second.Actor);
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.NoContent, TheNamedInvitationIsRevokedAsync, "204 - a Session carries no Space (FR-2); the Account owns SA2"),
                [IsolationSurface.ApiToken] = new SameAsNonMember { Then = TheNamedInvitationIsStillPendingAsync },
            },
            PendingUntil: "4.2",
            PendingReason: Story42Pending),
    ];

    /// <summary>
    /// Why story 4.2's rows are pending: the routes they name are mapped by its dev phase. GATE-003 fails any of them still
    /// pending once 4.2 reaches review.
    /// </summary>
    private const string Story42Pending = "GET and DELETE on the Invitation routes are mapped by story 4.2's dev phase (Task 5)";

    /// <summary>
    /// An Invitation issued through the product - 4.1's route on the owner's Session, in the owner's own Space - to a fresh
    /// address held as the owner's secret (I-17). Story 4.2.
    /// </summary>
    /// <returns>The Invitation's id and its address.</returns>
    private static async Task<(Guid Id, string Address)> IssueThroughTheProductAsync(IsolationWorld world, ProductActor owner, string label)
    {
        var address = InvitationWire.FreshAddress(label);

        owner.HoldsSecret(address);

        var issued = await owner.SendAsync(IsolationSurface.Session, HttpMethod.Post, InvitationRoutes.InvitationsOf(owner.SpaceId), InvitationWire.Body(address, "member"), headers: null);

        Assert.True(issued.StatusCode == HttpStatusCode.NoContent, $"Seeding an Invitation through the product: {issued.StatusCode} {issued.Body}");

        var row = Assert.Single(await world.Database.InvitationsInAsync(owner.SpaceId), candidate => candidate.EmailAddress == address);

        return (row.Id, address);
    }

    /// <summary>
    /// A fresh Account issues an Invitation in its own Personal Space as its Owner, and is then demoted to
    /// <paramref name="roleStableCode"/> there, so its Session and its Token hold that Role; the request lists the Space's
    /// Invitations or revokes that one. Story 4.2.
    /// </summary>
    private static async Task<CaseRequest> LesserRoleTouchesItsInvitationAsync(IsolationWorld world, string roleStableCode, bool revoke)
    {
        var actor = await world.NewActorAsync(string.Equals(roleStableCode, "member", StringComparison.Ordinal) ? "Mr" : "Vr");
        var (invitation, _) = await IssueThroughTheProductAsync(world, actor, "lesser-" + roleStableCode);

        await world.Database.ChangeRoleAsync(actor.SpaceId, actor.AccountId, roleStableCode);

        var path = revoke ? InvitationWire.RevokePathOf(actor.SpaceId, invitation) : InvitationRoutes.InvitationsOf(actor.SpaceId);

        return new CaseRequest(actor, revoke ? HttpMethod.Delete : HttpMethod.Get, [new(path, path)], actor.SpaceId);
    }

    /// <summary>
    /// I-3 for Invitations: A's list holds exactly A's actionable Invitations (pending, unexpired by the database clock) and
    /// none of B's ids or addresses - anchored both ways, since both Spaces hold one. Story 4.2.
    /// </summary>
    private static async Task ListsExactlyTheCallersPendingInvitationsAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var listed = InvitationWire.ParseList(answer.Body);
        var now = await world.Database.DatabaseNowAsync();
        var own = (await world.Database.InvitationsInAsync(world.A.SpaceId)).Where(row => row.State == "Pending" && row.ExpiresAt > now).ToList();
        var theirs = await world.Database.InvitationsInAsync(world.B.SpaceId);

        Assert.NotEmpty(own);
        Assert.NotEmpty(theirs);
        Assert.Equal(own.Select(row => row.Id).Order(), listed.Select(row => row.Id).Order());
        Assert.Empty(listed.Select(row => row.Id).Intersect(theirs.Select(row => row.Id)));
        Assert.All(theirs, row => Assert.DoesNotContain(row.EmailAddress, answer.Body, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The Invitation the request named - the last segment of its path - still exists in one of the case's Spaces and is
    /// still stored <c>Pending</c>. Story 4.2.
    /// </summary>
    private static async Task TheNamedInvitationIsStillPendingAsync(IsolationWorld world, CaseRequest request) =>
        Assert.Equal("Pending", (await NamedInvitationAsync(world, request)).State);

    /// <summary>
    /// The Invitation the request named is now stored <c>Revoked</c>: the served arm of a Token-elsewhere row. Story 4.2.
    /// </summary>
    private static async Task TheNamedInvitationIsRevokedAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        Assert.Equal(string.Empty, answer.Body);
        Assert.Equal("Revoked", (await NamedInvitationAsync(world, request)).State);
    }

    /// <summary>
    /// The actor's own Space still holds its seeded Invitation, still <c>Pending</c> (the anchor and the absence: a refused
    /// list wrote nothing). Story 4.2.
    /// </summary>
    private static async Task TheActorsInvitationsAreStillPendingAsync(IsolationWorld world, CaseRequest request)
    {
        var rows = await world.Database.InvitationsInAsync(request.Actor.SpaceId);

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Equal("Pending", row.State));
    }

    /// <summary>
    /// The Invitation the request's first path names, found in whichever of the case's Spaces holds it.
    /// </summary>
    private static async Task<InvitationRow> NamedInvitationAsync(IsolationWorld world, CaseRequest request)
    {
        var named = Guid.Parse(request.Paths[0].Path.Split('/')[^1]);
        Guid?[] spaces = [request.TargetSpaceId, request.Actor.SpaceId, request.Other?.SpaceId, world.A.SpaceId, world.B.SpaceId];

        foreach (var spaceId in spaces.OfType<Guid>().Distinct())
        {
            if ((await world.Database.InvitationsInAsync(spaceId)).SingleOrDefault(row => row.Id == named) is { } row)
            {
                return row;
            }
        }

        throw new InvalidOperationException($"The Invitation {named} the request named is in none of the case's Spaces.");
    }

    /// <summary>
    /// A fresh Account demoted to <paramref name="roleStableCode"/> in its own Personal Space, so its Session and its Token
    /// both hold that Role there; the request is a valid issue in it. Story 4.1.
    /// </summary>
    private static async Task<CaseRequest> LesserRoleIssuesAnInvitationAsync(IsolationWorld world, string roleStableCode)
    {
        var actor = await world.NewActorAsync(string.Equals(roleStableCode, "member", StringComparison.Ordinal) ? "Mi" : "Vi");
        var invitee = InvitationWire.FreshAddress("lesser");

        await world.Database.ChangeRoleAsync(actor.SpaceId, actor.AccountId, roleStableCode);
        actor.HoldsSecret(invitee);

        var path = InvitationRoutes.InvitationsOf(actor.SpaceId);

        return new CaseRequest(actor, HttpMethod.Post, [new(path, path)], actor.SpaceId, Body: () => InvitationWire.Body(invitee, "member"));
    }

    /// <summary>
    /// A fresh Account, Owner of its own Personal Space (its Token is bound there), issuing in it; the expectation chooses
    /// the addresses. Story 4.1.
    /// </summary>
    private static async Task<CaseRequest> FreshActorIssuesInItsOwnSpaceAsync(IsolationWorld world)
    {
        var actor = await world.NewActorAsync("Io");
        var path = InvitationRoutes.InvitationsOf(actor.SpaceId);

        return new CaseRequest(actor, HttpMethod.Post, [new(path, path)], actor.SpaceId);
    }

    /// <summary>
    /// The case's invited address - read back from its body - has no Invitation in the Space the request named, nor in the
    /// actor's own, and no outbox row. Story 4.1.
    /// </summary>
    private static async Task NoInvitationOrOutboxRowForTheCaseAddressAsync(IsolationWorld world, CaseRequest request)
    {
        var content = Assert.IsType<Func<HttpContent>>(request.Body)();

        using var body = JsonDocument.Parse(await content.ReadAsStringAsync());

        var invitee = body.RootElement.GetProperty("emailAddress").GetString();

        Assert.False(string.IsNullOrEmpty(invitee), "The case sent no address, so the absence below would be vacuous.");

        foreach (var spaceId in new[] { request.TargetSpaceId, request.Actor.SpaceId }.OfType<Guid>().Distinct())
        {
            Assert.DoesNotContain(await world.Database.InvitationsInAsync(spaceId), row => string.Equals(row.EmailAddress, invitee, StringComparison.OrdinalIgnoreCase));
        }

        Assert.DoesNotContain(await world.Database.OutboxRowsAsync(), row => string.Equals(row.Recipient, invitee, StringComparison.OrdinalIgnoreCase));
    }
}
