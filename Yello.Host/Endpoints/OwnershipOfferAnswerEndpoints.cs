using Yello.Application.Spaces;
using Yello.Application.Spaces.AcceptOwnershipOffer;
using Yello.Application.Spaces.DeclineOwnershipOffer;
using Yello.Application.Spaces.ReadMyOwnershipOffer;
using Yello.Contracts;
using Yello.Contracts.Spaces;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// The recipient's side of an Ownership Offer: read the offer naming the caller, accept it, decline it.
/// </summary>
/// <remarks>
/// <para>
/// <b>All three declare <c>ReadProjectsAndTasks</c></b>, which every Role holds: they are decided by row identity - is the
/// caller the Membership the offer names - inside the store's own statement, never by Role. Anyone else gets the in-Space
/// not-found, never a Role-shaped <c>403</c>. Both surfaces.
/// </para>
/// <para>
/// <b>No Membership check, no Role, no idempotency and no refusal record here</b> - the reasons
/// <see cref="ProjectEndpoints"/> gives.
/// </para>
/// </remarks>
public static class OwnershipOfferAnswerEndpoints
{
    /// <summary>
    /// Registers <c>GET /api/v1/spaces/{spaceId}/ownership-offers/mine</c>: the recipient's read.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapReadMyOwnershipOffer(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(OwnershipOfferRoutes.Mine, ReadMineAsync)
            .RequiresCapability(Capability.ReadProjectsAndTasks);

    /// <summary>
    /// Registers <c>POST /api/v1/spaces/{spaceId}/ownership-offers/{offerId}/acceptance</c>: the acceptance.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapAcceptOwnershipOffer(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(OwnershipOfferRoutes.Acceptance, AcceptAsync)
            .RequiresCapability(Capability.ReadProjectsAndTasks);

    /// <summary>
    /// Registers <c>POST /api/v1/spaces/{spaceId}/ownership-offers/{offerId}/decline</c>: the decline.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapDeclineOwnershipOffer(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(OwnershipOfferRoutes.Decline, DeclineAsync)
            .RequiresCapability(Capability.ReadProjectsAndTasks);

    /// <summary>
    /// Reads the actionable offer naming the caller's own Membership.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request: its services hold the resolved context.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>200</c> with the offer, or <c>204</c> when none names the caller.</returns>
    public static async Task<IResult> ReadMineAsync(ReadMyOwnershipOfferHandler handler, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();

        var offer = await handler.HandleAsync(context.AccountId, cancellationToken).ConfigureAwait(false);

        return offer is null
            ? Results.NoContent()
            : Results.Ok(new OwnershipOfferForRecipientResponse(offer.Id, offer.OffererName, offer.ExpiresAt, offer.ReadAt));
    }

    /// <summary>
    /// Accepts the offer naming the caller.
    /// </summary>
    /// <param name="offerId">The offer, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request: its services hold the resolved context.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>204</c>; <c>409 ownership-offer-not-pending</c> to the named recipient once the offer is no longer open, or when its
    /// offerer is no longer the Owner; <c>409 owner-role-fixed</c> should the promote meet a second Owner; the pipeline's
    /// recorded boundary <c>404</c> when the caller's own Membership went first; or the boundary's <c>404</c> - byte for byte,
    /// and not recorded - for an id that names no offer to the caller here.
    /// </returns>
    public static async Task<IResult> AcceptAsync(
        Guid offerId,
        AcceptOwnershipOfferHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();

        var outcome = await handler.HandleAsync(context.AccountId, offerId, cancellationToken).ConfigureAwait(false);

        return outcome switch
        {
            OwnershipOfferAcceptance.Accepted => Results.NoContent(),

            OwnershipOfferAcceptance.NotPending or OwnershipOfferAcceptance.OfferStale => OwnershipOfferEndpoints.NotPending(),

            OwnershipOfferAcceptance.OwnerRoleFixed => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: ProblemTypes.OwnerRoleFixed,
                title: "This Space's ownership has not changed."),

            // The pipeline records it and answers the boundary's 404.
            OwnershipOfferAcceptance.LostMembership => new MembershipLost(),

            // Not recorded: an in-Space not-found crossed no boundary, and says nothing of whom the offer names.
            OwnershipOfferAcceptance.NotFound => BoundaryProblems.NotAvailable(),

            _ => throw new InvalidOperationException($"Unanswered acceptance outcome {outcome}."),
        };
    }

    /// <summary>
    /// Declines the offer naming the caller.
    /// </summary>
    /// <param name="offerId">The offer, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request: its services hold the resolved context.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>204</c>; <c>409 ownership-offer-not-pending</c> to the named recipient once the offer is no longer open; the
    /// pipeline's recorded boundary <c>404</c> when the caller's own Membership went first; or the boundary's <c>404</c>,
    /// unrecorded, for an id that names no offer to the caller here.
    /// </returns>
    public static async Task<IResult> DeclineAsync(
        Guid offerId,
        DeclineOwnershipOfferHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();

        var outcome = await handler.HandleAsync(context.AccountId, offerId, cancellationToken).ConfigureAwait(false);

        return outcome switch
        {
            OwnershipOfferDecline.Declined => Results.NoContent(),

            OwnershipOfferDecline.NotPending => OwnershipOfferEndpoints.NotPending(),

            // The pipeline records it and answers the boundary's 404.
            OwnershipOfferDecline.LostMembership => new MembershipLost(),

            // Not recorded: an in-Space not-found crossed no boundary.
            OwnershipOfferDecline.NotFound => BoundaryProblems.NotAvailable(),

            _ => throw new InvalidOperationException($"Unanswered decline outcome {outcome}."),
        };
    }
}
