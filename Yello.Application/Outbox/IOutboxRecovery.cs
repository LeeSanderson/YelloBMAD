namespace Yello.Application.Outbox;

/// <summary>
/// The outbox's recovery sweep: sends rows a crash left unsent. AD-10 / AR-33; story 4.1, AC6.
/// </summary>
/// <remarks>
/// <b>Two triggers, neither a timer.</b> The Host sweeps once at process start, and otherwise piggybacks on inbound
/// authenticated requests at most once a minute per process. An idle database is never woken by it.
/// </remarks>
public interface IOutboxRecovery
{
    /// <summary>
    /// Starts a sweep now, off the caller's path. The process-start trigger; it does not consume the piggyback throttle.
    /// </summary>
    void SweepNow();

    /// <summary>
    /// Starts a sweep off the caller's path if none has been claimed by the piggyback trigger in the last minute.
    /// </summary>
    /// <returns><c>true</c> when a sweep was started.</returns>
    bool SweepIfDue();
}
