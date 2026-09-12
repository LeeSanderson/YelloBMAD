using System.Reflection;

namespace Yello.Infrastructure;

/// <summary>
/// A deterministic handle on this assembly for the architecture suite
/// (<see cref="Assembly"/>). An empty project offers none, and the ring gates cannot assert
/// against an assembly they cannot load.
/// </summary>
/// <remarks>
/// <para>
/// What this project contains: <c>YelloDbContext</c>, four entity configurations, one migration
/// creating Account, Space, Membership and StatusDefinition with their indexes and the row-level
/// security policy that scopes the last two, and the adapters behind the Domain's ports. Tables
/// are created by the story that first needs them, never upfront.
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
