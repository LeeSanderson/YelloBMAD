using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Yello.Contracts.Memberships;

/// <summary>
/// How a Membership is labelled wherever people are shown: its avatar's initials, and its display name - with a
/// Membership-scoped discriminator only when two names in the same Space collide. Story 4.4, AC3, Q13 (a); UX-DR34.
/// </summary>
/// <remarks>
/// <para>
/// <b>The reason, stated</b> (<c>EXPERIENCE.md:151</c>): "20px monospace initials and a display name do not disambiguate two
/// people, and the field every implementer reaches for to disambiguate a Membership picker is the address." Addresses are
/// visible in the Membership list and nowhere else (AD-23), so this takes no address at all and cannot reach for one.
/// </para>
/// <para>
/// <b>The discriminator</b> is the first four hex characters, upper case, of a SHA-256 of the Membership's id: stable across
/// calls and orderings, scoped to the Membership (a second Membership of the same Account elsewhere has another), and it
/// discloses nothing the reader does not already hold. A join-order ordinal was rejected (Q13 (b)): it renumbers when
/// someone leaves, so a reader's "Sam Lee (2)" would silently become someone else.
/// </para>
/// <para>
/// <b>Names collide when they look alike, not only when they are equal.</b> The collision key is the name after NFC, with
/// format characters (Unicode category <c>Cf</c>, the zero-width space among them) removed, trimmed, with each run of
/// whitespace collapsed to one space, compared ignoring case: "Sam Lee", "sam lee", "Sam  Lee " and a composed and a
/// decomposed "José" each read as one name at avatar size. Only the comparison folds - a name is shown as its owner wrote
/// it. Confusable letters from other scripts are not folded. Shared by the client and the server, so the Membership list,
/// the Assignee picker, the cards and the List View label people the same way.
/// </para>
/// </remarks>
public static class MembershipLabels
{
    private const int DiscriminatorLength = 4;


    /// <summary>
    /// The label for each Membership: the display name alone, or "Name · 3F2A" when another Membership in the list carries
    /// the same name.
    /// </summary>
    /// <param name="memberships">One Space's Memberships, by id and display name.</param>
    /// <returns>Each Membership's label, by id.</returns>
    public static IReadOnlyDictionary<Guid, string> Disambiguate(IReadOnlyList<(Guid MembershipId, string DisplayName)> memberships)
    {
        ArgumentNullException.ThrowIfNull(memberships);

        var collisions = memberships
            .GroupBy(membership => Normalised(membership.DisplayName), StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        var labels = new Dictionary<Guid, string>();

        foreach (var (membershipId, displayName) in memberships)
        {
            labels[membershipId] = collisions.Contains(Normalised(displayName))
                ? $"{displayName} · {Discriminator(membershipId)}"
                : displayName;
        }

        return labels;
    }

    /// <summary>
    /// The avatar's initials (DESIGN.md <c>avatar</c>): the first letter of the display name's first and last words, upper
    /// case; one letter for a one-word name.
    /// </summary>
    /// <param name="displayName">The display name.</param>
    /// <returns>The initials, or the empty string for a name with no words.</returns>
    public static string Initials(string displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);

        var words = displayName.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries);

        return words.Length switch
        {
            0 => string.Empty,
            1 => FirstLetter(words[0]),
            _ => FirstLetter(words[0]) + FirstLetter(words[^1]),
        };
    }

    private static string FirstLetter(string word) =>
        StringInfo.GetNextTextElement(word).ToUpper(CultureInfo.InvariantCulture);

    private static string Normalised(string displayName)
    {
        var folded = new StringBuilder(displayName.Length);
        var inWhitespace = false;

        foreach (var rune in displayName.Normalize(NormalizationForm.FormC).EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format)
            {
                continue;
            }

            if (Rune.IsWhiteSpace(rune))
            {
                inWhitespace = true;
                continue;
            }

            if (inWhitespace && folded.Length > 0)
            {
                folded.Append(' ');
            }

            inWhitespace = false;
            folded.Append(Rune.ToUpperInvariant(rune).ToString());
        }

        return folded.ToString();
    }

    private static string Discriminator(Guid membershipId) =>
        Convert.ToHexString(SHA256.HashData(membershipId.ToByteArray()))[..DiscriminatorLength];
}
