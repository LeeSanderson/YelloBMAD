using Microsoft.AspNetCore.Components;
using Yello.Contracts.Spaces;

namespace Yello.Client.Spaces;

/// <summary>
/// What a Space-scoped page receives once <c>SpaceArrival</c> has the server's answer about its Space.
/// Story 2.2, Task 10.
/// </summary>
/// <param name="Current">The active Space's answer on this load - its name and the Role held there.</param>
/// <param name="Refuse">
/// Renders the page's one refusal surface in place of the page: for a <c>404</c> the page's own request meets.
/// </param>
/// <param name="Reread">
/// Asks the server for the Role again and applies it in place, as a re-arrival does - for a page whose request was refused
/// for a Role it may no longer hold (story 4.4, AC10: a Role change answered <c>403</c>). A Role now lower than the page
/// permits follows the page's own narrated exit (UX-DR29).
/// </param>
public sealed record SpaceArrivalContext(ActiveSpaceResponse Current, EventCallback<Yello.Contracts.ProblemResponse?> Refuse, EventCallback Reread);
