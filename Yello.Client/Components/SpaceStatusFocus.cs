namespace Yello.Client.Components;

/// <summary>
/// Which control of a <see cref="SpaceStatusRow"/> a focus request lands on.
/// </summary>
public enum SpaceStatusFocus
{
    /// <summary>
    /// The row's rename control, or its rename field while it is open.
    /// </summary>
    Rename,

    /// <summary>
    /// The row's up control, or its down control once the row is first.
    /// </summary>
    Up,

    /// <summary>
    /// The row's down control, or its up control once the row is last.
    /// </summary>
    Down,

    /// <summary>
    /// The row's remove control.
    /// </summary>
    Remove,
}
