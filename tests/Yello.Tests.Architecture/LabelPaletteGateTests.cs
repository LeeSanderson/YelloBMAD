using System.Collections;
using System.Globalization;
using System.Reflection;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.4 AC4 and AC12: the Label palette is a constrained set that satisfies the fill rules by construction, and the
/// build proves it by computation, never by a hand-written table - every fill at least 3:1 against the dark and the
/// light card ground simultaneously, at least 4.5:1 against its own ink, and at least ΔE2000 10 from both values of
/// <c>focus-ring</c>, <c>danger</c>, <c>accent</c> and <c>presence</c>. UX-DR7, UX-DR13, X-1. Written by the story 2.4
/// ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two cases are active today</b> and stay so: the CIEDE2000 harness against Sharma, Wu and Dalal's published pairs
/// (story Task 9: "a known-answer test ... before it is trusted"), and the rule's own oracle - the story's eight
/// proposed fills pass it, and each planted failure (a fill at the accent, a fill at white, an ink at
/// <c>text-primary</c>) is reported. The grounds and the avoided colours are read from <c>tokens.css</c>, so the
/// oracle moves with the token layer.
/// </para>
/// <para>
/// <b>Scaffolded against Q3 and Q4</b>: the palette is a separate family of <c>--label-{key}</c> tokens plus one
/// <c>--label-ink</c>, declared once and never rebound at the theme boundary, and its keys are the Domain's
/// <c>LabelPalette</c>. The story's table (computed in a scratch script at creation, reproduced exactly by this harness
/// during the ATDD run) is the starting point, not the authority: if a figure here disagrees, fix the table.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "UX-DR7")]
[Trait("Requirement", "UX-DR13")]
[Trait("Requirement", "NFR-9")]
public sealed class LabelPaletteGateTests
{
    private const double CardThreshold = 3.0;

    private const double InkThreshold = 4.5;

    private const double SeparationThreshold = 10.0;

    private const string FamilyPrefix = "label-";

    private const string InkName = "label-ink";

    private const string LabelPaletteTypeName = "Yello.Domain.Labels.LabelPalette";

    /// <summary>
    /// The four colours a Label fill must stay apart from, by semantic name; each is checked in both themes.
    /// </summary>
    private static readonly string[] AvoidedNames = ["focus-ring", "danger", "accent", "presence"];

    /// <summary>
    /// The story's eight proposed fills (story 2.4 Task 9) and its proposed ink, for the oracle.
    /// </summary>
    private static readonly (string Key, string Fill)[] ProposedFills =
    [
        ("rust", "#D2592D"),
        ("ochre", "#B18225"),
        ("olive", "#8A8A42"),
        ("grass", "#5B9E2E"),
        ("jade", "#469169"),
        ("teal", "#428A8A"),
        ("blue", "#357FD4"),
        ("orchid", "#CC3DD6"),
    ];

    private const string ProposedInk = "#0B1020";

    /// <summary>
    /// AC12, Task 9 - the CIEDE2000 harness reproduces Sharma, Wu and Dalal's (2005) published test pairs to four
    /// decimal places, the hue-mean pairs that straddle 0/360 degrees included, before any palette figure is trusted.
    /// <b>Active.</b>
    /// </summary>
    /// <param name="first">L*, a*, b* of the first colour, comma-separated.</param>
    /// <param name="second">L*, a*, b* of the second.</param>
    /// <param name="expected">The published ΔE2000.</param>
    [Theory]
    [InlineData("50,2.6772,-79.7751", "50,0,-82.7485", 2.0425)]
    [InlineData("50,0,0", "50,-1,2", 2.3669)]
    [InlineData("50,2.49,-0.001", "50,-2.49,0.0009", 7.1792)]
    [InlineData("50,2.49,-0.001", "50,-2.49,0.0011", 7.2195)]
    [InlineData("50,2.5,0", "73,25,-18", 27.1492)]
    [InlineData("50,2.5,0", "50,3.1736,0.5854", 1.0000)]
    [InlineData("60.2574,-34.0099,36.2677", "60.4626,-34.1751,39.4387", 1.2644)]
    [InlineData("2.0776,0.0795,-1.135", "0.9033,-0.0636,-0.5514", 0.9082)]
    [Trait("Requirement", "X-1")]
    public void The_CIEDE2000_harness_reproduces_the_published_test_pairs(string first, string second, double expected)
    {
        var difference = CieDe2000.Between(LabOf(first), LabOf(second));

        Assert.True(
            Math.Abs(difference - expected) < 0.00005,
            $"ΔE2000 {difference.ToString("0.00000", CultureInfo.InvariantCulture)}; the published figure is {expected.ToString("0.0000", CultureInfo.InvariantCulture)}.");
    }

