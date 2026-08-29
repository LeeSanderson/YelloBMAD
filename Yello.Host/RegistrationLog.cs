namespace Yello.Host;

/// <summary>
/// Source-generated log messages for the registration endpoint. FS-NFR-1, AR-34.
/// </summary>
/// <remarks>
/// <para>
/// <c>[LoggerMessage]</c> partials rather than <c>logger.LogInformation</c> calls, following
/// <see cref="StartupLog"/>: CA1848 and CA1873 are errors here, and the generated methods do
/// their own <c>IsEnabled</c> check and allocate nothing when the level is off.
/// </para>
/// <para>
/// <b>EventIds start at 1008.</b> 1000-1007 belong to <see cref="StartupLog"/> and are not
/// reused - a duplicate id makes two different events indistinguishable to anything filtering on
/// it, which is the whole reason the numbers exist.
/// </para>
/// <para>
/// <b>NEITHER MESSAGE CARRIES THE EMAIL ADDRESS, AND NEITHER SAYS WHAT HAPPENED.</b> That is
/// AD-23 reaching the log rather than stopping at the response. If registration logged the
/// address, or logged "created" against one path and "already existed" against the other, the
/// uniform 204 would be undone by anyone who can read the log - and logs travel further than
/// responses do. There is deliberately no field here that differs between a new address and a
/// known one, so the two paths are indistinguishable in the log by construction rather than by
/// the care of whoever writes the next message.
/// </para>
/// <para>
/// <b>No password reaches any of this.</b> The command is never a log parameter, and the only
/// method that receives the password at all is the hasher.
/// </para>
/// </remarks>
internal static partial class RegistrationLog
{
    [LoggerMessage(
        EventId = 1008,
        Level = LogLevel.Information,
        Message = "Registration request processed.")]
    internal static partial void RegistrationProcessed(ILogger logger);

    [LoggerMessage(
        EventId = 1009,
        Level = LogLevel.Information,
        Message = "Registration request rejected as malformed: {Failures}")]
    internal static partial void RegistrationRejected(ILogger logger, string failures);
}
