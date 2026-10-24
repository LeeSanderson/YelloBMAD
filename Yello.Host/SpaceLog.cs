namespace Yello.Host;

/// <summary>
/// Source-generated log messages for Space resolution. AR-34.
/// </summary>
/// <remarks>
/// <para>
/// <b>EventIds 1016-1020.</b> 1000-1007 belong to <see cref="StartupLog"/>, 1008-1009 to
/// <see cref="RegistrationLog"/> and 1010-1015 to <see cref="SessionLog"/>; 1021 onwards to
/// <see cref="RateLimitLog"/>.
/// </para>
/// <para>
/// <b>The Space's id, never its name</b> (AR-34): a Space name is something a person typed, and a
/// log is not where it belongs. No Account identity either - a line tying an Account to the Space
/// ids it asked about is a disclosure this log has no need to make.
/// </para>
/// <para>
/// <b>One refusal message for both causes.</b> A Space that does not exist and a Space the caller is
/// not a member of are logged identically, as they are answered identically - a log line that
/// distinguished them would undo AD-3's "never distinguished" by a slower route.
/// </para>
/// </remarks>
internal static partial class SpaceLog
{
    [LoggerMessage(
        EventId = 1016,
        Level = LogLevel.Debug,
        Message = "Active Space resolved for {SpaceId}.")]
    internal static partial void SpaceResolved(ILogger logger, Guid spaceId);

    [LoggerMessage(
        EventId = 1017,
        Level = LogLevel.Information,
        Message = "Active Space could not be resolved for {SpaceId}; the request was refused.")]
    internal static partial void SpaceNotResolved(ILogger logger, Guid spaceId);

    [LoggerMessage(
        EventId = 1018,
        Level = LogLevel.Information,
        Message = "The Role held in {SpaceId} does not permit {Capability}; the request was refused.")]
    internal static partial void CapabilityRefused(ILogger logger, Guid spaceId, string capability);

    [LoggerMessage(
        EventId = 1019,
        Level = LogLevel.Warning,
        Message = "{SpaceId} is at its {Bound} bound of {Limit}; the request was refused.")]
    internal static partial void BoundReached(ILogger logger, Guid spaceId, string bound, int limit);

    [LoggerMessage(
        EventId = 1020,
        Level = LogLevel.Warning,
        Message = "The refusal-record retention purge failed; it is not retried until tomorrow.")]
    internal static partial void RefusalPurgeFailed(ILogger logger, Exception exception);
}
