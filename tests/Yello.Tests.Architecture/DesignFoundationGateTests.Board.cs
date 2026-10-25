using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.2's tightenings of the design gates, now that the Board builds the components they were waiting
/// for: the column count chip is the one and only pill (<c>deferred-work.md:32</c> - held by
/// <c>The_fully_round_radius_is_declared_once_and_used_by_exactly_one_component_the_column_count_chip</c>, into
/// which the ATDD run's separate case was merged), Task cards and columns take the 3px radius (<c>deferred-work.md:36</c>), and the Task card declares <c>box-shadow: none</c>
/// explicitly (DESIGN.md:431). AC5, AC6; UX-DR8. Written by the story 2.2 ATDD run.
/// </summary>
/// <remarks>
/// <b>The selectors are the DESIGN.md component names</b> - <c>.task-card</c>, <c>.column-count</c> - and
/// <c>.board-column</c> for DESIGN.md's <c>column</c>, which is too generic a class name to own. If Task 10
/// names them otherwise, correct <see cref="TaskCardClass"/>, <see cref="ColumnClass"/> and
/// <see cref="ColumnCountClass"/> and record it; the gates scan every sheet and every markup file, as the rest
/// of this class does.
/// </remarks>
public sealed partial class DesignFoundationGateTests
{
    private const string TaskCardClass = ".task-card";

    private const string ColumnClass = ".board-column";

    private const string ColumnCountClass = ".column-count";

    private const double CardAndColumnRadiusPx = 3;

    /// <summary>
    /// AC6, <c>deferred-work.md:36</c> - every radius declared on a Task card or a column resolves to 3px
    /// (<c>rounded.md</c>), and each declares one. The other mapped surfaces (dialogs, chips, the detail panel)
    /// stay with 2.3 and 2.4.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR8")]
    public void Task_cards_and_columns_take_the_3px_radius()
    {
        foreach (var component in new[] { TaskCardClass, ColumnClass })
        {
            var radii = CssCorpus.AllDeclarations()
                .Where(d => SelectsComponent(d.Rule.Selector, component))
                .Where(d => d.Declaration.Property.Contains("radius", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.True(radii.Count > 0, $"No rule gives {component} a radius.");

            var problems = radii
                .SelectMany(d => CssCorpus.PixelLengths(CssCorpus.Resolve(d.Declaration.Value))
                    .Where(px => Math.Abs(px - CardAndColumnRadiusPx) > LengthTolerance)
                    .Select(px => $"{Where(d.Sheet, d.Declaration)} gives {component} a {px}px radius."))
                .ToList();

            AssertNoProblems(problems, $"{component} does not take the 3px radius.", "Use var(--radius-md) on Task cards and columns.");
        }
    }

    /// <summary>
    /// AC6, DESIGN.md:421-431 - the Task card states <c>box-shadow: none</c> explicitly, "so nobody adds one
    /// back"; story 2.7's lifted card is the one sanctioned exception and widens this deliberately.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR8")]
    public void The_Task_card_declares_no_shadow_explicitly()
    {
        var shadows = CssCorpus.AllDeclarations()
            .Where(d => SelectsComponent(d.Rule.Selector, TaskCardClass))
            .Where(d => d.Declaration.Property.Equals("box-shadow", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(shadows);
        Assert.All(shadows, d => Assert.True(IsNoneValue(d.Declaration.Value), $"{Where(d.Sheet, d.Declaration)} is '{d.Declaration.Value}'."));
    }

    /// <summary>
    /// Whether a selector list has a branch whose last compound selects <paramref name="component"/> itself,
    /// not something inside it (<c>.task-card .title</c> is the title, not the card).
    /// </summary>
    private static bool SelectsComponent(string selector, string component) =>
        Array.Exists(SelectorBranches(selector), branch =>
        {
            var last = branch.Trim().Split([' ', '>', '+', '~'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;

            return last.Split(':')[0].Split('.').Skip(1).Any(@class => string.Equals("." + @class, component, StringComparison.Ordinal));
        });
}
