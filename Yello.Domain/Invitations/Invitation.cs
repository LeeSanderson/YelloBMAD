using Yello.Domain.Memberships;

namespace Yello.Domain.Invitations;

/// <summary>
/// An offer of Membership in exactly one Space at exactly one Role, addressed to an email address. FR-10, FR-39; AD-2,
/// AD-27, AD-28; story 4.1.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its Space and its Role are fixed at issue.</b> <see cref="SpaceId"/> and <see cref="Role"/> are <c>init</c>-only;
/// the database refuses a change of Space with a <c>BLOCK ... AFTER UPDATE</c> predicate; and no route or contract
/// changes either once the row exists.
/// </para>
/// <para>
/// <b>Expiry is derived, never stored</b> (AD-27). <see cref="State"/> holds <see cref="InvitationState.Pending"/>,
/// <see cref="InvitationState.Accepted"/> or <see cref="InvitationState.Revoked"/>; an Invitation is expired when it is
/// <c>Pending</c> and <see cref="ExpiresAt"/> has passed by the database clock. <see cref="ExpiresAt"/> is stamped by the
/// database itself at insert (a column default seven days after <c>SYSDATETIMEOFFSET()</c>), so the stamp and the shared
/// actionable predicate read one clock.
/// </para>
/// <para>
/// <b>The token is never stored</b>: <see cref="TokenHash"/> is the SHA-256 of the plaintext, which exists only in the
/// rendered email until it is sent. The token identifies which offer is in play and is never the authority for accepting
/// it (AD-28).
/// </para>
/// <para>
/// <b>The issuer is an Account, not a Membership</b> (FR-12, AD-5): an Invitation survives its issuer's demotion and
/// removal, and <see cref="IssuerName"/> is the attribution captured at issue. FR-12: "An Invitation remains valid if its
/// issuer later loses the capability to have issued it - by demotion, by removal, or by Account deletion. ... Any
/// remaining Owner or Admin may revoke it." It was legitimately issued, and does not depend on its issuer's continuing
/// authority, so neither the pending list nor the revoke (story 4.2) reads the issuer.
/// </para>
/// <para>
/// <b>Terminal states are retained</b> (FR-10, AD-27): a revoke sets <see cref="InvitationState.Revoked"/> and keeps the
/// row; nothing deletes an Invitation but its Space's deletion. No product surface shows a terminal one; an operator can
/// derive SM-4 and SM-C3 from the rows with the same predicate.
/// </para>
/// </remarks>
public sealed class Invitation
{
    private const int LongestIssuerName = 128;

    /// <summary>
    /// The longest issuer name the column holds: the Account display name's 128.
    /// </summary>
    public static int IssuerNameMaxLength => LongestIssuerName;

    /// <summary>
    /// The Invitation's identity, generated application-side (AR-34).
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The one Space the Invitation offers. Non-nullable (AD-2), and set once.
    /// </summary>
    public required Guid SpaceId { get; init; }

    /// <summary>
    /// The address as the issuer submitted it, trimmed - for display to the Space's Owners and Admins only (§6.1).
    /// </summary>
    public required string EmailAddress { get; init; }

    /// <summary>
    /// The address through <c>EmailAddressNormalisation.Normalise</c>: the comparable form, matching the Account column's
    /// type and collation exactly.
    /// </summary>
    public required string NormalizedEmailAddress { get; init; }

    /// <summary>
    /// The one Role offered - never <see cref="Memberships.Role.Owner"/> (FR-10, FR-42). Set once.
    /// </summary>
    public required Role Role { get; init; }

    /// <summary>
    /// The stored state. Written only by the Invitation store: the issue inserts <see cref="InvitationState.Pending"/>
    /// (story 4.1) and the revoke's guarded update sets <see cref="InvitationState.Revoked"/> (story 4.2); a gate holds that.
    /// </summary>
    public required InvitationState State { get; set; }

    /// <summary>
    /// When the offer stops being actionable, stamped by the database clock at insert. Compared only by the shared
    /// actionable predicate (AD-27).
    /// </summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// The SHA-256 of the token the email carries: 32 bytes, unique.
    /// </summary>
    public required byte[] TokenHash { get; init; }

    /// <summary>
    /// The Account that issued the Invitation.
    /// </summary>
    public required Guid IssuedByAccountId { get; init; }

    /// <summary>
    /// The issuer's display name as it stood at issue.
    /// </summary>
    /// <remarks>
    /// <b>The rule for every piece of authored content</b> (UX-DR34, <c>EXPERIENCE.md:156</c>; story 4.4, AC7): attribution
    /// uses the name captured at authoring time, never a live Account lookup, because resolving a current name globally
    /// would carry a later name change into a Space the reader no longer shares with that person. Story 7.2's description
    /// authorship inherits it. Only the Membership list reads <c>Account.DisplayName</c> live, for current Members (Lee's 4.4
    /// Q5 (a)); <c>RefusalBoundaryGateTests.Attribution_never_reads_a_live_Account_name</c> holds the named readers.
    /// </remarks>
    public required string IssuerName { get; init; }

    /// <summary>
    /// When the Invitation was issued, in UTC.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
