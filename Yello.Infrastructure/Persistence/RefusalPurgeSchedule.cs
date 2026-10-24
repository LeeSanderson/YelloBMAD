namespace Yello.Infrastructure.Persistence;

/// <summary>
/// When this process last claimed a refusal purge, so the purge runs at most once a day. AD-10 /
/// AR-33.
/// </summary>
/// <remarks>
/// <para>
/// <b>A singleton holding one instant, and not a cache.</b> NFR-2 forbids a cache that outlives a
/// request because a cached Role or Space-scoped answer would be served to a later request; this
/// holds no Space data and answers nothing but "has this process purged in the last day".
/// </para>
/// <para>
/// <b>Claimed atomically.</b> Two requests arriving together on a cold process race for the first
/// purge; exactly one wins and the other returns without touching the database.
/// </para>
/// <para>
/// <b>Per process, by design.</b> Several replicas each purge at most daily, which is still "at most
/// daily" per component and still never on a schedule. A failed purge is not retried until the next
/// day: the claim is taken before the delete runs, so a database that refuses it is not asked again
/// on every request.
/// </para>
/// </remarks>
internal sealed class RefusalPurgeSchedule
{
    /// <summary>
    /// The shortest interval between two purges in one process.
    /// </summary>
    internal static TimeSpan Interval { get; } = TimeSpan.FromDays(1);

    /// <summary>
    /// <see cref="DateTimeOffset.UtcTicks"/> of the last claim, or <see cref="long.MinValue"/> before
    /// the first.
    /// </summary>
    private long _lastClaimedTicks = long.MinValue;

    /// <summary>
    /// Claims the purge if none has been claimed in the last <see cref="Interval"/>.
    /// </summary>
    /// <param name="now">The current instant.</param>
    /// <returns><c>true</c> when the caller should purge now.</returns>
    internal bool TryClaim(DateTimeOffset now)
    {
        var nowTicks = now.UtcTicks;

        while (true)
        {
            var last = Interlocked.Read(ref _lastClaimedTicks);

            // A clock that stepped back behind the last claim is due now, rather than waiting out the
            // step as well as the interval.
            if (last != long.MinValue && nowTicks >= last && nowTicks - last < Interval.Ticks)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _lastClaimedTicks, nowTicks, last) == last)
            {
                return true;
            }
        }
    }
}
