using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// The contrast harness. UX-DR7 and NFR-9, computed from the token layer on every build.
/// </summary>
/// <remarks>
/// <para>
/// <b>It parses <c>tokens.css</c> rather than restating a table in C#.</b> That is the whole
/// design: a hardcoded copy of the palette cannot fail when the CSS changes, so it would assert
/// that the harness agrees with itself. What must be gated is that the values the BROWSER
/// resolves clear their thresholds. The light palette is therefore read through the theme
/// boundary's own rebindings - <c>--accent: var(--accent-light)</c> resolved to a hex - so the
/// figures computed here are the figures rendered.
/// </para>
/// <para>
/// <b>Why compute at all.</b> <c>docs/bmad-coverage.md:84</c> records that when these figures
/// were done by hand during the UX phase, <i>"eight of twelve hand-computed figures were wrong,
/// and the two genuine AA failures sat in pairs the table never thought to state at all (accent-
/// as-link against body text, 2.66:1; Role chip fill against its own ground, 1.05:1)"</i>. Two
/// lessons: hand-computed contrast is unreliable at a rate of two-thirds, and the pairs that fail
/// are the ones nobody thought to state. The first is why AC4 says "computed by the WCAG 2.x
/// formula rather than estimated". The second is why <i>Pairs this gate does not cover</i> below
/// is written down rather than left to be rediscovered.
/// </para>
/// <para>
/// <b>Pairs this gate does not cover, and it is deliberate.</b> AC4 fixes the gated set at
/// exactly 18, so widening it is an amendment to UX-DR7 rather than a developer's decision. Four
/// further combinations are used in the product, and all four were computed during story
/// creation and re-verified here by hand at implementation time:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <c>revoked-edge</c> appears in none of the 18, yet <c>DESIGN.md:462</c> makes it the
///     structural border on the read-only description editor and UX-DR4 holds structural borders
///     to 3:1. On <c>surface-page</c> it is <b>7.03 dark / 5.81 light</b>; on <c>surface-card</c>
///     5.90 / 6.29. All pass.
///   </description></item>
///   <item><description>
///     <c>focus-ring</c> is gated on card and column but not on <c>surface-page</c>, yet the
///     description editor sits on the page ground, so focus lands there. <b>8.55 dark / 4.27
///     light</b>. Both pass.
///   </description></item>
/// </list>
/// <para>
/// The natural closing story is <b>7.2 / 7.4</b>, which builds the description editor. Both are
/// recorded in <c>deferred-work.md</c> with that owner.
/// </para>
/// <para>
/// <b>Four more ratios are ungated on purpose, because they generate rules rather than
/// thresholds.</b> <c>focus-ring</c> against <c>accent</c> is 1.45/1.48 - the ring WOULD vanish
/// against an accented control, and what saves it is <c>outline-offset: 2px</c>, gated by
/// <c>DesignFoundationGateTests</c>. <c>accent</c> against <c>text-primary</c> is 2.66/2.55,
/// which is why the text link is always underlined. <c>accent</c> against <c>danger</c> is
/// 1.19/1.08 and they converge under deuteranopia, which is why destructiveness is carried by
/// copy. <c>role-chip</c> against <c>surface-card</c> is 1.05/1.29, which is why the Role chip
/// needs a border to read as a chip at all. None is a contrast failure; each is a geometry or
/// copy requirement, and gating them as ratios would assert the wrong thing.
/// </para>
/// <para>
/// Lives in this suite rather than a new project, per <c>tests/TESTING-CONVENTIONS.md:24-26</c>:
/// later stories add cases to the existing suites. It is outside the A-1..A-15 ArchUnitNET
/// numbering for the same reason the project-file gates are - it reads text files, not bytecode.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "UX-DR7")]
[Trait("Requirement", "NFR-9")]
public sealed class ColorTokenContrastTests
{
    private const double TextThreshold = 4.5;
    private const double NonTextThreshold = 3.0;

