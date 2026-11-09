using System.Text.RegularExpressions;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// The Offer indicator and the offer dialog in the sheets: the indicator is the only accent element in the context bar,
/// takes the 2px chip radius and is never hidden at a breakpoint; the dialog takes the 6px dialog radius.
/// </summary>
/// <remarks>
/// Each gate is anchored on a rule that selects its component: <c>.offer-indicator</c> and the dialog's root,
/// <c>.ownership-offer-dialog</c>.
/// </remarks>
public sealed partial class DesignFoundationGateTests
{
    private const string OfferIndicatorClass = ".offer-indicator";

    private const string OwnershipOfferDialogClass = ".ownership-offer-dialog";

    /// <summary>
    /// Inside the context bar the accent colour belongs to the Offer indicator alone, because it is the only chrome element
    /// that is a proposition rather than a statement: no rule selecting into <c>.context-bar</c> (or the Role chip) references
    /// <c>--accent</c> or <c>--accent-on</c> unless it selects <c>.offer-indicator</c>. Anchor: the indicator itself takes
    /// the accent. Plant 11 (a second accent element in the bar) turns it red.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR9")]
    public void The_Offer_indicator_is_the_only_accent_element_in_the_context_bar()
    {
        var accented = CssCorpus.AllDeclarations()
            .Where(d => AccentReference.IsMatch(d.Declaration.Value))
            .ToList();

        Assert.Contains(accented, d => SelectsComponent(d.Rule.Selector, OfferIndicatorClass));

        var problems = accented
            .Where(d => (d.Rule.Selector.Contains("context-bar", StringComparison.Ordinal) || SelectsComponent(d.Rule.Selector, RoleChipClass))
                && !SelectsComponent(d.Rule.Selector, OfferIndicatorClass))
            .Select(d => $"{Where(d.Sheet, d.Declaration)} gives '{d.Rule.Selector}' '{d.Declaration.Property}: {d.Declaration.Value}'.")
            .ToList();

        AssertNoProblems(problems, "An element in the context bar other than the Offer indicator takes the accent.", "Keep the accent for .offer-indicator; chrome states facts in neutral tokens.");
    }

    /// <summary>
    /// The Offer indicator takes the 2px chip radius.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR9")]
    public void The_Offer_indicator_takes_the_2px_radius() => AssertRadius(OfferIndicatorClass, 2, "var(--radius-sm)");

    /// <summary>
    /// The offer dialog takes the 6px dialog radius.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR12")]
    public void The_ownership_offer_dialog_takes_the_6px_radius() => AssertRadius(OwnershipOfferDialogClass, 6, "var(--radius-lg)");

    /// <summary>
    /// The Offer indicator survives every breakpoint, as the Role chip does: no rule inside an at-rule hides it - neither
    /// <c>display: none</c>, nor <c>visibility: hidden</c>, nor the visually-hidden clip. The bar and its Space group around it
    /// are already covered by <see cref="The_Role_chip_is_never_hidden_at_any_breakpoint"/>. Anchor: a rule selects the
    /// indicator. Plant 12 (the indicator hidden by a media query) turns it red.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR41")]
    public void The_Offer_indicator_is_never_hidden_at_any_breakpoint()
    {
        var declarations = CssCorpus.AllDeclarations().ToList();

        Assert.Contains(declarations, d => SelectsComponent(d.Rule.Selector, OfferIndicatorClass));

        var problems = declarations
            .Where(d => d.Rule.AtRulePath.Length > 0)
            .Where(d => SelectsComponent(d.Rule.Selector, OfferIndicatorClass))
            .Where(d => Hides(d.Declaration))
            .Select(d => $"{Where(d.Sheet, d.Declaration)} sets '{d.Declaration.Property}: {d.Declaration.Value}' on '{d.Rule.Selector}' under {d.Rule.AtRulePath}.")
            .ToList();

        AssertNoProblems(problems, "A rule hides the Offer indicator at a breakpoint.", "Drop the Project name first, then the switcher's label; never the Offer indicator or the Role (UX-DR41).");
    }

    [GeneratedRegex(@"var\(\s*--accent(?:-on)?\s*[,)]", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex AccentReference { get; }
}
