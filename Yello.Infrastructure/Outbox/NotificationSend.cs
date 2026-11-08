namespace Yello.Infrastructure.Outbox;

/// <summary>
/// The record that a notification was sent: its Space, its kind and when. Kept so the share of writes that notify
/// someone stays derivable after the outbox row, which holds the message, is deleted on send (FS-NFR-2, SM-C4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Never the content or the recipient.</b> It holds no address, subject, body, Task or Account - nothing a reader could
/// use to learn who was told what. No product surface reads it; the operator queries the table directly. A gate names its
/// only writer, <see cref="OutboxStore"/>.
/// </para>
/// <para>
/// <b>Written at enqueue, in the request's transaction, under the Space policy</b>: the dispatcher that sends runs with no
/// session key and could not insert a Space-scoped row. So it records an enqueue that committed, not a confirmed delivery -
/// an email the dispatcher abandons after its last attempt still counts.
/// </para>
/// </remarks>
internal sealed class NotificationSend
{
    /// <summary>
    /// The row's identity, generated application-side (AR-34).
    /// </summary>
    internal required Guid Id { get; init; }

    /// <summary>
    /// The Space the notification was about.
    /// </summary>
    internal required Guid SpaceId { get; init; }

    /// <summary>
    /// The message kind's stable code, such as <c>task-assigned</c>.
    /// </summary>
    internal required string Kind { get; init; }

    /// <summary>
    /// When it was recorded, by the database clock, in UTC. Stamped by the column's default.
    /// </summary>
    internal DateTimeOffset RecordedAt { get; init; }
}
