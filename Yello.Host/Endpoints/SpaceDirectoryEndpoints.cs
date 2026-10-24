using Yello.Application.Spaces.ListSpaces;
using Yello.Contracts.Spaces;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// <c>GET /api/v1/spaces</c> - the Space switcher, the first of AD-24's two Account-scoped surfaces.
/// FR-9; AD-24 / AR-29; I-11.
/// </summary>
/// <remarks>
/// <para>
/// <b>Space identity and nothing else.</b> Each row is an id and a name: no count, no Role, no
/// badge (story 1.7, AC2). The read beneath it runs under the <c>AccountId</c> session key, which
/// widens <c>Membership</c> alone, and reaches <c>Space</c> only through a join from it - so no
/// Project, Task, Status, presence or refusal record is reachable from here, and
/// <c>RefusalBoundaryGateTests.The_switcher_endpoint_reaches_no_Space_scoped_content</c> walks the IL
/// to hold that.
/// </para>
/// <para>
/// <b>Never a refusal surface.</b> An Account holding no Membership is answered <c>200 []</c>, and
/// nothing is recorded, ever: the only refusal here is the authentication boundary's <c>401</c>,
/// which the pipeline gives before this runs.
/// </para>
/// </remarks>
public static class SpaceDirectoryEndpoints
{
    /// <summary>
    /// Registers <c>GET /api/v1/spaces</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapListSpaces(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(SpaceRoutes.Directory, ListAsync)
            .AccountScoped(AccountScopedSurface.SpaceSwitcher);

    /// <summary>
    /// Lists the caller's Spaces.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="request">The Account-scoped context the pipeline established.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>200</c> with the Spaces, ordered by name then id.</returns>
    public static async Task<IResult> ListAsync(
        ListSpacesHandler handler,
        AccountScopedRequest request,
        CancellationToken cancellationToken)
    {
        var spaces = await handler.HandleAsync(request.Context, cancellationToken).ConfigureAwait(false);

        return Results.Ok(spaces
            .Select(space => new SpaceIdentityResponse(space.Id, space.Name))
            .ToList());
    }
}
