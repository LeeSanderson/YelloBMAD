namespace Yello.Application.Spaces;

/// <summary>
/// Makes, reads, revokes, accepts and declines the Ownership Offer of the Space the current unit of work is scoped to.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every write is one guarded statement under the Space's Memberships lock</b>, the lock every Membership write already
/// takes, so an offer and the removal of its recipient serialise. The offer proves in its own <c>WHERE</c> that the recipient
/// is a non-Owner Membership of this Space and that the caller is its Owner; the slice reads no Role.
/// </para>
/// <para>
/// <b>The recipient's read, acceptance and decline are decided by row identity, never by Role</b>: each statement's own
/// <c>WHERE</c> holds that the offer names the caller's Membership in this Space.
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
    /// Revokes an actionable pending offer of this Space, while the caller is still its Owner. The row is kept, set
    /// <c>Revoked</c>.
    /// </summary>
    /// <param name="actorAccountId">The revoking Account, which must be this Space's Owner when the statement runs.</param>
    /// <param name="offerId">The offer, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the revoke came to.</returns>
    Task<OwnershipOfferRevocation> RevokeAsync(Guid actorAccountId, Guid offerId, CancellationToken cancellationToken);

    /// <summary>
    /// The actionable offer naming the caller's own Membership, with the database clock's reading taken in the same
    /// statement; <c>null</c> when none names it. Writes nothing.
    /// </summary>
    /// <param name="actorAccountId">The reading Account.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The offer, or <c>null</c>.</returns>
    Task<OwnershipOfferForRecipient?> ReadMineAsync(Guid actorAccountId, CancellationToken cancellationToken);

    /// <summary>
    /// Accepts an actionable offer naming the caller's own Membership: the offer turns <c>Accepted</c>, the offering Owner
    /// becomes an <c>Admin</c> and the caller the <c>Owner</c>, all in the request's one transaction, and both Accounts'
    /// <c>MembershipChanged</c> is recorded to publish after the commit.
    /// </summary>
    /// <param name="actorAccountId">The accepting Account, which must hold the Membership the offer names.</param>
    /// <param name="offerId">The offer, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the acceptance came to.</returns>
    Task<OwnershipOfferAcceptance> AcceptAsync(Guid actorAccountId, Guid offerId, CancellationToken cancellationToken);

    /// <summary>
    /// Declines an actionable offer naming the caller's own Membership. The row is kept, set <c>Declined</c>; no Role moves.
    /// </summary>
    /// <param name="actorAccountId">The declining Account, which must hold the Membership the offer names.</param>
    /// <param name="offerId">The offer, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the decline came to.</returns>
    Task<OwnershipOfferDecline> DeclineAsync(Guid actorAccountId, Guid offerId, CancellationToken cancellationToken);
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

    /// <summary>
    /// The caller is still a Member here but no longer the Owner - ownership moved while its request waited. The recorded
    /// <c>403</c>, as its next request would be refused. Nothing written.
    /// </summary>
    NoLongerOwner,
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

    /// <summary>
    /// The caller is still a Member here but no longer the Owner - ownership moved while its request waited. The recorded
    /// <c>403</c>. Nothing written.
    /// </summary>
    NoLongerOwner,
}

/// <summary>
/// The actionable offer naming the caller, as its recipient reads it.
/// </summary>
/// <param name="Id">The offer.</param>
/// <param name="OffererName">The offering Owner's display name, as captured on the offer.</param>
/// <param name="ExpiresAt">When it stops being actionable, by the database clock.</param>
/// <param name="ReadAt">The database clock when it was read.</param>
public sealed record OwnershipOfferForRecipient(Guid Id, string OffererName, DateTimeOffset ExpiresAt, DateTimeOffset ReadAt);

/// <summary>
/// What an acceptance came to.
/// </summary>
public enum OwnershipOfferAcceptance
{
    /// <summary>
    /// The offer is <c>Accepted</c>, the caller the Owner, the offering Owner an Admin.
    /// </summary>
    Accepted,

    /// <summary>
    /// The offer names the caller but is no longer actionable. Nothing written.
    /// </summary>
    NotPending,

    /// <summary>
    /// No offer with this id names the caller in this Space - unknown, another Space's, or another Membership's. Answered as
    /// the in-Space not-found, so nothing is disclosed about whom an offer names.
    /// </summary>
    NotFound,

    /// <summary>
    /// The caller's own Membership was gone when the acceptance was written: the pipeline answers it as the boundary.
    /// </summary>
    LostMembership,

    /// <summary>
    /// The offer's offerer is no longer the Owner, so the terms it showed no longer hold. Everything rolls back.
    /// </summary>
    OfferStale,

    /// <summary>
    /// The promote met a second Owner, refused by the filtered unique index. Everything rolls back.
    /// </summary>
    OwnerRoleFixed,
}

/// <summary>
/// What a decline came to.
/// </summary>
public enum OwnershipOfferDecline
{
    /// <summary>
    /// The offer is <c>Declined</c>. No Role moved.
    /// </summary>
    Declined,

    /// <summary>
    /// The offer names the caller but is no longer actionable. Nothing written.
    /// </summary>
    NotPending,

    /// <summary>
    /// No offer with this id names the caller in this Space. Answered as the in-Space not-found.
    /// </summary>
    NotFound,

    /// <summary>
    /// The caller's own Membership was gone when the decline was written: the pipeline answers it as the boundary.
    /// </summary>
    LostMembership,
}
