namespace Yello.Infrastructure.Persistence;

/// <summary>
/// How the description log is compacted, bound from the <c>TaskDescription</c> configuration section.
/// </summary>
public sealed class TaskDescriptionOptions
{
    /// <summary>
    /// The configuration section the options are bound from.
    /// </summary>
    public static string SectionName => "TaskDescription";

    /// <summary>
    /// How many change rows may follow the latest snapshot before the write that reaches the number also appends a new one.
    /// </summary>
    /// <remarks>
    /// It bounds how many rows a rebuild reads, never how many are kept: change rows are never removed. Tests lower it to
    /// cross it in a few writes.
    /// </remarks>
    public int CompactionThreshold { get; set; } = 500;
}