    /// <summary>
    /// The widest ratio a pair of adjacent grounds may reach before it stops being an adjacency
    /// step and starts looking like a contrast pair someone forgot to gate.
    /// </summary>
    private const double AdjacencyCeiling = 1.5;

    /// <summary>
    /// The 15 semantic colour names, in the order <c>DESIGN.md</c> states them.
    /// </summary>
    private static readonly string[] SemanticNames =
    [
        "surface-page",
        "surface-column",
        "surface-card",
        "border-hairline",
        "text-primary",
        "text-muted",
        "accent",
        "accent-on",
        "focus-ring",
        "presence",
        "danger",
        "danger-on",
        "revoked-edge",
        "role-chip",
        "role-chip-on",
    ];

    /// <summary>
    /// The 18 gated pairs: twelve text pairs at 4.5:1 and six non-text and structural pairs at
    /// 3.0:1.
    /// </summary>
    /// <remarks>
    /// <c>DESIGN.md</c>'s table writes its grounds in shorthand - "on card", "on column", "on
    /// page". Those are <c>surface-card</c>, <c>surface-column</c> and <c>surface-page</c>; a
    /// harness driven literally by the table's strings would resolve none of them.
    /// <para>
    /// The light figure for <c>border-hairline</c> on <c>role-chip</c> is an em-dash in
    /// <c>DESIGN.md:341</c> - the source is one cell short of the 36 AC4 requires. It is
    /// <b>3.47</b> (<c>#6B7794</c> on <c>#E2E0FA</c>) and it passes. Computed like the other 35
    /// rather than special-cased, which is the point of computing rather than transcribing.
    /// </para>
    /// </remarks>
    private static readonly (string Foreground, string Background, ContrastClass Class)[] GatedPairs =
    [
        ("text-primary", "surface-card", ContrastClass.Text),
        ("text-primary", "surface-page", ContrastClass.Text),
        ("text-muted", "surface-card", ContrastClass.Text),
        ("text-muted", "surface-page", ContrastClass.Text),
        ("presence", "surface-card", ContrastClass.Text),
        ("presence", "surface-column", ContrastClass.Text),
        ("danger", "surface-card", ContrastClass.Text),
        ("danger-on", "danger", ContrastClass.Text),
        ("accent", "surface-card", ContrastClass.Text),
        ("accent", "surface-column", ContrastClass.Text),
        ("accent-on", "accent", ContrastClass.Text),
        ("role-chip-on", "role-chip", ContrastClass.Text),
        ("focus-ring", "surface-card", ContrastClass.NonText),
        ("focus-ring", "surface-column", ContrastClass.NonText),
        ("border-hairline", "surface-card", ContrastClass.NonText),
        ("border-hairline", "surface-column", ContrastClass.NonText),
        ("border-hairline", "surface-page", ContrastClass.NonText),
        ("border-hairline", "role-chip", ContrastClass.NonText),
    ];

    /// <summary>
    /// The two rows <c>DESIGN.md:345</c> states "for information, not as targets".
    /// </summary>
    /// <remarks>
    /// These separate grounds by hairline rather than by luminance, so a harness gating all
    /// twenty rows would fail permanently on these two and invite an unstated exception.
    /// <para>
    /// <b>The citation is <c>DESIGN.md:345</c>, not <c>:347</c>.</b> <c>epics.md:552</c> quotes
    /// <c>:347</c> - <i>"Two combinations that are load-bearing and must not be mistaken for
    /// contrast pairs:"</i> - but that sentence introduces the bullets at <c>:349</c> and
    /// <c>:350</c>, which are different combinations and not table rows at all: <c>accent</c>
    /// against <c>text-primary</c>, and <c>accent</c> against <c>danger</c>. The two adjacency
    /// ROWS are governed by <c>:345</c>: <i>"The last two rows are stated for information, not as
    /// targets."</i> The figures in the AC are correct and nothing about the gate changes - only
    /// the citation, which is why it is corrected here rather than copied.
    /// </para>
    /// </remarks>
    private static readonly (string Upper, string Lower)[] AdjacencyPairs =
    [
        ("surface-card", "surface-column"),
        ("surface-column", "surface-page"),
    ];

