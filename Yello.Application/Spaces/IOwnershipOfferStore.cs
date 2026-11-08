namespace Yello.Application.Spaces;

/// <summary>
/// Makes, reads and revokes the Ownership Offer of the Space the current unit of work is scoped to. FR-8, AD-26, AD-27.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every write is one guarded statement under the Space's Memberships lock</b>, the lock every Membership write already
/// takes, so an offer and the removal of its recipient serialise. The offer proves in its own <c>WHERE</c> that the recipient
/// is a non-Owner Membership of this Space and that the caller is its Owner; the slice reads no Role.
/// </para>
/// <para>
/// <b>At most one pending offer per Space</b> is the schema's guarantee, a filtered unique index; the store answers its
/// violation as <see cref="OwnershipOfferOutcome.AlreadyPending"/>, never a failure.
/// </para>
/// </remarks>
public interface IOwnershipOfferStore
{
    /// <summary>
    /// Makes an offer to <paramref name="recipientMembershipId"/>, first lapsing any pending offer of this Space whose expiry
    /// has passed, so a stale one never holds the Space's one pending place.
    /// </summary>
    /// <param name="actorAccountId">The offering Account, which must be this Space's Owner when the statement runs.</param>
    /// <param name="recipientMembershipId">The recipient, from the body.</param>
    /// <param name="offererName">The offering Owner's display name, captured as the offer's attribution.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the offer came to, with the new offer's id when it was made.</returns>
    Task<OwnershipOfferMade> OfferAsync(Guid actorAccountId, Guid recipientMembershipId, string offererName, CancellationToken cancellationToken);

    /// <summary>
    /// The Space's actionable pending offer, with the database clock's reading taken in the same statement; <c>null</c> when
    /// there is none. Writes nothing.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The offer, or <c>null</c>.</returns>
    Task<PendingOwnershipOffer?> ReadPendingAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Revokes an actionable pending offer of this Space. The row is kept, set <c>Revoked</c>.
    /// </summary>
    /// <param name="offerId">The offer, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the revoke came to.</returns>
    Task<OwnershipOfferRevocation> RevokeAsync(Guid offerId, CancellationToken cancellationToken);
}

/// <summary>
/// What an offer came to.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="OfferId">The new offer, when it was made.</param>
public sealed record OwnershipOfferMade(OwnershipOfferOutcome Outcome, Guid? OfferId);

/// <summary>
/// The outcomes of an offer.
/// </summary>
public enum OwnershipOfferOutcome
{
    /// <summary>
    /// Made: one <c>Pending</c> offer naming the recipient.
    /// </summary>
    Offered,

    /// <summary>
    /// Another offer of this Space is still pending. Nothing written.
    /// </summary>
    AlreadyPending,

    /// <summary>
    /// The recipient is not another Membership of this Space: unknown, another Space's, or the Owner's own. Nothing written.
    /// </summary>
    InvalidRecipient,

    /// <summary>
    /// The caller's own Membership was gone when the offer was written: the pipeline answers it as the boundary.
    /// </summary>
    LostMembership,

    /// <summary>
    /// The Space was deleted after the pipeline resolved it: the boundary's <c>404</c>.
    /// </summary>
    SpaceGone,
}

/// <summary>
/// A pending offer as the Owner reads it, before the recipient's name is added.
/// </summary>
/// <param name="Id">The offer.</param>
/// <param name="RecipientMembershipId">The recipient.</param>
/// <param name="ExpiresAt">When it stops being actionable, by the database clock.</param>
/// <param name="ReadAt">The database clock when it was read.</param>
public sealed record PendingOwnershipOffer(Guid Id, Guid RecipientMembershipId, DateTimeOffset ExpiresAt, DateTimeOffset ReadAt);

/// <summary>
/// A pending offer as it is answered: the stored offer and the recipient's display name now.
/// </summary>
/// <param name="Id">The offer.</param>
/// <param name="RecipientMembershipId">The recipient.</param>
/// <param name="RecipientName">The recipient's display name.</param>
/// <param name="ExpiresAt">When it stops being actionable, by the database clock.</param>
/// <param name="ReadAt">The database clock when it was read.</param>
public sealed record OwnershipOfferView(Guid Id, Guid RecipientMembershipId, string RecipientName, DateTimeOffset ExpiresAt, DateTimeOffset ReadAt);

/// <summary>
/// What a revoke came to.
/// </summary>
public enum OwnershipOfferRevocation
{
    /// <summary>
    /// The offer was actionable and is now <c>Revoked</c>.
    /// </summary>
    Revoked,

    /// <summary>
    /// The offer exists in this Space but is no longer actionable. Nothing written.
    /// </summary>
    NotPending,

    /// <summary>
    /// The id names no offer in this Space - never existed, or another Space's, which row-level security hides.
    /// </summary>
    NotFound,
}