    /// <summary>
    /// AC12 - the sRGB-to-CIELAB step the palette figures stand on: white is L* 100 and black L* 0, both neutral, and a
    /// colour against itself differs by nothing. <b>Active.</b>
    /// </summary>
    [Fact]
    [Trait("Requirement", "X-1")]
    public void The_sRGB_to_CIELAB_step_puts_white_and_black_at_the_ends_of_the_neutral_axis()
    {
        var white = CieDe2000.Lab("#FFFFFF");
        var black = CieDe2000.Lab("#000000");

        Assert.True(Math.Abs(white.L - 100) < 0.01 && Math.Abs(white.A) < 0.01 && Math.Abs(white.B) < 0.01, $"White is {white}.");
        Assert.True(Math.Abs(black.L) < 0.01 && Math.Abs(black.A) < 0.01 && Math.Abs(black.B) < 0.01, $"Black is {black}.");
        Assert.True(CieDe2000.Between("#D2592D", "#D2592D") < 0.00001);
    }

    /// <summary>
    /// The oracle for <see cref="Every_Label_fill_meets_the_fill_rules_in_both_themes"/>, against the real grounds and
    /// avoided colours in <c>tokens.css</c>: the story's eight proposed fills on its proposed ink pass the AC4 rule, and
    /// each planted failure the story names is reported - a fill at the accent (separation), a fill at white (the light
    /// card ground), and an ink at <c>text-primary</c>'s value (the ink ratio, for every fill). <b>Active: a permanent
    /// plant.</b>
    /// </summary>
    [Fact]
    [Trait("Requirement", "X-1")]
    public void The_fill_rule_passes_the_proposed_palette_and_reports_each_planted_failure()
    {
        var grounds = Grounds.FromTokens();

        Assert.Equal([], ProposedFills.SelectMany(fill => Violations(fill.Key, fill.Fill, ProposedInk, grounds)));

        var accent = CssCorpus.TokenValues["accent"].Trim();
        var accentPlant = Violations("planted", accent, ProposedInk, grounds);

        Assert.Contains(accentPlant, violation => violation.Contains("ΔE2000", StringComparison.Ordinal) && violation.Contains("accent", StringComparison.Ordinal));

        var whitePlant = Violations("planted", "#FFFFFF", ProposedInk, grounds);

        Assert.Contains(whitePlant, violation => violation.Contains("light card", StringComparison.Ordinal));

        // The dark ground's own value: 1:1 against the dark card (added at code review, so each ground has its plant).
        var darkCardPlant = Violations("planted", CssCorpus.TokenValues["surface-card"].Trim(), ProposedInk, grounds);

        Assert.Contains(darkCardPlant, violation => violation.Contains("dark card", StringComparison.Ordinal));

        var textPrimary = CssCorpus.TokenValues["text-primary"].Trim();

        Assert.All(ProposedFills, fill => Assert.Contains(Violations(fill.Key, fill.Fill, textPrimary, grounds), violation => violation.Contains("ink", StringComparison.Ordinal)));
    }