    private static readonly IReadOnlyDictionary<string, string> DarkPalette = BuildDarkPalette();

    private static readonly IReadOnlyDictionary<string, string> LightPalette = BuildLightPalette();

    /// <summary>
    /// Exactly 30 colour tokens: the 15 semantic names and their 15 <c>-light</c> siblings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The count is over declared NAMES, never over resolved values.</b> The 30 names resolve
    /// to only 26 distinct hex values, because three collisions are deliberate:
    /// <c>danger</c>/<c>revoked-edge</c> both <c>#FB7185</c>,
    /// <c>danger-light</c>/<c>revoked-edge-light</c> both <c>#BE123C</c>, and
    /// <c>surface-card-light</c>/<c>accent-on-light</c>/<c>danger-on-light</c> all
    /// <c>#FFFFFF</c>. A harness counting distinct values gets 26 and fails AC2 - and 26 is not a
    /// coincidence: the pre-remediation <c>epics.md</c> said "26 colour tokens", which is exactly
    /// the distinct-value count. Whoever wrote 26 was counting values. This assertion exists so
    /// nobody "re-corrects" 30 back down.
    /// </para>
    /// <para>
    /// Exact set equality in BOTH directions, and duplicates reported rather than collapsed.
    /// Story 1.1 hardened its solution-inventory gate from a subset check to exact equality and a
    /// later review still found the same defect class left in place elsewhere; a "contains at
    /// least" check here would accept a renamed token beside its replacement and report 30 while
    /// the design had 31.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_colour_token_layer_declares_exactly_the_thirty_names_the_design_states()
    {
        var expected = SemanticNames
            .Concat(SemanticNames.Select(n => $"{n}-light"))
            .ToHashSet(StringComparer.Ordinal);

        var declared = DeclaredColourTokenNames();
        var problems = new List<string>();

        problems.AddRange(declared
            .GroupBy(n => n, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"'--{g.Key}' is declared {g.Count()} times."));

        problems.AddRange(expected
            .Except(declared, StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => $"'--{n}' is missing."));

        problems.AddRange(declared
            .Distinct(StringComparer.Ordinal)
            .Except(expected, StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => $"'--{n}' is declared but is not one of the 30."));

        if (declared.Count != expected.Count)
        {
            problems.Add(
                $"{declared.Count} colour tokens are declared; the design states exactly " +
                $"{expected.Count}.");
        }

        Assert.True(problems.Count == 0,
            $"The colour token set does not match the design.{Environment.NewLine}{Environment.NewLine}" +
            string.Join(Environment.NewLine, problems.Select(p => $"  - {p}")) +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "The count is stated so an INCOMPLETE token set is detectable rather than merely " +
            "wrong. Count names, not values: the 30 names resolve to 26 distinct hex values and " +
            "three of those collisions are deliberate.");
    }

