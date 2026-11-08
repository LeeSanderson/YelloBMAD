using Yello.Application.Memberships;

namespace Yello.Application.Spaces.ReadOwnershipOffer;

/// <summary>
/// Reads the active Space's actionable pending Ownership Offer for its Owner: the recipient by id and current display name,
/// the expiry, and the database clock's reading, from which the client counts the days left.
/// </summary>
/// <remarks>
/// <b>Writes nothing.</b> A pending offer past its expiry reads as none, by the shared actionable predicate in the store's
/// own statement.
/// </remarks>
/// <param name="offers">Reads the offer.</param>
/// <param name="memberships">Reads the recipient's display name.</param>
public sealed class ReadOwnershipOfferHandler(IOwnershipOfferStore offers, IMembershipStore memberships)
{
    /// <summary>
    /// Reads the offer.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The offer, or <c>null</c> when none is actionable.</returns>
    public async Task<OwnershipOfferView?> HandleAsync(CancellationToken cancellationToken)
    {
        if (await offers.ReadPendingAsync(cancellationToken).ConfigureAwait(false) is not { } pending)
        {
            return null;
        }

        // A pending offer always names a current Membership: a removal or leave lapses it before the row goes.
        var name = await memberships.DisplayNameOfAsync(pending.RecipientMembershipId, cancellationToken).ConfigureAwait(false);

        return name is null
            ? null
            : new OwnershipOfferView(pending.Id, pending.RecipientMembershipId, name, pending.ExpiresAt, pending.ReadAt);
    }
}
