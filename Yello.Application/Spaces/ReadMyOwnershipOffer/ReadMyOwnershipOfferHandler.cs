namespace Yello.Application.Spaces.ReadMyOwnershipOffer;

/// <summary>
/// Reads the actionable Ownership Offer naming the caller's own Membership in the active Space: the offer, the offerer's
/// name as captured when it was made, the expiry, and the database clock's reading.
/// </summary>
/// <remarks>
/// <b>Writes nothing and reads no Role.</b> Anyone the offer does not name - the Owner included, who cannot be named - reads
/// none, the same as a Space with no offer.
/// </remarks>
/// <param name="offers">Reads the offer.</param>
public sealed class ReadMyOwnershipOfferHandler(IOwnershipOfferStore offers)
{
    /// <summary>
    /// Reads the offer.
    /// </summary>
    /// <param name="actorAccountId">The reading Account, as the pipeline resolved it.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The offer, or <c>null</c> when none names the caller.</returns>
    public Task<OwnershipOfferForRecipient?> HandleAsync(Guid actorAccountId, CancellationToken cancellationToken) =>
        offers.ReadMineAsync(actorAccountId, cancellationToken);
}
