namespace Yello.Host;

/// <summary>
/// Source-generated log messages for the password-hashing rate limit. AR-34.
/// </summary>
/// <remarks>
/// <b>EventIds 1021-1022.</b> 1016-1020 belong to <see cref="SpaceLog"/>. The resolved limit is
/// logged unconditionally, as the Session lifetime is: a limit that is wrong by an order of magnitude
/// is only obvious when the number is on screen.
/// </remarks>
internal static partial class RateLimitLog
{
    [LoggerMessage(
        EventId = 1021,
        Level = LogLevel.Warning,
        Message = "The rate limit configured under '{ConfigurationKey}' was not used: {Problem}")]
    internal static partial void PermitsUnusable(ILogger logger, string configurationKey, string problem);

    [LoggerMessage(
        EventId = 1022,
        Level = LogLevel.Information,
        Message = "Registration and sign-in are limited to {Permits} per client address per {Window}.")]
    internal static partial void PermitsResolved(ILogger logger, int permits, TimeSpan window);
}