    /// <summary>
    /// All 18 gated pairs meet their threshold, in both palettes, computed from the tokens.
    /// </summary>
    /// <remarks>
    /// 36 computations from 18 pairs. The build fails if any drops below its threshold, because
    /// NFR-9 makes WCAG 2.1 AA a release gate at consumer stakes rather than an aspiration.
    /// <para>
    /// The two thinnest are worth knowing: <c>presence</c> on <c>surface-column</c> at
    /// <b>4.61</b> light against a 4.5 floor, and <c>focus-ring</c> on <c>surface-column</c> at
    /// <b>3.97</b> light against a 3.0 floor. A palette tweak that looks harmless will land on
    /// one of those first.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_gated_pair_meets_its_threshold_in_both_palettes()
    {
        var problems = new List<string>();
        var computed = 0;

        foreach (var (foreground, background, contrastClass) in GatedPairs)
        {
            var threshold = ThresholdOf(contrastClass);

            foreach (var (theme, palette) in Palettes())
            {
                if (!palette.TryGetValue(foreground, out var foregroundHex))
                {
                    problems.Add($"{theme}: '{foreground}' does not resolve to a hex colour.");
                    continue;
                }

                if (!palette.TryGetValue(background, out var backgroundHex))
                {
                    problems.Add($"{theme}: '{background}' does not resolve to a hex colour.");
                    continue;
                }

                computed++;
                var ratio = WcagContrast.Ratio(foregroundHex, backgroundHex);

                if (ratio < threshold)
                {
                    problems.Add(
                        $"{theme}: '{foreground}' ({foregroundHex}) on '{background}' " +
                        $"({backgroundHex}) is {WcagContrast.Format(ratio)}:1, below the required " +
                        $"{WcagContrast.Format(threshold)}:1.");
                }
            }
        }

        var expectedComputations = GatedPairs.Length * 2;

        if (computed != expectedComputations)
        {
            problems.Add(
                $"{computed} of {expectedComputations} ratios were computed. AC4 requires the " +
                "harness to run over BOTH palettes, so a pair that resolves in one theme and not " +
                "the other is half a gate.");
        }

        Assert.True(problems.Count == 0,
            "A gated contrast pair does not meet its threshold, or could not be computed." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            string.Join(Environment.NewLine, problems.Select(p => $"  - {p}")) +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "These are computed from Yello.Client/wwwroot/css/tokens.css by the WCAG 2.x formula, " +
            "not transcribed - so this is the palette as it will render, and NFR-9 makes WCAG " +
            "2.1 AA a release gate.");
    }

    /// <summary>
    /// The two surface-adjacency ratios are deliberately low, and are gated against nothing.
    /// </summary>
    /// <remarks>
    /// At roughly 1.09 and 1.10 the tonal steps are effectively invisible as boundaries, which is
    /// exactly why the border carries component identity alone. They are asserted LOW rather than
    /// asserted high: if one ever climbed past <see cref="AdjacencyCeiling"/> it would have
    /// stopped being an adjacency step, and the design decision that the hairline separates
    /// grounds - not luminance - would no longer be true of the palette.
    /// <para>
    /// Note the light ladder is flatter than the dark one at 1.07, so the light border matters
    /// more, not less.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_two_surface_adjacency_ratios_are_deliberately_low_and_are_not_gated()
    {
        var problems = new List<string>();

        foreach (var (upper, lower) in AdjacencyPairs)
        {
            if (Array.Exists(GatedPairs, p =>
                    (p.Foreground.Equals(upper, StringComparison.Ordinal)
                     && p.Background.Equals(lower, StringComparison.Ordinal))
                    || (p.Foreground.Equals(lower, StringComparison.Ordinal)
                        && p.Background.Equals(upper, StringComparison.Ordinal))))
            {
                problems.Add(
                    $"'{upper}' on '{lower}' has been added to the gated set. DESIGN.md:345 states " +
                    "the last two rows \"for information, not as targets\" - gating them would " +
                    "fail permanently and invite an unstated exception.");
            }

            foreach (var (theme, palette) in Palettes())
            {
                if (!palette.TryGetValue(upper, out var upperHex) ||
                    !palette.TryGetValue(lower, out var lowerHex))
                {
                    problems.Add($"{theme}: '{upper}' or '{lower}' does not resolve to a hex colour.");
                    continue;
                }

                var ratio = WcagContrast.Ratio(upperHex, lowerHex);

                if (ratio >= AdjacencyCeiling)
                {
                    problems.Add(
                        $"{theme}: '{upper}' on '{lower}' is {WcagContrast.Format(ratio)}:1, at or " +
                        $"above {WcagContrast.Format(AdjacencyCeiling)}:1. These grounds are meant " +
                        "to be one barely-perceptible tonal step apart; a real step means the " +
                        "three-ground ladder has been redrawn and the border's job has changed.");
                }

                if (ratio <= 1.0)
                {
                    problems.Add(
                        $"{theme}: '{upper}' on '{lower}' is {WcagContrast.Format(ratio)}:1 - the " +
                        "two grounds are identical, so the tonal ladder has collapsed to one step.");
                }
            }
        }

        Assert.True(problems.Count == 0,
            "The surface-adjacency rows are not in the state the design requires." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            string.Join(Environment.NewLine, problems.Select(p => $"  - {p}")) +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "DESIGN.md:345 names these two rows explicitly as stated for information rather than " +
            "as targets - they separate grounds by hairline, not by luminance.");
    }

