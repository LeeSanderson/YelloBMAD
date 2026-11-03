using Microsoft.Extensions.Logging;

namespace Yello.Infrastructure.Memberships;

/// <summary>
/// Source-generated log messages for <c>MembershipChanged</c>. AR-16, AD-9, AR-34, I-17; story 4.4, AC6, Q7 (a).
/// </summary>
/// <remarks>
/// <b>EventIds 1047-1048. Ids only</b>: the Account and the Space, never an address or a display name (AD-23). Every
/// publish writes one line, so a publish is observable from a Host process without test-only production code (story 1.9,
/// Q4); the event's name leads the message for the same reason.
/// </remarks>
internal static partial class MembershipLog
{
    [LoggerMessage(
        EventId = 1047,
        EventName = "MembershipChangedPublished",
        Level = LogLevel.Information,
        Message = "MembershipChangedPublished: MembershipChanged for Account {AccountId} in Space {SpaceId} was published after commit.")]
    internal static partial void Published(ILogger logger, Guid accountId, Guid spaceId);

    [LoggerMessage(
        EventId = 1048,
        Level = LogLevel.Warning,
        Message = "A MembershipChanged subscriber failed for Account {AccountId} in Space {SpaceId}; the change itself is committed.")]
    internal static partial void SubscriberFailed(ILogger logger, Guid accountId, Guid spaceId, Exception exception);
}
