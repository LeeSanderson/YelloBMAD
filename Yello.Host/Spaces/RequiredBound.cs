using Yello.Contracts.Projects;
using Yello.Domain.Bounds;

namespace Yello.Host.Spaces;

/// <summary>
/// Endpoint metadata: the NFR-8 bound a creation endpoint must consult. AD-25 / AR-30.
/// </summary>
/// <remarks>
/// <b>Declared by the endpoint, checked by the pipeline</b>, as <see cref="RequiredCapability"/> is.
/// <see cref="ActiveSpaceResolution"/> reads the limit from <see cref="BoundRegistry"/>, counts inside
/// the request's unit of work under a lock held to its end, and answers <c>409 bound-reached</c> before
/// the endpoint runs when the Space - or, for a scoped bound, the route's Project - is already at the
/// limit. A slice never counts.
/// </remarks>
/// <param name="Bound">The bound.</param>
public sealed record RequiredBound(Bound Bound);

/// <summary>
/// Declares an endpoint's <see cref="RequiredBound"/>.
/// </summary>
public static class RequiredBoundExtensions
{
    /// <summary>
    /// Declares the bound a creation endpoint consults.
    /// </summary>
    /// <param name="builder">The endpoint.</param>
    /// <param name="bound">The bound.</param>
    /// <returns>The same endpoint, for chaining.</returns>
    public static RouteHandlerBuilder ConsultsBound(this RouteHandlerBuilder builder, Bound bound) =>
        builder.WithMetadata(new RequiredBound(bound));

    /// <summary>
    /// The route parameter a bound is counted within, for a bound narrower than the Space: Tasks per
    /// Project is counted within the route's Project (story 2.2, Task 5).
    /// </summary>
    /// <param name="bound">The bound.</param>
    /// <returns>The parameter's name, or <c>null</c> for a bound counted across the Space.</returns>
    internal static string? ScopeParameter(Bound bound) =>
        bound == Bound.TasksPerProject ? ProjectRoutes.ProjectIdParameter : null;

    /// <summary>
    /// The bound an endpoint declares, if any - at most one; a creation consults the bound of what it
    /// creates.
    /// </summary>
    /// <param name="endpoint">The endpoint.</param>
    /// <returns>The declared bound, or <c>null</c>.</returns>
    internal static Bound? Of(Endpoint? endpoint) =>
        endpoint?.Metadata.GetMetadata<RequiredBound>()?.Bound;
}
