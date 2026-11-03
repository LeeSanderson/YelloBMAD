namespace Yello.Application.Memberships;

/// <summary>
/// Collects <see cref="MembershipChanged"/> events in the request's unit of work, to publish once it commits. AR-16, AD-9;
/// story 4.4, AC6, Q7 (a).
/// </summary>
/// <remarks>
/// <b>Published after the commit and only after it</b>, in-process, to every registered
/// <see cref="IMembershipChangeSubscriber"/> - never on a rollback, a refusal or a no-op. "Any operation mutating a
/// Membership publishes <c>MembershipChanged(AccountId, SpaceId)</c> at its transaction boundary ... One publish per
/// affected Account" (AD-9). No timer, no table, no polling (AD-10).
/// </remarks>
public interface IMembershipChanges
{
    /// <summary>
    /// Records that this unit of work changed <paramref name="accountId"/>'s Membership in <paramref name="spaceId"/>.
    /// Recording the same pair twice publishes it once.
    /// </summary>
    /// <param name="accountId">The affected Account.</param>
    /// <param name="spaceId">The Space.</param>
    void Record(Guid accountId, Guid spaceId);
}

/// <summary>
/// A Membership was created, changed or removed: the affected Account and the Space. AR-16, AD-9; story 4.4, AC6.
/// </summary>
/// <param name="AccountId">The affected Account.</param>
/// <param name="SpaceId">The Space.</param>
public sealed record MembershipChanged(Guid AccountId, Guid SpaceId);

/// <summary>
/// Receives each <see cref="MembershipChanged"/> after its transaction commits, in-process. Story 4.4, AC6, Q7 (a).
/// </summary>
/// <remarks>
/// Production registers none yet: story 7.3 (lease invalidation) and 7.7 (the live-session clause) subscribe. A subscriber
/// runs on the request's thread after the commit, so it must not fail the request.
/// </remarks>
public interface IMembershipChangeSubscriber
{
    /// <summary>
    /// Handles one committed change.
    /// </summary>
    /// <param name="change">The change.</param>
    void OnMembershipChanged(MembershipChanged change);
}
