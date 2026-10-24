using Microsoft.AspNetCore.Routing.Patterns;
using Yello.Application.Spaces;
using Yello.Contracts;
using Yello.Contracts.Spaces;

namespace Yello.Host.Spaces;

/// <summary>
/// Space resolution, as the pipeline behaviour AR-3 requires it to be: the one place a request's
/// <see cref="ActiveSpaceContext"/> is established, before anything else about the request is
/// decided. AD-1, AD-2 / AR-5, AR-6; FR-15.
/// </summary>
/// <remarks>
/// <para>
/// <b>Attached to the route group, and matched by the shape of the route.</b> Every endpoint whose
/// route pattern carries <c>{spaceId}</c> is resolved, because of what it is rather than because a
/// list names it - the same rule the anti-forgery filter beside it follows, for the reason recorded
/// there: a list of protected routes rots quietly, because forgetting to add to it looks like
/// nothing at all.
/// </para>
/// <para>
/// <b>Three answers, in this order.</b> No authenticated Account: <c>401</c>, at the authentication
/// boundary, before resolution begins - a caller with no Session never learns anything about a
/// Space. No Membership in the named Space, or no such Space: one <c>404</c> for both, never a
/// <c>403</c> and never distinguished (AD-3). Otherwise the endpoint runs inside the unit of work
/// the resolution opened. That unit commits only once the endpoint has returned, only when it
/// returned a success, and only when what it returned has already been read.
/// </para>
/// <para>
/// <b>What story 1.6 hangs on this.</b> <see cref="Refuse"/> is the single refusal point, so the
/// <c>AccessRefusal</c> record and the refusal's full contract attach here rather than being
/// re-derived per endpoint. It is also the natural counting seam for NFR-8's "50 concurrent active
/// Sessions per Space" (<c>deferred-work.md:127</c>): every active Space resolution passes through
/// <see cref="ResolveCoreAsync"/>.
/// </para>
/// </remarks>
internal static class ActiveSpaceResolution
{
    /// <summary>
    /// The logger category, named for this type because a static class cannot be <c>T</c> in
    /// <c>ILogger&lt;T&gt;</c>.
    /// </summary>
    private static readonly string LoggerCategory = typeof(ActiveSpaceResolution).FullName!;

    /// <summary>
    /// Resolves the active Space for a Space-scoped endpoint, and passes every other one straight
    /// through. Registered once on the route group in the composition root.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <returns>The refusal, or whatever the endpoint returns.</returns>
    internal static ValueTask<object?> ResolveAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Not async, so S4457's argument check runs eagerly - the same split AntiforgeryRequirement
        // makes. An endpoint with no {spaceId} never enters the resolving path.
        return IsSpaceScoped(context.HttpContext)
            ? ResolveCoreAsync(context, next)
            : next(context);
    }

    /// <summary>
    /// Whether the endpoint serving this request is Space-scoped - decided from its route pattern.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <returns><c>true</c> when the route carries <c>{spaceId}</c>.</returns>
    internal static bool IsSpaceScoped(HttpContext httpContext) =>
        httpContext.GetEndpoint() is RouteEndpoint { RoutePattern: var pattern }
        && CarriesSpaceId(pattern);

    private static bool CarriesSpaceId(RoutePattern pattern) =>
        pattern.GetParameter(SpaceRoutes.SpaceIdParameter) is not null;

    private static async ValueTask<object?> ResolveCoreAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var services = httpContext.RequestServices;
        var cancellationToken = httpContext.RequestAborted;

        var established = services.GetRequiredService<ISpaceRequestEstablishment>().Establish(httpContext);

        if (established.AccountId is not { } accountId)
        {
            // The authentication boundary, and the same answer GET /api/v1/sessions/current gives.
            // Nothing about the named Space is read on this path.
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                type: ProblemTypes.AuthenticationFailed,
                title: "Authentication did not succeed.");
        }

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory);

        if (established.SpaceId is not { } spaceId)
        {
            // Unreachable through the guid-constrained routes in SpaceRoutes, and answered as a
            // refusal anyway: failing closed costs nothing, and a later route without the
            // constraint must not turn an unparseable segment into a default.
            return Refuse();
        }

        var unitOfWork = await services.GetRequiredService<IActiveSpaceResolver>()
            .BeginAsync(accountId, spaceId, cancellationToken)
            .ConfigureAwait(false);

        if (unitOfWork is null)
        {
            SpaceLog.SpaceNotResolved(logger, spaceId);
            return Refuse();
        }

        await using (unitOfWork.ConfigureAwait(false))
        {
            SpaceLog.SpaceResolved(logger, spaceId);

            var result = await next(context).ConfigureAwait(false);

            EnsureMaterialised(result);

            if (IsFailure(result))
            {
                // Disposed without completing, so it rolls back: an endpoint that wrote and then
                // answered with a problem leaves nothing behind, exactly as one that threw does.
                return result;
            }

            // Not the request's token. The endpoint has finished, so a client that disconnects now
            // must not decide whether its completed work is kept.
            await unitOfWork.CompleteAsync(CancellationToken.None).ConfigureAwait(false);

            return result;
        }
    }

    /// <summary>
    /// Whether the endpoint answered with an error, which the unit of work must not commit.
    /// </summary>
    /// <param name="result">What the endpoint returned.</param>
    /// <returns><c>true</c> for a <c>4xx</c> or <c>5xx</c> result.</returns>
    internal static bool IsFailure(object? result) =>
        result is IStatusCodeHttpResult { StatusCode: >= StatusCodes.Status400BadRequest };

    /// <summary>
    /// Refuses a result that would do its work after the unit of work has closed.
    /// </summary>
    /// <remarks>
    /// ASP.NET Core executes what an endpoint returns only after this filter has returned, and by then
    /// the transaction is committed and disposed. A query that runs at that point runs on a reopened
    /// connection with no session context, where row-level security fails closed and answers with
    /// nothing, silently. So a Space-scoped endpoint returns data it has already read; one that
    /// returns an <see cref="IQueryable"/> or an <see cref="IAsyncEnumerable{T}"/> fails loudly here,
    /// inside the unit of work, and is rolled back.
    /// </remarks>
    /// <param name="result">What the endpoint returned.</param>
    /// <exception cref="InvalidOperationException">The result is not materialised.</exception>
    internal static void EnsureMaterialised(object? result)
    {
        var value = result is IValueHttpResult { Value: var carried } ? carried : result;

        if (value is IQueryable || IsAsyncEnumerable(value))
        {
            throw new InvalidOperationException(
                $"A Space-scoped endpoint returned {value!.GetType().Name}, which would be enumerated after "
                + "its unit of work has closed. Materialise it before returning.");
        }
    }

    private static bool IsAsyncEnumerable(object? value) =>
        value is not null
        && Array.Exists(value.GetType().GetInterfaces(), type =>
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));

    /// <summary>
    /// The one refusal for a Space this request cannot act in.
    /// </summary>
    /// <returns>A <c>404</c> problem response.</returns>
    private static IResult Refuse() =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            type: ProblemTypes.SpaceNotFound,
            title: "The Space was not found.");
}
