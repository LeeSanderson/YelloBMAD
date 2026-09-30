using Yello.Contracts.Spaces;
using Yello.Host.Sessions;

namespace Yello.Host.Spaces;

/// <summary>
/// The browser half of <see cref="ISpaceRequestEstablishment"/>: the Account from the Session
/// ticket, the Space from the <c>{spaceId}</c> route segment.
/// </summary>
/// <remarks>
/// <b>The Account comes from <see cref="SessionPrincipal.AccountIdOf"/> and nothing else</b> - no
/// second claim, no cookie re-parse. The Session carries one claim (story 1.4's AC1), which is why
/// the Space has to come from the route: there is nothing on the Session for it to come from.
/// </remarks>
internal sealed class SessionSpaceRequestEstablishment : ISpaceRequestEstablishment
{
    /// <inheritdoc />
    public EstablishedSpaceRequest Establish(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var accountId = SessionPrincipal.AccountIdOf(httpContext.User);

        Guid? spaceId = null;

        if (httpContext.Request.RouteValues.TryGetValue(SpaceRoutes.SpaceIdParameter, out var value)
            && Guid.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), out var parsed))
        {
            spaceId = parsed;
        }

        return new EstablishedSpaceRequest(accountId, spaceId);
    }
}
