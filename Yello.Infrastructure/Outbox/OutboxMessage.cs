namespace Yello.Infrastructure.Outbox;

/// <summary>
/// One email waiting to be sent, rendered in full at enqueue. AD-10 / AR-33; story 4.1 (Lee's Q6).
/// </summary>
/// <remarks>
/// <para>
/// <b>Policy-free, with no Space column - a recorded AD-2 variance</b> (Lee, 2026-11-01). The recovery sweep must see
/// every Space's unsent mail at process start, and a second line setting a session key is forbidden
/// (<c>Exactly_one_production_line_sets_a_session_context_key</c>). So the row holds nothing the email itself does not
/// carry - the recipient, the subject, the body - and nothing that names a Space by id. Only
/// <see cref="OutboxStore"/>, <see cref="OutboxDispatcher"/> and the table's configuration reach it; a gate names them.
/// </para>
/// <para>
/// <b>Deleted once sent</b> (open question 7 (a)): nothing about a message - address, content or token - survives its
/// dispatch. Delivery is at least once: a crash between the send and the delete sends the email again.
/// </para>
/// </remarks>
internal sealed class OutboxMessage
{
    /// <summary>
    /// The longest stored kind code.
    /// </summary>
    internal const int KindMaxLength = 64;

    /// <summary>
    /// The longest stored subject.
    /// </summary>
    internal const int SubjectMaxLength = 512;

    /// <summary>
    /// The row's identity, generated application-side (AR-34).
    /// </summary>
    internal required Guid Id { get; init; }

    /// <summary>
    /// The message kind's stable code, such as <c>invitation-issued</c>.
    /// </summary>
    internal required string Kind { get; init; }

    /// <summary>
    /// The one recipient's address.
    /// </summary>
    internal required string Recipient { get; init; }

    /// <summary>
    /// The rendered subject.
    /// </summary>
    internal required string Subject { get; init; }

    /// <summary>
    /// The rendered plain-text body.
    /// </summary>
    internal required string Body { get; init; }

    /// <summary>
    /// When the row was enqueued, in UTC.
    /// </summary>
    internal required DateTimeOffset EnqueuedAt { get; init; }

    /// <summary>
    /// When a dispatcher last claimed the row, by the database clock, or <c>null</c> when never claimed. A claim older
    /// than five minutes is presumed abandoned and may be claimed again.
    /// </summary>
    internal DateTimeOffset? ClaimedAt { get; init; }

    /// <summary>
    /// How many times the row has been claimed.
    /// </summary>
    internal int Attempts { get; init; }
}
