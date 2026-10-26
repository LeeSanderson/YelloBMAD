namespace Yello.Domain.Labels;

/// <summary>
/// What a Label's name may be: required, trimmed, 1-50 characters. Unique within its Space, compared
/// case-insensitively - a rule the database's unique index holds, not this type. Story 2.4, open question 2.
/// </summary>
/// <remarks>
/// No source states a length; 50 keeps a chip readable and bounds the free-form input (the rubric review's
/// "unbounded name length").
/// </remarks>
public static class LabelName
{
    private const int Longest = 50;

    /// <summary>
    /// The longest name, in UTF-16 code units after trimming.
    /// </summary>
    public static int MaxLength => Longest;

    /// <summary>
    /// Trims a submitted name and says whether what remains is one.
    /// </summary>
    /// <param name="submitted">The name as submitted, which may be absent.</param>
    /// <param name="name">The trimmed name, or empty when it is not one.</param>
    /// <returns><c>true</c> when the trimmed name is 1-50 characters.</returns>
    public static bool TryNormalise(string? submitted, out string name)
    {
        name = submitted?.Trim() ?? string.Empty;

        if (name.Length >= 1 && name.Length <= MaxLength)
        {
            return true;
        }

        name = string.Empty;

        return false;
    }
}
