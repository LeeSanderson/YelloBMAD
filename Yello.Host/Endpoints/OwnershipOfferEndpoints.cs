using System.Text.Json;
using Yello.Application.Spaces;
using Yello.Application.Spaces.OfferOwnership;
using Yello.Application.Spaces.ReadOwnershipOffer;
using Yello.Application.Spaces.RevokeOwnershipOffer;
using Yello.Contracts;
using Yello.Contracts.Spaces;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// Offer ownership of the active Space to one of its Memberships, read the pending offer, and revoke it.
/// </summary>
/// <remarks>
/// <para>
/// <b>No Membership check, no Role, no idempotency and no refusal record here</b> - the reasons
/// <see cref="ProjectEndpoints"/> gives. All three declare <c>OfferOwnershipTransfer</c>, which only the Owner holds, so an
/// Admin, a Member or a Viewer meets the pipeline's recorded <c>403</c> before this code runs. Both surfaces.
/// </para>
/// <para>
/// <b>The offer's body is read as a document</b>, never bound, so a body that names no Membership - or names an address
/// instead - is the same <c>400 invalid-offer-recipient</c> as an id that is not another Membership of this Space.
/// </para>
/// </remarks>
public static class OwnershipOfferEndpoints
{
    private const string MembershipMember = "membershipId";

    /// <summary>
    /// Registers <c>POST /api/v1/spaces/{spaceId}/ownership-offers</c>: the offer.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapOfferOwnership(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(OwnershipOfferRoutes.Offers, OfferAsync)
            .RequiresCapability(Capability.OfferOwnershipTransfer);

    /// <summary>
    /// Registers <c>GET /api/v1/spaces/{spaceId}/ownership-offers/pending</c>: the Owner's read of the pending offer.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapReadOwnershipOffer(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(OwnershipOfferRoutes.Pending, ReadAsync)
            .RequiresCapability(Capability.OfferOwnershipTransfer);

    /// <summary>
    /// Registers <c>DELETE /api/v1/spaces/{spaceId}/ownership-offers/{offerId}</c>: the revoke.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapRevokeOwnershipOffer(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapDelete(OwnershipOfferRoutes.Offer, RevokeAsync)
            .RequiresCapability(Capability.OfferOwnershipTransfer);

    /// <summary>
    /// Offers ownership of the active Space to one of its Memberships.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request: its services hold the resolved context, and its body is read here.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>201</c> with the offer and a <c>Location</c> of the pending read; <c>400 invalid-offer-recipient</c> - one body for
    /// every cause - for a body naming no other Membership of this Space; <c>409 ownership-offer-pending</c> while another
    /// offer is pending; or the pipeline's recorded boundary <c>404</c> when the caller's own Membership went first.
    /// </returns>
    public static async Task<IResult> OfferAsync(
        OfferOwnershipHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (await ReadRecipientAsync(httpContext.Request, cancellationToken).ConfigureAwait(false) is not { } recipient)
        {
            return InvalidOfferRecipient();
        }

        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();

        var result = await handler.HandleAsync(context, recipient, cancellationToken).ConfigureAwait(false);

        switch (result.Outcome)
        {
            case OwnershipOfferOutcome.Offered:
                // Set before returning, so the idempotency record captures it and a replay carries it.
                httpContext.Response.Headers.Location = OwnershipOfferRoutes.PendingOf(context.SpaceId);

                return Results.Json(Response(result.Offer!), statusCode: StatusCodes.Status201Created);

            // Not recorded: a Membership that is not another of this Space's is a validation refusal, the same bytes for a
            // random id and a foreign one.
            case OwnershipOfferOutcome.InvalidRecipient:
                return InvalidOfferRecipient();

            case OwnershipOfferOutcome.AlreadyPending:
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    type: ProblemTypes.OwnershipOfferPending,
                    title: "This Space already has a pending Ownership Offer. Revoke it to make another.");

            // The pipeline records it and answers the boundary's 404.
            case OwnershipOfferOutcome.LostMembership:
                return new MembershipLost();

            // Not recorded: the Space went after the pipeline resolved it. The body is the boundary's.
            case OwnershipOfferOutcome.SpaceGone:
                return BoundaryProblems.NotAvailable();

            // A new outcome must be answered here, never fall into a success or the boundary's 404.
            default:
                throw new InvalidOperationException($"Unanswered offer outcome {result.Outcome}.");
        }
    }

    /// <summary>
    /// Reads the active Space's actionable pending offer.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>200</c> with the offer, or <c>204</c> when none is actionable.</returns>
    public static async Task<IResult> ReadAsync(ReadOwnershipOfferHandler handler, CancellationToken cancellationToken)
    {
        var offer = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);

        return offer is null ? Results.NoContent() : Results.Ok(Response(offer));
    }

    /// <summary>
    /// Revokes the active Space's pending offer.
    /// </summary>
    /// <param name="offerId">The offer, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>204</c>; <c>409 ownership-offer-not-pending</c> for an offer of this Space that is no longer actionable; or the
    /// boundary's <c>404 not-available</c> - byte for byte, and not recorded - for an id that names no offer here.
    /// </returns>
    public static async Task<IResult> RevokeAsync(Guid offerId, RevokeOwnershipOfferHandler handler, CancellationToken cancellationToken)
    {
        var outcome = await handler.HandleAsync(offerId, cancellationToken).ConfigureAwait(false);

        return outcome switch
        {
            OwnershipOfferRevocation.Revoked => Results.NoContent(),

            OwnershipOfferRevocation.NotPending => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: ProblemTypes.OwnershipOfferNotPending,
                title: "This offer is no longer open."),

            // Not recorded: an in-Space not-found crossed no boundary (AD-20). The body is the boundary's.
            OwnershipOfferRevocation.NotFound => BoundaryProblems.NotAvailable(),

            _ => throw new InvalidOperationException($"Unanswered revoke outcome {outcome}."),
        };
    }

    private static OwnershipOfferResponse Response(OwnershipOfferView offer) =>
        new(offer.Id, offer.RecipientMembershipId, offer.RecipientName, offer.ExpiresAt, offer.ReadAt);

    private static IResult InvalidOfferRecipient() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            type: ProblemTypes.InvalidOfferRecipient,
            title: "Ownership can be offered only to another Membership of this Space.");

    /// <summary>
    /// The Membership the body names, or <c>null</c> when it is not an object naming one by a Guid.
    /// </summary>
    private static async Task<Guid?> ReadRecipientAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType())
        {
            return null;
        }

        try
        {
            using var document = await JsonDocument.ParseAsync(request.Body, default, cancellationToken).ConfigureAwait(false);

            if (document.RootElement is not { ValueKind: JsonValueKind.Object } root || !root.TryGetProperty(MembershipMember, out var member))
            {
                return null;
            }

            return member.ValueKind == JsonValueKind.String && member.TryGetGuid(out var membershipId) ? membershipId : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
