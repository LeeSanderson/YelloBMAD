using System.Text.RegularExpressions;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.5's design gates on the destructive confirm - every <c>.destructive-confirm</c>: the Task's in the panel, the
/// Project's in its row, and 2.4's Label confirm, which this story changes deliberately. AC4, AC13; UX-DR4, UX-DR5,
/// UX-DR12; <c>DESIGN.md:277-284, :360, :468, :476, :519</c>; <c>deferred-work.md:36</c>. Written by the story 2.5 ATDD
/// run.
/// </summary>
/// <remarks>
/// <para>
/// <b>The copy carries the danger; the border reinforces it</b> (AC4: accent and danger are 1.19 apart and converge
/// under deuteranopia). So the border is gated here, and the copy - "This cannot be undone." in every confirm, one
/// <c>.button-danger</c>, no icon - is gated in the browser (<c>DeleteTests</c>), where the rendered confirm exists.
/// </para>
/// <para>
/// <b>Two of the three run today.</b> No rule styling the confirm or the danger button declares a transition, an
/// animation or a shadow now, so DSN-502 and DSN-503 are standing gates from this run on; DSN-501 is red until Task 11
/// gives the confirm its border (today <c>components.css</c>'s rule is flex, wrap and gap only).
/// </para>
/// </remarks>
public sealed partial class DesignFoundationGateTests
{
    private const string DestructiveConfirmClass = ".destructive-confirm";

    private const string DangerButtonClass = ".button-danger";

    private static readonly string[] MotionProperties = ["transition", "transition-property", "transition-duration", "animation", "animation-name", "animation-duration"];

    private static readonly Regex DestructiveConfirmSelector = new(@"\.destructive-confirm(?![\w-])", RegexOptions.None, TimeSpan.FromSeconds(1));

    /// <summary>
    /// DSN-501 - AC13, UX-DR12, <c>DESIGN.md:476</c> - the confirm carries the <c>danger</c> border at the emphasis
    /// width: a rule selecting <c>.destructive-confirm</c> declares a solid border whose colour is <c>var(--danger)</c>
    /// and whose width is <c>var(--border-emphasis-width)</c> (2px), as a shorthand or as its longhands.
    /// </summary>
    [Fact(Skip = "red: AC13 - .destructive-confirm declares no danger border yet (story Task 11)")]
    [Trait("Requirement", "UX-DR12")]
    [Trait("Requirement", "UX-DR5")]
    public void The_destructive_confirm_carries_the_danger_border_at_the_emphasis_width()
    {
        var borders = CssCorpus.AllDeclarations()
            .Where(d => SelectsComponent(d.Rule.Selector, DestructiveConfirmClass))
            .Where(d => d.Declaration.Property.StartsWith("border", StringComparison.OrdinalIgnoreCase)
                && !d.Declaration.Property.Contains("radius", StringComparison.OrdinalIgnoreCase))
            .Select(d => d.Declaration.Value)
            .ToList();

        Assert.Contains(borders, value => value.Contains("var(--danger)", StringComparison.Ordinal));
        Assert.Contains(borders, value => value.Contains("var(--border-emphasis-width)", StringComparison.Ordinal));
        Assert.Contains(borders, value => value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("solid", StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// DSN-502 - AC13, UX-DR4, <c>DESIGN.md:519</c> - nothing destructive moves: no rule that styles the confirm or
    /// anything inside it, and no rule styling the danger button, declares a transition or an animation (other than
    /// <c>none</c>). Green today; a standing gate.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR4")]
    [Trait("Requirement", "UX-DR12")]
    public void Nothing_in_a_destructive_confirm_transitions_or_animates()
    {
        var declarations = ConfirmDeclarations();

        // Anchor: both the confirm and the danger button are styled, so "none declares motion" is about something.
        Assert.Contains(declarations, d => DestructiveConfirmSelector.IsMatch(d.Rule.Selector));
        Assert.Contains(declarations, d => SelectsComponent(d.Rule.Selector, DangerButtonClass));

        var problems = declarations
            .Where(d => MotionProperties.Contains(d.Declaration.Property, StringComparer.OrdinalIgnoreCase) && !IsNoneValue(d.Declaration.Value))
            .Select(d => $"{Where(d.Sheet, d.Declaration)} is '{d.Declaration.Property}: {d.Declaration.Value}'.")
            .ToList();

        AssertNoProblems(problems, "A destructive confirm moves.", "A destructive confirm appears and goes without motion (DESIGN.md:519).");
    }

    /// <summary>
    /// DSN-503 - AC13, UX-DR5 - the confirm and the danger button carry no shadow other than <c>none</c>: the confirm
    /// lives in the panel or a row and is not lifted off it. Green today; a standing gate.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR5")]
    [Trait("Requirement", "UX-DR12")]
    public void A_destructive_confirm_carries_no_shadow()
    {
        var declarations = ConfirmDeclarations();

        Assert.NotEmpty(declarations);

        var problems = declarations
            .Where(d => d.Declaration.Property.Equals("box-shadow", StringComparison.OrdinalIgnoreCase) && !IsNoneValue(d.Declaration.Value))
            .Select(d => $"{Where(d.Sheet, d.Declaration)} gives a destructive confirm a shadow.")
            .ToList();

        AssertNoProblems(problems, "A destructive confirm carries a shadow.", "Leave the confirm flat; the danger border is its emphasis.");
    }

    /// <summary>
    /// Every declaration in a rule whose selector names <c>.destructive-confirm</c> anywhere (the confirm or something
    /// inside it), or whose last compound is the danger button.
    /// </summary>
    private static List<(CssCorpus.Sheet Sheet, CssCorpus.Rule Rule, CssCorpus.Declaration Declaration)> ConfirmDeclarations() =>
        [
            .. CssCorpus.AllDeclarations()
                .Where(d => DestructiveConfirmSelector.IsMatch(d.Rule.Selector) || SelectsComponent(d.Rule.Selector, DangerButtonClass)),
        ];
}
