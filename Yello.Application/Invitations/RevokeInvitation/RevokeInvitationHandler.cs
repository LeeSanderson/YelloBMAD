namespace Yello.Application.Invitations.RevokeInvitation;

/// <summary>
/// Revokes a pending Invitation in the active Space. FR-12, FR-10, AD-26, AD-27; story 4.2, AC2, AC3, AC4, AC5, AC7.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything cross-cutting happened before this ran</b> (AR-3): the pipeline resolved the Space, checked
/// <c>IssueAndRevokeInvitations</c> and replayed an <c>Idempotency-Key</c> it had already seen. FR-12 grants the revoke
/// to every Owner and Admin, so this slice never compares the caller with the issuer and reads no Role.
/// </para>
/// <para>
/// <b>The issuer's standing does not matter</b> (FR-12, AD-5): "An Invitation remains valid if its issuer later loses the
/// capability to have issued it - by demotion, by removal, or by Account deletion. ... Any remaining Owner or Admin may
/// revoke it." It was legitimately issued, and does not depend on its issuer's continuing authority.
/// </para>
/// <para>
/// <b>Only before acceptance.</b> An accepted Invitation is refused, because removing an accepted invitee is FR-14, not
/// FR-12; so are a revoked and an expired one (AD-27 applies the actionable predicate to every transition).
/// </para>
/// <para>
/// <b>No email</b> (AC7; FR-39, "exactly one email per Invitation"): the one already delivered cannot be recalled. This
/// handler takes no outbox, and a gate holds that.
/// </para>
/// </remarks>
/// <param name="store">Revokes the Invitation.</param>
public sealed class RevokeInvitationHandler(IInvitationStore store)
{
    /// <summary>
    /// Revokes the Invitation.
    /// </summary>
    /// <param name="invitationId">The Invitation, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened.</returns>
    public Task<InvitationRevocation> HandleAsync(Guid invitationId, CancellationToken cancellationToken) =>
        store.RevokeAsync(invitationId, cancellationToken);
}
