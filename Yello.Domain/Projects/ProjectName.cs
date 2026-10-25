namespace Yello.Domain.Projects;

/// <summary>
/// What a Project's name may be: required, trimmed, 1-100 characters, and not unique within a Space.
/// Story 2.1, open question 3.
/// </summary>
/// <remarks>
/// Not unique, deliberately: the PRD states no such rule, Space names are explicitly not unique, and a
/// uniqueness rule would make every rename a new refusal to design.
/// </remarks>
public static class ProjectName
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