    /// <summary>
    /// The gated set is exactly the 18 pairs the requirement names, split 12 and 6.
    /// </summary>
    /// <remarks>
    /// The 18 is itself gated so widening the set is a deliberate act that has to amend UX-DR7,
    /// rather than something a later story does in passing. The readiness report's remediation
    /// corrected this figure from 20 to 18; without this assertion nothing would stop it drifting
    /// back. See the class remarks for the four combinations knowingly outside it.
    /// </remarks>
    [Fact]
    public void The_gated_set_is_exactly_the_eighteen_pairs_the_requirement_names()
    {
        var problems = new List<string>();
        var textPairs = GatedPairs.Count(p => p.Class == ContrastClass.Text);
        var nonTextPairs = GatedPairs.Count(p => p.Class == ContrastClass.NonText);

        if (GatedPairs.Length != 18)
        {
            problems.Add($"The gated set holds {GatedPairs.Length} pairs; AC4 names 18.");
        }

        if (textPairs != 12)
        {
            problems.Add(
                $"{textPairs} pairs are gated at {WcagContrast.Format(TextThreshold)}:1; AC4 names " +
                "12 text pairs.");
        }

        if (nonTextPairs != 6)
        {
            problems.Add(
                $"{nonTextPairs} pairs are gated at {WcagContrast.Format(NonTextThreshold)}:1; AC4 " +
                "names 6 non-text and structural pairs.");
        }

        problems.AddRange(GatedPairs
            .GroupBy(p => $"{p.Foreground}|{p.Background}", StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"'{g.Key.Replace('|', '/')}' is gated {g.Count()} times, inflating the count."));

        problems.AddRange(GatedPairs
            .SelectMany(p => new[] { p.Foreground, p.Background })
            .Distinct(StringComparer.Ordinal)
            .Where(n => !SemanticNames.Contains(n, StringComparer.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => $"'{n}' is gated but is not one of the 15 semantic names."));

        Assert.True(problems.Count == 0,
            $"The gated pair set is not the one AC4 defines.{Environment.NewLine}{Environment.NewLine}" +
            string.Join(Environment.NewLine, problems.Select(p => $"  - {p}")) +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "Widening the set past 18 requires amending UX-DR7. Two structural pairs are " +
            "knowingly outside it - revoked-edge on surface-page and focus-ring on surface-page - " +
            "and both are recorded in deferred-work.md with story 7.2/7.4 as owner.");
    }

    /// <summary>
    /// Both palettes resolve every semantic name to a readable hex colour.
    /// </summary>
    /// <remarks>
    /// The gate above skips a pair it cannot resolve and reports it, which keeps its failure
    /// message about contrast. This one is about resolution itself, and it is what stops the
    /// harness quietly shrinking: a renamed token, or a theme boundary that stopped rebinding a
    /// name, would leave the light palette short an entry, and 36 computations would silently
    /// become 34.
    /// </remarks>
    [Fact]
    public void Both_palettes_resolve_every_semantic_name_to_a_hex_colour()
    {
        var problems = new List<string>();

        foreach (var (theme, palette) in Palettes())
        {
            problems.AddRange(SemanticNames
                .Where(n => !palette.ContainsKey(n))
                .Select(n => $"{theme}: '--{n}' does not resolve to a hex colour."));
        }

        Assert.True(problems.Count == 0,
            "A semantic colour name does not resolve to a hex value in both themes." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            string.Join(Environment.NewLine, problems.Select(p => $"  - {p}")) +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "The dark palette is read from the :root declarations and the light palette THROUGH " +
            "the theme boundary's own rebindings, so this also fails when the boundary stops " +
            "resolving a name - which is what would otherwise make the harness compute fewer " +
            "ratios while still reporting green.");
    }

    private static IEnumerable<(string Theme, IReadOnlyDictionary<string, string> Palette)> Palettes()
    {
        yield return ("dark", DarkPalette);
        yield return ("light", LightPalette);
    }

    /// <summary>
    /// The threshold each contrast class carries.
    /// </summary>
    /// <remarks>
    /// The class is carried as an enum rather than as a bare <c>double</c> on each row, so
    /// counting the twelve and the six is an enum comparison rather than a floating-point
    /// equality check - and so a row cannot be given a threshold that belongs to neither of the
    /// two classes AC4 defines.
    /// </remarks>
    private static double ThresholdOf(ContrastClass contrastClass) =>
        contrastClass == ContrastClass.Text ? TextThreshold : NonTextThreshold;

    /// <summary>
    /// The colour token names the token layer declares outside the theme boundary, duplicates
    /// preserved.
    /// </summary>
    /// <remarks>
    /// "Colour token" is decided by the value being a hex literal, not by the name. That is what
    /// keeps the count at 30 while the same file declares the type scale, the spacing scale, the
    /// radii, the border widths and the motion timings as custom properties too - and it is why
    /// <c>--border-hairline-width</c> does not inflate the count that <c>--border-hairline</c>
    /// belongs to.
    /// </remarks>
    private static IReadOnlyList<string> DeclaredColourTokenNames() =>
    [
        .. CssCorpus.Tokens.Rules
            .Where(r => !CssCorpus.IsInsideThemeBoundary(r.Offset))
            .SelectMany(r => r.Declarations)
            .Where(CssCorpus.IsCustomProperty)
            .Where(d => WcagContrast.IsHexColour(d.Value))
            .Select(d => d.Property[2..]),
    ];

    private static IReadOnlyDictionary<string, string> BuildDarkPalette()
    {
        var palette = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var name in SemanticNames)
        {
            if (CssCorpus.TokenValues.TryGetValue(name, out var value) && WcagContrast.IsHexColour(value))
            {
                palette[name] = value.Trim();
            }
        }

        return palette;
    }

