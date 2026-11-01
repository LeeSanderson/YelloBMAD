using System.Globalization;

namespace Yello.Domain.Accounts;

/// <summary>
/// The one plausibility rule for an email address, shared by registration (story 1.3) and invitation (story 4.1, AC3).
/// The client mirrors it; a gate holds the two in agreement.
/// </summary>
/// <remarks>
/// <para>
/// <b>Shallow on purpose.</b> RFC 5322's grammar admits quoted local parts, comments and bracketed address literals, and
/// every "full" validating regular expression in circulation rejects addresses that genuinely deliver. The only thing a
/// stricter rule could achieve is refusing a real person's real address - and FR-10 says any address may be invited.
/// </para>
/// <para>
/// <b>Shallow is not the same as blind</b> (story 1.3 code review, 2026-09-10): whitespace, the control category and the
/// format category (zero-width and bidirectional marks) are refused, because an address that differs from a visible one
/// only by an invisible character would otherwise normalise to a separate identity.
/// </para>
/// <para>
/// Written as a scan rather than a regular expression so there is no pattern to backtrack: it runs on input before any
/// cost is incurred. It reads only its argument, so no outcome of it can vary with stored state (AD-23).
/// </para>
/// </remarks>
public static class EmailAddressRule
{
    private const int Longest = 254;

    /// <summary>
    /// RFC 5321 caps an email path at 256 octets including the angle brackets, leaving 254 for the address. Matches the
    /// columns.
    /// </summary>
    public static int MaxLength => Longest;

    /// <summary>
    /// Whether <paramref name="candidate"/>, once trimmed, is a plausible email address: 1 to 254 characters, exactly one
    /// <c>@</c> that is neither first nor last, and no whitespace, control or format character.
    /// </summary>
    /// <param name="candidate">The address as submitted.</param>
    /// <returns><c>true</c> when it is plausible.</returns>
    public static bool IsPlausible(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var trimmed = candidate.Trim();

        return trimmed.Length <= MaxLength
            && HasOneInnerAt(trimmed, trimmed.IndexOf('@', StringComparison.Ordinal))
            && !trimmed.Any(IsNeverPartOfAnAddress);
    }

    /// <summary>
    /// Exactly one <c>@</c> - the one at <paramref name="at"/> - neither first nor last.
    /// </summary>
    private static bool HasOneInnerAt(string candidate, int at) =>
        at > 0 && at == candidate.LastIndexOf('@') && at < candidate.Length - 1;

    /// <summary>
    /// Characters no email address can contain: whitespace, the control category, and the format category that carries
    /// the zero-width and bidirectional marks.
    /// </summary>
    private static bool IsNeverPartOfAnAddress(char character) =>
        char.IsWhiteSpace(character)
        || char.IsControl(character)
        || char.GetUnicodeCategory(character) is UnicodeCategory.Format;
}
