using Yello.Domain.Memberships;

namespace Yello.Application.Memberships;

/// <summary>
/// Lists the resolved Space's Memberships and changes one's Role, in the request's Space-scoped unit of work. FR-13,
/// UX-DR14; story 4.4, AC1, AC2, AC4-AC7.
/// </summary>
/// <remarks>
/// <para>
/// <b>The Role is read by the store, never by the slice</b> (AD-1, AR-3): the list returns it as its stable code inside a
/// read model, and the change receives the acting authority's Role sets and composes them into one guarded statement.
/// </para>
/// <para>
/// <b>One serialised decision on change</b> (Q17 (a)): the store takes the Space's Memberships lock, then runs the guarded
/// <c>UPDATE</c> - target in the authority's <c>From</c> set, new Role in its <c>To</c> set, a different Role, and the actor
/// still holding a Role in its <c>HeldBy</c> set - and only on a rowcount of zero reads why.
/// </para>
/// </remarks>
public interface IMembershipStore
{
    /// <summary>
    /// One page of the resolved Space's Memberships in join order (<c>CreatedAt</c>, then <c>Id</c>), and the Space's true
    /// Membership count. AC1, Q8 (a).
    /// </summary>
    /// <param name="callerAccountId">The acting Account, which marks its own row.</param>
    /// <param name="after">The last row of the previous page, or <c>null</c> for the first.</param>
    /// <param name="take">How many rows to read at most.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The rows and the total.</returns>
    Task<(IReadOnlyList<ListedMembership> Rows, int Total)> ListAsync(
        Guid callerAccountId,
        MembershipCursor? after,
        int take,
        CancellationToken cancellationToken);

    /// <summary>
    /// Changes a Membership's Role within the actor's authority (AC4, AC5; Lee's Q1 (a)). On a committed change the target's
    /// Account is recorded for a <c>MembershipChanged</c> publish after commit (AC6).
    /// </summary>
    /// <param name="actorAccountId">The acting Account.</param>
    /// <param name="membershipId">The target, from the route.</param>
    /// <param name="proposed">The Role asked for - never Owner.</param>
    /// <param name="authority">The acting authority's Role sets, from the pipeline.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the change came to, with the target's row as it now stands.</returns>
    Task<RoleChangeResult> ChangeRoleAsync(
        Guid actorAccountId,
        Guid membershipId,
        Role proposed,
        RoleChangeAuthority authority,
        CancellationToken cancellationToken);

