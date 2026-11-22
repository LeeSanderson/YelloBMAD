// Declared in the production namespace on purpose, so moving the file into Yello.Merge changes nothing that uses it.
#pragma warning disable IDE0130
namespace Yello.Merge;
#pragma warning restore IDE0130

/// <summary>
/// The one way to obtain the merge strategy. The implementation behind it is never named outside <c>Yello.Merge</c>.
/// </summary>
/// <remarks>
/// A stand-in with no candidate behind it: the suite is committed before any implementation exists, and this throws until
/// the dev phase replaces the whole <c>PortStandIn/</c> folder with <c>Yello.Merge</c>'s own declarations.
/// </remarks>
public static class TextMerge
{
    public static ITextMergeStrategy CreateStrategy() =>
        throw new NotSupportedException("No merge candidate exists yet. The port is a stand-in in Yello.Tests.Merge/PortStandIn.");
}
