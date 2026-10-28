using System.Text.RegularExpressions;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.6's design gates on the Move picker and the card's actions menu: the picker treatment, nothing revealed only
/// on hover, and the moved card's reflow on <c>motion.quick</c> alone. AC2, AC14, AC15; UX-DR6, UX-DR11, UX-DR21, UX-DR41;
/// <c>DESIGN.md:256-262, :473, :504-521</c>; <c>EXPERIENCE.md:283-295</c>. Written by the story 2.6 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Class names are the built ones</b>: <c>.move-picker</c> for the picker (Q5 (a), <c>MovePicker.razor</c>),
/// <c>.task-menu</c> for the card's context menu (<c>TaskMenu.razor</c>) and <c>.task-card__actions</c> for the card's
/// visible actions control (Q6 (a)). The browser cases (<c>MoveTests</c>) locate the same parts by role and name.
/// </para>
/// <para>
/// <b>DSN-602 runs from the ATDD run on</b>, as a standing gate over the whole corpus: no rule under <c>:hover</c> sets
/// <c>display</c>, <c>visibility</c> or <c>opacity</c>, so no control can be revealed by hover alone ("never hover-only",
/// UX-DR21). It was proved against a plant by the ATDD run (see the checklist). DSN-601 and DSN-603 hold the picker, the
/// menu and the moved card's reflow that story 2.6 built.
/// </para>
/// </remarks>
public sealed partial class DesignFoundationGateTests
{
    private const string MovePickerClass = ".move-picker";

    private const string TaskMenuClass = ".task-menu";

    private static readonly string[] RevealProperties = ["display", "visibility", "opacity"];

    private static readonly Regex HoverPseudoClass = new(@":hover(?![\w-])", RegexOptions.None, TimeSpan.FromSeconds(1));

    /// <summary>
    /// DSN-601 - AC14, UX-DR11, <c>DESIGN.md:256-262</c> - the Move picker and the card's menu take the picker treatment:
    /// each is styled by a rule declaring the <c>surface-card</c> background, a hairline border and <c>--radius-md</c>, and
    /// carries no shadow (other than <c>none</c>).
    /// </summary>
    /// <param name="component">The component class.</param>
    [Theory]
    [InlineData(MovePickerClass)]
    [InlineData(TaskMenuClass)]
    [Trait("Requirement", "UX-DR11")]
    [Trait("Requirement", "UX-DR21")]
    public void The_Move_picker_and_the_card_menu_take_the_picker_treatment(string component)
    {
        var declarations = CssCorpus.AllDeclarations()
            .Where(d => SelectsComponent(d.Rule.Selector, component))
            .Select(d => (d.Declaration.Property, d.Declaration.Value))
            .ToList();

        Assert.Contains(declarations, d => d.Property.StartsWith("background", StringComparison.OrdinalIgnoreCase) && d.Value.Contains("var(--surface-card)", StringComparison.Ordinal));
        Assert.Contains(declarations, d => d.Property.StartsWith("border", StringComparison.OrdinalIgnoreCase) && d.Value.Contains("var(--border-hairline)", StringComparison.Ordinal));
        Assert.Contains(declarations, d => d.Property.Equals("border-radius", StringComparison.OrdinalIgnoreCase) && d.Value.Contains("var(--radius-md)", StringComparison.Ordinal));
        Assert.DoesNotContain(declarations, d => d.Property.Equals("box-shadow", StringComparison.OrdinalIgnoreCase) && !IsNoneValue(d.Value));
    }

    /// <summary>
    /// DSN-602 - AC2, AC14, UX-DR21, UX-DR41 - nothing is revealed by hover alone: no rule whose selector carries
    /// <c>:hover</c> sets <c>display</c>, <c>visibility</c> or <c>opacity</c>, anywhere in the client's CSS. A pointer-only
    /// reveal would make the Move control or the actions control hover-only, and absent on touch and to the keyboard.
    /// <b>Green today</b>; a standing gate, proved against a plant.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR21")]
    [Trait("Requirement", "UX-DR41")]
    public void No_rule_reveals_or_hides_anything_on_hover_alone()
    {
        var rules = CssCorpus.AllRules().ToList();

        // Anchor: the corpus is read - the card is styled.
        Assert.Contains(rules, r => SelectsComponent(r.Rule.Selector, TaskCardClass));

        var problems = CssCorpus.AllDeclarations()
            .Where(d => HoverPseudoClass.IsMatch(d.Rule.Selector))
            .Where(d => RevealProperties.Contains(d.Declaration.Property, StringComparer.OrdinalIgnoreCase))
            .Select(d => $"{Where(d.Sheet, d.Declaration)} sets '{d.Declaration.Property}' under ':hover' ({d.Rule.Selector}).")
            .ToList();

        AssertNoProblems(problems, "A control is revealed or hidden on hover alone.", "Render the control always; hover may change its colour, never whether it is there (UX-DR21).");
    }

    /// <summary>
    /// DSN-603 - AC15, UX-DR6, <c>DESIGN.md:517-521</c> - the moved card's reflow runs on <c>motion.quick</c>: a rule
    /// styling the card declares a transition on <c>var(--motion-quick)</c>, and every transition or animation declared on
    /// the card, the picker or the menu uses only <c>--motion-*</c> tokens, never a literal duration (base.css's
    /// reduced-motion reset then removes it). Story 2.7 adds the lifted card's lift on <c>--motion-lift</c> and the dropped
    /// card's settle on <c>--motion-settle</c>, which this gate holds to the same rule; renamed from
    /// <c>Only_the_moved_cards_reflow_moves_and_on_the_quick_motion_token</c>, which stopped being true.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR6")]
    public void The_cards_motion_reflows_on_the_quick_token_and_states_no_literal_duration()
    {
        var motion = CssCorpus.AllDeclarations()
            .Where(d => SelectsComponent(d.Rule.Selector, TaskCardClass)
                || SelectsComponent(d.Rule.Selector, MovePickerClass)
                || SelectsComponent(d.Rule.Selector, TaskMenuClass))
            .Where(d => MotionProperties.Contains(d.Declaration.Property, StringComparer.OrdinalIgnoreCase) && !IsNoneValue(d.Declaration.Value))
            .ToList();

        Assert.Contains(motion, d => SelectsComponent(d.Rule.Selector, TaskCardClass) && d.Declaration.Value.Contains("var(--motion-quick)", StringComparison.Ordinal));

        var problems = motion
            .Where(d => Regex.IsMatch(d.Declaration.Value, @"\d+m?s(?![\w-])", RegexOptions.None, TimeSpan.FromSeconds(1)))
            .Select(d => $"{Where(d.Sheet, d.Declaration)} states a literal duration: '{d.Declaration.Property}: {d.Declaration.Value}'.")
            .ToList();

        AssertNoProblems(problems, "The move's motion uses a literal duration.", "Use the --motion-* tokens (DESIGN.md:504-521).");
    }
}
