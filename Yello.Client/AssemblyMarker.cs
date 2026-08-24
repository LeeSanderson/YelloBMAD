namespace Yello.Client;

/// <summary>
/// A deterministic handle on this assembly for the architecture suite
/// (<c>typeof(Yello.Client.AssemblyMarker).Assembly</c>). An empty project offers none,
/// and the ring gates cannot assert against an assembly they cannot load.
/// </summary>
/// <remarks>
/// Story 1.1 adds no components and no CSS. Story 1.2 owns the design foundations and gates the token count at exactly 30, so a premature partial token set here is precisely the failure that AC exists to catch.
/// </remarks>
public static class AssemblyMarker;
