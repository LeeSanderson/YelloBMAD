namespace Yello.Tests.Merge.Conformance;

/// <summary>
/// The ways a payload can be damaged on the wire or in storage. Built from a valid encoding, so the cases rely only on
/// the port's one stated fact about bytes: the first is the format version.
/// </summary>
public enum Damage
{
    Empty,
    FormatByteOnly,
    Truncated,
    TrailingBytes,
    UnknownFormat,
}
