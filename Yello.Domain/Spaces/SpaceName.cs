namespace Yello.Domain.Spaces;

/// <summary>
/// What a Space's name may be: required, trimmed, 1-200 characters, and not unique anywhere. Story 3.1, open
/// question 6 (a); FR-6.
/// </summary>
/// <remarks>
/// <para>
/// <b>200, not <c>ProjectName</c>'s 100</b>: the longest name registration provisions is a 128-character display
/// name plus "'s Space" (<see cref="PersonalSpaceName"/>), so a Personal Space's name must be submittable back
/// unchanged. 200 is also the column's length - <c>SpaceConfiguration.NameMaxLength</c> is this value, so there is
/// one source of truth.
/// </para>
/// <para>
/// <b>Not unique</b> (FR-6, AC5): two Accounts may each own a Space with the same name, and a collision discloses
/// nothing because nothing is ever compared.
/// </para>
/// </remarks>
public static class SpaceName
{
    private const int Longest = 200;

    /// <summary>
    /// The longest name, in UTF-16 code units after trimming - and the column's length.
    /// </summary>
    public static int MaxLength => Longest;

    /// <summary>
    /// Trims a submitted name and says whether what remains is one.
    /// </summary>
    /// <param name="submitted">The name as submitted, which may be absent.</param>
    /// <param name="name">The trimmed name, or empty when it is not one.</param>
    /// <returns><c>true</c> when the trimmed name is 1-200 characters.</returns>
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
