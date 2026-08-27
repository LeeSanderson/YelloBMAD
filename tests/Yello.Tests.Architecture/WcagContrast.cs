using System.Globalization;

namespace Yello.Tests.Architecture;

/// <summary>
/// WCAG 2.x relative luminance and contrast ratio, computed rather than restated.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is here rather than in a package.</b> It is twenty lines of <c>double</c>
/// arithmetic with no dependency. A colour library would have to satisfy four gates together -
/// a pin in <c>Directory.Packages.props</c>, an entry in <c>ExpectedNonAr1Pins</c> with a stated
/// reason, a version-less <c>PackageReference</c>, and not being a
/// <c>GlobalPackageReference</c> - and would expose every build in the repository to
/// <c>NuGetAuditMode=all</c> with <c>NuGetAuditLevel=low</c> and <c>NU1900-NU1904</c> promoted
/// to errors. Any advisory at any severity, direct or transitive, would then break every build
/// until someone hand-pinned forward. That posture was reviewed and deliberately kept strict
/// during story 1.1; it is not worth spending on arithmetic.
/// </para>
/// <para>
/// <b>Why compute at all.</b> <c>docs/bmad-coverage.md:84</c> records what happened when these
/// figures were done by hand during the UX phase: <i>"Eight of twelve hand-computed figures were
/// wrong, and the two genuine AA failures sat in pairs the table never thought to state at
/// all"</i>. Hand-computed contrast is unreliable at a rate of two-thirds, which is why AC4 says
/// "computed by the WCAG 2.x formula rather than estimated".
/// </para>
/// <para>
/// The arithmetic is identical across WCAG 2.0, 2.1 and 2.2 - sRGB linearisation at the
/// <c>0.03928</c> threshold and <c>(L1+0.05)/(L2+0.05)</c> - so AC4's "WCAG 2.x formula" names
/// the maths, not a version. NFR-9 pins <b>2.1 AA</b> as the release gate.
/// </para>
/// <para>
/// Nothing here rounds. <c>Math.Round</c> without an explicit <c>MidpointRounding</c> is banned
/// by the coding standard, and rounding would be wrong anyway: the assertion is "at least 4.5",
/// not "equals 4.61". Rounding is applied only when formatting a figure into a failure message.
/// </para>
/// </remarks>
internal static class WcagContrast
{
    /// <summary>
    /// The contrast ratio between two sRGB hex colours, in the range 1.0 to 21.0.
    /// </summary>
    /// <remarks>
    /// Order-independent by construction: the formula puts the lighter luminance on top, so a
    /// pair stated as "text on card" and one stated as "card on text" yield the same figure. The
    /// contrast table is written foreground-on-background, and a gate that depended on that
    /// order would be one transcription away from reporting a passing pair as a failing one.
    /// </remarks>
    public static double Ratio(string firstHex, string secondHex)
    {
        var first = RelativeLuminance(firstHex);
        var second = RelativeLuminance(secondHex);

        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    /// <summary>
    /// The WCAG relative luminance of an sRGB hex colour.
    /// </summary>
    public static double RelativeLuminance(string hex)
    {
        var (red, green, blue) = Channels(hex);

        return (0.2126 * Linearise(red)) + (0.7152 * Linearise(green)) + (0.0722 * Linearise(blue));
    }

    /// <summary>
    /// A ratio formatted for a failure message a human has to act on.
    /// </summary>
    public static string Format(double ratio) =>
        ratio.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>
    /// Whether a value is a hex colour literal this class can read.
    /// </summary>
    public static bool IsHexColour(string value)
    {
        var trimmed = value.Trim();

        if (trimmed.Length is not (4 or 7) || trimmed[0] != '#')
        {
            return false;
        }

        return trimmed[1..].All(Uri.IsHexDigit);
    }

    /// <summary>
    /// sRGB linearisation, at the 0.03928 threshold WCAG 2.x states.
    /// </summary>
    /// <remarks>
    /// The published threshold is <c>0.03928</c>. Several implementations use <c>0.04045</c>
    /// instead - the value from the sRGB standard itself, which the WCAG text does not use. The
    /// difference is immaterial at these colours but the gate cites a specification, so it
    /// computes what that specification says.
    /// </remarks>
    private static double Linearise(int channel)
    {
        var normalised = channel / 255.0;

        return normalised <= 0.03928
            ? normalised / 12.92
            : Math.Pow((normalised + 0.055) / 1.055, 2.4);
    }

    /// <summary>
    /// The three channels of a <c>#RGB</c> or <c>#RRGGBB</c> literal.
    /// </summary>
    /// <remarks>
    /// An unreadable value throws rather than defaulting to black. A silent zero would make a
    /// malformed token compute a plausible ratio against its ground - frequently a passing one,
    /// since black contrasts well with most of this palette - so the harness would report green
    /// over a colour the browser cannot render.
    /// </remarks>
    private static (int Red, int Green, int Blue) Channels(string hex)
    {
        var trimmed = hex.Trim();

        if (!IsHexColour(trimmed))
        {
            throw new InvalidOperationException(
                $"'{hex}' is not a hex colour literal this harness can read. The token layer " +
                "states every colour as #RGB or #RRGGBB; a computed colour, a named colour or a " +
                "colour function cannot be verified against a contrast threshold from the CSS " +
                "text alone, so it is refused rather than skipped.");
        }

        var digits = trimmed[1..];

        return digits.Length == 3
            ? (Channel(digits[0], digits[0]), Channel(digits[1], digits[1]), Channel(digits[2], digits[2]))
            : (Channel(digits[0], digits[1]), Channel(digits[2], digits[3]), Channel(digits[4], digits[5]));
    }

    private static int Channel(char high, char low) =>
        int.Parse([high, low], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
