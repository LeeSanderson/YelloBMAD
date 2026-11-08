using Yello.Application.Memberships.ListAssignees;
using Yello.Contracts.Memberships;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// List the active Space's Assignee candidates: every Membership as its id and display name.
/// </summary>
/// <remarks>
/// <b>Under the read capability every Role holds</b>: assigners need it for the picker, and every Role for the Assignees on
/// the cards and in the List View's filter. It carries no address and no Role, so reading it discloses nothing the Membership
/// list confines to Owners and Admins (AD-23). No Membership check, no Role and no record here (AR-3); both surfaces (AD-4).
/// </remarks>
public static class AssigneeEndpoints
{
    /// <summary>
    /// Registers <c>GET /api/v1/spaces/{spaceId}/assignees</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapListAssignees(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(AssigneeRoutes.List, ListAsync)
            .RequiresCapability(Capability.ReadProjectsAndTasks);

    /// <summary>
    /// Lists the candidates, in join order.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>200</c> with every Membership of the Space.</returns>
    public static async Task<IResult> ListAsync(ListAssigneesHandler handler, CancellationToken cancellationToken)
    {
        var candidates = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(new AssigneeListResponse([.. candidates.Select(candidate => new AssigneeResponse(candidate.MembershipId, candidate.DisplayName))]));
    }
}
