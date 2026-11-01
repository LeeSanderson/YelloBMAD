using Microsoft.Extensions.Logging;

namespace Yello.Infrastructure.Outbox;

/// <summary>
/// Source-generated log messages for the outbox. AR-34, NFR-6, I-17; story 4.1.
/// </summary>
/// <remarks>
/// <b>EventIds 1041-1044.</b> <b>The outbox row's id, and nothing else</b>: never the recipient, the subject, the body
/// or the token the body carries. A gate holds that no log message takes a parameter named for any of them.
/// </remarks>
internal static partial class OutboxLog
{
    [LoggerMessage(
        EventId = 1041,
        Level = LogLevel.Information,
        Message = "Outbox message {OutboxId} sent.")]
    internal static partial void Sent(ILogger logger, Guid outboxId);

    [LoggerMessage(
        EventId = 1042,
        Level = LogLevel.Warning,
        Message = "Outbox message {OutboxId} was not sent; it is claimed again after five minutes.")]
    internal static partial void NotSent(ILogger logger, Guid outboxId, Exception exception);

    [LoggerMessage(
        EventId = 1043,
        Level = LogLevel.Warning,
        Message = "The outbox could not be swept; unsent messages wait for the next sweep.")]
    internal static partial void SweepFailed(ILogger logger, Exception exception);
}
