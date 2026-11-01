using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts.Invitations;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// Story 4.1 - issue an Invitation: the rows for <c>POST /api/v1/spaces/{spaceId}/invitations</c> (AC9; I-2, I-7, I-12;
/// A-4, A-5), each stating both surfaces. Written by the story 4.1 ATDD run (2026-11-01) as <c>PendingUntil: "4.1"</c>;
/// converted by its dev phase (GATE-003).
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
    ];

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
