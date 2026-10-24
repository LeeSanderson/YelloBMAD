using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// The answer to an <c>/api</c> path that matches no route: the same <c>not-available</c> problem a
/// Space-boundary refusal gives. AD-3; story 1.6, decision 8.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it is needed.</b> <c>{spaceId}</c> carries a <c>guid</c> constraint, so
/// <c>/api/v1/spaces/not-a-guid/statuses</c> matches no route, and routing's own answer is an empty
/// bare <c>404</c> - a different body from the boundary's. That difference tells a caller nothing
/// about any Space, but it is a second not-found shape, and AC1 asks for one.
/// </para>
/// <para>
/// <b>Nothing is recorded.</b> No Space boundary was reached, so this is not an
/// <c>AccessRefusal</c>.
/// </para>
/// </remarks>
public static class UnmatchedApiEndpoint
{
    /// <summary>
    /// Registers the fallback for every unmatched path under <c>/api</c>, for every method.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered fallback.</returns>
    public static RouteHandlerBuilder MapUnmatchedApi(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapFallback("/api/{**path}", Answer);

    /// <summary>
    /// The not-available problem.
    /// </summary>
    /// <returns><c>404</c>, byte-identical to a Space-boundary refusal.</returns>
    public static IResult Answer() => BoundaryProblems.NotAvailable();
}
