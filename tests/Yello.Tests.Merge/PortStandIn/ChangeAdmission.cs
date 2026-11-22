// Declared in the production namespace on purpose, so moving the file into Yello.Merge changes nothing that uses it.
#pragma warning disable IDE0130
namespace Yello.Merge;
#pragma warning restore IDE0130

/// <summary>
/// What the port did with bytes or a change it was given. A refusal is a value the caller branches on, not an exception:
/// the server meets one per bad frame.
/// </summary>
/// <remarks>
/// A stand-in declared in the test project; see <see cref="ITextMergeStrategy"/>'s remarks.
/// </remarks>
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
