namespace Yello.Domain.Invitations;

/// <summary>
/// The stored states of an <see cref="Invitation"/>. Story 4.1, open question 11 (a).
/// </summary>
/// <remarks>
/// <b>"Expired" is not one of them, and never will be</b> (AD-27): an Invitation is expired when it is
/// <see cref="Pending"/> and its <c>ExpiresAt</c> has passed by the database clock, which is derived on read. Nothing
/// lapses an Invitation by event either (FR-12), so there is no <c>Lapsed</c>. Stored as the member's name.
/// </remarks>
public enum InvitationState
{
    /// <summary>
    /// Issued and not yet accepted or revoked.
    /// </summary>
    Pending,

    /// <summary>
    /// Accepted (story 4.3): written by the accept's one guarded statement in <c>InvitationStore</c>, beside the caller's
    /// one new Membership. Terminal.
    /// </summary>
    Accepted,

    /// <summary>
    /// Revoked by an Owner or Admin before acceptance (story 4.2): set by the guarded revoke, and retained.
    /// </summary>
    Revoked,
}
