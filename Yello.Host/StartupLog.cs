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
/// </remarks>
internal static partial class StartupLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Warning,
        Message = "No 'yello' connection string was injected. Run the solution through Yello.AppHost (`dotnet aspire run`) so Aspire supplies it.")]
    internal static partial void ConnectionStringMissing(ILogger logger);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Connected to SQL Server {ServerVersion}, database {Database}.")]
    internal static partial void ConnectivityConfirmed(ILogger logger, string serverVersion, string database);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error,
        Message = "Could not open the injected 'yello' connection. Is the SQL Server container running?")]
    internal static partial void ConnectivityFailed(ILogger logger, Exception exception);
}
