using Yello.Application.Memberships;
using Yello.Application.Memberships.ChangeMembershipRole;
using Yello.Application.Spaces;
using Yello.Contracts;
using Yello.Contracts.Memberships;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// Change a Membership's Role in the active Space. FR-13, AR-12/AD-5, AR-16/AD-9.
/// </summary>
/// <remarks>
/// <para>
/// <b>Apart from <see cref="MembershipEndpoints"/> only to keep each class inside its dependency budget</b> (S1200); the
/// same rules hold. The pipeline resolved the Space, checked <c>ChangeMembershipRole</c> (Owner Allowed, Admin Qualified),
/// replayed a key it had already seen and set the acting authority before this code runs. Both surfaces (AD-4).
/// </para>
/// <para>
/// <b>A change beyond the acting authority is the pipeline's to answer</b> (AD-20): the endpoint hands back
/// <see cref="QualifiedRefusal"/>, and the pipeline records it and answers the capability-shaped <c>403</c>. An actor whose
/// own Membership went while it waited is <see cref="MembershipLost"/>, which the pipeline answers as the boundary's
/// recorded <c>404</c>.
/// </para>
/// </remarks>
public static class MembershipRoleChangeEndpoints
{
    /// <summary>
    /// Registers <c>PATCH /api/v1/spaces/{spaceId}/memberships/{membershipId}</c>: the Role change (AC4).
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapChangeMembershipRole(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPatch(MembershipRoutes.Membership, ChangeRoleAsync)
            .RequiresCapability(Capability.ChangeMembershipRole);

    /// <summary>
    /// Changes a Membership's Role in the active Space, within the caller's authority.
    /// </summary>
    /// <param name="membershipId">The Membership, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request: its services hold the resolved context, and its body is read here.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>200</c> with the row (changed, or unchanged when it already held the Role); <c>400 role-not-assignable</c>;
    /// <c>409 owner-role-fixed</c>; the pipeline's recorded <c>403</c> beyond the caller's authority, or its recorded
    /// boundary <c>404</c> when the caller's own Membership went first; or the boundary's <c>404 not-available</c> - byte for
    /// byte, and not recorded - for an id that names nothing in this Space.
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

            // The pipeline records it and answers the boundary's 404.
            RoleChangeOutcome.LostMembership => new MembershipLost(),

            // Not recorded: an in-Space not-found crossed no boundary (AD-20). The body is the boundary's.
            RoleChangeOutcome.NotFound => BoundaryProblems.NotAvailable(),

            // A new outcome must be answered here, never fall into the boundary's 404 (story 4.1 code review P10).
            _ => throw new InvalidOperationException($"Unanswered Role change outcome {result.Outcome}."),
        };
    }

    // No address: the pipeline records this answer whole, and no idempotency record may hold one (AD-23; code review
    // 2026-11-03, decided by Lee, option (b)).
    private static MembershipRoleChangeResponse ChangeResponse(ChangedMembership membership) =>
        new(membership.Id, membership.DisplayName, membership.RoleCode, membership.IsYou);
}
