namespace Yello.Application.Spaces.RevokeOwnershipOffer;

/// <summary>
/// Revokes the active Space's pending Ownership Offer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything cross-cutting happened before this ran</b>: the pipeline resolved the Space, checked
/// <c>OfferOwnershipTransfer</c> - the Owner's alone - and replayed a key it had already seen. This slice reads no Role.
/// </para>
/// <para>
/// <b>Nothing else moves</b>: every Membership and Role is left as it was, and nothing is sent - the email already delivered
/// cannot be recalled. This handler takes no outbox, and a gate holds that.
/// </para>
/// </remarks>
/// <param name="offers">Revokes the offer.</param>
public sealed class RevokeOwnershipOfferHandler(IOwnershipOfferStore offers)
{
    /// <summary>
    /// Revokes the offer.
    /// </summary>
    /// <param name="actorAccountId">The revoking Account, as the pipeline resolved it.</param>
    /// <param name="offerId">The offer, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened.</returns>
    public Task<OwnershipOfferRevocation> HandleAsync(Guid actorAccountId, Guid offerId, CancellationToken cancellationToken) =>
        offers.RevokeAsync(actorAccountId, offerId, cancellationToken);
}
