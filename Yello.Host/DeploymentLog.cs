namespace Yello.Host;

/// <summary>
/// Source-generated log messages for the Host's production shape: forwarded headers, the persisted
/// key ring, a database that cannot be reached, and the refusal purge's claim. AR-34; story 1.10.
/// </summary>
/// <remarks>
/// <b>EventIds 1024-1029.</b> 1023 belongs to <see cref="SessionLog"/>. Every line logs a setting or an
/// outcome, never a value taken from a request.
/// </remarks>
internal static partial class DeploymentLog
{
    [LoggerMessage(
        EventId = 1024,
        Level = LogLevel.Information,
        Message = "Forwarded headers are {State}: the scheme and the client address come from {Source}.")]
    internal static partial void ForwardedHeadersResolved(ILogger logger, string state, string source);

    [LoggerMessage(
        EventId = 1025,
        Level = LogLevel.Warning,
        Message = "The forwarded-headers setting under '{ConfigurationKey}' was not used: {Problem}")]
    internal static partial void ForwardedHeadersUnusable(ILogger logger, string configurationKey, string problem);

    [LoggerMessage(
        EventId = 1026,
        Level = LogLevel.Information,
        Message = "A request with no client address is counted in the shared rate-limit partition. Behind ingress that means the platform did not forward the address, which is a misconfiguration.")]
    internal static partial void MissingAddressSharesOnePartition(ILogger logger);

    [LoggerMessage(
        EventId = 1027,
        Level = LogLevel.Information,
        Message = "The Data Protection key ring is kept in the application database, under application name '{ApplicationName}'.")]
    internal static partial void KeyRingPersisted(ILogger logger, string applicationName);

    [LoggerMessage(
        EventId = 1028,
        Level = LogLevel.Warning,
        Message = "The database could not be reached; the request was answered 503 and admitted nothing.")]
    internal static partial void DatabaseUnavailable(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1029,
        Level = LogLevel.Information,
        Message = "RefusalPurgeClaimed: this request claimed the day's refusal-record purge, which runs off the request path.")]
    internal static partial void RefusalPurgeClaimed(ILogger logger);
}
