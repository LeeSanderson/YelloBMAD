using Microsoft.AspNetCore.Components;

namespace Yello.Client.Layout;

/// <summary>
/// The routed page's heading, as the skip control and a Space's first arrival reach it. Story 2.2, open
/// question 9 (a); UX-DR39.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a registry rather than a selector.</b> The skip control lives in the layout, above the page, and
/// a Space-scoped page's heading renders only once the server has answered - after the layout. A
/// <c>#main</c> link would send the router to <c>/#main</c> (<c>MainLayout</c>'s comment), and the client has
/// no script of its own to focus by selector, so the heading registers itself here when it renders
/// (<c>PageHeading</c>) and leaves when it goes.
/// </para>
/// <para>
/// <b>A focus request waits for the heading.</b> A Space's first arrival asks for the heading to take focus
/// (as <c>FocusOnNavigate</c> would have, had the heading existed when it ran); the request is claimed by
/// the next heading to render, so a heading drawn after a further answer - the Board's - still takes it.
/// Per tab, and holding no Space content.
/// </para>
/// </remarks>
public sealed class PageHeadingFocus
{
    private bool _requested;

    /// <summary>
    /// The heading on screen, or <c>null</c> when the page has none registered.
    /// </summary>
    public ElementReference? Current { get; private set; }

    /// <summary>
    /// Registers the routed page's heading.
    /// </summary>
    /// <param name="heading">The heading.</param>
    public void Register(ElementReference heading) => Current = heading;

    /// <summary>
    /// Forgets a heading that is leaving the page - unless another has replaced it.
    /// </summary>
    /// <param name="heading">The heading.</param>
    public void Unregister(ElementReference heading)
    {
        if (Current is { } current && current.Id == heading.Id)
        {
            Current = null;
        }
    }

    /// <summary>
    /// Asks for the next heading to render to take focus.
    /// </summary>
    public void RequestFocus() => _requested = true;

    /// <summary>
    /// Takes the pending focus request, if there is one.
    /// </summary>
    /// <returns><c>true</c> once per request.</returns>
    public bool ClaimRequest()
    {
        var requested = _requested;

        _requested = false;

        return requested;
    }
}
