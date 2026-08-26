namespace Yello.Host;

/// <summary>
/// Source-generated log messages for the startup connectivity check (AC4).
/// </summary>
/// <remarks>
/// Written as <c>[LoggerMessage]</c> partials rather than direct <c>logger.LogInformation</c>
/// calls because the analysers are correct on the general point (CA1848, CA1873) and this is
/// the first logging in the solution - whatever shape it takes here is the shape later
/// stories will copy. The generated methods perform their own <c>IsEnabled</c> check and
/// allocate nothing when the level is off.
/// <para>
/// The four outcomes below are deliberately distinct. The check previously had three states
/// that a reader could not tell apart from the log or from the exit code: "ran and passed",
/// "ran and failed", and "never ran because this is not Development". Distinguishing them is
/// the whole value of a check whose only output is a log line.
/// </para>
/// </remarks>
internal static partial class StartupLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Warning,
        Message = "No '{ResourceName}' connection string was injected. Run the solution through Yello.AppHost (`dotnet aspire run`) so Aspire supplies it.")]
    internal static partial void ConnectionStringMissing(ILogger logger, string resourceName);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Connected to SQL Server {ServerVersion}, database {Database}.")]
    internal static partial void ConnectivityConfirmed(ILogger logger, string serverVersion, string database);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error,
        Message = "Could not open the injected '{ResourceName}' connection. Is the SQL Server container running, and does the '{ResourceName}' database exist inside it?")]
    internal static partial void ConnectivityFailed(ILogger logger, string resourceName, Exception exception);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Error,
        Message = "The injected '{ResourceName}' connection string is not usable: {Reason}")]
    internal static partial void ConnectionStringUnusable(ILogger logger, string resourceName, string reason, Exception exception);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Warning,
        Message = "Gave up waiting for SQL Server after {TimeoutSeconds}s. Kestrel is starting anyway; the check does not gate the Host.")]
    internal static partial void ConnectivityTimedOut(ILogger logger, int timeoutSeconds);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Debug,
        Message = "Startup connectivity check skipped: it runs in Development only, and the environment is {Environment}.")]
    internal static partial void ConnectivityCheckSkipped(ILogger logger, string environment);
}
