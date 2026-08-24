namespace Yello.Domain;

/// <summary>
/// A deterministic handle on this assembly for the architecture suite
/// (<c>typeof(Yello.Domain.AssemblyMarker).Assembly</c>). An empty project offers none,
/// and the ring gates cannot assert against an assembly they cannot load.
/// </summary>
/// <remarks>
/// Story 1.1 deliberately adds no entities, invariants or ports here. The ring rule this
/// marker exists to police is that <c>Yello.Domain</c> references nothing at all.
/// </remarks>
public static class AssemblyMarker;
