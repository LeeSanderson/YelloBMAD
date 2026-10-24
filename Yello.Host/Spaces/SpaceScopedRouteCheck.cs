namespace Yello.Host.Spaces;

/// <summary>
/// The Host's refusal to start with a Space-scoped route the pipeline would not resolve. AR-9;
/// AD-3.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it exists.</b> Space resolution is attached to the <c>api</c> route group. A
/// <c>{spaceId}</c> route mapped directly on the application skips it entirely - no Membership read,
/// no session context, no capability check - and answers whatever its handler returns, which for a
/// read is an empty <c>200</c> because row-level security fails closed. Nothing about that looks
/// broken. So every <c>{spaceId}</c> route must carry the marker the group stamps on its endpoints,
/// and exactly one <see cref="RequiredCapability"/>, or the Host does not start.
/// </para>
/// <para>
/// Checked against the real route table, at startup, after every route is mapped - so a route added
/// anywhere, by any means, is covered.
/// </para>
/// </remarks>
internal static class SpaceScopedRouteCheck
{
    /// <summary>
    /// Every problem with the Space-scoped routes among <paramref name="endpoints"/>.
    /// </summary>
    /// <param name="endpoints">The route table.</param>
    /// <returns>One line per problem; empty when every Space-scoped route is resolved and declared.</returns>
    internal static IReadOnlyList<string> ProblemsIn(IEnumerable<Endpoint> endpoints) =>
        [.. endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => ActiveSpaceResolution.CarriesSpaceId(endpoint.RoutePattern))
            .SelectMany(ProblemsWith)];

    /// <summary>
    /// Throws when any Space-scoped route in the application is not resolved by the pipeline or does
    /// not declare exactly one capability.
    /// </summary>
    /// <param name="routeBuilder">The application's route builder, with every route mapped.</param>
    /// <exception cref="InvalidOperationException">A Space-scoped route is unprotected.</exception>
    internal static void EnsureEveryRouteIsResolved(IEndpointRouteBuilder routeBuilder)
    {
        ArgumentNullException.ThrowIfNull(routeBuilder);

        var problems = ProblemsIn(routeBuilder.DataSources.SelectMany(source => source.Endpoints));

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "Space-scoped routes the pipeline would not protect:" + Environment.NewLine
                + string.Join(Environment.NewLine, problems));
        }
    }

    private static IEnumerable<string> ProblemsWith(RouteEndpoint endpoint)
    {
        var route = endpoint.RoutePattern.RawText;

        if (endpoint.Metadata.GetMetadata<ResolvedBySpacePipeline>() is null)
        {
            yield return $"{route} is not mapped through the route group that resolves the active Space.";
        }

        var declared = endpoint.Metadata.GetOrderedMetadata<RequiredCapability>().Count;

        if (declared != 1)
        {
            yield return $"{route} declares {declared} required capabilities; it must declare exactly one.";
        }
    }
}

/// <summary>
/// The marker the resolving route group stamps on every endpoint mapped through it.
/// </summary>
internal sealed class ResolvedBySpacePipeline
{
    /// <summary>
    /// The one instance.
    /// </summary>
    internal static ResolvedBySpacePipeline Instance { get; } = new();

    private ResolvedBySpacePipeline()
    {
    }
}
