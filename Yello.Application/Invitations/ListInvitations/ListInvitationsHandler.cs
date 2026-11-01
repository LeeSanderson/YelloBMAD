namespace Yello.Application.Invitations.ListInvitations;

/// <summary>
/// Lists the active Space's pending Invitations: address, Role, issuer and issue date, newest first. FR-12, UX-DR14,
/// AD-27; story 4.2, AC1, AC3, AC6.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything cross-cutting happened before this ran</b> (AR-3): the pipeline resolved the Space and checked
/// <c>IssueAndRevokeInvitations</c> - the list carries email addresses, which section 6.1 and AD-23 confine to Owners and
/// Admins. This slice reads no Role and no Space; both isolation layers already narrow the store to the resolved Space.
/// </para>
/// <para>
/// <b>"Pending" means actionable</b> (open question 4 (a)): the store reads through the one shared predicate, so an
/// accepted, revoked or expired Invitation is never listed. Terminal rows are retained, and no product surface shows them
/// (FR-10).
/// </para>
/// <para>
/// <b>Nothing depends on the issuer</b> (FR-12: "An Invitation remains valid if its issuer later loses the capability to
/// have issued it - by demotion, by removal, or by Account deletion"). The name shown is the one captured at issue, never
/// a live Account lookup, and the row says nothing about the issuer's current standing, which would disclose a Membership
/// change to the reader.
/// </para>
/// </remarks>
/// <param name="store">Reads the Invitations.</param>
public sealed class ListInvitationsHandler(IInvitationStore store)
{
    /// <summary>
    /// Lists the Invitations.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The actionable pending Invitations, newest first.</returns>
    public Task<IReadOnlyList<PendingInvitation>> HandleAsync(CancellationToken cancellationToken) =>
        store.ListPendingAsync(cancellationToken);
}
