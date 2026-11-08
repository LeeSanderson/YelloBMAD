using Yello.Application.Memberships;
using Yello.Application.Spaces;
using Yello.Contracts.Spaces;
using Yello.Domain;
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
        /// Echoes the Role the request was judged under in <see cref="RoleHeader.HeaderName"/>, added when the answer starts
        /// so the final status decides it.
        /// </summary>
        /// <remarks>
        /// Only on a <c>2xx</c> or a <c>403</c>: every <c>403</c> after resolution is the capability refusal. A <c>404</c>, of
        /// either kind, never carries it, so the header cannot tell an in-Space <c>404</c> from the boundary's. Added at
        /// start rather than now, so an <c>Idempotency-Key</c> record never captures it and a replay carries the live Role.
        /// </remarks>
        internal static void EchoJudgedRole(HttpResponse response, Role role)
        {
            var code = StableCode.Of(role);

            response.OnStarting(() =>
            {
                var status = response.StatusCode;

                if (status == StatusCodes.Status403Forbidden
                    || (status >= StatusCodes.Status200OK && status < StatusCodes.Status300MultipleChoices))
                {
                    response.Headers[RoleHeader.HeaderName] = code;
                }

                return Task.CompletedTask;
            });
        }

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
            var role = unitOfWork.Context.Role;

            request.Services.GetRequiredService<ActingAuthority>()
                .Set(RoleCapabilityMatrix.RoleChangeAuthorityOf(role), RoleCapabilityMatrix.RemovalAuthorityOf(role));

            var result = await LostSpace.ServeOrRefuseAsync(context, next, unitOfWork, request).ConfigureAwait(false);

            if (result is MembershipLost)
            {
                // The actor's own Membership went while its request waited: answered and recorded exactly as its next
                // request would be, never as a 403 that would tell it it still reached the Space's authority check.
                SpaceLog.LostUnderRequest(request.Logger, request.SpaceId);
                await RecordAsync(request, AccessRefusalKind.CrossSpace).ConfigureAwait(false);

                return BoundaryProblems.NotAvailable();
            }

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

/// <summary>
/// A slice's answer that the acting Account's own Membership was gone by the time its change was written - removed while
/// the request waited on the Memberships lock. The pipeline records it <c>CrossSpace</c> and answers the boundary's
/// <c>404</c>; an endpoint never answers it itself.
/// </summary>
/// <remarks>
/// A failure status, so the unit of work rolls back and no <c>Idempotency-Key</c> records it. Executed only if it escaped the
/// pipeline, as the same boundary body - unrecorded, which the removed-actor race's record assertion would catch.
/// </remarks>
internal sealed class MembershipLost : IResult, IStatusCodeHttpResult
{
    /// <inheritdoc />
    public int? StatusCode => StatusCodes.Status404NotFound;

    /// <inheritdoc />
    public Task ExecuteAsync(HttpContext httpContext) => BoundaryProblems.NotAvailable().ExecuteAsync(httpContext);
}
