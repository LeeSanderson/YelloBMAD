namespace Yello.Application.Spaces;

/// <summary>
/// Counts what a deletion of the active Space would take: its Projects, its Tasks, and the other Accounts that would lose
/// access. Story 3.3, AC2, open question 2 (a); PRD §6.2 ("stated at the point of the action").
/// </summary>
/// <remarks>
/// <b>Counted live, on the request's Space-scoped unit of work</b>, so the confirm never states a cached number. It takes
/// no Space id: both isolation layers are already narrowed to the resolved Space.
/// </remarks>
public interface ISpaceContentsReader
{
    /// <summary>
    /// Counts the resolved Space's contents.
    /// </summary>
    /// <param name="context">The resolved context; its Account is the one not counted among the others.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The counts.</returns>
    Task<SpaceContents> CountAsync(ActiveSpaceContext context, CancellationToken cancellationToken);
}

/// <summary>
/// What a Space holds, as counts and nothing else.
/// </summary>
/// <param name="Projects">Its Projects.</param>
/// <param name="Tasks">Its Tasks, in every Project.</param>
/// <param name="OtherMembers">The Accounts other than the caller holding a Membership in it.</param>
public sealed record SpaceContents(int Projects, int Tasks, int OtherMembers);
