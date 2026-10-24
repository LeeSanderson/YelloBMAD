using Yello.Application.Spaces;
using Yello.Contracts.Spaces;
using Yello.Domain;
using Yello.Domain.Memberships;

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

        return Results.Ok(new ActiveSpaceResponse(context.SpaceId, name, StableCode.Of(context.Role)));
    }
}
