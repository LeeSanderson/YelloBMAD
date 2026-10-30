using Yello.Application.Spaces;
using Yello.Domain.Bounds;

namespace Yello.Host.Spaces;

/// <summary>
/// The creation bound step of the Space boundary pipeline. AD-25 / AR-30; story 2.1, Task 6; story 2.2,
/// Task 5.
/// </summary>
internal static partial class ActiveSpaceResolution
{
    /// <summary>
    /// The creation bound an endpoint declares, consulted inside the unit of work before the endpoint runs:
    /// the limit from <see cref="BoundRegistry"/>, the count - within the route's scope for a scoped bound -
    /// under a lock held to the unit's end.
    /// </summary>
    /// <remarks>
    /// Nested, so it stays the pipeline's own step for every gate that asks who reads the bound port or
    /// the registry (AR-3), while the resolution stays inside S1200's coupling limit.
    /// </remarks>
    private static class CreationBound
    {
        /// <summary>
        /// Consults the endpoint's declared bound.
        /// </summary>
        /// <param name="httpContext">The request.</param>
        /// <param name="logger">The resolution's logger.</param>
        /// <param name="spaceId">
        /// The resolved Space, or <c>null</c> on the Space creation unit of work (story 3.1), where the bound is the
        /// Account's own and no Space exists yet to log.
        /// </param>
        /// <param name="cancellationToken">The request's cancellation token.</param>
        /// <returns>The <c>409</c> when the Space, scope or Account is already at the limit; otherwise <c>null</c>.</returns>
        /// <exception cref="InvalidOperationException">
        /// A scoped bound is declared on a route that does not carry its scope - which
        /// <see cref="SpaceScopedRouteCheck"/> refuses at startup, so this fails closed only if that check
        /// is bypassed.
        /// </exception>
        internal static async Task<IResult?> ConsultAsync(
            HttpContext httpContext,
            ILogger logger,
            Guid? spaceId,
            CancellationToken cancellationToken)
        {
            if (RequiredBoundExtensions.Of(httpContext.GetEndpoint()) is not { } declared)
            {
                return null;
            }

            var bound = BoundRegistry.For(declared);
            var scopeId = ScopeOf(httpContext, bound.Bound);

            var count = await httpContext.RequestServices.GetRequiredService<IBoundCounter>()
                .CountAsync(bound.Bound, scopeId, cancellationToken)
                .ConfigureAwait(false);

            if (count < bound.Limit)
            {
                return null;
            }

            // Inside the creation's transaction, which the caller disposes without completing. Not an
            // AccessRefusal: AD-20 records two kinds, and this is neither.
            if (spaceId is { } resolved)
            {
                SpaceLog.BoundReachedFor(logger, resolved, bound);
            }
            else
            {
                SpaceLog.CreationBoundReachedFor(logger, bound);
            }

            return BoundaryProblems.BoundReached(bound);
        }

        /// <summary>
        /// The scope a bound is counted within, read from the route - never from the body, never by a
        /// slice.
        /// </summary>
        /// <returns>The scope's id, or <c>null</c> for a bound counted across the Space.</returns>
        private static Guid? ScopeOf(HttpContext httpContext, Bound bound)
        {
            if (RequiredBoundExtensions.ScopeParameter(bound) is not { } parameter)
            {
                return null;
            }

            // The route constrains the parameter to a guid, so anything else is a mis-declared route.
            return httpContext.GetRouteValue(parameter) is string value && Guid.TryParse(value, out var scopeId)
                ? scopeId
                : throw new InvalidOperationException($"The {bound} bound is counted within {{{parameter}}}, which this route does not carry.");
        }
    }
}
