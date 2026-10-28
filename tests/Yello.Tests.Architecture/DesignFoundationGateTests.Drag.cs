using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.7's design gates on the drag: the lifted card takes <c>task-card-lifted</c> - the 2px emphasis border, the
/// one shadow in the product from its token, the 1-degree tilt - and lifts on <c>motion.lift</c>; the dropped card settles
/// on <c>motion.settle</c>; the drop zone takes <c>drop-zone</c> - dashed in <c>focus-ring</c>, its words in
/// <c>text-primary</c>, 3px, never filled; and no rule
/// makes a surface carrying a colour token translucent. AC1, AC7, AC12; UX-DR4, UX-DR5, UX-DR6, UX-DR8;
/// <c>DESIGN.md:145-148, :197-200, :435, :452, :466, :506-521</c>; <c>deferred-work.md:36, :52</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Selectors are the built ones</b>: <c>.task-card.task-card--lifted</c> for the lifted card,
/// <c>.task-card.task-card--settled</c> for the settle, <c>.drop-zone</c> for the zone. The browser cases
/// (<c>DragTests</c>) read the computed style of the same elements mid-drag.
/// </para>
/// <para>
/// <b>The shadow exception lives in <c>No_surface_carries_a_shadow</c></b>, narrowed to exactly the lifted card by
/// <see cref="IsTheLiftedCardsShadow"/>; this class holds the lifted card to taking it.
/// </para>
/// </remarks>
public sealed partial class DesignFoundationGateTests
{
    private const string LiftedCardSelector = ".task-card.task-card--lifted";

    private const string SettledCardSelector = ".task-card.task-card--settled";

    private const string DropZoneClass = ".drop-zone";

    private const string LiftedShadowToken = "--shadow-lifted";

    private static readonly Regex OpacityBelowOne = new(@"^\s*(0?\.\d+|0|\d{1,2}(\.\d+)?%)\s*$", RegexOptions.None, TimeSpan.FromSeconds(1));

    /// <summary>
    /// AC1, AC12, UX-DR4, UX-DR5, <c>DESIGN.md:145-148</c> - the lifted card takes the 2px emphasis border, the shadow from
    /// its token and a -1deg tilt, in one rule.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR4")]
    [Trait("Requirement", "UX-DR22")]
    public void The_lifted_card_takes_the_emphasis_border_the_shadow_token_and_the_tilt()
    {
        var declarations = DeclarationsOf(LiftedCardSelector);

        Assert.Contains(declarations, d => d.Property == "border-width" && d.Value == "var(--border-emphasis-width)");
        Assert.Contains(declarations, d => d.Property == "box-shadow" && d.Value == $"var({LiftedShadowToken})");
        Assert.Contains(declarations, d => (d.Property == "rotate" && d.Value == "-1deg") || (d.Property == "transform" && d.Value.Contains("rotate(-1deg)", StringComparison.Ordinal)));
        Assert.True(CssCorpus.TokenValues.ContainsKey("shadow-lifted"), "The token layer declares no '--shadow-lifted'.");
    }

    /// <summary>
    /// AC1, AC12, UX-DR6, <c>DESIGN.md:506-521</c> - the lift transitions on <c>--motion-lift</c> and the settle animates on
    /// <c>--motion-settle</c>, each with no literal duration (the reduced-motion reset in base.css then removes both).
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR6")]
    public void The_lift_and_the_settle_run_on_their_motion_tokens()
    {
        var lift = DeclarationsOf(LiftedCardSelector).Where(d => MotionProperties.Contains(d.Property, StringComparer.Ordinal)).ToList();
        var settle = DeclarationsOf(SettledCardSelector).Where(d => MotionProperties.Contains(d.Property, StringComparer.Ordinal)).ToList();

        Assert.Contains(lift, d => d.Value.Contains("var(--motion-lift)", StringComparison.Ordinal));
        Assert.Contains(settle, d => d.Value.Contains("var(--motion-settle)", StringComparison.Ordinal));
        Assert.All(lift.Concat(settle), d => Assert.DoesNotMatch(@"\d+m?s(?![\w-])", d.Value));
    }

