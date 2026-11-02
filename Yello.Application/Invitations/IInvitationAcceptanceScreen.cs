namespace Yello.Application.Invitations;

/// <summary>
/// The accept's guard read, exposed to the request pipeline so it can consult the Memberships-per-Space bound after the
/// token has resolved to an actionable Invitation addressed to the caller, and before anything is written. Story 4.3,
/// AC9, open question 9 (a); AR-3, AD-25.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the pipeline reads it, and not the slice.</b> AR-3 makes the bound the pipeline's: a slice never counts. Q9 (a)
/// orders the checks lock, then actionable and addressed, then the bound, then the write - so a holder of someone else's
/// token, or of a dead one, never learns that the Space is full. The pipeline asks this port whether the accept would be
/// admitted, consults the bound only when it would, and then runs the slice, whose own guarded statement decides again.
/// </para>
/// <para>
/// <b>It takes the Space's Invitations lock</b> (open question 8 (a)), held to the unit of work's end, so the count that
/// follows and the slice's write see every acceptance and every issue committed before it.
/// </para>
/// </remarks>
public interface IInvitationAcceptanceScreen
{
    /// <summary>
    /// Takes the Space's Invitations lock and reads what the accept would come to, writing nothing.
    /// </summary>
    /// <param name="context">The unit of work's context, with the Session's Account.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the accept would come to before the bound.</returns>
    Task<InvitationAcceptanceOutcome> ScreenAsync(InvitationContext context, CancellationToken cancellationToken);
}
