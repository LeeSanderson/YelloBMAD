using Yello.Application.Outbox;

namespace Yello.Infrastructure.Outbox;

/// <summary>
/// Whether the request's unit of work enqueued an email, and the kick that follows its commit. AD-10; story 4.1, AC6.
/// </summary>
/// <remarks>
/// <b>A post-commit hook on the unit of work</b> (the story's preferred shape over <c>response.OnStarting</c>). The
/// Space-scoped unit of work calls <see cref="Committed"/> after its transaction has committed and at no other time, so
/// a rolled-back write - a failure result, a thrown exception, a failed idempotency record - enqueues nothing and sends
/// nothing. Scoped with the request.
/// </remarks>
/// <param name="recovery">The dispatcher, kicked after the commit.</param>
internal sealed class OutboxCommitSignal(IOutboxRecovery recovery)
{
    private bool _enqueued;

    /// <summary>
    /// Records that this unit of work saved an outbox row.
    /// </summary>
    internal void MarkEnqueued() => _enqueued = true;

    /// <summary>
    /// The unit of work has committed: dispatch what it enqueued, off the request path.
    /// </summary>
    internal void Committed()
    {
        if (!_enqueued)
        {
            return;
        }

        _enqueued = false;
        recovery.SweepNow();
    }
}
