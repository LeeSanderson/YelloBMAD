namespace Yello.Application.Invitations.AcceptInvitation;

/// <summary>
/// Accepts an Invitation as the signed-in Account: exactly one Membership, in exactly the invited Space, at exactly the
/// invited Role. FR-11, AD-22, AD-27, AD-28; story 4.3, AC3-AC7.
/// </summary>
/// <remarks>
/// <para>
/// <b>The token identifies the offer; the Account is the authority</b> (FR-11 <c>:277</c>, AD-28). The store's one
/// guarded statement requires the caller's own normalised address, read from the caller's Account row - never from the
/// request - so a link holder who is not the addressee joins nothing, and a different Account signed in is told so
/// without either address (AC7: "the helpful version discloses the invited address to whoever holds the link, which on a
/// forwarded link is a stranger").
/// </para>
/// <para>
/// <b>Never provisions</b> (AD-22): exactly one slice creates an Account, and an invitee without one registers through it
/// first (Lee's Q3 (a)); the invited Space's Membership is a separate, additional Membership. This handler takes no outbox
/// and publishes nothing (open question 14 (a): no <c>MembershipChanged</c> publisher exists before story 7.7).
/// </para>
/// <para>
/// <b>Everything cross-cutting happened before this ran</b> (AR-3): the pipeline resolved the token, opened the Invitation
/// unit of work, replayed an <c>Idempotency-Key</c> it had already answered, and consulted the Memberships-per-Space bound.
/// This slice reads no Role and counts nothing.
/// </para>
/// </remarks>
/// <param name="store">Accepts the Invitation.</param>
public sealed class AcceptInvitationHandler(IInvitationStore store)
{
    /// <summary>
    /// Accepts the Invitation.
    /// </summary>
    /// <param name="context">The Invitation unit of work's context, from the pipeline; its Account is the Session's.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the accept came to.</returns>
    public Task<InvitationAcceptanceOutcome> HandleAsync(InvitationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.AccountId is null
            ? throw new InvalidOperationException("An Invitation is accepted only by a signed-in Account; the pipeline refuses a request without a Session.")
            : store.AcceptAsync(context, cancellationToken);
    }
}
