namespace Yello.Host;

/// <summary>
/// A deterministic handle on this assembly for the architecture suite
/// (<c>typeof(Yello.Host.AssemblyMarker).Assembly</c>). An empty project offers none,
/// and the ring gates cannot assert against an assembly they cannot load.
/// </summary>
/// <remarks>
/// Story 1.1 adds no endpoints and no /sync WebSocket. Program.cs is the composition root only, plus AC4's one-shot connectivity log.
/// </remarks>
public static class AssemblyMarker;