    /// <summary>
    /// AC1, AC12, UX-DR5, <c>DESIGN.md:197-200, :466</c> - the drop zone is the 2px dashed <c>focus-ring</c> border with
    /// 3px corners, and NEVER filled: no rule styling it sets a background, so the order around it stays readable mid-drag.
    /// Its words are <c>text-primary</c>, not the <c>focus-ring</c> DESIGN.md:199 names: <c>focus-ring</c> on
    /// <c>surface-column</c> is 3.97:1 in the light theme, short of the 4.5:1 text needs (NFR-9; recorded for UX in
    /// <c>deferred-work.md</c>).
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR5")]
    [Trait("Requirement", "UX-DR22")]
    [Trait("Requirement", "NFR-9")]
    public void The_drop_zone_is_dashed_in_the_focus_ring_colour_and_never_filled()
    {
        var declarations = CssCorpus.AllDeclarations()
            .Where(d => SelectsComponent(d.Rule.Selector, DropZoneClass))
            .Select(d => (Property: d.Declaration.Property, Value: d.Declaration.Value.Trim()))
            .ToList();

        Assert.Contains(declarations, d => d.Property == "border"
            && d.Value.Contains("var(--border-emphasis-width)", StringComparison.Ordinal)
            && d.Value.Contains("dashed", StringComparison.Ordinal)
            && d.Value.Contains("var(--focus-ring)", StringComparison.Ordinal));
        Assert.Contains(declarations, d => d.Property == "color" && d.Value == "var(--text-primary)");
        Assert.DoesNotContain(declarations, d => d.Property == "color" && d.Value == "var(--focus-ring)");
        Assert.Contains(declarations, d => d.Property == "border-radius" && d.Value == "var(--radius-md)");
        Assert.DoesNotContain(declarations, d => d.Property.StartsWith("background", StringComparison.Ordinal));
    }

