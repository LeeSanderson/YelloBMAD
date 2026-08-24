namespace Yello.Merge;

/// <summary>
/// A deterministic handle on this assembly for the architecture suite
/// (<c>typeof(Yello.Merge.AssemblyMarker).Assembly</c>). An empty project offers none,
/// and the ring gates cannot assert against an assembly they cannot load.
/// </summary>
/// <remarks>
/// Story 1.1 adds no merge implementation. AR-40a is still open and story 7.1 writes the conformance suite first, so this project exists here as an empty, referenced ring.
/// </remarks>
public static class AssemblyMarker;
