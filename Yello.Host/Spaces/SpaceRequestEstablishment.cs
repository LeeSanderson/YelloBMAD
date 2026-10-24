using Yello.Contracts.Spaces;
using Yello.Host.ApiTokens;
using Yello.Host.Sessions;

namespace Yello.Host.Spaces;

/// <summary>
/// <see cref="ISpaceRequestEstablishment"/>, for both surfaces: the Account from the Session ticket
/// or from the API Token's row, the Space from the <c>{spaceId}</c> route segment either way. AD-4;
/// story 1.8, open question 2.
/// </summary>
/// <remarks>
/// <para>
/// <b>The one step the two surfaces do differently</b>, and the only reader of either principal on
/// the Space-scoped path. A Session gives its single claim (story 1.4's AC1). A Token gives the
/// Account it was issued by and the Space it is bound to - both read off its row this request - and
/// the bound Space travels beside the route's Space as <see cref="EstablishedSpaceRequest.BoundSpaceId"/>,
/// a claim for <see cref="ActiveSpaceResolution"/> to verify rather than a grant.
/// </para>
/// <para>
/// <b>Synchronous and allocation-cheap</b>: it runs twice per request.
/// </para>
/// </remarks>
internal sealed class SpaceRequestEstablishment : ISpaceRequestEstablishment
{
    /// <inheritdoc />
    public EstablishedSpaceRequest Establish(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var spaceId = RouteSpaceOf(httpContext);

        return ApiTokenPrincipal.Of(httpContext.User) is { } token
            ? new EstablishedSpaceRequest(token.AccountId, spaceId, token.BoundSpaceId)
            : new EstablishedSpaceRequest(SessionPrincipal.AccountIdOf(httpContext.User), spaceId);
    }

    private static Guid? RouteSpaceOf(HttpContext httpContext) =>
        httpContext.Request.RouteValues.TryGetValue(SpaceRoutes.SpaceIdParameter, out var value)
        && Guid.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), out var parsed)
            ? parsed
            : null;
}
