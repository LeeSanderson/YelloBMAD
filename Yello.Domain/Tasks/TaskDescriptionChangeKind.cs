namespace Yello.Domain.Tasks;

/// <summary>
/// What a <see cref="TaskDescriptionChange"/> row holds.
/// </summary>
public enum TaskDescriptionChangeKind
{
    /// <summary>
    /// One change an Account made, with its author and time.
    /// </summary>
    Change,

    /// <summary>
    /// The whole description's state up to the row's place, so a rebuild starts here rather than at the first change.
    /// </summary>
    Snapshot,
}