    /// <summary>
    /// AC12, <c>deferred-work.md:36</c> - the drop zone's radius resolves to 3px (<c>rounded.md</c>), as the card and the
    /// column do; the lifted card takes the card's 3px, held by <c>Task_cards_and_columns_take_the_3px_radius</c>, and
    /// declares no radius of its own.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR8")]
    public void The_drop_zone_takes_the_3px_radius_and_the_lifted_card_keeps_the_cards()
    {
        var radii = CssCorpus.AllDeclarations()
            .Where(d => SelectsComponent(d.Rule.Selector, DropZoneClass))
            .Where(d => d.Declaration.Property.Contains("radius", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(radii);
        Assert.All(radii, d => Assert.All(CssCorpus.PixelLengths(CssCorpus.Resolve(d.Declaration.Value)), px => Assert.Equal(CardAndColumnRadiusPx, px, LengthTolerance)));
        Assert.DoesNotContain(DeclarationsOf(LiftedCardSelector), d => d.Property.Contains("radius", StringComparison.Ordinal));
    }

    /// <summary>
    /// <c>deferred-work.md:52</c>, the "cheap interim" gate it names - no rule makes an element that carries a colour token
    /// translucent: a rule declaring <c>opacity</c> below 1 (or <c>filter: opacity(…)</c>) fails when any rule styling the same
    /// element (its last compound's classes, or a subset of them) states a colour from a token. Otherwise the 36 computed
    /// contrast ratios would report opaque figures for a surface the eye receives faded. The lifted card and the drop zone
    /// stay fully opaque (story 2.7, open question 8 (a)). Story 7.5's presence, which DESIGN animates by opacity on a dot,
    /// widens this deliberately when it arrives.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR7")]
    public void No_rule_makes_an_element_carrying_a_colour_token_translucent()
    {
        var problems = TranslucentColourCarriers(CssCorpus.AllDeclarations().Select(d => (d.Sheet.Path, d.Rule.Selector, d.Declaration.Property, d.Declaration.Value, Line: CssCorpus.LineAt(d.Sheet.Raw, d.Declaration.Offset))));

        AssertNoProblems(problems, "A surface carrying a colour token is made translucent.", "Keep it opaque: the contrast harness computes opaque colours only (deferred-work.md:52).");
    }

    /// <summary>
    /// The detector behind <see cref="No_rule_makes_an_element_carrying_a_colour_token_translucent"/>, fired on planted rules:
    /// <c>opacity: 0.7</c> on the lifted card, whose colours come from the base card rule, is caught; so is <c>50%</c> and a
    /// <c>filter: opacity()</c>; <c>opacity: 1</c>, and opacity on an element no colour rule styles, are not.
    /// </summary>
    [Fact]
    public void The_translucency_scan_detects_a_planted_opacity()
    {
        static (string Path, string Selector, string Property, string Value, int Line) Planted(string selector, string property, string value) =>
            ("planted.css", selector, property, value, 1);

        var card = Planted(".task-card", "color", "var(--text-primary)");

        Assert.NotEmpty(TranslucentColourCarriers([card, Planted(LiftedCardSelector, "opacity", "0.7")]));
        Assert.NotEmpty(TranslucentColourCarriers([card, Planted(".task-card", "opacity", "50%")]));
        Assert.NotEmpty(TranslucentColourCarriers([card, Planted(LiftedCardSelector, "filter", "opacity(0.6)")]));
        Assert.Empty(TranslucentColourCarriers([card, Planted(LiftedCardSelector, "opacity", "1")]));
        Assert.Empty(TranslucentColourCarriers([card, Planted(".presence-dot", "opacity", "0.5")]));
    }

    /// <summary>
    /// Whether a declaration is the one sanctioned shadow (story 2.7): the <c>--shadow-lifted</c> token in the token layer,
    /// or <c>box-shadow: var(--shadow-lifted)</c> on exactly <c>.task-card.task-card--lifted</c>.
    /// </summary>
    private static bool IsTheLiftedCardsShadow(CssCorpus.Sheet sheet, CssCorpus.Rule rule, CssCorpus.Declaration declaration)
    {
        var inTokens = sheet.File.Name.Equals(CssCorpus.TokensFileName, StringComparison.OrdinalIgnoreCase);

        return inTokens
            ? declaration.Property.Equals(LiftedShadowToken, StringComparison.Ordinal)
            : rule.Selector.Trim() == LiftedCardSelector
                && declaration.Property.Equals("box-shadow", StringComparison.OrdinalIgnoreCase)
                && declaration.Value.Trim() == $"var({LiftedShadowToken})";
    }

    private static List<(string Property, string Value)> DeclarationsOf(string selector) =>
        [.. CssCorpus.AllDeclarations()
            .Where(d => d.Rule.Selector.Trim() == selector)
            .Select(d => (d.Declaration.Property, d.Declaration.Value.Trim()))];

    private static List<string> TranslucentColourCarriers(IEnumerable<(string Path, string Selector, string Property, string Value, int Line)> declarations)
    {
        var all = declarations.ToList();

        // Every element a colour token is stated on, by the classes of the last compound of each selector branch.
        var colourCarriers = all
            .Where(d => IsColourProperty(d.Property) && !CssCorpus.IsCustomPropertyName(d.Property) && d.Value.Contains("var(--", StringComparison.Ordinal))
            .SelectMany(d => SelectorBranches(d.Selector).Select(LastCompoundClasses))
            .Where(classes => classes.Count > 0)
            .ToList();

        return [.. from d in all
                   where IsTranslucent(d.Property, d.Value)
                   from branch in SelectorBranches(d.Selector)
                   let classes = LastCompoundClasses(branch)
                   where colourCarriers.Exists(carrier => carrier.IsSubsetOf(classes))
                   select string.Create(CultureInfo.InvariantCulture, $"{d.Path}:{d.Line} declares '{d.Property}: {d.Value}' on '{d.Selector}', which carries a colour token.")];
    }

    private static bool IsTranslucent(string property, string value) =>
        (property.Equals("opacity", StringComparison.OrdinalIgnoreCase) && OpacityBelowOne.IsMatch(value))
        || (property.Equals("filter", StringComparison.OrdinalIgnoreCase) && value.Contains("opacity(", StringComparison.OrdinalIgnoreCase));

    private static HashSet<string> LastCompoundClasses(string branch)
    {
        var last = branch.Trim().Split([' ', '>', '+', '~'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;

        return [.. last.Split(':')[0].Split('.', StringSplitOptions.RemoveEmptyEntries).Skip(last.StartsWith('.') ? 0 : 1)];
    }
}
