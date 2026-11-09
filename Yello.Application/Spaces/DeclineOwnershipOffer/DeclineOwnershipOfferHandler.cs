namespace Yello.Application.Spaces.DeclineOwnershipOffer;

/// <summary>
/// Declines an Ownership Offer naming the caller.
/// </summary>
/// <remarks>
/// <para>
/// <b>Decided by row identity, never by Role</b>, as the acceptance is: the store's guarded statement holds that the offer
/// names the caller's own Membership. This slice reads no Role and records nothing.
/// </para>
/// <para>
/// <b>Nothing else moves</b>: no Role changes, so nothing is published, and nothing is sent. This handler takes no outbox,
/// and a gate holds that.
/// </para>
/// </remarks>
/// <param name="offers">Declines the offer.</param>
public sealed class DeclineOwnershipOfferHandler(IOwnershipOfferStore offers)
{
    /// <summary>
    /// Declines the offer.
    /// </summary>
    /// <param name="actorAccountId">The declining Account, as the pipeline resolved it.</param>
    /// <param name="offerId">The offer, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened.</returns>
    public Task<OwnershipOfferDecline> HandleAsync(Guid actorAccountId, Guid offerId, CancellationToken cancellationToken) =>
        offers.DeclineAsync(actorAccountId, offerId, cancellationToken);
}
