namespace Yello.Infrastructure;

/// <summary>
/// A deterministic handle on this assembly for the architecture suite
/// (<c>typeof(Yello.Infrastructure.AssemblyMarker).Assembly</c>). An empty project offers none,
/// and the ring gates cannot assert against an assembly they cannot load.
/// </summary>
/// <remarks>
/// Story 1.1 adds no DbContext, no migration and no table. Story 1.3 creates the first three tables (Account, Space, Membership). Tables are created by the story that first needs them, never upfront.
/// </remarks>
public static class AssemblyMarker;
