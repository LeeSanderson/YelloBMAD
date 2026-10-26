namespace Yello.Domain.Labels;

/// <summary>
/// The closed set of colours a Label may take, by key. UX-DR13, AC4; story 2.4, open question 3.
/// </summary>
/// <remarks>
/// <para>
/// <b>Keys only, never a colour value.</b> The token layer (<c>tokens.css</c>, <c>--label-{key}</c>) owns what each
/// key renders as, so a later adjustment of a fill changes no stored data. The Architecture suite holds this set and
/// the token family equal, and computes every fill against the AC4 rules.
/// </para>
/// <para>
/// <b>A constrained palette rather than a free colour</b>: every offered fill satisfies the contrast and separation
/// rules by construction, which a picker could not promise.
/// </para>
/// </remarks>
public static class LabelPalette
{
    /// <summary>
    /// The palette's keys, in the order the define form offers them.
    /// </summary>
    public static IReadOnlyList<string> Keys { get; } = ["rust", "ochre", "olive", "grass", "jade", "teal", "blue", "orchid"];

    /// <summary>
    /// Whether <paramref name="colour"/> is one of <see cref="Keys"/>, compared ordinally.
    /// </summary>
    /// <param name="colour">The submitted colour, which may be absent.</param>
    /// <returns><c>true</c> when it is a palette key.</returns>
    public static bool IsKnown(string? colour) =>
        colour is not null && Keys.Contains(colour, StringComparer.Ordinal);
}
