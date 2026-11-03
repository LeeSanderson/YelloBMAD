using Yello.Application.Memberships;
using Yello.Application.Memberships.ChangeMembershipRole;
using Yello.Application.Memberships.ListMemberships;
using Yello.Application.Spaces;
using Yello.Contracts;
using Yello.Contracts.Memberships;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// List the active Space's Memberships (story 4.4, AC1, AC2, AC7) and change one's Role (AC4, AC5, AC6). FR-13, UX-DR14,
/// UX-DR34; Lee's Q1 (a), Q4 (a), Q5 (a); Q8-Q11 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>No Membership check, no Role, no bound count, no idempotency and no refusal record here, and there must never be
/// any</b> - the reasons <see cref="ProjectEndpoints"/> gives (AR-3). The routes carry <c>{spaceId}</c>, so the pipeline has
/// resolved the Space, checked <c>ChangeMembershipRole</c> (Owner Allowed, Admin Qualified; a Member or Viewer is its
/// <c>403</c>, a non-member its boundary <c>404</c>), replayed a key it had already seen and set the acting authority before
/// this code runs. Both routes sit under that one capability - the list carries email addresses, which section 6.1 and
/// AD-23 confine to Owners and Admins (Lee's Q4 (a)) - and both serve both surfaces (AD-4).
/// </para>
/// <para>
/// <b>A change beyond the acting authority is the pipeline's to answer</b> (AD-20): the endpoint hands back
/// <see cref="QualifiedRefusal"/>, and the pipeline records it and answers the capability-shaped <c>403</c>.
/// </para>
/// </remarks>
public static class MembershipEndpoints
{
    /// <summary>
    /// Registers <c>GET /api/v1/spaces/{spaceId}/memberships</c>: one page of the Space's Memberships (AC1).
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapListMemberships(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(MembershipRoutes.List, ListAsync)
            .RequiresCapability(Capability.ChangeMembershipRole);

    /// <summary>
    /// Registers <c>PATCH /api/v1/spaces/{spaceId}/memberships/{membershipId}</c>: the Role change (AC4).
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapChangeMembershipRole(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPatch(MembershipRoutes.Membership, ChangeRoleAsync)
            .RequiresCapability(Capability.ChangeMembershipRole);

    /// <summary>
    /// Lists one page of the active Space's Memberships, in join order.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request: its services hold the resolved context, and its query the cursor.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>200</c> with the page, or <c>400 invalid-list-cursor</c> for a cursor this route did not issue.</returns>
    public static async Task<IResult> ListAsync(
        ListMembershipsHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        MembershipCursor? after = null;

        if (httpContext.Request.Query.TryGetValue(MembershipRoutes.AfterParameter, out var sent)
            && (sent.Count != 1 || !MembershipCursorText.TryRead(sent.ToString(), out after)))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: ProblemTypes.InvalidListCursor,
                title: "The cursor is not one this list issued.");
        }

        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();

        var (rows, total, nextAfter) = await handler.HandleAsync(context.AccountId, after, cancellationToken).ConfigureAwait(false);

        return Results.Ok(new MembershipPageResponse(total, rows.Select(ListResponse).ToList(), nextAfter));
    }

    /// <summary>
    /// Changes a Membership's Role in the active Space, within the caller's authority.
    /// </summary>
    /// <param name="membershipId">The Membership, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request: its services hold the resolved context, and its body is read here.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>200</c> with the row (changed, or unchanged when it already held the Role); <c>400 role-not-assignable</c>;
    /// <c>409 owner-role-fixed</c>; the pipeline's recorded <c>403</c> beyond the caller's authority; or the boundary's
    /// <c>404 not-available</c> - byte for byte, and not recorded - for an id that names nothing in this Space.
    /// </returns>
    public static async Task<IResult> ChangeRoleAsync(
        Guid membershipId,
        ChangeMembershipRoleHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var request = await JsonRequestBody.ReadAsync(httpContext.Request, typeof(ChangeMembershipRoleRequest), cancellationToken)
            .ConfigureAwait(false) as ChangeMembershipRoleRequest;

        // Before anything is read (Q10 (a)): owner, an unknown code or none.
        if (!AssignableRoles.TryParse(request?.Role, out var proposed))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: ProblemTypes.RoleNotAssignable,
                title: "A Role change assigns Admin, Member or Viewer.");
        }

        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();

        var result = await handler.HandleAsync(context.AccountId, membershipId, proposed, cancellationToken).ConfigureAwait(false);

        return result.Outcome switch
        {
            RoleChangeOutcome.Changed or RoleChangeOutcome.Unchanged => Results.Ok(ChangeResponse(result.Row!)),

            RoleChangeOutcome.OwnerRoleFixed => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: ProblemTypes.OwnerRoleFixed,
                title: "Ownership moves only by an Ownership Offer."),

            // The pipeline records it and answers the 403 (AD-20).
            RoleChangeOutcome.BeyondAuthority => new QualifiedRefusal(Capability.ChangeMembershipRole),

            // Not recorded: an in-Space not-found crossed no boundary (AD-20). The body is the boundary's.
            RoleChangeOutcome.NotFound => BoundaryProblems.NotAvailable(),

            // A new outcome must be answered here, never fall into the boundary's 404 (story 4.1 code review P10).
            _ => throw new InvalidOperationException($"Unanswered Role change outcome {result.Outcome}."),
        };
    }

    private static MembershipResponse ListResponse(ListedMembership membership) =>
        new(membership.Id, membership.DisplayName, membership.EmailAddress, membership.RoleCode, membership.IsYou);

    // No address: the pipeline records this answer whole, and no idempotency record may hold one (AD-23; code review
    // 2026-11-03, decided by Lee, option (b)).
    private static MembershipRoleChangeResponse ChangeResponse(ChangedMembership membership) =>
        new(membership.Id, membership.DisplayName, membership.RoleCode, membership.IsYou);
}