    /// <summary>
    /// One Membership of the resolved Space, as the list reads it, with whether its Account is present in this Space now.
    /// </summary>
    /// <param name="callerAccountId">The acting Account, which marks its own row.</param>
    /// <param name="membershipId">The Membership, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The row, or <c>null</c> when no Membership in this Space has the id.</returns>
    Task<ListedMembership?> ReadAsync(Guid callerAccountId, Guid membershipId, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a Membership within the actor's authority, or the actor's own non-Owner Membership. A committed removal
    /// deletes the row - which takes the removed Account's Tokens for this Space with it - and its presence here, and
    /// records the removed Account for a <c>MembershipChanged</c> publish after commit.
    /// </summary>
    /// <param name="actorAccountId">The acting Account.</param>
    /// <param name="membershipId">The target, from the route.</param>
    /// <param name="authority">The acting authority's removal sets, from the pipeline.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the removal came to.</returns>
    Task<RemovalOutcome> RemoveAsync(
        Guid actorAccountId,
        Guid membershipId,
        RemovalAuthority authority,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes the actor's own Membership unless it is the Owner's, with the same effects as a removal.
    /// </summary>
    /// <param name="actorAccountId">The acting Account, which leaves.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the leave came to: <see cref="RemovalOutcome.Removed"/>,
    /// <see cref="RemovalOutcome.OwnershipHeld"/> or <see cref="RemovalOutcome.LostMembership"/>.</returns>
    Task<RemovalOutcome> LeaveAsync(Guid actorAccountId, CancellationToken cancellationToken);
}

/// <summary>
/// One Membership as the list reads it. Story 4.4, AC1, AC2, AC7.
/// </summary>
/// <param name="Id">The Membership's id.</param>
/// <param name="DisplayName">The Account's display name, read live for a current Member (Lee's Q5 (a)).</param>
/// <param name="EmailAddress">The Account's address as typed at registration (Lee's Q4 (a)).</param>
/// <param name="RoleCode">The Role's stable code - read by the store, so the slice reads no Role.</param>
/// <param name="IsYou">Whether it is the acting Account's own Membership.</param>
/// <param name="CreatedAt">When it was created - the keyset's first column, never answered.</param>
/// <param name="InSpaceNow">Whether the Account has an unexpired presence in this Space, by the database clock.</param>
public sealed record ListedMembership(Guid Id, string DisplayName, string EmailAddress, string RoleCode, bool IsYou, DateTimeOffset CreatedAt, bool InSpaceNow);

/// <summary>
/// The keyset position a page follows: the last served row's <c>(CreatedAt, Id)</c>. Story 4.4, Q8 (a).
/// </summary>
/// <param name="CreatedAt">The row's creation instant.</param>
/// <param name="Id">The row's id - the tiebreaker.</param>
public sealed record MembershipCursor(DateTimeOffset CreatedAt, Guid Id);

/// <summary>
/// One Membership as a Role change answers it: the list's row without the address. Story 4.4, AC4; code review
/// 2026-11-03, decided by Lee (option (b)).
/// </summary>
/// <remarks>
/// <b>No address, deliberately.</b> The change's answer is recorded whole by the pipeline's idempotency record, and AD-23
/// confines addresses to the list response, so no idempotency record may hold one. The client keeps the address it
/// already shows from the list.
/// </remarks>
/// <param name="Id">The Membership's id.</param>
/// <param name="DisplayName">The Account's display name, read live for a current Member (Lee's Q5 (a)).</param>
/// <param name="RoleCode">The Role's stable code.</param>
/// <param name="IsYou">Whether it is the acting Account's own Membership.</param>
public sealed record ChangedMembership(Guid Id, string DisplayName, string RoleCode, bool IsYou);

/// <summary>
/// What a Role change came to. Story 4.4, AC4.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Row">The target's row as it now stands, when it exists in this Space.</param>
public sealed record RoleChangeResult(RoleChangeOutcome Outcome, ChangedMembership? Row);

/// <summary>
/// The outcomes of a Role change. Story 4.4, AC4, AC5, Q10 (a), Q11 (a).
/// </summary>
public enum RoleChangeOutcome
{
    /// <summary>
    /// Written and committed with the request: <c>200</c> with the updated row.
    /// </summary>
    Changed,

    /// <summary>
    /// The target already held the Role: <c>200</c> with the unchanged row, nothing written or published.
    /// </summary>
    Unchanged,

    /// <summary>
    /// The id names no Membership in this Space: the boundary's <c>404</c>, unrecorded.
    /// </summary>
    NotFound,

    /// <summary>
    /// The Owner's own Membership, by the Owner: <c>409 owner-role-fixed</c>.
    /// </summary>
    OwnerRoleFixed,

    /// <summary>
    /// Outside the acting authority: the pipeline's recorded <c>403 insufficient-role</c> (AD-20).
    /// </summary>
    BeyondAuthority,

    /// <summary>
    /// The actor's own Membership was gone by the time the change was written: the pipeline answers the boundary's
    /// <c>404</c> and records it <c>CrossSpace</c>, exactly what the actor's next request would meet.
    /// </summary>
    LostMembership,
}

/// <summary>
/// What a removal or a leave came to.
/// </summary>
public enum RemovalOutcome
{
    /// <summary>
    /// Deleted and committed with the request: <c>204</c>.
    /// </summary>
    Removed,

    /// <summary>
    /// The id names no Membership in this Space: the boundary's <c>404</c>, unrecorded.
    /// </summary>
    NotFound,

    /// <summary>
    /// The Owner's own Membership, by the Owner: <c>409 ownership-held</c>.
    /// </summary>
    OwnershipHeld,

    /// <summary>
    /// Outside the acting authority: the pipeline's recorded <c>403 insufficient-role</c>.
    /// </summary>
    BeyondAuthority,

    /// <summary>
    /// The actor's own Membership was gone by the time the removal was written: the pipeline's recorded boundary
    /// <c>404</c>.
    /// </summary>
    LostMembership,
}
