using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.11's design gates: the breakpoints are stated in em at the documented widths only; below 768px every unselected
/// column is hidden by construction; the Role chip is never hidden at any breakpoint; the Status pager's tabs, the context
/// bar and the Role chip take their mapped radii (<c>deferred-work.md:36</c>); and no script scrolls by a physical side but
/// the one call story 2.11 proved under RTL. AC6, AC7, AC8, AC11, AC16; UX-DR20, UX-DR41, UX-DR42.
/// </summary>
public sealed partial class DesignFoundationGateTests
{
    private const string PagerTabClass = ".status-pager__tab";

    private const string ContextBarClass = ".context-bar";

    private const string RoleChipClass = ".role-chip";

    private const string NarrowQuery = "max-width: 47.9375em";

    /// <summary>
    /// The width conditions the product breaks at, in em against the 16px initial size: 480px (2.10's reflow), 768px and
    /// 1280px (EXPERIENCE.md:409-421), each as its min-width value and its max-width value one sixteenth of a pixel below.
    /// </summary>
    private static readonly HashSet<string> DocumentedBreakpoints = new(StringComparer.Ordinal)
    {
        "30em", "29.9375em", "48em", "47.9375em", "80em", "79.9375em",
    };

    /// <summary>
    /// Story 2.11, open question 11 (a) - every <c>@media</c> width condition in every sheet is in em and is one of the
    /// documented breakpoints. A px breakpoint would not move with a text-size override, and an undocumented one is a layout
    /// nobody specified.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR41")]
    public void Breakpoints_are_stated_in_em_at_the_documented_widths_only()
    {
        var conditions = MediaWidthConditions().ToList();

        Assert.Contains(conditions, condition => condition.Value == "47.9375em");

        var problems = conditions
            .Where(condition => !DocumentedBreakpoints.Contains(condition.Value))
            .Select(condition => $"{condition.Where} breaks at '{condition.Value}'.")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        AssertNoProblems(problems, "A breakpoint is not one of the documented widths in em.", "Break at 30em, 48em or 80em (or 0.0625px below each, as 29.9375em, 47.9375em, 79.9375em).");
    }

    /// <summary>
    /// The detector behind <see cref="Breakpoints_are_stated_in_em_at_the_documented_widths_only"/>, fired on planted text.
    /// </summary>
    [Fact]
    public void The_breakpoint_scan_detects_a_plant()
    {
        Assert.Equal(["767px"], [.. WidthValues("@media (max-width: 767px)")]);
        Assert.Equal(["48em", "79.9375em"], [.. WidthValues("@media (min-width: 48em) and (max-width: 79.9375em)")]);
        Assert.Empty(WidthValues("@media (prefers-reduced-motion: reduce)"));
        Assert.DoesNotContain("767px", DocumentedBreakpoints);
    }

    /// <summary>
    /// Story 2.11, AC7, AC11, open question 1 (a) - below 768px a rule hides every column the Status pager has not selected,
    /// so the first paint at 320px is one column before .NET knows the width. Hiding is <c>display: none</c>, which takes the
    /// column out of the accessibility tree too.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR20")]
    public void Below_768px_an_unselected_column_is_hidden_by_construction()
    {
        var hiding = CssCorpus.AllDeclarations()
            .Where(d => d.Rule.AtRulePath.Contains(NarrowQuery, StringComparison.Ordinal))
            .Where(d => d.Rule.Selector.Contains(".board-column:not([data-pager-selected])", StringComparison.Ordinal))
            .Where(d => d.Declaration.Property.Equals("display", StringComparison.OrdinalIgnoreCase) && d.Declaration.Value.Trim() == "none")
            .ToList();

        Assert.True(hiding.Count > 0, $"No rule under @media ({NarrowQuery}) hides .board-column:not([data-pager-selected]) with display: none.");
    }

    /// <summary>
    /// Story 2.11, AC6, AC16, UX-DR9 - the Role chip survives every breakpoint: no rule inside an at-rule hides it, or hides
    /// the context bar or its Space group around it - neither <c>display: none</c>, nor <c>visibility: hidden</c>, nor the
    /// visually-hidden clip.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR41")]
    public void The_Role_chip_is_never_hidden_at_any_breakpoint()
    {
        string[] containers = [RoleChipClass, ContextBarClass, ".context-bar__space"];

        var problems = CssCorpus.AllDeclarations()
            .Where(d => d.Rule.AtRulePath.Length > 0)
            .Where(d => Array.Exists(containers, container => SelectsComponent(d.Rule.Selector, container)))
            .Where(d => Hides(d.Declaration))
            .Select(d => $"{Where(d.Sheet, d.Declaration)} sets '{d.Declaration.Property}: {d.Declaration.Value}' on '{d.Rule.Selector}' under {d.Rule.AtRulePath}.")
            .ToList();

        AssertNoProblems(problems, "A rule hides the Role chip, or the bar holding it, at a breakpoint.", "Drop the Project name first, then the switcher's label; never the Role (UX-DR41).");
    }

    /// <summary>
    /// The detector behind <see cref="The_Role_chip_is_never_hidden_at_any_breakpoint"/>, fired on planted declarations.
    /// </summary>
    [Fact]
    public void The_hiding_scan_detects_a_plant()
    {
        Assert.True(Hides(new CssCorpus.Declaration("display", "none", IsImportant: false, Offset: 0)));
        Assert.True(Hides(new CssCorpus.Declaration("visibility", "hidden", IsImportant: false, Offset: 0)));
        Assert.True(Hides(new CssCorpus.Declaration("clip-path", "inset(50%)", IsImportant: false, Offset: 0)));
        Assert.False(Hides(new CssCorpus.Declaration("display", "inline-flex", IsImportant: false, Offset: 0)));
        Assert.True(SelectsComponent(".context-bar .role-chip", RoleChipClass));
    }

