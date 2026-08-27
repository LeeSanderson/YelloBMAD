using System.Reflection;
using Yello.Host;

namespace Yello.Tests.Slices;

/// <summary>
/// Values the build stamps into <c>Yello.Host</c>, read the way the production code reads them.
/// </summary>
/// <remarks>
/// Shared by the in-process and out-of-process AC4 tests. The resource name is read from the
/// Host assembly's metadata rather than written as a literal for the same reason
/// <c>BuildConstants</c> does it: a literal would be a second source of truth, and a gate
/// asserts that the consumers of this value state it nowhere in source. A test that hard-coded
/// the key would also pass while the Host looked for a different one, which is the exact defect
/// the shared-value work closed.
/// </remarks>
internal static class HostMetadata
{
    /// <summary>
    /// The Aspire database resource name, i.e. the configuration key the Host asks for.
    /// </summary>
    internal static string DatabaseResourceName { get; } =
        typeof(AssemblyMarker).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key.Equals("Yello.DatabaseResourceName", StringComparison.Ordinal))
            .Value!;
}
