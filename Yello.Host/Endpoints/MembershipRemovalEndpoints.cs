using Yello.Application.Memberships;
using Yello.Application.Memberships.LeaveSpace;
using Yello.Application.Memberships.RemoveMembership;
using Yello.Application.Spaces;
using Yello.Contracts;
using Yello.Contracts.Memberships;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// Remove a Membership from the active Space, or leave it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Apart from <see cref="MembershipEndpoints"/> only to keep each class inside its dependency budget</b> (S1200); the
/// same rules hold. No Membership check, no Role, no idempotency and no refusal record here: the pipeline did all of it
/// before this code runs.
/// </para>
/// <para>
/// <b>The removal declares <c>RemoveMembership</c></b> (Owner Allowed, Admin Qualified). A removal beyond the acting
/// authority is <see cref="QualifiedRefusal"/>, which the pipeline records and answers <c>403</c>.
/// </para>
/// <para>
/// <b>The leave declares <c>ReadProjectsAndTasks</c>, the capability every Role holds, deliberately.</b> Leaving is decided
/// by the caller's identity, not its Role, and its statement can delete nothing but the caller's own non-Owner row. It is
/// the one write under that capability, and a gate keeps it so. It is not marked as destroying its Space: its idempotency
/// record hangs off the Space, which stays, and a replay finds no Membership and is the boundary's <c>404</c>.
/// </para>
/// <para>
/// <b>An actor whose own Membership went while it waited</b> is <see cref="MembershipLost"/>, which the pipeline answers as
/// the boundary's recorded <c>404</c>. Both answer <c>204</c> with no body, so an idempotency record holds no address.
/// </para>
/// </remarks>
public static class MembershipRemovalEndpoints
{
    /// <summary>
    /// Registers <c>DELETE /api/v1/spaces/{spaceId}/memberships/{membershipId}</c>: the removal.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapRemoveMembership(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapDelete(MembershipRoutes.Membership, RemoveAsync)
            .RequiresCapability(Capability.RemoveMembership);

    /// <summary>
    /// Registers <c>DELETE /api/v1/spaces/{spaceId}/membership</c>: the caller leaves the Space.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapLeaveSpace(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapDelete(MembershipRoutes.Own, LeaveAsync)
            .RequiresCapability(Capability.ReadProjectsAndTasks);

    /// <summary>
    /// Removes a Membership from the active Space, within the caller's authority.
    /// </summary>
    /// <param name="membershipId">The Membership, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request: its services hold the resolved context.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>204</c>; <c>409 ownership-held</c> for the Owner's own row; the pipeline's recorded <c>403</c> beyond the caller's
    /// authority, or its recorded boundary <c>404</c> when the caller's own Membership went first; or the boundary's
    /// <c>404 not-available</c> - byte for byte, and not recorded - for an id that names nothing in this Space.
    /// </returns>
    public static async Task<IResult> RemoveAsync(
        Guid membershipId,
        RemoveMembershipHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();

        var outcome = await handler.HandleAsync(context.AccountId, membershipId, cancellationToken).ConfigureAwait(false);

        return outcome switch
        {
            RemovalOutcome.Removed => Results.NoContent(),

            RemovalOutcome.OwnershipHeld => OwnershipHeld(),

            // The pipeline records it and answers the 403 (AD-20).
            RemovalOutcome.BeyondAuthority => new QualifiedRefusal(Capability.RemoveMembership),

            // The pipeline records it and answers the boundary's 404.
            RemovalOutcome.LostMembership => new MembershipLost(),

            // Not recorded: an in-Space not-found crossed no boundary (AD-20). The body is the boundary's.
            RemovalOutcome.NotFound => BoundaryProblems.NotAvailable(),

            // A new outcome must be answered here, never fall into a success or the boundary's 404.
            _ => throw new InvalidOperationException($"Unanswered removal outcome {outcome}."),
        };
    }

    /// <summary>
    /// The caller leaves the active Space.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request: its services hold the resolved context.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>204</c>; <c>409 ownership-held</c> for the Owner; or the pipeline's recorded boundary <c>404</c> when the caller's
    /// Membership went first.
    /// </returns>
    public static async Task<IResult> LeaveAsync(
        LeaveSpaceHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();

        var outcome = await handler.HandleAsync(context.AccountId, cancellationToken).ConfigureAwait(false);

        return outcome switch
        {
            RemovalOutcome.Removed => Results.NoContent(),

            RemovalOutcome.OwnershipHeld => OwnershipHeld(),

            // The pipeline records it and answers the boundary's 404.
            RemovalOutcome.LostMembership => new MembershipLost(),

            // The leave names only the caller's own row, so no other outcome can reach here; fail loudly if one does.
            _ => throw new InvalidOperationException($"Unanswered leave outcome {outcome}."),
        };
    }

    private static IResult OwnershipHeld() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: ProblemTypes.OwnershipHeld,
            title: "The Owner leaves by having an Ownership Offer accepted, or by deleting the Space.");
}
