using System.Reflection;

namespace Yello.Application;

/// <summary>
/// A deterministic handle on this assembly for the architecture suite
/// (<see cref="Assembly"/>). An empty project offers none, and the ring gates cannot assert
/// against an assembly they cannot load.
/// </summary>
/// <remarks>
/// The pipeline behaviours -- authorisation, Space resolution, refusal recording, idempotency,
/// NFR-8 bound checks -- live in the request pipeline, never in a slice; AR-3 makes a slice that
/// re-implements any of them a defect. The ports the pipeline calls are declared here, in
/// <c>Spaces</c>: <c>IActiveSpaceResolver</c>, <c>IAccessRefusalRecorder</c> and
/// <c>ISpacePresenceRegister</c>. Idempotency is story 2.1's.
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
