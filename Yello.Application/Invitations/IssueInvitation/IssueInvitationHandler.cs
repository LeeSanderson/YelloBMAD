using Yello.Application.Outbox;
using Yello.Application.Spaces;
using Yello.Domain.Memberships;

namespace Yello.Application.Invitations.IssueInvitation;

/// <summary>
/// Issues an Invitation to an email address at a Role, and enqueues the one email that carries it. FR-10, FR-39;
/// AD-10, AD-23, AD-27; story 4.1.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything cross-cutting happened before this ran</b> (AR-3): the pipeline resolved the Space, checked
/// <c>IssueAndRevokeInvitations</c> against the matrix and replayed an <c>Idempotency-Key</c> it had already seen. This
/// slice reads the Space and the acting Account off the resolved context - never its Role - and consults no bound (the
/// 100-Membership bound lands on acceptance, story 4.3).
/// </para>
/// <para>
/// <b>No existence branch</b> (AD-23). Nothing here asks whether an Account exists for the address. An issue to an
/// address with an Account and one without take the same path: the same lock, the same two reads, the same token, the
/// same insert, the same rendering and the same outbox row.
/// </para>
/// <para>
/// <b>One unit of work.</b> The Invitation and its outbox row are saved in the request's transaction, which the
/// pipeline commits after this returns; the dispatcher sends only once that commit has happened.
/// </para>
/// </remarks>
/// <param name="store">Issues the Invitation.</param>
/// <param name="spaceNames">Reads the resolved Space's name, for the email.</param>
/// <param name="links">Builds the email's link.</param>
/// <param name="outbox">Enqueues the email.</param>
public sealed class IssueInvitationHandler(
    IInvitationStore store,
    IActiveSpaceNameReader spaceNames,
    InvitationLinks links,
    IOutbox outbox)
{
    /// <summary>
    /// Issues the Invitation.
    /// </summary>
    /// <param name="context">The active Space, as the pipeline resolved it on this request.</param>
    /// <param name="emailAddress">The address, already plausible.</param>
    /// <param name="role">The Role offered - Admin, Member or Viewer (FR-10; Lee's Q2).</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened.</returns>
    public Task<IssueInvitationResult> HandleAsync(
        ActiveSpaceContext context,
        string emailAddress,
        Role role,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);

        // The endpoint refuses Owner before this runs; an Owner offer reaching here is a wiring defect (FR-42).
        if (role == Role.Owner)
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "An Invitation is never issued at Owner.");
        }

        return IssueAsync(context.SpaceId, context.AccountId, emailAddress, role, cancellationToken);
    }

    private async Task<IssueInvitationResult> IssueAsync(
        Guid spaceId,
        Guid accountId,
        string emailAddress,
        Role role,
        CancellationToken cancellationToken)
    {
        var issue = await store.IssueAsync(spaceId, accountId, emailAddress, role, cancellationToken).ConfigureAwait(false);

        if (issue is not { Outcome: InvitationIssueOutcome.Issued, Invitation: { } invitation, Token: { } token })
        {
            return new IssueInvitationResult(issue.Outcome);
        }

        // Null only when the Space was deleted after the pipeline resolved it (story 3.3); the caller answers the
        // boundary's 404 and the unit of work rolls back the Invitation with it.
        if (await spaceNames.ReadAsync(cancellationToken).ConfigureAwait(false) is not { } spaceName)
        {
            return IssueInvitationResult.SpaceGone;
        }

        var email = InvitationEmail.Render(spaceName, role.ToString(), invitation.IssuerName, links.For(token), invitation.ExpiresAt);

        await outbox.EnqueueAsync(
            new OutboxEmail(OutboxKinds.InvitationIssued, invitation.EmailAddress, email.Subject, email.Body),
            cancellationToken).ConfigureAwait(false);

        return new IssueInvitationResult(InvitationIssueOutcome.Issued);
    }
}

/// <summary>
/// The outcome of <see cref="IssueInvitationHandler.HandleAsync"/>.
/// </summary>
/// <param name="Outcome">What happened, or <c>null</c> when the Space was gone.</param>
public sealed record IssueInvitationResult(InvitationIssueOutcome? Outcome)
{
    /// <summary>
    /// The Space was deleted after the pipeline resolved it.
    /// </summary>
    public static IssueInvitationResult SpaceGone { get; } = new(Outcome: null);
}
