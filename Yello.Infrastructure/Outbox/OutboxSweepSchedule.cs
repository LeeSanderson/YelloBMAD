namespace Yello.Infrastructure.Outbox;

/// <summary>
/// When this process last claimed a piggyback sweep, so the inbound-traffic trigger sweeps at most once a minute.
/// AD-10 / AR-33; story 4.1, AC6. The <c>RefusalPurgeSchedule</c> shape.
/// </summary>
/// <remarks>
/// <para>
/// <b>A singleton holding one instant, and not a cache</b>: it holds no Space data and answers nothing but "has this
/// process swept on traffic in the last minute".
/// </para>
/// <para>
/// <b>Claimed atomically</b>, so two requests arriving together start one sweep. <b>The process-start sweep does not
/// claim it</b>: a Host started a moment ago still sweeps on its first authenticated request.
/// </para>
/// </remarks>
internal sealed class OutboxSweepSchedule
{
    /// <summary>
    /// The shortest interval between two piggyback sweeps in one process.
    /// </summary>
    internal static TimeSpan Interval { get; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// <see cref="DateTimeOffset.UtcTicks"/> of the last claim, or <see cref="long.MinValue"/> before the first.
    /// </summary>
    private long _lastClaimedTicks = long.MinValue;

    /// <summary>
    /// Claims the sweep if none has been claimed in the last <see cref="Interval"/>.
    /// </summary>
    /// <param name="now">The current instant.</param>
    /// <returns><c>true</c> when the caller should sweep now.</returns>
    internal bool TryClaim(DateTimeOffset now)
    {
        var nowTicks = now.UtcTicks;

        while (true)
        {
            var last = Interlocked.Read(ref _lastClaimedTicks);

            // A clock that stepped back behind the last claim is due now.
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
