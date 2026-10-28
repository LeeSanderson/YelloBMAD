namespace Yello.Domain.Tasks;

/// <summary>
/// A Task's position within its column (AD-15): a string ordered by ordinal - binary - comparison,
/// unique within its (Project, Status). Story 2.2, open question 2 (a); story 2.6, AC10.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two generators, one alphabet.</b> <see cref="After"/> appends: a creation (story 2.2) and a Status change over
/// the edit (story 2.3) put the Task after its column's last key. <see cref="Between"/> places: a move (story 2.6) puts
/// the Task strictly between two neighbours, before the first, after the last or into an empty column, with jitter. Only
/// the moved Task's key is ever written - no generator renumbers a sibling.
/// </para>
/// <para>
/// <b>Every key placer takes one lock first</b>: the Project's Task range under <c>UPDLOCK, HOLDLOCK</c> - a creation in
/// the pipeline's bound count (<c>BoundCounter</c>), a deletion and a move in the task store in that count's statement
/// shape, and a Status edit in a <c>MAX(CASE…)</c> read of the same index and range - so two placements into one column
/// serialise there and the second reads the first's key. A move reads its neighbours only after it holds that range. The jitter is a second line behind the lock, not the convergence
/// mechanism.
/// </para>
/// <para>
/// <b>The alphabet is base 62 in ordinal order</b> - digits, then upper case, then lower case, which is
/// their order in ASCII - so a key compares the same in .NET's <see cref="StringComparer.Ordinal"/> and in
/// SQL Server's <c>Latin1_General_100_BIN2</c> collation, which the column and its unique index are declared in.
/// </para>
/// <para>
/// <b>Placing into one gap again and again lengthens the key</b>: each placement roughly halves the gap, so a character
/// lasts about six placements. Repeated placement immediately after one neighbour reaches <see cref="MaxLength"/> after
/// several hundred placements (measured by <c>PositionKeyBetweenTests</c>), and the next is refused rather than
/// widening the column or renumbering (story 2.6, open question 10).
/// </para>
/// </remarks>
public static class PositionKey
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    private const int Longest = 128;

    /// <summary>
    /// The fewest candidate characters at one position for <see cref="Between"/> to choose among the middle half of them
    /// at random rather than take the middle one.
    /// </summary>
    private const int FewestForJitter = 4;

    private const int Halves = 2;

    private const int Quarters = 4;

    /// <summary>
    /// The key an empty column's first Task takes: in the middle of the alphabet, so later moves
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

    /// <summary>
    /// A key strictly between <paramref name="lower"/> and <paramref name="upper"/> in ordinal order, near the middle of
    /// the gap and jittered (AD-15).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Walks the two keys' common prefix, then at the first position where they differ takes a character strictly
    /// between theirs; when none fits, keeps the lower key's character and places after the rest of the lower key.
    /// Where at least <see cref="FewestForJitter"/> characters fit, one of the middle half is chosen by
    /// <paramref name="jitter"/>, so two independent placements into one gap are unlikely to coincide.
    /// </para>
    /// <para>
    /// The key it places never ends in the alphabet's first character when another key fits, because nothing sorts
    /// between a key and that key followed by the first character.
    /// </para>
    /// </remarks>
    /// <param name="lower">The key to sort after, or <c>null</c> for the top of the column.</param>
    /// <param name="upper">The key to sort before, or <c>null</c> for the bottom of the column.</param>
    /// <param name="jitter">
    /// Given a count <c>n</c>, returns a number from <c>0</c> to <c>n - 1</c>. Injected so a test can seed it.
    /// </param>
    /// <returns>The new key, in the alphabet.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="lower"/> does not sort strictly before <paramref name="upper"/>, or either holds a character outside
    /// the alphabet.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// No key of at most <see cref="MaxLength"/> characters fits between them.
    /// </exception>
    public static string Between(string? lower, string? upper, Func<int, int> jitter)
    {
        ArgumentNullException.ThrowIfNull(jitter);

        var from = lower ?? string.Empty;

        if (!InAlphabet(from) || (upper is not null && (upper.Length == 0 || !InAlphabet(upper))))
        {
            throw new ArgumentException("A position key holds only the base-62 alphabet.", nameof(lower));
        }

        if (upper is not null && string.CompareOrdinal(from, upper) >= 0)
        {
            throw new ArgumentException("The lower key must sort strictly before the upper key.", nameof(lower));
        }

        var key = Middle(from, upper, jitter)
            ?? throw new InvalidOperationException("No position key fits between those two keys.");

        return key.Length <= MaxLength
            ? key
            : throw new InvalidOperationException("A key between those two would exceed the longest length a column holds.");
    }

    /// <summary>
    /// A key strictly between <paramref name="lower"/> (empty: unbounded below) and <paramref name="upper"/>
    /// (<c>null</c>: unbounded above), or <c>null</c> when none exists.
    /// </summary>
    private static string? Middle(string lower, string? upper, Func<int, int> jitter)
    {
        var shared = upper is null ? 0 : SharedLength(lower, upper);

        // A bounded upper is longer than the shared prefix: were it a prefix of lower, it would not sort after lower.
        var prefix = lower[..shared];
        var low = shared < lower.Length ? Alphabet.IndexOf(lower[shared], StringComparison.Ordinal) : -1;
        var high = upper is null ? Alphabet.Length : Alphabet.IndexOf(upper[shared], StringComparison.Ordinal);

        // The characters strictly between low and high, never the first: a key ending in it leaves no room below it.
        var first = Math.Max(low + 1, 1);
        var count = high - first;

        if (count > 0)
        {
            return prefix + Alphabet[Choose(first, count, jitter)];
        }

        return low >= 0
            ? prefix + lower[shared] + Middle(lower[(shared + 1)..], null, jitter)
            : BelowUpper(prefix, upper!, shared, jitter);
    }

    /// <summary>
    /// The key when lower has ended at <paramref name="shared"/> and upper continues there with the alphabet's first or
    /// second character - so the key continues with the first - or <c>null</c> when nothing fits.
    /// </summary>
    private static string? BelowUpper(string prefix, string upper, int shared, Func<int, int> jitter)
    {
        if (upper[shared] != Alphabet[0])
        {
            return prefix + Alphabet[0] + Middle(string.Empty, null, jitter);
        }

        var rest = upper[(shared + 1)..];

        return rest.Length == 0
            ? null
            : prefix + Alphabet[0] + Middle(string.Empty, rest, jitter);
    }

    private static int SharedLength(string lower, string upper)
    {
        var shared = 0;

        while (shared < lower.Length && shared < upper.Length && lower[shared] == upper[shared])
        {
            shared++;
        }

        return shared;
    }

    private static int Choose(int first, int count, Func<int, int> jitter)
    {
        if (count < FewestForJitter)
        {
            return first + (count / Halves);
        }

        var quarter = count / Quarters;
        var span = count - quarter - quarter;

        return first + quarter + Math.Clamp(jitter(span), 0, span - 1);
    }

    private static bool InAlphabet(string key) =>
        key.All(character => Alphabet.Contains(character, StringComparison.Ordinal));

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
