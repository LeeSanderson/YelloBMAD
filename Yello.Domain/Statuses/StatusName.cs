namespace Yello.Domain.Statuses;

/// <summary>
/// What a Status's name may be: required, trimmed, 1-100 characters, and unique within a Project's effective set
/// compared case-insensitively.
/// </summary>
/// <remarks>
/// The uniqueness is the application's to check under the Project's lock, not an index's: a renamed default's name
/// lives in the delta, so no index sees the effective set. <see cref="Comparer"/> is case-insensitive like the name
/// columns' collation, so the filtered indexes that back it up never disagree with it on ordinary names.
/// </remarks>
public static class StatusName
{
    private const int Longest = 100;

    /// <summary>
    /// The longest name, in UTF-16 code units after trimming.
    /// </summary>
    public static int MaxLength => Longest;

    /// <summary>
    /// How two names are compared for uniqueness.
    /// </summary>
    public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Trims a submitted name and says whether what remains is one.
    /// </summary>
    /// <param name="submitted">The name as submitted, which may be absent.</param>
    /// <param name="name">The trimmed name, or empty when it is not one.</param>
    /// <returns><c>true</c> when the trimmed name is 1-100 characters.</returns>
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
