using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.3's tightenings of the design gates for the first dialog-shaped surface: the Task detail panel
/// takes the 6px radius, a hairline border and no shadow, and the Status picker inside it takes 3px
/// (<c>DESIGN.md:238-244, :256-262</c>; <c>deferred-work.md:36</c>). AC13; UX-DR5, UX-DR10. Written by the story
/// 2.3 ATDD run.
/// </summary>
/// <remarks>
/// <b>The selectors are the DESIGN.md component names</b>: <c>.task-detail</c> and <c>.picker</c>. If Task 9
/// names them otherwise, correct <see cref="TaskDetailClass"/> and <see cref="PickerClass"/> and record it; the
/// gates scan every sheet, as the rest of this class does.
/// </remarks>
public sealed partial class DesignFoundationGateTests
{
    private const string TaskDetailClass = ".task-detail";

    private const string PickerClass = ".picker";

    private const double TaskDetailRadiusPx = 6;

    private const double PickerRadiusPx = 3;

    /// <summary>
    /// AC13, UX-DR5, <c>deferred-work.md:36</c> - every radius declared on the Task detail panel resolves to 6px
    /// (<c>rounded.lg</c>), and one is declared.
    /// </summary>
    [Fact(Skip = "red: AC13 - the Task detail panel has no styles yet (Task 9 CSS)")]
    [Trait("Requirement", "UX-DR5")]
    [Trait("Requirement", "UX-DR10")]
    public void The_Task_detail_panel_takes_the_6px_radius() =>
        AssertComponentRadius(TaskDetailClass, TaskDetailRadiusPx, "Use var(--radius-lg) on the Task detail panel.");

    /// <summary>
    /// AC13, UX-DR11, <c>DESIGN.md:256-262</c> - every radius declared on the Status picker resolves to 3px
    /// (<c>rounded.md</c>), and one is declared.
    /// </summary>
    [Fact(Skip = "red: AC13 - the Status picker has no styles yet (Task 9 CSS; Q11)")]
    [Trait("Requirement", "UX-DR5")]
    [Trait("Requirement", "UX-DR11")]
    public void The_Status_picker_takes_the_3px_radius() =>
        AssertComponentRadius(PickerClass, PickerRadiusPx, "Use var(--radius-md) on the picker.");

    /// <summary>
    /// AC13, UX-DR10, <c>DESIGN.md:238-244</c> - the panel states <c>box-shadow: none</c> explicitly and draws its
    /// edge with the hairline width token, as the card does: a dialog here is a surface, not a floating layer.
    /// </summary>
    [Fact(Skip = "red: AC13 - the Task detail panel has no styles yet (Task 9 CSS)")]
    [Trait("Requirement", "UX-DR10")]
    public void The_Task_detail_panel_declares_no_shadow_and_a_hairline_border()
    {
        var declarations = CssCorpus.AllDeclarations()
            .Where(d => SelectsComponent(d.Rule.Selector, TaskDetailClass))
            .ToList();

        var shadows = declarations.Where(d => d.Declaration.Property.Equals("box-shadow", StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.NotEmpty(shadows);
        Assert.All(shadows, d => Assert.True(IsNoneValue(d.Declaration.Value), $"{Where(d.Sheet, d.Declaration)} is '{d.Declaration.Value}'."));

        Assert.Contains(
            declarations,
            d => d.Declaration.Property.StartsWith("border", StringComparison.OrdinalIgnoreCase)
                && d.Declaration.Value.Contains("var(--border-hairline-width)", StringComparison.Ordinal));
    }

    private static void AssertComponentRadius(string component, double expectedPx, string remedy)
    {
        var radii = CssCorpus.AllDeclarations()
            .Where(d => SelectsComponent(d.Rule.Selector, component))
            .Where(d => d.Declaration.Property.Contains("radius", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(radii.Count > 0, $"No rule gives {component} a radius.");

        var problems = radii
            .SelectMany(d => CssCorpus.PixelLengths(CssCorpus.Resolve(d.Declaration.Value))
                .Where(px => Math.Abs(px - expectedPx) > LengthTolerance)
                .Select(px => $"{Where(d.Sheet, d.Declaration)} gives {component} a {px}px radius."))
            .ToList();

        AssertNoProblems(problems, $"{component} does not take the {expectedPx}px radius.", remedy);
    }
}
