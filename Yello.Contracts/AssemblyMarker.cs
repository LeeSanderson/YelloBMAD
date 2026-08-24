namespace Yello.Contracts;

/// <summary>
/// A deterministic handle on this assembly for the architecture suite
/// (<c>typeof(Yello.Contracts.AssemblyMarker).Assembly</c>). An empty project offers none,
/// and the ring gates cannot assert against an assembly they cannot load.
/// </summary>
/// <remarks>
/// Story 1.1 adds no wire DTOs. Contracts references nothing, so it is shared by client and server without dragging either ring across the boundary.
/// </remarks>
public static class AssemblyMarker;
