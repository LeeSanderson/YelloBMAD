namespace Yello.Application.Spaces.AcceptOwnershipOffer;

/// <summary>
/// Accepts an Ownership Offer naming the caller: the caller becomes the Space's Owner and the offering Owner an Admin.
/// </summary>
/// <remarks>
/// <para>
/// <b>Decided by row identity, never by Role.</b> The route declares <c>ReadProjectsAndTasks</c>, which every Role holds, so
/// the pipeline admits any Membership; whether the caller is the one the offer names is decided inside the store's guarded
/// statement. This slice reads no Role and records nothing.
/// </para>
/// <para>
/// <b>Nothing is sent</b>: an offer has exactly one email, the one that announced it. This handler takes no outbox, and a
/// gate holds that.
/// </para>
/// </remarks>
/// <param name="offers">Accepts the offer.</param>
public sealed class AcceptOwnershipOfferHandler(IOwnershipOfferStore offers)
{
    /// <summary>
    /// Accepts the offer.
    /// </summary>
    /// <param name="actorAccountId">The accepting Account, as the pipeline resolved it.</param>
    /// <param name="offerId">The offer, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened.</returns>
    public Task<OwnershipOfferAcceptance> HandleAsync(Guid actorAccountId, Guid offerId, CancellationToken cancellationToken) =>
        offers.AcceptAsync(actorAccountId, offerId, cancellationToken);
}
