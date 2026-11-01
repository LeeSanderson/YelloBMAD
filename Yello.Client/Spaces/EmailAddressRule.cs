using System.Buffers;
using System.Globalization;
using System.Text;

namespace Yello.Client.Spaces;

/// <summary>
/// The email address plausibility rule, mirrored for the client: trimmed, 1-254 characters, exactly one <c>@</c> that is
/// neither first nor last, and no whitespace, control or format character. Story 4.1, AC3.
/// </summary>
/// <remarks>
/// <para>
/// <b>A courtesy, never the decision.</b> The invite form checks an address before sending it so an implausible one is
/// said at once; the server checks it again with the Domain's rule, and its <c>400 invalid-email-address</c> is what
/// stands.
/// </para>
/// <para>
/// <b>Held to the Domain's <c>EmailAddressRule</c> on every edge by a gate</b>:
/// <c>RefusalBoundaryGateTests.The_Domain_email_rule_and_the_clients_mirror_agree_on_every_edge</c>. The client cannot
/// reference the Domain ring.
/// </para>
/// </remarks>
public static class EmailAddressRule
{
    private const int Longest = 254;

    /// <summary>
    /// Whether a submitted address is plausible once trimmed.
    /// </summary>
    /// <param name="submitted">The address as typed.</param>
    /// <returns><c>true</c> when it is plausible.</returns>
    public static bool IsPlausible(string? submitted)
    {
        var trimmed = submitted?.Trim() ?? string.Empty;
        var at = trimmed.IndexOf('@', StringComparison.Ordinal);

        return trimmed.Length is >= 1 and <= Longest
            && OneInnerAt(trimmed, at)
            && HasOnlyVisibleScalars(trimmed)
            && trimmed.Normalize(NormalizationForm.FormC).Length <= Longest;
    }

    private static bool OneInnerAt(string candidate, int at) =>
        at > 0 && at == candidate.LastIndexOf('@') && at < candidate.Length - 1;

    private static bool HasOnlyVisibleScalars(ReadOnlySpan<char> candidate)
    {
        var remaining = candidate;

        while (!remaining.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(remaining, out var rune, out var consumed) != OperationStatus.Done
                || IsNeverPartOfAnAddress(rune))
            {
                return false;
            }

            remaining = remaining[consumed..];
        }

        return true;
    }

    private static bool IsNeverPartOfAnAddress(Rune rune) =>
        Rune.IsWhiteSpace(rune)
        || Rune.IsControl(rune)
        || Rune.GetUnicodeCategory(rune) is UnicodeCategory.Format;
}
