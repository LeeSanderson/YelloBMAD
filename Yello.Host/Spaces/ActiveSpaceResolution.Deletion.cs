using Yello.Application.Spaces;
using Yello.Domain.AccessRefusals;
using Yello.Infrastructure.Persistence;

namespace Yello.Host.Spaces;

/// <summary>
/// The pipeline's handling of a Space that a request deletes, or that is deleted under a request. Story 3.3, open
/// questions 1 (a) and 4 (a); AC9, AC13.
/// </summary>
internal static partial class ActiveSpaceResolution
{
    /// <summary>
    /// A Space deleted by the request, or under it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A route that destroys its own Space records no idempotency key</b> (<see cref="DestroysItsSpace"/>): the record's
    /// foreign key to the Space would refuse the insert once the Space is gone. Its replay finds no Membership and is the
    /// boundary's <c>404</c> - AD-18's recorded variance.
    /// </para>
    /// <para>
    /// <b>A Space deleted under a request.</b> A write that references the resolved Space - the presence row, a Project, a
    /// Task, an idempotency record, a Token - meets its foreign key (547) when a deletion committed after the resolution.
    /// The unit of work rolls back; then the pipeline asks once more what the next request would be told: it resolves the
    /// same Account in the same Space again, on a fresh unit of work, and disposes it at once. No Membership - the deletion
    /// took it - and the answer is the boundary's uniform <c>404</c> with its <c>CrossSpace</c> record, which is the next
    /// request's answer, so nothing new is disclosed. A Membership still there means the conflict is a defect, and it is
    /// rethrown.
    /// </para>
    /// <para>
    /// <b>Why resolve again rather than read the Space.</b> <c>Space</c> carries no policy, so a reader of it is a named
    /// exception (<c>RefusalBoundaryGateTests.Space_is_read_only_where_named</c>); the resolver already exists, is the
    /// pipeline's own, sets the <c>SpaceId</c> key alone through the one production line that sets a key, and answers
    /// exactly the question asked. No gate is widened.
    /// </para>
    /// <para>
    /// <b>Nested, deliberately</b>: it keeps the pipeline's own type within its dependency budget (S1200), and it is still
    /// the pipeline - the refusal it records is recorded by <see cref="ActiveSpaceResolution"/>.
    /// </para>
    /// </remarks>
    private static class LostSpace
    {
        /// <summary>
        /// Whether the endpoint's effect destroys the Space its idempotency record would hang off.
        /// </summary>
        /// <param name="httpContext">The request.</param>
        /// <returns><c>true</c> on the Space deletion route.</returns>
        internal static bool DestroysItsOwnScope(HttpContext httpContext) =>
            httpContext.GetEndpoint()?.Metadata.GetMetadata<DestroysItsSpace>() is not null;

        /// <summary>
        /// Serves a permitted member inside the unit of work, which it owns from here and disposes; a Space deleted under
        /// the request is answered as the boundary refuses.
        /// </summary>
        /// <param name="context">The request.</param>
        /// <param name="next">The rest of the pipeline.</param>
        /// <param name="unitOfWork">The resolved unit of work.</param>
        /// <param name="request">What the boundary decision needs about the request.</param>
        /// <returns>Whatever the endpoint returns, or the boundary's <c>404</c>.</returns>
        internal static async ValueTask<object?> ServeOrRefuseAsync(
            EndpointFilterInvocationContext context,
            EndpointFilterDelegate next,
            IActiveSpaceUnitOfWork unitOfWork,
            BoundaryRequest request)
        {
            try
            {
                await using (unitOfWork.ConfigureAwait(false))
                {
                    return await ServeAsync(context, next, unitOfWork, request).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (ReferenceConflict.IsReferenceConflict(exception))
            {
                if (await StillAMemberAsync(request).ConfigureAwait(false))
                {
                    throw;
                }

                SpaceLog.SpaceDeletedUnderRequest(request.Logger, request.SpaceId);
                await RecordAsync(request, AccessRefusalKind.CrossSpace).ConfigureAwait(false);

                return BoundaryProblems.NotAvailable();
            }
        }

        /// <summary>
        /// Resolves the request's Account in its Space again, on a fresh unit of work disposed at once - what the next
        /// request would be told. Not cancelled by the request: the work has already failed, and its answer is the point.
        /// </summary>
        private static async Task<bool> StillAMemberAsync(BoundaryRequest request)
        {
            var again = await request.Services.GetRequiredService<IActiveSpaceResolver>()
                .BeginAsync(request.AccountId, request.SpaceId, CancellationToken.None)
                .ConfigureAwait(false);

            if (again is null)
            {
                return false;
            }

            await again.DisposeAsync().ConfigureAwait(false);

            return true;
        }
    }
}
