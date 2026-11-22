namespace Yello.Merge.Sequence;

/// <summary>
/// The format byte every encoding starts with. A reader refuses a higher one as unsupported and a lower one as malformed.
/// </summary>
/// <remarks>
/// Replica state has a format of its own. It moved to 2 when the withdrawn set became keyed by change identity, so that a
/// reload under a new identity keeps refusing what the old identity withdrew; state in format 1 is refused as unsupported
/// rather than read with the hole it had.
/// </remarks>
internal static class Format
{
    internal const byte Current = 1;

    internal const byte State = 2;

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

    /// <summary>
    /// What the leading byte of replica state says: a retired state format is unsupported, not malformed.
    /// </summary>
    internal static ChangeAdmission CheckState(ReadOnlySpan<byte> encoded) => encoded switch
    {
        [] or [0, ..] => ChangeAdmission.Malformed,
        [State, ..] => ChangeAdmission.Admitted,
        _ => ChangeAdmission.UnsupportedVersion,
    };
}
