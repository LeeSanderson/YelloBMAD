using Yello.Domain.Invitations;
using Yello.Domain.Memberships;

namespace Yello.Application.Invitations;

/// <summary>
/// Issues, lists, revokes, shows and accepts Invitations in the Space the current unit of work is scoped to. FR-10,
/// FR-11, FR-12; stories 4.1, 4.2 and 4.3.
/// </summary>
/// <remarks>
/// <para>
/// <b>One serialised decision on issue.</b> The store takes the Space's Invitations lock, then reads whether the address
/// already holds a Membership here (AC4) and whether it already holds an actionable pending Invitation here (AC13), and
/// only then inserts - so a concurrent issue of the same address cannot slip past either read. It never asks whether an
/// Account exists for the address anywhere (AD-23).
/// </para>
/// <para>
/// <b>One guarded statement on revoke</b> (story 4.2, open question 10 (a)): the state changes only where the shared
/// actionable predicate still holds, inside the statement's own <c>WHERE</c>, so no check-then-write window exists.
/// </para>
/// </remarks>
public interface IInvitationStore
{
    /// <summary>
    /// The resolved Space's actionable pending Invitations, newest first (story 4.2, AC1, AC6). Terminal and expired ones
    /// are never returned.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Invitations.</returns>
    Task<IReadOnlyList<PendingInvitation>> ListPendingAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Revokes an actionable pending Invitation in the resolved Space (story 4.2, AC2, AC4). The row is kept, set
    /// <c>Revoked</c>; nothing is deleted (AD-27).
    /// </summary>
    /// <param name="invitationId">The Invitation.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the revoke came to.</returns>
    Task<InvitationRevocation> RevokeAsync(Guid invitationId, CancellationToken cancellationToken);

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

    /// <summary>
    /// The offer an actionable Invitation makes, read through the shared actionable predicate on the Invitation unit of
    /// work (story 4.3, AC1, AC2): the Space's name, the Role and the issuer's captured name, and - with a Session - whether
    /// the Invitation names the caller's address. <c>null</c> for anything that is not an actionable Invitation. Writes
    /// nothing.
    /// </summary>
    /// <param name="context">The Invitation unit of work's context.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The offer, or <c>null</c>.</returns>
    Task<InvitationOffer?> ReadOfferAsync(InvitationContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Accepts the Invitation as the Session's Account (story 4.3, AC4, AC5): under the Space's Invitations lock, one
    /// guarded <c>UPDATE</c> composed with the shared actionable predicate and the caller's own normalised address, then -
    /// only on a rowcount of one - one Membership at the Invitation's Role. The token is never the authority.
    /// </summary>
    /// <param name="context">The Invitation unit of work's context; its Account is required.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the accept came to.</returns>
    Task<InvitationAcceptanceOutcome> AcceptAsync(InvitationContext context, CancellationToken cancellationToken);
}

/// <summary>
/// What an actionable Invitation offers, as the view reads it. Story 4.3, AC2.
/// </summary>
/// <param name="SpaceName">The invited Space's name.</param>
/// <param name="Role">The Role offered.</param>
/// <param name="IssuerName">The issuer's display name as captured at issue.</param>
/// <param name="AddressedToYou">With a Session, whether the Invitation names its Account's address; otherwise <c>null</c>.</param>
public sealed record InvitationOffer(string SpaceName, Role Role, string IssuerName, bool? AddressedToYou);

/// <summary>
/// What an accept came to, or - from the screen - would come to before the bound. Story 4.3, AC4-AC7, open question 12.
/// </summary>
public enum InvitationAcceptanceOutcome
{
    /// <summary>
    /// Accepted: the Invitation is <c>Accepted</c> and the caller holds one Membership at its Role. From the screen: the
    /// accept would be admitted, subject to the bound.
    /// </summary>
    Accepted,

    /// <summary>
    /// The token names no actionable Invitation - revoked, accepted, expired, never issued, or gone with its Space. The one
    /// uniform answer (AC6); nothing written.
    /// </summary>
    NotValid,

    /// <summary>
    /// The Invitation is actionable but names another address than the caller's (AC7). Nothing written.
    /// </summary>
    ForAnotherAccount,

    /// <summary>
    /// The caller already holds a Membership in the Space (open question 12 (a)). Nothing written.
    /// </summary>
    AlreadyAMember,
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
    /// in the Space's pending list (story 4.2).
    /// </summary>
    AlreadyPending,
}

/// <summary>
/// One actionable pending Invitation, as the list reads it: exactly the five columns the list answers. Story 4.2, AC1.
/// </summary>
/// <param name="Id">The Invitation's id.</param>
/// <param name="EmailAddress">The address as issued, trimmed.</param>
/// <param name="Role">The Role offered.</param>
/// <param name="IssuerName">The issuer's display name as captured at issue.</param>
/// <param name="IssuedAt">The row's <c>CreatedAt</c>.</param>
public sealed record PendingInvitation(Guid Id, string EmailAddress, Role Role, string IssuerName, DateTimeOffset IssuedAt);

/// <summary>
/// What a revoke came to. Story 4.2, AC2, AC4.
/// </summary>
public enum InvitationRevocation
{
    /// <summary>
    /// The Invitation was actionable and is now <c>Revoked</c>.
    /// </summary>
    Revoked,

    /// <summary>
    /// The Invitation exists in this Space but is accepted, already revoked, or expired: nothing was written (AD-26).
    /// </summary>
    NotPending,

    /// <summary>
    /// The id names no Invitation in this Space - never existed, or another Space's, which row-level security hides.
    /// </summary>
    NotFound,
}
