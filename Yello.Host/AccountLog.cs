namespace Yello.Host;

/// <summary>
/// Source-generated log messages for the Account endpoint.
/// </summary>
/// <remarks>
/// The deleted Account is named by its id alone: the address and the display name are personal data the deletion has
/// just erased, and a log line carrying either would outlive it.
/// </remarks>
internal static partial class AccountLog
{
    [LoggerMessage(
        EventId = 1049,
        Level = LogLevel.Information,
        Message = "Account {AccountId} deleted.")]
    internal static partial void Deleted(ILogger logger, Guid accountId);
}
