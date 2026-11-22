using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// The bulk move bar in the sheets: it is the only component bordered in the accent, and it takes the emphasis-width accent
/// border, the card surface, the 3px radius and the 9px padding.
/// </summary>
/// <remarks>
/// The accent is not a signal of danger or of a choice; the bar takes it because it is the only transient operation that runs
/// over many Tasks at once and must be unmistakable while it runs.
/// </remarks>
public sealed partial class DesignFoundationGateTests
{
    private const string BulkMoveBarClass = ".bulk-move-bar";

    /// <summary>
    /// The components whose hairline edge already takes the accent: the primary button, whose edge is its own fill, and the
    /// Offer indicator, the context bar's one proposition. Each is held to the hairline width, so neither reads as the bar.
    /// </summary>
    private static readonly string[] HairlineAccentEdges = [".button-primary", OfferIndicatorClass];

    /// <summary>
    /// No rule but one selecting <c>.bulk-move-bar</c> gives a border the accent colour, except the hairline edges of
    /// <see cref="HairlineAccentEdges"/>, which stay at the hairline width. Anchor: the corpus holds border declarations, so
    /// the scan reads them.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR17")]
    [Trait("Requirement", "UX-DR5")]
    public void No_component_but_the_bulk_move_bar_is_bordered_in_the_accent()
    {
        var borders = CssCorpus.AllDeclarations()
            .Where(d => d.Declaration.Property.StartsWith("border", StringComparison.OrdinalIgnoreCase)
                && !d.Declaration.Property.Contains("radius", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(borders);

        var problems = borders
            .Where(d => AccentReference.IsMatch(d.Declaration.Value) && !SelectsComponent(d.Rule.Selector, BulkMoveBarClass))
            .Where(d => !(Array.Exists(HairlineAccentEdges, edge => SelectsComponent(d.Rule.Selector, edge))
                && d.Declaration.Value.Contains("var(--border-hairline-width)", StringComparison.Ordinal)))
            .Select(d => $"{Where(d.Sheet, d.Declaration)} gives '{d.Rule.Selector}' '{d.Declaration.Property}: {d.Declaration.Value}'.")
            .ToList();

        AssertNoProblems(problems, "A component other than the bulk move bar is bordered in the accent.", "Keep the accent border for .bulk-move-bar; border other components in neutral tokens.");
    }

    /// <summary>
    /// The bar is bordered <c>var(--border-emphasis-width) solid var(--accent)</c>, sits on <c>var(--surface-card)</c>, pads by
    /// <c>var(--space-3)</c> and takes the 3px radius.
    /// </summary>
    [Fact(Skip = "red: AC10 - the .bulk-move-bar rule is not in the sheets yet")]
    [Trait("Requirement", "UX-DR17")]
    public void The_bulk_move_bar_takes_the_accent_emphasis_border_on_the_card_surface()
    {
        var declarations = CssCorpus.AllDeclarations()
            .Where(d => SelectsComponent(d.Rule.Selector, BulkMoveBarClass))
            .ToList();
        var borders = declarations
            .Where(d => d.Declaration.Property.StartsWith("border", StringComparison.OrdinalIgnoreCase)
                && !d.Declaration.Property.Contains("radius", StringComparison.OrdinalIgnoreCase))
            .Select(d => d.Declaration.Value)
            .ToList();

        Assert.Contains(borders, value => value.Contains("var(--accent)", StringComparison.Ordinal));
        Assert.Contains(borders, value => value.Contains("var(--border-emphasis-width)", StringComparison.Ordinal));
        Assert.Contains(borders, value => value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("solid", StringComparer.OrdinalIgnoreCase));
        Assert.Contains(declarations, d => d.Declaration.Property.StartsWith("background", StringComparison.OrdinalIgnoreCase) && d.Declaration.Value.Contains("var(--surface-card)", StringComparison.Ordinal));
        Assert.Contains(declarations, d => d.Declaration.Property.StartsWith("padding", StringComparison.OrdinalIgnoreCase) && d.Declaration.Value.Contains("var(--space-3)", StringComparison.Ordinal));
        AssertRadius(BulkMoveBarClass, 3, "var(--radius-md)");
    }
}
