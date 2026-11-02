using Yello.Application.Invitations;
using Yello.Application.Invitations.AcceptInvitation;
using Yello.Application.Invitations.ViewInvitation;
using Yello.Contracts;
using Yello.Contracts.Invitations;
using Yello.Domain;
using Yello.Domain.Bounds;
using Yello.Host.ApiTokens;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// View an Invitation and accept it, by its token (FR-11; story 4.3, AC1-AC9; Lee's Q4 (a), Q6 (a), Q7 (a)).
/// </summary>
/// <remarks>
/// <para>
/// <b>No <c>{spaceId}</c>, no capability, no Role</b> (story 4.3, C7): the link holder does not know the Space until the
/// view answers, and holds no Membership in it before the accept. The pipeline's Invitation step has resolved the token to
/// its Space on the server and opened the Invitation unit of work keyed to that Space alone; for the accept it has also
/// replayed an <c>Idempotency-Key</c> already answered and consulted the Memberships-per-Space bound. These endpoints
/// only map what the slices came to (AR-3).
/// </para>
/// <para>
/// <b>One answer for five states</b> (AC6): a revoked, accepted, expired, lapsed or unrecognised token is the same
/// <c>404 invitation-not-valid</c> on both routes, byte for byte, and nothing on either route is recorded as an
/// <c>AccessRefusal</c> - none of these answers is one of AD-20's two kinds.
/// </para>
/// <para>
/// <b>Why the mismatch names neither address</b> (AC7, <c>EXPERIENCE.md:265</c>): "the helpful version discloses the
/// invited address to whoever holds the link, which on a forwarded link is a stranger".
/// </para>
/// </remarks>
public static class InvitationAcceptanceEndpoints
{
    /// <summary>
    /// Registers <c>GET /api/v1/invitations/offer</c>: the Invitation view, the token in the <c>Yello-Invitation-Token</c>
    /// header. No credential required: the same offer to no Session, any Session and a bearer Token.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapViewInvitation(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(InvitationRoutes.Offer, ViewAsync)
            .ResolvedByInvitationToken(InvitationTokenUse.Offer);

    /// <summary>
    /// Registers <c>POST /api/v1/invitations/acceptance</c>: the accept, the token in the body. Session-only (Lee's Q7 (a):
    /// a Token is bound to one Space and must never join its Account to another), and the Memberships-per-Space bound,
    /// which the pipeline consults after its screen (AC9, open question 9 (a)).
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapAcceptInvitation(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(InvitationRoutes.Acceptance, AcceptAsync)
            .SessionOnly()
            .ResolvedByInvitationToken(InvitationTokenUse.Acceptance)
            .ConsultsBound(Bound.MembershipsPerSpace);

    /// <summary>
    /// Shows what an Invitation offers.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="request">The Invitation context the pipeline established.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>200</c> with the Space's name, the Role's code, the issuer's name and - with a Session - whether the Invitation
    /// names its Account; or the uniform <c>404 invitation-not-valid</c>.
    /// </returns>
    public static Task<IResult> ViewAsync(ViewInvitationHandler handler, InvitationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(request);

        // Split for S4457.
        return ViewCoreAsync(handler, request.Context, cancellationToken);
    }

    /// <summary>
    /// Accepts an Invitation as the signed-in Account.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="request">The Invitation context the pipeline established.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>200</c> with the joined Space's id; the uniform <c>404 invitation-not-valid</c> (AC5, AC6);
    /// <c>403 invitation-for-another-account</c> (AC7, open question 13 (a)); or <c>409 already-a-member</c> (open question
    /// 12 (a)). The pipeline answers the bound's <c>409</c> before this runs.
    /// </returns>
    public static Task<IResult> AcceptAsync(AcceptInvitationHandler handler, InvitationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(request);

        // Split for S4457.
        return AcceptCoreAsync(handler, request.Context, cancellationToken);
    }

    private static async Task<IResult> ViewCoreAsync(ViewInvitationHandler handler, InvitationContext context, CancellationToken cancellationToken)
    {
        var offer = await handler.HandleAsync(context, cancellationToken).ConfigureAwait(false);

        return offer is null
            ? NotValid()
            : Results.Ok(new InvitationOfferResponse(offer.SpaceName, StableCode.Of(offer.Role), offer.IssuerName, offer.AddressedToYou));
    }

    private static async Task<IResult> AcceptCoreAsync(AcceptInvitationHandler handler, InvitationContext context, CancellationToken cancellationToken)
    {
        var outcome = await handler.HandleAsync(context, cancellationToken).ConfigureAwait(false);

        return outcome switch
        {
            InvitationAcceptanceOutcome.Accepted => Results.Ok(new AcceptedInvitationResponse(context.SpaceId)),

            InvitationAcceptanceOutcome.NotValid => NotValid(),

            InvitationAcceptanceOutcome.ForAnotherAccount => Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                type: ProblemTypes.InvitationForAnotherAccount,
                title: "This invitation is for a different Account."),

            InvitationAcceptanceOutcome.AlreadyAMember => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: ProblemTypes.AlreadyAMember,
                title: "This Account is already a Member of this Space."),

            // A new outcome must be answered here, never fall into the uniform 404 (story 4.1 code review P10).
            _ => throw new InvalidOperationException($"Unanswered Invitation acceptance outcome {outcome}."),
        };
    }

    /// <summary>
    /// The one answer for a revoked, accepted, expired, lapsed or unrecognised Invitation, on both routes, byte for byte
    /// (AC6; Lee's Q6 (a)): no instance, no id, no Space.
    /// </summary>
    private static IResult NotValid() =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            type: ProblemTypes.InvitationNotValid,
            title: "This invitation is no longer valid.");
}
