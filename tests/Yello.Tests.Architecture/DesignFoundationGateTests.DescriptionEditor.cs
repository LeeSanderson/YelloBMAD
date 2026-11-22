using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// The description editor as <c>DESIGN.md</c> specifies it: on the page ground, not the card ground, in the primary text
/// colour, with a hairline border, the 3px radius, the third spacing step as padding and the body type.
/// </summary>
/// <remarks>
/// The selector is the DESIGN.md component name, <c>.description-editor</c>. If the component names it otherwise, correct
/// <see cref="DescriptionEditorClass"/> and record it; the gates scan every sheet.
/// </remarks>
public sealed partial class DesignFoundationGateTests
{
    private const string DescriptionEditorClass = ".description-editor";

    private const double DescriptionEditorRadiusPx = 3;

    /// <summary>
    /// Every radius declared on the editor resolves to 3px (<c>rounded.md</c>), and one is declared.
    /// </summary>
    [Fact(Skip = "red: AC7 - no rule styles .description-editor")]
    [Trait("Requirement", "UX-DR5")]
    [Trait("Requirement", "UX-DR10")]
    public void The_description_editor_takes_the_3px_radius() =>
        AssertComponentRadius(DescriptionEditorClass, DescriptionEditorRadiusPx, "Use var(--radius-md) on the description editor.");

    /// <summary>
    /// The editor sits on the page ground in the primary text colour, draws a hairline border, pads by the third spacing
    /// step and sets the body type size.
    /// </summary>
    /// <param name="property">The property, or its prefix for the border.</param>
    /// <param name="token">The token its value names.</param>
    [Theory(Skip = "red: AC7 - no rule styles .description-editor")]
    [InlineData("background", "var(--surface-page)")]
    [InlineData("color", "var(--text-primary)")]
    [InlineData("border", "var(--border-hairline-width)")]
    [InlineData("padding", "var(--space-3)")]
    [InlineData("font-size", "var(--type-body-size)")]
    [Trait("Requirement", "UX-DR10")]
    public void The_description_editor_is_on_the_page_ground_with_a_hairline_border_and_the_body_type(string property, string token)
    {
        var declarations = CssCorpus.AllDeclarations()
            .Where(d => SelectsComponent(d.Rule.Selector, DescriptionEditorClass))
            .ToList();

        Assert.Contains(
            declarations,
            d => d.Declaration.Property.StartsWith(property, StringComparison.OrdinalIgnoreCase)
                && d.Declaration.Value.Contains(token, StringComparison.Ordinal));
        Assert.DoesNotContain(
            declarations,
            d => d.Declaration.Property.StartsWith("background", StringComparison.OrdinalIgnoreCase)
                && d.Declaration.Value.Contains("var(--surface-card)", StringComparison.Ordinal));
    }
}
