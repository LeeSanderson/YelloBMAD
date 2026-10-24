using Yello.Application.Statuses.ListStatuses;
using Yello.Contracts.Spaces;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// <c>GET /api/v1/spaces/{spaceId}/statuses</c> - the product's first Space-scoped endpoint.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no Membership check here, and there must never be one.</b> The route carries
/// <c>{spaceId}</c>, so <c>ActiveSpaceResolution</c> has already resolved the Space, scoped the
/// connection to it and refused the request if the caller is not a member, before this method is
/// called. A check written here as well would be the slice re-implementing Space resolution that
/// AR-3 names as a defect.
/// </para>
/// <para>
/// <b>It does not take the <c>spaceId</c> it is routed on.</b> The Space is the pipeline's answer,
/// not the endpoint's input, and a parameter here would be the first place a later edit could pass
/// the route value somewhere the resolved context should have gone.
/// </para>
/// </remarks>
public static class SpaceStatusEndpoints
{
    /// <summary>
    /// Registers <c>GET /api/v1/spaces/{spaceId}/statuses</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    /// <remarks>
    /// Declares <see cref="Capability.ReadProjectsAndTasks"/>, which every Role holds: Statuses are
    /// the columns Tasks are read in. The pipeline checks it; this endpoint never sees a Role.
    /// </remarks>
    public static RouteHandlerBuilder MapListStatuses(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(SpaceRoutes.Statuses, ListAsync)
            .RequiresCapability(Capability.ReadProjectsAndTasks);

    /// <summary>
    /// Lists the active Space's Statuses.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>200</c> with the Statuses in Board order.</returns>
    public static async Task<IResult> ListAsync(
        ListStatusesHandler handler,
        CancellationToken cancellationToken)
    {
        var statuses = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(statuses
            .Select(status => new StatusResponse(status.Id, status.Name, status.Position))
            .ToList());
    }
}
