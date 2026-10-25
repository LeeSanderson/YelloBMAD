namespace Yello.Domain.Tasks;

/// <summary>
/// What a Task's title may be: required, trimmed, 1-200 characters, and not unique. Story 2.2, open
/// question 3 (a).
/// </summary>
/// <remarks>
/// Longer than a Project's name, because a title is the only content on a card and runs longer than a
/// name does. Nothing in the PRD states a length.
/// </remarks>
public static class TaskTitle
{
    private const int Longest = 200;

    /// <summary>
    /// The longest title, in UTF-16 code units after trimming.
    /// </summary>
    public static int MaxLength => Longest;

    /// <summary>
    /// Trims a submitted title and says whether what remains is one.
    /// </summary>
    /// <param name="submitted">The title as submitted, which may be absent.</param>
    /// <param name="title">The trimmed title, or empty when it is not one.</param>
    /// <returns><c>true</c> when the trimmed title is 1-200 characters.</returns>
    public static bool TryNormalise(string? submitted, out string title)
    {
        title = submitted?.Trim() ?? string.Empty;

        if (title.Length >= 1 && title.Length <= MaxLength)
        {
            return true;
        }

        title = string.Empty;

        return false;
    }
}