    /// <summary>
    /// Story 2.11, open question 12 (a), <c>deferred-work.md:36</c> - the Status pager's tabs take the 2px chip radius.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR20")]
    public void The_Status_pager_tabs_take_the_2px_radius() => AssertRadius(PagerTabClass, 2, "var(--radius-sm)");

    /// <summary>
    /// Story 2.11, <c>deferred-work.md:36</c> - the context bar takes the 3px radius (DESIGN.md:162-168).
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR9")]
    public void The_context_bar_takes_the_3px_radius() => AssertRadius(ContextBarClass, 3, "var(--radius-md)");

    /// <summary>
    /// Story 2.11, <c>deferred-work.md:36</c> - the Role chip takes the 2px chip radius (DESIGN.md:169-174).
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR9")]
    public void The_Role_chip_takes_the_2px_radius() => AssertRadius(RoleChipClass, 2, "var(--radius-sm)");

    /// <summary>
    /// Story 2.11, AC8, AC13, open question 11 (a) - no script scrolls by a physical side (<c>left</c> or <c>right</c> in a
    /// <c>scrollBy</c>, <c>scrollTo</c> or <c>scrollIntoView</c> call), except the drag's one sideways auto-scroll, which
    /// pairs a physical <c>clientX</c> with a physical <c>scrollBy({ left })</c> and which <c>SmallViewportTests</c> proves under
    /// <c>dir="rtl"</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR42")]
    public void No_script_scrolls_by_a_physical_side_except_the_proved_one()
    {
        var scripts = Directory.EnumerateFiles(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client", "wwwroot"), "*.js", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .ToList();

        Assert.Contains(scripts, script => script.Name == "board-drag.js");

        var calls = scripts
            .SelectMany(script => PhysicalScrolls(File.ReadAllText(script.FullName)).Select(call => (Script: script.Name, Call: call)))
            .ToList();

        var problems = calls
            .Where(found => !(found.Script == "board-drag.js" && found.Call.StartsWith("scrollBy({ left: sideways", StringComparison.Ordinal)))
            .Select(found => $"{found.Script} calls {found.Call}")
            .ToList();

        AssertNoProblems(problems, "A script scrolls by a physical side.", "Scroll by the logical axis (block/inline), or prove the call under dir=\"rtl\" and allow it here.");
        Assert.Single(calls);
    }

    /// <summary>
    /// The detector behind <see cref="No_script_scrolls_by_a_physical_side_except_the_proved_one"/>, fired on planted text.
    /// </summary>
    [Fact]
    public void The_physical_scroll_scan_detects_a_plant()
    {
        Assert.Single(PhysicalScrolls("element.scrollTo({ right: 0 });"));
        Assert.Single(PhysicalScrolls("this.#root.scrollBy({ left: sideways, behavior: 'instant' });"));
        Assert.Empty(PhysicalScrolls("column.element.scrollBy({ top: step, behavior: 'instant' });"));
        Assert.Empty(PhysicalScrolls("zone.scrollIntoView({ block: 'nearest', inline: 'nearest' });"));
    }

    private static void AssertRadius(string component, double expectedPx, string remedy)
    {
        var radii = CssCorpus.AllDeclarations()
            .Where(d => SelectsComponent(d.Rule.Selector, component))
            .Where(d => d.Declaration.Property.Contains("radius", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(radii.Count > 0, $"No rule gives {component} a radius.");

        var problems = radii
            .SelectMany(d => CssCorpus.PixelLengths(CssCorpus.Resolve(d.Declaration.Value))
                .Where(px => Math.Abs(px - expectedPx) > LengthTolerance)
                .Select(px => $"{Where(d.Sheet, d.Declaration)} gives {component} a {px.ToString(CultureInfo.InvariantCulture)}px radius."))
            .ToList();

        AssertNoProblems(problems, $"{component} does not take the {expectedPx.ToString(CultureInfo.InvariantCulture)}px radius.", $"Use {remedy} on {component}.");
    }

    private static bool Hides(CssCorpus.Declaration declaration)
    {
        var property = declaration.Property.Trim().ToUpperInvariant();
        var value = declaration.Value.Trim().ToUpperInvariant();

        if (property is "CLIP-PATH" or "CLIP")
        {
            return true;
        }

        return (property == "DISPLAY" && value == "NONE") || (property == "VISIBILITY" && value is "HIDDEN" or "COLLAPSE");
    }

    private static IEnumerable<(string Where, string Value)> MediaWidthConditions() =>
        CssCorpus.AllRules()
            .Where(pair => pair.Rule.AtRulePath.Contains("@media", StringComparison.OrdinalIgnoreCase))
            .SelectMany(pair => WidthValues(pair.Rule.AtRulePath).Select(value => ($"{pair.Sheet.Path}:{CssCorpus.LineAt(pair.Sheet.Raw, pair.Rule.Offset)}", value)));

    private static IEnumerable<string> WidthValues(string atRules) =>
        WidthCondition.Matches(atRules).Select(match => match.Groups["value"].Value);

    private static IEnumerable<string> PhysicalScrolls(string script) =>
        PhysicalScroll.Matches(script).Select(match => match.Value);

    [GeneratedRegex(@"\((?:min-|max-)?width\s*:\s*(?<value>[^)\s]+)\s*\)", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 5000)]
    private static partial Regex WidthCondition { get; }

    [GeneratedRegex(@"scroll(?:By|To|IntoView)\(\{[^}]*\b(?:left|right)\s*:[^}]*\}\)", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex PhysicalScroll { get; }
}
