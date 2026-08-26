using System.Reflection;

namespace Yello.Host;

/// <summary>
/// Values that more than one project has to agree on, read from the assembly metadata that
/// <c>Directory.Build.props</c> emits into every assembly.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why each consumer reads its own assembly rather than sharing this class.</b> Three
/// projects need one of these values - this one, <c>Yello.AppHost</c> and
/// <c>tests/Yello.Tests.Shared</c> - and no two of them can share a type. The AppHost's
/// project references are Aspire project <i>resources</i>, which the Aspire SDK marks
/// <c>ReferenceOutputAssembly=false</c>, so it cannot compile against Host at all;
/// <c>Yello.Tests.Shared</c> has an empty row in the ring table on purpose, because a fixture
/// that knew about a ring would let a suite reach a ring its own row forbids. So the reader is
/// written three times and the value is written once, which is the right way round: the
/// duplication is in the mechanism, not in the fact. A gate asserts that no source file states
/// either value literally.
/// </para>
/// <para>
/// Assembly metadata rather than a <c>const</c> shared across projects: a public constant is
/// copied into every consumer at compile time, so changing it would not reach an assembly that
/// had not been rebuilt. For a value whose whole job is to be the one place something is
/// stated, a compile-time copy is exactly the wrong semantics.
/// </para>
/// </remarks>
internal static class BuildConstants
{
    /// <summary>
    /// The name of the Aspire database resource. The AppHost registers it under this name and
    /// the Host asks for its connection string under this name; a rename that reached only one
    /// of them used to leave the Host starting normally with AC4's check silently skipped -
    /// same log, same exit code, no evidence.
    /// </summary>
    internal static string DatabaseResourceName { get; } =
        AssemblyMetadata.Read(typeof(BuildConstants).Assembly, "Yello.DatabaseResourceName");
}

/// <summary>
/// Reads a value <c>Directory.Build.props</c> stamped into an assembly.
/// </summary>
internal static class AssemblyMetadata
{
    internal static string Read(Assembly assembly, string key)
    {
        var value = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key.Equals(key, StringComparison.Ordinal))
            ?.Value;

        // Throwing is the point. These values are emitted by Directory.Build.props for every
        // project, so an absent one means the build no longer works the way every consumer
        // assumes - and a fallback default would be a second place the value is stated, which
        // is the defect this exists to remove.
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"Assembly metadata '{key}' is missing from {assembly.GetName().Name}. " +
                "Directory.Build.props emits it for every project; if it is gone, the value it " +
                "carried has no source at all.")
            : value;
    }
}
