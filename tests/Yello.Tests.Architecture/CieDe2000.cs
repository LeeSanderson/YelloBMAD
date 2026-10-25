using System.Globalization;

namespace Yello.Tests.Architecture;

/// <summary>
/// The CIEDE2000 colour difference between two sRGB hex colours, computed rather than estimated - the separation rule
/// of story 2.4's AC4 and UX-DR13 ("at least ΔE2000 10 from the focus ring, danger, accent and presence colours").
/// Written by the story 2.4 ATDD run beside <see cref="WcagContrast"/>, for the same reasons that class gives for not
/// taking a package: it is arithmetic with no dependency, and a colour library would cost four gates and the strict
/// NuGet audit.
/// </summary>
/// <remarks>
/// <para>
/// <b>The formula is Sharma, Wu and Dalal (2005)</b>, "The CIEDE2000 color-difference formula: implementation notes,
/// supplementary test data, and mathematical observations", with <c>kL = kC = kH = 1</c>. Its published test pairs are
/// <c>LabelPaletteGateTests.The_CIEDE2000_harness_reproduces_the_published_test_pairs</c>, run before anything trusts
/// the figures - including pairs 11 and 13, which exercise the hue-mean branch across 0/360 degrees, where
/// implementations most often go wrong.
/// </para>
/// <para>
/// <b>sRGB to CIELAB</b> is IEC 61966-2-1 linearisation (the sRGB threshold <c>0.04045</c>, not WCAG's
/// <c>0.03928</c> - this is colorimetry, not WCAG luminance), the sRGB-to-XYZ matrix, and CIELAB under the D65 white
/// point. Nothing rounds.
/// </para>
/// </remarks>
internal static class CieDe2000
{
    private const double Pow25To7 = 6_103_515_625.0;

    private static readonly double Epsilon = Math.Pow(6.0 / 29.0, 3);

    private static readonly double Kappa = 3 * Math.Pow(6.0 / 29.0, 2);

    /// <summary>
    /// ΔE2000 between two sRGB hex colours.
    /// </summary>
    /// <param name="firstHex">A <c>#RRGGBB</c> or <c>#RGB</c> literal.</param>
    /// <param name="secondHex">Another.</param>
    /// <returns>The difference; 0 for the same colour.</returns>
    public static double Between(string firstHex, string secondHex) =>
        Between(Lab(firstHex), Lab(secondHex));

    /// <summary>
    /// ΔE2000 between two CIELAB colours.
    /// </summary>
    /// <param name="first">L*, a*, b*.</param>
    /// <param name="second">L*, a*, b*.</param>
    /// <returns>The difference.</returns>
    public static double Between((double L, double A, double B) first, (double L, double A, double B) second)
    {
        var (l1, a1, b1) = first;
        var (l2, a2, b2) = second;

        var meanChroma = (Math.Sqrt((a1 * a1) + (b1 * b1)) + Math.Sqrt((a2 * a2) + (b2 * b2))) / 2;
        var g = 0.5 * (1 - Math.Sqrt(Math.Pow(meanChroma, 7) / (Math.Pow(meanChroma, 7) + Pow25To7)));

        var a1Prime = (1 + g) * a1;
        var a2Prime = (1 + g) * a2;
        var c1Prime = Math.Sqrt((a1Prime * a1Prime) + (b1 * b1));
        var c2Prime = Math.Sqrt((a2Prime * a2Prime) + (b2 * b2));
        var h1Prime = HueDegrees(b1, a1Prime);
        var h2Prime = HueDegrees(b2, a2Prime);

        var deltaL = l2 - l1;
        var deltaC = c2Prime - c1Prime;
        var chromaProduct = c1Prime * c2Prime;
        var deltaH = 2 * Math.Sqrt(chromaProduct) * Math.Sin(Radians(HueDifference(h1Prime, h2Prime, chromaProduct) / 2));

        var meanL = (l1 + l2) / 2;
        var meanCPrime = (c1Prime + c2Prime) / 2;
        var meanH = HueMean(h1Prime, h2Prime, chromaProduct);

        var t = 1
            - (0.17 * Math.Cos(Radians(meanH - 30)))
            + (0.24 * Math.Cos(Radians(2 * meanH)))
            + (0.32 * Math.Cos(Radians((3 * meanH) + 6)))
            - (0.20 * Math.Cos(Radians((4 * meanH) - 63)));
        var deltaTheta = 30 * Math.Exp(-Math.Pow((meanH - 275) / 25, 2));
        var rotationC = 2 * Math.Sqrt(Math.Pow(meanCPrime, 7) / (Math.Pow(meanCPrime, 7) + Pow25To7));
        var sL = 1 + (0.015 * Math.Pow(meanL - 50, 2) / Math.Sqrt(20 + Math.Pow(meanL - 50, 2)));
        var sC = 1 + (0.045 * meanCPrime);
        var sH = 1 + (0.015 * meanCPrime * t);
        var rotation = -Math.Sin(Radians(2 * deltaTheta)) * rotationC;

        var lightness = deltaL / sL;
        var chroma = deltaC / sC;
        var hue = deltaH / sH;

        return Math.Sqrt((lightness * lightness) + (chroma * chroma) + (hue * hue) + (rotation * chroma * hue));
    }