    /// <summary>
    /// AC4, AC12, UX-DR7 - every <c>--label-{key}</c> fill the token layer declares meets the rule in both themes,
    /// computed: 3:1 against <c>surface-card</c> and <c>surface-card-light</c>, 4.5:1 against <c>--label-ink</c>, and
    /// ΔE2000 10 from the dark and light value of each avoided colour. Anchored: the family and the ink are declared.
    /// </summary>
    [Fact]
    public void Every_Label_fill_meets_the_fill_rules_in_both_themes()
    {
        var fills = DeclaredFills();

        Assert.NotEmpty(fills);
        Assert.True(CssCorpus.TokenValues.TryGetValue(InkName, out var ink) && WcagContrast.IsHexColour(ink), $"--{InkName} is not declared as a hex colour.");

        var problems = fills.SelectMany(fill => Violations(fill.Key, fill.Value, ink.Trim(), Grounds.FromTokens())).ToList();

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// AC12, Task 9 - the fills the token layer declares are exactly the Domain's <c>LabelPalette</c> keys (the wire
    /// carries a key, the token layer owns its hex, Q3), and the family holds nothing else but <c>--label-ink</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-34")]
    public void The_token_palette_and_the_Domain_palette_are_the_same_set()
    {
        var palette = ProductionAssemblies.Domain.GetType(LabelPaletteTypeName);

        Assert.True(palette is not null, $"No {LabelPaletteTypeName} in the Domain assembly.");

        var domainKeys = StaticStringSet(palette);

        Assert.NotEmpty(domainKeys);
        Assert.Equal(domainKeys.Order(StringComparer.Ordinal), DeclaredFills().Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// AC12, Q3 - the keys the wire states for a client (<c>Yello.Contracts.Labels.LabelColours</c>, which the client
    /// offers and which cannot reference the Domain) are the Domain's palette, in the same order. Added by the dev
    /// phase with that contract.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-34")]
    public void The_wire_palette_is_the_Domain_palette_in_the_same_order()
    {
        Assert.NotEmpty(Yello.Domain.Labels.LabelPalette.Keys);
        Assert.Equal(Yello.Domain.Labels.LabelPalette.Keys, Yello.Contracts.Labels.LabelColours.Keys);
    }

    /// <summary>
    /// AC4 ("against both card grounds simultaneously") - one fill renders in both themes, so no theme-boundary rule
    /// rebinds a <c>--label-*</c> token; a rebinding would let a fill pass the gate in one theme while rendering another.
    /// Anchored: the boundary rules exist and the family is declared outside them.
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    public void No_theme_boundary_rule_rebinds_a_Label_token()
    {
        Assert.NotEmpty(CssCorpus.ThemeBoundaryRules);
        Assert.NotEmpty(DeclaredFills());

        var rebound = CssCorpus.ThemeBoundaryRules
            .SelectMany(rule => rule.Declarations)
            .Where(CssCorpus.IsCustomProperty)
            .Where(declaration => declaration.Property[2..].StartsWith(FamilyPrefix, StringComparison.Ordinal))
            .Select(declaration => declaration.Property)
            .ToList();

        Assert.Equal([], rebound);
    }

    /// <summary>
    /// The AC4 rule for one fill on one ink: every reason it fails, each naming the figure, or none.
    /// </summary>
    private static List<string> Violations(string key, string fill, string ink, Grounds grounds)
    {
        var problems = new List<string>();

        foreach (var (ground, value) in new[] { ("dark card", grounds.DarkCard), ("light card", grounds.LightCard) })
        {
            var ratio = WcagContrast.Ratio(fill, value);

            if (ratio < CardThreshold)
            {
                problems.Add($"{key} {fill} is {WcagContrast.Format(ratio)}:1 against the {ground} ground {value}; it needs {CardThreshold}:1.");
            }
        }

        var inkRatio = WcagContrast.Ratio(fill, ink);

        if (inkRatio < InkThreshold)
        {
            problems.Add($"{key} {fill} is {WcagContrast.Format(inkRatio)}:1 against its ink {ink}; it needs {InkThreshold}:1.");
        }

        foreach (var (name, value) in grounds.Avoided)
        {
            var difference = CieDe2000.Between(fill, value);

            if (difference < SeparationThreshold)
            {
                problems.Add($"{key} {fill} is ΔE2000 {CieDe2000.Format(difference)} from {name} {value}; it needs {SeparationThreshold}.");
            }
        }

        return problems;
    }

    /// <summary>
    /// The <c>--label-{key}</c> fills declared outside the theme boundary, by key, <c>--label-ink</c> excepted.
    /// </summary>
    private static Dictionary<string, string> DeclaredFills() =>
        CssCorpus.TokenValues
            .Where(token => token.Key.StartsWith(FamilyPrefix, StringComparison.Ordinal) && token.Key != InkName)
            .ToDictionary(token => token.Key[FamilyPrefix.Length..], token => token.Value.Trim(), StringComparer.Ordinal);

    /// <summary>
    /// The one public static member of <paramref name="type"/> holding a set of strings - the palette's keys, whatever
    /// the dev phase names it.
    /// </summary>
    private static List<string> StaticStringSet(Type type)
    {
        const BindingFlags members = BindingFlags.Public | BindingFlags.Static;
        var candidates = type.GetProperties(members).Select(property => (property.Name, Value: property.GetValue(null)))
            .Concat(type.GetFields(members).Select(field => (field.Name, Value: field.GetValue(null))))
            .Select(member => member.Value is IEnumerable set and not string ? set : null)
            .OfType<IEnumerable>()
            .ToList();

        Assert.True(candidates.Count == 1, $"{type.FullName} should expose exactly one public static set of keys; it exposes {candidates.Count}.");

        return [.. candidates[0].OfType<object>().Select(key => key.ToString() ?? string.Empty)];
    }

    private static (double L, double A, double B) LabOf(string triple)
    {
        var parts = triple.Split(',').Select(part => double.Parse(part, CultureInfo.InvariantCulture)).ToArray();

        return (parts[0], parts[1], parts[2]);
    }

    /// <summary>
    /// The two card grounds and the eight avoided values, read from the token layer's own declarations.
    /// </summary>
    private sealed record Grounds(string DarkCard, string LightCard, IReadOnlyList<(string Name, string Value)> Avoided)
    {
        public static Grounds FromTokens() =>
            new(
                Token("surface-card"),
                Token("surface-card-light"),
                [.. AvoidedNames.SelectMany(name => new[] { (name, Token(name)), ($"{name}-light", Token($"{name}-light")) })]);

        private static string Token(string name) =>
            CssCorpus.TokenValues.TryGetValue(name, out var value) && WcagContrast.IsHexColour(value)
                ? value.Trim()
                : throw new InvalidOperationException($"--{name} is not declared as a hex colour in tokens.css.");
    }
}
