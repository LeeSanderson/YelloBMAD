namespace Yello.Domain.Tasks;

/// <summary>
/// A Task's position within its column (AD-15): a string ordered by ordinal - binary - comparison,
/// unique within its (Project, Status). Story 2.2, open question 2 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>Story 2.2 only ever appends.</b> A new Task takes a key strictly after its column's current last
/// key, so it lands at the bottom of the column. Story 2.6 replaces this with the jittered midpoint for
/// moves, and adds the BIN2 schema assertion and the mixed-case case.
/// </para>
/// <para>
/// <b>Two creations cannot compute the same key.</b> Every Task creation counts the Project's Tasks under
/// <c>UPDLOCK, HOLDLOCK</c> before its endpoint runs (the Tasks-per-Project bound, <c>BoundCounter</c>), so
/// two creations in one Project serialise there, and the second reads the first's key as the last.
/// </para>
/// <para>
/// <b>The alphabet is base 62 in ordinal order</b> - digits, then upper case, then lower case, which is
/// their order in ASCII - so a key compares the same in .NET's <see cref="StringComparer.Ordinal"/> and in
/// SQL Server's <c>Latin1_General_100_BIN2</c> collation, which the column is declared in.
/// </para>
/// </remarks>
public static class PositionKey
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    private const int Longest = 128;

    /// <summary>
    /// The key an empty column's first Task takes: in the middle of the alphabet, so later moves (2.6)
    /// have room before it, and wide enough that appending rarely lengthens it.
    /// </summary>
    public static string First => "V0000";

    /// <summary>
    /// The longest key the column holds, in characters.
    /// </summary>
    public static int MaxLength => Longest;

    /// <summary>
    /// A key strictly after <paramref name="last"/> in ordinal order.
    /// </summary>
    /// <remarks>
    /// Increments the key as a base-62 number of the same length, carrying from the right, so a key grows
    /// by one character only when every character is already the alphabet's last. A character outside the
    /// alphabet is stepped to the next alphabet character above it.
    /// </remarks>
    /// <param name="last">The column's current last key, or <c>null</c> for an empty column.</param>
    /// <returns>The new key.</returns>
    /// <exception cref="InvalidOperationException">The key would exceed <see cref="MaxLength"/>.</exception>
    public static string After(string? last)
    {
        if (string.IsNullOrEmpty(last))
        {
            return First;
        }

        var characters = last.ToCharArray();

        for (var index = characters.Length - 1; index >= 0; index--)
        {
            if (Above(characters[index]) is { } next)
            {
                characters[index] = next;

                for (var reset = index + 1; reset < characters.Length; reset++)
                {
                    characters[reset] = Alphabet[0];
                }

                return new string(characters);
            }
        }

        var lengthened = last + First[0];

        return lengthened.Length <= MaxLength
            ? lengthened
            : throw new InvalidOperationException("A column's position keys have reached their longest length.");
    }

    private static char? Above(char character)
    {
        foreach (var candidate in Alphabet)
        {
            if (candidate > character)
            {
                return candidate;
            }
        }

        return null;
    }
}
