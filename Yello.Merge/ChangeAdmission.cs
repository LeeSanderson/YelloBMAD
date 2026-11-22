namespace Yello.Merge;

/// <summary>
/// What the port did with bytes or a change it was given. A refusal is a value the caller branches on, not an exception:
/// the server meets one per bad frame.
/// </summary>
public enum ChangeAdmission
{
    /// <summary>
    /// Accepted, including a change already held, which changes nothing.
    /// </summary>
    Admitted = 0,

    /// <summary>
    /// Empty, truncated, carrying trailing bytes, or otherwise unreadable.
    /// </summary>
    Malformed = 1,

    /// <summary>
    /// The leading format byte names an encoding this build does not know.
    /// </summary>
    UnsupportedVersion = 2,
}
