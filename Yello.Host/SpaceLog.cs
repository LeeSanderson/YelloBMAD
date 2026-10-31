using Yello.Domain;
using Yello.Domain.Bounds;
using Yello.Domain.Memberships;

namespace Yello.Host;

/// <summary>
/// Source-generated log messages for Space resolution. AR-34.
/// </summary>
/// <remarks>
/// <para>
/// <b>EventIds 1016-1020, 1031-1032 (story 2.1's idempotency), 1033-1034 (story 2.2's not-found floor) and
/// 1035-1038 (story 3.1's Space creation) and 1039-1040 (story 3.3's Space deletion).</b> 1000-1007 belong to
/// <see cref="StartupLog"/>, 1008-1009 to <see cref="RegistrationLog"/> and 1010-1015 to
/// <see cref="SessionLog"/>; 1021 onwards to <see cref="RateLimitLog"/> and the others.
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

    /// <summary>
    /// A capability refusal, logged by the capability's stable code.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="spaceId">The Space.</param>
    /// <param name="capability">The capability refused.</param>
    internal static void CapabilityRefusedFor(ILogger logger, Guid spaceId, Capability capability) =>
        CapabilityRefused(logger, spaceId, StableCode.Of(capability));

    /// <summary>
    /// A bound refusal, logged by the bound's stable code and its value.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="spaceId">The Space.</param>
    /// <param name="bound">The bound reached.</param>
    internal static void BoundReachedFor(ILogger logger, Guid spaceId, BoundEntry bound) =>
        BoundReached(logger, spaceId, StableCode.Of(bound.Bound), bound.Limit);

    [LoggerMessage(
        EventId = 1031,
        Level = LogLevel.Information,
        Message = "A request in {SpaceId} repeated an Idempotency-Key; the recorded response was replayed and nothing was applied.")]
    internal static partial void IdempotentReplay(ILogger logger, Guid spaceId);

    [LoggerMessage(
        EventId = 1032,
        Level = LogLevel.Information,
        Message = "A request in {SpaceId} carried an Idempotency-Key it could not use ({Problem}); the request was refused.")]
    internal static partial void IdempotencyKeyRefused(ILogger logger, Guid spaceId, string problem);

    [LoggerMessage(
        EventId = 1033,
        Level = LogLevel.Warning,
        Message = "The not-found floor configured under '{ConfigurationKey}' was not used: {Problem}")]
    internal static partial void NotAvailableFloorUnusable(ILogger logger, string configurationKey, string problem);

    [LoggerMessage(
        EventId = 1034,
        Level = LogLevel.Information,
        Message = "A Space-scoped not-found answer takes at least {Floor} (zero is off).")]
    internal static partial void NotAvailableFloorResolved(ILogger logger, TimeSpan floor);

    /// <summary>
    /// A Space creation refused at an Account's bound (story 3.1). Neither the Account nor the Space it would have
    /// created is named: the line says which bound, and nothing that ties an Account to anything.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="bound">The bound's stable code.</param>
    /// <param name="limit">Its value.</param>
    [LoggerMessage(
        EventId = 1035,
        Level = LogLevel.Warning,
        Message = "A Space creation was refused at the {Bound} bound of {Limit}.")]
    internal static partial void CreationBoundReached(ILogger logger, string bound, int limit);

    /// <summary>
    /// A Space creation's bound refusal, by the bound's stable code and its value.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="bound">The bound reached.</param>
    internal static void CreationBoundReachedFor(ILogger logger, BoundEntry bound) =>
        CreationBoundReached(logger, StableCode.Of(bound.Bound), bound.Limit);

    [LoggerMessage(
        EventId = 1036,
        Level = LogLevel.Information,
        Message = "A Space creation repeated an Idempotency-Key; the recorded response was replayed and nothing was created.")]
    internal static partial void CreationReplayed(ILogger logger);

    [LoggerMessage(
        EventId = 1037,
        Level = LogLevel.Information,
        Message = "A Space creation carried an Idempotency-Key it could not use ({Problem}); the request was refused.")]
    internal static partial void CreationIdempotencyKeyRefused(ILogger logger, string problem);

    [LoggerMessage(
        EventId = 1038,
        Level = LogLevel.Information,
        Message = "Space {SpaceId} was created.")]
    internal static partial void SpaceCreated(ILogger logger, Guid spaceId);

    /// <summary>
    /// A Space deleted by its Owner (story 3.3). Its id only, never its name (I-17).
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="spaceId">The Space.</param>
    [LoggerMessage(
        EventId = 1039,
        Level = LogLevel.Information,
        Message = "Space {SpaceId} was deleted.")]
    internal static partial void SpaceDeleted(ILogger logger, Guid spaceId);

    /// <summary>
    /// A Space-scoped request whose Space was deleted between its resolution and its write (story 3.3, open question
    /// 4 (a)); it was answered as the boundary refuses.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="spaceId">The Space.</param>
    [LoggerMessage(
        EventId = 1040,
        Level = LogLevel.Information,
        Message = "{SpaceId} was deleted under a request resolved in it; the request was refused.")]
    internal static partial void SpaceDeletedUnderRequest(ILogger logger, Guid spaceId);
}
