using Xunit;
using Yello.Domain.Tasks;

namespace Yello.Tests.Slices.Tasks.MoveTask;

/// <summary>
/// Story 2.6 AC10 and AC3 at the unit level: <c>PositionKey.Between</c>, the jittered midpoint a move places its key with
/// (AD-15, AR-21, <c>PositionKey.cs:9-11</c>, <c>deferred-work.md:494</c>, Q10). No database - the column's BIN2 half of
/// "sorts strictly between" is <c>TaskEndpointTests.Keys_differing_only_in_case_coexist_in_one_column_and_read_back_in_ordinal_order</c>.
/// Written red by the story 2.6 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="Between"/> is a seam, not the product.</b> <c>PositionKey.Between</c> does not exist yet and story Task 2
/// leaves its jitter source open (a <c>Func&lt;int,int&gt;</c>, a <see cref="Random"/>, or a Domain interface). Every case
/// calls this one private method with a seeded <see cref="Random"/>; when the generator lands, bind the seam to it in one
/// line (adapting the jitter argument) and activate the cases one at a time.
/// </para>
/// <para>
/// <b>Assumed contract</b> (story Task 2): <c>lower == null</c> is "before the first", <c>upper == null</c> "after the
/// last", both null an empty column; <c>lower</c> not strictly below <c>upper</c> throws <see cref="ArgumentException"/>; a
/// key that would exceed <see cref="PositionKey.MaxLength"/> throws <see cref="InvalidOperationException"/>, as
/// <see cref="PositionKey.After"/> does today, for the store to answer as Q10's refusal. Correct the case if the dev phase
/// decides otherwise, and record it.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-21")]
[Trait("Requirement", "AD-15")]
public sealed class PositionKeyBetweenTests
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>
    /// PK-01 (AC10) - for 500 seeded random ordered pairs of alphabet keys, the key placed between them sorts strictly
    /// between them under <see cref="StringComparer.Ordinal"/>.
    /// </summary>
    [Fact]
    public void A_key_placed_between_two_keys_sorts_strictly_between_them()
    {
        var random = new Random(2026);

        for (var pair = 0; pair < 500; pair++)
        {
            var (lower, upper) = OrderedPair(random);
            var key = Between(lower, upper, new Random(pair));

            Assert.True(
                string.CompareOrdinal(lower, key) < 0 && string.CompareOrdinal(key, upper) < 0,
                $"'{key}' is not strictly between '{lower}' and '{upper}'.");
        }
    }

    /// <summary>
    /// PK-02 (AC10) - before the first key, after the last key, and into an empty column: the first sorts strictly
    /// before, the second strictly after, and the third is a key at all (story 2.2's <see cref="PositionKey.First"/> or
    /// another), each within <see cref="PositionKey.MaxLength"/>.
    /// </summary>
    [Fact]
    public void Before_the_first_after_the_last_and_into_an_empty_column_are_all_placed()
    {
        var before = Between(null, PositionKey.First, new Random(1));
        var after = Between(PositionKey.First, null, new Random(2));
        var empty = Between(null, null, new Random(3));

        Assert.True(string.CompareOrdinal(before, PositionKey.First) < 0, $"'{before}' is not before '{PositionKey.First}'.");
        Assert.True(string.CompareOrdinal(after, PositionKey.First) > 0, $"'{after}' is not after '{PositionKey.First}'.");
        Assert.False(string.IsNullOrEmpty(empty));
        Assert.All(new[] { before, after, empty }, key => Assert.InRange(key.Length, 1, PositionKey.MaxLength));
    }

    /// <summary>
    /// PK-03 (AC10, Q10) - repeated placement into the same gap - always immediately after the lower neighbour, the worst
    /// case for growth - stays strictly ordered, never exceeds <see cref="PositionKey.MaxLength"/>, and either completes
    /// 1,000 placements or stops at Q10's documented exhaustion. The count and the length reached are written to the test
    /// output for the Debug Log and the 2.9 ledger entry (key length under load).
    /// </summary>
    [Fact]
    public void Repeated_placement_into_one_gap_stays_ordered_and_its_growth_is_measured()
    {
        const string lower = "V0000";
        var upper = "V0001";
        var placed = new List<string>();
        var exhausted = false;

        for (var placement = 0; placement < 1000 && !exhausted; placement++)
        {
            try
            {
                upper = Between(lower, upper, new Random(placement));
                placed.Add(upper);
            }
            catch (InvalidOperationException)
            {
                exhausted = true;
            }
        }

        Assert.NotEmpty(placed);
        Assert.All(placed, key => Assert.InRange(key.Length, 1, PositionKey.MaxLength));

        // Each new key went in below the previous one and above the fixed lower neighbour.
        Assert.Equal(placed.OrderByDescending(key => key, StringComparer.Ordinal), placed);
        Assert.All(placed, key => Assert.True(string.CompareOrdinal(lower, key) < 0));

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Same-gap placements: {placed.Count} ({(exhausted ? "stopped at exhaustion" : "all 1,000 placed")}); longest key {placed.Max(key => key.Length)} characters.");
    }

    /// <summary>
    /// PK-04 (AC10, AC4's reason) - keys that differ only in case order exactly as <see cref="StringComparer.Ordinal"/>
    /// says: a key placed between <c>A</c> and <c>a</c>, and between <c>Z</c> and <c>a</c>, sorts between them ordinally
    /// (upper case before lower case), which is also <c>Latin1_General_100_BIN2</c>'s order for this alphabet.
    /// </summary>
    [Fact]
    public void A_key_between_keys_differing_only_in_case_follows_ordinal_order()
    {
        foreach (var (lower, upper) in new[] { ("A", "a"), ("Z", "a"), ("B0", "b0") })
        {
            var key = Between(lower, upper, new Random(7));

            Assert.True(
                string.CompareOrdinal(lower, key) < 0 && string.CompareOrdinal(key, upper) < 0,
                $"'{key}' is not ordinally between '{lower}' and '{upper}'.");
        }
    }

    /// <summary>
    /// PK-05 (AC10) - every character of every placed key is in the base-62 alphabet (digits, upper case, lower case), so
    /// .NET's ordinal order and the column's BIN2 order agree on it.
    /// </summary>
    [Fact]
    public void Every_character_of_a_placed_key_is_in_the_base_62_alphabet()
    {
        var random = new Random(62);

        for (var pair = 0; pair < 200; pair++)
        {
            var (lower, upper) = OrderedPair(random);
            var key = Between(lower, upper, new Random(pair));

            Assert.All(key, character => Assert.Contains(character, Alphabet));
        }
    }

    /// <summary>
    /// PK-06 (AC10, story Task 2's guard) - an impossible gap is refused: the same key on both sides, and the two sides
    /// reversed, each throw <see cref="ArgumentException"/>. The store must never ask for one.
    /// </summary>
    [Fact]
    public void An_impossible_gap_is_refused()
    {
        Assert.ThrowsAny<ArgumentException>(() => Between("V0000", "V0000", new Random(1)));
        Assert.ThrowsAny<ArgumentException>(() => Between("V0001", "V0000", new Random(1)));
    }

    /// <summary>
    /// PK-07 (AC10, AD-15 "jitter") - two independent placements into one gap are unlikely to coincide: across 100
    /// differently seeded jitter sources, the gap <c>V0000</c>..<c>V0001</c> receives more than one distinct key. With the
    /// server's range lock this is the second line of defence, not the convergence mechanism (spine <c>:429</c>).
    /// </summary>
    [Fact]
    public void Independent_placements_into_one_gap_carry_jitter()
    {
        var keys = Enumerable.Range(0, 100).Select(seed => Between("V0000", "V0001", new Random(seed))).ToHashSet(StringComparer.Ordinal);

        Assert.True(keys.Count > 1, "Every independently jittered placement into one gap produced the same key.");
    }

    /// <summary>
    /// The seam story Task 2 binds to <c>PositionKey.Between</c>.
    /// </summary>
    private static string Between(string? lower, string? upper, Random jitter) =>
        PositionKey.Between(lower, upper, jitter.Next);

    /// <summary>
    /// Two distinct alphabet keys of 1-8 characters, ordered ordinally, whose upper is not the lower with only
    /// <c>0</c>s appended (no key fits between <c>V</c> and <c>V0</c>).
    /// </summary>
    private static (string Lower, string Upper) OrderedPair(Random random)
    {
        while (true)
        {
            var first = RandomKey(random);
            var second = RandomKey(random);
            var compared = string.CompareOrdinal(first, second);

            if (compared == 0)
            {
                continue;
            }

            var (lower, upper) = compared < 0 ? (first, second) : (second, first);

            if (upper.StartsWith(lower, StringComparison.Ordinal) && upper[lower.Length..].All(character => character == Alphabet[0]))
            {
                continue;
            }

            return (lower, upper);
        }
    }

    private static string RandomKey(Random random) =>
        new([.. Enumerable.Range(0, random.Next(1, 9)).Select(_ => Alphabet[random.Next(Alphabet.Length)])]);
}
