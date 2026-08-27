using System.Reflection;

namespace Yello.Client;

/// <summary>
/// A deterministic handle on this assembly for the architecture suite
/// (<see cref="Assembly"/>). An empty project offers none, and the ring gates cannot assert
/// against an assembly they cannot load.
/// </summary>
/// <remarks>
/// <para>
/// Superseded 2026-08-27 by story 1.2. This previously read "Story 1.1 adds no components and
/// no CSS. Story 1.2 owns the design foundations and gates the token count at exactly 30" - a
/// hand-off that became false when <c>wwwroot/css/tokens.css</c> landed.
/// </para>
/// <para>
/// The design foundations now exist: 30 colour tokens, 8 type roles, the spacing, radius,
/// border and motion scales in <c>wwwroot/css/tokens.css</c>, and the type roles, focus ring,
/// text link, target floor, reduced-motion contract and locale-aware casing in
/// <c>wwwroot/css/base.css</c>. Still no components - task cards, columns, the context bar,
/// dialogs, pickers and buttons are epic 2 onward.
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
