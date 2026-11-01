using Microsoft.Extensions.Logging;

namespace Yello.Infrastructure.Outbox;

/// <summary>
/// Source-generated log messages for the outbox. AR-34, NFR-6, I-17; story 4.1.
/// </summary>
/// <remarks>
/// <b>EventIds 1041-1043 and 1045-1046</b> (1044 is the Host's sender choice). <b>The outbox row's id, and nothing else</b>: never the recipient, the subject, the body
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

    [LoggerMessage(
        EventId = 1045,
        Level = LogLevel.Warning,
        Message = "Outbox message {OutboxId} was deleted unsent after {Attempts} attempts or a permanent refusal.")]
    internal static partial void Abandoned(ILogger logger, Guid outboxId, int attempts, Exception exception);

    [LoggerMessage(
        EventId = 1046,
        Level = LogLevel.Warning,
        Message = "Outbox message {OutboxId} was sent but could not be deleted; it is sent again once its claim is stale.")]
    internal static partial void SentButNotDeleted(ILogger logger, Guid outboxId, Exception exception);
}
