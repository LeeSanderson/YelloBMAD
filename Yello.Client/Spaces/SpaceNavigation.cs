using Microsoft.AspNetCore.Components;
using Yello.Client.Pages;

namespace Yello.Client.Spaces;

/// <summary>
/// The one door a component navigates through (story 3.4, AC13; Q9 (a)): the tab leaves the Space it is in for the
/// destination's Space, which purges the departed Space, and only then navigates.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not rely on the location-changing hook.</b> The composition root's hook (<c>Program.cs</c>) purges on a link
/// and on browser Back or Forward, but story 3.4 did not see it purge before the destination's first frame for a
/// programmatic <c>NavigateTo</c>, with <c>replace: true</c> or without: with the deletion's explicit call removed, a frame
/// at <c>/</c> rendered the deleted Space's name in both cases, and with the hook unregistered too, so the observation
/// cannot tell a hook that does not run from one that runs too late (story 3.3 had traced the same with a console line). Either way the
/// hook is not relied on. So the purge is part of this call, before the navigation starts. <c>Navigating</c> is idempotent for
/// one Space, so a caller that already left the Space (the deletion, a creation) loses nothing.
/// </para>
/// <para>
/// <b>Gated.</b> <c>SpaceNavigationGateTests</c> allows no other <c>NavigateTo</c> in the client, except the calls it
/// counts by file with the reason each is safe.
/// </para>
/// <para>
/// <b>Client routes only.</b> The location is resolved against the base address to find its Space, so an address
/// outside the base is not a destination this door takes (<c>ToBaseRelativePath</c> throws on one).
/// </para>
/// </remarks>
public static class SpaceNavigation
{
    /// <summary>
    /// Leaves the current Space for <paramref name="location"/>'s Space (none, outside Space routes), then navigates there.
    /// </summary>
    /// <param name="navigation">The navigation manager.</param>
    /// <param name="activeSpace">The tab's active Space.</param>
    /// <param name="location">The destination: a client route, relative to the base address or absolute within it.</param>
    public static void GoTo(this NavigationManager navigation, ActiveSpaceState activeSpace, string location) =>
        GoTo(navigation, activeSpace, location, replace: false);

    /// <summary>
    /// Leaves the current Space for <paramref name="location"/>'s Space (none, outside Space routes), then navigates there.
    /// </summary>
    /// <param name="navigation">The navigation manager.</param>
    /// <param name="activeSpace">The tab's active Space.</param>
    /// <param name="location">The destination: a client route, relative to the base address or absolute within it.</param>
    /// <param name="replace">Whether the destination replaces the current history entry.</param>
    public static void GoTo(this NavigationManager navigation, ActiveSpaceState activeSpace, string location, bool replace)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(activeSpace);

        activeSpace.Navigating(ClientRoutes.SpaceIn(navigation.ToBaseRelativePath(navigation.ToAbsoluteUri(location).ToString())));
        navigation.NavigateTo(location, forceLoad: false, replace: replace);
    }
}
