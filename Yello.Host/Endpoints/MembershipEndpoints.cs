using Yello.Application.Memberships;
using Yello.Application.Memberships.ReadMembership;
using Yello.Application.Memberships.ListMemberships;
using Yello.Application.Spaces;
using Yello.Contracts;
using Yello.Contracts.Memberships;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// List the active Space's Memberships and read one. FR-13, UX-DR14, UX-DR34, UX-DR36. The Role change is
/// <see cref="MembershipRoleChangeEndpoints"/>; the removal and the leave are <see cref="MembershipRemovalEndpoints"/> - three
/// classes only to keep each inside its dependency budget (S1200).
/// </summary>
/// <remarks>
/// <para>
/// <b>No Membership check, no Role, no bound count, no idempotency and no refusal record here, and there must never be
/// any</b> - the reasons <see cref="ProjectEndpoints"/> gives (AR-3). The routes carry <c>{spaceId}</c>, so the pipeline has
/// resolved the Space, checked <c>ChangeMembershipRole</c> (Owner Allowed, Admin Qualified; a Member or Viewer is its
/// <c>403</c>, a non-member its boundary <c>404</c>), replayed a key it had already seen and set the acting authority before
/// this code runs. Both routes sit under that one capability - the rows carry email addresses, which section 6.1 and
/// AD-23 confine to Owners and Admins (Lee's Q4 (a)) - and both serve both surfaces (AD-4). The single row is what the
/// removal confirm states, its presence read fresh when the confirm opens.
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
    /// Registers <c>GET /api/v1/spaces/{spaceId}/memberships/{membershipId}</c>: one row, with its presence read fresh.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapReadMembership(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(MembershipRoutes.Membership, ReadAsync)
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
    /// Reads one Membership of the active Space with its presence.
    /// </summary>
    /// <param name="membershipId">The Membership, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request: its services hold the resolved context.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>200</c> with the row, or the boundary's <c>404 not-available</c> - byte for byte, and not recorded - for an id that
    /// names nothing in this Space.
    /// </returns>
    public static async Task<IResult> ReadAsync(
        Guid membershipId,
        ReadMembershipHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();

        var row = await handler.HandleAsync(context.AccountId, membershipId, cancellationToken).ConfigureAwait(false);

        // Not recorded: an in-Space not-found crossed no boundary (AD-20). The body is the boundary's.
        return row is null ? BoundaryProblems.NotAvailable() : Results.Ok(ListResponse(row));
    }

    /// <summary>
    /// A Membership as the list and the single-row read answer it.
    /// </summary>
    /// <param name="membership">The row the store read.</param>
    /// <returns>The response row.</returns>
    internal static MembershipResponse ListResponse(ListedMembership membership) =>
        new(membership.Id, membership.DisplayName, membership.EmailAddress, membership.RoleCode, membership.IsYou, membership.InSpaceNow);
}
