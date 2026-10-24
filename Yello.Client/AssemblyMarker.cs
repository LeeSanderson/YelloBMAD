using System.Reflection;

namespace Yello.Client;

/// <summary>
/// A deterministic handle on this assembly for the architecture suite
/// (<see cref="Assembly"/>). An empty project offers none, and the ring gates cannot assert
/// against an assembly they cannot load.
/// </summary>
/// <remarks>
/// <para>
/// What this project contains: the token layer and base layer, <c>wwwroot/css/components.css</c>,
/// a <c>Router</c>, a layout, the registration page, and three components - a form field, a
/// primary button and an inline error region. Also the localisation resources and the culture
/// provider that sets the document's language, which is what makes <c>base.css</c>'s 26-locale
/// casing exclusions reachable at all.
/// </para>
/// <para>
/// Story 1.7 added the context bar, the Space switcher, the Role chip, a Space's home and the entry
/// point at <c>/</c>. Still absent, and owned elsewhere: task cards, columns, dialogs and pickers are
/// epic 2 onward.
/// </para>
/// </remarks>
public static class AssemblyMarker
{
    /// <summary>
    /// This project's compiled assembly. Reading it is a real compile-time reference, which
    /// is what guarantees the DLL is copied next to the architecture suite's binary; an empty
    /// marker type would be a reference the compiler could elide.
    /// </summary>
    public static Assembly Assembly => typeof(AssemblyMarker).Assembly;
}
