using System.Reflection;

namespace Yello.Infrastructure;

/// <summary>
/// A deterministic handle on this assembly for the architecture suite
/// (<see cref="Assembly"/>). An empty project offers none, and the ring gates cannot assert
/// against an assembly they cannot load.
/// </summary>
/// <remarks>
/// <para>
/// Superseded 2026-08-29 by story 1.3, which is the story this comment used to be waiting for. It
/// previously read "Story 1.1 adds no DbContext, no migration and no table. Story 1.3 creates the
/// first three tables (Account, Space, Membership)" - and the count was wrong as well as the
/// tense: there are <b>four</b>, because <c>StatusDefinition</c> is seeded with the Space (FR-24).
/// </para>
/// <para>
/// What exists now: <c>YelloDbContext</c>, four entity configurations, one migration creating
/// Account, Space, Membership and StatusDefinition with their indexes and the row-level security
/// policy that scopes the last two, and the adapters behind the Domain's ports. Tables are still
/// created by the story that first needs them, never upfront.
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
