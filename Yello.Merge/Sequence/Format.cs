namespace Yello.Merge.Sequence;

/// <summary>
/// The format byte every encoding starts with. A reader refuses a higher one as unsupported and a lower one as malformed.
/// </summary>
internal static class Format
{
    internal const byte Current = 1;

    /// <summary>
    /// What the leading format byte says about bytes before they are read further.
    /// </summary>
    internal static ChangeAdmission Check(ReadOnlySpan<byte> encoded) => encoded switch
    {
        [] => ChangeAdmission.Malformed,
        [> Current, ..] => ChangeAdmission.UnsupportedVersion,
        [Current, ..] => ChangeAdmission.Admitted,
        _ => ChangeAdmission.Malformed,
    };
}
