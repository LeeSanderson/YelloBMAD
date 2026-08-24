using System.Reflection;
using ArchUnitNET.Loader;

// ArchUnitNET.Domain declares its own Assembly and Architecture types, and this project's
// own namespace is Yello.Tests.Architecture. Both collide, so both are aliased rather than
// imported: `Architecture` unqualified would be read as this namespace, not as the loaded
// model.
using LoadedArchitecture = ArchUnitNET.Domain.Architecture;
using ReflectedAssembly = System.Reflection.Assembly;

namespace Yello.Tests.Architecture;

/// <summary>
/// Loads the eight production assemblies once, for the bytecode gates to assert against.
/// </summary>
/// <remarks>
/// <para>
/// Seven are reached through their <c>AssemblyMarker</c>. That type exists for exactly this
/// reason: an empty project offers no deterministic handle on its assembly, and
/// <c>typeof(Yello.Domain.AssemblyMarker).Assembly</c> is also a real compile-time
/// reference, which is what guarantees the DLL is copied next to the test binary.
/// </para>
/// <para>
/// <c>Yello.AppHost</c> has no marker - it holds only top-level statements - so it is
/// loaded by name. Its DLL still arrives in the output directory via the project reference.
/// </para>
/// </remarks>
internal static class ProductionAssemblies
{
    public static ReflectedAssembly Domain { get; } = typeof(global::Yello.Domain.AssemblyMarker).Assembly;

    public static ReflectedAssembly Application { get; } = typeof(global::Yello.Application.AssemblyMarker).Assembly;

    public static ReflectedAssembly Infrastructure { get; } = typeof(global::Yello.Infrastructure.AssemblyMarker).Assembly;

    public static ReflectedAssembly Host { get; } = typeof(global::Yello.Host.AssemblyMarker).Assembly;

    public static ReflectedAssembly Contracts { get; } = typeof(global::Yello.Contracts.AssemblyMarker).Assembly;

    public static ReflectedAssembly Merge { get; } = typeof(global::Yello.Merge.AssemblyMarker).Assembly;

    public static ReflectedAssembly Client { get; } = typeof(global::Yello.Client.AssemblyMarker).Assembly;

    /// <summary>The AppHost, loaded by name because it carries no AssemblyMarker.</summary>
    public static ReflectedAssembly AppHost { get; } = ReflectedAssembly.Load(new AssemblyName("Yello.AppHost"));

    public static IReadOnlyList<ReflectedAssembly> All { get; } =
    [
        Domain, Application, Infrastructure, Host, Contracts, Merge, Client, AppHost,
    ];

    /// <summary>
    /// The loaded architecture, built once and shared. ArchUnitNET analyses compiled
    /// bytecode through Mono.Cecil, so this is a materially different view of the solution
    /// from Gate A's - and a strictly weaker one for undeclared intent, which is why both
    /// gates exist. Roslyn emits no <c>AssemblyRef</c> for a referenced assembly whose types
    /// are never used, so a ring-violating reference that no code depends on yet is
    /// invisible here.
    /// </summary>
    public static LoadedArchitecture Architecture { get; } = new ArchLoader()
        .LoadAssemblies([.. All])
        .Build();
}
