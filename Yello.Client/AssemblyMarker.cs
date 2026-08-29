using System.Reflection;

namespace Yello.Client;

/// <summary>
/// A deterministic handle on this assembly for the architecture suite
/// (<see cref="Assembly"/>). An empty project offers none, and the ring gates cannot assert
/// against an assembly they cannot load.
/// </summary>
/// <remarks>
/// <para>
/// Superseded 2026-08-29 by story 1.3. The previous text ended "Still no components - task cards,
/// columns, the context bar, dialogs, pickers and buttons are epic 2 onward", which stopped being
/// true when the registration surface landed.
/// </para>
/// <para>
/// What exists now: the token layer and base layer from story 1.2, plus this story's
/// <c>wwwroot/css/components.css</c>, a <c>Router</c>, a layout, the registration page, and the
/// first three components - a form field, a primary button and an inline error region. Also the
/// first localisation resources and the culture provider that sets the document's language, which
/// is what makes <c>base.css</c>'s 26-locale casing exclusions reachable at all.
/// </para>
/// <para>
/// Still absent, and owned elsewhere: task cards, columns, dialogs and pickers are epic 2 onward;
/// the context bar, the Space switcher and the Role chip are story 1.7's.
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
