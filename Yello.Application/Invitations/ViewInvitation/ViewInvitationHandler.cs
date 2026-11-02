namespace Yello.Application.Invitations.ViewInvitation;

/// <summary>
/// Shows what an Invitation offers: the Space's name, the Role and the issuer's captured name. FR-11, AD-28, UX-DR18;
/// story 4.3, AC1, AC2, AC7.
/// </summary>
/// <remarks>
/// <para>
/// <b>A safe, side-effect-free read</b> (FR-11 <c>:277</c>): a mail scanner, a link prefetcher or anyone the mail was
/// forwarded to may fetch it, so it writes nothing - no Invitation column, no Membership, no refusal record, no outbox
/// row, no idempotency record - and its unit of work is never committed.
/// </para>
/// <para>
/// <b>Everything cross-cutting happened before this ran</b> (AR-3): the pipeline resolved the token to its Space through
/// the token index and opened the Invitation unit of work keyed to it. This slice reads no Role and counts nothing.
/// </para>
/// <para>
/// <b>Not a credential check.</b> It answers the same offer to no Session, any Session and a bearer Token; only the
/// addressee indicator reads a Session (Lee's Q5 (a)). The mismatch is stated without either address, because the
/// helpful version "discloses the invited address to whoever holds the link, which on a forwarded link is a stranger"
/// (<c>EXPERIENCE.md:265</c>).
/// </para>
/// </remarks>
/// <param name="store">Reads the offer.</param>
public sealed class ViewInvitationHandler(IInvitationStore store)
{
    /// <summary>
    /// Reads the offer.
    /// </summary>
    /// <param name="context">The Invitation unit of work's context, from the pipeline.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The offer, or <c>null</c> when the token names no actionable Invitation.</returns>
    public Task<InvitationOffer?> HandleAsync(InvitationContext context, CancellationToken cancellationToken) =>
        store.ReadOfferAsync(context, cancellationToken);
}