    /// <summary>
    /// The CIELAB (D65) coordinates of an sRGB hex colour.
    /// </summary>
    /// <param name="hex">A hex literal <see cref="WcagContrast.IsHexColour"/> accepts.</param>
    /// <returns>L*, a*, b*.</returns>
    public static (double L, double A, double B) Lab(string hex)
    {
        if (!WcagContrast.IsHexColour(hex))
        {
            throw new InvalidOperationException($"'{hex}' is not a hex colour literal; a ΔE2000 figure cannot be computed for it.");
        }

        var digits = hex.Trim()[1..];

        if (digits.Length == 3)
        {
            digits = string.Concat(digits.Select(digit => new string(digit, 2)));
        }

        var red = Linearise(Channel(digits, 0));
        var green = Linearise(Channel(digits, 2));
        var blue = Linearise(Channel(digits, 4));

        var x = (0.4124564 * red) + (0.3575761 * green) + (0.1804375 * blue);
        var y = (0.2126729 * red) + (0.7151522 * green) + (0.0721750 * blue);
        var z = (0.0193339 * red) + (0.1191920 * green) + (0.9503041 * blue);

        var fx = LabF(x / 0.95047);
        var fy = LabF(y / 1.0);
        var fz = LabF(z / 1.08883);

        return ((116 * fy) - 16, 500 * (fx - fy), 200 * (fy - fz));
    }

    /// <summary>
    /// A difference formatted for a failure message a human has to act on.
    /// </summary>
    /// <param name="difference">The figure.</param>
    /// <returns>It, to one decimal place.</returns>
    public static string Format(double difference) => difference.ToString("0.0", CultureInfo.InvariantCulture);

    private static double HueDegrees(double b, double aPrime)
    {
        if (b is 0d && aPrime is 0d)
        {
            return 0;
        }

        var degrees = Math.Atan2(b, aPrime) * 180 / Math.PI;

        return degrees < 0 ? degrees + 360 : degrees;
    }

    private static double HueDifference(double h1, double h2, double chromaProduct)
    {
        if (chromaProduct is 0d)
        {
            return 0;
        }

        var difference = h2 - h1;

        return difference switch
        {
            > 180 => difference - 360,
            < -180 => difference + 360,
            _ => difference,
        };
    }

    private static double HueMean(double h1, double h2, double chromaProduct)
    {
        if (chromaProduct is 0d)
        {
            return h1 + h2;
        }

        if (Math.Abs(h1 - h2) <= 180)
        {
            return (h1 + h2) / 2;
        }

        return h1 + h2 < 360 ? (h1 + h2 + 360) / 2 : (h1 + h2 - 360) / 2;
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180;

    private static double LabF(double t) => t > Epsilon ? Math.Cbrt(t) : (t / Kappa) + (4.0 / 29.0);

    private static double Linearise(int channel)
    {
        var normalised = channel / 255.0;

        return normalised <= 0.04045
            ? normalised / 12.92
            : Math.Pow((normalised + 0.055) / 1.055, 2.4);
    }

    private static int Channel(string digits, int offset) =>
        int.Parse(digits.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
