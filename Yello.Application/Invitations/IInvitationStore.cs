using Yello.Domain.Invitations;
using Yello.Domain.Memberships;

namespace Yello.Application.Invitations;

/// <summary>
/// Issues Invitations in the Space the current unit of work is scoped to. FR-10; story 4.1.
/// </summary>
/// <remarks>
/// <b>One serialised decision.</b> The store takes the Space's Invitations lock, then reads whether the address already
/// holds a Membership here (AC4) and whether it already holds an actionable pending Invitation here (AC13), and only then
/// inserts - so a concurrent issue of the same address cannot slip past either read. It never asks whether an Account
/// exists for the address anywhere (AD-23).
/// </remarks>
public interface IInvitationStore
{
    /// <summary>
    /// Issues an Invitation, unless the address is already a Member of, or already invited to, the resolved Space.
    /// </summary>
    /// <param name="spaceId">The resolved Space, from the pipeline's context and never from the request.</param>
    /// <param name="issuerAccountId">The acting Account.</param>
    /// <param name="emailAddress">The address, already plausible.</param>
    /// <param name="role">The Role offered - never Owner.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the issue came to.</returns>
    Task<InvitationIssue> IssueAsync(
        Guid spaceId,
        Guid issuerAccountId,
        string emailAddress,
        Role role,
        CancellationToken cancellationToken);
}

/// <summary>
/// What an issue came to.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Invitation">The saved Invitation, with its database-stamped expiry, when issued.</param>
/// <param name="Token">The token's plaintext, when issued. It goes into the email and nowhere else.</param>
public sealed record InvitationIssue(InvitationIssueOutcome Outcome, Invitation? Invitation, string? Token)
{
    /// <summary>
    /// The address already holds a Membership in this Space.
    /// </summary>
    public static InvitationIssue AlreadyAMember { get; } = new(InvitationIssueOutcome.AlreadyAMember, null, null);

    /// <summary>
    /// The Space already holds an actionable pending Invitation for the address.
    /// </summary>
    public static InvitationIssue AlreadyPending { get; } = new(InvitationIssueOutcome.AlreadyPending, null, null);

    /// <summary>
    /// The Invitation was issued.
    /// </summary>
    /// <param name="invitation">The saved Invitation.</param>
    /// <param name="token">The token's plaintext.</param>
    /// <returns>The result.</returns>
    public static InvitationIssue Issued(Invitation invitation, string token) => new(InvitationIssueOutcome.Issued, invitation, token);
}

/// <summary>
/// The outcomes of an issue.
/// </summary>
public enum InvitationIssueOutcome
{
    /// <summary>
    /// Issued.
    /// </summary>
    Issued,

    /// <summary>
    /// Refused: the address is already a Member here (AC4). Disclosable - the issuer can see the Membership.
    /// </summary>
    AlreadyAMember,

    /// <summary>
    /// Refused: the address already holds an actionable pending Invitation here (AC13). Disclosable - the issuer sees it
    /// in 4.2's list.
    /// </summary>
    AlreadyPending,
}
