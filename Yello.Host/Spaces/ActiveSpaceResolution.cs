using Microsoft.AspNetCore.Routing.Patterns;
using Yello.Application.Spaces;
using Yello.Contracts.Spaces;
using Yello.Domain;
using Yello.Domain.AccessRefusals;
using Yello.Domain.Bounds;
using Yello.Domain.Memberships;

namespace Yello.Host.Spaces;

/// <summary>
/// Space resolution and the Space boundary, as the pipeline behaviour AR-3 requires them to be: the
/// one place a request's <see cref="ActiveSpaceContext"/> is established, its capability checked,
/// its bound consulted and its refusal recorded. AD-1, AD-2, AD-3, AD-20, AD-25; FR-15, FR-16.
/// </summary>
/// <remarks>
/// <para>
/// <b>Attached to the route group, and matched by the shape of the route.</b> Every endpoint whose
/// route pattern carries <c>{spaceId}</c> is resolved, because of what it is rather than because a
/// list names it - the same rule the anti-forgery filter beside it follows, for the reason recorded
/// there: a list of protected routes rots quietly, because forgetting to add to it looks like
/// nothing at all. The Host refuses to start with a <c>{spaceId}</c> route mapped anywhere else
/// (<see cref="SpaceScopedRouteCheck"/>).
/// </para>
/// <para>
/// <b>The answers, in this order.</b> No authenticated Account: <c>401</c>, at the authentication
/// boundary, before anything about a Space is read, and not recorded. No Membership in the named
/// Space, or no such Space: one <c>404</c> for both, never a <c>403</c>, never distinguished (AD-3),
/// and recorded as <c>CrossSpace</c>. A member whose Role the matrix denies the endpoint's declared
/// capability: <c>403</c>, capability-shaped, recorded as <c>InsufficientRole</c>. A member who
/// would exceed the Space's concurrent-presence bound: <c>409</c>, raised inside the resolution
/// transaction and not recorded. Otherwise the endpoint runs inside the unit of work the resolution
/// opened, which commits only once the endpoint has returned, only when it returned a success, and
/// only when what it returned has already been read.
/// </para>
/// <para>
/// <b>The two refusal points never convert into each other.</b> The <c>404</c> is produced only
/// where resolution finds no Membership, and the <c>403</c> only where the matrix denies a member;
/// nothing downstream rewrites either.
/// </para>
/// <para>
/// <b>Recorded synchronously, before the response, on a unit of work of its own.</b> Both
/// boundary-<c>404</c> causes therefore do identical work - resolve, find nothing, roll back,
/// record - which is what keeps them indistinguishable in duration as well as in body.
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
    /// Refuses a Space-scoped request that carries no authenticated Account, ahead of every other
    /// filter on the group, and passes every other request straight through.
    /// </summary>
    /// <remarks>
    /// Registered <i>before</i> the anti-forgery filter, so an unauthenticated state-changing
    /// request to a Space-scoped route is answered <c>401</c> - the authentication boundary - rather
    /// than with the anti-forgery <c>400</c> (story 1.6, decision 11). <see cref="ResolveAsync"/>
    /// keeps the same check, so neither filter depends on the other's presence.
    /// </remarks>
    /// <param name="context">The request.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <returns>The refusal, or whatever the rest of the pipeline returns.</returns>
    internal static ValueTask<object?> RequireAccountAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);

        var httpContext = context.HttpContext;

        if (IsSpaceScoped(httpContext)
            && httpContext.RequestServices.GetRequiredService<ISpaceRequestEstablishment>()
                .Establish(httpContext).AccountId is null)
        {
            return ValueTask.FromResult<object?>(BoundaryProblems.Unauthenticated());
        }

        return next(context);
    }

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

    /// <summary>
    /// Whether a route pattern carries <c>{spaceId}</c>.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns><c>true</c> when it does.</returns>
    internal static bool CarriesSpaceId(RoutePattern pattern) =>
        pattern.GetParameter(SpaceRoutes.SpaceIdParameter) is not null;

    private static async ValueTask<object?> ResolveCoreAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var services = httpContext.RequestServices;

        var established = services.GetRequiredService<ISpaceRequestEstablishment>().Establish(httpContext);

        if (established.AccountId is not { } accountId)
        {
            // The authentication boundary, and the same answer GET /api/v1/sessions/current gives.
            // Nothing about the named Space is read on this path, and nothing is recorded: AD-3 draws
            // the refusal line at the Space boundary and nowhere else.
            return BoundaryProblems.Unauthenticated();
        }

        // Read before anything else so a Space-scoped endpoint that declares no capability fails
        // closed whoever calls it, and so a boundary refusal can record what was attempted.
        var capability = RequiredCapabilityExtensions.Of(httpContext.GetEndpoint());

        if (established.SpaceId is not { } spaceId)
        {
            // Unreachable through the guid-constrained routes in SpaceRoutes, and answered as a
            // refusal anyway: failing closed costs nothing. Not recorded - no Space was named.
            return BoundaryProblems.NotAvailable();
        }

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory);
        var request = new BoundaryRequest(services, logger, accountId, spaceId, capability, httpContext.RequestAborted);

        var unitOfWork = await services.GetRequiredService<IActiveSpaceResolver>()
            .BeginAsync(accountId, spaceId, request.CancellationToken)
            .ConfigureAwait(false);

        if (unitOfWork is null)
        {
            SpaceLog.SpaceNotResolved(logger, spaceId);
            await RecordAsync(request, AccessRefusalKind.CrossSpace).ConfigureAwait(false);

            return BoundaryProblems.NotAvailable();
        }

        await using (unitOfWork.ConfigureAwait(false))
        {
            // The Role off the Membership row on this request (AD-1), against the matrix. A
            // Qualified cell passes the Role gate; its target constraint is the owning slice's.
            if (RoleCapabilityMatrix.Decide(unitOfWork.Context.Role, capability) != CapabilityDecision.Denied)
            {
                return await ServeAsync(context, next, unitOfWork, request).ConfigureAwait(false);
            }
        }

        // Denied, and the unit of work disposed without completing - rolled back - before the
        // refusal is recorded on a unit of its own.
        SpaceLog.CapabilityRefused(logger, spaceId, StableCode.Of(capability));
        await RecordAsync(request, AccessRefusalKind.InsufficientRole).ConfigureAwait(false);

        return BoundaryProblems.InsufficientRole(capability);
    }

    /// <summary>
    /// A permitted member: consult the presence bound inside the unit of work, then run the
    /// endpoint and commit only a materialised success.
    /// </summary>
    private static async ValueTask<object?> ServeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next,
        IActiveSpaceUnitOfWork unitOfWork,
        BoundaryRequest request)
    {
        var bound = BoundRegistry.For(Bound.ConcurrentActiveSessionsPerSpace);

        var admitted = await request.Services.GetRequiredService<ISpacePresenceRegister>()
            .TryEnterAsync(request.AccountId, request.SpaceId, bound.Limit, request.CancellationToken)
            .ConfigureAwait(false);

        if (!admitted)
        {
            // Raised inside the resolution transaction, which the caller disposes without
            // completing: nothing is written for the refused Account (AC7).
            SpaceLog.BoundReached(request.Logger, request.SpaceId, StableCode.Of(bound.Bound), bound.Limit);

            return BoundaryProblems.BoundReached(bound);
        }

        SpaceLog.SpaceResolved(request.Logger, request.SpaceId);

        var result = await next(context).ConfigureAwait(false);

        UnitOfWorkResults.EnsureMaterialised(result);

        if (UnitOfWorkResults.IsFailure(result))
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

    /// <summary>
    /// Writes the refusal record. The request pipeline is its only writer (AR-3).
    /// </summary>
    private static Task RecordAsync(BoundaryRequest request, AccessRefusalKind kind) =>
        request.Services.GetRequiredService<IAccessRefusalRecorder>()
            .RecordAsync(request.AccountId, request.SpaceId, request.Capability, kind, request.CancellationToken);

    /// <summary>
    /// What a Space-boundary decision needs about the request, gathered once.
    /// </summary>
    private sealed record BoundaryRequest(
        IServiceProvider Services,
        ILogger Logger,
        Guid AccountId,
        Guid SpaceId,
        Capability Capability,
        CancellationToken CancellationToken);
}
