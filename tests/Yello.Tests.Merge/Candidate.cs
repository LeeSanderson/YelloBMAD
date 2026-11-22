using Yello.Merge;

namespace Yello.Tests.Merge;

/// <summary>
/// The one binding point between the conformance suite and whatever sits behind the port. No case names a production
/// type; each takes its strategy from here, so admitting a different candidate changes this line and no assertion.
/// </summary>
internal static class Candidate
{
    public static ITextMergeStrategy Strategy => TextMerge.CreateStrategy();
}