    /// <summary>
    /// The light palette, resolved the way the browser resolves it.
    /// </summary>
    /// <remarks>
    /// Read through the theme boundary's rebindings rather than by appending <c>-light</c> to
    /// each name. Appending the suffix would compute the palette from a NAMING CONVENTION, and
    /// the convention is not what renders: a boundary that rebound <c>--accent</c> to
    /// <c>var(--presence-light)</c>, or failed to rebind it at all, would leave the harness
    /// verifying colours the light theme never shows. <c>DesignFoundationGateTests</c> separately
    /// requires every boundary rule to rebind all 15 names identically, which is what makes
    /// reading the first rule sufficient here.
    /// </remarks>
    private static IReadOnlyDictionary<string, string> BuildLightPalette()
    {
        var palette = new Dictionary<string, string>(StringComparer.Ordinal);

        if (CssCorpus.ThemeBoundaryBindings.Count == 0)
        {
            return palette;
        }

        foreach (var name in SemanticNames)
        {
            if (!CssCorpus.ThemeBoundaryBindings[0].TryGetValue(name, out var binding))
            {
                continue;
            }

            var resolved = CssCorpus.Resolve(binding);

            if (WcagContrast.IsHexColour(resolved))
            {
                palette[name] = resolved.Trim();
            }
        }

        return palette;
    }

    /// <summary>
    /// The two classes of gated pair AC4 defines, and nothing else.
    /// </summary>
    private enum ContrastClass
    {
        /// <summary>
        /// Text and images of text. WCAG 1.4.3 at AA: 4.5:1.
        /// </summary>
        Text,

        /// <summary>
        /// Non-text and structural contrast. WCAG 1.4.11 at AA: 3.0:1.
        /// </summary>
        NonText,
    }
}
