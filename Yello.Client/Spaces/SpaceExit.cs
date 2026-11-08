using Microsoft.AspNetCore.Components;
using Yello.Client.Pages;

namespace Yello.Client.Spaces;

/// <summary>
/// Takes the tab out of a Space it no longer belongs to - the Space deleted, or the Account gone from it - and to the entry
/// point, which lands on the first remaining listed Space or on the no-Space state.
/// </summary>
/// <remarks>
/// <para>
/// <b>In this order, and nowhere else</b>: the Space leaves the switcher's held list, the sentence is held for the page the
/// tab lands on, the tab leaves the Space before it navigates - so the purge runs now and where it lands is a first
/// arrival - and only then does it go, with replace, so Back does not return to a Space it can no longer read.
/// </para>
/// <para>
/// The sentence is the client's own copy and names no Space (UX-DR38).
/// </para>
/// </remarks>
/// <param name="directory">The switcher's held answer.</param>
/// <param name="departure">The sentence the landing page says.</param>
/// <param name="activeSpace">The tab's active Space.</param>
/// <param name="navigation">The navigation manager.</param>
public sealed class SpaceExit(SpaceDirectory directory, DepartureNarration departure, ActiveSpaceState activeSpace, NavigationManager navigation)
{
    /// <summary>
    /// Leaves <paramref name="spaceId"/> for good.
    /// </summary>
    /// <param name="spaceId">The Space the tab no longer belongs to.</param>
    /// <param name="sentence">What the landing page says, or <c>null</c> to say nothing.</param>
    public void Leave(Guid spaceId, string? sentence)
    {
        directory.Removed(spaceId);

        if (sentence is not null)
        {
            departure.Say(sentence);
        }

        activeSpace.Navigating(target: null);
        navigation.GoTo(activeSpace, ClientRoutes.Home, replace: true);
    }
}
