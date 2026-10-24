namespace Yello.Domain.ApiTokens;

/// <summary>
/// What a Token's name may be: required, trimmed, 1-100 characters. Story 1.8, open question 8.
/// </summary>
public static class ApiTokenName
{
    private const int Longest = 100;

    /// <summary>
    /// The longest name, in UTF-16 code units after trimming.
    /// </summary>
    public static int MaxLength => Longest;

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
