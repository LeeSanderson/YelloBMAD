using Yello.Host.Sessions;
using Yello.Host.Spaces;

namespace Yello.Host;

/// <summary>
/// The route group every product endpoint is mapped through, with its filters in the order that is
/// the contract. AR-3, AR-14; AD-3.
/// </summary>
/// <remarks>
/// <para>
/// <b>Here rather than inline in <c>Program.cs</c></b> so the order can be asserted: top-level
/// statements cannot be called from a test, and an order nobody can test is one a tidy-up can
/// silently change.
/// </para>
/// <para>
/// <b>The order.</b> First, a Space-scoped request with no Session is answered <c>401</c> - the
/// authentication boundary, ahead of anti-forgery's <c>400</c>, which would describe a malformed
/// request rather than a missing Session (story 1.6, decision 11). Then an Account-scoped endpoint
/// (AD-24's two surfaces) gets the same <c>401</c> without a Session, or its
/// <c>AccountScopedContext</c> from the Session (story 1.7). Then anti-forgery, for every
/// state-changing request, so a forged change is refused before any Space is read. Then the
/// not-found floor (story 2.2, Q7), which times everything after it and holds a Space-scoped <c>404</c>
/// to a fixed minimum, so a boundary refusal and an in-Space not-found take the same time. Then Space
/// resolution, the capability check, the bound and the refusal record. The group also stamps every
/// endpoint it maps, which is how the Host knows at startup that no <c>{spaceId}</c> route was
/// mapped around it.
/// </para>
/// </remarks>
internal static class ApiRouteGroup
{
    /// <summary>
    /// Creates the group, with its filters and its stamp.
    /// </summary>
    /// <param name="routes">The application's route builder.</param>
    /// <returns>The group to map endpoints through.</returns>
    internal static RouteGroupBuilder MapApiGroup(this IEndpointRouteBuilder routes) =>
        routes.MapGroup(string.Empty)
            .WithMetadata(ResolvedBySpacePipeline.Instance)
            .AddEndpointFilter(ActiveSpaceResolution.RequireAccountAsync)
            .AddEndpointFilter(AccountScopedResolution.EstablishAsync)
            .AddEndpointFilter(AntiforgeryRequirement.ValidateStateChangingAsync)
            .AddEndpointFilter(NotAvailableFloor.HoldAsync)
            .AddEndpointFilter(ActiveSpaceResolution.ResolveAsync);
}
