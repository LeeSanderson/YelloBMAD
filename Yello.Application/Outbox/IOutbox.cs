namespace Yello.Application.Outbox;

/// <summary>
/// Enqueues an email in the current unit of work, so it is committed - or rolled back - with the write that triggered
/// it. AD-10 / AR-33; story 4.1, AC6.
/// </summary>
/// <remarks>
/// <b>Sent only after the commit.</b> The enqueue saves its row in the request's transaction; the in-process dispatcher
/// is signalled once that transaction has committed, so a rolled-back write enqueues and sends nothing. A row a crash
/// leaves unsent is picked up by the recovery sweep at process start or on the next inbound authenticated request -
/// never by a timer.
/// </remarks>
public interface IOutbox
{
    /// <summary>
    /// Adds the email to the outbox in the current unit of work, and saves it.
    /// </summary>
    /// <param name="email">The rendered email.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>A task.</returns>
    Task EnqueueAsync(OutboxEmail email, CancellationToken cancellationToken);
}
