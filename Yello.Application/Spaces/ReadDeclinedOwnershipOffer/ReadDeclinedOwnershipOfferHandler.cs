using Yello.Application.Memberships;

namespace Yello.Application.Spaces.ReadDeclinedOwnershipOffer;

/// <summary>
/// Tells the active Space's Owner whether its most recent Ownership Offer was declined, and by whom.
/// </summary>
/// <remarks>
/// <b>Writes nothing, emits nothing.</b> Declining sends no email, so the Owner learns of it only by this read.
/// </remarks>
/// <param name="offers">Reads the offer.</param>
/// <param name="memberships">Reads the recipient's display name.</param>
public sealed class ReadDeclinedOwnershipOfferHandler(IOwnershipOfferStore offers, IMembershipStore memberships)
{
    /// <summary>
    /// Reads the declined offer.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The declined offer, or <c>null</c> when the most recent offer was not declined.</returns>
    public async Task<DeclinedOwnershipOfferView?> HandleAsync(CancellationToken cancellationToken)
    {
        if (await offers.ReadLastDeclinedAsync(cancellationToken).ConfigureAwait(false) is not { } declined)
        {
            return null;
        }

        // A retained offer outlives the Membership it names, so the name may be gone.
        var name = await memberships.DisplayNameOfAsync(declined.RecipientMembershipId, cancellationToken).ConfigureAwait(false);

        return new DeclinedOwnershipOfferView(declined.Id, name);
    }
}
