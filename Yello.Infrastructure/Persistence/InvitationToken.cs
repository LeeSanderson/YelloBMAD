namespace Yello.Infrastructure.Persistence;

/// <summary>
/// One row of the Invitation token index: a token's hash, and the Invitation and Space it was issued for. Story 4.3,
/// Lee's Q1 (a); a named AD-2 variance beside <c>ApiToken.BoundSpaceId</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it exists.</b> The Invitation view and accept carry only the token, and <c>Invitation</c> is under the Space
/// policy: with no Space key set, a lookup by hash sees nothing. This table sits outside the policy, so a hash resolves to
/// its Space before any Space context exists - the one read (<see cref="InvitationScope"/>) that then sets the
/// <c>SpaceId</c> key to the Space stored here, never to anything the request carried.
/// </para>
/// <para>
/// <b>What it discloses, and to whom.</b> Hash to Invitation and Space ids, and only to a caller who already holds the
/// 256-bit token whose SHA-256 it is. Nothing else about the Invitation is here; everything else is read under the Space
/// key, through the policy and the query filter, by the Invitation store.
/// </para>
/// <para>
/// <b>Written in the issue's transaction, deleted with its Invitation.</b> The Invitation store inserts it beside the
/// Invitation; its foreign key to <c>Invitation (SpaceId, Id)</c> cascades, so deleting a Space - one statement - takes it
/// with the Invitation, and a deleted Space's link then resolves to nothing (ledger <c>:722</c>). Its Space column is
/// <c>OfferedSpaceId</c>, not <c>SpaceId</c>, for the reason <c>ApiToken.BoundSpaceId</c>'s is: it is not a row of that
/// Space's content, and the policy does not cover it.
/// </para>
/// </remarks>
internal sealed class InvitationToken
{
    /// <summary>
    /// The SHA-256 of the token's plaintext (unique).
    /// </summary>
    internal required byte[] TokenHash { get; init; }

    /// <summary>
    /// The Invitation the token was issued for.
    /// </summary>
    internal required Guid InvitationId { get; init; }

    /// <summary>
    /// The Space the Invitation offers, as stored at issue.
    /// </summary>
    internal required Guid OfferedSpaceId { get; init; }
}
