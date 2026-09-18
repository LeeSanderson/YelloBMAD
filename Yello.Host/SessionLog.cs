namespace Yello.Host;

/// <summary>
/// Source-generated log messages for the Session endpoints. AD-23 / AR-28, AR-34.
/// </summary>
/// <remarks>
/// <para>
/// <c>[LoggerMessage]</c> partials rather than <c>logger.LogInformation</c> calls, following
/// <see cref="StartupLog"/> and <see cref="RegistrationLog"/>: CA1848 and CA1873 are errors here,
/// and the generated methods do their own <c>IsEnabled</c> check and allocate nothing when the
/// level is off.
/// </para>
/// <para>
/// <b>EventIds start at 1010.</b> 1000-1007 belong to <see cref="StartupLog"/> and 1008-1009 to
/// <see cref="RegistrationLog"/>; none is reused, because a duplicate id makes two different
/// events indistinguishable to anything filtering on it, which is the whole reason the numbers
/// exist.
/// </para>
/// <para>
/// <b>ONE MESSAGE COVERS SUCCESS AND BOTH FAILURE CAUSES, AND IT CARRIES NO FIELD AT ALL.</b>
/// That is not tidiness - it is AD-23 reaching the log rather than stopping at the response.
/// <see cref="SignInProcessed"/> is emitted by the success path and by the 401 path alike, so the
/// log records that a sign-in was attempted and nothing else. If it named the address, or said
/// "succeeded" against one path and "no such account" against another, the uniform 401 would be
/// undone by anyone who can read the log - and logs travel further than responses do. Story 1.3
/// met this exact trap from an unexpected direction on 2026-09-12, when EF Core's command log
/// named every duplicate registration by the shape of the statements it did not run; the filter
/// that closed it (<c>Program.cs</c>) is unconditional and covers this story too.
/// </para>
/// <para>
/// <b>Never the cookie.</b> AR-34 (<c>ARCHITECTURE-SPINE.md:274</c>) forbids logging it, and
/// nothing here takes one - no method below receives an <c>HttpContext</c>, a header or a ticket.
/// No password reaches any of this either: the command is never a log parameter, and the only
/// method that receives the password at all is the hasher.
/// </para>
/// </remarks>
internal static partial class SessionLog
{
    [LoggerMessage(
        EventId = 1010,
        Level = LogLevel.Information,
        Message = "Sign-in request processed.")]
    internal static partial void SignInProcessed(ILogger logger);

    [LoggerMessage(
        EventId = 1011,
        Level = LogLevel.Information,
        Message = "Sign-in request rejected as malformed: {Failures}")]
    internal static partial void SignInRejected(ILogger logger, string failures);

    [LoggerMessage(
        EventId = 1012,
        Level = LogLevel.Warning,
        Message = "No client origin is configured under '{ConfigurationKey}', so every "
            + "cross-origin browser request will be refused. Set it to the address the client is "
            + "served from.")]
    internal static partial void NoClientOriginConfigured(ILogger logger, string configurationKey);
}
