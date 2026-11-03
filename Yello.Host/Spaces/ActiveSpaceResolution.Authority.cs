using Yello.Application.Memberships;
using Yello.Application.Spaces;
using Yello.Domain.AccessRefusals;
using Yello.Domain.Memberships;

namespace Yello.Host.Spaces;

/// <summary>
/// The acting-authority step of the request pipeline: the Qualified cells' target rule, handed to the slice as Role sets,
/// and the slice's typed refusal turned into the pipeline's recorded <c>403</c>. Story 4.4, AC4; Lee's Q1 (a); AD-1, AR-3,
/// AD-20; <c>deferred-work.md:329</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Part of <see cref="ActiveSpaceResolution"/>, deliberately</b>: the pipeline is the one reader of the acting Role
/// (<c>Nothing_but_the_pipeline_reads_the_Role_off_the_active_Space_context</c>), so it is the one place that can ask the
/// matrix for the Role's target rule. It sets <see cref="ActingAuthority"/> - Role sets, never the Role - for every
/// permitted request, before the endpoint runs; <c>Only_the_pipeline_sets_the_acting_authority</c> holds that it alone
/// writes it.
/// </para>
/// <para>
/// <b>Every <c>403</c> is the pipeline's</b> (AD-20). A slice whose store found the change beyond the authority answers
/// <see cref="QualifiedRefusal"/>; the unit of work rolls back like any failure, and this step then records the refusal as
/// <c>InsufficientRole</c> on a unit of its own and answers the same capability-shaped body a Role the matrix denies gets.
/// </para>
/// </remarks>
internal static partial class ActiveSpaceResolution
{
    /// <summary>
    /// The acting-authority step - nested, as <see cref="SpaceCreation"/> is, so it stays the pipeline's own for every gate
    /// while the resolution stays inside S1200's coupling limit.
    /// </summary>
    private static class QualifiedAuthority
    {
        /// <summary>
        /// Whether the matrix lets the Role read on this request past the Role gate for the endpoint's capability - an
        /// Allowed cell, or a Qualified one whose target rule the acting authority then carries.
        /// </summary>
        internal static bool Permits(Role role, Capability capability) =>
            RoleCapabilityMatrix.Decide(role, capability) != CapabilityDecision.Denied;

        /// <summary>
        /// Sets the request's acting authority from the Role read on it, serves the request, and turns a slice's
        /// <see cref="QualifiedRefusal"/> into the recorded <c>403</c>.
        /// </summary>
        internal static async ValueTask<object?> ServeWithAuthorityAsync(
            EndpointFilterInvocationContext context,
            EndpointFilterDelegate next,
            IActiveSpaceUnitOfWork unitOfWork,
            BoundaryRequest request)
        {
            request.Services.GetRequiredService<ActingAuthority>()
                .Set(RoleCapabilityMatrix.RoleChangeAuthorityOf(unitOfWork.Context.Role));

            var result = await LostSpace.ServeOrRefuseAsync(context, next, unitOfWork, request).ConfigureAwait(false);

            if (result is not QualifiedRefusal)
            {
                return result;
            }

            // The unit of work has been disposed without completing - rolled back - before the refusal is recorded on a
            // unit of its own, exactly as a Role the matrix denies.
            SpaceLog.CapabilityRefusedFor(request.Logger, request.SpaceId, request.Capability);
            await RecordAsync(request, AccessRefusalKind.InsufficientRole).ConfigureAwait(false);

            return BoundaryProblems.InsufficientRole(request.Capability);
        }
    }
}

/// <summary>
/// A slice's answer that the change it was asked for is beyond the acting authority - the target rule of a Qualified
/// matrix cell (story 4.4, AC4; Lee's Q1 (a)). The pipeline records it and answers the <c>403</c>; an endpoint never
/// answers it itself.
/// </summary>
/// <remarks>
/// A failure status, so the unit of work rolls back and no <c>Idempotency-Key</c> records it. Executed only if it escaped the
/// pipeline, as the same capability-shaped <c>403</c> - unrecorded, which <c>ISO-CHANGE-ROLE-ADMIN-BEYOND-AUTHORITY</c>'s
/// record assertion would catch.
/// </remarks>
/// <param name="capability">The capability the endpoint declares.</param>
internal sealed class QualifiedRefusal(Capability capability) : IResult, IStatusCodeHttpResult
{
    /// <inheritdoc />
    public int? StatusCode => StatusCodes.Status403Forbidden;

    /// <inheritdoc />
    public Task ExecuteAsync(HttpContext httpContext) => BoundaryProblems.InsufficientRole(capability).ExecuteAsync(httpContext);
}
