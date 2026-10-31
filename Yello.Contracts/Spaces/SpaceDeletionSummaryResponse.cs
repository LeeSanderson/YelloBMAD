namespace Yello.Contracts.Spaces;

/// <summary>
/// What deleting the active Space would take, for the confirm to state at the point of the action (PRD §6.2, UX-DR37).
/// Story 3.3, AC2, open question 2 (a).
/// </summary>
/// <remarks>
/// <b>Counts and nothing else.</b> No Space, Project, Task or Account name crosses this answer; the confirm names the
/// Space from the context it already holds.
/// </remarks>
/// <param name="Projects">The Space's Projects.</param>
/// <param name="Tasks">Its Tasks, in every Project.</param>
/// <param name="OtherMembers">The other Accounts holding a Membership in it, who would lose access.</param>
public sealed record SpaceDeletionSummaryResponse(int Projects, int Tasks, int OtherMembers);
