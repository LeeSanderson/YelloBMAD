using Yello.Merge.Sequence;

namespace Yello.Merge;

/// <summary>
/// The one way to obtain the merge strategy. The implementation behind it is never named outside <c>Yello.Merge</c>, so
/// replacing it changes this line and nothing that uses the port.
/// </summary>
public static class TextMerge
{
    public static ITextMergeStrategy CreateStrategy() => new SequenceMergeStrategy();
}
