using Yello.Application.Spaces;
using Yello.Application.Spaces.DeleteSpace;
using Yello.Application.Spaces.RenameSpace;
using Yello.Contracts.Spaces;
using Yello.Domain;
using Yello.Domain.Memberships;
using Yello.Domain.Spaces;
using Yello.Host.Endpoints;

namespace Yello.Host.Spaces;

/// <summary>
/// <c>GET /api/v1/spaces/{spaceId}</c> - the active Space's own answer for the context bar: its name
/// and the Role the caller holds there, read on this request. Story 1.7, AC4 / AC5; AD-1, I-10.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pipeline-owned, which is why it lives beside <see cref="ActiveSpaceResolution"/> rather than in
/// a slice.</b> It reports the pipeline's own resolution: the <see cref="ActiveSpaceContext"/>
/// <see cref="ActiveSpaceResolution"/> resolved from the Membership row on <i>this</i> request. A
/// slice may not read the Role (A-12: <c>RefusalBoundaryGateTests.Nothing_but_the_pipeline_reads_the_Role_off_the_active_Space_context</c>),
/// and this is named in that gate's permitted list for exactly this reason - it decides nothing by
/// Role; it states it.
/// </para>
/// <para>
/// <b>A Space-scoped route like any other.</b> It carries <c>{spaceId}</c>, so the pipeline resolves
/// it: a non-member and a missing Space get the boundary's one <c>404</c> and a <c>CrossSpace</c>
/// record, and it declares the read capability every Role holds. Each call refreshes the caller's
/// presence in the Space (story 1.6, decision 1), so arriving in a Space counts toward its
/// concurrent-Sessions bound - and a <c>409</c> here is that bound.
/// </para>
/// <para>
/// <b>Nothing is carried.</b> No Role is put on the Session, a cookie, a claim or a cache (AD-1); the
/// client asks again on every arrival in a Space.
/// </para>
/// </remarks>
public static class ActiveSpaceEndpoints
{
    /// <summary>
    /// Registers <c>GET /api/v1/spaces/{spaceId}</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapActiveSpace(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(SpaceRoutes.Active, GetAsync)
            .RequiresCapability(Capability.ReadProjectsAndTasks);

    /// <summary>
    /// Answers the active Space's name and the caller's Role in it.
    /// </summary>
    /// <param name="httpContext">
    /// The request, whose services hold the context the pipeline resolved. Read at execution rather
    /// than bound as an argument, because minimal APIs bind arguments before the pipeline's filters
    /// have resolved anything.
    /// </param>
    /// <param name="names">Reads the resolved Space's name.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>200</c> with the Space's id, name and the Role's stable code.</returns>
    public static async Task<IResult> GetAsync(
        HttpContext httpContext,
        IActiveSpaceNameReader names,
        CancellationToken cancellationToken)
    {
        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();

        var name = await names.ReadAsync(cancellationToken).ConfigureAwait(false);

        // No name: the Space was deleted after the pipeline resolved it (story 3.3, deferred-work.md:360). The answer the
        // next request gets - the boundary's 404 - not a 500. Not recorded: refusal records are the pipeline's (2.5).
        return name is null
            ? BoundaryProblems.NotAvailable()
            : Results.Ok(new ActiveSpaceResponse(context.SpaceId, name, StableCode.Of(context.Role)));
    }

    /// <summary>
    /// Registers <c>PATCH /api/v1/spaces/{spaceId}</c> - renaming the active Space (story 3.1, FR-6). Space-scoped on
    /// both surfaces, declaring <c>RenameSpace</c>, which the matrix grants Owner and Admin.
    /// </summary>
    /// <remarks>
    /// <b>Here, beside the read it answers like</b>: a rename answers the same <see cref="ActiveSpaceResponse"/> the
    /// context bar reads, so the Role it states is the pipeline's resolution reported, as <see cref="GetAsync"/>
    /// reports it - decided by nothing here.
    /// </remarks>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapRenameSpace(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPatch(SpaceRoutes.Active, RenameAsync)
            .RequiresCapability(Capability.RenameSpace);

    /// <summary>
    /// Renames the active Space.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request: its services hold the resolved context, and its body is read here.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>200</c> with the Space's id, new name and the caller's Role, or <c>400 invalid-space-name</c>.</returns>
    public static async Task<IResult> RenameAsync(
        RenameSpaceHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var request = await JsonRequestBody.ReadAsync(httpContext.Request, typeof(RenameSpaceRequest), cancellationToken)
            .ConfigureAwait(false) as RenameSpaceRequest;

        if (!SpaceName.TryNormalise(request?.Name, out var name))
        {
            return SpaceNameProblems.Invalid();
        }

        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();

        var renamed = await handler.HandleAsync(context, name, cancellationToken).ConfigureAwait(false);

        // No row renamed: the Space was deleted after the pipeline resolved it (story 3.3, deferred-work.md:666).
        return renamed is null
            ? BoundaryProblems.NotAvailable()
            : Results.Ok(new ActiveSpaceResponse(renamed.Id, renamed.Name, StableCode.Of(context.Role)));
    }

    /// <summary>
    /// Registers <c>DELETE /api/v1/spaces/{spaceId}</c> - deleting the active Space (story 3.3, FR-7). Space-scoped on
    /// both surfaces, declaring <c>DeleteSpace</c>, which the matrix grants the Owner alone, and marked
    /// <see cref="DestroysItsSpace"/>, so the pipeline records no idempotency record against the Space it destroys (open
    /// question 1 (a)).
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapDeleteSpace(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapDelete(SpaceRoutes.Active, DeleteAsync)
            .RequiresCapability(Capability.DeleteSpace)
            .DestroysItsSpace();

    /// <summary>
    /// Deletes the active Space.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request, whose services hold the resolved context.</param>
    /// <param name="loggerFactory">Builds the logger the deletion is recorded on.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>204</c> once the deletion is in the unit of work the pipeline commits, or the boundary's <c>404</c> when the
    /// Space was already gone - not recorded, as story 2.5's Project deletion is not.
    /// </returns>
    public static async Task<IResult> DeleteAsync(
        DeleteSpaceHandler handler,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();

        var deleted = await handler.HandleAsync(context, cancellationToken).ConfigureAwait(false);

        if (deleted == SpaceDeletion.NotFound)
        {
            return BoundaryProblems.NotAvailable();
        }

        SpaceLog.SpaceDeleted(loggerFactory.CreateLogger(typeof(ActiveSpaceEndpoints).FullName!), context.SpaceId);

        return Results.NoContent();
    }

    /// <summary>
    /// Registers <c>GET /api/v1/spaces/{spaceId}/deletion</c> - what deleting the active Space would take, for the
    /// confirm (story 3.3, AC2, open question 2 (a)). Declares <c>DeleteSpace</c>, so only the Owner reads it.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapSpaceDeletionSummary(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(SpaceRoutes.DeletionSummary, DeletionSummaryAsync)
            .RequiresCapability(Capability.DeleteSpace);

    /// <summary>
    /// Counts the active Space's Projects, Tasks and other Members.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request, whose services hold the resolved context.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>200</c> with the counts and nothing else.</returns>
    public static async Task<IResult> DeletionSummaryAsync(
        ReadDeletionSummaryHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();

        var contents = await handler.HandleAsync(context, cancellationToken).ConfigureAwait(false);

        return Results.Ok(new SpaceDeletionSummaryResponse(contents.Projects, contents.Tasks, contents.OtherMembers));
    }
}
