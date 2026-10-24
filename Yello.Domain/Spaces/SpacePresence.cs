namespace Yello.Domain.Spaces;

/// <summary>
/// One Account's live presence in one Space - what NFR-8's "concurrent active Sessions per Space"
/// counts. AD-25 / AR-30.
/// </summary>
/// <remarks>
/// <para>
/// <b>An Account, not a cookie.</b> A Session has no server-side identity - it is a plain cookie
/// ticket with no table - so the bound counts distinct Accounts with a presence that has not
/// expired. Two browsers signed in as one Account are one presence (story 1.6, decision 1).
/// </para>
/// <para>
/// <b>Written at Space resolution, expired on read.</b> Every resolved Space-scoped request refreshes
/// the caller's <see cref="ExpiresAt"/>; nothing ever deletes or rewrites a row on a schedule. Expiry
/// is evaluated in the counting statement's own <c>WHERE</c> (AD-27), so a presence past its instant
/// simply stops being counted.
/// </para>
/// <para>
/// <b>Space-scoped</b>, so it carries <see cref="SpaceId"/>, the row-level security predicates and a
/// query filter from application state, like every other Space-scoped table.
/// </para>
/// </remarks>
public sealed class SpacePresence
{
    /// <summary>
    /// The Space the Account is present in.
    /// </summary>
    public required Guid SpaceId { get; init; }

    /// <summary>
    /// The Account that is present.
    /// </summary>
    public required Guid AccountId { get; init; }

    /// <summary>
    /// When the presence stops counting, in UTC. Moved forward by the Account's own requests.
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; set; }
}
