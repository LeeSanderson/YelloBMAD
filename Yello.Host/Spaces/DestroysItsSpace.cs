namespace Yello.Host.Spaces;

/// <summary>
/// Endpoint metadata: the endpoint's effect destroys the Space it was resolved in, and with it the scope its
/// <c>Idempotency-Key</c> would be recorded in. Story 3.3, open question 1 (a); AD-18's recorded variance.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read by the pipeline's idempotency step.</b> On a route carrying it, a key is still validated (a malformed one is
/// <c>400</c>) and still looked up, but a success is <b>not recorded</b>: the record's foreign key to <c>Space</c> would
/// refuse the insert (547) once the Space is gone, and the cascade would take the record anyway. A retry after a lost
/// answer re-enters the pipeline: if the first attempt committed, the retry finds no Membership and is the boundary's
/// <c>404</c>; if it did not, the retry deletes. Either way nothing applies twice (NFR-5).
/// </para>
/// <para>
/// <b>One route carries it</b> - <c>DELETE /api/v1/spaces/{spaceId}</c> - and
/// <c>RefusalBoundaryGateTests.Only_the_deletion_route_carries_the_marker_that_it_destroys_its_own_Space</c> holds that,
/// because on any other route it would silently drop the replay protection AD-18 promises.
/// </para>
/// </remarks>
public sealed class DestroysItsSpace
{
    /// <summary>
    /// The one instance; the marker carries nothing.
    /// </summary>
    internal static DestroysItsSpace Instance { get; } = new();

    private DestroysItsSpace()
    {
    }
}

/// <summary>
/// Declares <see cref="DestroysItsSpace"/> on an endpoint.
/// </summary>
public static class DestroysItsSpaceExtensions
{
    /// <summary>
    /// Declares that the endpoint's effect destroys the Space it was resolved in.
    /// </summary>
    /// <param name="builder">The endpoint.</param>
    /// <returns>The same endpoint, for chaining.</returns>
    public static RouteHandlerBuilder DestroysItsSpace(this RouteHandlerBuilder builder) =>
        builder.WithMetadata(Spaces.DestroysItsSpace.Instance);
}
