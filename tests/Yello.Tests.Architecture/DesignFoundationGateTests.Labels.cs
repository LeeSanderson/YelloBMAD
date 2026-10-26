using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.4's tightenings of the design gates: the Label chip takes the 2px radius (<c>--radius-sm</c>, the token
/// declared for it), no fixed height and no shadow; the card's metadata row wraps before it ever scrolls; and every chip
/// takes its fill and ink from the palette tokens through a class, never a literal. AC5, AC13; UX-DR5, UX-DR8, UX-DR13,
/// UX-DR40, UX-DR41; <c>DESIGN.md:180-183, :451, :457-462</c>; <c>deferred-work.md:36</c>. Written by the story 2.4
/// ATDD run.
/// </summary>
/// <remarks>
/// <b>The selectors are the story's names</b> - <c>.label-chip</c>, <c>.label-chip--{key}</c> and 2.2's
/// <c>.task-card__meta</c>. If Task 10 names them otherwise, correct <see cref="LabelChipClass"/> and
/// <see cref="MetaRowClass"/> and record it.
/// </remarks>
public sealed partial class DesignFoundationGateTests
{
    private const string LabelChipClass = ".label-chip";

    private const string MetaRowClass = ".task-card__meta";

    private const double LabelChipRadiusPx = 2;

    /// <summary>
    /// The palette keys story 2.4 Q3 recommends; held equal to the Domain's and the token layer's by
    /// <c>LabelPaletteGateTests.The_token_palette_and_the_Domain_palette_are_the_same_set</c>.
    /// </summary>
    private static readonly string[] LabelPaletteKeys = ["rust", "ochre", "olive", "grass", "jade", "teal", "blue", "orchid"];

    private static readonly string[] FixedHeightProperties = ["height", "block-size", "max-height", "max-block-size"];

    /// <summary>
    /// AC13, UX-DR5, <c>deferred-work.md:36</c> - every radius declared on a Label chip resolves to 2px (<c>rounded.sm</c>),
    /// and one is declared: never the fully round radius, which belongs to the column count chip alone.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR5")]
    public void Label_chips_take_the_2px_radius() =>
        AssertComponentRadius(LabelChipClass, LabelChipRadiusPx, "Use var(--radius-sm) on the Label chip.");

    /// <summary>
    /// AC13, UX-DR40 ("chips size to content"), <c>DESIGN.md:451</c> - the Label chip states no fixed or maximum height
    /// (so text spacing and 200% text zoom grow it) and no shadow other than <c>none</c>. Anchored: a rule styles it.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR40")]
    [Trait("Requirement", "UX-DR5")]
    public void Label_chips_size_to_their_content_and_carry_no_shadow()
    {
        var declarations = CssCorpus.AllDeclarations()
            .Where(d => SelectsComponent(d.Rule.Selector, LabelChipClass))
            .ToList();

        Assert.NotEmpty(declarations);

        var problems = declarations
            .Where(d => FixedHeightProperties.Contains(d.Declaration.Property, StringComparer.OrdinalIgnoreCase) && CssCorpus.PixelLengths(CssCorpus.Resolve(d.Declaration.Value)).Any())
            .Select(d => $"{Where(d.Sheet, d.Declaration)} fixes the chip's height.")
            .Concat(declarations
                .Where(d => d.Declaration.Property.Equals("box-shadow", StringComparison.OrdinalIgnoreCase) && !IsNoneValue(d.Declaration.Value))
                .Select(d => $"{Where(d.Sheet, d.Declaration)} gives the chip a shadow."))
            .ToList();

        AssertNoProblems(problems, "The Label chip does not size to its content.", "Let padding and the meta type role size the chip.");
    }

    /// <summary>
    /// AC5, AC13, UX-DR41, WCAG 1.4.10 - the card's metadata row wraps (<c>flex-wrap: wrap</c>, or <c>flex-flow</c>
    /// carrying <c>wrap</c>) and nothing on it scrolls sideways or refuses to wrap (<c>overflow-x: auto | scroll</c>,
    /// <c>white-space: nowrap</c>): horizontal overflow inside a card at 320px is the 1.4.10 failure in the exact layout
    /// an audit uses.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR8")]
    [Trait("Requirement", "UX-DR41")]
    public void The_card_metadata_row_wraps_and_never_scrolls_sideways()
    {
        var declarations = CssCorpus.AllDeclarations()
            .Where(d => SelectsComponent(d.Rule.Selector, MetaRowClass))
            .ToList();

        Assert.Contains(
            declarations,
            d => (d.Declaration.Property.Equals("flex-wrap", StringComparison.OrdinalIgnoreCase) || d.Declaration.Property.Equals("flex-flow", StringComparison.OrdinalIgnoreCase))
                && d.Declaration.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("wrap", StringComparer.OrdinalIgnoreCase));

        var problems = declarations
            .Where(d => (d.Declaration.Property is "overflow-x" or "overflow" && d.Declaration.Value.Trim() is "auto" or "scroll")
                || (d.Declaration.Property is "white-space" or "flex-wrap" && d.Declaration.Value.Trim() is "nowrap"))
            .Select(d => $"{Where(d.Sheet, d.Declaration)} is '{d.Declaration.Property}: {d.Declaration.Value}'.")
            .ToList();

        AssertNoProblems(problems, "The card's metadata row can overflow sideways.", "Wrap the row; never scroll it or forbid wrapping.");
    }

    /// <summary>
    /// AC4, AC13, UX-DR7 ("nothing depends on colour"), Q3 - every palette key has a chip modifier class whose fill is
    /// exactly <c>var(--label-{key})</c>, and the chip's text is <c>var(--label-ink)</c>: the fill reaches the chip
    /// through a class, never an inline style, so <c>No_colour_is_stated_as_a_literal_outside_the_token_layer</c> keeps
    /// holding and the palette gate's figures are the rendered ones.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR13")]
    [Trait("Priority", "P1")]
    public void Every_palette_key_has_a_chip_class_filled_from_its_token_and_inked_from_the_ink_token()
    {
        var declarations = CssCorpus.AllDeclarations().ToList();
        var problems = new List<string>();

        foreach (var key in LabelPaletteKeys)
        {
            var fills = declarations
                .Where(d => SelectsComponent(d.Rule.Selector, $"{LabelChipClass}--{key}"))
                .Where(d => d.Declaration.Property is "background" or "background-color")
                .Select(d => d.Declaration.Value.Trim())
                .ToList();

            if (!fills.Contains($"var(--label-{key})", StringComparer.Ordinal))
            {
                problems.Add($"{LabelChipClass}--{key} does not take its fill from var(--label-{key}).");
            }
        }

        if (!declarations.Exists(d => SelectsComponent(d.Rule.Selector, LabelChipClass) && d.Declaration.Property == "color" && d.Declaration.Value.Trim() == "var(--label-ink)"))
        {
            problems.Add($"{LabelChipClass} does not take its text colour from var(--label-ink).");
        }

        AssertNoProblems(problems, "A Label chip is not drawn from the palette tokens.", "Give each key a modifier class over var(--label-{key}) and the chip var(--label-ink).");
    }
}
