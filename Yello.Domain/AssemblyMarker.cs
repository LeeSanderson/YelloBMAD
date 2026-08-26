using System.Reflection;

namespace Yello.Domain;

/// <summary>
/// A deterministic handle on this assembly for the architecture suite
/// (<see cref="Assembly"/>). An empty project offers none, and the ring gates cannot assert
/// against an assembly they cannot load.
/// </summary>
/// <remarks>
/// Story 1.1 deliberately adds no entities, invariants or ports here. The ring rule this
/// marker exists to police is that <c>Yello.Domain</c> references nothing at all.
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
