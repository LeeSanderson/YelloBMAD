using Yello.Application.Invitations;
using Yello.Application.Invitations.IssueInvitation;
using Yello.Application.Invitations.ListInvitations;
using Yello.Application.Invitations.RevokeInvitation;
using Yello.Application.Spaces;
using Yello.Contracts;
using Yello.Contracts.Invitations;
using Yello.Domain;
using Yello.Domain.Accounts;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// Issue an Invitation into the active Space (FR-10, FR-39; story 4.1, AC1-AC5, AC13), list its pending Invitations and
/// revoke one (FR-12; story 4.2, AC1-AC4, AC10).
/// </summary>
/// <remarks>
/// <para>
/// <b>No Membership check, no Role, no bound count and no idempotency here, and there must never be any</b> - the
/// reasons <see cref="ProjectEndpoints"/> gives (AR-3). The route carries <c>{spaceId}</c>, so the pipeline has resolved
/// the Space, checked <c>IssueAndRevokeInvitations</c> (Owner and Admin; a Member or Viewer is its <c>403</c>, a
/// non-member its boundary <c>404</c>) and replayed a key it had already seen before this code runs.
/// </para>
/// <para>
/// <b>Both surfaces</b> (AD-4, FR-35). <b>The body is read inside the endpoint</b>, never bound.
/// </para>
/// <para>
/// <b>One success answer: <c>204</c>, no body, no <c>Location</c></b> (open question 5 (a)) - byte for byte the same
/// whether or not the address has an Account (AD-23). Every refusal is about what was sent or what the issuer can
/// already see: <c>400 invalid-email-address</c>, <c>400 role-not-offerable</c>, <c>409 already-a-member</c>,
/// <c>409 invitation-pending</c>. None echoes the address.
/// </para>
/// </remarks>
public static class InvitationEndpoints
{
    /// <summary>
    /// The Roles an Invitation may offer, by stable code: every Role but Owner (FR-10, FR-42; Lee's Q2 - an Admin may
    /// offer Admin too).
    /// </summary>
    private static readonly Dictionary<string, Role> OfferableRoles = Enum.GetValues<Role>()
        .Where(role => role != Role.Owner)
        .ToDictionary(role => StableCode.Of(role), role => role, StringComparer.Ordinal);

    /// <summary>
    /// Registers <c>POST /api/v1/spaces/{spaceId}/invitations</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapIssueInvitation(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(InvitationRoutes.Issue, IssueAsync)
            .RequiresCapability(Capability.IssueAndRevokeInvitations);

    /// <summary>
    /// Registers <c>GET /api/v1/spaces/{spaceId}/invitations</c>: the pending list (story 4.2, AC1). Under
    /// <c>IssueAndRevokeInvitations</c>, never <c>ReadProjectsAndTasks</c> - the body carries email addresses, which section
    /// 6.1 and AD-23 confine to Owners and Admins (open question 16 (a)).
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapListInvitations(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(InvitationRoutes.List, ListAsync)
            .RequiresCapability(Capability.IssueAndRevokeInvitations);

    /// <summary>
    /// Registers <c>DELETE /api/v1/spaces/{spaceId}/invitations/{invitationId}</c>: the revoke (story 4.2, AC2, AC4, open
    /// question 1 (a)).
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapRevokeInvitation(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapDelete(InvitationRoutes.Invitation, RevokeAsync)
            .RequiresCapability(Capability.IssueAndRevokeInvitations);

    /// <summary>
    /// Lists the active Space's actionable pending Invitations, newest first.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>200</c> with the rows, or <c>[]</c>.</returns>
    public static async Task<IResult> ListAsync(ListInvitationsHandler handler, CancellationToken cancellationToken)
    {
        var invitations = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(invitations.Select(Response).ToList());
    }

    /// <summary>
    /// Revokes a pending Invitation in the active Space. Any Owner or Admin may, whoever issued it (FR-12).
    /// </summary>
    /// <param name="invitationId">The Invitation, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>204</c>; <c>409 invitation-not-pending</c> for an accepted, revoked or expired Invitation, naming no address; or
    /// the boundary's <c>404 not-available</c> - byte for byte, and not recorded - for an id that names nothing in the active
    /// Space, another Space's included (open question 3 (a)).
    /// </returns>
    public static async Task<IResult> RevokeAsync(Guid invitationId, RevokeInvitationHandler handler, CancellationToken cancellationToken)
    {
        var revocation = await handler.HandleAsync(invitationId, cancellationToken).ConfigureAwait(false);

        return revocation switch
        {
            InvitationRevocation.Revoked => Results.NoContent(),

            InvitationRevocation.NotPending => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: ProblemTypes.InvitationNotPending,
                title: "This Invitation is no longer pending."),

            // Not recorded: an in-Space not-found crossed no boundary (AD-20). The body is the boundary's.
            InvitationRevocation.NotFound => BoundaryProblems.NotAvailable(),

            // A new outcome must be answered here, never fall into the boundary's 404 (story 4.1 code review P10).
            _ => throw new InvalidOperationException($"Unanswered Invitation revocation outcome {revocation}."),
        };
    }

    /// <summary>
    /// Issues an Invitation into the active Space.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request: its services hold the resolved context, and its body is read here.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>204</c>; <c>400 invalid-email-address</c> or <c>400 role-not-offerable</c>; <c>409 already-a-member</c> or
    /// <c>409 invitation-pending</c>; or the boundary's <c>404</c> when the Space was deleted under the request.
    /// </returns>
    public static async Task<IResult> IssueAsync(
        IssueInvitationHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var request = await JsonRequestBody.ReadAsync(httpContext.Request, typeof(IssueInvitationRequest), cancellationToken)
            .ConfigureAwait(false) as IssueInvitationRequest;

        if (!EmailAddressRule.IsPlausible(request?.EmailAddress))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: ProblemTypes.InvalidEmailAddress,
                title: "The email address is not one an email can be sent to.");
        }

        if (request!.Role is not { } code || !OfferableRoles.TryGetValue(code, out var role))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: ProblemTypes.RoleNotOfferable,
                title: "An Invitation offers Admin, Member or Viewer.");
        }

        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();

        var result = await handler.HandleAsync(context, request.EmailAddress!, role, cancellationToken).ConfigureAwait(false);

        return result.Outcome switch
        {
            InvitationIssueOutcome.Issued => Results.NoContent(),

            InvitationIssueOutcome.AlreadyAMember => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: ProblemTypes.AlreadyAMember,
                title: "This address already belongs to a Member of this Space."),

            InvitationIssueOutcome.AlreadyPending => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: ProblemTypes.InvitationPending,
                title: "This address already has a pending Invitation to this Space."),

            // The Space was deleted after the pipeline resolved it (story 3.3). The failure rolls the unit of work back.
            null => BoundaryProblems.NotAvailable(),

            // A new outcome must be answered here, never fall into the boundary's 404 (story 4.1 code review).
            _ => throw new InvalidOperationException($"Unanswered Invitation issue outcome {result.Outcome}."),
        };
    }

    private static InvitationResponse Response(PendingInvitation invitation) =>
        new(invitation.Id, invitation.EmailAddress, StableCode.Of(invitation.Role), invitation.IssuerName, invitation.IssuedAt);
}
